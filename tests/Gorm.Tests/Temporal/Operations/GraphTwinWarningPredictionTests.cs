using Gorm.Application.Intelligence.Projection;
using Gorm.Application.Temporal.Operations.Drift;
using Gorm.Application.Temporal.Operations.Prediction;
using Gorm.Application.Temporal.Operations.Warnings;
using Gorm.Application.Temporal.Snapshots;
using Gorm.Core.Primitives;

namespace Gorm.Tests.Temporal.Operations;

[TestClass]
public sealed class GraphTwinWarningPredictionTests
{
    public TestContext TestContext { get; set; }
    private static readonly Guid NodeId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset Day1 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly string[] Expected = ["critical", "ratio"];

    [TestMethod]
    public void WarningEngine_MatchesAndOrdersTransparentRules()
    {
        var evaluation = GraphTwinWarningEngine.Evaluate(
            Drift(),
            [
                Rule("ratio", GraphTwinWarningSeverity.Warning, report => report.EntityChangeRatio, 0.5),
                Rule("critical", GraphTwinWarningSeverity.Critical, report => report.Difference.Changes.Count, 1)
            ], cancellationToken: TestContext.CancellationToken);
        Assert.AreEqual(2, evaluation.EvaluatedRuleCount);
        CollectionAssert.AreEqual(
            Expected,
            evaluation.Warnings.Select(warning => warning.RuleId).ToArray());
        Assert.IsTrue(evaluation.Warnings[0].Explanation.Contains(">=", StringComparison.Ordinal));
    }

    [TestMethod]
    public void WarningEngine_RejectsDuplicateNonFiniteAndOverflow()
    {
        var duplicate = Rule("same", GraphTwinWarningSeverity.Warning, _ => 1, 1);
        Assert.ThrowsExactly<ArgumentException>(() => GraphTwinWarningEngine.Evaluate(Drift(), [duplicate, duplicate], cancellationToken: TestContext.CancellationToken));
        Assert.ThrowsExactly<InvalidOperationException>(() => GraphTwinWarningEngine.Evaluate(Drift(), [Rule("nan", GraphTwinWarningSeverity.Warning, _ => double.NaN, 1)],
            cancellationToken: TestContext.CancellationToken));
        Assert.ThrowsExactly<InvalidOperationException>(() => GraphTwinWarningEngine.Evaluate(
            Drift(),
            [
                Rule("one", GraphTwinWarningSeverity.Warning, _ => 1, 1),
                Rule("two", GraphTwinWarningSeverity.Warning, _ => 1, 1)
            ],
            new GraphTwinWarningOptions { MaximumWarnings = 1 }, cancellationToken: TestContext.CancellationToken));
    }

    [TestMethod]
    public void Predictor_FindsLinearFutureCrossingWithFitEvidence()
    {
        var prediction = GraphThresholdPredictor.Predict([Sample(2, 30), Sample(0, 10), Sample(1, 20)], 50, GraphThresholdDirection.AtOrAbove, TimeSpan.FromHours(3),
            cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(Day1.AddHours(4), prediction.PredictedBreachAt);
        Assert.IsTrue(prediction.WithinHorizon);
        Assert.IsFalse(prediction.AlreadyBreached);
        Assert.AreEqual(1d, prediction.RSquared);
        Assert.IsLessThan(1e-12, Math.Abs(prediction.SlopePerSecond - (10d / 3600)));
    }

    [TestMethod]
    public void Predictor_ReportsAlreadyBreachedAndTrendAwayFromThreshold()
    {
        var breached = GraphThresholdPredictor.Predict([Sample(0, 1), Sample(1, 3)], 2, GraphThresholdDirection.AtOrAbove, TimeSpan.FromHours(1),
            cancellationToken: TestContext.CancellationToken);
        var away = GraphThresholdPredictor.Predict([Sample(0, 3), Sample(1, 2)], 5, GraphThresholdDirection.AtOrAbove, TimeSpan.FromHours(1),
            cancellationToken: TestContext.CancellationToken);

        Assert.IsTrue(breached.AlreadyBreached);
        Assert.AreEqual(Day1.AddHours(1), breached.PredictedBreachAt);
        Assert.IsNull(away.PredictedBreachAt);
        Assert.IsFalse(away.WithinHorizon);
    }

    [TestMethod]
    public void Predictor_DistinguishesCrossingOutsideHorizon()
    {
        var prediction = GraphThresholdPredictor.Predict([Sample(0, 0), Sample(1, 1)], 10, GraphThresholdDirection.AtOrAbove, TimeSpan.FromHours(2),
            cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(Day1.AddHours(10), prediction.PredictedBreachAt);
        Assert.IsFalse(prediction.WithinHorizon);
    }

    [TestMethod]
    public void Predictor_RejectsDuplicateTimesAndSampleLimits()
    {
        Assert.ThrowsExactly<ArgumentException>(() => GraphThresholdPredictor.Predict([Sample(0, 1), Sample(0, 2)], 3, GraphThresholdDirection.AtOrAbove, TimeSpan.FromHours(1),
            cancellationToken: TestContext.CancellationToken));
        Assert.ThrowsExactly<InvalidOperationException>(() => GraphThresholdPredictor.Predict(
            [Sample(0, 1), Sample(1, 2), Sample(2, 3)],
            4,
            GraphThresholdDirection.AtOrAbove,
            TimeSpan.FromHours(1),
            new GraphThresholdPredictionOptions { MaximumSamples = 2 },
            cancellationToken: TestContext.CancellationToken));
    }

    [TestMethod]
    public void WarningAndPrediction_ObserveCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsExactly<OperationCanceledException>(() => GraphTwinWarningEngine.Evaluate(
            Drift(),
            [Rule("cancel", GraphTwinWarningSeverity.Warning, _ => 1, 1)],
            cancellationToken: cancellation.Token));
        Assert.ThrowsExactly<OperationCanceledException>(() => GraphThresholdPredictor.Predict(
            [Sample(0, 1), Sample(1, 2)],
            3,
            GraphThresholdDirection.AtOrAbove,
            TimeSpan.FromHours(1),
            cancellationToken: cancellation.Token));
    }

    private static GraphTwinWarningRule Rule(string id, GraphTwinWarningSeverity severity, Func<GraphTwinDriftReport, double> metric, double threshold) =>
        new()
        {
            Id = id,
            Severity = severity,
            MetricSelector = metric,
            Comparison = GraphThresholdComparison.GreaterThanOrEqual,
            Threshold = threshold
        };

    private static GraphTwinMetricSample Sample(int hours, double value) =>
        new() { At = Day1.AddHours(hours), Value = value };

    private static GraphTwinDriftReport Drift() =>
        GraphTwinDriftDetector.Compare(World(1, "expected"), World(2, "observed"));

    private static GraphWorldSnapshot World(long version, string name) =>
        GraphWorldSnapshot.Capture(
            new GraphWorldSnapshot.CaptureParameters
            {
                WorldKey = new GraphProjectionKey("warning/eu"),
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