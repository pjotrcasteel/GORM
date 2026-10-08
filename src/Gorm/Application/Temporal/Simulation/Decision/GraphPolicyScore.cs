namespace Gorm.Application.Temporal.Simulation.Decision;

/// <summary>
/// Contains a ranked policy and full normalized decision evidence.
/// </summary>
public sealed class GraphPolicyScore
{
    /// <summary>
    /// Gets the one-based rank.
    /// </summary>
    public required int Rank { get; init; }

    /// <summary>
    /// Gets the candidate id.
    /// </summary>
    public required string CandidateId { get; init; }

    /// <summary>
    /// Gets the normalized weighted score from zero through one.
    /// </summary>
    public required double Score { get; init; }

    /// <summary>
    /// Gets criterion evidence in criterion-name order.
    /// </summary>
    public required IReadOnlyList<GraphPolicyCriterionScore> Criteria { get; init; }

    /// <summary>
    /// Gets a deterministic explanation.
    /// </summary>
    public required string Explanation { get; init; }
}