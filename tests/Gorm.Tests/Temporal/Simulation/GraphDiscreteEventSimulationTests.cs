using Gorm.Application.Intelligence.Projection;
using Gorm.Application.Temporal.Scenarios;
using Gorm.Application.Temporal.Simulation;
using Gorm.Application.Temporal.Snapshots;
using Gorm.Core.Primitives;

namespace Gorm.Tests.Temporal.Simulation;

[TestClass]
public sealed class GraphDiscreteEventSimulationTests
{
    private static readonly Guid AlphaId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid BetaId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly DateTimeOffset Day1 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public TestContext TestContext { get; set; } = null!;

    private static readonly string[] Expected = ["priority", "same-a", "same-b", "late"];

    [TestMethod]
    public void Run_OrdersByClockPriorityAndStableId()
    {
        var events = new[]
        {
            Event("late", TimeSpan.FromHours(2), 0, BetaId, "late"),
            Event("same-b", TimeSpan.FromHours(1), 1, BetaId, "same-b"),
            Event("same-a", TimeSpan.FromHours(1), 1, AlphaId, "same-a"),
            Event("priority", TimeSpan.FromHours(1), 0, AlphaId, "priority")
        };

        var result = GraphDiscreteEventSimulator.Run(Baseline(), new GraphScenarioId("ordered"), Day1, events, cancellationToken: TestContext.CancellationToken);

        CollectionAssert.AreEqual(
            Expected,
            result.Steps.Select(step => step.EventId).ToArray());
        Assert.AreEqual(Day1.AddHours(1), result.Steps[0].SimulatedAt);
        Assert.AreEqual(Day1.AddHours(2), result.Steps[^1].SimulatedAt);
        Assert.IsFalse(result.Truncated);
    }

    [TestMethod]
    public void Run_IsDeterministicAndLeavesBaselineUnchanged()
    {
        var baseline = Baseline();
        var events = new[] { Event("update", TimeSpan.FromHours(1), 0, AlphaId, "changed") };

        var first = GraphDiscreteEventSimulator.Run(baseline, new GraphScenarioId("deterministic"), Day1, events, cancellationToken: TestContext.CancellationToken);
        var second = GraphDiscreteEventSimulator.Run(baseline, new GraphScenarioId("deterministic"), Day1, events.Reverse(), cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(first.Scenario.Current.Identity.SnapshotId, second.Scenario.Current.Identity.SnapshotId);
        Assert.AreEqual("alpha", baseline.Nodes[0].Materialize<TestNode>().Name);
        Assert.AreEqual("changed", first.Scenario.Current.Nodes[0].Materialize<TestNode>().Name);
    }

    [TestMethod]
    public void Run_ReportsEventsBeyondHorizon()
    {
        var result = GraphDiscreteEventSimulator.Run(
            Baseline(),
            new GraphScenarioId("horizon"),
            Day1,
            [
                Event("inside", TimeSpan.FromHours(1), 0, AlphaId, "inside"),
                Event("outside", TimeSpan.FromHours(3), 0, BetaId, "outside")
            ],
            new GraphDiscreteEventSimulationOptions { MaximumDuration = TimeSpan.FromHours(2) },
            TestContext.CancellationToken);

        Assert.HasCount(1, result.Steps);
        Assert.AreEqual(1, result.PendingEventCount);
        Assert.IsTrue(result.Truncated);
    }

    [TestMethod]
    public void Run_RejectsDuplicateNegativeAndOverLimitEvents()
    {
        var duplicate = Event("same", TimeSpan.Zero, 0, AlphaId, "one");
        Assert.ThrowsExactly<ArgumentException>(() => GraphDiscreteEventSimulator.Run(
            Baseline(),
            new GraphScenarioId("duplicate"),
            Day1,
            [duplicate, Event("same", TimeSpan.FromHours(1), 0, BetaId, "two")],
            cancellationToken: TestContext.CancellationToken));

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => GraphDiscreteEventSimulator.Run(
            Baseline(),
            new GraphScenarioId("negative"),
            Day1,
            [Event("negative", TimeSpan.FromHours(-1), 0, AlphaId, "negative")],
            cancellationToken: TestContext.CancellationToken));

        Assert.ThrowsExactly<InvalidOperationException>(() => GraphDiscreteEventSimulator.Run(
            Baseline(),
            new GraphScenarioId("limit"),
            Day1,
            [
                Event("one", TimeSpan.Zero, 0, AlphaId, "one"),
                Event("two", TimeSpan.FromHours(1), 0, BetaId, "two")
            ],
            new GraphDiscreteEventSimulationOptions { MaximumEvents = 1 },
            TestContext.CancellationToken));
    }

    [TestMethod]
    public void Run_ObservesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsExactly<OperationCanceledException>(() => GraphDiscreteEventSimulator.Run(
            Baseline(),
            new GraphScenarioId("cancel"),
            Day1,
            [Event("one", TimeSpan.Zero, 0, AlphaId, "one")],
            cancellationToken: cancellation.Token));
    }

    private static GraphSimulationEvent Event(string id, TimeSpan offset, int priority, Guid nodeId, string name) =>
        new()
        {
            EventId = id,
            Offset = offset,
            Priority = priority,
            Mutation = GraphScenarioMutation.UpdateNode(
                new TestNode { Id = nodeId, Name = name },
                Day1,
                Day1)
        };

    private static GraphWorldSnapshot Baseline() =>
        GraphWorldSnapshot.Capture(
            new GraphWorldSnapshot.CaptureParameters
            {
                WorldKey = new GraphProjectionKey("orders/eu"),
                Version = 1,
                ValidAt = Day1,
                RecordedAt = Day1,
                Nodes = [
                    new TestNode { Id = AlphaId, Name = "alpha" },
                    new TestNode { Id = BetaId, Name = "beta" }
                ],
                Edges = []
            });

    private sealed class TestNode : Node
    {
        public string Name { get; set; } = string.Empty;
    }
}