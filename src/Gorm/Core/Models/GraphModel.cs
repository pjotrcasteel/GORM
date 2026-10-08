using Gorm.Application.Querying.Models;
using Gorm.Core.Metadata;

namespace Gorm.Core.Models;

/// <summary>
/// Represents graph model.
/// </summary>
public sealed class GraphModel
{
    private readonly Dictionary<Type, NodeTypeMapping> _nodesByType;
    private readonly Dictionary<Type, EdgeTypeMapping> _edgesByType;
    private readonly Dictionary<(Type OwnerType, string RelationshipName), GraphRelationshipMapping> _relationshipsByName;
    private readonly Dictionary<(Type OwnerType, string NavigationName), GraphNavigationMapping> _navigationsByName;
    private readonly Dictionary<(Type OwnerType, string RelationshipName), GraphNavigationMapping[]> _navigationsByRelationshipName;
    private readonly Dictionary<RelationshipShapeKey, GraphRelationshipMapping[]> _relationshipsByShape;
    private readonly Dictionary<Type, string[]> _nodeProjectionPropertyNamesByType;
    private readonly Dictionary<Type, string[]> _edgeProjectionPropertyNamesByType;

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphModel"/> class.
    /// </summary>
    /// <param name="nodes">The nodes.</param>
    /// <param name="edges">The edges.</param>
    public GraphModel(IReadOnlyList<NodeTypeMapping> nodes, IReadOnlyList<EdgeTypeMapping> edges)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(edges);

        Nodes = nodes;
        Edges = edges;

        _nodesByType = BuildNodeTypeMap(nodes);
        _edgesByType = BuildEdgeTypeMap(edges);

        ValidateTableMappings(nodes, edges);

        var relationshipIndexes = BuildRelationshipIndexes(nodes, _edgesByType);
        _relationshipsByName = relationshipIndexes.ByName;
        _relationshipsByShape = relationshipIndexes.ByShape;

        var navigationIndexes = BuildNavigationIndexes(nodes);
        _navigationsByName = navigationIndexes.ByName;
        _navigationsByRelationshipName = navigationIndexes.ByRelationshipName;

        _nodeProjectionPropertyNamesByType = BuildNodeProjectionPropertyNames(nodes);
        _edgeProjectionPropertyNamesByType = BuildEdgeProjectionPropertyNames(edges);
    }

    /// <summary>
    /// Gets or sets the nodes.
    /// </summary>
    public IReadOnlyList<NodeTypeMapping> Nodes { get; }

    /// <summary>
    /// Gets or sets the edges.
    /// </summary>
    public IReadOnlyList<EdgeTypeMapping> Edges { get; }

    /// <summary>
    /// Gets the value.
    /// </summary>
    /// <param name="clrType">The clr type.</param>
    /// <returns>The value.</returns>
    public NodeTypeMapping GetNode(Type clrType)
    {
        ArgumentNullException.ThrowIfNull(clrType);

        if (_nodesByType.TryGetValue(clrType, out var mapping))
        {
            return mapping;
        }

        throw new InvalidOperationException(
            $"No node mapping exists for CLR type '{clrType.FullName}'.");
    }

    /// <summary>
    /// Executes try get node.
    /// </summary>
    /// <param name="clrType">The clr type.</param>
    /// <param name="mapping">The mapping.</param>
    /// <returns>True when successful; otherwise, false.</returns>
    public bool TryGetNode(Type clrType, out NodeTypeMapping? mapping)
    {
        ArgumentNullException.ThrowIfNull(clrType);
        return _nodesByType.TryGetValue(clrType, out mapping);
    }

    /// <summary>
    /// Gets the value.
    /// </summary>
    /// <param name="clrType">The clr type.</param>
    /// <returns>The value.</returns>
    public EdgeTypeMapping GetEdge(Type clrType)
    {
        ArgumentNullException.ThrowIfNull(clrType);

        if (_edgesByType.TryGetValue(clrType, out var mapping))
        {
            return mapping;
        }

        throw new InvalidOperationException(
            $"No edge mapping exists for CLR type '{clrType.FullName}'.");
    }

    /// <summary>
    /// Executes try get edge.
    /// </summary>
    /// <param name="clrType">The clr type.</param>
    /// <param name="mapping">The mapping.</param>
    /// <returns>True when successful; otherwise, false.</returns>
    public bool TryGetEdge(Type clrType, out EdgeTypeMapping? mapping)
    {
        ArgumentNullException.ThrowIfNull(clrType);
        return _edgesByType.TryGetValue(clrType, out mapping);
    }

    /// <summary>
    /// Gets the value.
    /// </summary>
    /// <param name="ownerNodeType">The owner node type.</param>
    /// <returns>The items.</returns>
    public IReadOnlyList<GraphRelationshipMapping> GetRelationships(Type ownerNodeType)
    {
        ArgumentNullException.ThrowIfNull(ownerNodeType);
        return GetNode(ownerNodeType).Relationships;
    }

    /// <summary>
    /// Gets the value.
    /// </summary>
    /// <param name="ownerNodeType">The owner node type.</param>
    /// <param name="relationshipName">The relationship name.</param>
    /// <returns>The value.</returns>
    public GraphRelationshipMapping GetRelationship(Type ownerNodeType, string relationshipName)
    {
        ArgumentNullException.ThrowIfNull(ownerNodeType);
        ValidateName(relationshipName, nameof(relationshipName), "Relationship name");

        if (_relationshipsByName.TryGetValue((ownerNodeType, relationshipName), out var mapping))
        {
            return mapping;
        }

        throw new InvalidOperationException(
            $"No relationship named '{relationshipName}' exists for owner node '{ownerNodeType.FullName}'.");
    }

    /// <summary>
    /// Gets the value.
    /// </summary>
    /// <typeparam name="TOwner">The type of t owner.</typeparam>
    /// <param name="relationshipName">The relationship name.</param>
    /// <returns>The value.</returns>
    public GraphRelationshipMapping GetRelationship<TOwner>(string relationshipName) => GetRelationship(typeof(TOwner), relationshipName);

    /// <summary>
    /// Gets the navigation.
    /// </summary>
    /// <param name="ownerNodeType">The owner node type.</param>
    /// <param name="propertyName">The property name.</param>
    /// <returns>The value.</returns>
    internal GraphNavigationMapping GetNavigationCore(Type ownerNodeType, string propertyName)
    {
        ArgumentNullException.ThrowIfNull(ownerNodeType);
        ValidateName(propertyName, nameof(propertyName), "Navigation property name");

        if (_navigationsByName.TryGetValue((ownerNodeType, propertyName), out var navigation))
        {
            return navigation;
        }

        throw new InvalidOperationException(
            $"No navigation '{propertyName}' exists for node '{ownerNodeType.FullName}'.");
    }

    /// <summary>
    /// Gets navigations for relationship.
    /// </summary>
    /// <param name="ownerNodeType">The owner node type.</param>
    /// <param name="relationshipName">The relationship name.</param>
    /// <returns>The items.</returns>
    public GraphNavigationMapping[] GetNavigationsForRelationship(Type ownerNodeType, string relationshipName)
    {
        ArgumentNullException.ThrowIfNull(ownerNodeType);
        ValidateName(relationshipName, nameof(relationshipName), "Relationship name");

        return _navigationsByRelationshipName.TryGetValue((ownerNodeType, relationshipName), out var navigations) ? navigations : [];
    }

    /// <summary>
    /// Gets inverse relationships.
    /// </summary>
    /// <param name="relationship">The relationship.</param>
    /// <param name="inverseOwnerNodeType">The inverse owner node type.</param>
    /// <param name="inverseRelatedNodeType">The inverse related node type.</param>
    /// <returns>The items.</returns>
    public GraphRelationshipMapping[] GetInverseRelationships(GraphRelationshipMapping relationship, Type inverseOwnerNodeType, Type inverseRelatedNodeType)
    {
        ArgumentNullException.ThrowIfNull(relationship);
        ArgumentNullException.ThrowIfNull(inverseOwnerNodeType);
        ArgumentNullException.ThrowIfNull(inverseRelatedNodeType);

        var expectedDirection = relationship.Direction == GraphTraversalDirection.Outgoing ? GraphTraversalDirection.Incoming : GraphTraversalDirection.Outgoing;

        var key = new RelationshipShapeKey(inverseOwnerNodeType, inverseRelatedNodeType, relationship.EdgeType, expectedDirection);

        return _relationshipsByShape.TryGetValue(key, out var inverseRelationships) ? inverseRelationships : [];
    }

    /// <summary>
    /// Gets node projection property names.
    /// </summary>
    /// <param name="clrType">The clr type.</param>
    /// <returns>The value.</returns>
    public string[] GetNodeProjectionPropertyNames(Type clrType)
    {
        ArgumentNullException.ThrowIfNull(clrType);

        if (_nodeProjectionPropertyNamesByType.TryGetValue(clrType, out var propertyNames))
        {
            return propertyNames;
        }

        _ = GetNode(clrType);
        throw new InvalidOperationException($"No node projection property cache exists for '{clrType.FullName}'.");
    }

    /// <summary>
    /// Gets edge projection property names.
    /// </summary>
    /// <param name="clrType">The clr type.</param>
    /// <returns>The value.</returns>
    public string[] GetEdgeProjectionPropertyNames(Type clrType)
    {
        ArgumentNullException.ThrowIfNull(clrType);

        if (_edgeProjectionPropertyNamesByType.TryGetValue(clrType, out var propertyNames))
        {
            return propertyNames;
        }

        _ = GetEdge(clrType);
        throw new InvalidOperationException($"No edge projection property cache exists for '{clrType.FullName}'.");
    }

    private static Dictionary<Type, NodeTypeMapping> BuildNodeTypeMap(IReadOnlyList<NodeTypeMapping> nodes)
    {
        var result = new Dictionary<Type, NodeTypeMapping>(nodes.Count);

        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];

            if (!result.TryAdd(node.ClrType, node))
            {
                throw new InvalidOperationException(
                    $"Duplicate node mapping detected for CLR type '{node.ClrType.FullName}'.");
            }
        }

        return result;
    }

    private static Dictionary<Type, EdgeTypeMapping> BuildEdgeTypeMap(IReadOnlyList<EdgeTypeMapping> edges)
    {
        var result = new Dictionary<Type, EdgeTypeMapping>(edges.Count);

        for (var i = 0; i < edges.Count; i++)
        {
            var edge = edges[i];

            if (!result.TryAdd(edge.ClrType, edge))
            {
                throw new InvalidOperationException(
                    $"Duplicate edge mapping detected for CLR type '{edge.ClrType.FullName}'.");
            }
        }

        return result;
    }

    private static RelationshipIndexes BuildRelationshipIndexes(IReadOnlyList<NodeTypeMapping> nodes, Dictionary<Type, EdgeTypeMapping> edgeLookup)
    {
        var byName = new Dictionary<(Type OwnerType, string RelationshipName), GraphRelationshipMapping>();
        var byShapeBuilder = new Dictionary<RelationshipShapeKey, List<GraphRelationshipMapping>>();

        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];

            for (var j = 0; j < node.Relationships.Count; j++)
            {
                var relationship = node.Relationships[j];

                ValidateRelationship(node, relationship, edgeLookup);

                var byNameKey = (node.ClrType, relationship.Name);

                if (!byName.TryAdd(byNameKey, relationship))
                {
                    throw new InvalidOperationException(
                        $"Duplicate relationship '{relationship.Name}' detected for node '{node.ClrType.FullName}'.");
                }

                var shapeKey = new RelationshipShapeKey(relationship.OwnerNodeType, relationship.RelatedNodeType, relationship.EdgeType, relationship.Direction);

                if (!byShapeBuilder.TryGetValue(shapeKey, out var relationshipsForShape))
                {
                    relationshipsForShape = [];
                    byShapeBuilder[shapeKey] = relationshipsForShape;
                }

                relationshipsForShape.Add(relationship);
            }
        }

        var byShape = new Dictionary<RelationshipShapeKey, GraphRelationshipMapping[]>(byShapeBuilder.Count);

        foreach (var item in byShapeBuilder)
        {
            byShape[item.Key] = [.. item.Value];
        }

        return new RelationshipIndexes(byName, byShape);
    }

    private static NavigationIndexes BuildNavigationIndexes(IReadOnlyList<NodeTypeMapping> nodes)
    {
        var byName = new Dictionary<(Type OwnerType, string NavigationName), GraphNavigationMapping>();
        var byRelationshipNameBuilder = new Dictionary<(Type OwnerType, string RelationshipName), List<GraphNavigationMapping>>();

        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];

            for (var j = 0; j < node.Navigations.Count; j++)
            {
                var navigation = node.Navigations[j];

                var byNameKey = (node.ClrType, navigation.PropertyName);

                if (!byName.TryAdd(byNameKey, navigation))
                {
                    throw new InvalidOperationException(
                        $"Duplicate navigation '{navigation.PropertyName}' detected for node '{node.ClrType.FullName}'.");
                }

                var byRelationshipKey = (node.ClrType, navigation.RelationshipName);

                if (!byRelationshipNameBuilder.TryGetValue(byRelationshipKey, out var navigationsForRelationship))
                {
                    navigationsForRelationship = [];
                    byRelationshipNameBuilder[byRelationshipKey] = navigationsForRelationship;
                }

                navigationsForRelationship.Add(navigation);
            }
        }

        var byRelationshipName = new Dictionary<(Type OwnerType, string RelationshipName), GraphNavigationMapping[]>(byRelationshipNameBuilder.Count);

        foreach (var item in byRelationshipNameBuilder)
        {
            byRelationshipName[item.Key] = [.. item.Value];
        }

        return new NavigationIndexes(byName, byRelationshipName);
    }

    private static Dictionary<Type, string[]> BuildNodeProjectionPropertyNames(IReadOnlyList<NodeTypeMapping> nodes)
    {
        var result = new Dictionary<Type, string[]>(nodes.Count);

        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            result[node.ClrType] = BuildProjectionPropertyNames(node.KeyPropertyName, node.Properties);
        }

        return result;
    }

    private static Dictionary<Type, string[]> BuildEdgeProjectionPropertyNames(IReadOnlyList<EdgeTypeMapping> edges)
    {
        var result = new Dictionary<Type, string[]>(edges.Count);

        for (var i = 0; i < edges.Count; i++)
        {
            var edge = edges[i];
            result[edge.ClrType] = BuildProjectionPropertyNames(edge.KeyPropertyName, edge.Properties);
        }

        return result;
    }

    private static string[] BuildProjectionPropertyNames(string keyPropertyName, IReadOnlyList<PropertyMapping> mappedProperties)
    {
        var result = new List<string>(mappedProperties.Count + 1)
        {
            keyPropertyName
        };

        for (var i = 0; i < mappedProperties.Count; i++)
        {
            var propertyName = mappedProperties[i].PropertyName;

            if (!string.Equals(propertyName, keyPropertyName, StringComparison.Ordinal))
            {
                result.Add(propertyName);
            }
        }

        return [.. result];
    }

    private static void ValidateTableMappings(IReadOnlyList<NodeTypeMapping> nodes, IReadOnlyList<EdgeTypeMapping> edges)
    {
        var nodeTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var edgeTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < nodes.Count; i++)
        {
            var tableName = BuildTableKey(nodes[i].Schema, nodes[i].TableName);

            if (!nodeTables.Add(tableName))
            {
                throw new InvalidOperationException($"Duplicate node table mapping detected for '{tableName}'.");
            }
        }

        for (var i = 0; i < edges.Count; i++)
        {
            var tableName = BuildTableKey(edges[i].Schema, edges[i].TableName);

            if (!edgeTables.Add(tableName))
            {
                throw new InvalidOperationException($"Duplicate edge table mapping detected for '{tableName}'.");
            }

            if (nodeTables.Contains(tableName))
            {
                throw new InvalidOperationException($"The table '{tableName}' cannot be mapped as both a node and an edge.");
            }
        }
    }

    private static void ValidateRelationship(NodeTypeMapping ownerNode, GraphRelationshipMapping relationship, Dictionary<Type, EdgeTypeMapping> edgeLookup)
    {
        if (!edgeLookup.TryGetValue(relationship.EdgeType, out var edgeMapping))
        {
            throw new InvalidOperationException(
                $"Relationship '{relationship.Name}' on node '{ownerNode.ClrType.FullName}' " +
                $"references edge type '{relationship.EdgeType.FullName}', but that edge is not mapped.");
        }

        if (relationship.Direction == GraphTraversalDirection.Outgoing)
        {
            ValidateOutgoingRelationship(ownerNode, relationship, edgeMapping);
            return;
        }

        ValidateIncomingRelationship(ownerNode, relationship, edgeMapping);
    }

    private static void ValidateOutgoingRelationship(NodeTypeMapping ownerNode, GraphRelationshipMapping relationship, EdgeTypeMapping edgeMapping)
    {
        if (edgeMapping.FromNodeType != ownerNode.ClrType)
        {
            throw new InvalidOperationException(
                $"Relationship '{relationship.Name}' on node '{ownerNode.ClrType.FullName}' is outgoing, " +
                $"but edge '{edgeMapping.ClrType.FullName}' starts from '{edgeMapping.FromNodeType.FullName}'.");
        }

        if (edgeMapping.ToNodeType != relationship.RelatedNodeType)
        {
            throw new InvalidOperationException(
                $"Relationship '{relationship.Name}' on node '{ownerNode.ClrType.FullName}' points to related node '{relationship.RelatedNodeType.FullName}', " +
                $"but edge '{edgeMapping.ClrType.FullName}' points to '{edgeMapping.ToNodeType.FullName}'.");
        }
    }

    private static void ValidateIncomingRelationship(NodeTypeMapping ownerNode, GraphRelationshipMapping relationship, EdgeTypeMapping edgeMapping)
    {
        if (edgeMapping.ToNodeType != ownerNode.ClrType)
        {
            throw new InvalidOperationException(
                $"Relationship '{relationship.Name}' on node '{ownerNode.ClrType.FullName}' is incoming, " +
                $"but edge '{edgeMapping.ClrType.FullName}' ends at '{edgeMapping.ToNodeType.FullName}'.");
        }

        if (edgeMapping.FromNodeType != relationship.RelatedNodeType)
        {
            throw new InvalidOperationException(
                $"Relationship '{relationship.Name}' on node '{ownerNode.ClrType.FullName}' expects related node '{relationship.RelatedNodeType.FullName}', " +
                $"but edge '{edgeMapping.ClrType.FullName}' starts from '{edgeMapping.FromNodeType.FullName}'.");
        }
    }

    private static void ValidateName(string value, string parameterName, string displayName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{displayName} cannot be null or whitespace.", parameterName);
        }
    }

    private static string BuildTableKey(string schema, string tableName) => $"{schema}.{tableName}";

    private sealed record RelationshipIndexes(
        Dictionary<(Type OwnerType, string RelationshipName), GraphRelationshipMapping> ByName,
        Dictionary<RelationshipShapeKey, GraphRelationshipMapping[]> ByShape);

    private sealed record NavigationIndexes(
        Dictionary<(Type OwnerType, string NavigationName), GraphNavigationMapping> ByName,
        Dictionary<(Type OwnerType, string RelationshipName), GraphNavigationMapping[]> ByRelationshipName);

    private readonly record struct RelationshipShapeKey(
        Type OwnerNodeType,
        Type RelatedNodeType,
        Type EdgeType,
        GraphTraversalDirection Direction);
}