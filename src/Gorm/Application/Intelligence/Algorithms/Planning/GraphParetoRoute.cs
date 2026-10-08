using Gorm.Core.Primitives;

namespace Gorm.Application.Intelligence.Algorithms.Planning;

/// <summary>
/// Represents one non-dominated route and its normalized ranking.
/// </summary>
public sealed class GraphParetoRoute
{
    /// <summary>
    /// Gets zero-based rank within the returned Pareto frontier.
    /// </summary>
    public required int Rank { get; init; }

    /// <summary>
    /// Gets ordered route nodes.
    /// </summary>
    public required IReadOnlyList<Node> Nodes { get; init; }

    /// <summary>
    /// Gets ordered route edges.
    /// </summary>
    public required IReadOnlyList<Edge> Edges { get; init; }

    /// <summary>
    /// Gets accumulated raw metric values by criterion name.
    /// </summary>
    public required IReadOnlyDictionary<string, double> Metrics { get; init; }

    /// <summary>
    /// Gets normalized weighted loss used to order non-dominated routes. Lower is better.
    /// </summary>
    public required double RankingScore { get; init; }

    /// <summary>
    /// Gets an explanation of Pareto membership and ranking.
    /// </summary>
    public required string Explanation { get; init; }
}