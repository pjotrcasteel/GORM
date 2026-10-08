namespace Gorm.Application.Querying.Models;

/// <summary>
/// Represents graph ordering.
/// </summary>
public sealed class GraphOrdering
{
    /// <summary>
    /// Gets the name of the property to order by.
    /// </summary>
    public required string PropertyName { get; init; }
    /// <summary>
    /// Gets whether the ordering is descending.
    /// </summary>
    public required bool Descending { get; init; }
    /// <summary>
    /// Gets whether this ordering was applied after a projection.
    /// </summary>
    public bool IsProjected { get; init; }
}