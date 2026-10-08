namespace Gorm.Application.Intelligence.Algorithms.Planning;

/// <summary>
/// Defines whether a route metric is minimized or maximized.
/// </summary>
public enum GraphOptimizationGoal
{
    /// <summary>
    /// Lower accumulated values are preferred.
    /// </summary>
    Minimize = 0,

    /// <summary>
    /// Higher accumulated values are preferred.
    /// </summary>
    Maximize = 1
}