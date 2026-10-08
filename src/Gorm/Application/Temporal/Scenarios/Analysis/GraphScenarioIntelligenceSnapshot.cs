namespace Gorm.Application.Temporal.Scenarios.Analysis;

/// <summary>
/// Contains topology and optional Intelligence evidence for one compared world.
/// </summary>
public sealed class GraphScenarioIntelligenceSnapshot
{
    /// <summary>
    /// Gets the projected node count.
    /// </summary>
    public required int NodeCount { get; init; }

    /// <summary>
    /// Gets the projected edge count.
    /// </summary>
    public required int EdgeCount { get; init; }

    /// <summary>
    /// Gets directed graph density.
    /// </summary>
    public required double Density { get; init; }

    /// <summary>
    /// Gets the number of weakly connected components.
    /// </summary>
    public required int WeakComponentCount { get; init; }

    /// <summary>
    /// Gets project duration when Critical Path Method was requested and the graph was acyclic.
    /// </summary>
    public double? CriticalPathDuration { get; init; }

    /// <summary>
    /// Gets the number of critical nodes when Critical Path Method succeeded.
    /// </summary>
    public int? CriticalNodeCount { get; init; }

    /// <summary>
    /// Gets why Critical Path Method was unavailable, when requested.
    /// </summary>
    public string? CriticalPathUnavailableReason { get; init; }

    /// <summary>
    /// Gets the final deterministic community count when requested.
    /// </summary>
    public int? CommunityCount { get; init; }

    /// <summary>
    /// Gets final deterministic modularity when requested.
    /// </summary>
    public double? Modularity { get; init; }

    /// <summary>
    /// Gets outgoing reachable nodes including the source, when impact reach was requested.
    /// </summary>
    public int? ImpactReachableNodeCount { get; init; }

    /// <summary>
    /// Gets whether the requested impact source existed in this world.
    /// </summary>
    public bool? ImpactSourceAvailable { get; init; }
}