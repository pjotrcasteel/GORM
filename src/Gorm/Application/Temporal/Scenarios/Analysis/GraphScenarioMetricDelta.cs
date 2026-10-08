namespace Gorm.Application.Temporal.Scenarios.Analysis;

/// <summary>
/// Compares one named numeric metric between baseline and scenario.
/// </summary>
public sealed class GraphScenarioMetricDelta
{
    /// <summary>
    /// Gets the stable metric name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the application-readable unit.
    /// </summary>
    public required string Unit { get; init; }

    /// <summary>
    /// Gets the baseline metric.
    /// </summary>
    public required double BaselineValue { get; init; }

    /// <summary>
    /// Gets the scenario metric.
    /// </summary>
    public required double ScenarioValue { get; init; }

    /// <summary>
    /// Gets scenario minus baseline.
    /// </summary>
    public double Delta => ScenarioValue - BaselineValue;
}