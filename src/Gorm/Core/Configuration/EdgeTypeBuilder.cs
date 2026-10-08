using System.Linq.Expressions;
using Gorm.Core.Configuration.Interfaces;
using Gorm.Core.Metadata;
using Gorm.Core.Primitives;

namespace Gorm.Core.Configuration;

/// <summary>
/// Represents edge type builder.
/// </summary>
public sealed class EdgeTypeBuilder<TEdge> : IEdgeTypeBuilder
    where TEdge : Edge
{
    private readonly HashSet<string> _ignoredProperties = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PropertyBuilder> _properties = new(StringComparer.Ordinal);

    private readonly List<GraphIndexBuilder> _indexes = [];

    private string _tableName = typeof(TEdge).Name;
    private string _schema = "dbo";
    private string _keyPropertyName = nameof(Edge.Id);
    private Type? _fromNodeType;
    private Type? _toNodeType;

    /// <summary>
    /// Executes to table.
    /// </summary>
    /// <param name="tableName">The table name.</param>
    /// <param name="schema">The schema.</param>
    /// <returns>The builder.</returns>
    public EdgeTypeBuilder<TEdge> ToTable(string tableName, string schema = "dbo")
    {
        (_tableName, _schema) = GraphTypeBuilderSupport.BuildTable(tableName, schema);
        return this;
    }

    /// <summary>
    /// Executes has key.
    /// </summary>
    /// <param name="keyExpression">The key expression.</param>
    /// <returns>The builder.</returns>
    public EdgeTypeBuilder<TEdge> HasKey(Expression<Func<TEdge, object?>> keyExpression)
    {
        _keyPropertyName = GraphTypeBuilderSupport.GetPropertyName(keyExpression);
        return this;
    }

    /// <summary>
    /// Executes from.
    /// </summary>
    /// <typeparam name="TFrom">The type of t from.</typeparam>
    /// <returns>The builder.</returns>
    public EdgeTypeBuilder<TEdge> From<TFrom>()
        where TFrom : Node
    {
        _fromNodeType = typeof(TFrom);
        return this;
    }

    /// <summary>
    /// Executes to.
    /// </summary>
    /// <typeparam name="TTo">The type of t to.</typeparam>
    /// <returns>The builder.</returns>
    public EdgeTypeBuilder<TEdge> To<TTo>()
        where TTo : Node
    {
        _toNodeType = typeof(TTo);
        return this;
    }

    /// <summary>
    /// Executes property.
    /// </summary>
    /// <typeparam name="TProperty">The type of t property.</typeparam>
    /// <param name="propertyExpression">The property expression.</param>
    /// <returns>The builder.</returns>
    public PropertyBuilder Property<TProperty>(Expression<Func<TEdge, TProperty>> propertyExpression) =>
        GraphTypeBuilderSupport.GetOrAddProperty(_properties, propertyExpression);

    /// <summary>
    /// Executes has index.
    /// </summary>
    /// <param name="propertyExpressions">The property expressions.</param>
    /// <returns>The builder.</returns>
    public GraphIndexBuilder HasIndex(params Expression<Func<TEdge, object?>>[] propertyExpressions) =>
        GraphTypeBuilderSupport.AddIndex(_indexes, _tableName, propertyExpressions);

    /// <summary>
    /// Executes has unique index.
    /// </summary>
    /// <param name="propertyExpressions">The property expressions.</param>
    /// <returns>The builder.</returns>
    public GraphIndexBuilder HasUniqueIndex(params Expression<Func<TEdge, object?>>[] propertyExpressions)
    {
        var index = HasIndex(propertyExpressions);
        index.IsUnique();
        return index;
    }

    public EdgeTypeBuilder<TEdge> Ignore<TProperty>(Expression<Func<TEdge, TProperty>> propertyExpression)
    {
        GraphTypeBuilderSupport.Ignore(_properties, _ignoredProperties, propertyExpression);
        return this;
    }

    /// <summary>
    /// Builds the result.
    /// </summary>
    /// <returns>The value.</returns>
    public EdgeTypeMapping BuildMapping()
    {
        if (_fromNodeType is null)
        {
            throw new InvalidOperationException(
                $"Edge '{typeof(TEdge).FullName}' does not define a From node type.");
        }

        if (_toNodeType is null)
        {
            throw new InvalidOperationException(
                $"Edge '{typeof(TEdge).FullName}' does not define a To node type.");
        }

        var properties = GraphPropertyMappingDiscovery.BuildProperties(
            typeof(TEdge),
            _keyPropertyName,
            _properties.Values,
            [
                .. _ignoredProperties,
            nameof(Edge.FromId),
            nameof(Edge.ToId)
            ]);

        GraphTypeBuilderSupport.ValidateIndexes<TEdge>("edge", _indexes, properties);

        return new EdgeTypeMapping
        {
            ClrType = typeof(TEdge),
            TableName = _tableName,
            Schema = _schema,
            KeyPropertyName = _keyPropertyName,
            FromNodeType = _fromNodeType,
            ToNodeType = _toNodeType,
            Properties = properties,
            Indexes = [.. _indexes.Select(x => x.Build())]
        };
    }
}