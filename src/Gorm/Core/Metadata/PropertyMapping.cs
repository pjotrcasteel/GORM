namespace Gorm.Core.Metadata;

/// <summary>
/// Represents property mapping.
/// </summary>
public sealed class PropertyMapping
{
    /// <summary>
    /// Gets or sets the property name.
    /// </summary>
    public required string PropertyName { get; init; }
    /// <summary>
    /// Gets or sets the property type.
    /// </summary>
    public required Type PropertyType { get; init; }
    /// <summary>
    /// Gets a value indicating whether is required.
    /// </summary>
    public bool IsRequired { get; init; }
    /// <summary>
    /// Gets or sets the max length.
    /// </summary>
    public int? MaxLength { get; init; }
}