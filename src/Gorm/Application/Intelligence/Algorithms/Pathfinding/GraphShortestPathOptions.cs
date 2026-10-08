using Gorm.Application.Intelligence.Algorithms.Abstractions;
using Gorm.Core.Primitives;

namespace Gorm.Application.Intelligence.Algorithms.Pathfinding;

/// <summary>
/// Configures shortest-path execution.
/// </summary>
public sealed class GraphShortestPathOptions
{
    /// <summary>
    /// Gets or sets the directions that may be traversed.
    /// </summary>
    public GraphAlgorithmTraversalDirection Direction { get; set; } = GraphAlgorithmTraversalDirection.Outgoing;

    /// <summary>
    /// Gets or sets an optional non-negative edge weight selector. Every edge has weight 1 when omitted.
    /// </summary>
    public Func<Edge, double>? WeightSelector { get; set; }

    /// <summary>
    /// Gets or sets an optional edge predicate used to constrain the path.
    /// </summary>
    public Func<Edge, bool>? EdgePredicate { get; set; }
}