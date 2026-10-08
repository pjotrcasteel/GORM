using Gorm.Application.History;
using Gorm.Application.History.Querying;
using Gorm.Application.History.Storage;
using Gorm.Demo.Domain.Edges;
using Gorm.Demo.Domain.Nodes;
using Gorm.Demo.Infrastructure;

namespace Gorm.SqlServer.Tests;

public sealed partial class SqlServerGraphIntegrationTests
{
    [TestMethod]
    public async Task SqlHistory_Disconnect_PreservesBeforeAndRemovesAfterRelationship()
    {
        var clock = new ControlledHistoryTimeProvider(new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.Zero));
        var context = CreateHistoryContext(clock);
        var (source, target) = await CreateHistoryNodesAsync(context);

        clock.Advance(TimeSpan.FromMinutes(1));
        var edge = ConnectHistoricalEdge(context, source, target);
        await context.SaveChangesAsync(TestContext.CancellationToken);
        var beforeDisconnect = clock.GetUtcNow().UtcDateTime;

        clock.Advance(TimeSpan.FromMinutes(1));
        context.Disconnect<CharacteristicSpecificationMapEdge, CharacteristicSpecificationNode, CharacteristicSpecificationNode>(source, target);
        await context.SaveChangesAsync(TestContext.CancellationToken);
        var afterDisconnect = clock.GetUtcNow().UtcDateTime;

        var edges = (await new SqlServerGraphHistoryReader(context.ConnectionFactory!)
                .ReadEdgeHistoryAsync<CharacteristicSpecificationMapEdge>(TestContext.CancellationToken))
            .Where(x => x.EntityId == edge.Id).ToList();
        Assert.HasCount(2, edges);
        Assert.AreEqual(GraphHistoryOperationKind.Connected, edges[0].OperationKind);
        Assert.AreEqual(GraphHistoryOperationKind.Disconnected, edges[1].OperationKind);
        Assert.AreEqual(source.Id, edges[1].FromId);
        Assert.AreEqual(target.Id, edges[1].ToId);

        Assert.HasCount(1, HistoricalTargets(context, source.Id, beforeDisconnect));
        Assert.IsEmpty(HistoricalTargets(context, source.Id, afterDisconnect));
        Assert.IsEmpty(context.EdgeHistory<CharacteristicSpecificationMapEdge>().Where(x => x.EntityId == edge.Id).Current().ToList());
    }

    [TestMethod]
    public async Task SqlHistory_ExplicitEdgeDelete_PreservesEarlierSnapshotAndRemovesCurrent()
    {
        var clock = new ControlledHistoryTimeProvider(new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero));
        var context = CreateHistoryContext(clock);
        var (source, target) = await CreateHistoryNodesAsync(context);

        clock.Advance(TimeSpan.FromMinutes(1));
        var edge = ConnectHistoricalEdge(context, source, target);
        await context.SaveChangesAsync(TestContext.CancellationToken);
        var beforeDelete = clock.GetUtcNow().UtcDateTime;

        clock.Advance(TimeSpan.FromMinutes(1));
        context.Remove(edge);
        await context.SaveChangesAsync(TestContext.CancellationToken);
        var afterDelete = clock.GetUtcNow().UtcDateTime;

        var edges = (await new SqlServerGraphHistoryReader(context.ConnectionFactory!)
                .ReadEdgeHistoryAsync<CharacteristicSpecificationMapEdge>(TestContext.CancellationToken))
            .Where(x => x.EntityId == edge.Id).ToList();

        Assert.IsTrue(edges.Any(x => x.OperationKind == GraphHistoryOperationKind.Deleted));
        var deleted = edges.Single(x => x.OperationKind == GraphHistoryOperationKind.Deleted);
        Assert.AreEqual(source.Id, deleted.FromId);
        Assert.AreEqual(target.Id, deleted.ToId);
        Assert.HasCount(1, HistoricalTargets(context, source.Id, beforeDelete));
        Assert.IsEmpty(HistoricalTargets(context, source.Id, afterDelete));
    }

    [TestMethod]
    public async Task SqlHistory_ReconnectAfterDisconnect_RestoresLaterTraversal()
    {
        var clock = new ControlledHistoryTimeProvider(new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero));
        var context = CreateHistoryContext(clock);
        var (source, target) = await CreateHistoryNodesAsync(context);

        clock.Advance(TimeSpan.FromMinutes(1));
        var first = ConnectHistoricalEdge(context, source, target);
        await context.SaveChangesAsync(TestContext.CancellationToken);
        var connected = clock.GetUtcNow().UtcDateTime;

        clock.Advance(TimeSpan.FromMinutes(1));
        context.Disconnect<CharacteristicSpecificationMapEdge, CharacteristicSpecificationNode, CharacteristicSpecificationNode>(source, target);
        await context.SaveChangesAsync(TestContext.CancellationToken);
        var disconnected = clock.GetUtcNow().UtcDateTime;

        clock.Advance(TimeSpan.FromMinutes(1));
        var second = ConnectHistoricalEdge(context, source, target);
        await context.SaveChangesAsync(TestContext.CancellationToken);
        var reconnected = clock.GetUtcNow().UtcDateTime;

        Assert.AreNotEqual(first.Id, second.Id);
        Assert.HasCount(1, HistoricalTargets(context, source.Id, connected));
        Assert.IsEmpty(HistoricalTargets(context, source.Id, disconnected));
        Assert.HasCount(1, HistoricalTargets(context, source.Id, reconnected));
        var current = context.EdgeHistory<CharacteristicSpecificationMapEdge>().Current().Where(x => x.EntityId == second.Id).ToList();
        Assert.HasCount(1, current);
    }

    [TestMethod]
    public async Task SqlHistory_ParallelEdges_AreTrackedByPersistentEdgeIdentity()
    {
        var clock = new ControlledHistoryTimeProvider(new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero));
        var context = CreateHistoryContext(clock);
        var (source, target) = await CreateHistoryNodesAsync(context);

        clock.Advance(TimeSpan.FromMinutes(1));
        var first = ConnectHistoricalEdge(context, source, target);
        await context.SaveChangesAsync(TestContext.CancellationToken);
        clock.Advance(TimeSpan.FromMinutes(1));
        var second = ConnectHistoricalEdge(context, source, target);
        await context.SaveChangesAsync(TestContext.CancellationToken);
        var beforeDelete = clock.GetUtcNow().UtcDateTime;

        Assert.HasCount(2, HistoricalTargets(context, source.Id, beforeDelete));

        clock.Advance(TimeSpan.FromMinutes(1));
        context.Remove(first);
        await context.SaveChangesAsync(TestContext.CancellationToken);
        var afterDelete = clock.GetUtcNow().UtcDateTime;

        Assert.HasCount(1, HistoricalTargets(context, source.Id, afterDelete));
        var active = context.EdgeHistory<CharacteristicSpecificationMapEdge>().Current().ToList();
        Assert.IsFalse(active.Any(x => x.EntityId == first.Id));
        Assert.IsTrue(active.Any(x => x.EntityId == second.Id));
    }

    [TestMethod]
    public async Task SqlHistory_DisconnectRollback_LeavesCommittedEdgeAndHistoryIntact()
    {
        var clock = new ControlledHistoryTimeProvider(new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero));
        var context = CreateHistoryContext(clock);
        var (source, target) = await CreateHistoryNodesAsync(context);
        clock.Advance(TimeSpan.FromMinutes(1));
        var edge = ConnectHistoricalEdge(context, source, target);
        await context.SaveChangesAsync(TestContext.CancellationToken);
        var connected = clock.GetUtcNow().UtcDateTime;

        clock.Advance(TimeSpan.FromMinutes(1));
        await using (var transaction = await context.BeginTransactionAsync(TestContext.CancellationToken))
        {
            context.Disconnect<CharacteristicSpecificationMapEdge, CharacteristicSpecificationNode, CharacteristicSpecificationNode>(source, target);
            await context.SaveChangesAsync(TestContext.CancellationToken);
            await transaction.RollbackAsync(TestContext.CancellationToken);
        }

        var history = (await new SqlServerGraphHistoryReader(context.ConnectionFactory!)
                .ReadEdgeHistoryAsync<CharacteristicSpecificationMapEdge>(TestContext.CancellationToken))
            .Where(x => x.EntityId == edge.Id).ToList();

        Assert.HasCount(1, history);
        Assert.AreEqual(GraphHistoryOperationKind.Connected, history[0].OperationKind);
        Assert.HasCount(1, HistoricalTargets(context, source.Id, connected));
        Assert.HasCount(1, HistoricalTargets(context, source.Id, clock.GetUtcNow().UtcDateTime));
    }

    private async Task<(CharacteristicSpecificationNode Source, CharacteristicSpecificationNode Target)> CreateHistoryNodesAsync(
        SqlSpecificationGraphContext context)
    {
        var source = new CharacteristicSpecificationNode { Id = Guid.NewGuid(), Name = "Source", Payload = "{}" };
        var target = new CharacteristicSpecificationNode { Id = Guid.NewGuid(), Name = "Target", Payload = "{}" };
        context.AddRange(source, target);
        await context.SaveChangesAsync(TestContext.CancellationToken);
        return (source, target);
    }

    private static CharacteristicSpecificationMapEdge ConnectHistoricalEdge(SqlSpecificationGraphContext context,
        CharacteristicSpecificationNode source, CharacteristicSpecificationNode target) =>
        context.Connect<CharacteristicSpecificationMapEdge, CharacteristicSpecificationNode, CharacteristicSpecificationNode>(
            source, target, x => { x.Id = Guid.NewGuid(); x.Payload = "{}"; });

    private static List<CharacteristicSpecificationNode> HistoricalTargets(SqlSpecificationGraphContext context, Guid sourceId, DateTime instant) =>
        context.History<CharacteristicSpecificationNode>().AsOf(instant)
            .Where(x => x.EntityId == sourceId)
            .TemporalOutgoing<CharacteristicSpecificationMapEdge, CharacteristicSpecificationNode>(context)
            .ToList();
}
