using Gorm.Application.Context;
using Gorm.Application.Execution;
using Gorm.Application.Querying;
using Gorm.Core.Configuration;
using Gorm.Core.Primitives;
using Gorm.Infrastructure.Persistence.Connections;
using Gorm.Infrastructure.Providers.SqlServer;

namespace Gorm.SqlServer.Tests;

public sealed partial class SqlServerGraphIntegrationTests
{
    [TestMethod]
    public async Task NestedTransaction_InnerRollback_PreservesOuterChanges()
    {
        var context = CreateVersionedContext();
        var outerId = Guid.NewGuid();
        var rolledBackId = Guid.NewGuid();
        var laterId = Guid.NewGuid();

        await using (var outer = await context.BeginTransactionAsync(TestContext.CancellationToken))
        {
            context.Add(new VersionedGraphNode { Id = outerId, Name = "outer" });
            await context.SaveChangesAsync(TestContext.CancellationToken);

            await using (var nested = await context.BeginTransactionAsync(TestContext.CancellationToken))
            {
                Assert.IsTrue(nested.IsNested);
                context.Add(new VersionedGraphNode { Id = rolledBackId, Name = "rollback" });
                await context.SaveChangesAsync(TestContext.CancellationToken);
                await nested.RollbackAsync(TestContext.CancellationToken);
            }

            context.Add(new VersionedGraphNode { Id = laterId, Name = "later" });
            await context.SaveChangesAsync(TestContext.CancellationToken);
            await outer.CommitAsync(TestContext.CancellationToken);
        }

        var reader = CreateVersionedContext();
        var persisted = await reader.Set<VersionedGraphNode>()
            .Where(x => x.Id == outerId || x.Id == rolledBackId || x.Id == laterId)
            .AsNoTracking()
            .ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(2, persisted);
        Assert.IsTrue(persisted.Any(x => x.Id == outerId));
        Assert.IsTrue(persisted.Any(x => x.Id == laterId));
        Assert.IsFalse(persisted.Any(x => x.Id == rolledBackId));
    }

    [TestMethod]
    public async Task Savepoint_RollbackToNamedPoint_PreservesLaterWrites()
    {
        var context = CreateVersionedContext();
        var beforeId = Guid.NewGuid();
        var undoneId = Guid.NewGuid();
        var afterId = Guid.NewGuid();

        await using (var transaction = await context.BeginTransactionAsync(TestContext.CancellationToken))
        {
            context.Add(new VersionedGraphNode { Id = beforeId, Name = "before" });
            await context.SaveChangesAsync(TestContext.CancellationToken);

            var savepoint = await transaction.CreateSavepointAsync(cancellationToken: TestContext.CancellationToken);
            context.Add(new VersionedGraphNode { Id = undoneId, Name = "undone" });
            await context.SaveChangesAsync(TestContext.CancellationToken);
            await transaction.RollbackToSavepointAsync(savepoint, TestContext.CancellationToken);

            context.Add(new VersionedGraphNode { Id = afterId, Name = "after" });
            await context.SaveChangesAsync(TestContext.CancellationToken);
            await transaction.CommitAsync(TestContext.CancellationToken);
        }

        var persisted = await CreateVersionedContext().Set<VersionedGraphNode>()
            .Where(x => x.Id == beforeId || x.Id == undoneId || x.Id == afterId)
            .AsNoTracking()
            .ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(2, persisted);
        Assert.IsTrue(persisted.Any(x => x.Id == beforeId));
        Assert.IsTrue(persisted.Any(x => x.Id == afterId));
        Assert.IsFalse(persisted.Any(x => x.Id == undoneId));
    }

    [TestMethod]
    public async Task SaveChanges_StaleVersionedUpdate_ThrowsConcurrencyConflict()
    {
        var id = await SeedVersionedNodeAsync("initial");
        var firstContext = CreateVersionedContext();
        var secondContext = CreateVersionedContext();
        var first = await firstContext.Set<VersionedGraphNode>().Where(x => x.Id == id).AsTracking().SingleAsync(TestContext.CancellationToken);
        var stale = await secondContext.Set<VersionedGraphNode>().Where(x => x.Id == id).AsTracking().SingleAsync(TestContext.CancellationToken);

        Assert.AreEqual(1, first.Version);
        Assert.AreEqual(1, stale.Version);

        first.Name = "winner";
        firstContext.Update(first);
        await firstContext.SaveChangesAsync(TestContext.CancellationToken);

        stale.Name = "stale";
        secondContext.Update(stale);
        await Assert.ThrowsExactlyAsync<GraphConcurrencyException>(() => secondContext.SaveChangesAsync(TestContext.CancellationToken));
        Assert.AreEqual(1, stale.Version);

        var persisted = await CreateVersionedContext().Set<VersionedGraphNode>()
            .Where(x => x.Id == id).AsNoTracking().SingleAsync(TestContext.CancellationToken);
        Assert.AreEqual("winner", persisted.Name);
        Assert.AreEqual(2, persisted.Version);
    }

    [TestMethod]
    public async Task SaveChanges_StaleVersionedDelete_ThrowsConcurrencyConflict()
    {
        var id = await SeedVersionedNodeAsync("initial");
        var firstContext = CreateVersionedContext();
        var secondContext = CreateVersionedContext();
        var winner = await firstContext.Set<VersionedGraphNode>().Where(x => x.Id == id).AsTracking().SingleAsync(TestContext.CancellationToken);
        var stale = await secondContext.Set<VersionedGraphNode>().Where(x => x.Id == id).AsTracking().SingleAsync(TestContext.CancellationToken);

        winner.Name = "updated";
        firstContext.Update(winner);
        await firstContext.SaveChangesAsync(TestContext.CancellationToken);

        secondContext.Remove(stale);
        await Assert.ThrowsExactlyAsync<GraphConcurrencyException>(() => secondContext.SaveChangesAsync(TestContext.CancellationToken));

        var persisted = await CreateVersionedContext().Set<VersionedGraphNode>()
            .Where(x => x.Id == id).AsNoTracking().SingleAsync(TestContext.CancellationToken);
        Assert.AreEqual("updated", persisted.Name);
        Assert.AreEqual(2, persisted.Version);
    }

    [TestMethod]
    public async Task SaveChanges_SequentialVersionedUpdates_IncrementVersion()
    {
        var id = await SeedVersionedNodeAsync("created");
        var context = CreateVersionedContext();
        var node = await context.Set<VersionedGraphNode>().Where(x => x.Id == id).AsTracking().SingleAsync(TestContext.CancellationToken);

        node.Name = "first";
        context.Update(node);
        await context.SaveChangesAsync(TestContext.CancellationToken);
        Assert.AreEqual(2, node.Version);

        node.Name = "second";
        context.Update(node);
        await context.SaveChangesAsync(TestContext.CancellationToken);
        Assert.AreEqual(3, node.Version);

        var persisted = await CreateVersionedContext().Set<VersionedGraphNode>()
            .Where(x => x.Id == id).AsNoTracking().SingleAsync(TestContext.CancellationToken);
        Assert.AreEqual("second", persisted.Name);
        Assert.AreEqual(3, persisted.Version);
    }

    private static VersionedGraphContext CreateVersionedContext()
    {
        var context = new VersionedGraphContext();
        context.UseConnectionFactory(new SqlConnectionFactory(new GormSqlServerOptions { ConnectionString = _databaseConnectionString }));
        return context;
    }

    private async Task<Guid> SeedVersionedNodeAsync(string name)
    {
        var context = CreateVersionedContext();
        var id = Guid.NewGuid();
        context.Add(new VersionedGraphNode { Id = id, Name = name });
        await context.SaveChangesAsync(TestContext.CancellationToken);
        return id;
    }
}

internal sealed class VersionedGraphContext : GraphContext
{
    protected override void OnModelCreating(GraphModelBuilder modelBuilder)
    {
        modelBuilder.Node<VersionedGraphNode>(node =>
        {
            node.ToTable("VersionedGraphNodes");
            node.HasKey(x => x.Id);
            node.Property(x => x.Name).HasMaxLength(200).IsRequired();
            node.Property(x => x.Version).IsRequired();
        });
    }
}

internal sealed class VersionedGraphNode : Node, IHasConcurrencyToken
{
    public string Name { get; set; } = string.Empty;

    public int Version { get; set; }
}
