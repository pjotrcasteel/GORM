using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Gorm.Application.Execution.InMemory;
using Gorm.Application.Querying.Models;
using Gorm.Application.Querying.Translation;
using Gorm.Core.Loading;
using Gorm.Core.Metadata;
using Gorm.Core.Models;
using Gorm.Core.Primitives;

namespace Gorm.Application.Context.Extensions;

/// <summary>
/// Represents graph context relationship loading extensions.
/// </summary>
public static class GraphContextRelationshipLoadingExtensions
{
    /// <summary>
    /// Loads the specified relationship.
    /// </summary>
    /// <param name="context">The graph context.</param>
    /// <param name="owner">The owner node.</param>
    /// <param name="relationshipName">The relationship name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public static Task<IReadOnlyList<object>> LoadRelationshipAsync(this GraphContext context, Node owner, string relationshipName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(owner);

        var request = new GraphIncludeRequest
        {
            Name = relationshipName,
            NameKind = GraphIncludeNameKind.Relationship,
            IncludeEdge = false
        };

        return LoadRelationshipAsync(context, owner, request, cancellationToken);
    }

    /// <summary>
    /// Loads the specified relationship.
    /// </summary>
    /// <typeparam name="TNode">The type of t node.</typeparam>
    /// <typeparam name="TRelated">The type of t related.</typeparam>
    /// <param name="context">The graph context.</param>
    /// <param name="owner">The owner node.</param>
    /// <param name="navigationExpression">The navigation expression.</param>
    /// <param name="configure">The include configuration.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public static Task<IReadOnlyList<object>> LoadRelationshipAsync<TNode, TRelated>(
        this GraphContext context,
        TNode owner,
        Expression<Func<TNode, IEnumerable<TRelated>>> navigationExpression,
        Func<GraphRelationshipIncludeBuilder<TRelated>, GraphRelationshipIncludeBuilder<TRelated>>? configure = null,
        CancellationToken cancellationToken = default)
        where TNode : Node
        where TRelated : Node
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(navigationExpression);

        var propertyName = GraphNavigationExpressionHelper.GetPropertyName(navigationExpression);

        var builder = new GraphRelationshipIncludeBuilder<TRelated>(propertyName, GraphIncludeNameKind.Navigation, includeEdge: false);

        var request = (configure is null ? builder : configure(builder)).Build();

        return LoadRelationshipAsync(context, owner, request, cancellationToken);
    }

    /// <summary>
    /// Loads the specified relationship.
    /// </summary>
    /// <param name="context">The graph context.</param>
    /// <param name="owner">The owner node.</param>
    /// <param name="request">The include request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public static Task<IReadOnlyList<object>> LoadRelationshipAsync(
        this GraphContext context,
        Node owner,
        GraphIncludeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(request);

        var relationship = ResolveRelationship(context, owner, request);
        ValidateRequestAgainstRelationship(request, relationship);
        return LoadRelationshipCoreAsync(context, owner, request, relationship, cancellationToken);
    }

    private static async Task<IReadOnlyList<object>> LoadRelationshipCoreAsync(
        GraphContext context,
        Node owner,
        GraphIncludeRequest request,
        GraphRelationshipMapping relationship,
        CancellationToken cancellationToken)
    {
        var rawItems = await LoadRawRelationshipItemsAsync(context, owner, relationship, request, cancellationToken);

        var finalItems = ApplyPostProcessing(rawItems, request);

        if (!request.IncludeEdge && rawItems.Count > 0 && rawItems[0] is not Node)
        {
            finalItems = ExtractRelatedNodes(finalItems);
        }

        RegisterFixup(context, owner, relationship, finalItems, request.IncludeEdge);

        return finalItems;
    }

    /// <summary>
    /// Loads the specified relationship.
    /// </summary>
    /// <param name="context">The graph context.</param>
    /// <param name="owner">The owner node.</param>
    /// <param name="relationshipName">The relationship name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public static Task<IReadOnlyList<object>> LoadRelationshipWithEdgesAsync(
        this GraphContext context,
        Node owner,
        string relationshipName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(owner);

        var request = new GraphIncludeRequest
        {
            Name = relationshipName,
            NameKind = GraphIncludeNameKind.Relationship,
            IncludeEdge = true
        };

        return LoadRelationshipAsync(context, owner, request, cancellationToken);
    }

    /// <summary>
    /// Loads the specified relationship.
    /// </summary>
    /// <typeparam name="TNode">The type of t node.</typeparam>
    /// <typeparam name="TRelated">The type of t related.</typeparam>
    /// <param name="context">The graph context.</param>
    /// <param name="owner">The owner node.</param>
    /// <param name="navigationExpression">The navigation expression.</param>
    /// <param name="configure">The include configuration.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public static Task<IReadOnlyList<object>> LoadRelationshipWithEdgesAsync<TNode, TRelated>(
        this GraphContext context,
        TNode owner,
        Expression<Func<TNode, IEnumerable<TRelated>>> navigationExpression,
        Func<GraphRelationshipIncludeBuilder<TRelated>, GraphRelationshipIncludeBuilder<TRelated>>? configure = null,
        CancellationToken cancellationToken = default)
        where TNode : Node
        where TRelated : Node
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(navigationExpression);

        var propertyName = GraphNavigationExpressionHelper.GetPropertyName(navigationExpression);

        var builder = new GraphRelationshipIncludeBuilder<TRelated>(propertyName, GraphIncludeNameKind.Navigation, includeEdge: true);

        var request = (configure is null ? builder : configure(builder)).Build();

        return LoadRelationshipAsync(context, owner, request, cancellationToken);
    }

    private static GraphRelationshipMapping ResolveRelationship(GraphContext context, Node owner, GraphIncludeRequest request)
    {
        if (request.NameKind == GraphIncludeNameKind.Relationship)
        {
            return context.Model.GetRelationship(owner.GetType(), request.Name);
        }

        var navigation = context.Model.GetNavigation(owner.GetType(), request.Name);
        return context.Model.GetRelationship(owner.GetType(), navigation.RelationshipName);
    }

    private static void ValidateRequestAgainstRelationship(GraphIncludeRequest request, GraphRelationshipMapping relationship)
    {
        ValidateEdgeLambda(request.EdgePredicate, relationship.EdgeType, "Edge predicate");
        ValidateEdgeLambda(request.EdgeOrderBy, relationship.EdgeType, "Edge order");

        if (request.Skip is < 0)
        {
            throw new InvalidOperationException("Skip count cannot be negative.");
        }

        if (request.Take is < 0)
        {
            throw new InvalidOperationException("Take count cannot be negative.");
        }
    }

    private static async Task<IReadOnlyList<object>> LoadRawRelationshipItemsAsync(
        GraphContext context,
        Node owner,
        GraphRelationshipMapping relationship,
        GraphIncludeRequest request,
        CancellationToken cancellationToken)
    {
        if (context.ExecutionEngine is InMemoryGraphExecutionEngine inMemoryExecutionEngine)
        {
            return LoadRawRelationshipItemsInMemory(owner, relationship, request, inMemoryExecutionEngine.Store);
        }

        var loadWithEdges = ShouldLoadWithEdges(request);
        var invoker = GraphRelationshipRuntimeCache.GetRelationshipLoadInvoker(relationship, loadWithEdges, request.EdgePredicate is not null);

        var result = await invoker(context, owner, request.EdgePredicate, cancellationToken).ConfigureAwait(false);
        return ToObjectList(result);
    }

    private static List<object> LoadRawRelationshipItemsInMemory(Node owner, GraphRelationshipMapping relationship, GraphIncludeRequest request, InMemoryGraphStore store)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(relationship);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(store);

        if (!store.EdgesByType.TryGetValue(relationship.EdgeType, out var edgeBucket) ||
            !store.NodesByType.TryGetValue(relationship.RelatedNodeType, out var relatedNodeBucket))
        {
            return [];
        }

        var loadWithEdges = ShouldLoadWithEdges(request);
        var edgePredicate = request.EdgePredicate is null ? null : GraphRelationshipRuntimeCache.GetObjectPredicate(request.EdgePredicate);
        var result = new List<object>();

        foreach (var edge in edgeBucket.Values)
        {
            if (!TryGetRelatedNode(owner, relationship.Direction, edge, relatedNodeBucket, out var relatedNode))
            {
                continue;
            }

            if (edgePredicate is not null && !edgePredicate(edge))
            {
                continue;
            }

            result.Add(loadWithEdges ? GraphRelationshipRuntimeCache.CreateEdgeResult(relationship.EdgeType, relationship.RelatedNodeType, edge, relatedNode) : relatedNode);
        }

        return result;
    }

    private static bool TryGetRelatedNode(
        Node owner,
        GraphTraversalDirection direction,
        Edge edge,
        Dictionary<Guid, Node> relatedNodesById,
        [NotNullWhen(true)] out Node? relatedNode)
    {
        relatedNode = null;

        var matchesOwner = direction == GraphTraversalDirection.Outgoing ? edge.FromId == owner.Id : edge.ToId == owner.Id;

        if (!matchesOwner)
        {
            return false;
        }

        var relatedNodeId = direction == GraphTraversalDirection.Outgoing ? edge.ToId : edge.FromId;

        return relatedNodesById.TryGetValue(relatedNodeId, out relatedNode);
    }

    private static IReadOnlyList<object> ApplyPostProcessing(IReadOnlyList<object> items, GraphIncludeRequest request)
    {
        if (request.EdgeOrderBy is null &&
            request.RelatedPredicate is null &&
            request.RelatedOrderBy is null &&
            request.Skip is null &&
            request.Take is null)
        {
            return items;
        }

        IEnumerable<object> query = items;

        if (request.EdgeOrderBy is not null)
        {
            var keySelector = GraphRelationshipRuntimeCache.GetObjectKeySelector(request.EdgeOrderBy);

            query = request.EdgeOrderDescending
                ? query.OrderByDescending(x => keySelector(GraphRelationshipRuntimeCache.ExtractEdgeFromEdgeResult(x)!))
                : query.OrderBy(x => keySelector(GraphRelationshipRuntimeCache.ExtractEdgeFromEdgeResult(x)!));
        }

        if (request.RelatedPredicate is not null)
        {
            var predicate = GraphRelationshipRuntimeCache.GetObjectPredicate(request.RelatedPredicate);

            query = query.Where(x =>
            {
                var related = GraphRelationshipRuntimeCache.GetRelatedNode(x);
                return related is not null && predicate(related);
            });
        }

        if (request.RelatedOrderBy is not null)
        {
            var keySelector = GraphRelationshipRuntimeCache.GetObjectKeySelector(request.RelatedOrderBy);

            query = request.RelatedOrderDescending
                ? query.OrderByDescending(x => keySelector(GraphRelationshipRuntimeCache.GetRelatedNode(x)!))
                : query.OrderBy(x => keySelector(GraphRelationshipRuntimeCache.GetRelatedNode(x)!));
        }

        if (request.Skip is int skip)
        {
            query = query.Skip(skip);
        }

        if (request.Take is int take)
        {
            query = query.Take(take);
        }

        return [.. query];
    }

    private static bool ShouldLoadWithEdges(GraphIncludeRequest request) => request.IncludeEdge || request.EdgeOrderBy is not null;

    private static void ValidateEdgeLambda(LambdaExpression? expression, Type edgeType, string operationName)
    {
        if (expression is null)
        {
            return;
        }

        var expectedInputType = expression.Parameters.Count == 1 ? expression.Parameters[0].Type : null;

        if (expectedInputType != edgeType)
        {
            throw new InvalidOperationException($"{operationName} type '{expectedInputType?.FullName ?? "unknown"}' does not match relationship edge type '{edgeType.FullName}'.");
        }
    }

    private static void RegisterFixup(GraphContext context, Node owner, GraphRelationshipMapping relationship, IReadOnlyList<object> items, bool includeEdge)
    {
        var relatedNodes = includeEdge ? ExtractRelatedNodes(items) : ExtractNodeObjects(items);

        context.RelationshipFixupStore.Set(owner, relationship.Name, includeEdge, includeEdge ? items : relatedNodes);

        context.ApplyNavigationFixup(owner, relationship.Name, relatedNodes);

        if (!includeEdge)
        {
            for (var i = 0; i < relatedNodes.Count; i++)
            {
                if (relatedNodes[i] is Node relatedNode)
                {
                    RegisterInverseNodeFixup(context, owner, relationship, relatedNode);
                }
            }

            return;
        }

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var relatedNode = GraphRelationshipRuntimeCache.ExtractNodeFromEdgeResult(item);

            if (relatedNode is null)
            {
                continue;
            }

            RegisterInverseNodeFixup(context, owner, relationship, relatedNode);
            RegisterInverseEdgeFixup(context, owner, relationship, relatedNode, item);
        }
    }

    private static void RegisterInverseNodeFixup(GraphContext context, Node owner, GraphRelationshipMapping relationship, Node relatedNode)
    {
        var inverseRelationships = GraphRelationshipRuntimeCache.GetInverseRelationships(context.Model, relationship, relatedNode.GetType(), owner.GetType());

        for (var i = 0; i < inverseRelationships.Length; i++)
        {
            var inverseRelationshipName = inverseRelationships[i].Name;

            context.RelationshipFixupStore.Add(relatedNode, inverseRelationshipName, false, owner);
            context.ApplyNavigationFixup(relatedNode, inverseRelationshipName, GetRelatedAsObjects(context, relatedNode, inverseRelationshipName));
        }
    }

    private static void RegisterInverseEdgeFixup(GraphContext context, Node owner, GraphRelationshipMapping relationship, Node relatedNode, object edgeResult)
    {
        var edge = GraphRelationshipRuntimeCache.ExtractEdgeFromEdgeResult(edgeResult);

        if (edge is null)
        {
            return;
        }

        var inverseRelationships = GraphRelationshipRuntimeCache.GetInverseRelationships(context.Model, relationship, relatedNode.GetType(), owner.GetType());

        for (var i = 0; i < inverseRelationships.Length; i++)
        {
            var inverseResult = GraphRelationshipRuntimeCache.CreateEdgeResult(relationship.EdgeType, owner.GetType(), edge, owner);

            context.RelationshipFixupStore.Add(relatedNode, inverseRelationships[i].Name, includeEdge: true, inverseResult);
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

    private static List<object> ExtractNodeObjects(IReadOnlyList<object> items)
    {
        var result = new List<object>(items.Count);

        for (var i = 0; i < items.Count; i++)
        {
            if (items[i] is Node node)
            {
                result.Add(node);
            }
        }

        return result;
    }

    private static List<object> ExtractRelatedNodes(IEnumerable<object> items)
    {
        var result = new List<object>();

        foreach (var item in items)
        {
            var node = GraphRelationshipRuntimeCache.ExtractNodeFromEdgeResult(item);

            if (node is not null)
            {
                result.Add(node);
            }
        }

        return result;
    }

    private static List<object> ToObjectList(object? value)
    {
        if (value is null)
        {
            return [];
        }

        if (value is IEnumerable enumerable)
        {
            var list = value is ICollection collection ? new List<object>(collection.Count) : [];

            foreach (var item in enumerable)
            {
                if (item is not null)
                {
                    list.Add(item);
                }
            }

            return list;
        }

        throw new InvalidOperationException("Relationship loader result is not enumerable.");
    }
}