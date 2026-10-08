using System.Data.Common;
using Gorm.Application.Context;
using Gorm.Infrastructure.Persistence.Connections;
using Gorm.Infrastructure.Sql;

namespace Gorm.Application.Execution;

/// <summary>
/// Represents an execution command lease that either owns a fresh connection or reuses the active graph transaction connection.
/// </summary>
internal sealed class GraphQueryCommandLease : IAsyncDisposable
{
    private readonly bool _ownsConnection;

    private GraphQueryCommandLease(DbConnection connection, DbTransaction? transaction, bool ownsConnection)
    {
        Connection = connection;
        Transaction = transaction;
        _ownsConnection = ownsConnection;
    }

    public DbConnection Connection { get; }

    public DbTransaction? Transaction { get; }

    public static async ValueTask<GraphQueryCommandLease> OpenAsync(GraphContext context, IGormDbConnectionFactory connectionFactory, CancellationToken cancellationToken)
    {
        if (context.TryGetCurrentTransaction(out var transactionConnection, out var transaction))
        {
            return new GraphQueryCommandLease(transactionConnection, transaction, ownsConnection: false);
        }

        var connection = connectionFactory.CreateConnection();
        try
        {
            await connection.OpenAsync(cancellationToken);
            return new GraphQueryCommandLease(connection, transaction: null, ownsConnection: true);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    public DbCommand CreateCommand(GraphSqlQuery sql)
    {
        ArgumentNullException.ThrowIfNull(sql);
        return GraphSqlCommandFactory.CreateCommand(Connection, sql, Transaction);
    }

    public async ValueTask DisposeAsync()
    {
        if (_ownsConnection)
        {
            await Connection.DisposeAsync();
        }
    }
}