using Gorm.Application.Intelligence.Algorithms.Pathfinding;

namespace Gorm.Application.Intelligence.Algorithms.Planning;

/// <summary>
/// Represents the routing impact of failed nodes and edges.
/// </summary>
public sealed class GraphImpactReroutingResult
{
    /// <summary>
    /// Gets the optimal route before configured failures, or null when no baseline route exists.
    /// </summary>
    public required GraphRoutePath? BaselinePath { get; init; }

    /// <summary>
    /// Gets the recommended optimal route after failures, or null when the destination is unavailable.
    /// </summary>
    public required GraphRoutePath? RecommendedPath { get; init; }

    /// <summary>
    /// Gets available loopless alternatives ordered by cost. The first item is the recommendation.
    /// </summary>
    public required IReadOnlyList<GraphRoutePath> AlternativePaths { get; init; }

    /// <summary>
    /// Gets added route cost compared with the baseline, or null when either route is unavailable.
    /// </summary>
    public required double? AdditionalCost { get; init; }

    /// <summary>
    /// Gets added hop count compared with the baseline, or null when either route is unavailable.
    /// </summary>
    public required int? AdditionalHopCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether a post-failure route exists.
    /// </summary>
    public bool RouteAvailable => RecommendedPath is not null;

    /// <summary>
    /// Gets an explicit impact and recommendation explanation.
    /// </summary>
    public required string Explanation { get; init; }
}