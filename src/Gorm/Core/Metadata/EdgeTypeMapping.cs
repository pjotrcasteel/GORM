namespace Gorm.Core.Metadata;

/// <summary>
/// Represents edge type mapping.
/// </summary>
public sealed class EdgeTypeMapping
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
    /// Gets or sets the from node type.
    /// </summary>
    public required Type FromNodeType { get; init; }
    /// <summary>
    /// Gets or sets the to node type.
    /// </summary>
    public required Type ToNodeType { get; init; }
    /// <summary>
    /// Gets or sets the properties.
    /// </summary>
    public required IReadOnlyList<PropertyMapping> Properties { get; init; }
    /// <summary>
    /// Gets or sets the indexes.
    /// </summary>
    public required IReadOnlyList<GraphIndexMapping> Indexes { get; init; }
}