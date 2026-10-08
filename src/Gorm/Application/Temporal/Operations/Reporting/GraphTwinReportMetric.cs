namespace Gorm.Application.Temporal.Operations.Reporting;

/// <summary>
/// Serializable named scenario metric.
/// </summary>
public sealed class GraphTwinReportMetric
{
    /// <summary>
    /// Gets metric name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets finite value.
    /// </summary>
    public required double Value { get; init; }
}