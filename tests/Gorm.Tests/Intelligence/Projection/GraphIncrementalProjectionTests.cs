using Gorm.Application.Intelligence.Projection;
using Gorm.Core.Primitives;

namespace Gorm.Tests.Intelligence.Projection;

[TestClass]
public sealed class GraphIncrementalProjectionTests
{
    private static readonly GraphProjectionKey ProjectionKey = new("fulfilment-plan:42");

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void ApplyDelta_AddsUpdatesAndRemovesEntitiesWithoutChangingPreviousSnapshot()
    {
        var first = CreateNode(1, "first");
        var second = CreateNode(2, "second");
        var removed = CreateNode(3, "removed");
        var firstEdge = CreateEdge(11, first, second, 1);
        var removedEdge = CreateEdge(12, second, removed, 2);
        var snapshot = Snapshot(10, [first, second, removed], [firstEdge, removedEdge]);
        var replacement = CreateNode(2, "second-updated");
        var added = CreateNode(4, "added");
        var addedEdge = CreateEdge(13, replacement, added, 3);
        var delta = new GraphProjectionDeltaBuilder(ProjectionKey, 10, 11)
            .UpdateNodes([replacement])
            .RemoveNodes([removed.Id])
            .AddNodes([added])
            .RemoveEdges([firstEdge.Id])
            .AddEdges([addedEdge])
            .Build();

        var result = snapshot.ApplyDelta(delta, TestContext.CancellationToken);

        Assert.AreEqual(GraphProjectionUpdateMode.Incremental, result.Mode);
        Assert.AreEqual(11, result.Snapshot.Version);
        Assert.HasCount(3, result.Snapshot.Projection.Nodes);
        Assert.HasCount(1, result.Snapshot.Projection.Edges);
        Assert.AreEqual("second-updated", ((IncrementalNode)result.Snapshot.Projection.GetNode(second.Id)).Name);
        Assert.AreEqual(added.Id, result.Snapshot.Projection.GetOutgoingNeighbors(second.Id).Single().Id);
        Assert.IsTrue(result.AffectedNodeIds.Contains(removed.Id));
        Assert.IsTrue(result.AffectedEdgeIds.Contains(removedEdge.Id));
        Assert.HasCount(3, snapshot.Projection.Nodes);
        Assert.HasCount(2, snapshot.Projection.Edges);
    }

    [TestMethod]
    public void ApplyDelta_RemovingNodeCascadesIncidentEdges()
    {
        var first = CreateNode(1, "first");
        var second = CreateNode(2, "second");
        var edge = CreateEdge(11, first, second, 1);
        var snapshot = Snapshot(4, [first, second], [edge]);
        var delta = new GraphProjectionDeltaBuilder(ProjectionKey, 4, 5).RemoveNodes([second.Id]).Build();

        var result = snapshot.ApplyDelta(delta, TestContext.CancellationToken);

        Assert.HasCount(1, result.Snapshot.Projection.Nodes);
        Assert.HasCount(0, result.Snapshot.Projection.Edges);
        Assert.IsTrue(result.AffectedEdgeIds.Contains(edge.Id));
    }

    [TestMethod]
    public void ApplyDelta_CanRetargetExistingEdgeAwayFromRemovedNode()
    {
        var first = CreateNode(1, "first");
        var removed = CreateNode(2, "removed");
        var replacement = CreateNode(3, "replacement");
        var edge = CreateEdge(11, first, removed, 1);
        var retargeted = CreateEdge(11, first, replacement, 2);
        var snapshot = Snapshot(1, [first, removed, replacement], [edge]);
        var delta = new GraphProjectionDeltaBuilder(ProjectionKey, 1, 2).RemoveNodes([removed.Id]).UpdateEdges([retargeted]).Build();

        var result = snapshot.ApplyDelta(delta, TestContext.CancellationToken);

        Assert.AreEqual(replacement.Id, result.Snapshot.Projection.GetOutgoingNeighbors(first.Id).Single().Id);
        Assert.AreEqual(2, ((IncrementalEdge)result.Snapshot.Projection.Edges.Single()).Cost);
    }

    [TestMethod]
    public void ApplyDelta_WithWrongBaseVersion_IsRejectedExplicitly()
    {
        var snapshot = Snapshot(5, [CreateNode(1, "first")], []);
        var delta = new GraphProjectionDeltaBuilder(ProjectionKey, 4, 6).Build();

        var exception = Assert.ThrowsExactly<GraphProjectionDeltaException>(
            () => snapshot.ApplyDelta(delta, TestContext.CancellationToken));

        Assert.AreEqual(GraphProjectionDeltaFailureReason.VersionMismatch, exception.Reason);
        Assert.Contains("snapshot version is 5", exception.Message);
    }

    [TestMethod]
    public void ApplyDelta_WithEntityStateMismatch_IsRejectedExplicitly()
    {
        var existing = CreateNode(1, "existing");
        var snapshot = Snapshot(1, [existing], []);
        var delta = new GraphProjectionDeltaBuilder(ProjectionKey, 1, 2).AddNodes([CreateNode(1, "duplicate")]).Build();

        var exception = Assert.ThrowsExactly<GraphProjectionDeltaException>(
            () => snapshot.ApplyDelta(delta, TestContext.CancellationToken));

        Assert.AreEqual(GraphProjectionDeltaFailureReason.EntityStateMismatch, exception.Reason);
    }

    [TestMethod]
    public async Task ApplyDeltaOrRebuildAsync_WhenDeltaIsIncompatible_UsesExplicitCurrentRebuild()
    {
        var oldSnapshot = Snapshot(1, [CreateNode(1, "old")], []);
        var rebuilt = Snapshot(4, [CreateNode(2, "current")], []);
        var delta = new GraphProjectionDeltaBuilder(ProjectionKey, 2, 3).Build();
        var rebuildCalls = 0;

        var result = await oldSnapshot.ApplyDeltaOrRebuildAsync(
            delta,
            _ =>
            {
                rebuildCalls++;
                return Task.FromResult(rebuilt);
            },
            TestContext.CancellationToken);

        Assert.AreEqual(1, rebuildCalls);
        Assert.AreEqual(GraphProjectionUpdateMode.FullRebuild, result.Mode);
        Assert.AreSame(rebuilt, result.Snapshot);
        Assert.Contains("full rebuild", result.Explanation);
    }

    [TestMethod]
    public async Task ApplyDeltaOrRebuildAsync_WithStaleRebuild_IsRejected()
    {
        var oldSnapshot = Snapshot(1, [CreateNode(1, "old")], []);
        var staleRebuild = Snapshot(2, [CreateNode(2, "still-stale")], []);
        var delta = new GraphProjectionDeltaBuilder(ProjectionKey, 2, 3).Build();

        GraphProjectionDeltaException? exception = null;
        try
        {
            await oldSnapshot.ApplyDeltaOrRebuildAsync(
                delta,
                _ => Task.FromResult(staleRebuild),
                TestContext.CancellationToken);
        }
        catch (GraphProjectionDeltaException caught)
        {
            exception = caught;
        }

        Assert.IsNotNull(exception);
        Assert.AreEqual(GraphProjectionDeltaFailureReason.RebuildRejected, exception.Reason);
    }

    [TestMethod]
    public void ApplyDelta_WithMissingAddedEdgeEndpoint_IsRejected()
    {
        var first = CreateNode(1, "first");
        var missing = CreateNode(2, "missing");
        var snapshot = Snapshot(1, [first], []);
        var delta = new GraphProjectionDeltaBuilder(ProjectionKey, 1, 2).AddEdges([CreateEdge(11, first, missing, 1)]).Build();

        var exception = Assert.ThrowsExactly<GraphProjectionDeltaException>(
            () => snapshot.ApplyDelta(delta, TestContext.CancellationToken));

        Assert.AreEqual(GraphProjectionDeltaFailureReason.InvalidEndpoint, exception.Reason);
    }

    [TestMethod]
    public void DeltaBuilder_WithConflictingOperations_IsRejected()
    {
        var node = CreateNode(1, "node");
        var builder = new GraphProjectionDeltaBuilder(ProjectionKey, 1, 2).AddNodes([node]);

        var exception = Assert.ThrowsExactly<ArgumentException>(() => builder.RemoveNodes([node.Id]));

        Assert.Contains("already contains an operation", exception.Message);
    }

    [TestMethod]
    public void ApplyDelta_OrdersAddedEntitiesByIdentifier()
    {
        var existing = CreateNode(1, "existing");
        var high = CreateNode(3, "high");
        var low = CreateNode(2, "low");
        var snapshot = Snapshot(1, [existing], []);
        var delta = new GraphProjectionDeltaBuilder(ProjectionKey, 1, 2).AddNodes([high, low]).Build();

        var result = snapshot.ApplyDelta(delta, TestContext.CancellationToken);

        CollectionAssert.AreEqual(
            new[] { existing.Id, low.Id, high.Id },
            result.Snapshot.Projection.Nodes.Select(node => node.Id).ToArray());
    }

    [TestMethod]
    public void ApplyDelta_WhenCancelled_DoesNotProducePartialSnapshot()
    {
        var snapshot = Snapshot(1, [CreateNode(1, "first")], []);
        var delta = new GraphProjectionDeltaBuilder(ProjectionKey, 1, 2).Build();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsExactly<OperationCanceledException>(() => snapshot.ApplyDelta(delta, cancellation.Token));
        Assert.AreEqual(1, snapshot.Version);
    }

    private static GraphProjectionSnapshot Snapshot(long version, IReadOnlyList<IncrementalNode> nodes, IReadOnlyList<IncrementalEdge> edges) =>
        new(ProjectionKey, version, GraphProjection.Create(nodes, edges));

    private static IncrementalNode CreateNode(int identifier, string name) => new()
    {
        Id = CreateGuid(identifier),
        Name = name
    };

    private static IncrementalEdge CreateEdge(int identifier, IncrementalNode from, IncrementalNode to, double cost) => new()
    {
        Id = CreateGuid(identifier),
        FromId = from.Id,
        ToId = to.Id,
        Cost = cost
    };

    private static Guid CreateGuid(int identifier) =>
        Guid.Parse($"00000000-0000-0000-0000-{identifier:D12}");

    private sealed class IncrementalNode : Node
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class IncrementalEdge : Edge
    {
        public double Cost { get; set; }
    }
}