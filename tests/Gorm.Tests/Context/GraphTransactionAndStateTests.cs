using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using Gorm.Application.Context;
using Gorm.Application.Execution;
using Gorm.Core.Configuration;
using Gorm.Infrastructure.Persistence.Connections;

namespace Gorm.Tests.Context;

[TestClass]
public sealed class GraphTransactionAndStateTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void GraphContextTransactionState_HasActiveTransaction_False_By_Default()
    {
        var state = new GraphContextTransactionState();
        Assert.IsFalse(state.HasActiveTransaction);
    }

    [TestMethod]
    public void GraphContextTransactionState_HasActiveTransaction_True_When_All_Are_Set()
    {
        var state = new GraphContextTransactionState();
        var connection = new TxFakeDbConnection();
        var transaction = new TxFakeDbTransaction(connection);
        var context = new TxGraphContext(new TxFakeConnectionFactory(connection));
        var graphTransaction = new GraphTransaction(context, connection, transaction, savepointName: null, ownsTransaction: false);

        state.CurrentTransactionConnection = connection;
        state.CurrentTransaction = transaction;
        state.CurrentGraphTransaction = graphTransaction;

        Assert.IsTrue(state.HasActiveTransaction);
    }

    [TestMethod]
    public void GraphContextTransactionState_Clear_Resets_All_Fields()
    {
        var state = new GraphContextTransactionState();
        var connection = new TxFakeDbConnection();
        var transaction = new TxFakeDbTransaction(connection);
        var context = new TxGraphContext(new TxFakeConnectionFactory(connection));
        var graphTransaction = new GraphTransaction(context, connection, transaction, savepointName: null, ownsTransaction: false);

        state.CurrentTransactionConnection = connection;
        state.CurrentTransaction = transaction;
        state.CurrentGraphTransaction = graphTransaction;

        state.Clear();

        Assert.IsNull(state.CurrentTransactionConnection);
        Assert.IsNull(state.CurrentTransaction);
        Assert.IsNull(state.CurrentGraphTransaction);
        Assert.IsFalse(state.HasActiveTransaction);
    }

    [TestMethod]
    public void GraphTransaction_Constructor_Throws_For_Null_Context()
    {
        var connection = new TxFakeDbConnection();
        var transaction = new TxFakeDbTransaction(connection);

        Assert.ThrowsExactly<ArgumentNullException>(() => new GraphTransaction(null!, connection, transaction, savepointName: null, ownsTransaction: false));
    }

    [TestMethod]
    public void GraphTransaction_Constructor_Throws_For_Null_Connection()
    {
        var context = new TxGraphContext(new TxFakeConnectionFactory(new TxFakeDbConnection()));
        var transaction = new TxFakeDbTransaction(new TxFakeDbConnection());

        Assert.ThrowsExactly<ArgumentNullException>(() => new GraphTransaction(context, null!, transaction, savepointName: null, ownsTransaction: false));
    }

    [TestMethod]
    public void GraphTransaction_Constructor_Throws_For_Null_Transaction()
    {
        var connection = new TxFakeDbConnection();
        var context = new TxGraphContext(new TxFakeConnectionFactory(connection));

        Assert.ThrowsExactly<ArgumentNullException>(() => new GraphTransaction(context, connection, null!, savepointName: null, ownsTransaction: false));
    }

    [TestMethod]
    public async Task GraphTransaction_CommitAsync_On_Root_Commits_DbTransaction()
    {
        var connection = new TxFakeDbConnection();
        var transaction = new TxFakeDbTransaction(connection);
        var context = new TxGraphContext(new TxFakeConnectionFactory(connection));
        await using var graphTransaction = new GraphTransaction(context, connection, transaction, savepointName: null, ownsTransaction: false);

        await graphTransaction.CommitAsync(TestContext.CancellationToken);

        Assert.IsTrue(graphTransaction.IsCompleted);
        Assert.AreEqual(1, transaction.CommitCount);
    }

    [TestMethod]
    public async Task GraphTransaction_CommitAsync_On_Nested_Does_Not_Commit_DbTransaction()
    {
        var connection = new TxFakeDbConnection();
        var transaction = new TxFakeDbTransaction(connection);
        var context = new TxGraphContext(new TxFakeConnectionFactory(connection));
        await using var graphTransaction = new GraphTransaction(context, connection, transaction, savepointName: "sp1", ownsTransaction: false);

        await graphTransaction.CommitAsync(TestContext.CancellationToken);

        Assert.IsTrue(graphTransaction.IsNested);
        Assert.AreEqual(0, transaction.CommitCount);
    }

    [TestMethod]
    public async Task GraphTransaction_RollbackAsync_On_Root_Rolls_Back_DbTransaction()
    {
        var connection = new TxFakeDbConnection();
        var transaction = new TxFakeDbTransaction(connection);
        var context = new TxGraphContext(new TxFakeConnectionFactory(connection));
        await using var graphTransaction = new GraphTransaction(context, connection, transaction, savepointName: null, ownsTransaction: false);

        await graphTransaction.RollbackAsync(TestContext.CancellationToken);

        Assert.IsTrue(graphTransaction.IsCompleted);
        Assert.AreEqual(1, transaction.RollbackCount);
    }

    [TestMethod]
    public async Task GraphTransaction_RollbackAsync_On_Nested_Uses_Savepoint_Command()
    {
        var connection = new TxFakeDbConnection();
        var transaction = new TxFakeDbTransaction(connection);
        var context = new TxGraphContext(new TxFakeConnectionFactory(connection));
        await using var graphTransaction = new GraphTransaction(context, connection, transaction, savepointName: "sp1", ownsTransaction: false);

        await graphTransaction.RollbackAsync(TestContext.CancellationToken);

        Assert.Contains("ROLLBACK TRANSACTION [sp1]", connection.ExecutedCommandTexts);
    }

    [TestMethod]
    public async Task GraphTransaction_CreateSavepointAsync_Uses_Generated_Name_When_Null()
    {
        var connection = new TxFakeDbConnection();
        var transaction = new TxFakeDbTransaction(connection);
        var context = new TxGraphContext(new TxFakeConnectionFactory(connection));
        await using var graphTransaction = new GraphTransaction(context, connection, transaction, savepointName: null, ownsTransaction: false);

        var name = await graphTransaction.CreateSavepointAsync(null, TestContext.CancellationToken);

        Assert.IsTrue(name.StartsWith("gorm_sp_", StringComparison.Ordinal));
        Assert.Contains($"SAVE TRANSACTION [{name}]", connection.ExecutedCommandTexts);
    }

    [TestMethod]
    public async Task GraphTransaction_RollbackToSavepointAsync_SavepointContainsClosingBracket_EscapesIdentifier()
    {
        var connection = new TxFakeDbConnection();
        var transaction = new TxFakeDbTransaction(connection);
        var context = new TxGraphContext(new TxFakeConnectionFactory(connection));
        await using var graphTransaction = new GraphTransaction(context, connection, transaction, savepointName: null, ownsTransaction: false);

        await graphTransaction.RollbackToSavepointAsync("sp]1", TestContext.CancellationToken);

        Assert.Contains("ROLLBACK TRANSACTION [sp]]1]", connection.ExecutedCommandTexts);
    }

    [TestMethod]
    public async Task GraphTransaction_RollbackToSavepointAsync_Throws_For_Whitespace_Name()
    {
        var connection = new TxFakeDbConnection();
        var transaction = new TxFakeDbTransaction(connection);
        var context = new TxGraphContext(new TxFakeConnectionFactory(connection));
        await using var graphTransaction = new GraphTransaction(context, connection, transaction, savepointName: null, ownsTransaction: false);

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => graphTransaction.RollbackToSavepointAsync(" ", TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task GraphTransaction_BeginTransactionAsync_Returns_New_Transaction()
    {
        var connection = new TxFakeDbConnection();
        var context = new TxGraphContext(new TxFakeConnectionFactory(connection));
        await using var graphTransaction = new GraphTransaction(context, connection, new TxFakeDbTransaction(connection), savepointName: null, ownsTransaction: false);

        await using var nested = await graphTransaction.BeginTransactionAsync(TestContext.CancellationToken);

        Assert.IsNotNull(nested);
    }

    [TestMethod]
    public async Task BeginTransactionAsync_WhenOpeningConnectionFails_DisposesConnection()
    {
        var connection = new TxFakeDbConnection { OpenException = new InvalidOperationException("Open failed.") };
        var context = new TxGraphContext(new TxFakeConnectionFactory(connection));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => context.BeginTransactionAsync(TestContext.CancellationToken));

        Assert.AreEqual(1, connection.DisposeAsyncCount);
    }

    [TestMethod]
    public async Task BeginTransactionAsync_WhenBeginningTransactionFails_DisposesConnection()
    {
        var connection = new TxFakeDbConnection { BeginTransactionException = new InvalidOperationException("Begin failed.") };
        var context = new TxGraphContext(new TxFakeConnectionFactory(connection));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => context.BeginTransactionAsync(TestContext.CancellationToken));

        Assert.AreEqual(1, connection.DisposeAsyncCount);
    }

    [TestMethod]
    public async Task GraphQueryCommandLease_OpenAsync_WhenOpeningConnectionFails_DisposesConnection()
    {
        var connection = new TxFakeDbConnection { OpenException = new InvalidOperationException("Open failed.") };
        var factory = new TxFakeConnectionFactory(connection);
        var context = new TxGraphContext(factory);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            GraphQueryCommandLease.OpenAsync(context, factory, TestContext.CancellationToken).AsTask());

        Assert.AreEqual(1, connection.DisposeAsyncCount);
    }

    [TestMethod]
    public async Task GraphTransaction_DisposeAsync_Rolls_Back_When_Not_Completed()
    {
        var connection = new TxFakeDbConnection();
        var transaction = new TxFakeDbTransaction(connection);
        var context = new TxGraphContext(new TxFakeConnectionFactory(connection));
        var graphTransaction = new GraphTransaction(context, connection, transaction, savepointName: null, ownsTransaction: false);

        await graphTransaction.DisposeAsync();

        Assert.AreEqual(1, transaction.RollbackCount);
    }

    [TestMethod]
    public async Task GraphTransaction_DisposeAsync_Does_Not_Roll_Back_After_Commit()
    {
        var connection = new TxFakeDbConnection();
        var transaction = new TxFakeDbTransaction(connection);
        var context = new TxGraphContext(new TxFakeConnectionFactory(connection));
        var graphTransaction = new GraphTransaction(context, connection, transaction, savepointName: null, ownsTransaction: false);

        await graphTransaction.CommitAsync(TestContext.CancellationToken);
        await graphTransaction.DisposeAsync();

        Assert.AreEqual(0, transaction.RollbackCount);
    }

    [TestMethod]
    public async Task GraphTransaction_CommitAsync_Throws_When_Already_Completed()
    {
        var connection = new TxFakeDbConnection();
        var transaction = new TxFakeDbTransaction(connection);
        var context = new TxGraphContext(new TxFakeConnectionFactory(connection));
        await using var graphTransaction = new GraphTransaction(context, connection, transaction, savepointName: null, ownsTransaction: false);

        await graphTransaction.CommitAsync(TestContext.CancellationToken);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => graphTransaction.CommitAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task GraphTransaction_RollbackAsync_Throws_When_Already_Completed()
    {
        var connection = new TxFakeDbConnection();
        var transaction = new TxFakeDbTransaction(connection);
        var context = new TxGraphContext(new TxFakeConnectionFactory(connection));
        await using var graphTransaction = new GraphTransaction(context, connection, transaction, savepointName: null, ownsTransaction: false);

        await graphTransaction.CommitAsync(TestContext.CancellationToken);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => graphTransaction.RollbackAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task GraphTransaction_CommitAsync_Throws_When_Disposed()
    {
        var connection = new TxFakeDbConnection();
        var transaction = new TxFakeDbTransaction(connection);
        var context = new TxGraphContext(new TxFakeConnectionFactory(connection));
        var graphTransaction = new GraphTransaction(context, connection, transaction, savepointName: null, ownsTransaction: false);

        await graphTransaction.DisposeAsync();

        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => graphTransaction.CommitAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task GraphTransaction_RollbackToSavepointAsync_Throws_When_Disposed()
    {
        var connection = new TxFakeDbConnection();
        var transaction = new TxFakeDbTransaction(connection);
        var context = new TxGraphContext(new TxFakeConnectionFactory(connection));
        var graphTransaction = new GraphTransaction(context, connection, transaction, savepointName: null, ownsTransaction: false);

        await graphTransaction.DisposeAsync();

        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => graphTransaction.RollbackToSavepointAsync("sp1", TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task GraphTransaction_ExecuteSavepointAsync_Throws_For_Null_Connection()
    {
        var connection = new TxFakeDbConnection();
        var transaction = new TxFakeDbTransaction(connection);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => GraphTransaction.ExecuteSavepointAsync(null!, transaction, "sp1", CancellationToken.None));
    }

    [TestMethod]
    public async Task GraphTransaction_ExecuteSavepointAsync_Throws_For_Null_Transaction()
    {
        var connection = new TxFakeDbConnection();

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => GraphTransaction.ExecuteSavepointAsync(connection, null!, "sp1", CancellationToken.None));
    }

    [TestMethod]
    public async Task GraphTransaction_ExecuteSavepointAsync_Throws_For_Whitespace_Savepoint()
    {
        var connection = new TxFakeDbConnection();
        var transaction = new TxFakeDbTransaction(connection);

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => GraphTransaction.ExecuteSavepointAsync(connection, transaction, " ", CancellationToken.None));
    }

    [TestMethod]
    public async Task GraphTransaction_DisposeAsync_When_OwnsTransaction_Disposes_Connection_And_Transaction()
    {
        var connection = new TxFakeDbConnection();
        var transaction = new TxFakeDbTransaction(connection);
        var context = new TxGraphContext(new TxFakeConnectionFactory(connection));
        var graphTransaction = new GraphTransaction(context, connection, transaction, savepointName: null, ownsTransaction: true);

        await graphTransaction.CommitAsync(TestContext.CancellationToken);
        await graphTransaction.DisposeAsync();

        Assert.AreEqual(1, transaction.DisposeAsyncCount);
        Assert.AreEqual(1, connection.DisposeAsyncCount);
    }

    private sealed class TxGraphContext : GraphContext
    {
        public TxGraphContext(IGormDbConnectionFactory factory)
            : base(factory)
        {
        }

        protected override void OnModelCreating(GraphModelBuilder modelBuilder)
        {
        }
    }

    private sealed class TxFakeConnectionFactory : IGormDbConnectionFactory
    {
        private readonly DbConnection _connection;

        public TxFakeConnectionFactory(DbConnection connection)
        {
            _connection = connection;
        }

        public DbConnection CreateConnection() => _connection;
    }

    private sealed class TxFakeDbConnection : DbConnection
    {
        private ConnectionState _state = ConnectionState.Closed;

        public List<string> ExecutedCommandTexts { get; } = [];
        public int DisposeAsyncCount { get; private set; }
        public Exception? OpenException { get; init; }
        public Exception? BeginTransactionException { get; init; }

        [AllowNull]
        public override string ConnectionString { get; set; } = "Fake";

        public override string Database => "Fake";

        public override string DataSource => "Fake";

        public override string ServerVersion => "1";

        public override ConnectionState State => _state;

        public override void ChangeDatabase(string databaseName)
        {
        }

        public override void Close() => _state = ConnectionState.Closed;

        public override void Open() => _state = ConnectionState.Open;

        public override Task OpenAsync(CancellationToken cancellationToken)
        {
            if (OpenException is not null)
            {
                return Task.FromException(OpenException);
            }

            _state = ConnectionState.Open;
            return Task.CompletedTask;
        }

        public override ValueTask DisposeAsync()
        {
            DisposeAsyncCount++;
            return ValueTask.CompletedTask;
        }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
            BeginTransactionException is null
                ? new TxFakeDbTransaction(this)
                : throw BeginTransactionException;

        protected override DbCommand CreateDbCommand() => new TxFakeDbCommand(this);
    }

    private sealed class TxFakeDbTransaction : DbTransaction
    {
        private readonly DbConnection _connection;

        public TxFakeDbTransaction(DbConnection connection)
        {
            _connection = connection;
        }

        public int CommitCount { get; private set; }

        public int RollbackCount { get; private set; }
        public int DisposeAsyncCount { get; private set; }

        public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;

        protected override DbConnection? DbConnection => _connection;

        public override void Commit() => CommitCount++;

        public override void Rollback() => RollbackCount++;

        public override Task CommitAsync(CancellationToken cancellationToken = default)
        {
            CommitCount++;
            return Task.CompletedTask;
        }

        public override Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            RollbackCount++;
            return Task.CompletedTask;
        }

        public override ValueTask DisposeAsync()
        {
            DisposeAsyncCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TxFakeDbCommand : DbCommand
    {
        private readonly TxFakeDbConnection _connection;
        private readonly TxFakeDbParameterCollection _parameters = new();

        public TxFakeDbCommand(TxFakeDbConnection connection)
        {
            _connection = connection;
        }

        [AllowNull]
        public override string CommandText { get; set; }

        public override int CommandTimeout { get; set; }

        public override CommandType CommandType { get; set; }

        public override bool DesignTimeVisible { get; set; }

        public override UpdateRowSource UpdatedRowSource { get; set; }

        protected override DbConnection? DbConnection
        {
            get => _connection;
            set => _ = value;
        }

        protected override DbParameterCollection DbParameterCollection => _parameters;

        protected override DbTransaction? DbTransaction { get; set; }

        public override void Cancel()
        {
        }

        public override int ExecuteNonQuery()
        {
            _connection.ExecutedCommandTexts.Add(CommandText);
            return 1;
        }

        public override object? ExecuteScalar() => 1;

        public override void Prepare()
        {
        }

        protected override DbParameter CreateDbParameter() => new TxFakeDbParameter();

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotSupportedException();

        public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
        {
            _connection.ExecutedCommandTexts.Add(CommandText);
            return Task.FromResult(1);
        }
    }

    private sealed class TxFakeDbParameter : DbParameter
    {
        public override DbType DbType { get; set; }
        public override ParameterDirection Direction { get; set; }
        public override bool IsNullable { get; set; }

        [AllowNull]
        public override string ParameterName { get; set; } = string.Empty;

        [AllowNull]
        public override string SourceColumn { get; set; } = string.Empty;

        public override object? Value { get; set; }
        public override bool SourceColumnNullMapping { get; set; }
        public override int Size { get; set; }
        public override void ResetDbType()
        { }
    }

    private sealed class TxFakeDbParameterCollection : DbParameterCollection
    {
        private readonly List<DbParameter> _items = [];
        public override int Count => _items.Count;
        public override object SyncRoot => ((ICollection)_items).SyncRoot;
        public override int Add(object value)
        { _items.Add((DbParameter)value); return _items.Count - 1; }
        public override void AddRange(Array values)
        { foreach (var value in values) { Add(value); } }
        public override void Clear() => _items.Clear();
        public override bool Contains(object value) => _items.Contains((DbParameter)value);
        public override bool Contains(string value) => _items.Any(x => x.ParameterName == value);
        public override void CopyTo(Array array, int index) => _items.ToArray().CopyTo(array, index);
        public override IEnumerator GetEnumerator() => _items.GetEnumerator();
        public override int IndexOf(object value) => _items.IndexOf((DbParameter)value);
        public override int IndexOf(string parameterName) => _items.FindIndex(x => x.ParameterName == parameterName);
        public override void Insert(int index, object value) => _items.Insert(index, (DbParameter)value);
        public override void Remove(object value) => _items.Remove((DbParameter)value);
        public override void RemoveAt(int index) => _items.RemoveAt(index);
        public override void RemoveAt(string parameterName)
        { var i = IndexOf(parameterName); if (i >= 0) { _items.RemoveAt(i); } }
        protected override DbParameter GetParameter(int index) => _items[index];
        protected override DbParameter GetParameter(string parameterName) => _items[IndexOf(parameterName)];
        protected override void SetParameter(int index, DbParameter value) => _items[index] = value;
        protected override void SetParameter(string parameterName, DbParameter value)
        {
            var index = IndexOf(parameterName);
            if (index >= 0)
            { _items[index] = value; }
            else
            { _items.Add(value); }
        }
    }
}