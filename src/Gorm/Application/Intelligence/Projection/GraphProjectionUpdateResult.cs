namespace Gorm.Application.Intelligence.Projection;

/// <summary>
/// Describes the outcome and invalidation scope of a projection update.
/// </summary>
public sealed class GraphProjectionUpdateResult
{
    /// <summary>
    /// Gets the exact previous source version against which the update was evaluated.
    /// </summary>
    public required long BaseVersion { get; init; }

    /// <summary>
    /// Gets whether the delta was applied incrementally or a full rebuild was used.
    /// </summary>
    public required GraphProjectionUpdateMode Mode { get; init; }

    /// <summary>
    /// Gets the new immutable versioned snapshot.
    /// </summary>
    public required GraphProjectionSnapshot Snapshot { get; init; }

    /// <summary>
    /// Gets node identifiers whose values or adjacency may have changed.
    /// </summary>
    public required Guid[] AffectedNodeIds { get; init; }

    /// <summary>
    /// Gets edge identifiers that were added, replaced or removed.
    /// </summary>
    public required Guid[] AffectedEdgeIds { get; init; }

    /// <summary>
    /// Gets a reproducible explanation of the selected update mode.
    /// </summary>
    public required string Explanation { get; init; }
}