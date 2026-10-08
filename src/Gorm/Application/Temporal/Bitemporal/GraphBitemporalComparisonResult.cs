using Gorm.Application.Temporal.Diff;
using Gorm.Application.Temporal.History;

namespace Gorm.Application.Temporal.Bitemporal;

/// <summary>
/// Contains two resolved worlds, their difference and the temporal axis interpretation.
/// </summary>
public sealed class GraphBitemporalComparisonResult
{
    /// <summary>
    /// Gets the comparison classification.
    /// </summary>
    public required GraphBitemporalComparisonKind Kind { get; init; }

    /// <summary>
    /// Gets the first point-in-time projection.
    /// </summary>
    public required GraphWorldHistoryProjectionResult First { get; init; }

    /// <summary>
    /// Gets the second point-in-time projection.
    /// </summary>
    public required GraphWorldHistoryProjectionResult Second { get; init; }

    /// <summary>
    /// Gets the entity-level difference between both worlds.
    /// </summary>
    public required GraphWorldSnapshotDiff Difference { get; init; }

    /// <summary>
    /// Gets a reproducible explanation of the comparison axis.
    /// </summary>
    public required string Explanation { get; init; }
}