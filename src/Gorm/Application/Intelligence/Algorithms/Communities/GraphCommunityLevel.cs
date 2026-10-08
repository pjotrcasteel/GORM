namespace Gorm.Application.Intelligence.Algorithms.Communities;

/// <summary>
/// Represents one level in the detected community hierarchy.
/// </summary>
public sealed class GraphCommunityLevel
{
    /// <summary>
    /// Gets the zero-based hierarchy level.
    /// </summary>
    public required int Level { get; init; }

    /// <summary>
    /// Gets directed or undirected modularity at this level.
    /// </summary>
    public required double Modularity { get; init; }

    /// <summary>
    /// Gets communities at this hierarchy level.
    /// </summary>
    public required IReadOnlyList<GraphCommunity> Communities { get; init; }
}