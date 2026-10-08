using System.Linq.Expressions;
using Gorm.Application.Querying.Models;
using Gorm.Application.Querying.Translation;
using Gorm.Core.Metadata;
using Gorm.Core.Models;
using Gorm.Core.Primitives;

namespace Gorm.Application.Context;

/// <summary>
/// Represents graph context relationship mutation extensions.
/// </summary>
public static class GraphContextRelationshipMutationExtensions
{
    /// <summary>
    /// Adds the item.
    /// </summary>
    /// <typeparam name="TNode">The type of t node.</typeparam>
    /// <typeparam name="TRelated">The type of t related.</typeparam>
    /// <param name="context">The graph context.</param>
    /// <param name="owner">The owner node.</param>
    /// <param name="navigationExpression">The navigation expression.</param>
    /// <param name="related">The related node.</param>
    public static void AddRelationship<TNode, TRelated>(
        this GraphContext context,
        TNode owner,
        Expression<Func<TNode, IEnumerable<TRelated>>> navigationExpression,
        TRelated related)
        where TNode : Node
        where TRelated : Node
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(navigationExpression);
        ArgumentNullException.ThrowIfNull(related);

        var mutation = ResolveMutation(context, owner, navigationExpression, related);
        var edge = GraphRelationshipRuntimeCache.CreateEdge(mutation.Relationship.EdgeType);

        context.AddEdge(mutation.FromNode, mutation.ToNode, edge);

        ApplyAddFixup(context, mutation, edge);
    }

    /// <summary>
    /// Removes the item.
    /// </summary>
    /// <typeparam name="TNode">The type of t node.</typeparam>
    /// <typeparam name="TRelated">The type of t related.</typeparam>
    /// <param name="context">The graph context.</param>
    /// <param name="owner">The owner node.</param>
    /// <param name="navigationExpression">The navigation expression.</param>
    /// <param name="related">The related node.</param>
    public static void RemoveRelationship<TNode, TRelated>(
        this GraphContext context,
        TNode owner,
        Expression<Func<TNode, IEnumerable<TRelated>>> navigationExpression,
        TRelated related)
        where TNode : Node
        where TRelated : Node
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(navigationExpression);
        ArgumentNullException.ThrowIfNull(related);

        var mutation = ResolveMutation(context, owner, navigationExpression, related);

        context.ChangeTracker.AddEdgeDisconnection(new Tracking.PendingEdgeDisconnection
        {
            EdgeType = mutation.Relationship.EdgeType,
            FromNode = mutation.FromNode,
            ToNode = mutation.ToNode
        });

        ApplyRemoveFixup(context, mutation);
    }

    private static RelationshipMutation ResolveMutation<TNode, TRelated>(
        GraphContext context,
        TNode owner,
        Expression<Func<TNode, IEnumerable<TRelated>>> navigationExpression,
        TRelated related)
        where TNode : Node
        where TRelated : Node
    {
        var navigationName = GraphNavigationExpressionHelper.GetPropertyName(navigationExpression);
        var navigation = context.Model.GetNavigation(typeof(TNode), navigationName);
        var relationship = context.Model.GetRelationship<TNode>(navigation.RelationshipName);

        if (navigation.Kind != GraphNavigationKind.Collection)
        {
            throw new InvalidOperationException(
                $"Navigation '{navigation.PropertyName}' on '{typeof(TNode).FullName}' is not a collection navigation.");
        }

        if (!relationship.RelatedNodeType.IsAssignableFrom(typeof(TRelated)))
        {
            throw new InvalidOperationException(
                $"Navigation '{navigation.PropertyName}' expects related node type '{relationship.RelatedNodeType.FullName}', but got '{typeof(TRelated).FullName}'.");
        }

        Node fromNode = relationship.Direction == GraphTraversalDirection.Outgoing ? owner : related;

        Node toNode = relationship.Direction == GraphTraversalDirection.Outgoing ? related : owner;

        return new RelationshipMutation
        {
            Navigation = navigation,
            Relationship = relationship,
            Owner = owner,
            Related = related,
            FromNode = fromNode,
            ToNode = toNode
        };
    }

    private static void ApplyAddFixup(GraphContext context, RelationshipMutation mutation, Edge edge)
    {
        context.RelationshipFixupStore.Add(mutation.Owner, mutation.Relationship.Name, includeEdge: false, mutation.Related);

        context.ApplyNavigationFixup(mutation.Owner, mutation.Relationship.Name, GetRelatedAsObjects(context, mutation.Owner, mutation.Relationship.Name));

        var forwardEdgeResult = GraphRelationshipRuntimeCache.CreateEdgeResult(mutation.Relationship.EdgeType, mutation.Related.GetType(), edge, mutation.Related);

        context.RelationshipFixupStore.Add(mutation.Owner, mutation.Relationship.Name, includeEdge: true, forwardEdgeResult);

        var inverseRelationships = GraphRelationshipRuntimeCache.GetInverseRelationships(
            context.Model,
            mutation.Relationship,
            mutation.Related.GetType(),
            mutation.Owner.GetType());

        for (var i = 0; i < inverseRelationships.Length; i++)
        {
            var inverseRelationshipName = inverseRelationships[i].Name;

            context.RelationshipFixupStore.Add(mutation.Related, inverseRelationshipName, includeEdge: false, mutation.Owner);

            context.ApplyNavigationFixup(mutation.Related, inverseRelationshipName, GetRelatedAsObjects(context, mutation.Related, inverseRelationshipName));

            var inverseEdgeResult = GraphRelationshipRuntimeCache.CreateEdgeResult(mutation.Relationship.EdgeType, mutation.Owner.GetType(), edge, mutation.Owner);

            context.RelationshipFixupStore.Add(mutation.Related, inverseRelationshipName, includeEdge: true, inverseEdgeResult);
        }
    }

    private static void ApplyRemoveFixup(GraphContext context, RelationshipMutation mutation)
    {
        RemoveRelationshipAndApplyNavigationFixup(context, mutation.Owner, mutation.Relationship.Name, mutation.Related);

        RemoveMatchingEdgeResult(context, mutation.Owner, mutation.Relationship.Name, mutation.Related.Id);

        var inverseRelationships = GraphRelationshipRuntimeCache.GetInverseRelationships(
            context.Model,
            mutation.Relationship,
            mutation.Related.GetType(),
            mutation.Owner.GetType());

        for (var i = 0; i < inverseRelationships.Length; i++)
        {
            var inverseRelationshipName = inverseRelationships[i].Name;

            RemoveRelationshipAndApplyNavigationFixup(context, mutation.Related, inverseRelationshipName, mutation.Owner);

            RemoveMatchingEdgeResult(context, mutation.Related, inverseRelationshipName, mutation.Owner.Id);
        }
    }

    private static void RemoveRelationshipAndApplyNavigationFixup(GraphContext context, Node owner, string relationshipName, Node related)
    {
        context.RelationshipFixupStore.Remove(owner, relationshipName, includeEdge: false, related);

        context.ApplyNavigationFixup(owner, relationshipName, GetRelatedAsObjects(context, owner, relationshipName));
    }

    private static void RemoveMatchingEdgeResult(GraphContext context, Node owner, string relationshipName, Guid relatedNodeId)
    {
        if (!context.RelationshipFixupStore.TryGetRelatedWithEdges(owner, relationshipName, out var edgeResults) ||
            edgeResults is null)
        {
            return;
        }

        for (var i = 0; i < edgeResults.Count; i++)
        {
            var edgeResult = edgeResults[i];
            var node = GraphRelationshipRuntimeCache.ExtractNodeFromEdgeResult(edgeResult);

            if (node is null || node.Id != relatedNodeId)
            {
                continue;
            }

            context.RelationshipFixupStore.Remove(owner, relationshipName, includeEdge: true, edgeResult);
        }
    }

    private static object[] GetRelatedAsObjects(GraphContext context, Node owner, string relationshipName)
    {
        var related = context.GetRelated<Node>(owner, relationshipName);

        if (related.Count == 0)
        {
            return [];
        }

        var result = new object[related.Count];

        for (var i = 0; i < related.Count; i++)
        {
            result[i] = related[i];
        }

        return result;
    }

    /// <summary>
    /// Represents relationship mutation.
    /// </summary>
    private sealed class RelationshipMutation
    {
        /// <summary>
        /// Gets or sets the navigation.
        /// </summary>
        public required GraphNavigationMapping Navigation { get; init; }
        /// <summary>
        /// Gets or sets the relationship.
        /// </summary>
        public required GraphRelationshipMapping Relationship { get; init; }
        /// <summary>
        /// Gets or sets the owner.
        /// </summary>
        public required Node Owner { get; init; }
        /// <summary>
        /// Gets or sets the related.
        /// </summary>
        public required Node Related { get; init; }
        /// <summary>
        /// Gets or sets the from node.
        /// </summary>
        public required Node FromNode { get; init; }
        /// <summary>
        /// Gets or sets the to node.
        /// </summary>
        public required Node ToNode { get; init; }
    }
}