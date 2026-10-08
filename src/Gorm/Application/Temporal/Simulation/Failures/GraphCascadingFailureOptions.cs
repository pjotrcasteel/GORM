using Gorm.Core.Primitives;

namespace Gorm.Application.Temporal.Simulation.Failures;

/// <summary>
/// Controls deterministic capacity-driven cascading failure simulation.
/// </summary>
public sealed class GraphCascadingFailureOptions
{
    /// <summary>
    /// Gets or sets node identifiers failed before propagation begins.
    /// </summary>
    public IReadOnlyCollection<Guid> InitiallyFailedNodeIds { get; init; } = [];

    /// <summary>
    /// Gets or sets required incoming capacity per node.
    /// </summary>
    public required Func<Node, double> DemandSelector { get; init; }

    /// <summary>
    /// Gets or sets available capacity per directed edge.
    /// </summary>
    public required Func<Edge, double> CapacitySelector { get; init; }

    /// <summary>
    /// Gets or sets an optional edge availability predicate.
    /// </summary>
    public Func<Edge, bool>? EdgePredicate { get; init; }

    /// <summary>
    /// Gets or sets whether nodes with no selected incoming edges are treated as protected sources.
    /// </summary>
    public bool ProtectSourceNodes { get; init; } = true;

    /// <summary>
    /// Gets or sets the maximum propagation rounds.
    /// </summary>
    public int MaximumRounds { get; init; } = 10_000;

    /// <summary>
    /// Gets or sets the maximum failed nodes including initial failures.
    /// </summary>
    public int MaximumFailures { get; init; } = 1_000_000;

    /// <summary>
    /// Gets or sets finite non-negative comparison tolerance.
    /// </summary>
    public double Epsilon { get; init; } = 1e-9;

    /// <summary>
    /// Gets or sets optional maximum-flow source for capacity-loss evidence.
    /// </summary>
    public Guid? FlowSourceNodeId { get; init; }

    /// <summary>
    /// Gets or sets optional maximum-flow destination for capacity-loss evidence.
    /// </summary>
    public Guid? FlowDestinationNodeId { get; init; }

    /// <summary>
    /// Gets or sets the maximum augmenting paths for each flow calculation.
    /// </summary>
    public int MaximumFlowAugmentations { get; init; } = 1_000_000;
}