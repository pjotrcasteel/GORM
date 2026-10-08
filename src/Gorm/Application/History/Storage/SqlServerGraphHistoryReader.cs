using System.Data;
using System.Data.Common;
using System.Text.Json;
using Gorm.Application.History.Envelopes;
using Gorm.Application.Temporal.Bitemporal;
using Gorm.Application.Temporal.History;
using Gorm.Core.Primitives;
using Gorm.Application.History;
using Gorm.Infrastructure.Persistence.Connections;
using Gorm.Infrastructure.Providers.SqlServer.Helpers;

namespace Gorm.Application.History.Storage;

/// <summary>
/// Reads graph history envelopes from SQL Server generic history tables.
/// </summary>
public sealed class SqlServerGraphHistoryReader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IGormDbConnectionFactory _connectionFactory;
    private readonly string _schemaName;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlServerGraphHistoryReader"/> class.
    /// </summary>
    /// <param name="connectionFactory">The connection factory.</param>
    /// <param name="schemaName">The schema name.</param>
    public SqlServerGraphHistoryReader(IGormDbConnectionFactory connectionFactory, string schemaName = "dbo")
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _schemaName = string.IsNullOrWhiteSpace(schemaName) ? "dbo" : schemaName;
    }

    /// <summary>
    /// Reads node history for the specified node type.
    /// </summary>
    /// <typeparam name="TNode">The node type.</typeparam>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The history envelopes.</returns>
    public Task<IReadOnlyList<GraphHistoryEnvelope>> ReadNodeHistoryAsync<TNode>(CancellationToken cancellationToken = default) =>
        ReadHistoryAsync(typeof(TNode), isEdge: false, cancellationToken);

    /// <summary>
    /// Reads edge history for the specified edge type.
    /// </summary>
    /// <typeparam name="TEdge">The edge type.</typeparam>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The history envelopes.</returns>
    public Task<IReadOnlyList<GraphHistoryEnvelope>> ReadEdgeHistoryAsync<TEdge>(CancellationToken cancellationToken = default) =>
        ReadHistoryAsync(typeof(TEdge), isEdge: true, cancellationToken);

    /// <summary>
    /// Reads node history synchronously for callers using the synchronous query API.
    /// </summary>
    public IReadOnlyList<GraphHistoryEnvelope> ReadNodeHistory<TNode>() =>
        ReadHistory(typeof(TNode), isEdge: false);

    /// <summary>
    /// Reads edge history synchronously for callers using the synchronous query API.
    /// </summary>
    public IReadOnlyList<GraphHistoryEnvelope> ReadEdgeHistory<TEdge>() =>
        ReadHistory(typeof(TEdge), isEdge: true);

    internal IReadOnlyList<EdgeHistoryState> ReadActiveEdgeStates(Type edgeType, Guid? fromId = null, Guid? toId = null, Guid? entityId = null)
    {
        ArgumentNullException.ThrowIfNull(edgeType);
        var entries = ReadHistory(edgeType, isEdge: true, fromId, toId, entityId);
        return [.. entries
            .GroupBy(x => x.EntityId)
            .Select(x => x.OrderByDescending(e => e.CapturedAtUtc).ThenBy(e => e.ValidToUtc.HasValue).First())
            .Where(x => x.OperationKind is not (GraphHistoryOperationKind.Disconnected or GraphHistoryOperationKind.Deleted))
            .Select(x => new EdgeHistoryState(x.EntityId, x.Snapshot))];
    }

    internal sealed record EdgeHistoryState(Guid Id, object Snapshot);

    /// <summary>
    /// Captures a bounded bitemporal dataset for explicitly scoped node and edge identities.
    /// Node and edge history are read in one SQL Server snapshot transaction to prevent
    /// mixed generations when concurrent history writers commit.
    /// </summary>
    /// <typeparam name="TNode">The history node type.</typeparam>
    /// <typeparam name="TEdge">The history edge type.</typeparam>
    /// <param name="nodeIds">Node identifiers to include in the detached dataset.</param>
    /// <param name="edgeIds">Edge identifiers to include in the detached dataset.</param>
    /// <param name="maximumHistoryEntries">Maximum selected history envelopes to materialize.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A detached dataset for valid-time and recorded-time projections.</returns>
    public async Task<GraphWorldHistoryDataset> CaptureBitemporalDatasetAsync<TNode, TEdge>(
        IReadOnlyCollection<Guid> nodeIds,
        IReadOnlyCollection<Guid> edgeIds,
        int maximumHistoryEntries = 1_000_000,
        CancellationToken cancellationToken = default)
        where TNode : Node
        where TEdge : Edge
    {
        ArgumentNullException.ThrowIfNull(nodeIds);
        ArgumentNullException.ThrowIfNull(edgeIds);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumHistoryEntries);
        var nodeFilter = nodeIds.ToHashSet();
        var edgeFilter = edgeIds.ToHashSet();
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Snapshot, cancellationToken);

        var nodes = nodeFilter.Count == 0
            ? []
            : (await ReadHistoryAsync(typeof(TNode), isEdge: false, connection, transaction, cancellationToken))
                .Where(x => nodeFilter.Contains(x.EntityId)).ToArray();

        if (nodes.Length > maximumHistoryEntries)
        {
            throw new GraphWorldHistoryProjectionException(
                GraphWorldHistoryProjectionFailureReason.EntryLimitExceeded,
                $"Node history exceeded {nameof(maximumHistoryEntries)} ({maximumHistoryEntries}).");
        }

        var edges = edgeFilter.Count == 0
            ? []
            : (await ReadHistoryAsync(typeof(TEdge), isEdge: true, connection, transaction, cancellationToken))
                .Where(x => edgeFilter.Contains(x.EntityId)).ToArray();

        var dataset = GraphWorldHistoryDataset.Capture(nodes.Concat(edges), maximumHistoryEntries, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return dataset;
    }

    private List<GraphHistoryEnvelope> ReadHistory(Type entityType, bool isEdge, Guid? fromId = null, Guid? toId = null, Guid? entityId = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var command = CreateHistoryCommand(connection, entityType, isEdge, fromId, toId, entityId);
        using var reader = command.ExecuteReader();
        var results = new List<GraphHistoryEnvelope>();
        while (reader.Read())
        {
            results.Add(ReadEnvelope(reader, entityType, isEdge));
        }

        return results;
    }

    private async Task<IReadOnlyList<GraphHistoryEnvelope>> ReadHistoryAsync(Type entityType, bool isEdge, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = CreateHistoryCommand(connection, entityType, isEdge);

        var results = new List<GraphHistoryEnvelope>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(ReadEnvelope(reader, entityType, isEdge));
        }

        return results;
    }

    private async Task<IReadOnlyList<GraphHistoryEnvelope>> ReadHistoryAsync(
        Type entityType, bool isEdge, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = CreateHistoryCommand(connection, entityType, isEdge);
        command.Transaction = transaction;
        var results = new List<GraphHistoryEnvelope>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(ReadEnvelope(reader, entityType, isEdge));
        }

        return results;
    }

    private DbCommand CreateHistoryCommand(DbConnection connection, Type entityType, bool isEdge, Guid? fromId = null, Guid? toId = null, Guid? entityId = null)
    {
        var tableName = SqlGenerationHelpers.EscapeFullName(_schemaName, isEdge ? "GormEdgeHistory" : "GormNodeHistory");
        var command = connection.CreateCommand();
        var edgeFilter = isEdge
            ? (fromId.HasValue ? " AND [FromId] = @FromId" : string.Empty) +
              (toId.HasValue ? " AND [ToId] = @ToId" : string.Empty) +
              (entityId.HasValue ? " AND [EntityId] = @EntityId" : string.Empty)
            : string.Empty;
        command.CommandText = isEdge
            ? $"""
               SELECT
                   [EntityType],
                   [EntityId],
                   [OperationKind],
                   [CapturedAtUtc],
                   [ValidFromUtc],
                   [ValidToUtc],
                   [FromId],
                   [ToId],
                   [SnapshotJson]
               FROM {tableName}
               WHERE [EntityType] = @EntityType {edgeFilter}
               ORDER BY [CapturedAtUtc] ASC
               """
            : $"""
               SELECT
                   [EntityType],
                   [EntityId],
                   [OperationKind],
                   [CapturedAtUtc],
                   [ValidFromUtc],
                   [ValidToUtc],
                   [SnapshotJson]
               FROM {tableName}
               WHERE [EntityType] = @EntityType
               ORDER BY [CapturedAtUtc] ASC
               """;
        AddParameter(command, "@EntityType", entityType.FullName ?? entityType.Name);
        if (isEdge)
        {
            if (fromId.HasValue) AddParameter(command, "@FromId", fromId.Value);
            if (toId.HasValue) AddParameter(command, "@ToId", toId.Value);
            if (entityId.HasValue) AddParameter(command, "@EntityId", entityId.Value);
        }
        return command;
    }

    private static GraphHistoryEnvelope ReadEnvelope(DbDataReader reader, Type entityType, bool isEdge) =>
        isEdge ? ReadEdgeEnvelope(reader, entityType) : ReadNodeEnvelope(reader, entityType);

    private static GraphHistoryEnvelope ReadNodeEnvelope(DbDataReader reader, Type entityType)
    {
        var entityId = reader.GetGuid(reader.GetOrdinal("EntityId"));
        var operationKind = (GraphHistoryOperationKind)reader.GetInt32(reader.GetOrdinal("OperationKind"));
        var capturedAtUtc = reader.GetDateTime(reader.GetOrdinal("CapturedAtUtc"));
        var validFromUtc = reader.GetDateTime(reader.GetOrdinal("ValidFromUtc"));
        var validToUtc = ReadNullableDateTime(reader, "ValidToUtc");
        var snapshotJson = reader.GetString(reader.GetOrdinal("SnapshotJson"));

        var snapshot = JsonSerializer.Deserialize(snapshotJson, entityType, SerializerOptions) ?? throw new InvalidOperationException(
            $"Failed to deserialize history snapshot for '{entityType.FullName}'.");

        return new GraphHistoryEnvelope
        {
            EntityType = entityType,
            EntityId = entityId,
            OperationKind = operationKind,
            CapturedAtUtc = DateTime.SpecifyKind(capturedAtUtc, DateTimeKind.Utc),
            ValidFromUtc = DateTime.SpecifyKind(validFromUtc, DateTimeKind.Utc),
            ValidToUtc = validToUtc is null
                ? null
                : DateTime.SpecifyKind(validToUtc.Value, DateTimeKind.Utc),
            IsEdge = false,
            FromId = null,
            ToId = null,
            Snapshot = snapshot
        };
    }

    private static GraphHistoryEnvelope ReadEdgeEnvelope(DbDataReader reader, Type entityType)
    {
        var entityId = reader.GetGuid(reader.GetOrdinal("EntityId"));
        var operationKind = (GraphHistoryOperationKind)reader.GetInt32(reader.GetOrdinal("OperationKind"));
        var capturedAtUtc = reader.GetDateTime(reader.GetOrdinal("CapturedAtUtc"));
        var validFromUtc = reader.GetDateTime(reader.GetOrdinal("ValidFromUtc"));
        var validToUtc = ReadNullableDateTime(reader, "ValidToUtc");
        var fromId = ReadNullableGuid(reader, "FromId");
        var toId = ReadNullableGuid(reader, "ToId");
        var snapshotJson = reader.GetString(reader.GetOrdinal("SnapshotJson"));

        var snapshot = JsonSerializer.Deserialize(snapshotJson, entityType, SerializerOptions) ?? throw new InvalidOperationException(
            $"Failed to deserialize history snapshot for '{entityType.FullName}'.");

        return new GraphHistoryEnvelope
        {
            EntityType = entityType,
            EntityId = entityId,
            OperationKind = operationKind,
            CapturedAtUtc = DateTime.SpecifyKind(capturedAtUtc, DateTimeKind.Utc),
            ValidFromUtc = DateTime.SpecifyKind(validFromUtc, DateTimeKind.Utc),
            ValidToUtc = validToUtc is null
                ? null
                : DateTime.SpecifyKind(validToUtc.Value, DateTimeKind.Utc),
            IsEdge = true,
            FromId = fromId,
            ToId = toId,
            Snapshot = snapshot
        };
    }

    private static Guid? ReadNullableGuid(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal) ? null : reader.GetGuid(ordinal);
    }

    private static DateTime? ReadNullableDateTime(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
    }

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}