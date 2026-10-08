using Gorm.Application.Intelligence.Projection;

namespace Gorm.Application.Temporal.Scenarios.Analysis;

/// <summary>
/// Defines one deterministic domain metric evaluated against baseline and scenario projections.
/// </summary>
public sealed class GraphScenarioMetricDefinition
{
    /// <summary>
    /// Gets the stable metric name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the application-readable unit.
    /// </summary>
    public string Unit { get; init; } = string.Empty;

    /// <summary>
    /// Gets the pure projection selector used for baseline and scenario.
    /// </summary>
    public required Func<GraphProjection, double> Selector { get; init; }
}