using Gorm.Application.Context;
using Gorm.Application.History;
using Gorm.Application.History.Querying;
using Gorm.Application.History.Storage;
using Gorm.Application.Execution;
using Gorm.Demo.Domain.Edges;
using Gorm.Demo.Infrastructure;
using Gorm.Demo.Domain.Nodes;

namespace Gorm.SqlServer.Tests;

public sealed partial class SqlServerGraphIntegrationTests
{
    [TestMethod]
    public async Task SqlHistory_NodeLifecycle_AsOfResolvesCreatedUpdatedAndDeleted()
    {
        var clock = new ControlledHistoryTimeProvider(new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero));
        var context = CreateHistoryContext(clock);
        var node = new CharacteristicSpecificationNode { Id = Guid.NewGuid(), Name = "initial", Payload = "{}" };
        var beforeCreate = clock.GetUtcNow().UtcDateTime.AddTicks(-1);
        context.Add(node);
        await context.SaveChangesAsync(TestContext.CancellationToken);
        var atCreate = clock.GetUtcNow().UtcDateTime;

        clock.Advance(TimeSpan.FromMinutes(1));
        node.Name = "updated";
        context.Update(node);
        await context.SaveChangesAsync(TestContext.CancellationToken);
        var atUpdate = clock.GetUtcNow().UtcDateTime;

        clock.Advance(TimeSpan.FromMinutes(1));
        context.Remove(node);
        await context.SaveChangesAsync(TestContext.CancellationToken);
        var atDelete = clock.GetUtcNow().UtcDateTime;

        var history = (await new SqlServerGraphHistoryReader(context.ConnectionFactory!)
            .ReadNodeHistoryAsync<CharacteristicSpecificationNode>(TestContext.CancellationToken))
            .Where(x => x.EntityId == node.Id).ToList();

        Assert.IsTrue(history.Any(x => x.OperationKind == GraphHistoryOperationKind.Created));
        Assert.IsTrue(history.Any(x => x.OperationKind == GraphHistoryOperationKind.Updated));
        Assert.IsTrue(history.Any(x => x.OperationKind == GraphHistoryOperationKind.Deleted));

        var snapshots = history.AsQueryable();
        Assert.IsEmpty(snapshots.AsOf(beforeCreate).ToList());
        Assert.AreEqual("initial", snapshots.AsOf(atCreate).SelectNodeSnapshot<CharacteristicSpecificationNode>().Single().Name);
        Assert.AreEqual("updated", snapshots.AsOf(atUpdate).SelectNodeSnapshot<CharacteristicSpecificationNode>().Single().Name);
        Assert.IsEmpty(snapshots.AsOf(atDelete).ToList());
        Assert.IsEmpty(snapshots.Current().ToList());
    }

    [TestMethod]
    public async Task SqlHistory_ConnectedEdge_RestoresPersistedEndpointsAndOutgoingTraversal()
    {
        var clock = new ControlledHistoryTimeProvider(new DateTimeOffset(2026, 2, 1, 10, 0, 0, TimeSpan.Zero));
        var context = CreateHistoryContext(clock);
        var source = new CharacteristicSpecificationNode { Id = Guid.NewGuid(), Name = "Source", Payload = "{}" };
        var target = new CharacteristicSpecificationNode { Id = Guid.NewGuid(), Name = "Target", Payload = "{}" };
        context.AddRange(source, target);
        await context.SaveChangesAsync(TestContext.CancellationToken);
        var beforeConnect = clock.GetUtcNow().UtcDateTime;

        clock.Advance(TimeSpan.FromMinutes(1));
        var edge = context.Connect<CharacteristicSpecificationMapEdge, CharacteristicSpecificationNode, CharacteristicSpecificationNode>(
            source, target, x => { x.Id = Guid.NewGuid(); x.Payload = "{}"; });
        await context.SaveChangesAsync(TestContext.CancellationToken);
        var afterConnect = clock.GetUtcNow().UtcDateTime;

        var reader = new SqlServerGraphHistoryReader(context.ConnectionFactory!);
        var persistedEdges = await reader.ReadEdgeHistoryAsync<CharacteristicSpecificationMapEdge>(TestContext.CancellationToken);
        var record = persistedEdges.Single(x => x.EntityId == edge.Id && x.OperationKind == GraphHistoryOperationKind.Connected);
        Assert.AreEqual(source.Id, record.FromId);
        Assert.AreEqual(target.Id, record.ToId);
        Assert.AreEqual(source.Id, record.GetSnapshotOfType<CharacteristicSpecificationMapEdge>().FromId);
        Assert.AreEqual(target.Id, record.GetSnapshotOfType<CharacteristicSpecificationMapEdge>().ToId);

        var before = context.History<CharacteristicSpecificationNode>().AsOf(beforeConnect)
            .Where(x => x.EntityId == source.Id)
            .TemporalOutgoing<CharacteristicSpecificationMapEdge, CharacteristicSpecificationNode>(context).ToList();
        var after = context.History<CharacteristicSpecificationNode>().AsOf(afterConnect)
            .Where(x => x.EntityId == source.Id)
            .TemporalOutgoing<CharacteristicSpecificationMapEdge, CharacteristicSpecificationNode>(context).ToList();

        Assert.IsEmpty(before);
        Assert.HasCount(1, after);
        Assert.AreEqual(target.Id, after[0].Id);
    }

    [TestMethod]
    public async Task SqlHistory_ConnectedEdge_RestoresIncomingTraversalFromSqlSnapshots()
    {
        var clock = new ControlledHistoryTimeProvider(new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero));
        var context = CreateHistoryContext(clock);
        var source = new CharacteristicSpecificationNode { Id = Guid.NewGuid(), Name = "Source", Payload = "{}" };
        var target = new CharacteristicSpecificationNode { Id = Guid.NewGuid(), Name = "Target", Payload = "{}" };
        context.AddRange(source, target);
        await context.SaveChangesAsync(TestContext.CancellationToken);

        clock.Advance(TimeSpan.FromMinutes(1));
        context.Connect<CharacteristicSpecificationMapEdge, CharacteristicSpecificationNode, CharacteristicSpecificationNode>(
            source, target, x => { x.Id = Guid.NewGuid(); x.Payload = "{}"; });
        await context.SaveChangesAsync(TestContext.CancellationToken);

        var incoming = context.History<CharacteristicSpecificationNode>()
            .AsOf(clock.GetUtcNow().UtcDateTime)
            .Where(x => x.EntityId == target.Id)
            .TemporalIncoming<CharacteristicSpecificationMapEdge, CharacteristicSpecificationNode>(context)
            .ToList();

        Assert.HasCount(1, incoming);
        Assert.AreEqual(source.Id, incoming[0].Id);
    }

    [TestMethod]
    public async Task SqlHistory_RolledBackTransaction_HasNoCommittedNodeOrHistory()
    {
        var clock = new ControlledHistoryTimeProvider(new DateTimeOffset(2026, 4, 1, 10, 0, 0, TimeSpan.Zero));
        var context = CreateHistoryContext(clock);
        var id = Guid.NewGuid();

        await using (var transaction = await context.BeginTransactionAsync(TestContext.CancellationToken))
        {
            context.Add(new CharacteristicSpecificationNode { Id = id, Name = "NeverCommitted", Payload = "{}" });
            await context.SaveChangesAsync(TestContext.CancellationToken);
            await transaction.RollbackAsync(TestContext.CancellationToken);
        }

        var nodeIsPersisted = await CreateContext().CharacteristicSpecifications.Where(x => x.Id == id)
            .AnyAsync(TestContext.CancellationToken);
        var history = await new SqlServerGraphHistoryReader(context.ConnectionFactory!)
            .ReadNodeHistoryAsync<CharacteristicSpecificationNode>(TestContext.CancellationToken);

        Assert.IsFalse(nodeIsPersisted);
        Assert.IsFalse(history.Any(x => x.EntityId == id), "History must roll back atomically with the node insert.");
    }

    private SqlSpecificationGraphContext CreateHistoryContext(ControlledHistoryTimeProvider clock)
    {
        var context = CreateContext();
        context.UseSqlServerHistory(context.ConnectionFactory!);
        context.UseTimeProvider(clock);
        return context;
    }

    private sealed class ControlledHistoryTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan duration) => _now = _now.Add(duration);
    }
}
