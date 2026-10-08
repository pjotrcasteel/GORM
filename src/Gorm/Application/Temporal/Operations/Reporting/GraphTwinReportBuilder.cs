using Gorm.Application.Temporal.Operations.Drift;
using Gorm.Application.Temporal.Operations.Prediction;
using Gorm.Application.Temporal.Operations.Warnings;

namespace Gorm.Application.Temporal.Operations.Reporting;

/// <summary>
/// Builds bounded canonical reports from existing Twin evidence.
/// </summary>
public static class GraphTwinReportBuilder
{
    /// <summary>
    /// Combines drift, warnings, predictions and scenarios without triggering new execution or output.
    /// </summary>
    public static GraphTwinEvidenceReport Create(CreateParameters inputs)
    {
        var reportId = inputs.ReportId;
        var createdAt = inputs.CreatedAt;
        var synchronization = inputs.Synchronization;
        var warnings = inputs.Warnings;
        var predictions = inputs.Predictions;
        var scenarios = inputs.Scenarios;
        var options = inputs.Options;
        var cancellationToken = inputs.CancellationToken;

        if (string.IsNullOrWhiteSpace(reportId))
        {
            throw new ArgumentException("Report id is required.", nameof(inputs));
        }

        ArgumentNullException.ThrowIfNull(synchronization);
        ArgumentNullException.ThrowIfNull(synchronization.Drift);
        cancellationToken.ThrowIfCancellationRequested();
        predictions ??= [];
        scenarios ??= [];
        options ??= new GraphTwinReportOptions();
        ValidateLimits(synchronization, warnings, predictions, scenarios, options);
        var drift = synchronization.Drift;
        var reportChanges = CreateChanges(drift, cancellationToken);
        var reportWarnings = CreateWarnings(warnings);
        var reportPredictions = CreatePredictions(predictions);
        var reportScenarios = scenarios
            .OrderBy(scenario => scenario.ScenarioId, StringComparer.Ordinal)
            .Select(scenario => NormalizeScenario(scenario, options, cancellationToken))
            .ToArray();
        var content = new GraphTwinReportContent
        {
            SchemaVersion = "gorm.temporal.report/1",
            ReportId = reportId.Trim(),
            CreatedAt = createdAt.ToUniversalTime(),
            ObservationSequence = synchronization.Sequence,
            WorldKey = drift.Expected.WorldKey.Value,
            ExpectedSnapshotId = drift.Expected.SnapshotId,
            ObservedSnapshotId = drift.Observed.SnapshotId,
            DriftSeverity = drift.Severity,
            EntityChangeRatio = drift.EntityChangeRatio,
            TopologyChanged = drift.Difference.TopologyChanged,
            Changes = reportChanges,
            Warnings = reportWarnings,
            Predictions = reportPredictions,
            Scenarios = reportScenarios,
            Summary = $"Twin report '{reportId.Trim()}': {reportChanges.Length} changes, {reportWarnings.Length} warnings, " +
                $"{reportPredictions.Length} predictions and {reportScenarios.Length} scenarios."
        };
        return new GraphTwinEvidenceReport
        {
            Content = content,
            ReportFingerprint = GraphTwinReportSerializer.Fingerprint(content)
        };
    }

    private static GraphTwinReportChange[] CreateChanges(GraphTwinDriftReport drift, CancellationToken cancellationToken) =>
        drift.Difference.Changes
            .OrderBy(change => change.EntityKind)
            .ThenBy(change => change.EntityId)
            .Select(change =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new GraphTwinReportChange
                {
                    EntityKind = change.EntityKind,
                    ChangeKind = change.ChangeKind,
                    EntityId = change.EntityId,
                    BeforeType = change.BeforeType?.AssemblyQualifiedName,
                    AfterType = change.AfterType?.AssemblyQualifiedName,
                    BeforeFingerprint = change.BeforeStateFingerprint,
                    AfterFingerprint = change.AfterStateFingerprint
                };
            })
            .ToArray();

    private static GraphTwinReportWarning[] CreateWarnings(GraphTwinWarningEvaluation? warnings) =>
        (warnings?.Warnings ?? [])
            .OrderByDescending(warning => warning.Severity)
            .ThenBy(warning => warning.RuleId, StringComparer.Ordinal)
            .Select(warning => new GraphTwinReportWarning
            {
                RuleId = warning.RuleId,
                Severity = warning.Severity,
                MetricValue = warning.MetricValue,
                Threshold = warning.Threshold,
                Explanation = warning.Explanation
            })
            .ToArray();

    private static GraphTwinReportPrediction[] CreatePredictions(IReadOnlyCollection<GraphThresholdPrediction> predictions) =>
        predictions
            .OrderBy(prediction => prediction.Threshold)
            .ThenBy(prediction => prediction.Direction)
            .ThenBy(prediction => prediction.PredictedBreachAt)
            .Select(prediction => new GraphTwinReportPrediction
            {
                Threshold = prediction.Threshold,
                Direction = prediction.Direction,
                SlopePerSecond = prediction.SlopePerSecond,
                RSquared = prediction.RSquared,
                AlreadyBreached = prediction.AlreadyBreached,
                PredictedBreachAt = prediction.PredictedBreachAt,
                WithinHorizon = prediction.WithinHorizon,
                Explanation = prediction.Explanation
            })
            .ToArray();

    private static GraphTwinReportScenario NormalizeScenario(GraphTwinScenarioEvidence scenario, GraphTwinReportOptions options, CancellationToken cancellationToken)
    {
        if (scenario is null || string.IsNullOrWhiteSpace(scenario.ScenarioId) || string.IsNullOrWhiteSpace(scenario.SnapshotId))
        {
            throw new ArgumentException("Scenario report identity is required.");
        }

        ArgumentNullException.ThrowIfNull(scenario.Metrics);
        ArgumentNullException.ThrowIfNull(scenario.Assumptions);
        if (scenario.Metrics.Count > options.MaximumMetricsPerScenario)
        {
            throw new InvalidOperationException(
                $"Scenario '{scenario.ScenarioId}' exceeds MaximumMetricsPerScenario ({options.MaximumMetricsPerScenario}).");
        }

        var metrics = scenario.Metrics.OrderBy(item => item.Key, StringComparer.Ordinal).Select(item =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(item.Key) || !double.IsFinite(item.Value))
            {
                throw new InvalidOperationException($"Scenario '{scenario.ScenarioId}' contains an invalid metric.");
            }

            return new GraphTwinReportMetric { Name = item.Key, Value = item.Value };
        }).ToArray();

        return new GraphTwinReportScenario
        {
            ScenarioId = scenario.ScenarioId,
            Revision = scenario.Revision,
            SnapshotId = scenario.SnapshotId,
            Metrics = metrics,
            Assumptions = [.. scenario.Assumptions.Order(StringComparer.Ordinal)]
        };
    }

    private static void ValidateLimits(
        GraphTwinSynchronizationResult synchronization,
        GraphTwinWarningEvaluation? warnings,
        IReadOnlyCollection<GraphThresholdPrediction> predictions,
        IReadOnlyCollection<GraphTwinScenarioEvidence> scenarios,
        GraphTwinReportOptions options)
    {
        if (options.MaximumChanges <= 0 || options.MaximumWarnings <= 0 || options.MaximumPredictions <= 0 ||
            options.MaximumScenarios <= 0 || options.MaximumMetricsPerScenario <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }

        if (synchronization.Drift.Difference.Changes.Count > options.MaximumChanges ||
            (warnings?.Warnings.Count ?? 0) > options.MaximumWarnings ||
            predictions.Count > options.MaximumPredictions ||
            scenarios.Count > options.MaximumScenarios)
        {
            throw new InvalidOperationException("Twin report evidence exceeds configured limits.");
        }

        if (scenarios.Select(scenario => scenario.ScenarioId).Distinct(StringComparer.Ordinal).Count() != scenarios.Count)
        {
            throw new ArgumentException("Scenario evidence ids must be unique.", nameof(scenarios));
        }
    }

    /// <summary>
    /// Groups the inputs for Create.
    /// </summary>
    public sealed class CreateParameters
    {
        /// <summary>
        /// Gets or initializes reportId.
        /// </summary>
        public required string ReportId { get; init; }

        /// <summary>
        /// Gets or initializes createdAt.
        /// </summary>
        public required DateTimeOffset CreatedAt { get; init; }

        /// <summary>
        /// Gets or initializes synchronization.
        /// </summary>
        public required GraphTwinSynchronizationResult Synchronization { get; init; }

        /// <summary>
        /// Gets or initializes warnings.
        /// </summary>
        public GraphTwinWarningEvaluation? Warnings { get; init; } = null;

        /// <summary>
        /// Gets or initializes predictions.
        /// </summary>
        public IReadOnlyCollection<GraphThresholdPrediction>? Predictions { get; init; } = null;

        /// <summary>
        /// Gets or initializes scenarios.
        /// </summary>
        public IReadOnlyCollection<GraphTwinScenarioEvidence>? Scenarios { get; init; } = null;

        /// <summary>
        /// Gets or initializes options.
        /// </summary>
        public GraphTwinReportOptions? Options { get; init; } = null;

        /// <summary>
        /// Gets or initializes cancellationToken.
        /// </summary>
        public CancellationToken CancellationToken { get; init; } = default;
    }
}