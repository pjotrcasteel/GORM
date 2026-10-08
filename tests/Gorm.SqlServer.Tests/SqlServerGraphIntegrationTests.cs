using Gorm.Application.Context;
using Gorm.Application.Diagnostics;
using Gorm.Application.Execution;
using Gorm.Application.History.Storage;
using Gorm.Application.Querying;
using Gorm.Demo.Domain.Edges;
using Gorm.Demo.Domain.Nodes;

namespace Gorm.SqlServer.Tests;

public sealed partial class SqlServerGraphIntegrationTests
{
    [TestMethod]
    public async Task ValidateSchema_AfterSqlScript_MatchesGraphModel()
    {
        var report = await CreateContext().ValidateSchemaAsync(TestContext.CancellationToken);
        Assert.IsTrue(report.IsValid, report.ToString());
    }

    [TestMethod]
    public async Task SaveChanges_InsertedNode_IsLoadedByFreshContext()
    {
        var id = Guid.NewGuid();
        var context = CreateContext();
        context.Add(new CharacteristicSpecificationNode { Id = id, Name = "Persisted", Payload = "{}" });
        await context.SaveChangesAsync(TestContext.CancellationToken);

        var read = await CreateContext().CharacteristicSpecifications
            .Where(x => x.Id == id)
            .AsNoTracking()
            .SingleAsync(TestContext.CancellationToken);

        Assert.AreEqual(id, read.Id);
        Assert.AreEqual("Persisted", read.Name);
    }

    [TestMethod]
    public async Task Connect_AfterSave_TracesOutgoingAndIncomingEdges()
    {
        var first = new CharacteristicSpecificationNode { Id = Guid.NewGuid(), Name = "Source", Payload = "{}" };
        var second = new CharacteristicSpecificationNode { Id = Guid.NewGuid(), Name = "Target", Payload = "{}" };
        var context = CreateContext();

        context.AddRange(first, second);
        await context.SaveChangesAsync(TestContext.CancellationToken);
        context.Connect<CharacteristicSpecificationMapEdge, CharacteristicSpecificationNode, CharacteristicSpecificationNode>(
            first, second, edge => { edge.Id = Guid.NewGuid(); edge.Payload = "{}"; });
        await context.SaveChangesAsync(TestContext.CancellationToken);

        var fresh = CreateContext();
        var outgoing = await fresh.CharacteristicSpecifications.Where(x => x.Id == first.Id)
            .Outgoing<CharacteristicSpecificationMapEdge, CharacteristicSpecificationNode>()
            .ToListAsync(TestContext.CancellationToken);
        var incoming = await fresh.CharacteristicSpecifications.Where(x => x.Id == second.Id)
            .Incoming<CharacteristicSpecificationMapEdge, CharacteristicSpecificationNode>()
            .ToListAsync(TestContext.CancellationToken);

        Assert.IsTrue(outgoing.Any(x => x.Id == second.Id), "SQL Server Graph outgoing traversal failed.");
        Assert.IsTrue(incoming.Any(x => x.Id == first.Id), "SQL Server Graph incoming traversal failed.");
    }

    [TestMethod]
    public async Task BeginTransaction_AfterRollback_NodeIsNotPersisted()
    {
        var id = Guid.NewGuid();
        var context = CreateContext();

        await using (var transaction = await context.BeginTransactionAsync(TestContext.CancellationToken))
        {
            context.Add(new CharacteristicSpecificationNode { Id = id, Name = "Rolled back", Payload = "{}" });
            await context.SaveChangesAsync(TestContext.CancellationToken);
            await transaction.RollbackAsync(TestContext.CancellationToken);
        }

        var exists = await CreateContext().CharacteristicSpecifications
            .Where(x => x.Id == id)
            .AnyAsync(TestContext.CancellationToken);
        Assert.IsFalse(exists, "Rolled-back data is visible from a new database connection.");
    }

    [TestMethod]
    public async Task UseSqlServerHistory_AfterInsert_RecordsNodeHistory()
    {
        var id = Guid.NewGuid();
        var context = CreateContext();
        context.UseSqlServerHistory(context.ConnectionFactory!);
        context.Add(new CharacteristicSpecificationNode { Id = id, Name = "Historical", Payload = "{}" });
        await context.SaveChangesAsync(TestContext.CancellationToken);

        var reader = new SqlServerGraphHistoryReader(context.ConnectionFactory!);
        var history = await reader.ReadNodeHistoryAsync<CharacteristicSpecificationNode>(TestContext.CancellationToken);
        Assert.IsTrue(history.Any(x => x.EntityId == id), "The insert did not produce SQL Server history.");
    }
}
