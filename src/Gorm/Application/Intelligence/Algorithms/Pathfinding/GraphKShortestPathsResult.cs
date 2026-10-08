namespace Gorm.Application.Intelligence.Algorithms.Pathfinding;

/// <summary>
/// Represents an ordered Yen K-shortest loopless-path result.
/// </summary>
public sealed class GraphKShortestPathsResult
{
    internal GraphKShortestPathsResult(IReadOnlyList<GraphRoutePath> paths, int spurSearches, bool exhausted, bool truncated)
    {
        Paths = paths;
        SpurSearches = spurSearches;
        Exhausted = exhausted;
        Truncated = truncated;
    }

    /// <summary>
    /// Gets unique loopless paths ordered by cost and deterministic topology signature.
    /// </summary>
    public IReadOnlyList<GraphRoutePath> Paths { get; }

    /// <summary>
    /// Gets the number of executed spur shortest-path searches.
    /// </summary>
    public int SpurSearches { get; }

    /// <summary>
    /// Gets a value indicating whether no additional loopless path exists.
    /// </summary>
    public bool Exhausted { get; }

    /// <summary>
    /// Gets a value indicating whether the spur-search safety limit stopped execution.
    /// </summary>
    public bool Truncated { get; }
}