using Gorm.Application.Execution;
using Gorm.Application.Querying;

namespace Gorm.SqlServer.Tests;

public sealed partial class SqlServerGraphIntegrationTests
{
    [TestMethod]
    public async Task SaveChanges_ExplicitTransactionConcurrencyFailure_DoesNotPersistEarlierInsert()
    {
        var existingId = await SeedVersionedNodeAsync("existing");
        var staleContext = CreateVersionedContext();
        var stale = await staleContext.Set<VersionedGraphNode>().Where(x => x.Id == existingId)
            .AsTracking().SingleAsync(TestContext.CancellationToken);
        var competing = CreateVersionedContext();
        var winner = await competing.Set<VersionedGraphNode>().Where(x => x.Id == existingId)
            .AsTracking().SingleAsync(TestContext.CancellationToken);
        winner.Name = "newer";
        competing.Update(winner);
        await competing.SaveChangesAsync(TestContext.CancellationToken);

        var insertedId = Guid.NewGuid();
        await using (var transaction = await staleContext.BeginTransactionAsync(TestContext.CancellationToken))
        {
            staleContext.Add(new VersionedGraphNode { Id = insertedId, Name = "should rollback with failed save" });
            stale.Name = "old version";
            staleContext.Update(stale);
            await Assert.ThrowsExactlyAsync<GraphConcurrencyException>(
                () => staleContext.SaveChangesAsync(TestContext.CancellationToken));

            // Committing an outer transaction after the failed SaveChanges must not commit a partial insert.
            await transaction.CommitAsync(TestContext.CancellationToken);
        }

        var escaped = await CreateVersionedContext().Set<VersionedGraphNode>()
            .Where(x => x.Id == insertedId).AsNoTracking().AnyAsync(TestContext.CancellationToken);
        Assert.IsFalse(escaped, "A row inserted earlier in the failed SaveChanges must not escape into a later outer commit.");
    }

    [TestMethod]
    public async Task SaveChanges_ExplicitTransactionConcurrencyFailure_RollsBackEarlierInsertButKeepsOuterTransaction()
    {
        var existingId = await SeedVersionedNodeAsync("original");
        var winner = CreateVersionedContext();
        var winnerNode = await winner.Set<VersionedGraphNode>().Where(x => x.Id == existingId)
            .AsTracking().SingleAsync(TestContext.CancellationToken);
        var staleContext = CreateVersionedContext();
        var staleNode = await staleContext.Set<VersionedGraphNode>().Where(x => x.Id == existingId)
            .AsTracking().SingleAsync(TestContext.CancellationToken);

        winnerNode.Name = "winner";
        winner.Update(winnerNode);
        await winner.SaveChangesAsync(TestContext.CancellationToken);

        var insertedId = Guid.NewGuid();
        var laterId = Guid.NewGuid();
        await using (var transaction = await staleContext.BeginTransactionAsync(TestContext.CancellationToken))
        {
            staleContext.Add(new VersionedGraphNode { Id = insertedId, Name = "must not persist" });
            staleNode.Name = "stale update";
            staleContext.Update(staleNode);
            await Assert.ThrowsExactlyAsync<GraphConcurrencyException>(
                () => staleContext.SaveChangesAsync(TestContext.CancellationToken));

            Assert.IsFalse(staleContext.ChangeTracker.HasChanges(),
                "Failed SaveChanges must not leave invalid tracked changes after rolling its own savepoint back.");

            staleContext.Add(new VersionedGraphNode { Id = laterId, Name = "after failed save" });
            await staleContext.SaveChangesAsync(TestContext.CancellationToken);
            await transaction.CommitAsync(TestContext.CancellationToken);
        }

        var rows = await CreateVersionedContext().Set<VersionedGraphNode>()
            .Where(x => x.Id == existingId || x.Id == insertedId || x.Id == laterId)
            .AsNoTracking().ToListAsync(TestContext.CancellationToken);
        Assert.IsFalse(rows.Any(x => x.Id == insertedId), "An inserted row from a failed SaveChanges escaped into the parent transaction.");
        Assert.AreEqual("winner", rows.Single(x => x.Id == existingId).Name);
        Assert.IsTrue(rows.Any(x => x.Id == laterId), "The caller must be able to continue the outer transaction after a failed savepoint.");
    }
}
