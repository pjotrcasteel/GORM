using Gorm.Core.Primitives;

namespace Gorm.Application.Intelligence.Algorithms.Centrality;

/// <summary>
/// Represents the betweenness-centrality score of a projected node.
/// </summary>
public sealed class GraphBetweennessCentralityScore
{
    /// <summary>
    /// Gets the projected node.
    /// </summary>
    public required Node Node { get; init; }

    /// <summary>
    /// Gets the zero-based position after sorting scores from high to low.
    /// </summary>
    public required int Rank { get; init; }

    /// <summary>
    /// Gets the raw number of shortest-path dependencies passing through the node.
    /// </summary>
    public required double Score { get; init; }

    /// <summary>
    /// Gets the score normalized to the range from zero through one when the graph has at least three nodes.
    /// </summary>
    public required double NormalizedScore { get; init; }
}