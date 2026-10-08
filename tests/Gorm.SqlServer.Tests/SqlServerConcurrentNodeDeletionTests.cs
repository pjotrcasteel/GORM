using Gorm.Application.Execution;
using Gorm.Application.History;
using Gorm.Application.History.Storage;
using Gorm.Application.Querying;
using Gorm.Demo.Domain.Edges;
using Gorm.Demo.Domain.Nodes;
using Microsoft.Data.SqlClient;

namespace Gorm.SqlServer.Tests;

public sealed partial class SqlServerGraphIntegrationTests
{
    [TestMethod]
    public async Task DeleteNode_ConcurrentConnectionAfterEdgeCleanup_NeverCommitsDanglingEdge()
    {
        var seed = CreateContext();
        var source = new CharacteristicSpecificationNode { Id = Guid.NewGuid(), Name = "race source", Payload = "{}" };
        var target = new CharacteristicSpecificationNode { Id = Guid.NewGuid(), Name = "race target", Payload = "{}" };
        seed.AddRange(source, target);
        await seed.SaveChangesAsync(TestContext.CancellationToken);

        var cleanupFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseDeletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var edgeLookupStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var deleting = CreateContext();
        deleting.AfterIncidentEdgeCleanupForTesting = async token =>
        {
            cleanupFinished.TrySetResult();
            await releaseDeletion.Task.WaitAsync(token);
        };

        var deletionTask = DeleteNodeAsync(deleting, source);
        try
        {
            await cleanupFinished.Task.WaitAsync(TimeSpan.FromSeconds(20), TestContext.CancellationToken);

            var writer = CreateContext();
            writer.BeforeEdgeEndpointLookupForTesting = _ =>
            {
                edgeLookupStarted.TrySetResult();
                return Task.CompletedTask;
            };

            var edgeId = Guid.NewGuid();
            writer.Connect<CharacteristicSpecificationMapEdge, CharacteristicSpecificationNode, CharacteristicSpecificationNode>(
                source, target, edge => { edge.Id = edgeId; edge.Payload = "{}"; });
            var connectionTask = writer.SaveChangesAsync(TestContext.CancellationToken);
            await edgeLookupStarted.Task.WaitAsync(TimeSpan.FromSeconds(20), TestContext.CancellationToken);

            await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.CancellationToken);
            Assert.IsFalse(connectionTask.IsCompleted,
                "The edge writer must wait for the deleting node's transaction; completing before deletion permits a dangling graph edge.");

            releaseDeletion.TrySetResult();
            await deletionTask.WaitAsync(TimeSpan.FromSeconds(20), TestContext.CancellationToken);
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                async () => await connectionTask.WaitAsync(TimeSpan.FromSeconds(20), TestContext.CancellationToken));

            await using var sql = new SqlConnection(_databaseConnectionString);
            await sql.OpenAsync(TestContext.CancellationToken);
            await using var cmd = sql.CreateCommand();
            cmd.CommandText = "SELECT COUNT_BIG(*) FROM [dbo].[CharacteristicSpecificationMapsIntoCharacteristicSpecifications] WHERE [Id] = @Id";
            cmd.Parameters.AddWithValue("@Id", edgeId);
            Assert.AreEqual(0L, (long)(await cmd.ExecuteScalarAsync(TestContext.CancellationToken))!);
            var nodePresent = await CreateContext().CharacteristicSpecifications.Where(x => x.Id == source.Id)
                .AsNoTracking().AnyAsync(TestContext.CancellationToken);
            Assert.IsFalse(nodePresent);
        }
        finally
        {
            releaseDeletion.TrySetResult();
            try { await deletionTask.WaitAsync(TimeSpan.FromSeconds(20), TestContext.CancellationToken); }
            catch { /* Keep original assertion failure as primary evidence. */ }
        }
    }

    [TestMethod]
    public async Task DeleteNode_EdgeCommittedAfterHistoryPreparation_RecordsOneTerminalEdgeEvent()
    {
        var clock = new ControlledHistoryTimeProvider(new DateTimeOffset(2026, 10, 8, 10, 0, 0, TimeSpan.Zero));
        var seed = CreateHistoryContext(clock);
        var (source, target) = await CreateHistoryNodesAsync(seed);

        var prepared = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var deleting = CreateHistoryContext(clock);
        deleting.AfterHistoryPreparationForTesting = async token =>
        {
            prepared.TrySetResult();
            await release.Task.WaitAsync(token);
        };

        var deleteTask = DeleteNodeAsync(deleting, source);
        try
        {
            await prepared.Task.WaitAsync(TimeSpan.FromSeconds(20), TestContext.CancellationToken);

            clock.Advance(TimeSpan.FromMinutes(1));
            var writer = CreateHistoryContext(clock);
            var edge = ConnectHistoricalEdge(writer, source, target);
            await writer.SaveChangesAsync(TestContext.CancellationToken);
            clock.Advance(TimeSpan.FromMinutes(1));

            release.TrySetResult();
            await deleteTask.WaitAsync(TimeSpan.FromSeconds(20), TestContext.CancellationToken);

            Assert.IsEmpty(await ReadStoredEdgeIdsAsync([edge.Id]));
            var history = await new SqlServerGraphHistoryReader(deleting.ConnectionFactory!)
                .ReadEdgeHistoryAsync<CharacteristicSpecificationMapEdge>(TestContext.CancellationToken);
            var recorded = history.Where(x => x.EntityId == edge.Id).ToArray();
            Assert.AreEqual(1, recorded.Count(x => x.OperationKind == GraphHistoryOperationKind.Connected));
            Assert.AreEqual(1, recorded.Count(x => x.OperationKind == GraphHistoryOperationKind.Deleted),
                "A committed edge preceding the node lock must produce a terminal history event when its node is deleted.");
        }
        finally
        {
            release.TrySetResult();
            try { await deleteTask.WaitAsync(TimeSpan.FromSeconds(20), TestContext.CancellationToken); }
            catch { /* Keep original assertion failure as primary evidence. */ }
        }
    }

    private async Task DeleteNodeAsync(Gorm.Application.Context.GraphContext context, CharacteristicSpecificationNode node)
    {
        await using var transaction = await context.BeginTransactionAsync(TestContext.CancellationToken);
        context.Remove(node);
        await context.SaveChangesAsync(TestContext.CancellationToken);
        await transaction.CommitAsync(TestContext.CancellationToken);
    }
}
