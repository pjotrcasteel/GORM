using System.Data.Common;
using System.Globalization;
using System.Runtime.CompilerServices;
using Gorm.Application.Context;
using Gorm.Application.Tracking;
using Gorm.Core.Primitives;
using Gorm.Infrastructure.Persistence.Connections;
using Gorm.Infrastructure.Providers.SqlServer.Helpers;
using Gorm.Infrastructure.Sql;

namespace Gorm.Application.Execution;

/// <summary>
/// Represents graph save changes executor.
/// </summary>
public sealed class GraphSaveChangesExecutor
{
    private readonly IGormDbConnectionFactory _connectionFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphSaveChangesExecutor"/> class.
    /// </summary>
    /// <param name="connectionFactory">The connection factory.</param>
    public GraphSaveChangesExecutor(IGormDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <summary>
    /// Saves pending changes.
    /// </summary>
    /// <param name="context">The graph context.</param>
    /// <param name="changeTracker">The change tracker.</param>
    /// <param name="acceptAllChangesOnSuccess">The accept all changes on success.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public Task<int> SaveChangesAsync(GraphContext context, GraphChangeTracker changeTracker, bool acceptAllChangesOnSuccess = true, CancellationToken cancellationToken = default)
        => SaveChangesAsync(context, changeTracker, acceptAllChangesOnSuccess, captureHistoryAsync: null, cancellationToken);

    internal Task<int> SaveChangesAsync(
        GraphContext context,
        GraphChangeTracker changeTracker,
        bool acceptAllChangesOnSuccess,
        Func<DbConnection, DbTransaction, CancellationToken, Task>? captureHistoryAsync,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(changeTracker);
        return SaveChangesCoreAsync(context, changeTracker, acceptAllChangesOnSuccess, captureHistoryAsync, cancellationToken);
    }

    private async Task<int> SaveChangesCoreAsync(
        GraphContext context,
        GraphChangeTracker changeTracker,
        bool acceptAllChangesOnSuccess,
        Func<DbConnection, DbTransaction, CancellationToken, Task>? captureHistoryAsync,
        CancellationToken cancellationToken)
    {
        if (!changeTracker.HasChanges())
        {
            return 0;
        }

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        return await SaveChangesAsync(
            new SaveChangesAsyncParameters
            {
                Context = context,
                ChangeTracker = changeTracker,
                Connection = connection,
                Transaction = transaction,
                OwnsTransaction = true,
                AcceptAllChangesOnSuccess = acceptAllChangesOnSuccess,
                CaptureHistoryAsync = captureHistoryAsync,
                CancellationToken = cancellationToken
            });
    }

    /// <summary>
    /// Saves pending changes.
    /// </summary>
    /// <param name="context">The graph context.</param>
    /// <param name="changeTracker">The change tracker.</param>
    /// <param name="connection">The connection.</param>
    /// <param name="transaction">The transaction.</param>
    /// <param name="ownsTransaction">The owns transaction.</param>
    /// <param name="acceptAllChangesOnSuccess">The accept all changes on success.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public static Task<int> SaveChangesAsync(SaveChangesAsyncParameters inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var context = inputs.Context;
        var changeTracker = inputs.ChangeTracker;
        var connection = inputs.Connection;
        var transaction = inputs.Transaction;

        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(changeTracker);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        return SaveChangesCoreAsync(inputs);
    }

    private static async Task<int> SaveChangesCoreAsync(SaveChangesAsyncParameters inputs)
    {
        var context = inputs.Context;
        var changeTracker = inputs.ChangeTracker;
        var connection = inputs.Connection;
        var transaction = inputs.Transaction;
        var ownsTransaction = inputs.OwnsTransaction;
        var acceptAllChangesOnSuccess = inputs.AcceptAllChangesOnSuccess;
        var captureHistoryAsync = inputs.CaptureHistoryAsync;
        var cancellationToken = inputs.CancellationToken;

        if (!changeTracker.HasChanges())
        {
            return 0;
        }

        try
        {
            var saveSet = GraphSaveChangesSet.Create(changeTracker);
            var persistedNodeIds = new Dictionary<object, object?>(ReferenceEqualityComparer.Instance);
            var affectedRows = 0;

            affectedRows += await InsertAddedEntitiesAsync(context, saveSet, connection, transaction, persistedNodeIds, cancellationToken);

            affectedRows += await ProcessPendingEdgeConnectionsAsync(context, saveSet, connection, transaction, persistedNodeIds, cancellationToken);

            affectedRows += await ProcessPendingEdgeDisconnectionsAsync(context, saveSet, connection, transaction, persistedNodeIds, cancellationToken);

            affectedRows += await ProcessModifiedEntitiesAsync(context, saveSet, connection, transaction, cancellationToken);

            affectedRows += await DeleteIncidentEdgesForDeletedNodesAsync(context, saveSet, connection, transaction, cancellationToken);

            if (saveSet.DeletedEntries.Any(x => x.Entity is Node) && context.AfterIncidentEdgeCleanupForTesting is { } afterCleanup)
            {
                await afterCleanup(cancellationToken);
            }

            affectedRows += await ProcessDeletedEntitiesAsync(context, saveSet, connection, transaction, cancellationToken);

            if (captureHistoryAsync is not null)
            {
                await captureHistoryAsync(connection, transaction, cancellationToken);
            }

            if (ownsTransaction)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            if (acceptAllChangesOnSuccess)
            {
                changeTracker.AcceptAllChanges(context.Model);
            }

            return affectedRows;
        }
        catch
        {
            await RollbackIfNeededAsync(transaction, ownsTransaction, cancellationToken);
            throw;
        }
    }

    private static async Task<int> InsertAddedEntitiesAsync(
        GraphContext context,
        GraphSaveChangesSet saveSet,
        DbConnection connection,
        DbTransaction transaction,
        IDictionary<object, object?> persistedNodeIds,
        CancellationToken cancellationToken)
    {
        var affectedRows = 0;

        for (var i = 0; i < saveSet.AddedEntries.Count; i++)
        {
            var entry = saveSet.AddedEntries[i];

            switch (entry.Entity)
            {
                case Node node:
                    affectedRows += await InsertAddedNodeAsync(
                        new InsertAddedNodeAsyncParameters
                        {
                            Context = context,
                            Connection = connection,
                            Transaction = transaction,
                            PersistedNodeIds = persistedNodeIds,
                            Entry = entry,
                            Node = node,
                            CancellationToken = cancellationToken
                        });
                    break;

                case Edge edge:
                    PrepareAddedEdge(edge, saveSet.AddedEdgesWithConnection);
                    break;
            }
        }

        return affectedRows;
    }

    private static async Task<int> InsertAddedNodeAsync(InsertAddedNodeAsyncParameters inputs)
    {
        var context = inputs.Context;
        var connection = inputs.Connection;
        var transaction = inputs.Transaction;
        var persistedNodeIds = inputs.PersistedNodeIds;
        var entry = inputs.Entry;
        var node = inputs.Node;
        var cancellationToken = inputs.CancellationToken;

        if (node.Id == Guid.Empty)
        {
            node.Id = Guid.NewGuid();
        }

        SetInitialConcurrencyTokenIfNeeded(node);

        var mapping = context.Model.GetNode(entry.ClrType);
        var plan = GraphSaveCommandPlanCache.GetNodePlan(mapping);
        var affectedRows = await InsertNodeAsync(connection, transaction, plan, node, cancellationToken);
        var nodeId = await LoadNodeIdAsync(connection, transaction, plan, node, cancellationToken);

        persistedNodeIds[node] = nodeId;

        return affectedRows;
    }

    private static void PrepareAddedEdge(Edge edge, HashSet<object> addedEdgesWithConnection)
    {
        if (edge.Id == Guid.Empty)
        {
            edge.Id = Guid.NewGuid();
        }

        if (!addedEdgesWithConnection.Contains(edge))
        {
            throw new NotSupportedException("Adding standalone edges without endpoints is not supported; use AddEdge(...) or Connect(...).");
        }

        SetInitialConcurrencyTokenIfNeeded(edge);
    }

    private static async Task<int> ProcessPendingEdgeConnectionsAsync(
        GraphContext context,
        GraphSaveChangesSet saveSet,
        DbConnection connection,
        DbTransaction transaction,
        Dictionary<object, object?> persistedNodeIds,
        CancellationToken cancellationToken)
    {
        var affectedRows = 0;

        for (var i = 0; i < saveSet.PendingEdgeConnections.Count; i++)
        {
            var pendingEdge = saveSet.PendingEdgeConnections[i];

            if (pendingEdge.Edge.Id == Guid.Empty)
            {
                pendingEdge.Edge.Id = Guid.NewGuid();
            }

            SetInitialConcurrencyTokenIfNeeded(pendingEdge.Edge);

            if (context.BeforeEdgeEndpointLookupForTesting is { } beforeLookup)
            {
                await beforeLookup(cancellationToken);
            }

            var edgeMapping = context.Model.GetEdge(pendingEdge.Edge.GetType());
            var edgePlan = GraphSaveCommandPlanCache.GetEdgePlan(edgeMapping);

            var fromNodeId = await GetOrLoadNodeIdAsync(connection, transaction, context, persistedNodeIds, pendingEdge.FromNode, cancellationToken);

            var toNodeId = await GetOrLoadNodeIdAsync(connection, transaction, context, persistedNodeIds, pendingEdge.ToNode, cancellationToken);

            affectedRows += await InsertEdgeAsync(
                new InsertEdgeAsyncParameters
                {
                    Connection = connection,
                    Transaction = transaction,
                    Plan = edgePlan,
                    Edge = pendingEdge.Edge,
                    FromNodeId = fromNodeId,
                    ToNodeId = toNodeId,
                    CancellationToken = cancellationToken
                });
        }

        return affectedRows;
    }

    private static async Task<int> ProcessPendingEdgeDisconnectionsAsync(
        GraphContext context,
        GraphSaveChangesSet saveSet,
        DbConnection connection,
        DbTransaction transaction,
        Dictionary<object, object?> persistedNodeIds,
        CancellationToken cancellationToken)
    {
        var affectedRows = 0;

        for (var i = 0; i < saveSet.PendingEdgeDisconnections.Count; i++)
        {
            var pendingDisconnection = saveSet.PendingEdgeDisconnections[i];
            var edgeMapping = context.Model.GetEdge(pendingDisconnection.EdgeType);
            var edgePlan = GraphSaveCommandPlanCache.GetEdgePlan(edgeMapping);

            var fromNodeId = await GetOrLoadNodeIdAsync(connection, transaction, context, persistedNodeIds, pendingDisconnection.FromNode, cancellationToken);

            var toNodeId = await GetOrLoadNodeIdAsync(connection, transaction, context, persistedNodeIds, pendingDisconnection.ToNode, cancellationToken);

            affectedRows += await DeleteEdgeByNodeIdsAsync(connection, transaction, edgePlan, fromNodeId, toNodeId, cancellationToken);
        }

        return affectedRows;
    }

    private static async Task<int> ProcessModifiedEntitiesAsync(
        GraphContext context,
        GraphSaveChangesSet saveSet,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        var affectedRows = 0;

        for (var i = 0; i < saveSet.ModifiedEntries.Count; i++)
        {
            var entry = saveSet.ModifiedEntries[i];

            switch (entry.Entity)
            {
                case Node node:
                    affectedRows += await UpdateModifiedNodeAsync(context, connection, transaction, entry, node, cancellationToken);
                    break;

                case Edge edge:
                    affectedRows += await UpdateModifiedEdgeAsync(context, connection, transaction, entry, edge, cancellationToken);
                    break;
            }
        }

        return affectedRows;
    }

    private static Task<int> UpdateModifiedNodeAsync(
        GraphContext context,
        DbConnection connection,
        DbTransaction transaction,
        GraphEntityEntry entry,
        Node node,
        CancellationToken cancellationToken)
    {
        var mapping = context.Model.GetNode(entry.ClrType);
        var plan = GraphSaveCommandPlanCache.GetNodePlan(mapping);

        return UpdateNodeAsync(connection, transaction, plan, entry, node, cancellationToken);
    }

    private static Task<int> UpdateModifiedEdgeAsync(
        GraphContext context,
        DbConnection connection,
        DbTransaction transaction,
        GraphEntityEntry entry,
        Edge edge,
        CancellationToken cancellationToken)
    {
        var mapping = context.Model.GetEdge(entry.ClrType);
        var plan = GraphSaveCommandPlanCache.GetEdgePlan(mapping);

        return UpdateEdgeAsync(connection, transaction, plan, entry, edge, cancellationToken);
    }

    private static async Task<int> DeleteIncidentEdgesForDeletedNodesAsync(
        GraphContext context,
        GraphSaveChangesSet saveSet,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        var affectedRows = 0;
        foreach (var entry in saveSet.DeletedEntries)
        {
            if (entry.Entity is not Node node) continue;

            var nodeMapping = context.Model.GetNode(entry.ClrType);
            var nodeTable = SqlGenerationHelpers.EscapeFullName(nodeMapping.Schema, nodeMapping.TableName);
            var nodeKey = SqlGenerationHelpers.Escape(nodeMapping.KeyPropertyName);

            foreach (var edgeMapping in context.Model.Edges)
            {
                var predicates = new List<string>(2);
                if (edgeMapping.FromNodeType == entry.ClrType)
                {
                    predicates.Add($"e.$from_id = (SELECT n.$node_id FROM {nodeTable} AS n WHERE n.{nodeKey} = @NodeId)");
                }

                if (edgeMapping.ToNodeType == entry.ClrType)
                {
                    predicates.Add($"e.$to_id = (SELECT n.$node_id FROM {nodeTable} AS n WHERE n.{nodeKey} = @NodeId)");
                }

                if (predicates.Count == 0) continue;

                var edgeTable = SqlGenerationHelpers.EscapeFullName(edgeMapping.Schema, edgeMapping.TableName);
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = $"DELETE e FROM {edgeTable} AS e WHERE {string.Join(" OR ", predicates)};";
                AddParameter(command, "@NodeId", node.Id);
                affectedRows += await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        return affectedRows;
    }

    private static async Task<int> ProcessDeletedEntitiesAsync(
        GraphContext context,
        GraphSaveChangesSet saveSet,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        var affectedRows = 0;

        for (var i = 0; i < saveSet.DeletedEntries.Count; i++)
        {
            var entry = saveSet.DeletedEntries[i];

            switch (entry.Entity)
            {
                case Edge edge:
                    affectedRows += await DeleteDeletedEdgeAsync(context, connection, transaction, entry, edge, cancellationToken);
                    break;

                case Node node:
                    affectedRows += await DeleteDeletedNodeAsync(context, connection, transaction, entry, node, cancellationToken);
                    break;
            }
        }

        return affectedRows;
    }

    private static Task<int> DeleteDeletedEdgeAsync(
        GraphContext context,
        DbConnection connection,
        DbTransaction transaction,
        GraphEntityEntry entry,
        Edge edge,
        CancellationToken cancellationToken)
    {
        var mapping = context.Model.GetEdge(entry.ClrType);
        var plan = GraphSaveCommandPlanCache.GetEdgePlan(mapping);

        return DeleteEdgeByKeyAsync(connection, transaction, plan, entry, edge, cancellationToken);
    }

    private static Task<int> DeleteDeletedNodeAsync(
        GraphContext context,
        DbConnection connection,
        DbTransaction transaction,
        GraphEntityEntry entry,
        Node node,
        CancellationToken cancellationToken)
    {
        var mapping = context.Model.GetNode(entry.ClrType);
        var plan = GraphSaveCommandPlanCache.GetNodePlan(mapping);

        return DeleteNodeAsync(connection, transaction, plan, entry, node, cancellationToken);
    }

    private static Task RollbackIfNeededAsync(DbTransaction transaction, bool ownsTransaction, CancellationToken cancellationToken)
    {
        if (!ownsTransaction)
        {
            return Task.CompletedTask;
        }

        return transaction.RollbackAsync(cancellationToken);
    }

    private static void SetInitialConcurrencyTokenIfNeeded(object entity)
    {
        if (entity is not IHasConcurrencyToken concurrencyTracked)
        {
            return;
        }

        if (concurrencyTracked.Version <= 0)
        {
            concurrencyTracked.Version = 1;
        }
    }

    private static async Task<int> InsertNodeAsync(
        DbConnection connection,
        DbTransaction transaction,
        GraphNodeSaveCommandPlan plan,
        Node node,
        CancellationToken cancellationToken)
    {
        using var command = CreateCommand(connection, transaction, plan.InsertCommandText);

        for (var i = 0; i < plan.InsertProperties.Length; i++)
        {
            AddParameter(command, plan.InsertParameterNames[i], plan.InsertProperties[i].Get(node));
        }

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> UpdateNodeAsync(
        DbConnection connection,
        DbTransaction transaction,
        GraphNodeSaveCommandPlan plan,
        GraphEntityEntry entry,
        Node node,
        CancellationToken cancellationToken)
    {
        if (plan.ConcurrencyProperty is null)
        {
            return await UpdateWithoutConcurrencyAsync(
                new UpdateWithoutConcurrencyAsyncParameters
                {
                    Connection = connection,
                    Transaction = transaction,
                    CommandText = plan.UpdateWithoutConcurrencyCommandText,
                    ParameterNames = plan.UpdateWithoutConcurrencyParameterNames,
                    UpdatableProperties = plan.UpdatableProperties,
                    KeyValue = plan.KeyProperty.Get(node),
                    Entity = node,
                    CancellationToken = cancellationToken
                });
        }

        return await UpdateWithConcurrencyAsync(
            new UpdateWithConcurrencyAsyncParameters
            {
                Connection = connection,
                Transaction = transaction,
                CommandText = plan.UpdateWithConcurrencyCommandText!,
                ParameterNames = plan.UpdateWithConcurrencyParameterNames,
                NonConcurrencyProperties = plan.NonConcurrencyUpdatableProperties,
                ConcurrencyProperty = plan.ConcurrencyProperty,
                KeyProperty = plan.KeyProperty,
                Entry = entry,
                Entity = node,
                ClrType = plan.ClrType,
                CancellationToken = cancellationToken
            });
    }

    private static async Task<int> DeleteNodeAsync(
        DbConnection connection,
        DbTransaction transaction,
        GraphNodeSaveCommandPlan plan,
        GraphEntityEntry entry,
        Node node,
        CancellationToken cancellationToken)
    {
        if (plan.ConcurrencyProperty is null)
        {
            using var command = CreateCommand(connection, transaction, plan.DeleteWithoutConcurrencyCommandText);
            AddParameter(command, plan.DeleteWithoutConcurrencyParameterNames[0], plan.KeyProperty.Get(node));

            return await command.ExecuteNonQueryAsync(cancellationToken);
        }

        return await DeleteWithConcurrencyAsync(
            new DeleteWithConcurrencyAsyncParameters
            {
                Connection = connection,
                Transaction = transaction,
                CommandText = plan.DeleteWithConcurrencyCommandText!,
                ParameterNames = plan.DeleteWithConcurrencyParameterNames,
                ConcurrencyProperty = plan.ConcurrencyProperty,
                KeyProperty = plan.KeyProperty,
                Entry = entry,
                Entity = node,
                ClrType = plan.ClrType,
                CancellationToken = cancellationToken
            });
    }

    private static async Task<int> InsertEdgeAsync(InsertEdgeAsyncParameters inputs)
    {
        var connection = inputs.Connection;
        var transaction = inputs.Transaction;
        var plan = inputs.Plan;
        var edge = inputs.Edge;
        var fromNodeId = inputs.FromNodeId;
        var toNodeId = inputs.ToNodeId;
        var cancellationToken = inputs.CancellationToken;

        using var command = CreateCommand(connection, transaction, plan.InsertCommandText);

        AddParameter(command, plan.InsertParameterNames[0], fromNodeId);
        AddParameter(command, plan.InsertParameterNames[1], toNodeId);

        for (var i = 0; i < plan.InsertProperties.Length; i++)
        {
            AddParameter(command, plan.InsertParameterNames[i + 2], plan.InsertProperties[i].Get(edge));
        }

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> UpdateEdgeAsync(
        DbConnection connection,
        DbTransaction transaction,
        GraphEdgeSaveCommandPlan plan,
        GraphEntityEntry entry,
        Edge edge,
        CancellationToken cancellationToken)
    {
        if (plan.ConcurrencyProperty is null)
        {
            return await UpdateWithoutConcurrencyAsync(
                new UpdateWithoutConcurrencyAsyncParameters
                {
                    Connection = connection,
                    Transaction = transaction,
                    CommandText = plan.UpdateWithoutConcurrencyCommandText,
                    ParameterNames = plan.UpdateWithoutConcurrencyParameterNames,
                    UpdatableProperties = plan.UpdatableProperties,
                    KeyValue = plan.KeyProperty.Get(edge),
                    Entity = edge,
                    CancellationToken = cancellationToken
                });
        }

        return await UpdateWithConcurrencyAsync(
            new UpdateWithConcurrencyAsyncParameters
            {
                Connection = connection,
                Transaction = transaction,
                CommandText = plan.UpdateWithConcurrencyCommandText!,
                ParameterNames = plan.UpdateWithConcurrencyParameterNames,
                NonConcurrencyProperties = plan.NonConcurrencyUpdatableProperties,
                ConcurrencyProperty = plan.ConcurrencyProperty,
                KeyProperty = plan.KeyProperty,
                Entry = entry,
                Entity = edge,
                ClrType = plan.ClrType,
                CancellationToken = cancellationToken
            });
    }

    private static async Task<int> DeleteEdgeByKeyAsync(
        DbConnection connection,
        DbTransaction transaction,
        GraphEdgeSaveCommandPlan plan,
        GraphEntityEntry entry,
        Edge edge,
        CancellationToken cancellationToken)
    {
        if (plan.ConcurrencyProperty is null)
        {
            using var command = CreateCommand(connection, transaction, plan.DeleteWithoutConcurrencyCommandText);
            AddParameter(command, plan.DeleteWithoutConcurrencyParameterNames[0], plan.KeyProperty.Get(edge));

            return await command.ExecuteNonQueryAsync(cancellationToken);
        }

        return await DeleteWithConcurrencyAsync(
            new DeleteWithConcurrencyAsyncParameters
            {
                Connection = connection,
                Transaction = transaction,
                CommandText = plan.DeleteWithConcurrencyCommandText!,
                ParameterNames = plan.DeleteWithConcurrencyParameterNames,
                ConcurrencyProperty = plan.ConcurrencyProperty,
                KeyProperty = plan.KeyProperty,
                Entry = entry,
                Entity = edge,
                ClrType = plan.ClrType,
                CancellationToken = cancellationToken
            });
    }

    private static async Task<int> DeleteEdgeByNodeIdsAsync(
        DbConnection connection,
        DbTransaction transaction,
        GraphEdgeSaveCommandPlan plan,
        object? fromNodeId,
        object? toNodeId,
        CancellationToken cancellationToken)
    {
        using var command = CreateCommand(connection, transaction, plan.DeleteByNodeIdsCommandText);

        AddParameter(command, plan.DeleteByNodeIdsParameterNames[0], fromNodeId);
        AddParameter(command, plan.DeleteByNodeIdsParameterNames[1], toNodeId);

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> UpdateWithoutConcurrencyAsync(UpdateWithoutConcurrencyAsyncParameters inputs)
    {
        var connection = inputs.Connection;
        var transaction = inputs.Transaction;
        var commandText = inputs.CommandText;
        var parameterNames = inputs.ParameterNames;
        var updatableProperties = inputs.UpdatableProperties;
        var keyValue = inputs.KeyValue;
        var entity = inputs.Entity;
        var cancellationToken = inputs.CancellationToken;

        if (updatableProperties.Length == 0)
        {
            return 0;
        }

        using var command = CreateCommand(connection, transaction, commandText);

        for (var i = 0; i < updatableProperties.Length; i++)
        {
            AddParameter(command, parameterNames[i], updatableProperties[i].Get(entity));
        }

        AddParameter(command, parameterNames[updatableProperties.Length], keyValue);

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> UpdateWithConcurrencyAsync(UpdateWithConcurrencyAsyncParameters inputs)
    {
        var connection = inputs.Connection;
        var transaction = inputs.Transaction;
        var commandText = inputs.CommandText;
        var parameterNames = inputs.ParameterNames;
        var nonConcurrencyProperties = inputs.NonConcurrencyProperties;
        var concurrencyProperty = inputs.ConcurrencyProperty;
        var keyProperty = inputs.KeyProperty;
        var entry = inputs.Entry;
        var entity = inputs.Entity;
        var clrType = inputs.ClrType;
        var cancellationToken = inputs.CancellationToken;

        var originalVersion = GetOriginalConcurrencyValue(entry, concurrencyProperty.Name);
        var nextVersion = originalVersion + 1;

        using var command = CreateCommand(connection, transaction, commandText);

        for (var i = 0; i < nonConcurrencyProperties.Length; i++)
        {
            AddParameter(command, parameterNames[i], nonConcurrencyProperties[i].Get(entity));
        }

        var versionParameterIndex = nonConcurrencyProperties.Length;
        AddParameter(command, parameterNames[versionParameterIndex], nextVersion);
        AddParameter(command, parameterNames[versionParameterIndex + 1], keyProperty.Get(entity));
        AddParameter(command, parameterNames[versionParameterIndex + 2], originalVersion);

        var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken);

        if (affectedRows == 0)
        {
            throw new GraphConcurrencyException($"Concurrency conflict while updating '{clrType.Name}' with key '{keyProperty.Get(entity)}'.");
        }

        concurrencyProperty.Set?.Invoke(entity, nextVersion);
        return affectedRows;
    }

    private static async Task<int> DeleteWithConcurrencyAsync(DeleteWithConcurrencyAsyncParameters inputs)
    {
        var connection = inputs.Connection;
        var transaction = inputs.Transaction;
        var commandText = inputs.CommandText;
        var parameterNames = inputs.ParameterNames;
        var concurrencyProperty = inputs.ConcurrencyProperty;
        var keyProperty = inputs.KeyProperty;
        var entry = inputs.Entry;
        var entity = inputs.Entity;
        var clrType = inputs.ClrType;
        var cancellationToken = inputs.CancellationToken;

        var keyValue = keyProperty.Get(entity);
        var originalVersion = GetOriginalConcurrencyValue(entry, concurrencyProperty.Name);

        using var command = CreateCommand(connection, transaction, commandText);

        AddParameter(command, parameterNames[0], keyValue);
        AddParameter(command, parameterNames[1], originalVersion);

        var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken);

        if (affectedRows == 0)
        {
            throw new GraphConcurrencyException($"Concurrency conflict while deleting '{clrType.Name}' with key '{keyValue}'.");
        }

        return affectedRows;
    }

    private static async Task<object?> LoadNodeIdAsync(
        DbConnection connection,
        DbTransaction transaction,
        GraphNodeSaveCommandPlan plan,
        Node node,
        CancellationToken cancellationToken)
    {
        var keyValue = plan.KeyProperty.Get(node);

        using var command = CreateCommand(connection, transaction, plan.LoadNodeIdCommandText);
        AddParameter(command, plan.LoadNodeIdParameterNames[0], keyValue);

        var result = await command.ExecuteScalarAsync(cancellationToken);

        if (result is null || result == DBNull.Value)
        {
            throw new InvalidOperationException($"Unable to load $node_id for '{plan.ClrType.FullName}' with key '{keyValue}'.");
        }

        return result;
    }

    private static async Task<object?> GetOrLoadNodeIdAsync(
        DbConnection connection,
        DbTransaction transaction,
        GraphContext context,
        Dictionary<object, object?> cache,
        Node node,
        CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(node, out var cached))
        {
            return cached;
        }

        var mapping = context.Model.GetNode(node.GetType());
        var plan = GraphSaveCommandPlanCache.GetNodePlan(mapping);
        var nodeId = await LoadNodeIdAsync(connection, transaction, plan, node, cancellationToken);

        cache[node] = nodeId;

        return nodeId;
    }

    private static int GetOriginalConcurrencyValue(GraphEntityEntry entry, string concurrencyPropertyName)
    {
        var originalValue = entry.GetOriginalValue(concurrencyPropertyName);

        return originalValue switch
        {
            int intValue => intValue,
            long longValue => checked((int)longValue),
            decimal decimalValue => checked((int)decimalValue),
            null => throw new InvalidOperationException($"Original concurrency value for '{concurrencyPropertyName}' is null."),
            _ => Convert.ToInt32(originalValue, CultureInfo.InvariantCulture)
        };
    }

    private static DbCommand CreateCommand(DbConnection connection, DbTransaction transaction, string commandText)
    {
        var command = connection.CreateCommand();

        command.Transaction = transaction;
        command.CommandText = commandText;

        return command;
    }

    private static void AddParameter(DbCommand command, string name, object? value) =>
        GraphSqlCommandFactory.AddParameter(command, name, value);

    private sealed class GraphSaveChangesSet
    {
        public required List<GraphEntityEntry> AddedEntries { get; init; }
        public required List<GraphEntityEntry> ModifiedEntries { get; init; }
        public required List<GraphEntityEntry> DeletedEntries { get; init; }
        public required IReadOnlyList<PendingEdgeConnection> PendingEdgeConnections { get; init; }
        public required IReadOnlyList<PendingEdgeDisconnection> PendingEdgeDisconnections { get; init; }
        public required HashSet<object> AddedEdgesWithConnection { get; init; }

        public static GraphSaveChangesSet Create(GraphChangeTracker changeTracker)
        {
            var addedEntries = new List<GraphEntityEntry>();
            var modifiedEntries = new List<GraphEntityEntry>();
            var deletedEntries = new List<GraphEntityEntry>();

            foreach (var entry in changeTracker.Entries)
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        addedEntries.Add(entry);
                        break;

                    case EntityState.Modified:
                        modifiedEntries.Add(entry);
                        break;

                    case EntityState.Deleted:
                        deletedEntries.Add(entry);
                        break;
                }
            }

            var addedEdgesWithConnection = new HashSet<object>(ReferenceEqualityComparer.Instance);

            for (var i = 0; i < changeTracker.PendingEdgeConnections.Count; i++)
            {
                addedEdgesWithConnection.Add(changeTracker.PendingEdgeConnections[i].Edge);
            }

            return new GraphSaveChangesSet
            {
                AddedEntries = addedEntries,
                ModifiedEntries = modifiedEntries,
                DeletedEntries = deletedEntries,
                PendingEdgeConnections = changeTracker.PendingEdgeConnections,
                PendingEdgeDisconnections = changeTracker.PendingEdgeDisconnections,
                AddedEdgesWithConnection = addedEdgesWithConnection
            };
        }
    }

    /// <summary>
    /// Represents reference equality comparer.
    /// </summary>
    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        /// <summary>
        /// Executes new.
        /// </summary>
        /// <returns>The value.</returns>
        public static ReferenceEqualityComparer Instance { get; } = new();

        /// <summary>
        /// Executes equals.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <returns>True when successful; otherwise, false.</returns>
        public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

        /// <summary>
        /// Gets the value.
        /// </summary>
        /// <param name="obj">The obj.</param>
        /// <returns>The value.</returns>
        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }

    /// <summary>
    /// Groups the inputs for SaveChangesAsync.
    /// </summary>
    public sealed class SaveChangesAsyncParameters
    {
        /// <summary>
        /// Gets or initializes context.
        /// </summary>
        public required GraphContext Context { get; init; }

        /// <summary>
        /// Gets or initializes changeTracker.
        /// </summary>
        public required GraphChangeTracker ChangeTracker { get; init; }

        /// <summary>
        /// Gets or initializes connection.
        /// </summary>
        public required DbConnection Connection { get; init; }

        /// <summary>
        /// Gets or initializes transaction.
        /// </summary>
        public required DbTransaction Transaction { get; init; }

        /// <summary>
        /// Gets or initializes ownsTransaction.
        /// </summary>
        public required bool OwnsTransaction { get; init; }

        /// <summary>
        /// Gets or initializes acceptAllChangesOnSuccess.
        /// </summary>
        public bool AcceptAllChangesOnSuccess { get; init; } = true;

        /// <summary>
        /// Gets or initializes history capture that participates in the active transaction.
        /// </summary>
        public Func<DbConnection, DbTransaction, CancellationToken, Task>? CaptureHistoryAsync { get; init; }

        /// <summary>
        /// Gets or initializes cancellationToken.
        /// </summary>
        public CancellationToken CancellationToken { get; init; } = default;
    }

    private sealed class InsertAddedNodeAsyncParameters
    {
        public required GraphContext Context { get; init; }

        public required DbConnection Connection { get; init; }

        public required DbTransaction Transaction { get; init; }

        public required IDictionary<object, object?> PersistedNodeIds { get; init; }

        public required GraphEntityEntry Entry { get; init; }

        public required Node Node { get; init; }

        public required CancellationToken CancellationToken { get; init; }
    }

    private sealed class InsertEdgeAsyncParameters
    {
        public required DbConnection Connection { get; init; }

        public required DbTransaction Transaction { get; init; }

        public required GraphEdgeSaveCommandPlan Plan { get; init; }

        public required Edge Edge { get; init; }

        public required object? FromNodeId { get; init; }

        public required object? ToNodeId { get; init; }

        public required CancellationToken CancellationToken { get; init; }
    }

    private sealed class UpdateWithoutConcurrencyAsyncParameters
    {
        public required DbConnection Connection { get; init; }

        public required DbTransaction Transaction { get; init; }

        public required string CommandText { get; init; }

        public required string[] ParameterNames { get; init; }

        public required GraphSavePropertyPlan[] UpdatableProperties { get; init; }

        public required object? KeyValue { get; init; }

        public required object Entity { get; init; }

        public required CancellationToken CancellationToken { get; init; }
    }

    private sealed class UpdateWithConcurrencyAsyncParameters
    {
        public required DbConnection Connection { get; init; }

        public required DbTransaction Transaction { get; init; }

        public required string CommandText { get; init; }

        public required string[] ParameterNames { get; init; }

        public required GraphSavePropertyPlan[] NonConcurrencyProperties { get; init; }

        public required GraphSavePropertyPlan ConcurrencyProperty { get; init; }

        public required GraphSavePropertyPlan KeyProperty { get; init; }

        public required GraphEntityEntry Entry { get; init; }

        public required object Entity { get; init; }

        public required Type ClrType { get; init; }

        public required CancellationToken CancellationToken { get; init; }
    }

    private sealed class DeleteWithConcurrencyAsyncParameters
    {
        public required DbConnection Connection { get; init; }

        public required DbTransaction Transaction { get; init; }

        public required string CommandText { get; init; }

        public required string[] ParameterNames { get; init; }

        public required GraphSavePropertyPlan ConcurrencyProperty { get; init; }

        public required GraphSavePropertyPlan KeyProperty { get; init; }

        public required GraphEntityEntry Entry { get; init; }

        public required object Entity { get; init; }

        public required Type ClrType { get; init; }

        public required CancellationToken CancellationToken { get; init; }
    }
}