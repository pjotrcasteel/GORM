using Gorm.Application.Intelligence.Algorithms.Abstractions;
using Gorm.Core.Primitives;

namespace Gorm.Application.Intelligence.Algorithms.Pathfinding;

/// <summary>
/// Configures A* optimal-path search.
/// </summary>
public sealed class GraphAStarOptions
{
    /// <summary>
    /// Gets or sets allowed traversal directions.
    /// </summary>
    public GraphAlgorithmTraversalDirection Direction { get; set; } = GraphAlgorithmTraversalDirection.Outgoing;

    /// <summary>
    /// Gets or sets an optional non-negative edge-cost selector. Every selected edge costs 1 when omitted.
    /// </summary>
    public Func<Edge, double>? CostSelector { get; set; }

    /// <summary>
    /// Gets or sets an optional edge availability predicate.
    /// </summary>
    public Func<Edge, bool>? EdgePredicate { get; set; }

    /// <summary>
    /// Gets or sets an optional finite non-negative admissible cost estimate from a node to the destination.
    /// A missing heuristic produces Dijkstra behaviour.
    /// </summary>
    public Func<Node, Node, double>? Heuristic { get; set; }
}