using Gorm.Application.Intelligence.Projection;
using Gorm.Application.Temporal.Operations.Drift;
using Gorm.Application.Temporal.Operations.Prediction;
using Gorm.Application.Temporal.Operations.Reporting;
using Gorm.Application.Temporal.Operations.Studio;
using Gorm.Application.Temporal.Operations.Warnings;
using Gorm.Application.Temporal.Scenarios;
using Gorm.Application.Temporal.Snapshots;
using Gorm.Core.Primitives;

namespace Gorm.Tests.Temporal.Operations;

[TestClass]
public sealed class GraphTwinReportingStudioTests
{
    public TestContext TestContext { get; set; }

    private static readonly Guid NodeId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset Day1 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly string[] Expected = ["alpha", "zeta"];
    private static readonly string[] Expected2 = ["availability", "loss"];
    private static readonly string[] Expected3 = ["changes", "warnings", "predictions", "scenarios"];

    [TestMethod]
    public void Create_IsDeterministicAcrossEvidenceInputOrder()
    {
        var sync = Synchronization();
        var first = GraphTwinReportBuilder.Create(
            new GraphTwinReportBuilder.CreateParameters
            {
                ReportId = "report-1",
                CreatedAt = Day1,
                Synchronization = sync,
                Predictions = [Prediction(2), Prediction(1)],
                Scenarios = [Scenario("zeta"), Scenario("alpha")]
            });
        var second = GraphTwinReportBuilder.Create(
            new GraphTwinReportBuilder.CreateParameters
            {
                ReportId = "report-1",
                CreatedAt = Day1,
                Synchronization = sync,
                Predictions = [Prediction(1), Prediction(2)],
                Scenarios = [Scenario("alpha"), Scenario("zeta")]
            });

        Assert.AreEqual(first.ReportFingerprint, second.ReportFingerprint);
        Assert.AreEqual(first.ToJson(), second.ToJson());
        CollectionAssert.AreEqual(
            Expected,
            first.Content.Scenarios.Select(scenario => scenario.ScenarioId).ToArray());
    }

    [TestMethod]
    public void Serializer_RoundTripsAndDetectsTampering()
    {
        var report = GraphTwinReportBuilder.Create(
            new GraphTwinReportBuilder.CreateParameters
            {
                ReportId = "report-1",
                CreatedAt = Day1,
                Synchronization = Synchronization()
            });
        var json = report.ToJson(indented: true);
        var restored = GraphTwinReportSerializer.DeserializeAndVerify(json);

        Assert.AreEqual(report.ReportFingerprint, restored.ReportFingerprint);
        Assert.ThrowsExactly<InvalidOperationException>(() => GraphTwinReportSerializer.DeserializeAndVerify(json.Replace("report-1", "tampered", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Create_CombinesDriftWarningsPredictionsAndScenarioEvidence()
    {
        var sync = Synchronization();
        var warning = GraphTwinWarningEngine.Evaluate(
            sync.Drift,
            [
                new GraphTwinWarningRule
                {
                    Id = "drift",
                    Severity = GraphTwinWarningSeverity.Warning,
                    MetricSelector = report => report.EntityChangeRatio,
                    Comparison = GraphThresholdComparison.GreaterThan,
                    Threshold = 0
                }
            ], cancellationToken: TestContext.CancellationToken);
        var report = GraphTwinReportBuilder.Create(
            new GraphTwinReportBuilder.CreateParameters
            {
                ReportId = "complete",
                CreatedAt = Day1,
                Synchronization = sync,
                Warnings = warning,
                Predictions = [Prediction(1)],
                Scenarios = [Scenario("capacity")]
            });

        Assert.HasCount(1, report.Content.Changes);
        Assert.HasCount(1, report.Content.Warnings);
        Assert.HasCount(1, report.Content.Predictions);
        Assert.HasCount(1, report.Content.Scenarios);
        CollectionAssert.AreEqual(
            Expected2,
            report.Content.Scenarios[0].Metrics.Select(metric => metric.Name).ToArray());
    }

    [TestMethod]
    public void StudioBridge_ProducesNativeTimelineAndExplorerDtos()
    {
        var sync = Synchronization();
        var report = GraphTwinReportBuilder.Create(
            new GraphTwinReportBuilder.CreateParameters
            {
                ReportId = "studio",
                CreatedAt = Day1,
                Synchronization = sync,
                Scenarios = [Scenario("one")]
            });
        var timeline = GraphTwinStudioBridge.CreateTimelineEntry(sync);
        var explorer = GraphTwinStudioBridge.CreateExplorerModel(report);

        Assert.AreEqual(42L, timeline.Sequence);
        Assert.AreEqual(1, timeline.Modified);
        CollectionAssert.AreEqual(
            Expected3,
            explorer.Panels.Select(panel => panel.Id).ToArray());
        Assert.AreEqual(1, explorer.Panels.Single(panel => panel.Id == "scenarios").ItemCount);
    }

    [TestMethod]
    public void Create_RejectsEvidenceLimitsAndNonFiniteScenarioMetrics()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => GraphTwinReportBuilder.Create(
            new GraphTwinReportBuilder.CreateParameters
            {
                ReportId = "limit",
                CreatedAt = Day1,
                Synchronization = Synchronization(),
                Scenarios = [Scenario("one"), Scenario("two")],
                Options = new GraphTwinReportOptions { MaximumScenarios = 1, MaximumMetricsPerScenario = 1, MaximumChanges = 1, MaximumWarnings = 1, MaximumPredictions = 1 }
            }
        ));

        var scenario = GraphTwinScenarioEvidence.Capture(
            GraphScenario.Fork(World(1, "expected"), new GraphScenarioId("nan")),
            new Dictionary<string, double> { ["loss"] = double.NaN });
        Assert.ThrowsExactly<InvalidOperationException>(() => GraphTwinReportBuilder.Create(
            new GraphTwinReportBuilder.CreateParameters
            {
                ReportId = "nan",
                CreatedAt = Day1,
                Synchronization = Synchronization(),
                Scenarios = [scenario]
            }
        ));
    }

    [TestMethod]
    public void Create_ObservesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsExactly<OperationCanceledException>(() => GraphTwinReportBuilder.Create(
            new GraphTwinReportBuilder.CreateParameters
            {
                ReportId = "cancel",
                CreatedAt = Day1,
                Synchronization = Synchronization(),
                CancellationToken = cancellation.Token
            }
        ));
    }

    private static GraphTwinScenarioEvidence Scenario(string id)
    {
        var scenario = GraphScenario.Fork(World(1, "expected"), new GraphScenarioId(id));
        return GraphTwinScenarioEvidence.Capture(
            scenario,
            new Dictionary<string, double> { ["loss"] = 2, ["availability"] = 0.99 },
            ["constant demand", "independent failures"]);
    }

    private static GraphThresholdPrediction Prediction(double threshold) =>
        new()
        {
            SampleCount = 3,
            SlopePerSecond = 0.1,
            RSquared = 1,
            Threshold = threshold,
            Direction = GraphThresholdDirection.AtOrAbove,
            AlreadyBreached = false,
            PredictedBreachAt = Day1.AddHours(threshold),
            WithinHorizon = true,
            Explanation = "linear test prediction"
        };

    private static GraphTwinSynchronizationResult Synchronization() =>
        new()
        {
            Sequence = 42,
            ObservedAt = Day1,
            Drift = GraphTwinDriftDetector.Compare(World(1, "expected"), World(2, "observed"))
        };

    private static GraphWorldSnapshot World(long version, string name) =>
        GraphWorldSnapshot.Capture(
            new GraphWorldSnapshot.CaptureParameters
            {
                WorldKey = new GraphProjectionKey("report/eu"),
                Version = version,
                ValidAt = Day1,
                RecordedAt = Day1,
                Nodes = [new TestNode { Id = NodeId, Name = name }],
                Edges = []
            });

    private sealed class TestNode : Node
    {
        public string Name { get; set; } = string.Empty;
    }
}