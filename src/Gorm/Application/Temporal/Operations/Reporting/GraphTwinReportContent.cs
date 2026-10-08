using Gorm.Application.Temporal.Operations.Drift;

namespace Gorm.Application.Temporal.Operations.Reporting;

/// <summary>
/// Contains canonical report content protected by the outer fingerprint.
/// </summary>
public sealed class GraphTwinReportContent
{
    /// <summary>
    /// Gets stable schema id.
    /// </summary>
    public required string SchemaVersion { get; init; }

    /// <summary>
    /// Gets application report id.
    /// </summary>
    public required string ReportId { get; init; }

    /// <summary>
    /// Gets normalized report creation time.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Gets live observation sequence.
    /// </summary>
    public required long ObservationSequence { get; init; }

    /// <summary>
    /// Gets logical world key.
    /// </summary>
    public required string WorldKey { get; init; }

    /// <summary>
    /// Gets expected snapshot id.
    /// </summary>
    public required string ExpectedSnapshotId { get; init; }

    /// <summary>
    /// Gets observed snapshot id.
    /// </summary>
    public required string ObservedSnapshotId { get; init; }

    /// <summary>
    /// Gets drift severity.
    /// </summary>
    public required GraphTwinDriftSeverity DriftSeverity { get; init; }

    /// <summary>
    /// Gets entity change ratio.
    /// </summary>
    public required double EntityChangeRatio { get; init; }

    /// <summary>
    /// Gets whether topology changed.
    /// </summary>
    public required bool TopologyChanged { get; init; }

    /// <summary>
    /// Gets exact entity changes.
    /// </summary>
    public required IReadOnlyList<GraphTwinReportChange> Changes { get; init; }

    /// <summary>
    /// Gets triggered warnings.
    /// </summary>
    public required IReadOnlyList<GraphTwinReportWarning> Warnings { get; init; }

    /// <summary>
    /// Gets threshold predictions.
    /// </summary>
    public required IReadOnlyList<GraphTwinReportPrediction> Predictions { get; init; }

    /// <summary>
    /// Gets scenario evidence.
    /// </summary>
    public required IReadOnlyList<GraphTwinReportScenario> Scenarios { get; init; }

    /// <summary>
    /// Gets deterministic human-readable summary.
    /// </summary>
    public required string Summary { get; init; }
}