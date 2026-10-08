namespace Gorm.Application.Intelligence.Algorithms.Centrality;

/// <summary>
/// Represents degree centrality scores ordered from highest to lowest total centrality.
/// </summary>
public sealed class GraphDegreeCentralityResult
{
    internal GraphDegreeCentralityResult(IReadOnlyList<GraphDegreeCentralityScore> scores)
    {
        Scores = scores;
    }

    /// <summary>
    /// Gets the calculated scores.
    /// </summary>
    public IReadOnlyList<GraphDegreeCentralityScore> Scores { get; }
}