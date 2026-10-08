using Gorm.Application.History;
using Gorm.Application.History.Envelopes;
using Gorm.Application.Intelligence.Projection;
using Gorm.Application.Temporal.Bitemporal;
using Gorm.Application.Temporal.History;
using Gorm.Core.Primitives;

namespace Gorm.Tests.Temporal.History;

[TestClass]
public sealed class GraphBitemporalWorldTests
{
    public TestContext TestContext { get; set; }

    private static readonly Guid NodeId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly DateTime Day1 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Day2 = Day1.AddDays(1);
    private static readonly DateTime Day3 = Day1.AddDays(2);
    private static readonly DateTime Day4 = Day1.AddDays(3);

    [TestMethod]
    public void Coordinate_NormalizesBothAxesToUtc()
    {
        var coordinate = new GraphBitemporalCoordinate(new DateTimeOffset(Day2).ToOffset(TimeSpan.FromHours(2)), new DateTimeOffset(Day3).ToOffset(TimeSpan.FromHours(-5)));

        Assert.AreEqual(TimeSpan.Zero, coordinate.ValidAt.Offset);
        Assert.AreEqual(TimeSpan.Zero, coordinate.RecordedAt.Offset);
        Assert.AreEqual(new DateTimeOffset(Day2), coordinate.ValidAt);
        Assert.AreEqual(new DateTimeOffset(Day3), coordinate.RecordedAt);
    }

    [TestMethod]
    public void Dataset_DetachesSourceHistoryAndIsRepeatable()
    {
        var sourceNode = new TestNode { Id = NodeId, Name = "original" };
        var envelope = GraphHistoryEnvelope.ForNode(sourceNode, GraphHistoryOperationKind.Created, Day1, Day1);
        var dataset = GraphWorldHistoryDataset.Capture([envelope], cancellationToken: TestContext.CancellationToken);
        sourceNode.Name = "mutated";

        var coordinate = new GraphBitemporalCoordinate(new DateTimeOffset(Day2), new DateTimeOffset(Day2));
        var first = dataset.Project(new GraphProjectionKey("world"), 1, coordinate, cancellationToken: TestContext.CancellationToken);
        var second = dataset.Project(new GraphProjectionKey("world"), 1, coordinate, cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual("original", first.Snapshot.Nodes[0].Materialize<TestNode>().Name);
        Assert.AreEqual(first.Snapshot.Identity.SnapshotId, second.Snapshot.Identity.SnapshotId);
    }

    [TestMethod]
    public void Compare_ClassifiesRecordedKnowledgeCorrection()
    {
        var dataset = GraphWorldHistoryDataset.Capture([Envelope("original", Day1, Day1), Envelope("corrected", Day3, Day1)], cancellationToken: TestContext.CancellationToken);

        var result = dataset.Compare(
            new GraphProjectionKey("world"),
            1,
            new GraphBitemporalCoordinate(new DateTimeOffset(Day2), new DateTimeOffset(Day2)),
            2,
            new GraphBitemporalCoordinate(new DateTimeOffset(Day2), new DateTimeOffset(Day4)));

        Assert.AreEqual(GraphBitemporalComparisonKind.RecordedKnowledgeCorrection, result.Kind);
        Assert.AreEqual(1, result.Difference.ModifiedCount);
        Assert.AreEqual("corrected", result.Second.Snapshot.Nodes[0].Materialize<TestNode>().Name);
    }

    [TestMethod]
    public void Compare_ClassifiesValidTimeEvolutionAndMixedChanges()
    {
        var dataset = GraphWorldHistoryDataset.Capture([Envelope("old", Day1, Day1, Day3), Envelope("new", Day2, Day3)], cancellationToken: TestContext.CancellationToken);
        var fixedRecordedAt = new DateTimeOffset(Day4);

        var evolution = dataset.Compare(
            new GraphProjectionKey("world"),
            1,
            new GraphBitemporalCoordinate(new DateTimeOffset(Day2), fixedRecordedAt),
            2,
            new GraphBitemporalCoordinate(new DateTimeOffset(Day3), fixedRecordedAt));
        var mixed = dataset.Compare(
            new GraphProjectionKey("world"),
            3,
            new GraphBitemporalCoordinate(new DateTimeOffset(Day2), new DateTimeOffset(Day2)),
            4,
            new GraphBitemporalCoordinate(new DateTimeOffset(Day3), fixedRecordedAt));

        Assert.AreEqual(GraphBitemporalComparisonKind.ValidTimeEvolution, evolution.Kind);
        Assert.AreEqual(GraphBitemporalComparisonKind.Mixed, mixed.Kind);
        Assert.AreEqual(1, evolution.Difference.ModifiedCount);
    }

    [TestMethod]
    public void Dataset_EnforcesCaptureLimitAndCancellation()
    {
        Assert.ThrowsExactly<GraphWorldHistoryProjectionException>(() => GraphWorldHistoryDataset.Capture(
            [Envelope("one", Day1, Day1), Envelope("two", Day2, Day2)],
            maximumHistoryEntries: 1,
            cancellationToken: TestContext.CancellationToken));

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsExactly<OperationCanceledException>(() => GraphWorldHistoryDataset.Capture([Envelope("one", Day1, Day1)], cancellationToken: cancellation.Token));
    }

    private static GraphHistoryEnvelope Envelope(string name, DateTime capturedAt, DateTime validFrom, DateTime? validTo = null) =>
        GraphHistoryEnvelope.ForNode(
            new TestNode { Id = NodeId, Name = name },
            capturedAt == Day1 ? GraphHistoryOperationKind.Created : GraphHistoryOperationKind.Updated,
            capturedAt,
            validFrom,
            validTo);

    private sealed class TestNode : Node
    {
        public string Name { get; set; } = string.Empty;
    }
}