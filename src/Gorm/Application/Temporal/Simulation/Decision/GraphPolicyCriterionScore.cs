namespace Gorm.Application.Temporal.Simulation.Decision;

/// <summary>
/// Explains one criterion's contribution to a policy score.
/// </summary>
public sealed class GraphPolicyCriterionScore
{
    /// <summary>
    /// Gets the criterion name.
    /// </summary>
    public required string CriterionName { get; init; }

    /// <summary>
    /// Gets the original candidate metric.
    /// </summary>
    public required double Value { get; init; }

    /// <summary>
    /// Gets the normalized utility from zero through one.
    /// </summary>
    public required double Utility { get; init; }

    /// <summary>
    /// Gets utility multiplied by criterion weight.
    /// </summary>
    public required double WeightedUtility { get; init; }
}