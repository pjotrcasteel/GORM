namespace Gorm.Core.Metadata;

/// <summary>
/// Represents node type mapping.
/// </summary>
public sealed class NodeTypeMapping
{
    /// <summary>
    /// Gets or sets the clr type.
    /// </summary>
    public required Type ClrType { get; init; }
    /// <summary>
    /// Gets or sets the table name.
    /// </summary>
    public required string TableName { get; init; }
    /// <summary>
    /// Gets or sets the schema.
    /// </summary>
    public required string Schema { get; init; }
    /// <summary>
    /// Gets or sets the key property name.
    /// </summary>
    public required string KeyPropertyName { get; init; }
    /// <summary>
    /// Gets or sets the properties.
    /// </summary>
    public required IReadOnlyList<PropertyMapping> Properties { get; init; }
    /// <summary>
    /// Gets or sets the relationships.
    /// </summary>
    public required IReadOnlyList<GraphRelationshipMapping> Relationships { get; init; }
    /// <summary>
    /// Gets or sets the navigations.
    /// </summary>
    public required IReadOnlyList<GraphNavigationMapping> Navigations { get; init; }
    /// <summary>
    /// Gets or sets the indexes.
    /// </summary>
    public required IReadOnlyList<GraphIndexMapping> Indexes { get; init; }
}