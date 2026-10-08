using Gorm.Application.Temporal.Operations.Warnings;

namespace Gorm.Application.Temporal.Operations.Reporting;

/// <summary>
/// Serializable warning evidence.
/// </summary>
public sealed class GraphTwinReportWarning
{
    /// <summary>
    /// Gets rule id.
    /// </summary>
    public required string RuleId { get; init; }

    /// <summary>
    /// Gets warning severity.
    /// </summary>
    public required GraphTwinWarningSeverity Severity { get; init; }

    /// <summary>
    /// Gets observed metric.
    /// </summary>
    public required double MetricValue { get; init; }

    /// <summary>
    /// Gets threshold.
    /// </summary>
    public required double Threshold { get; init; }

    /// <summary>
    /// Gets explanation.
    /// </summary>
    public required string Explanation { get; init; }
}