namespace Gorm.Application.Intelligence.Algorithms.Centrality;

/// <summary>
/// Represents a PageRank execution result.
/// </summary>
public sealed class GraphPageRankResult
{
    private readonly Dictionary<Guid, double> _scoresByNodeId;

    internal GraphPageRankResult(IReadOnlyList<GraphPageRankScore> scores, int iterations, bool converged, double totalDelta)
    {
        Scores = scores;
        Iterations = iterations;
        Converged = converged;
        TotalDelta = totalDelta;
        _scoresByNodeId = scores.ToDictionary(score => score.Node.Id, score => score.Score);
    }

    /// <summary>
    /// Gets scores ordered from highest to lowest.
    /// </summary>
    public IReadOnlyList<GraphPageRankScore> Scores { get; }

    /// <summary>
    /// Gets the number of executed iterations.
    /// </summary>
    public int Iterations { get; }

    /// <summary>
    /// Gets a value indicating whether execution met the configured tolerance.
    /// </summary>
    public bool Converged { get; }

    /// <summary>
    /// Gets the total score delta of the final iteration.
    /// </summary>
    public double TotalDelta { get; }

    /// <summary>
    /// Gets the score for a projected node identifier.
    /// </summary>
    public double GetScore(Guid nodeId) =>
        _scoresByNodeId.TryGetValue(nodeId, out var score)
            ? score
            : throw new KeyNotFoundException($"Node '{nodeId}' has no PageRank score.");
}