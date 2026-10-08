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
}
