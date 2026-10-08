using Gorm.Core.Primitives;

namespace Gorm.Application.Intelligence.Algorithms.Centrality;

/// <summary>
/// Represents the PageRank score of a node.
/// </summary>
public sealed class GraphPageRankScore
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
    /// Gets the normalized PageRank score.
    /// </summary>
    public required double Score { get; init; }
}