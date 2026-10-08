namespace Gorm.Application.Temporal.Operations.Warnings;

/// <summary>
/// Defines how a finite warning metric is compared with its threshold.
/// </summary>
public enum GraphThresholdComparison
{
    /// <summary>
    /// Metric must be greater than the threshold.
    /// </summary>
    GreaterThan,

    /// <summary>
    /// Metric must be greater than or equal to the threshold.
    /// </summary>
    GreaterThanOrEqual,

    /// <summary>
    /// Metric must be less than the threshold.
    /// </summary>
    LessThan,

    /// <summary>
    /// Metric must be less than or equal to the threshold.
    /// </summary>
    LessThanOrEqual
}