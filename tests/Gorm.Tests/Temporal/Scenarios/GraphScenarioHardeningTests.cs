using Gorm.Application.Intelligence.Projection;
using Gorm.Application.Temporal.Scenarios;
using Gorm.Application.Temporal.Scenarios.Replay;
using Gorm.Application.Temporal.Snapshots;
using Gorm.Core.Primitives;

namespace Gorm.Tests.Temporal.Scenarios;

[TestClass]
public sealed class GraphScenarioHardeningTests
{
    private static readonly Guid AlphaId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid BetaId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly DateTimeOffset Day1 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Scenario_EnforcesRevisionSizeAndRecordedTimeLimits()
    {
        var scenario = GraphScenario.Fork(
            Baseline(),
            new GraphScenarioId("bounded"),
            new GraphScenarioOptions
            {
                MaximumRevisions = 1,
                MaximumNodes = 2,
                MaximumEdges = 1
            });
        scenario = scenario.Apply(GraphScenarioMutation.UpdateNode(
            new TestNode { Id = AlphaId, Name = "changed" },
            Day1.AddDays(1),
            Day1.AddDays(1)), TestContext.CancellationToken).Scenario;

        var revision = Assert.ThrowsExactly<GraphScenarioMutationException>(() => scenario.Apply(
            GraphScenarioMutation.UpdateNode(
                new TestNode { Id = BetaId, Name = "changed" },
                Day1.AddDays(2),
                Day1.AddDays(2)), TestContext.CancellationToken));
        Assert.AreEqual(GraphScenarioFailureReason.RevisionLimitExceeded, revision.Reason);

        var sizeScenario = GraphScenario.Fork(
            Baseline(),
            new GraphScenarioId("size"),
            new GraphScenarioOptions { MaximumNodes = 2, MaximumEdges = 1 });
        var size = Assert.ThrowsExactly<GraphScenarioMutationException>(() => sizeScenario.Apply(
            GraphScenarioMutation.AddNode(
                new TestNode
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000003"),
                    Name = "gamma"
                },
                Day1.AddDays(1),
                Day1.AddDays(1)), TestContext.CancellationToken));
        Assert.AreEqual(GraphScenarioFailureReason.SizeLimitExceeded, size.Reason);

        var time = Assert.ThrowsExactly<GraphScenarioMutationException>(() => sizeScenario.Apply(
            GraphScenarioMutation.UpdateNode(
                new TestNode { Id = AlphaId, Name = "past" },
                Day1,
                Day1.AddDays(-1)), TestContext.CancellationToken));
        Assert.AreEqual(GraphScenarioFailureReason.TemporalOrderViolation, time.Reason);
    }

    [TestMethod]
    public void Checkpoint_VerifiesExactReplayAndRejectsWrongBaseline()
    {
        var scenario = GraphScenario.Fork(Baseline(), new GraphScenarioId("verified"));
        scenario = scenario.Apply(GraphScenarioMutation.UpdateNode(
            new TestNode { Id = AlphaId, Name = "changed" },
            Day1.AddDays(1),
            Day1.AddDays(1)), TestContext.CancellationToken).Scenario;
        var stream = GraphScenarioEventStream.Create(scenario.Id, scenario.Mutations, cancellationToken: TestContext.CancellationToken);
        var checkpoint = GraphScenarioCheckpoint.Create(scenario, stream);

        var valid = GraphScenarioVerifier.Verify(checkpoint, scenario.Baseline, stream, TestContext.CancellationToken);

        Assert.IsTrue(valid.IsValid);
        Assert.AreEqual(checkpoint.FinalSnapshotId, valid.Replay!.Scenario.Current.Identity.SnapshotId);

        var otherBaseline = GraphWorldSnapshot.Capture(
            new GraphWorldSnapshot.CaptureParameters
            {
                WorldKey = new GraphProjectionKey("orders/eu"),
                Version = 1,
                ValidAt = Day1,
                RecordedAt = Day1,
                Nodes = [
                    new TestNode { Id = AlphaId, Name = "different" },
                    new TestNode { Id = BetaId, Name = "beta" }
                ],
                Edges = []
            });
        var invalid = GraphScenarioVerifier.Verify(checkpoint, otherBaseline, stream, TestContext.CancellationToken);
        Assert.IsFalse(invalid.IsValid);
        Assert.IsNull(invalid.Replay);
    }

    [TestMethod]
    public void Checkpoint_RejectsUnrelatedEventStream()
    {
        var scenario = GraphScenario.Fork(Baseline(), new GraphScenarioId("one"));
        var unrelated = GraphScenarioEventStream.Create(new GraphScenarioId("two"), [], cancellationToken: TestContext.CancellationToken);

        Assert.ThrowsExactly<ArgumentException>(() => GraphScenarioCheckpoint.Create(scenario, unrelated));
    }

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