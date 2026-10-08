using Gorm.Application.History;
using Gorm.Application.History.Envelopes;
using Gorm.Application.History.Storage;
using Gorm.Application.Intelligence.Projection;
using Gorm.Application.Temporal.Bitemporal;
using Gorm.Application.Temporal.History;
using Gorm.Demo.Domain.Edges;
using Gorm.Demo.Domain.Nodes;
using Gorm.Infrastructure.Providers.SqlServer;
using Microsoft.Data.SqlClient;

namespace Gorm.SqlServer.Tests;

public sealed partial class SqlServerGraphIntegrationTests
{
    private static readonly DateTime BitemporalDay1 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime BitemporalDay2 = BitemporalDay1.AddDays(1);
    private static readonly DateTime BitemporalDay3 = BitemporalDay1.AddDays(2);
    private static readonly DateTime BitemporalDay4 = BitemporalDay1.AddDays(3);
    private static readonly DateTime BitemporalDay5 = BitemporalDay1.AddDays(4);

    [TestMethod]
    public async Task SqlBitemporal_LateRecordedCorrection_PreservesPastKnowledgeAndUpdatesLaterView()
    {
        var id = Guid.NewGuid();
        await PersistBitemporalHistoryAsync(
            GraphHistoryEnvelope.ForNode(NewHistoryNode(id, "original"), GraphHistoryOperationKind.Created, BitemporalDay1, BitemporalDay1),
            GraphHistoryEnvelope.ForNode(NewHistoryNode(id, "corrected"), GraphHistoryOperationKind.Updated, BitemporalDay3, BitemporalDay1));

        var dataset = await BitemporalReader().CaptureBitemporalDatasetAsync<CharacteristicSpecificationNode, CharacteristicSpecificationMapEdge>(
            [id], [], cancellationToken: TestContext.CancellationToken);
        var result = dataset.Compare(
            new GraphProjectionKey("temporal/late-correction"), 1, At(BitemporalDay2, BitemporalDay2),
            2, At(BitemporalDay2, BitemporalDay4));

        Assert.AreEqual(GraphBitemporalComparisonKind.RecordedKnowledgeCorrection, result.Kind);
        Assert.AreEqual("original", result.First.Snapshot.Nodes.Single().Materialize<CharacteristicSpecificationNode>().Name);
        Assert.AreEqual("corrected", result.Second.Snapshot.Nodes.Single().Materialize<CharacteristicSpecificationNode>().Name);
        Assert.AreEqual(1, result.Difference.ModifiedCount);
        Assert.AreEqual(1, result.First.KnownHistoryEntries);
        Assert.AreEqual(2, result.Second.KnownHistoryEntries);
    }

    [TestMethod]
    public async Task SqlBitemporal_HalfOpenValidTimeBoundary_SelectsCorrectBusinessState()
    {
        var id = Guid.NewGuid();
        await PersistBitemporalHistoryAsync(
            GraphHistoryEnvelope.ForNode(NewHistoryNode(id, "old"), GraphHistoryOperationKind.Created, BitemporalDay1, BitemporalDay1, BitemporalDay3),
            GraphHistoryEnvelope.ForNode(NewHistoryNode(id, "new"), GraphHistoryOperationKind.Updated, BitemporalDay2, BitemporalDay3));

        var dataset = await BitemporalReader().CaptureBitemporalDatasetAsync<CharacteristicSpecificationNode, CharacteristicSpecificationMapEdge>(
            [id], [], cancellationToken: TestContext.CancellationToken);
        var earlier = dataset.Project(new GraphProjectionKey("temporal/validity"), 1, At(BitemporalDay2, BitemporalDay4), cancellationToken: TestContext.CancellationToken);
        var boundary = dataset.Project(new GraphProjectionKey("temporal/validity"), 2, At(BitemporalDay3, BitemporalDay4), cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual("old", earlier.Snapshot.Nodes.Single().Materialize<CharacteristicSpecificationNode>().Name);
        Assert.AreEqual("new", boundary.Snapshot.Nodes.Single().Materialize<CharacteristicSpecificationNode>().Name);
        Assert.AreEqual(1, boundary.ApplicableHistoryEntries);
        var evolution = dataset.Compare(new GraphProjectionKey("temporal/validity"), 3, At(BitemporalDay2, BitemporalDay4), 4, At(BitemporalDay3, BitemporalDay4));
        Assert.AreEqual(GraphBitemporalComparisonKind.ValidTimeEvolution, evolution.Kind);
    }

    [TestMethod]
    public async Task SqlBitemporal_EdgeCorrection_UsesBothRecordedAndValidTime()
    {
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var edgeId = Guid.NewGuid();
        await PersistBitemporalHistoryAsync(
            GraphHistoryEnvelope.ForNode(NewHistoryNode(sourceId, "Source"), GraphHistoryOperationKind.Created, BitemporalDay1, BitemporalDay1),
            GraphHistoryEnvelope.ForNode(NewHistoryNode(targetId, "Target"), GraphHistoryOperationKind.Created, BitemporalDay1, BitemporalDay1),
            GraphHistoryEnvelope.ForEdge(NewHistoryEdge(edgeId, sourceId, targetId), GraphHistoryOperationKind.Connected, BitemporalDay2, BitemporalDay1),
            GraphHistoryEnvelope.ForEdge(NewHistoryEdge(edgeId, sourceId, targetId), GraphHistoryOperationKind.Disconnected, BitemporalDay4, BitemporalDay3));

        var dataset = await BitemporalReader().CaptureBitemporalDatasetAsync<CharacteristicSpecificationNode, CharacteristicSpecificationMapEdge>(
            [sourceId, targetId], [edgeId], cancellationToken: TestContext.CancellationToken);
        var key = new GraphProjectionKey("temporal/relationship");
        var beforeKnowledge = dataset.Project(key, 1, At(BitemporalDay2, BitemporalDay1), cancellationToken: TestContext.CancellationToken);
        var knownConnected = dataset.Project(key, 2, At(BitemporalDay2, BitemporalDay3), cancellationToken: TestContext.CancellationToken);
        var historicalConnected = dataset.Project(key, 3, At(BitemporalDay2, BitemporalDay5), cancellationToken: TestContext.CancellationToken);
        var knownDisconnected = dataset.Project(key, 4, At(BitemporalDay3, BitemporalDay5), cancellationToken: TestContext.CancellationToken);

        Assert.IsEmpty(beforeKnowledge.Snapshot.Edges);
        Assert.HasCount(2, knownConnected.Snapshot.Nodes);
        Assert.HasCount(1, knownConnected.Snapshot.Edges);
        Assert.HasCount(1, historicalConnected.Snapshot.Edges);
        Assert.AreEqual(edgeId, historicalConnected.Snapshot.Edges.Single().Id);
        Assert.IsEmpty(knownDisconnected.Snapshot.Edges);
        Assert.AreEqual(1, knownDisconnected.InactiveEntityStates);
    }

    [TestMethod]
    public async Task SqlBitemporal_DatasetCapturedBeforeCorrection_RemainsRepeatable()
    {
        var id = Guid.NewGuid();
        await PersistBitemporalHistoryAsync(
            GraphHistoryEnvelope.ForNode(NewHistoryNode(id, "first"), GraphHistoryOperationKind.Created, BitemporalDay1, BitemporalDay1));

        var reader = BitemporalReader();
        var originalDataset = await reader.CaptureBitemporalDatasetAsync<CharacteristicSpecificationNode, CharacteristicSpecificationMapEdge>(
            [id], [], cancellationToken: TestContext.CancellationToken);
        await PersistBitemporalHistoryAsync(
            GraphHistoryEnvelope.ForNode(NewHistoryNode(id, "corrected"), GraphHistoryOperationKind.Updated, BitemporalDay3, BitemporalDay1));
        var updatedDataset = await reader.CaptureBitemporalDatasetAsync<CharacteristicSpecificationNode, CharacteristicSpecificationMapEdge>(
            [id], [], cancellationToken: TestContext.CancellationToken);

        var original = originalDataset.Project(new GraphProjectionKey("temporal/repeatable"), 1, At(BitemporalDay2, BitemporalDay4));
        var updated = updatedDataset.Project(new GraphProjectionKey("temporal/repeatable"), 1, At(BitemporalDay2, BitemporalDay4));

        Assert.AreEqual(1, originalDataset.Count);
        Assert.AreEqual(2, updatedDataset.Count);
        Assert.AreEqual("first", original.Snapshot.Nodes.Single().Materialize<CharacteristicSpecificationNode>().Name);
        Assert.AreEqual("corrected", updated.Snapshot.Nodes.Single().Materialize<CharacteristicSpecificationNode>().Name);
    }

    [TestMethod]
    public async Task SqlBitemporal_InvalidValidityWindow_RejectsEntireHistoryBatch()
    {
        var id = Guid.NewGuid();
        var invalid = GraphHistoryEnvelope.ForNode(NewHistoryNode(Guid.NewGuid(), "invalid"),
            GraphHistoryOperationKind.Created, BitemporalDay3, BitemporalDay3, BitemporalDay2);
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => BitemporalRecorder().PersistAsync(
            [GraphHistoryEnvelope.ForNode(NewHistoryNode(id, "good"), GraphHistoryOperationKind.Created, BitemporalDay1, BitemporalDay1), invalid],
            connection: null, transaction: null, TestContext.CancellationToken));

        var persisted = await BitemporalReader().ReadNodeHistoryAsync<CharacteristicSpecificationNode>(TestContext.CancellationToken);
        Assert.IsFalse(persisted.Any(x => x.EntityId == id), "Invalid history must not cause partial batch inserts.");
    }

    [TestMethod]
    public async Task SqlBitemporal_RolledBackRecordedCorrection_DoesNotAlterKnownHistory()
    {
        var id = Guid.NewGuid();
        await PersistBitemporalHistoryAsync(
            GraphHistoryEnvelope.ForNode(NewHistoryNode(id, "initial"), GraphHistoryOperationKind.Created, BitemporalDay1, BitemporalDay1));

        await using var connection = new SqlConnection(_databaseConnectionString);
        await connection.OpenAsync(TestContext.CancellationToken);
        await using (var transaction = await connection.BeginTransactionAsync(TestContext.CancellationToken))
        {
            var correction = GraphHistoryEnvelope.ForNode(NewHistoryNode(id, "uncommitted"),
                GraphHistoryOperationKind.Updated, BitemporalDay3, BitemporalDay1);
            await BitemporalRecorder().PersistAsync([correction], connection, transaction, TestContext.CancellationToken);
            await transaction.RollbackAsync(TestContext.CancellationToken);
        }

        var dataset = await BitemporalReader().CaptureBitemporalDatasetAsync<CharacteristicSpecificationNode, CharacteristicSpecificationMapEdge>(
            [id], [], cancellationToken: TestContext.CancellationToken);
        var result = dataset.Project(new GraphProjectionKey("temporal/rollback"), 1, At(BitemporalDay2, BitemporalDay5));

        Assert.AreEqual(1, dataset.Count);
        Assert.AreEqual("initial", result.Snapshot.Nodes.Single().Materialize<CharacteristicSpecificationNode>().Name);
    }

    [TestMethod]
    public async Task SqlBitemporal_HistoryLimit_RejectsOversizedSelectedDataset()
    {
        var id = Guid.NewGuid();
        await PersistBitemporalHistoryAsync(
            GraphHistoryEnvelope.ForNode(NewHistoryNode(id, "first"), GraphHistoryOperationKind.Created, BitemporalDay1, BitemporalDay1),
            GraphHistoryEnvelope.ForNode(NewHistoryNode(id, "second"), GraphHistoryOperationKind.Updated, BitemporalDay2, BitemporalDay1));

        await Assert.ThrowsExactlyAsync<GraphWorldHistoryProjectionException>(() =>
            BitemporalReader().CaptureBitemporalDatasetAsync<CharacteristicSpecificationNode, CharacteristicSpecificationMapEdge>(
                [id], [], maximumHistoryEntries: 1, cancellationToken: TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task SqlBitemporal_AtomicCapture_IncludesCommittedNodeAndEdgeHistory()
    {
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var edgeId = Guid.NewGuid();
        await PersistBitemporalHistoryAsync(
            GraphHistoryEnvelope.ForNode(NewHistoryNode(sourceId, "Source"), GraphHistoryOperationKind.Created, BitemporalDay1, BitemporalDay1),
            GraphHistoryEnvelope.ForNode(NewHistoryNode(targetId, "Target"), GraphHistoryOperationKind.Created, BitemporalDay1, BitemporalDay1),
            GraphHistoryEnvelope.ForEdge(NewHistoryEdge(edgeId, sourceId, targetId), GraphHistoryOperationKind.Connected, BitemporalDay1, BitemporalDay1));

        var dataset = await BitemporalReader().CaptureBitemporalDatasetAsync<CharacteristicSpecificationNode, CharacteristicSpecificationMapEdge>(
            [sourceId, targetId], [edgeId], cancellationToken: TestContext.CancellationToken);
        var result = dataset.Project(new GraphProjectionKey("temporal/atomic"), 1, At(BitemporalDay2, BitemporalDay2));

        Assert.AreEqual(3, dataset.Count);
        Assert.HasCount(2, result.Snapshot.Nodes);
        Assert.HasCount(1, result.Snapshot.Edges);
        Assert.AreEqual(edgeId, result.Snapshot.Edges.Single().Id);
    }

    private static GraphBitemporalCoordinate At(DateTime validAt, DateTime recordedAt) =>
        new(new DateTimeOffset(validAt), new DateTimeOffset(recordedAt));

    private static CharacteristicSpecificationNode NewHistoryNode(Guid id, string name) =>
        new() { Id = id, Name = name, Payload = "{}" };

    private static CharacteristicSpecificationMapEdge NewHistoryEdge(Guid id, Guid from, Guid to) =>
        new() { Id = id, FromId = from, ToId = to, Payload = "{}" };

    private SqlServerGraphHistoryRecorder BitemporalRecorder() => new(CreateContext().ConnectionFactory!);

    private SqlServerGraphHistoryReader BitemporalReader() => new(CreateContext().ConnectionFactory!);

    private Task PersistBitemporalHistoryAsync(params GraphHistoryEnvelope[] envelopes) =>
        BitemporalRecorder().PersistAsync(envelopes, connection: null, transaction: null, TestContext.CancellationToken);
}
