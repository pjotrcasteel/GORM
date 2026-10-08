using Gorm.Application.Intelligence.Algorithms.Abstractions;
using Gorm.Core.Primitives;

namespace Gorm.Application.Intelligence.Algorithms.Pathfinding;

/// <summary>
/// Configures Yen K-shortest loopless-path search.
/// </summary>
public sealed class GraphKShortestPathsOptions
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
    /// Gets or sets the maximum number of ordered loopless paths returned.
    /// </summary>
    public int MaximumPaths { get; set; } = 10;

    /// <summary>
    /// Gets or sets the maximum number of spur shortest-path searches.
    /// </summary>
    public int MaximumSpurSearches { get; set; } = 10_000;
}