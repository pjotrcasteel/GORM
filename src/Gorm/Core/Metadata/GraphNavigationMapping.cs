namespace Gorm.Core.Metadata;

/// <summary>
/// Represents graph navigation mapping.
/// </summary>
public sealed class GraphNavigationMapping
{
    /// <summary>
    /// Gets or sets the property name.
    /// </summary>
    public required string PropertyName { get; init; }

    /// <summary>
    /// Gets or sets the relationship name.
    /// </summary>
    public required string RelationshipName { get; init; }

    /// <summary>
    /// Gets or sets the property type.
    /// </summary>
    public required Type PropertyType { get; init; }

    /// <summary>
    /// Gets or sets the element type.
    /// </summary>
    public required Type ElementType { get; init; }

    /// <summary>
    /// Gets or sets the kind.
    /// </summary>
    public required GraphNavigationKind Kind { get; init; }
}