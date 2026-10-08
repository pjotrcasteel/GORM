using Gorm.Application.Intelligence.Projection;
using Gorm.Application.Temporal.Snapshots;
using Gorm.Core.Primitives;

namespace Gorm.Tests.Temporal.Snapshots;

[TestClass]
public sealed class GraphWorldSnapshotTests
{
    private static readonly Guid AlphaId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid BetaId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid EdgeId = Guid.Parse("00000000-0000-0000-0000-000000000010");
    private static readonly DateTimeOffset ValidAt = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset RecordedAt = new(2026, 1, 2, 10, 0, 0, TimeSpan.Zero);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Capture_DetachesWorldStateFromSourcesAndMaterializedCopies()
    {
        var alpha = new TestNode { Id = AlphaId, Name = "alpha" };
        var beta = new TestNode { Id = BetaId, Name = "beta" };
        var edge = new TestEdge { Id = EdgeId, FromId = AlphaId, ToId = BetaId, Capacity = 5 };
        var snapshot = Capture([alpha, beta], [edge]);

        alpha.Name = "changed source";
        edge.Capacity = 99;

        var firstNode = snapshot.Nodes[0].Materialize<TestNode>();
        var firstEdge = snapshot.Edges[0].Materialize<TestEdge>();
        Assert.AreEqual("alpha", firstNode.Name);
        Assert.AreEqual(5, firstEdge.Capacity);

        firstNode.Name = "changed materialization";
        firstEdge.Capacity = 100;

        Assert.AreEqual("alpha", snapshot.Nodes[0].Materialize<TestNode>().Name);
        Assert.AreEqual(5, snapshot.Edges[0].Materialize<TestEdge>().Capacity);
    }

    [TestMethod]
    public void Capture_IsDeterministicAcrossInputOrder()
    {
        var alpha = new TestNode { Id = AlphaId, Name = "alpha" };
        var beta = new TestNode { Id = BetaId, Name = "beta" };
        var edge = new TestEdge { Id = EdgeId, FromId = AlphaId, ToId = BetaId, Capacity = 5 };

        var first = Capture([alpha, beta], [edge]);
        var second = Capture([beta, alpha], [edge]);

        CollectionAssert.AreEqual(new[] { AlphaId, BetaId }, first.Nodes.Select(node => node.Id).ToArray());
        Assert.AreEqual(first.Identity.ContentFingerprint, second.Identity.ContentFingerprint);
        Assert.AreEqual(first.Identity.SnapshotId, second.Identity.SnapshotId);
    }

    [TestMethod]
    public void Capture_PreservesSeparateValidAndRecordedTime()
    {
        var offsetValidAt = ValidAt.ToOffset(TimeSpan.FromHours(2));
        var offsetRecordedAt = RecordedAt.ToOffset(TimeSpan.FromHours(-4));
        var snapshot = GraphWorldSnapshot.Capture(
            new GraphWorldSnapshot.CaptureParameters
            {
                WorldKey = new GraphProjectionKey("orders/eu"),
                Version = 7,
                ValidAt = offsetValidAt,
                RecordedAt = offsetRecordedAt,
                Nodes = [new TestNode { Id = AlphaId, Name = "alpha" }],
                Edges = []
            });

        Assert.AreEqual(ValidAt, snapshot.Identity.ValidAt);
        Assert.AreEqual(RecordedAt, snapshot.Identity.RecordedAt);
        Assert.AreEqual(TimeSpan.Zero, snapshot.Identity.ValidAt.Offset);
        Assert.AreEqual(TimeSpan.Zero, snapshot.Identity.RecordedAt.Offset);
    }

    [TestMethod]
    public void CreateIntelligenceSnapshot_ReturnsFreshProjectionWithTemporalProvenance()
    {
        var snapshot = Capture(
            [
                new TestNode { Id = AlphaId, Name = "alpha" },
                new TestNode { Id = BetaId, Name = "beta" }
            ],
            [new TestEdge { Id = EdgeId, FromId = AlphaId, ToId = BetaId, Capacity = 5 }]);

        var first = snapshot.CreateIntelligenceSnapshot(TestContext.CancellationToken);
        var second = snapshot.CreateIntelligenceSnapshot(TestContext.CancellationToken);

        Assert.AreEqual(snapshot.Identity.WorldKey, first.Key);
        Assert.AreEqual(snapshot.Identity.Version, first.Version);
        Assert.AreEqual(snapshot.Identity.RecordedAt, first.Metadata.CreatedAt);
        Assert.AreEqual("temporal-world", first.Metadata.Origin);
        Assert.AreNotSame(first.Projection, second.Projection);
        Assert.AreNotSame(first.Projection.Nodes[0], second.Projection.Nodes[0]);
    }

    [TestMethod]
    public void Capture_FromIntelligenceSnapshot_PreservesKeyVersionAndRecordedTime()
    {
        var projection = GraphProjection.Create(
            [new TestNode { Id = AlphaId, Name = "alpha" }],
            []);
        var source = new GraphProjectionSnapshot(
            new GraphProjectionKey("orders/eu"),
            7,
            projection,
            GraphProjectionSnapshotMetadata.Create(new GraphProjectionKey("orders/eu"), 7, projection, RecordedAt));

        var snapshot = GraphWorldSnapshot.Capture(source, ValidAt, cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(source.Key, snapshot.Identity.WorldKey);
        Assert.AreEqual(source.Version, snapshot.Identity.Version);
        Assert.AreEqual(source.Metadata.CreatedAt, snapshot.Identity.RecordedAt);
    }

    [TestMethod]
    public void Capture_RejectsInvalidTopologyAndVersion()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => GraphWorldSnapshot.Capture(
            new GraphWorldSnapshot.CaptureParameters
            {
                WorldKey = new GraphProjectionKey("orders/eu"),
                Version = -1,
                ValidAt = ValidAt,
                RecordedAt = RecordedAt,
                Nodes = [],
                Edges = []
            }
        ));

        Assert.ThrowsExactly<ArgumentException>(() => Capture(
            [new TestNode { Id = AlphaId, Name = "alpha" }],
            [new TestEdge { Id = EdgeId, FromId = AlphaId, ToId = BetaId, Capacity = 5 }]));
    }

    [TestMethod]
    public void Capture_ObservesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsExactly<OperationCanceledException>(() => GraphWorldSnapshot.Capture(
            new GraphWorldSnapshot.CaptureParameters
            {
                WorldKey = new GraphProjectionKey("orders/eu"),
                Version = 7,
                ValidAt = ValidAt,
                RecordedAt = RecordedAt,
                Nodes = [new TestNode { Id = AlphaId, Name = "alpha" }],
                Edges = [],
                CancellationToken = cancellation.Token
            }
        ));
    }

    private static GraphWorldSnapshot Capture(IEnumerable<Node> nodes, IEnumerable<Edge> edges) =>
        GraphWorldSnapshot.Capture(
            new GraphWorldSnapshot.CaptureParameters
            {
                WorldKey = new GraphProjectionKey("orders/eu"),
                Version = 7,
                ValidAt = ValidAt,
                RecordedAt = RecordedAt,
                Nodes = nodes,
                Edges = edges
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