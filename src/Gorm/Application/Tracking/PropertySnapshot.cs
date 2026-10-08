namespace Gorm.Application.Tracking;

/// <summary>
/// Represents property snapshot.
/// </summary>
public sealed class PropertySnapshot
{
    /// <summary>
    /// Gets or sets the property name.
    /// </summary>
    public required string PropertyName { get; init; }
    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public object? Value { get; init; }
}