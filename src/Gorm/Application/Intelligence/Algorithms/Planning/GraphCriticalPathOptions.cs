using Gorm.Core.Primitives;

namespace Gorm.Application.Intelligence.Algorithms.Planning;

/// <summary>
/// Configures Critical Path Method analysis.
/// </summary>
public sealed class GraphCriticalPathOptions
{
    /// <summary>
    /// Gets or sets an optional non-negative node-duration selector. Node duration is zero when omitted.
    /// </summary>
    public Func<Node, double>? DurationSelector { get; set; }

    /// <summary>
    /// Gets or sets an optional non-negative edge-lag selector. Every edge has lag 1 when omitted.
    /// </summary>
    public Func<Edge, double>? LagSelector { get; set; }

    /// <summary>
    /// Gets or sets an optional edge predicate used to constrain the dependency graph.
    /// </summary>
    public Func<Edge, bool>? EdgePredicate { get; set; }

    /// <summary>
    /// Gets or sets the absolute floating-point tolerance used to classify critical nodes and edges.
    /// </summary>
    public double CriticalTolerance { get; set; } = 1e-9;
}