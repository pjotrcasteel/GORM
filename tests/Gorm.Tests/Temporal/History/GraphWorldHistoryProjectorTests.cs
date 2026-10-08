using Gorm.Application.History;
using Gorm.Application.History.Envelopes;
using Gorm.Application.Intelligence.Projection;
using Gorm.Application.Temporal.History;
using Gorm.Core.Primitives;

namespace Gorm.Tests.Temporal.History;

[TestClass]
public sealed class GraphWorldHistoryProjectorTests
{
    private static readonly Guid AlphaId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid BetaId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid EdgeId = Guid.Parse("00000000-0000-0000-0000-000000000010");
    private static readonly DateTime Day1 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Day2 = Day1.AddDays(1);
    private static readonly DateTime Day3 = Day1.AddDays(2);
    private static readonly DateTime Day4 = Day1.AddDays(3);

    [TestMethod]
    public void Project_SeparatesValidTimeFromRecordedTimeForCorrections()
    {
        var history = new[]
        {
            NodeEnvelope("original", GraphHistoryOperationKind.Created, Day1, Day1),
            NodeEnvelope("corrected", GraphHistoryOperationKind.Updated, Day3, Day1)
        };

        var beforeCorrection = Project(history, Day2, Day2);
        var afterCorrection = Project(history, Day2, Day4);

        Assert.AreEqual("original", beforeCorrection.Snapshot.Nodes[0].Materialize<TestNode>().Name);
        Assert.AreEqual("corrected", afterCorrection.Snapshot.Nodes[0].Materialize<TestNode>().Name);
        Assert.AreNotEqual(beforeCorrection.Snapshot.Identity.SnapshotId, afterCorrection.Snapshot.Identity.SnapshotId);
    }

    [TestMethod]
    public void Project_ExcludesLatestDeletedAndDisconnectedStates()
    {
        var history = new[]
        {
            NodeEnvelope("alpha", GraphHistoryOperationKind.Created, Day1, Day1),
            NodeEnvelope("beta", GraphHistoryOperationKind.Created, Day1, Day1, BetaId),
            EdgeEnvelope(GraphHistoryOperationKind.Connected, Day1, Day1),
            EdgeEnvelope(GraphHistoryOperationKind.Disconnected, Day3, Day3)
        };

        var connected = Project(history, Day2, Day4);
        var disconnected = Project(history, Day4, Day4);

        Assert.HasCount(1, connected.Snapshot.Edges);
        Assert.IsEmpty(disconnected.Snapshot.Edges);
        Assert.AreEqual(1, disconnected.InactiveEntityStates);
    }

    [TestMethod]
    public void Project_IsDeterministicAcrossHistoryOrderAndQueryableInput()
    {
        var history = new[]
        {
            NodeEnvelope("alpha", GraphHistoryOperationKind.Created, Day1, Day1),
            NodeEnvelope("beta", GraphHistoryOperationKind.Created, Day1, Day1, BetaId),
            EdgeEnvelope(GraphHistoryOperationKind.Connected, Day1, Day1)
        };

        var first = Project(history, Day2, Day2);
        var second = Project(history.Reverse().AsQueryable(), Day2, Day2);

        Assert.AreEqual(first.Snapshot.Identity.ContentFingerprint, second.Snapshot.Identity.ContentFingerprint);
        Assert.AreEqual(first.Snapshot.Identity.SnapshotId, second.Snapshot.Identity.SnapshotId);
    }

    [TestMethod]
    public void Project_UsesHalfOpenValidityIntervals()
    {
        var history = new[]
        {
            NodeEnvelope("old", GraphHistoryOperationKind.Created, Day1, Day1, validTo: Day3),
            NodeEnvelope("new", GraphHistoryOperationKind.Updated, Day2, Day3)
        };

        var atBoundary = Project(history, Day3, Day4);

        Assert.AreEqual("new", atBoundary.Snapshot.Nodes[0].Materialize<TestNode>().Name);
        Assert.AreEqual(1, atBoundary.ApplicableHistoryEntries);
    }

    [TestMethod]
    public void Project_RejectsAmbiguousState()
    {
        var history = new[]
        {
            NodeEnvelope("first", GraphHistoryOperationKind.Created, Day1, Day1),
            NodeEnvelope("second", GraphHistoryOperationKind.Updated, Day1, Day1)
        };

        var exception = Assert.ThrowsExactly<GraphWorldHistoryProjectionException>(() => Project(history, Day2, Day2));

        Assert.AreEqual(GraphWorldHistoryProjectionFailureReason.AmbiguousState, exception.Reason);
    }

    [TestMethod]
    public void Project_RejectsInvalidEnvelopeAndEntryOverflow()
    {
        var invalid = NodeEnvelope("alpha", GraphHistoryOperationKind.Created, Day1, Day1);
        invalid = Copy(invalid, entityId: BetaId);

        var invalidException = Assert.ThrowsExactly<GraphWorldHistoryProjectionException>(() => Project([invalid], Day2, Day2));
        Assert.AreEqual(GraphWorldHistoryProjectionFailureReason.InvalidEnvelope, invalidException.Reason);

        var limitException = Assert.ThrowsExactly<GraphWorldHistoryProjectionException>(() =>
            GraphWorldHistoryProjector.Project(
                new GraphWorldHistoryProjector.ProjectParameters
                {
                    WorldKey = new GraphProjectionKey("orders/eu"),
                    Version = 1,
                    ValidAt = new DateTimeOffset(Day2),
                    RecordedAt = new DateTimeOffset(Day2),
                    History = [
                        NodeEnvelope("alpha", GraphHistoryOperationKind.Created, Day1, Day1),
                        NodeEnvelope("beta", GraphHistoryOperationKind.Created, Day1, Day1, BetaId)
                    ],
                    Options = new GraphWorldHistoryProjectionOptions { MaximumHistoryEntries = 1 }
                }
            ));
        Assert.AreEqual(GraphWorldHistoryProjectionFailureReason.EntryLimitExceeded, limitException.Reason);
    }

    [TestMethod]
    public void Project_ObservesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsExactly<OperationCanceledException>(() => GraphWorldHistoryProjector.Project(
            new GraphWorldHistoryProjector.ProjectParameters
            {
                WorldKey = new GraphProjectionKey("orders/eu"),
                Version = 1,
                ValidAt = new DateTimeOffset(Day2),
                RecordedAt = new DateTimeOffset(Day2),
                History = [NodeEnvelope("alpha", GraphHistoryOperationKind.Created, Day1, Day1)],
                CancellationToken = cancellation.Token
            }
        ));
    }

    private static GraphWorldHistoryProjectionResult Project(IEnumerable<GraphHistoryEnvelope> history, DateTime validAt, DateTime recordedAt) =>
        GraphWorldHistoryProjector.Project(
            new GraphWorldHistoryProjector.ProjectParameters
            {
                WorldKey = new GraphProjectionKey("orders/eu"),
                Version = 1,
                ValidAt = new DateTimeOffset(validAt),
                RecordedAt = new DateTimeOffset(recordedAt),
                History = history
            });

    private static GraphHistoryEnvelope NodeEnvelope(
        string name,
        GraphHistoryOperationKind operation,
        DateTime capturedAt,
        DateTime validFrom,
        Guid? id = null,
        DateTime? validTo = null) =>
        GraphHistoryEnvelope.ForNode(
            new TestNode { Id = id ?? AlphaId, Name = name },
            operation,
            capturedAt,
            validFrom,
            validTo);

    private static GraphHistoryEnvelope EdgeEnvelope(GraphHistoryOperationKind operation, DateTime capturedAt, DateTime validFrom) =>
        GraphHistoryEnvelope.ForEdge(
            new TestEdge { Id = EdgeId, FromId = AlphaId, ToId = BetaId },
            operation,
            capturedAt,
            validFrom);

    private static GraphHistoryEnvelope Copy(GraphHistoryEnvelope source, Guid entityId) =>
        new()
        {
            EntityType = source.EntityType,
            EntityId = entityId,
            OperationKind = source.OperationKind,
            CapturedAtUtc = source.CapturedAtUtc,
            ValidFromUtc = source.ValidFromUtc,
            ValidToUtc = source.ValidToUtc,
            IsEdge = source.IsEdge,
            FromId = source.FromId,
            ToId = source.ToId,
            Snapshot = source.Snapshot
        };

    private sealed class TestNode : Node
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class TestEdge : Edge;
}