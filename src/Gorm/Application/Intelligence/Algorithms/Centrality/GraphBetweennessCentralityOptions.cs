using Gorm.Application.Intelligence.Algorithms.Abstractions;
using Gorm.Core.Primitives;

namespace Gorm.Application.Intelligence.Algorithms.Centrality;

/// <summary>
/// Configures betweenness-centrality execution.
/// </summary>
public sealed class GraphBetweennessCentralityOptions
{
    /// <summary>
    /// Gets or sets the directions that may be traversed.
    /// </summary>
    public GraphAlgorithmTraversalDirection Direction { get; set; } = GraphAlgorithmTraversalDirection.Outgoing;

    /// <summary>
    /// Gets or sets an optional strictly positive edge-weight selector. Every edge has weight 1 when omitted.
    /// </summary>
    public Func<Edge, double>? WeightSelector { get; set; }

    /// <summary>
    /// Gets or sets an optional edge predicate used to constrain the analysed graph.
    /// </summary>
    public Func<Edge, bool>? EdgePredicate { get; set; }
}