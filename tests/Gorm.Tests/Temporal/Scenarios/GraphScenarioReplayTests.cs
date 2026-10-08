using Gorm.Application.Intelligence.Projection;
using Gorm.Application.Temporal.Scenarios;
using Gorm.Application.Temporal.Scenarios.Replay;
using Gorm.Application.Temporal.Snapshots;
using Gorm.Core.Primitives;

namespace Gorm.Tests.Temporal.Scenarios;

[TestClass]
public sealed class GraphScenarioReplayTests
{
    private static readonly Guid AlphaId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid BetaId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly DateTimeOffset Day1 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Create_ProducesDeterministicHashChain()
    {
        var mutations = Mutations();
        var first = GraphScenarioEventStream.Create(new GraphScenarioId("replay"), mutations, cancellationToken: TestContext.CancellationToken);
        var second = GraphScenarioEventStream.Create(new GraphScenarioId("replay"), mutations, cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(first.StreamFingerprint, second.StreamFingerprint);
        CollectionAssert.AreEqual(first.Events.Select(item => item.EventId).ToArray(), second.Events.Select(item => item.EventId).ToArray());
        Assert.IsNull(first.Events[0].PreviousEventId);
        Assert.AreEqual(first.Events[0].EventId, first.Events[1].PreviousEventId);
    }

    [TestMethod]
    public void Replay_IsBitForBitDeterministic()
    {
        var stream = GraphScenarioEventStream.Create(new GraphScenarioId("replay"), Mutations(), cancellationToken: TestContext.CancellationToken);

        var first = stream.Replay(Baseline(), cancellationToken: TestContext.CancellationToken);
        var second = stream.Replay(Baseline(), cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(stream.StreamFingerprint, first.StreamFingerprint);
        Assert.AreEqual(first.Scenario.Current.Identity.SnapshotId, second.Scenario.Current.Identity.SnapshotId);
        Assert.AreEqual(2, first.AppliedEventCount);
        Assert.AreEqual("beta changed", first.Scenario.Current.Nodes[1].Materialize<TestNode>().Name);
    }

    [TestMethod]
    public void Restore_RejectsSequenceAndIntegrityTampering()
    {
        var stream = GraphScenarioEventStream.Create(new GraphScenarioId("replay"), Mutations(), cancellationToken: TestContext.CancellationToken);
        var first = stream.Events[0];
        var sequenceTampered = new GraphScenarioEvent
        {
            Sequence = 2,
            PreviousEventId = first.PreviousEventId,
            EventId = first.EventId,
            Mutation = first.Mutation
        };
        var sequenceException = Assert.ThrowsExactly<GraphScenarioReplayException>(() => GraphScenarioEventStream.Restore(new GraphScenarioId("replay"), [sequenceTampered], cancellationToken: TestContext.CancellationToken));
        Assert.AreEqual(GraphScenarioReplayFailureReason.SequenceMismatch, sequenceException.Reason);

        var hashTampered = new GraphScenarioEvent
        {
            Sequence = first.Sequence,
            PreviousEventId = first.PreviousEventId,
            EventId = new string('0', 64),
            Mutation = first.Mutation
        };
        var integrityException = Assert.ThrowsExactly<GraphScenarioReplayException>(() => GraphScenarioEventStream.Restore(new GraphScenarioId("replay"), [hashTampered], cancellationToken: TestContext.CancellationToken));
        Assert.AreEqual(GraphScenarioReplayFailureReason.IntegrityMismatch, integrityException.Reason);
    }

    [TestMethod]
    public void Replay_RejectsBackwardsRecordedTime()
    {
        var mutation = GraphScenarioMutation.UpdateNode(
            new TestNode { Id = AlphaId, Name = "past" },
            Day1,
            Day1.AddDays(-1));
        var stream = GraphScenarioEventStream.Create(new GraphScenarioId("past"), [mutation], cancellationToken: TestContext.CancellationToken);

        var exception = Assert.ThrowsExactly<GraphScenarioReplayException>(() => stream.Replay(Baseline(), cancellationToken: TestContext.CancellationToken));

        Assert.AreEqual(GraphScenarioReplayFailureReason.TemporalOrderViolation, exception.Reason);
    }

    [TestMethod]
    public void Stream_EnforcesLimitAndCancellation()
    {
        var limitException = Assert.ThrowsExactly<GraphScenarioReplayException>(() =>
            GraphScenarioEventStream.Create(
                new GraphScenarioId("limited"),
                Mutations(),
                new GraphScenarioReplayOptions { MaximumEvents = 1 },
                TestContext.CancellationToken));
        Assert.AreEqual(GraphScenarioReplayFailureReason.EventLimitExceeded, limitException.Reason);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsExactly<OperationCanceledException>(() => GraphScenarioEventStream.Create(new GraphScenarioId("cancel"), Mutations(), cancellationToken: cancellation.Token));
    }

    private static GraphScenarioMutation[] Mutations() =>
    [
        GraphScenarioMutation.UpdateNode(
            new TestNode { Id = AlphaId, Name = "alpha changed" },
            Day1.AddDays(1),
            Day1.AddDays(1)),
        GraphScenarioMutation.UpdateNode(
            new TestNode { Id = BetaId, Name = "beta changed" },
            Day1.AddDays(2),
            Day1.AddDays(2))
    ];

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