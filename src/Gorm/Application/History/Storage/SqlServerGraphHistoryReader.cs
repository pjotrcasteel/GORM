using System.Data.Common;
using System.Text.Json;
using Gorm.Application.History.Envelopes;
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

    private List<GraphHistoryEnvelope> ReadHistory(Type entityType, bool isEdge)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var command = CreateHistoryCommand(connection, entityType, isEdge);
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

    private DbCommand CreateHistoryCommand(DbConnection connection, Type entityType, bool isEdge)
    {
        var tableName = SqlGenerationHelpers.EscapeFullName(_schemaName, isEdge ? "GormEdgeHistory" : "GormNodeHistory");
        var command = connection.CreateCommand();
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
               WHERE [EntityType] = @EntityType
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