namespace Gorm.Application.Temporal.Simulation.Decision;

/// <summary>
/// Defines whether a policy criterion rewards smaller or larger values.
/// </summary>
public enum GraphPolicyGoal
{
    /// <summary>
    /// Smaller values are preferable.
    /// </summary>
    Minimize,

    /// <summary>
    /// Larger values are preferable.
    /// </summary>
    Maximize
}