using Gorm.Application.Temporal.Operations.Drift;

namespace Gorm.Application.Temporal.Operations.Warnings;

/// <summary>
/// Defines one explainable threshold rule over a drift report.
/// </summary>
public sealed class GraphTwinWarningRule
{
    /// <summary>
    /// Gets the stable rule identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the warning severity when the rule matches.
    /// </summary>
    public required GraphTwinWarningSeverity Severity { get; init; }

    /// <summary>
    /// Gets the pure finite metric selector.
    /// </summary>
    public required Func<GraphTwinDriftReport, double> MetricSelector { get; init; }

    /// <summary>
    /// Gets the comparison operator.
    /// </summary>
    public required GraphThresholdComparison Comparison { get; init; }

    /// <summary>
    /// Gets the finite threshold.
    /// </summary>
    public required double Threshold { get; init; }

    /// <summary>
    /// Gets an optional runbook-oriented description.
    /// </summary>
    public string? Description { get; init; }
}