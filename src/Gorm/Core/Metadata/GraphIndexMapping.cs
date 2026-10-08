namespace Gorm.Core.Metadata;

/// <summary>
/// Represents graph index mapping.
/// </summary>
public sealed class GraphIndexMapping
{
    /// <summary>
    /// Gets or sets the database name.
    /// </summary>
    public required string DatabaseName { get; init; }
    /// <summary>
    /// Gets or sets the property names.
    /// </summary>
    public required IReadOnlyList<string> PropertyNames { get; init; }
    /// <summary>
    /// Gets a value indicating whether is unique.
    /// </summary>
    public bool IsUnique { get; init; }
}