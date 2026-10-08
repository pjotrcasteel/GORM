using System.Data;
using System.Data.Common;
using System.Text.Json;
using Gorm.Application.Context;
using Gorm.Application.History.Abstractions;
using Gorm.Application.History.Envelopes;
using Gorm.Application.Tracking;
using Gorm.Infrastructure.Persistence.Connections;
using Gorm.Infrastructure.Providers.SqlServer.Helpers;

namespace Gorm.Infrastructure.Providers.SqlServer;

/// <summary>
/// Persists graph history envelopes to SQL Server generic history tables.
/// </summary>
public sealed class SqlServerGraphHistoryRecorder : IGraphHistoryBatchRecorder
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IGormDbConnectionFactory _connectionFactory;
    private readonly string _schemaName;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlServerGraphHistoryRecorder"/> class.
    /// </summary>
    /// <param name="connectionFactory">The connection factory.</param>
    /// <param name="schemaName">The schema name.</param>
    public SqlServerGraphHistoryRecorder(IGormDbConnectionFactory connectionFactory, string schemaName = "dbo")
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _schemaName = string.IsNullOrWhiteSpace(schemaName) ? "dbo" : schemaName;
    }

    /// <summary>
    /// Captures history for the pending graph changes.
    /// </summary>
    /// <param name="context">The graph context.</param>
    /// <param name="changeTracker">The change tracker.</param>
    /// <param name="capturedAtUtc">The capture timestamp in UTC.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task CaptureAsync(GraphContext context, GraphChangeTracker changeTracker, DateTime capturedAtUtc, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(changeTracker);

        var envelopes = GraphHistoryEnvelopeCollector.Collect(context, changeTracker, capturedAtUtc);
        return PersistAsync(envelopes, connection: null, transaction: null, cancellationToken);
    }

    /// <inheritdoc />
    public Task PersistAsync(
        IReadOnlyList<GraphHistoryEnvelope> envelopes,
        DbConnection? connection,
        DbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelopes);
        cancellationToken.ThrowIfCancellationRequested();

        // Validate the full batch before opening a transaction or inserting any history rows.
        foreach (var envelope in envelopes)
        {
            ArgumentNullException.ThrowIfNull(envelope);
            if (envelope.ValidToUtc is { } validTo && validTo < envelope.ValidFromUtc)
            {
                throw new ArgumentException("History ValidToUtc cannot precede ValidFromUtc.", nameof(envelopes));
            }
        }

        if (connection is null && transaction is null)
        {
            return envelopes.Count == 0
                ? Task.CompletedTask
                : CaptureCoreAsync(envelopes, cancellationToken);
        }

        if (connection is null || transaction is null)
        {
            throw new ArgumentException("History persistence requires both a connection and a transaction.");
        }

        return PersistInTransactionAsync(connection, transaction, envelopes, cancellationToken);
    }

    private async Task CaptureCoreAsync(IReadOnlyList<GraphHistoryEnvelope> envelopes, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        try
        {
            await PersistInTransactionAsync(connection, transaction, envelopes, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task PersistInTransactionAsync(
        DbConnection connection,
        DbTransaction transaction,
        IReadOnlyList<GraphHistoryEnvelope> envelopes,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < envelopes.Count; index++)
        {
            await InsertEnvelopeAsync(connection, transaction, envelopes[index], cancellationToken);
        }
    }

    private async Task InsertEnvelopeAsync(DbConnection connection, DbTransaction transaction, GraphHistoryEnvelope envelope, CancellationToken cancellationToken)
    {
        var tableName = envelope.IsEdge
            ? SqlGenerationHelpers.EscapeFullName(_schemaName, "GormEdgeHistory")
            : SqlGenerationHelpers.EscapeFullName(_schemaName, "GormNodeHistory");

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = envelope.IsEdge
            ? $"""
               INSERT INTO {tableName}
               (
                   [HistoryId],
                   [EntityType],
                   [EntityId],
                   [OperationKind],
                   [CapturedAtUtc],
                   [ValidFromUtc],
                   [ValidToUtc],
                   [FromId],
                   [ToId],
                   [SnapshotJson]
               )
               VALUES
               (
                   @HistoryId,
                   @EntityType,
                   @EntityId,
                   @OperationKind,
                   @CapturedAtUtc,
                   @ValidFromUtc,
                   @ValidToUtc,
                   @FromId,
                   @ToId,
                   @SnapshotJson
               );
               """
            : $"""
               INSERT INTO {tableName}
               (
                   [HistoryId],
                   [EntityType],
                   [EntityId],
                   [OperationKind],
                   [CapturedAtUtc],
                   [ValidFromUtc],
                   [ValidToUtc],
                   [SnapshotJson]
               )
               VALUES
               (
                   @HistoryId,
                   @EntityType,
                   @EntityId,
                   @OperationKind,
                   @CapturedAtUtc,
                   @ValidFromUtc,
                   @ValidToUtc,
                   @SnapshotJson
               );
               """;

        AddParameter(command, "@HistoryId", Guid.NewGuid());
        AddParameter(command, "@EntityType", envelope.EntityType.FullName ?? envelope.EntityType.Name);
        AddParameter(command, "@EntityId", envelope.EntityId);
        AddParameter(command, "@OperationKind", (int)envelope.OperationKind);
        AddParameter(command, "@CapturedAtUtc", envelope.CapturedAtUtc);
        AddParameter(command, "@ValidFromUtc", envelope.ValidFromUtc);
        AddParameter(command, "@ValidToUtc", envelope.ValidToUtc);
        AddParameter(command, "@SnapshotJson", JsonSerializer.Serialize(envelope.Snapshot, envelope.EntityType, SerializerOptions));

        if (envelope.IsEdge)
        {
            AddParameter(command, "@FromId", envelope.FromId);
            AddParameter(command, "@ToId", envelope.ToId);
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
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