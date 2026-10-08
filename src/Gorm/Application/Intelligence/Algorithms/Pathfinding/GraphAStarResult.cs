namespace Gorm.Application.Intelligence.Algorithms.Pathfinding;

/// <summary>
/// Represents a successful A* search.
/// </summary>
public sealed class GraphAStarResult
{
    /// <summary>
    /// Gets the optimal route.
    /// </summary>
    public required GraphRoutePath Path { get; init; }

    /// <summary>
    /// Gets the number of dequeued non-stale search states.
    /// </summary>
    public required int ExploredNodeCount { get; init; }
}