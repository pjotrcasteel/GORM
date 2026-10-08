using Gorm.Application.History;
using Gorm.Application.History.Storage;
using Gorm.Demo.Domain.Edges;
using Gorm.Demo.Domain.Nodes;
using Microsoft.Data.SqlClient;

namespace Gorm.SqlServer.Tests;

public sealed partial class SqlServerGraphIntegrationTests
{
    [TestMethod]
    public async Task SqlHistory_DeleteNodeWithConnectedEdge_RemovesLiveEdgeAndRecordsTerminalHistory()
    {
        var clock = new ControlledHistoryTimeProvider(new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.Zero));
        var context = CreateHistoryContext(clock);
        var (source, target) = await CreateHistoryNodesAsync(context);

        clock.Advance(TimeSpan.FromMinutes(1));
        var edge = ConnectHistoricalEdge(context, source, target);
        await context.SaveChangesAsync(TestContext.CancellationToken);
        var beforeDelete = clock.GetUtcNow().UtcDateTime;
        Assert.HasCount(1, HistoricalTargets(context, source.Id, beforeDelete));

        clock.Advance(TimeSpan.FromMinutes(1));
        context.Remove(source);
        await context.SaveChangesAsync(TestContext.CancellationToken);
        var afterDelete = clock.GetUtcNow().UtcDateTime;

        await using var connection = new SqlConnection(_databaseConnectionString);
        await connection.OpenAsync(TestContext.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT_BIG(*) FROM [dbo].[CharacteristicSpecificationMapsIntoCharacteristicSpecifications] WHERE [Id] = @Id";
        command.Parameters.AddWithValue("@Id", edge.Id);
        Assert.AreEqual(0L, (long)(await command.ExecuteScalarAsync(TestContext.CancellationToken))!);

        var reader = new SqlServerGraphHistoryReader(context.ConnectionFactory!);
        var edgeHistory = (await reader.ReadEdgeHistoryAsync<CharacteristicSpecificationMapEdge>(TestContext.CancellationToken))
            .Where(x => x.EntityId == edge.Id).ToList();
        Assert.IsTrue(edgeHistory.Any(x => x.OperationKind == GraphHistoryOperationKind.Connected));
        var terminal = edgeHistory.Where(x => x.OperationKind is GraphHistoryOperationKind.Disconnected or GraphHistoryOperationKind.Deleted).ToList();
        Assert.HasCount(1, terminal, "Implicit edge cleanup must be reflected in persisted history.");
        Assert.AreEqual(source.Id, terminal[0].FromId);
        Assert.AreEqual(target.Id, terminal[0].ToId);
        Assert.IsEmpty(HistoricalTargets(context, source.Id, afterDelete));
    }
    [TestMethod]
    public async Task SqlHistory_DeleteNodeWithIncomingOutgoingParallelAndSelfEdges_LeavesOnlyUnrelatedEdges()
    {
        var clock = new ControlledHistoryTimeProvider(new DateTimeOffset(2026, 10, 2, 10, 0, 0, TimeSpan.Zero));
        var context = CreateHistoryContext(clock);
        var (source, target) = await CreateHistoryNodesAsync(context);
        var other = new CharacteristicSpecificationNode { Id = Guid.NewGuid(), Name = "Other", Payload = "{}" };
        context.Add(other);
        await context.SaveChangesAsync(TestContext.CancellationToken);

        clock.Advance(TimeSpan.FromMinutes(1));
        var outgoing1 = ConnectHistoricalEdge(context, source, target);
        var outgoing2 = ConnectHistoricalEdge(context, source, target);
        var incoming = ConnectHistoricalEdge(context, target, source);
        var self = ConnectHistoricalEdge(context, source, source);
        var unrelated = ConnectHistoricalEdge(context, target, other);
        await context.SaveChangesAsync(TestContext.CancellationToken);

        clock.Advance(TimeSpan.FromMinutes(1));
        context.Remove(source);
        await context.SaveChangesAsync(TestContext.CancellationToken);

        var deleted = new[] { outgoing1.Id, outgoing2.Id, incoming.Id, self.Id };
        var remainingIds = await ReadStoredEdgeIdsAsync([.. deleted, unrelated.Id]);
        Assert.HasCount(1, remainingIds);
        Assert.AreEqual(unrelated.Id, remainingIds.Single(), "Unrelated edges must remain live.");

        var history = await new SqlServerGraphHistoryReader(context.ConnectionFactory!)
            .ReadEdgeHistoryAsync<CharacteristicSpecificationMapEdge>(TestContext.CancellationToken);
        foreach (var edgeId in deleted)
        {
            var records = history.Where(x => x.EntityId == edgeId).ToArray();
            Assert.AreEqual(1, records.Count(x => x.OperationKind == GraphHistoryOperationKind.Connected));
            Assert.AreEqual(1, records.Count(x => x.OperationKind == GraphHistoryOperationKind.Deleted));
        }

        Assert.IsFalse(history.Any(x => x.EntityId == unrelated.Id && x.OperationKind == GraphHistoryOperationKind.Deleted));
    }

    [TestMethod]
    public async Task SqlHistory_DeleteNodeWithLegacyUnrecordedEdge_CleansLiveGraphWithoutInventingPastHistory()
    {
        var original = CreateContext();
        var source = new CharacteristicSpecificationNode { Id = Guid.NewGuid(), Name = "Old source", Payload = "{}" };
        var target = new CharacteristicSpecificationNode { Id = Guid.NewGuid(), Name = "Old target", Payload = "{}" };
        original.AddRange(source, target);
        await original.SaveChangesAsync(TestContext.CancellationToken);
        var edge = ConnectHistoricalEdge(original, source, target);
        await original.SaveChangesAsync(TestContext.CancellationToken);

        var historyContext = CreateHistoryContext(new ControlledHistoryTimeProvider(
            new DateTimeOffset(2026, 10, 4, 10, 0, 0, TimeSpan.Zero)));
        historyContext.Remove(source);
        await historyContext.SaveChangesAsync(TestContext.CancellationToken);

        Assert.IsEmpty(await ReadStoredEdgeIdsAsync([edge.Id]));
        var history = await new SqlServerGraphHistoryReader(historyContext.ConnectionFactory!)
            .ReadEdgeHistoryAsync<CharacteristicSpecificationMapEdge>(TestContext.CancellationToken);
        Assert.IsFalse(history.Any(x => x.EntityId == edge.Id),
            "Enabling history later must not fabricate missing earlier connection evidence.");
    }

    [TestMethod]
    public async Task SqlHistory_DeleteNodeWithEdge_TransactionRollbackRestoresNodeEdgeAndOriginalHistory()
    {
        var clock = new ControlledHistoryTimeProvider(new DateTimeOffset(2026, 10, 3, 10, 0, 0, TimeSpan.Zero));
        var context = CreateHistoryContext(clock);
        var (source, target) = await CreateHistoryNodesAsync(context);
        var edge = ConnectHistoricalEdge(context, source, target);
        await context.SaveChangesAsync(TestContext.CancellationToken);

        clock.Advance(TimeSpan.FromMinutes(1));
        await using (var transaction = await context.BeginTransactionAsync(TestContext.CancellationToken))
        {
            context.Remove(source);
            await context.SaveChangesAsync(TestContext.CancellationToken);
            await transaction.RollbackAsync(TestContext.CancellationToken);
        }

        var remainingIds = await ReadStoredEdgeIdsAsync([edge.Id]);
        Assert.AreEqual(edge.Id, remainingIds.Single());
        var exists = await Gorm.Application.Execution.GraphQueryableOperatorsExtensions.AnyAsync(
            CreateContext().CharacteristicSpecifications.Where(x => x.Id == source.Id), TestContext.CancellationToken);
        Assert.IsTrue(exists, "Rollback must restore the deleted node.");

        var history = (await new SqlServerGraphHistoryReader(context.ConnectionFactory!)
            .ReadEdgeHistoryAsync<CharacteristicSpecificationMapEdge>(TestContext.CancellationToken))
            .Where(x => x.EntityId == edge.Id).ToArray();
        Assert.HasCount(1, history);
        Assert.AreEqual(GraphHistoryOperationKind.Connected, history[0].OperationKind);
    }

    private async Task<HashSet<Guid>> ReadStoredEdgeIdsAsync(IReadOnlyList<Guid> ids)
    {
        await using var connection = new SqlConnection(_databaseConnectionString);
        await connection.OpenAsync(TestContext.CancellationToken);
        await using var command = connection.CreateCommand();
        var parameterNames = ids.Select((_, index) => $"@id{index}").ToArray();
        command.CommandText = $"SELECT [Id] FROM [dbo].[CharacteristicSpecificationMapsIntoCharacteristicSpecifications] WHERE [Id] IN ({string.Join(", ", parameterNames)})";
        for (var index = 0; index < ids.Count; index++)
        {
            command.Parameters.AddWithValue(parameterNames[index], ids[index]);
        }

        var results = new HashSet<Guid>();
        await using var reader = await command.ExecuteReaderAsync(TestContext.CancellationToken);
        while (await reader.ReadAsync(TestContext.CancellationToken))
        {
            results.Add(reader.GetGuid(0));
        }

        return results;
    }

}
