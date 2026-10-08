namespace Gorm.Application.Temporal.Simulation.Failures;

/// <summary>
/// Describes the nodes that fail simultaneously in one deterministic cascade round.
/// </summary>
public sealed class GraphCascadeRound
{
    /// <summary>
    /// Gets the zero-based round; zero contains explicitly seeded failures.
    /// </summary>
    public required int Round { get; init; }

    /// <summary>
    /// Gets the failed node identifiers in stable order.
    /// </summary>
    public required Guid[] FailedNodeIds { get; init; }
}