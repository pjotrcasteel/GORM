namespace Gorm.Application.Temporal.Operations.Reporting;

/// <summary>
/// Serializable scenario revision evidence.
/// </summary>
public sealed class GraphTwinReportScenario
{
    /// <summary>
    /// Gets scenario id.
    /// </summary>
    public required string ScenarioId { get; init; }

    /// <summary>
    /// Gets scenario revision.
    /// </summary>
    public required int Revision { get; init; }

    /// <summary>
    /// Gets final snapshot id.
    /// </summary>
    public required string SnapshotId { get; init; }

    /// <summary>
    /// Gets metrics ordered by name.
    /// </summary>
    public required IReadOnlyList<GraphTwinReportMetric> Metrics { get; init; }

    /// <summary>
    /// Gets assumptions in ordinal order.
    /// </summary>
    public required IReadOnlyList<string> Assumptions { get; init; }
}