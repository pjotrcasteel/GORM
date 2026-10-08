namespace Gorm.Application.Temporal.Simulation.Decision;

/// <summary>
/// Defines one weighted policy objective.
/// </summary>
public sealed class GraphPolicyCriterion
{
    /// <summary>
    /// Gets the stable metric name expected from every candidate.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets whether smaller or larger values are preferable.
    /// </summary>
    public required GraphPolicyGoal Goal { get; init; }

    /// <summary>
    /// Gets the finite positive decision weight.
    /// </summary>
    public double Weight { get; init; } = 1;
}