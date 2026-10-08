using System.Collections.ObjectModel;

namespace Gorm.Application.Temporal.Simulation.Decision;

/// <summary>
/// Contains deterministic multi-objective policy ranking.
/// </summary>
public sealed class GraphPolicyComparisonResult
{
    internal GraphPolicyComparisonResult(GraphPolicyScore[] ranking)
    {
        Ranking = Array.AsReadOnly(ranking);
        Best = ranking[0];
        Assumptions = "Scores use observed candidate minima/maxima and a compensatory weighted utility model; weights encode application policy.";
    }

    /// <summary>
    /// Gets candidates ordered by score descending then stable id.
    /// </summary>
    public ReadOnlyCollection<GraphPolicyScore> Ranking { get; }

    /// <summary>
    /// Gets the highest-ranked candidate.
    /// </summary>
    public GraphPolicyScore Best { get; }

    /// <summary>
    /// Gets decision-model assumptions.
    /// </summary>
    public string Assumptions { get; }
}