using Gorm.Application.Temporal.Diff;
using Gorm.Application.Temporal.Snapshots;

namespace Gorm.Application.Temporal.Timeline;

/// <summary>
/// Associates one world identity with changes from the preceding version.
/// </summary>
public sealed class GraphWorldTimelinePoint
{
    /// <summary>
    /// Gets the world identity at this timeline point.
    /// </summary>
    public required GraphWorldSnapshotIdentity Snapshot { get; init; }

    /// <summary>
    /// Gets changes from the preceding version, or null for the first point.
    /// </summary>
    public GraphWorldSnapshotDiff? ChangesFromPrevious { get; init; }
}