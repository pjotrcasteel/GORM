namespace Gorm.Application.Temporal.Operations.Reporting;

/// <summary>
/// Bounds report evidence captured for desktop or external consumers.
/// </summary>
public sealed class GraphTwinReportOptions
{
    /// <summary>
    /// Gets or sets maximum entity changes.
    /// </summary>
    public int MaximumChanges { get; init; } = 1_000_000;

    /// <summary>
    /// Gets or sets maximum warnings.
    /// </summary>
    public int MaximumWarnings { get; init; } = 100_000;

    /// <summary>
    /// Gets or sets maximum predictions.
    /// </summary>
    public int MaximumPredictions { get; init; } = 100_000;

    /// <summary>
    /// Gets or sets maximum scenario summaries.
    /// </summary>
    public int MaximumScenarios { get; init; } = 10_000;

    /// <summary>
    /// Gets or sets maximum metrics per scenario.
    /// </summary>
    public int MaximumMetricsPerScenario { get; init; } = 10_000;
}