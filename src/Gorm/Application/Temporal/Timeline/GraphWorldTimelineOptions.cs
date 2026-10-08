namespace Gorm.Application.Temporal.Timeline;

/// <summary>
/// Bounds construction of a temporal world timeline.
/// </summary>
public sealed class GraphWorldTimelineOptions
{
    /// <summary>
    /// Gets or sets the maximum number of snapshots in one timeline.
    /// </summary>
    public int MaximumSnapshots { get; init; } = 100_000;

    /// <summary>
    /// Gets or sets the maximum number of changes between adjacent snapshots.
    /// </summary>
    public int MaximumChangesPerSnapshot { get; init; } = 1_000_000;
}