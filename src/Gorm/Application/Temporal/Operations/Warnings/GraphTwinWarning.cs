namespace Gorm.Application.Temporal.Operations.Warnings;

/// <summary>
/// Contains the metric and comparison evidence that triggered one warning.
/// </summary>
public sealed class GraphTwinWarning
{
    /// <summary>
    /// Gets the matching rule id.
    /// </summary>
    public required string RuleId { get; init; }

    /// <summary>
    /// Gets warning severity.
    /// </summary>
    public required GraphTwinWarningSeverity Severity { get; init; }

    /// <summary>
    /// Gets the observed finite metric.
    /// </summary>
    public required double MetricValue { get; init; }

    /// <summary>
    /// Gets the configured threshold.
    /// </summary>
    public required double Threshold { get; init; }

    /// <summary>
    /// Gets the comparison operator.
    /// </summary>
    public required GraphThresholdComparison Comparison { get; init; }

    /// <summary>
    /// Gets deterministic evidence text.
    /// </summary>
    public required string Explanation { get; init; }
}