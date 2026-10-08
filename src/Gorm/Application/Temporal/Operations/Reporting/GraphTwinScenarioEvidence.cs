using Gorm.Application.Temporal.Scenarios;

namespace Gorm.Application.Temporal.Operations.Reporting;

/// <summary>
/// Captures application metrics and assumptions for one immutable scenario revision.
/// </summary>
public sealed class GraphTwinScenarioEvidence
{
    private GraphTwinScenarioEvidence()
    {
    }

    /// <summary>
    /// Gets scenario id.
    /// </summary>
    public required string ScenarioId { get; init; }

    /// <summary>
    /// Gets immutable scenario revision.
    /// </summary>
    public required int Revision { get; init; }

    /// <summary>
    /// Gets final snapshot id.
    /// </summary>
    public required string SnapshotId { get; init; }

    /// <summary>
    /// Gets application-owned finite metrics.
    /// </summary>
    public required IReadOnlyDictionary<string, double> Metrics { get; init; }

    /// <summary>
    /// Gets visible model assumptions.
    /// </summary>
    public IReadOnlyCollection<string> Assumptions { get; init; } = [];

    /// <summary>
    /// Captures identity from a scenario without mutating it.
    /// </summary>
    public static GraphTwinScenarioEvidence Capture(GraphScenario scenario, IReadOnlyDictionary<string, double>? metrics = null, IReadOnlyCollection<string>? assumptions = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        return new GraphTwinScenarioEvidence
        {
            ScenarioId = scenario.Id.Value,
            Revision = scenario.Revision,
            SnapshotId = scenario.Current.Identity.SnapshotId,
            Metrics = metrics ?? new Dictionary<string, double>(),
            Assumptions = assumptions ?? []
        };
    }
}