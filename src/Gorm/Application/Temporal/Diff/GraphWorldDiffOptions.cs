namespace Gorm.Application.Temporal.Diff;

/// <summary>
/// Bounds temporal world comparison.
/// </summary>
public sealed class GraphWorldDiffOptions
{
    /// <summary>
    /// Gets or sets the maximum number of entity changes returned by one comparison.
    /// </summary>
    public int MaximumChanges { get; init; } = 1_000_000;
}