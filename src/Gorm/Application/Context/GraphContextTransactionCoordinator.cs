using System.Data.Common;
using Gorm.Application.Execution;
using Gorm.Application.Tracking;
using Gorm.Infrastructure.Persistence.Connections;

namespace Gorm.Application.Context;

internal sealed class GraphContextTransactionCoordinator
{
    private readonly GraphContextTransactionState _transactionState = new();

    public Task<int> SaveChangesAsync(
        GraphContext context,
        GraphChangeTracker changeTracker,
        IGormDbConnectionFactory connectionFactory,
        bool acceptAllChangesOnSuccess,
        Func<DbConnection, DbTransaction, CancellationToken, Task>? captureHistoryAsync,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(changeTracker);
        ArgumentNullException.ThrowIfNull(connectionFactory);
        return SaveChangesCoreAsync(context, changeTracker, connectionFactory, acceptAllChangesOnSuccess, captureHistoryAsync, cancellationToken);
    }

    private async Task<int> SaveChangesCoreAsync(
        GraphContext context,
        GraphChangeTracker changeTracker,
        IGormDbConnectionFactory connectionFactory,
        bool acceptAllChangesOnSuccess,
        Func<DbConnection, DbTransaction, CancellationToken, Task>? captureHistoryAsync,
        CancellationToken cancellationToken)
    {
        if (TryGetCurrentTransaction(out var connection, out var transaction))
        {
            return await GraphSaveChangesExecutor.SaveChangesAsync(
                new GraphSaveChangesExecutor.SaveChangesAsyncParameters
                {
                    Context = context,
                    ChangeTracker = changeTracker,
                    Connection = connection,
                    Transaction = transaction,
                    OwnsTransaction = false,
                    AcceptAllChangesOnSuccess = acceptAllChangesOnSuccess,
                    CaptureHistoryAsync = captureHistoryAsync,
                    CancellationToken = cancellationToken
                });
        }

        var executor = new GraphSaveChangesExecutor(connectionFactory);

        return await executor.SaveChangesAsync(context, changeTracker, acceptAllChangesOnSuccess, captureHistoryAsync, cancellationToken);
    }

    public Task<GraphTransaction> BeginTransactionAsync(GraphContext context, IGormDbConnectionFactory connectionFactory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(connectionFactory);
        return BeginTransactionCoreAsync(context, connectionFactory, cancellationToken);
    }

    private async Task<GraphTransaction> BeginTransactionCoreAsync(GraphContext context, IGormDbConnectionFactory connectionFactory, CancellationToken cancellationToken)
    {
        if (_transactionState.HasActiveTransaction)
        {
            var savepointName = GraphTransaction.CreateSavepointName();

            await GraphTransaction.ExecuteSavepointAsync(_transactionState.CurrentTransactionConnection!, _transactionState.CurrentTransaction!, savepointName, cancellationToken);

            return new GraphTransaction(context, _transactionState.CurrentTransactionConnection!, _transactionState.CurrentTransaction!, savepointName, ownsTransaction: false);
        }

        var connection = connectionFactory.CreateConnection();
        DbTransaction? transaction = null;

        try
        {
            await connection.OpenAsync(cancellationToken);
            transaction = await connection.BeginTransactionAsync(cancellationToken);

            var graphTransaction = new GraphTransaction(context, connection, transaction, savepointName: null, ownsTransaction: true);

            _transactionState.CurrentTransactionConnection = connection;
            _transactionState.CurrentTransaction = transaction;
            _transactionState.CurrentGraphTransaction = graphTransaction;

            return graphTransaction;
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }

            await connection.DisposeAsync();
            throw;
        }
    }

    public bool TryGetCurrentTransaction(out DbConnection connection, out DbTransaction transaction)
    {
        if (_transactionState.CurrentTransactionConnection is not null &&
            _transactionState.CurrentTransaction is not null &&
            _transactionState.CurrentGraphTransaction is not null &&
            !_transactionState.CurrentGraphTransaction.IsCompleted)
        {
            connection = _transactionState.CurrentTransactionConnection;
            transaction = _transactionState.CurrentTransaction;
            return true;
        }

        connection = null!;
        transaction = null!;
        return false;
    }

    public void ClearCurrentTransaction(GraphTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        if (!ReferenceEquals(_transactionState.CurrentGraphTransaction, transaction))
        {
            return;
        }

        _transactionState.Clear();
    }
}