namespace Gorm.Application.Temporal.Simulation.Decision;

/// <summary>
/// Bounds policy comparison search space.
/// </summary>
public sealed class GraphPolicyComparisonOptions
{
    /// <summary>
    /// Gets or sets the maximum candidate count.
    /// </summary>
    public int MaximumCandidates { get; init; } = 1_000;

    /// <summary>
    /// Gets or sets the maximum criterion count.
    /// </summary>
    public int MaximumCriteria { get; init; } = 100;
}