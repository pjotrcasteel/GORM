using Gorm.Core.Primitives;

namespace Gorm.Application.Intelligence.Algorithms.Communities;

/// <summary>
/// Configures deterministic weighted Leiden community detection.
/// </summary>
public sealed class GraphCommunityDetectionOptions
{
    /// <summary>
    /// Gets or sets how selected edges are interpreted.
    /// </summary>
    public GraphCommunityEdgeMode EdgeMode { get; set; } = GraphCommunityEdgeMode.Undirected;

    /// <summary>
    /// Gets or sets an optional non-negative edge-weight selector. Every selected edge has weight 1 when omitted.
    /// Zero-weight edges do not influence communities.
    /// </summary>
    public Func<Edge, double>? WeightSelector { get; set; }

    /// <summary>
    /// Gets or sets an optional edge predicate used to select the analysed subgraph.
    /// </summary>
    public Func<Edge, bool>? EdgePredicate { get; set; }

    /// <summary>
    /// Gets or sets the modularity resolution. Larger values generally produce smaller communities.
    /// </summary>
    public double Resolution { get; set; } = 1;

    /// <summary>
    /// Gets or sets the deterministic seed used to order local-moving passes.
    /// </summary>
    public int RandomSeed { get; set; } = 42;

    /// <summary>
    /// Gets or sets the maximum number of hierarchy levels.
    /// </summary>
    public int MaximumLevels { get; set; } = 20;

    /// <summary>
    /// Gets or sets the maximum local-moving passes per hierarchy level.
    /// </summary>
    public int MaximumLocalMovingPasses { get; set; } = 100;

    /// <summary>
    /// Gets or sets the minimum modularity increase required to move a vertex.
    /// </summary>
    public double MinimumModularityGain { get; set; } = 1e-12;

    /// <summary>
    /// Gets or sets the minimum cross-community anomaly score in the range zero through one.
    /// </summary>
    public double AnomalyThreshold { get; set; } = 0.65;
}