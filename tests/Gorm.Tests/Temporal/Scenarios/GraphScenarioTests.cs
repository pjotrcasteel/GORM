using Gorm.Application.Intelligence.Projection;
using Gorm.Application.Temporal.Scenarios;
using Gorm.Application.Temporal.Snapshots;
using Gorm.Core.Primitives;

namespace Gorm.Tests.Temporal.Scenarios;

[TestClass]
public sealed class GraphScenarioTests
{
    private static readonly Guid AlphaId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid BetaId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid EdgeId = Guid.Parse("00000000-0000-0000-0000-000000000010");
    private static readonly DateTimeOffset Day1 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void ForkAndUpdate_KeepBaselineAndPreviousRevisionUnchanged()
    {
        var baseline = Baseline();
        var fork = GraphScenario.Fork(baseline, new GraphScenarioId("capacity-test"));
        var updatedNode = new TestNode { Id = AlphaId, Name = "changed" };

        var applied = fork.Apply(GraphScenarioMutation.UpdateNode(updatedNode, Day1.AddDays(1), Day1.AddDays(1)), TestContext.CancellationToken);
        updatedNode.Name = "mutated after command creation";

        Assert.AreEqual(0, fork.Revision);
        Assert.AreEqual(1, applied.Scenario.Revision);
        Assert.AreEqual("alpha", baseline.Nodes[0].Materialize<TestNode>().Name);
        Assert.AreEqual("alpha", fork.Current.Nodes[0].Materialize<TestNode>().Name);
        Assert.AreEqual("changed", applied.Scenario.Current.Nodes[0].Materialize<TestNode>().Name);
        Assert.AreEqual(1, applied.Difference.ModifiedCount);
    }

    [TestMethod]
    public void RemoveNode_CascadesIncidentEdgesOnlyInsideScenario()
    {
        var baseline = Baseline();
        var fork = GraphScenario.Fork(baseline, new GraphScenarioId("failure"));

        var applied = fork.Apply(GraphScenarioMutation.RemoveNode(BetaId, Day1.AddDays(1), Day1.AddDays(1)), TestContext.CancellationToken);

        Assert.AreEqual(1, applied.CascadedEdgeRemovals);
        Assert.HasCount(1, applied.Scenario.Current.Nodes);
        Assert.IsEmpty(applied.Scenario.Current.Edges);
        Assert.HasCount(2, baseline.Nodes);
        Assert.HasCount(1, baseline.Edges);
        Assert.AreEqual(2, applied.Difference.RemovedCount);
    }

    [TestMethod]
    public void AddNodeThenEdge_ProducesIndependentRevisions()
    {
        var scenario = GraphScenario.Fork(Baseline(), new GraphScenarioId("expansion"));
        var gammaId = Guid.Parse("00000000-0000-0000-0000-000000000003");
        scenario = scenario.Apply(GraphScenarioMutation.AddNode(
            new TestNode { Id = gammaId, Name = "gamma" },
            Day1.AddDays(1),
            Day1.AddDays(1)), TestContext.CancellationToken).Scenario;
        scenario = scenario.Apply(GraphScenarioMutation.AddEdge(
            new TestEdge
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000011"),
                FromId = BetaId,
                ToId = gammaId,
                Capacity = 3
            },
            Day1.AddDays(2),
            Day1.AddDays(2)), TestContext.CancellationToken).Scenario;

        Assert.AreEqual(2, scenario.Revision);
        Assert.HasCount(3, scenario.Current.Nodes);
        Assert.HasCount(2, scenario.Current.Edges);
        Assert.AreEqual(3L, scenario.Current.Identity.Version);
    }

    [TestMethod]
    public void Apply_RejectsExistenceAndTopologyConflicts()
    {
        var scenario = GraphScenario.Fork(Baseline(), new GraphScenarioId("invalid"));
        var duplicate = Assert.ThrowsExactly<GraphScenarioMutationException>(() => scenario.Apply(
            GraphScenarioMutation.AddNode(
                new TestNode { Id = AlphaId, Name = "duplicate" },
                Day1,
                Day1), TestContext.CancellationToken));
        Assert.AreEqual(GraphScenarioFailureReason.EntityAlreadyExists, duplicate.Reason);

        var missing = Assert.ThrowsExactly<GraphScenarioMutationException>(() => scenario.Apply(
            GraphScenarioMutation.RemoveNode(Guid.Parse("00000000-0000-0000-0000-000000000099"), Day1, Day1), TestContext.CancellationToken));
        Assert.AreEqual(GraphScenarioFailureReason.EntityNotFound, missing.Reason);

        var invalidEdge = Assert.ThrowsExactly<GraphScenarioMutationException>(() => scenario.Apply(
            GraphScenarioMutation.AddEdge(
                new TestEdge
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000011"),
                    FromId = AlphaId,
                    ToId = Guid.Parse("00000000-0000-0000-0000-000000000099")
                },
                Day1,
                Day1), TestContext.CancellationToken));
        Assert.AreEqual(GraphScenarioFailureReason.InvalidTopology, invalidEdge.Reason);
    }

    [TestMethod]
    public void Apply_ObservesCancellation()
    {
        var scenario = GraphScenario.Fork(Baseline(), new GraphScenarioId("cancel"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsExactly<OperationCanceledException>(() => scenario.Apply(
            GraphScenarioMutation.UpdateNode(
                new TestNode { Id = AlphaId, Name = "changed" },
                Day1,
                Day1),
            cancellation.Token));
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
                Edges = [new TestEdge { Id = EdgeId, FromId = AlphaId, ToId = BetaId, Capacity = 5 }]
            });

    private sealed class TestNode : Node
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class TestEdge : Edge
    {
        public int Capacity { get; set; }
    }
}