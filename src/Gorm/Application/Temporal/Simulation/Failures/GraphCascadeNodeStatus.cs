namespace Gorm.Application.Temporal.Simulation.Failures;

/// <summary>
/// Contains final demand, capacity and failure evidence for a baseline node.
/// </summary>
public sealed class GraphCascadeNodeStatus
{
    /// <summary>
    /// Gets the node identifier.
    /// </summary>
    public required Guid NodeId { get; init; }

    /// <summary>
    /// Gets the configured node demand.
    /// </summary>
    public required double Demand { get; init; }

    /// <summary>
    /// Gets available incoming capacity after propagation.
    /// </summary>
    public required double AvailableIncomingCapacity { get; init; }

    /// <summary>
    /// Gets whether the node had no selected incoming edges in the baseline.
    /// </summary>
    public required bool IsSource { get; init; }

    /// <summary>
    /// Gets the failure round, or <see langword="null"/> when the node survived.
    /// </summary>
    public int? FailedRound { get; init; }
}