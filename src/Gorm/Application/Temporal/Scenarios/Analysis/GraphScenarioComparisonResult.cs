using System.Collections.ObjectModel;
using Gorm.Application.Temporal.Diff;

namespace Gorm.Application.Temporal.Scenarios.Analysis;

/// <summary>
/// Combines topology, built-in Intelligence and domain metrics for a scenario decision.
/// </summary>
public sealed class GraphScenarioComparisonResult
{
    internal GraphScenarioComparisonResult(
        GraphScenarioId scenarioId,
        GraphWorldSnapshotDiff topologyDifference,
        GraphScenarioIntelligenceSnapshot baseline,
        GraphScenarioIntelligenceSnapshot scenario,
        GraphScenarioMetricDelta[] metrics)
    {
        ScenarioId = scenarioId;
        TopologyDifference = topologyDifference;
        Baseline = baseline;
        Scenario = scenario;
        Metrics = Array.AsReadOnly(metrics);
        Explanation =
            $"Scenario '{scenarioId.Value}' compared {metrics.Length} metrics with " +
            $"{topologyDifference.Changes.Count} entity changes from baseline.";
    }

    /// <summary>
    /// Gets the compared scenario identifier.
    /// </summary>
    public GraphScenarioId ScenarioId { get; }

    /// <summary>
    /// Gets exact entity-level changes from the baseline.
    /// </summary>
    public GraphWorldSnapshotDiff TopologyDifference { get; }

    /// <summary>
    /// Gets baseline topology and Intelligence evidence.
    /// </summary>
    public GraphScenarioIntelligenceSnapshot Baseline { get; }

    /// <summary>
    /// Gets scenario topology and Intelligence evidence.
    /// </summary>
    public GraphScenarioIntelligenceSnapshot Scenario { get; }

    /// <summary>
    /// Gets numeric metric deltas in stable name order.
    /// </summary>
    public ReadOnlyCollection<GraphScenarioMetricDelta> Metrics { get; }

    /// <summary>
    /// Gets a reproducible comparison explanation.
    /// </summary>
    public string Explanation { get; }
}