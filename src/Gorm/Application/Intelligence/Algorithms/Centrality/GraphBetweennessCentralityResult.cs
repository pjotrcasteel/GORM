namespace Gorm.Application.Intelligence.Algorithms.Centrality;

/// <summary>
/// Represents a betweenness-centrality execution result.
/// </summary>
public sealed class GraphBetweennessCentralityResult
{
    private readonly Dictionary<Guid, GraphBetweennessCentralityScore> _scoresByNodeId;

    internal GraphBetweennessCentralityResult(IReadOnlyList<GraphBetweennessCentralityScore> scores)
    {
        Scores = scores;
        _scoresByNodeId = scores.ToDictionary(score => score.Node.Id);
    }

    /// <summary>
    /// Gets scores ordered from highest to lowest.
    /// </summary>
    public IReadOnlyList<GraphBetweennessCentralityScore> Scores { get; }

    /// <summary>
    /// Gets the score for a projected node identifier.
    /// </summary>
    public GraphBetweennessCentralityScore GetScore(Guid nodeId) =>
        _scoresByNodeId.TryGetValue(nodeId, out var score)
            ? score
            : throw new KeyNotFoundException($"Node '{nodeId}' has no betweenness-centrality score.");
}