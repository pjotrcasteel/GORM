using Gorm.Application.Intelligence.Algorithms.Abstractions;
using Gorm.Core.Primitives;

namespace Gorm.Application.Intelligence.Algorithms.Planning;

/// <summary>
/// Configures impact-aware optimal rerouting around failed topology.
/// </summary>
public sealed class GraphImpactReroutingOptions
{
    /// <summary>
    /// Gets or sets allowed traversal directions.
    /// </summary>
    public GraphAlgorithmTraversalDirection Direction { get; set; } = GraphAlgorithmTraversalDirection.Outgoing;

    /// <summary>
    /// Gets or sets an optional non-negative edge-cost selector. Every available edge costs 1 when omitted.
    /// </summary>
    public Func<Edge, double>? CostSelector { get; set; }

    /// <summary>
    /// Gets or sets an optional base edge availability predicate applied before failures.
    /// </summary>
    public Func<Edge, bool>? EdgePredicate { get; set; }

    /// <summary>
    /// Gets or sets an optional admissible A* heuristic for the recommended route.
    /// </summary>
    public Func<Node, Node, double>? Heuristic { get; set; }

    /// <summary>
    /// Gets failed projected node identifiers.
    /// </summary>
    public ISet<Guid> FailedNodeIds { get; } = new HashSet<Guid>();

    /// <summary>
    /// Gets failed projected edge identifiers.
    /// </summary>
    public ISet<Guid> FailedEdgeIds { get; } = new HashSet<Guid>();

    /// <summary>
    /// Gets or sets the maximum number of available loopless alternatives.
    /// </summary>
    public int MaximumAlternativePaths { get; set; } = 3;

    /// <summary>
    /// Gets or sets the maximum number of Yen spur searches used to enumerate alternatives.
    /// </summary>
    public int MaximumSpurSearches { get; set; } = 10_000;
}