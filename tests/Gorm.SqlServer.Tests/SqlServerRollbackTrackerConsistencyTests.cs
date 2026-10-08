using Gorm.Application.Execution;
using Gorm.Application.Querying;
using Gorm.Application.Tracking;

namespace Gorm.SqlServer.Tests;

public sealed partial class SqlServerGraphIntegrationTests
{
    [TestMethod]
    public async Task TransactionRollback_AfterSuccessfulSave_DetachesStaleTrackedEntities()
    {
        var context = CreateVersionedContext();
        var node = new VersionedGraphNode { Id = Guid.NewGuid(), Name = "will rollback" };

        await using (var transaction = await context.BeginTransactionAsync(TestContext.CancellationToken))
        {
            context.Add(node);
            await context.SaveChangesAsync(TestContext.CancellationToken);
            Assert.AreEqual(EntityState.Unchanged, context.Entry(node).State);
            await transaction.RollbackAsync(TestContext.CancellationToken);
        }

        Assert.AreEqual(EntityState.Detached, context.Entry(node).State,
            "The context must not report rolled-back data as successfully persisted.");
        Assert.IsFalse(context.ChangeTracker.HasChanges());

        context.Add(node);
        await context.SaveChangesAsync(TestContext.CancellationToken);
        var persisted = await CreateVersionedContext().Set<VersionedGraphNode>().Where(x => x.Id == node.Id)
            .AsNoTracking().SingleAsync(TestContext.CancellationToken);
        Assert.AreEqual(node.Name, persisted.Name);
    }

    [TestMethod]
    public async Task SavepointRollback_AfterSuccessfulSave_DetachesStaleTrackedEntities()
    {
        var context = CreateVersionedContext();
        var before = new VersionedGraphNode { Id = Guid.NewGuid(), Name = "before" };
        var rolledBack = new VersionedGraphNode { Id = Guid.NewGuid(), Name = "undo" };

        await using (var transaction = await context.BeginTransactionAsync(TestContext.CancellationToken))
        {
            context.Add(before);
            await context.SaveChangesAsync(TestContext.CancellationToken);

            var savepoint = await transaction.CreateSavepointAsync(cancellationToken: TestContext.CancellationToken);
            context.Add(rolledBack);
            await context.SaveChangesAsync(TestContext.CancellationToken);
            await transaction.RollbackToSavepointAsync(savepoint, TestContext.CancellationToken);

            Assert.AreEqual(EntityState.Detached, context.Entry(before).State,
                "Partial rollback conservatively invalidates even earlier tracked snapshots.");
            Assert.AreEqual(EntityState.Detached, context.Entry(rolledBack).State);
            Assert.IsFalse(context.ChangeTracker.HasChanges());

            await transaction.CommitAsync(TestContext.CancellationToken);
        }

        var persisted = await CreateVersionedContext().Set<VersionedGraphNode>()
            .Where(x => x.Id == before.Id || x.Id == rolledBack.Id).AsNoTracking().ToListAsync(TestContext.CancellationToken);
        Assert.HasCount(1, persisted);
        Assert.AreEqual(before.Id, persisted[0].Id);
    }

    [TestMethod]
    public async Task NestedRollback_AfterSuccessfulSave_DetachesStaleTrackedEntities()
    {
        var context = CreateVersionedContext();
        var node = new VersionedGraphNode { Id = Guid.NewGuid(), Name = "nested" };

        await using (var outer = await context.BeginTransactionAsync(TestContext.CancellationToken))
        {
            await using (var inner = await context.BeginTransactionAsync(TestContext.CancellationToken))
            {
                context.Add(node);
                await context.SaveChangesAsync(TestContext.CancellationToken);
                await inner.RollbackAsync(TestContext.CancellationToken);
            }

            Assert.AreEqual(EntityState.Detached, context.Entry(node).State);
            await outer.CommitAsync(TestContext.CancellationToken);
        }

        var exists = await CreateVersionedContext().Set<VersionedGraphNode>()
            .Where(x => x.Id == node.Id).AsNoTracking().AnyAsync(TestContext.CancellationToken);
        Assert.IsFalse(exists);
    }

    [TestMethod]
    public async Task ImplicitRollback_DisposingUncommittedTransaction_DetachesTrackedEntities()
    {
        var context = CreateVersionedContext();
        var node = new VersionedGraphNode { Id = Guid.NewGuid(), Name = "uncommitted" };

        await using (var transaction = await context.BeginTransactionAsync(TestContext.CancellationToken))
        {
            context.Add(node);
            await context.SaveChangesAsync(TestContext.CancellationToken);
        }

        Assert.AreEqual(EntityState.Detached, context.Entry(node).State);
        Assert.IsFalse(context.ChangeTracker.HasChanges());
        var exists = await CreateVersionedContext().Set<VersionedGraphNode>()
            .Where(x => x.Id == node.Id).AsNoTracking().AnyAsync(TestContext.CancellationToken);
        Assert.IsFalse(exists);
    }
}
