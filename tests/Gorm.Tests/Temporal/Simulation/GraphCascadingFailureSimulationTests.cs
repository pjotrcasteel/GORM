using Gorm.Application.Intelligence.Projection;
using Gorm.Application.Temporal.Scenarios;
using Gorm.Application.Temporal.Simulation.Failures;
using Gorm.Application.Temporal.Snapshots;
using Gorm.Core.Primitives;

namespace Gorm.Tests.Temporal.Simulation;

[TestClass]
public sealed class GraphCascadingFailureSimulationTests
{
    private static readonly Guid SourceId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid MiddleId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid DestinationId = Guid.Parse("00000000-0000-0000-0000-000000000003");
    private static readonly DateTimeOffset Day1 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Run_PropagatesSimultaneousFailuresAndCalculatesCapacityLoss()
    {
        var result = GraphCascadingFailureSimulator.Run(Baseline(5, 5), new GraphScenarioId("source-outage"), Day1, Options(SourceId, calculateFlow: true), TestContext.CancellationToken);

        Assert.HasCount(3, result.Rounds);
        CollectionAssert.AreEqual(new[] { SourceId }, result.Rounds[0].FailedNodeIds.ToArray());
        CollectionAssert.AreEqual(new[] { MiddleId }, result.Rounds[1].FailedNodeIds.ToArray());
        CollectionAssert.AreEqual(new[] { DestinationId }, result.Rounds[2].FailedNodeIds.ToArray());
        Assert.AreEqual(5d, result.BaselineMaximumFlow);
        Assert.AreEqual(0d, result.SurvivingMaximumFlow);
        Assert.AreEqual(5d, result.CapacityLoss);
        Assert.IsEmpty(result.Scenario.Current.Nodes);
        Assert.IsFalse(result.Truncated);
    }

    [TestMethod]
    public void Run_DetectsCapacityShortfallWithoutSeededFailure()
    {
        var result = GraphCascadingFailureSimulator.Run(Baseline(2, 5), new GraphScenarioId("shortfall"), Day1, Options(), TestContext.CancellationToken);

        Assert.HasCount(2, result.Rounds);
        CollectionAssert.AreEqual(new[] { MiddleId }, result.Rounds[0].FailedNodeIds.ToArray());
        CollectionAssert.AreEqual(new[] { DestinationId }, result.Rounds[1].FailedNodeIds.ToArray());
        CollectionAssert.AreEqual(new[] { MiddleId, DestinationId }, result.FailedNodeIds.ToArray());
        Assert.HasCount(1, result.Scenario.Current.Nodes);
        Assert.IsTrue(result.NodeStatuses.Single(status => status.NodeId == SourceId).IsSource);
    }

    [TestMethod]
    public void Run_IsDeterministicAndProtectsSourcesByDefault()
    {
        var baseline = Baseline(5, 5);
        var first = GraphCascadingFailureSimulator.Run(baseline, new GraphScenarioId("stable"), Day1, Options(), TestContext.CancellationToken);
        var second = GraphCascadingFailureSimulator.Run(baseline, new GraphScenarioId("stable"), Day1, Options(), TestContext.CancellationToken);

        Assert.AreEqual(first.Scenario.Current.Identity.SnapshotId, second.Scenario.Current.Identity.SnapshotId);
        Assert.IsEmpty(first.FailedNodeIds);
        Assert.HasCount(3, baseline.Nodes);
    }

    [TestMethod]
    public void Run_ReportsRoundTruncationAndEnforcesFailureLimit()
    {
        var truncated = GraphCascadingFailureSimulator.Run(Baseline(5, 5), new GraphScenarioId("truncated"), Day1, Options(SourceId, maximumRounds: 1), TestContext.CancellationToken);

        Assert.IsTrue(truncated.Truncated);
        CollectionAssert.AreEqual(new[] { SourceId, MiddleId }, truncated.FailedNodeIds.ToArray());

        Assert.ThrowsExactly<InvalidOperationException>(() => GraphCascadingFailureSimulator.Run(
            Baseline(5, 5),
            new GraphScenarioId("failure-limit"),
            Day1,
            Options(SourceId, maximumFailures: 1),
            TestContext.CancellationToken));
    }

    [TestMethod]
    public void Run_ValidatesCapacityDemandAndFlowConfiguration()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => GraphCascadingFailureSimulator.Run(
            Baseline(5, 5),
            new GraphScenarioId("negative-demand"),
            Day1,
            new GraphCascadingFailureOptions
            {
                DemandSelector = _ => -1,
                CapacitySelector = edge => ((TestEdge)edge).Capacity
            },
            TestContext.CancellationToken));

        Assert.ThrowsExactly<InvalidOperationException>(() => GraphCascadingFailureSimulator.Run(
            Baseline(5, 5),
            new GraphScenarioId("negative-capacity"),
            Day1,
            new GraphCascadingFailureOptions
            {
                DemandSelector = node => ((TestNode)node).Demand,
                CapacitySelector = _ => -1
            },
            TestContext.CancellationToken));

        Assert.ThrowsExactly<ArgumentException>(() => GraphCascadingFailureSimulator.Run(
            Baseline(5, 5),
            new GraphScenarioId("flow-pair"),
            Day1,
            new GraphCascadingFailureOptions
            {
                DemandSelector = node => ((TestNode)node).Demand,
                CapacitySelector = edge => ((TestEdge)edge).Capacity,
                FlowSourceNodeId = SourceId
            },
            TestContext.CancellationToken));
    }

    [TestMethod]
    public void Run_ObservesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsExactly<OperationCanceledException>(() => GraphCascadingFailureSimulator.Run(
            Baseline(5, 5),
            new GraphScenarioId("cancel"),
            Day1,
            Options(),
            cancellation.Token));
    }

    private static GraphCascadingFailureOptions Options(Guid? initialFailure = null, bool calculateFlow = false, int maximumRounds = 100, int maximumFailures = 100) =>
        new()
        {
            InitiallyFailedNodeIds = initialFailure is Guid nodeId ? new[] { nodeId } : [],
            DemandSelector = node => ((TestNode)node).Demand,
            CapacitySelector = edge => ((TestEdge)edge).Capacity,
            FlowSourceNodeId = calculateFlow ? SourceId : null,
            FlowDestinationNodeId = calculateFlow ? DestinationId : null,
            MaximumRounds = maximumRounds,
            MaximumFailures = maximumFailures
        };

    private static GraphWorldSnapshot Baseline(double firstCapacity, double secondCapacity) =>
        GraphWorldSnapshot.Capture(
            new GraphWorldSnapshot.CaptureParameters
            {
                WorldKey = new GraphProjectionKey("network/eu"),
                Version = 1,
                ValidAt = Day1,
                RecordedAt = Day1,
                Nodes = [
                    new TestNode { Id = SourceId, Demand = 100 },
                    new TestNode { Id = MiddleId, Demand = 4 },
                    new TestNode { Id = DestinationId, Demand = 4 }
                ],
                Edges = [
                    new TestEdge
                    {
                        Id = Guid.Parse("00000000-0000-0000-0000-000000000011"),
                        FromId = SourceId,
                        ToId = MiddleId,
                        Capacity = firstCapacity
                    },
                    new TestEdge
                    {
                        Id = Guid.Parse("00000000-0000-0000-0000-000000000012"),
                        FromId = MiddleId,
                        ToId = DestinationId,
                        Capacity = secondCapacity
                    }
                ]
            });

    private sealed class TestNode : Node
    {
        public double Demand { get; set; }
    }

    private sealed class TestEdge : Edge
    {
        public double Capacity { get; set; }
    }
}