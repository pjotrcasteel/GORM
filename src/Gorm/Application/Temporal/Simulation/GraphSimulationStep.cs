using Gorm.Application.Temporal.Diff;

namespace Gorm.Application.Temporal.Simulation;

/// <summary>
/// Records one processed simulation event and its resulting world evidence.
/// </summary>
public sealed class GraphSimulationStep
{
    /// <summary>
    /// Gets the one-based execution sequence.
    /// </summary>
    public required int Sequence { get; init; }

    /// <summary>
    /// Gets the stable scheduled event identifier.
    /// </summary>
    public required string EventId { get; init; }

    /// <summary>
    /// Gets the absolute UTC simulation instant.
    /// </summary>
    public required DateTimeOffset SimulatedAt { get; init; }

    /// <summary>
    /// Gets the exact world change produced by the event.
    /// </summary>
    public required GraphWorldSnapshotDiff Difference { get; init; }

    /// <summary>
    /// Gets the resulting immutable snapshot identifier.
    /// </summary>
    public required string SnapshotId { get; init; }

    /// <summary>
    /// Gets the number of incident edges removed with a node.
    /// </summary>
    public required int CascadedEdgeRemovals { get; init; }
}