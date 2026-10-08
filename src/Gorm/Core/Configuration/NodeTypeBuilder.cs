using System.Linq.Expressions;
using Gorm.Application.Querying.Models;
using Gorm.Core.Configuration.Interfaces;
using Gorm.Core.Metadata;
using Gorm.Core.Primitives;

namespace Gorm.Core.Configuration;

/// <summary>
/// Represents node type builder.
/// </summary>
public sealed class NodeTypeBuilder<TNode> : INodeTypeBuilder
    where TNode : Node
{
    private readonly HashSet<string> _ignoredProperties = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PropertyBuilder> _properties = new(StringComparer.Ordinal);

    private readonly List<GraphRelationshipMapping> _relationships = [];
    private readonly List<GraphNavigationMapping> _navigations = [];
    private readonly List<GraphIndexBuilder> _indexes = [];

    private string _tableName = typeof(TNode).Name;
    private string _schema = "dbo";
    private string _keyPropertyName = nameof(Node.Id);

    /// <summary>
    /// Executes to table.
    /// </summary>
    /// <param name="tableName">The table name.</param>
    /// <param name="schema">The schema.</param>
    /// <returns>The builder.</returns>
    public NodeTypeBuilder<TNode> ToTable(string tableName, string schema = "dbo")
    {
        (_tableName, _schema) = GraphTypeBuilderSupport.BuildTable(tableName, schema);
        return this;
    }

    /// <summary>
    /// Executes has key.
    /// </summary>
    /// <param name="keyExpression">The key expression.</param>
    /// <returns>The builder.</returns>
    public NodeTypeBuilder<TNode> HasKey(Expression<Func<TNode, object?>> keyExpression)
    {
        _keyPropertyName = GraphTypeBuilderSupport.GetPropertyName(keyExpression);
        return this;
    }

    /// <summary>
    /// Executes property.
    /// </summary>
    /// <typeparam name="TProperty">The type of t property.</typeparam>
    /// <param name="propertyExpression">The property expression.</param>
    /// <returns>The builder.</returns>
    public PropertyBuilder Property<TProperty>(Expression<Func<TNode, TProperty>> propertyExpression) =>
        GraphTypeBuilderSupport.GetOrAddProperty(_properties, propertyExpression);

    /// <summary>
    /// Executes has index.
    /// </summary>
    /// <param name="propertyExpressions">The property expressions.</param>
    /// <returns>The builder.</returns>
    public GraphIndexBuilder HasIndex(params Expression<Func<TNode, object?>>[] propertyExpressions) =>
        GraphTypeBuilderSupport.AddIndex(_indexes, _tableName, propertyExpressions);

    /// <summary>
    /// Executes has unique index.
    /// </summary>
    /// <param name="propertyExpressions">The property expressions.</param>
    /// <returns>The builder.</returns>
    public GraphIndexBuilder HasUniqueIndex(params Expression<Func<TNode, object?>>[] propertyExpressions)
    {
        var index = HasIndex(propertyExpressions);
        index.IsUnique();
        return index;
    }

    /// <summary>
    /// Executes has outgoing relationship.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <typeparam name="TRelated">The type of t related.</typeparam>
    /// <param name="relationshipName">The relationship name.</param>
    /// <param name="multiplicity">The multiplicity.</param>
    /// <returns>The builder.</returns>
    public NodeTypeBuilder<TNode> HasOutgoingRelationship<TEdge, TRelated>(string relationshipName, GraphRelationshipMultiplicity multiplicity = GraphRelationshipMultiplicity.Many)
        where TEdge : Edge
        where TRelated : Node
    {
        AddRelationship(relationshipName, typeof(TEdge), typeof(TRelated), GraphTraversalDirection.Outgoing, multiplicity);

        return this;
    }

    /// <summary>
    /// Executes has incoming relationship.
    /// </summary>
    /// <typeparam name="TEdge">The type of t edge.</typeparam>
    /// <typeparam name="TRelated">The type of t related.</typeparam>
    /// <param name="relationshipName">The relationship name.</param>
    /// <param name="multiplicity">The multiplicity.</param>
    /// <returns>The builder.</returns>
    public NodeTypeBuilder<TNode> HasIncomingRelationship<TEdge, TRelated>(string relationshipName, GraphRelationshipMultiplicity multiplicity = GraphRelationshipMultiplicity.Many)
        where TEdge : Edge
        where TRelated : Node
    {
        AddRelationship(relationshipName, typeof(TEdge), typeof(TRelated), GraphTraversalDirection.Incoming, multiplicity);

        return this;
    }

    /// <summary>
    /// Executes has navigation.
    /// </summary>
    /// <typeparam name="TProperty">The type of t property.</typeparam>
    /// <param name="navigationExpression">The navigation expression.</param>
    /// <param name="relationshipName">The relationship name.</param>
    /// <returns>The builder.</returns>
    public NodeTypeBuilder<TNode> HasNavigation<TProperty>(Expression<Func<TNode, TProperty>> navigationExpression, string relationshipName)
    {
        var propertyName = GraphTypeBuilderSupport.GetPropertyName(navigationExpression);
        var navigation = BuildNavigation(propertyName, typeof(TProperty), relationshipName);

        if (_navigations.Any(x => string.Equals(x.PropertyName, propertyName, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"Navigation '{propertyName}' is already configured for node '{typeof(TNode).FullName}'.");
        }

        _navigations.Add(navigation);

        return this;
    }

    public NodeTypeBuilder<TNode> Ignore<TProperty>(Expression<Func<TNode, TProperty>> propertyExpression)
    {
        GraphTypeBuilderSupport.Ignore(_properties, _ignoredProperties, propertyExpression);
        return this;
    }

    /// <summary>
    /// Builds the result.
    /// </summary>
    /// <returns>The value.</returns>
    public NodeTypeMapping BuildMapping()
    {
        var properties = GraphPropertyMappingDiscovery.BuildProperties(typeof(TNode), _keyPropertyName, _properties.Values, [.. _ignoredProperties]);

        ValidateNavigations();
        GraphTypeBuilderSupport.ValidateIndexes<TNode>("node", _indexes, properties);

        return new NodeTypeMapping
        {
            ClrType = typeof(TNode),
            TableName = _tableName,
            Schema = _schema,
            KeyPropertyName = _keyPropertyName,
            Properties = properties,
            Relationships = [.. _relationships],
            Navigations = [.. _navigations],
            Indexes = [.. _indexes.Select(x => x.Build())]
        };
    }

    private void AddRelationship(string relationshipName, Type edgeType, Type relatedNodeType, GraphTraversalDirection direction, GraphRelationshipMultiplicity multiplicity)
    {
        if (string.IsNullOrWhiteSpace(relationshipName))
        {
            throw new ArgumentException("Relationship name cannot be null or whitespace.", nameof(relationshipName));
        }

        var exists = _relationships.Any(x => string.Equals(x.Name, relationshipName, StringComparison.Ordinal));

        if (exists)
        {
            throw new InvalidOperationException(
                $"Relationship '{relationshipName}' is already configured for node '{typeof(TNode).FullName}'.");
        }

        _relationships.Add(new GraphRelationshipMapping
        {
            Name = relationshipName,
            OwnerNodeType = typeof(TNode),
            RelatedNodeType = relatedNodeType,
            EdgeType = edgeType,
            Direction = direction,
            Multiplicity = multiplicity
        });
    }

    private void ValidateNavigations()
    {
        foreach (var navigation in _navigations)
        {
            var relationship = _relationships.SingleOrDefault(x =>
                string.Equals(x.Name, navigation.RelationshipName, StringComparison.Ordinal)) ?? throw new InvalidOperationException(
                    $"Navigation '{navigation.PropertyName}' on node '{typeof(TNode).FullName}' references relationship '{navigation.RelationshipName}'," +
                    $" but that relationship is not configured.");

            if (navigation.ElementType != relationship.RelatedNodeType)
            {
                throw new InvalidOperationException(
                    $"Navigation '{navigation.PropertyName}' on node '{typeof(TNode).FullName}' has element type '{navigation.ElementType.FullName}'," +
                    $" but relationship '{relationship.Name}' points to '{relationship.RelatedNodeType.FullName}'.");
            }

            if (navigation.Kind == GraphNavigationKind.Reference &&
                relationship.Multiplicity != GraphRelationshipMultiplicity.One)
            {
                throw new InvalidOperationException(
                    $"Navigation '{navigation.PropertyName}' on node '{typeof(TNode).FullName}' is a reference navigation, " +
                    $"but relationship '{relationship.Name}' is not configured with multiplicity One.");
            }
        }
    }

    private static GraphNavigationMapping BuildNavigation(string propertyName, Type propertyType, string relationshipName)
    {
        if (string.IsNullOrWhiteSpace(relationshipName))
        {
            throw new ArgumentException("Relationship name cannot be null or whitespace.", nameof(relationshipName));
        }

        if (TryGetCollectionElementType(propertyType, out var elementType))
        {
            return new GraphNavigationMapping
            {
                PropertyName = propertyName,
                RelationshipName = relationshipName,
                PropertyType = propertyType,
                ElementType = elementType,
                Kind = GraphNavigationKind.Collection
            };
        }

        if (typeof(Node).IsAssignableFrom(propertyType))
        {
            return new GraphNavigationMapping
            {
                PropertyName = propertyName,
                RelationshipName = relationshipName,
                PropertyType = propertyType,
                ElementType = propertyType,
                Kind = GraphNavigationKind.Reference
            };
        }

        throw new InvalidOperationException(
            $"Navigation '{propertyName}' must be a node reference or a collection of nodes.");
    }

    private static bool TryGetCollectionElementType(Type propertyType, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Type? elementType)
    {
        if (TryGetArrayElementType(propertyType, out elementType))
        {
            return true;
        }

        if (TryGetDirectGenericCollectionElementType(propertyType, out elementType))
        {
            return true;
        }

        if (TryGetInterfaceCollectionElementType(propertyType, out elementType))
        {
            return true;
        }

        elementType = null;
        return false;
    }

    private static bool TryGetArrayElementType(Type propertyType, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Type? elementType)
    {
        if (!propertyType.IsArray)
        {
            elementType = null;
            return false;
        }

        var candidate = propertyType.GetElementType();

        if (candidate is null || !typeof(Node).IsAssignableFrom(candidate))
        {
            elementType = null;
            return false;
        }

        elementType = candidate;
        return true;
    }

    private static bool TryGetDirectGenericCollectionElementType(Type propertyType, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Type? elementType)
    {
        if (!propertyType.IsGenericType)
        {
            elementType = null;
            return false;
        }

        var genericTypeDefinition = propertyType.GetGenericTypeDefinition();

        if (!IsSupportedCollectionTypeDefinition(genericTypeDefinition))
        {
            elementType = null;
            return false;
        }

        var candidate = propertyType.GetGenericArguments()[0];

        if (!typeof(Node).IsAssignableFrom(candidate))
        {
            elementType = null;
            return false;
        }

        elementType = candidate;
        return true;
    }

    private static bool TryGetInterfaceCollectionElementType(Type propertyType, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Type? elementType)
    {
        var interfaceMatch = propertyType.GetInterfaces().FirstOrDefault(IsSupportedNodeCollectionInterface);

        if (interfaceMatch is null)
        {
            elementType = null;
            return false;
        }

        elementType = interfaceMatch.GetGenericArguments()[0];
        return true;
    }

    private static bool IsSupportedNodeCollectionInterface(Type type)
    {
        return type.IsGenericType && IsSupportedCollectionTypeDefinition(type.GetGenericTypeDefinition()) && typeof(Node).IsAssignableFrom(type.GetGenericArguments()[0]);
    }

    private static bool IsSupportedCollectionTypeDefinition(Type type)
    {
        return type == typeof(ICollection<>) ||
            type == typeof(IList<>) ||
            type == typeof(List<>) ||
            type == typeof(IEnumerable<>) ||
            type == typeof(IReadOnlyCollection<>) ||
            type == typeof(IReadOnlyList<>);
    }
}