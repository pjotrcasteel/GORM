using System.Reflection;
using Gorm.Application.Intelligence.Projection;
using Gorm.Application.Temporal.Operations;
using Gorm.Application.Temporal.Operations.Benchmarking;
using Gorm.Application.Temporal.Operations.Drift;
using Gorm.Application.Temporal.Operations.Reporting;
using Gorm.Application.Temporal.Snapshots;
using Gorm.Core.Primitives;

namespace Gorm.Tests.Temporal.Operations;

[TestClass]
public sealed class GraphTwinProductHardeningTests
{
    public TestContext TestContext { get; set; }

    private static readonly Guid NodeId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset Day1 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static GraphTwinBenchmarkResult? LastResult { get; private set; }

    [TestMethod]
    public void Benchmark_IsBoundedMeasuredAndDeterministic()
    {
        LastResult = GraphTwinBenchmarkRunner.Run(
            World(1, "expected"),
            World(2, "observed"),
            new GraphTwinBenchmarkOptions { WarmupIterations = 5, Iterations = 250, MaximumIterations = 1_000 },
            cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(250, LastResult.Iterations);
        Assert.IsGreaterThan(0d, LastResult.MeanMicroseconds);
        Assert.IsGreaterThan(0d, LastResult.OperationsPerSecond);
        Assert.IsGreaterThan(0L, LastResult.AllocatedBytes);
        Assert.IsTrue(LastResult.Deterministic);
        Assert.AreEqual(64, LastResult.ReportFingerprint.Length);
    }

    [TestMethod]
    public void Benchmark_RejectsOverflowAndObservesCancellation()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => GraphTwinBenchmarkRunner.Run(
            World(1, "expected"),
            World(2, "observed"),
            new GraphTwinBenchmarkOptions { WarmupIterations = 1, Iterations = 2, MaximumIterations = 2 },
            cancellationToken: TestContext.CancellationToken));

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsExactly<OperationCanceledException>(() => GraphTwinBenchmarkRunner.Run(
            World(1, "expected"),
            World(2, "observed"),
            new GraphTwinBenchmarkOptions { Iterations = 2 },
            cancellation.Token));
    }

    [TestMethod]
    public void ProductContract_PublishesMajorThreeGuarantees()
    {
        var contractType = typeof(GraphTemporalProductContract);
        Assert.AreEqual("3.0", contractType.GetField(nameof(GraphTemporalProductContract.ApiVersion))!.GetRawConstantValue());
        Assert.AreEqual("gorm.temporal.report/1", contractType.GetField(nameof(GraphTemporalProductContract.ReportSchemaVersion))!.GetRawConstantValue());
        Assert.HasCount(5, GraphTemporalProductContract.Guarantees);
        Assert.IsTrue(GraphTemporalProductContract.Guarantees.Any(value => value.Contains("never persist implicitly", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void StableApi_ContainsCoreTemporalProductEntryPoints()
    {
        var requiredTypes = new[]
        {
            "Gorm.Application.Temporal.Snapshots.GraphWorldSnapshot",
            "Gorm.Application.Temporal.History.GraphWorldHistoryProjector",
            "Gorm.Application.Temporal.Scenarios.GraphScenario",
            "Gorm.Application.Temporal.Simulation.GraphDiscreteEventSimulator",
            "Gorm.Application.Temporal.Simulation.Failures.GraphCascadingFailureSimulator",
            "Gorm.Application.Temporal.Simulation.MonteCarlo.GraphMonteCarloSimulator",
            "Gorm.Application.Temporal.Simulation.Decision.GraphPolicyComparator",
            "Gorm.Application.Temporal.Operations.Drift.GraphTwinSynchronizer",
            "Gorm.Application.Temporal.Operations.Reporting.GraphTwinReportBuilder",
            "Gorm.Application.Temporal.Operations.Studio.GraphTwinStudioBridge"
        };
        var assembly = typeof(GraphTemporalProductContract).Assembly;

        Assert.IsTrue(requiredTypes.All(name => assembly.GetType(name, throwOnError: false) is { IsPublic: true }));
        Assert.IsNotNull(typeof(GraphTwinDriftDetector).GetMethod("Compare", BindingFlags.Public | BindingFlags.Static));
        Assert.IsNotNull(typeof(GraphTwinReportBuilder).GetMethod("Create", BindingFlags.Public | BindingFlags.Static));
    }

    [TestMethod]
    public void ReportSchemaContract_MatchesProducedReports()
    {
        var drift = GraphTwinDriftDetector.Compare(World(1, "expected"), World(2, "observed"), cancellationToken: TestContext.CancellationToken);
        var report = GraphTwinReportBuilder.Create(
            new GraphTwinReportBuilder.CreateParameters
            {
                ReportId = "schema",
                CreatedAt = Day1,
                Synchronization = new GraphTwinSynchronizationResult { Sequence = 2, ObservedAt = Day1, Drift = drift }
            });

        Assert.AreEqual(GraphTemporalProductContract.ReportSchemaVersion, report.Content.SchemaVersion);
        Assert.AreEqual(report.ReportFingerprint, GraphTwinReportSerializer.DeserializeAndVerify(report.ToJson()).ReportFingerprint);
    }

    private static GraphWorldSnapshot World(long version, string name) =>
        GraphWorldSnapshot.Capture(
            new GraphWorldSnapshot.CaptureParameters
            {
                WorldKey = new GraphProjectionKey("hardening/eu"),
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