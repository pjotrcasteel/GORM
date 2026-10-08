using System.Data.Common;
using Gorm.Application.Context;
using Gorm.Infrastructure.Providers.SqlServer.Helpers;

namespace Gorm.Application.Execution;

/// <summary>
/// Represents graph transaction.
/// </summary>
public sealed class GraphTransaction : IAsyncDisposable, IDisposable
{
    private readonly GraphContext _context;
    private readonly DbConnection _connection;
    private readonly DbTransaction _transaction;
    private readonly string? _savepointName;
    private readonly bool _ownsTransaction;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphTransaction"/> class.
    /// </summary>
    /// <param name="context">The graph context.</param>
    /// <param name="connection">The connection.</param>
    /// <param name="transaction">The transaction.</param>
    /// <param name="savepointName">The savepoint name.</param>
    /// <param name="ownsTransaction">The owns transaction.</param>
    internal GraphTransaction(GraphContext context, DbConnection connection, DbTransaction transaction, string? savepointName, bool ownsTransaction)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
        _savepointName = savepointName;
        _ownsTransaction = ownsTransaction;
    }

    /// <summary>
    /// Checks if the transaction is nested (i.e., created within another transaction) based on the presence of a savepoint name.
    /// </summary>
    public bool IsNested =>
        !string.IsNullOrWhiteSpace(_savepointName);

    /// <summary>
    /// Gets a value indicating whether this transaction has already been committed or rolled back.
    /// </summary>
    public bool IsCompleted { get; private set; }

    /// <summary>
    /// Executes begin transaction async.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public async Task<GraphTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        await _context.BeginTransactionAsync(cancellationToken);

    /// <summary>
    /// Creates the item.
    /// </summary>
    /// <param name="savepointName">The savepoint name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation result.</returns>
    public async Task<string> CreateSavepointAsync(string? savepointName = null, CancellationToken cancellationToken = default)
    {
        var effectiveSavepointName = string.IsNullOrWhiteSpace(savepointName) ? CreateSavepointName() : savepointName;

        await ExecuteSavepointAsync(_connection, _transaction, effectiveSavepointName, cancellationToken);

        return effectiveSavepointName;
    }

    /// <summary>
    /// Executes rollback to savepoint async.
    /// </summary>
    /// <param name="savepointName">The savepoint name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task RollbackToSavepointAsync(string savepointName, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(savepointName);

        await ExecuteTransactionalCommandAsync(_connection, _transaction, $"ROLLBACK TRANSACTION {SqlGenerationHelpers.Escape(savepointName)}", cancellationToken);
    }

    /// <summary>
    /// Executes commit async.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (IsCompleted)
        {
            throw new InvalidOperationException("The graph transaction has already been completed.");
        }

        IsCompleted = true;

        if (IsNested)
        {
            return Task.CompletedTask;
        }

        return _transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Executes rollback async.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (IsCompleted)
        {
            throw new InvalidOperationException("The graph transaction has already been completed.");
        }

        IsCompleted = true;

        if (IsNested)
        {
            await ExecuteTransactionalCommandAsync(_connection, _transaction, $"ROLLBACK TRANSACTION {SqlGenerationHelpers.Escape(_savepointName!)}", cancellationToken);

            return;
        }

        await _transaction.RollbackAsync(cancellationToken);
    }

    /// <summary>
    /// Executes dispose.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            if (!IsCompleted)
            {
                if (IsNested)
                {
                    ExecuteTransactionalCommand(_connection, _transaction, $"ROLLBACK TRANSACTION {SqlGenerationHelpers.Escape(_savepointName!)}");
                }
                else
                {
                    _transaction.Rollback();
                }
            }
        }
        finally
        {
            _disposed = true;
            if (_ownsTransaction)
            {
                _context.ClearCurrentTransaction(this);
                _transaction.Dispose();
                _connection.Dispose();
            }
        }
    }

    /// <summary>
    /// Executes dispose async.
    /// </summary>
    /// <returns>The value.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            if (!IsCompleted)
            {
                if (IsNested)
                {
                    await ExecuteTransactionalCommandAsync(
                        _connection,
                        _transaction,
                        $"ROLLBACK TRANSACTION {SqlGenerationHelpers.Escape(_savepointName!)}",
                        CancellationToken.None);
                }
                else
                {
                    await _transaction.RollbackAsync(CancellationToken.None);
                }
            }
        }
        finally
        {
            _disposed = true;

            if (_ownsTransaction)
            {
                _context.ClearCurrentTransaction(this);
                await _transaction.DisposeAsync();
                await _connection.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Creates the item.
    /// </summary>
    /// <returns>The value.</returns>
    internal static string CreateSavepointName()
        => $"gorm_sp_{Guid.NewGuid():N}";

    /// <summary>
    /// Executes execute savepoint async.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <param name="transaction">The transaction.</param>
    /// <param name="savepointName">The savepoint name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal static Task ExecuteSavepointAsync(DbConnection connection, DbTransaction transaction, string savepointName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentException.ThrowIfNullOrWhiteSpace(savepointName);

        return ExecuteTransactionalCommandAsync(connection, transaction, $"SAVE TRANSACTION {SqlGenerationHelpers.Escape(savepointName)}", cancellationToken);
    }

    private static async Task ExecuteTransactionalCommandAsync(DbConnection connection, DbTransaction transaction, string commandText, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void ExecuteTransactionalCommand(DbConnection connection, DbTransaction transaction, string commandText)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
        command.ExecuteNonQuery();
    }
}