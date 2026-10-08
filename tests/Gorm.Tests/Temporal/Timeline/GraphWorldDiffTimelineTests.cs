using Gorm.Application.Intelligence.Projection;
using Gorm.Application.Temporal.Diff;
using Gorm.Application.Temporal.Snapshots;
using Gorm.Application.Temporal.Timeline;
using Gorm.Core.Primitives;

namespace Gorm.Tests.Temporal.Timeline;

[TestClass]
public sealed class GraphWorldDiffTimelineTests
{
    private static readonly Guid AlphaId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid BetaId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid GammaId = Guid.Parse("00000000-0000-0000-0000-000000000003");
    private static readonly Guid EdgeId = Guid.Parse("00000000-0000-0000-0000-000000000010");
    private static readonly DateTimeOffset Day1 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Compare_FindsAddedRemovedAndModifiedEntities()
    {
        var before = World(1, [Node(AlphaId, "alpha"), Node(BetaId, "beta")], [Edge(AlphaId, BetaId, 5)]);
        var after = World(2, [Node(AlphaId, "alpha changed"), Node(GammaId, "gamma")], [Edge(AlphaId, GammaId, 5)]);

        var diff = GraphWorldSnapshotDiffer.Compare(before, after, cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(1, diff.AddedCount);
        Assert.AreEqual(1, diff.RemovedCount);
        Assert.AreEqual(2, diff.ModifiedCount);
        Assert.IsTrue(diff.TopologyChanged);
        CollectionAssert.AreEqual(
            new[] { AlphaId, BetaId, GammaId, EdgeId },
            diff.Changes.Select(change => change.EntityId).ToArray());
    }

    [TestMethod]
    public void Compare_DistinguishesPropertyChangeFromTopologyChange()
    {
        var before = World(1, [Node(AlphaId, "alpha")], []);
        var after = World(2, [Node(AlphaId, "renamed")], []);

        var diff = GraphWorldSnapshotDiffer.Compare(before, after, cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(1, diff.ModifiedCount);
        Assert.IsFalse(diff.TopologyChanged);
        Assert.AreNotEqual(diff.Changes[0].BeforeStateFingerprint, diff.Changes[0].AfterStateFingerprint);
    }

    [TestMethod]
    public void Compare_IdenticalContentHasNoChangesEvenWhenTemporalIdentityChanges()
    {
        var before = World(1, [Node(AlphaId, "alpha")], []);
        var after = World(2, [Node(AlphaId, "alpha")], []);

        var diff = GraphWorldSnapshotDiffer.Compare(before, after, cancellationToken: TestContext.CancellationToken);

        Assert.IsEmpty(diff.Changes);
        Assert.IsFalse(diff.TopologyChanged);
    }

    [TestMethod]
    public void Compare_IsDeterministicAndBounded()
    {
        var before = World(1, [Node(AlphaId, "alpha")], []);
        var after = World(2, [Node(GammaId, "gamma"), Node(BetaId, "beta")], []);
        var first = GraphWorldSnapshotDiffer.Compare(before, after, cancellationToken: TestContext.CancellationToken);
        var second = GraphWorldSnapshotDiffer.Compare(before, after, cancellationToken: TestContext.CancellationToken);

        CollectionAssert.AreEqual(first.Changes.Select(change => change.EntityId).ToArray(), second.Changes.Select(change => change.EntityId).ToArray());
        Assert.ThrowsExactly<GraphWorldDiffLimitException>(() => GraphWorldSnapshotDiffer.Compare(
            before,
            after,
            new GraphWorldDiffOptions { MaximumChanges = 1 },
            TestContext.CancellationToken));
    }

    [TestMethod]
    public void Timeline_SortsVersionsAndBuildsAdjacentDiffs()
    {
        var version1 = World(1, [Node(AlphaId, "one")], []);
        var version2 = World(2, [Node(AlphaId, "two")], []);
        var version3 = World(3, [Node(AlphaId, "three"), Node(BetaId, "beta")], []);

        var timeline = GraphWorldTimeline.Create([version3, version1, version2], cancellationToken: TestContext.CancellationToken);

        CollectionAssert.AreEqual(
            new long[] { 1, 2, 3 },
            timeline.Points.Select(point => point.Snapshot.Version).ToArray());
        Assert.IsNull(timeline.Points[0].ChangesFromPrevious);
        Assert.AreEqual(1, timeline.Points[1].ChangesFromPrevious!.ModifiedCount);
        Assert.AreEqual(1, timeline.Points[2].ChangesFromPrevious!.AddedCount);
    }

    [TestMethod]
    public void Timeline_RejectsVersionAndWorldKeyConflicts()
    {
        var first = World(1, [Node(AlphaId, "alpha")], []);
        var sameVersion = World(1, [Node(AlphaId, "changed")], []);
        Assert.ThrowsExactly<ArgumentException>(() => GraphWorldTimeline.Create([first, sameVersion], cancellationToken: TestContext.CancellationToken));

        var otherKey = GraphWorldSnapshot.Capture(
            new GraphWorldSnapshot.CaptureParameters
            {
                WorldKey = new GraphProjectionKey("other"),
                Version = 2,
                ValidAt = Day1.AddDays(1),
                RecordedAt = Day1.AddDays(1),
                Nodes = [Node(AlphaId, "alpha")],
                Edges = []
            });
        Assert.ThrowsExactly<ArgumentException>(() => GraphWorldTimeline.Create([first, otherKey], cancellationToken: TestContext.CancellationToken));
    }

    [TestMethod]
    public void Timeline_ObservesLimitsAndCancellation()
    {
        var first = World(1, [Node(AlphaId, "alpha")], []);
        var second = World(2, [Node(AlphaId, "changed")], []);
        Assert.ThrowsExactly<InvalidOperationException>(() => GraphWorldTimeline.Create(
            [first, second],
            new GraphWorldTimelineOptions { MaximumSnapshots = 1 },
            TestContext.CancellationToken));

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsExactly<OperationCanceledException>(() => GraphWorldTimeline.Create([first], cancellationToken: cancellation.Token));
    }

    private static GraphWorldSnapshot World(long version, IEnumerable<Node> nodes, IEnumerable<Edge> edges) =>
        GraphWorldSnapshot.Capture(
            new GraphWorldSnapshot.CaptureParameters
            {
                WorldKey = new GraphProjectionKey("orders/eu"),
                Version = version,
                ValidAt = Day1.AddDays(version - 1),
                RecordedAt = Day1.AddDays(version - 1),
                Nodes = nodes,
                Edges = edges
            });

    private static TestNode Node(Guid id, string name) => new() { Id = id, Name = name };

    private static TestEdge Edge(Guid fromId, Guid toId, int capacity) =>
        new() { Id = EdgeId, FromId = fromId, ToId = toId, Capacity = capacity };

    private sealed class TestNode : Node
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class TestEdge : Edge
    {
        public int Capacity { get; set; }
    }
}