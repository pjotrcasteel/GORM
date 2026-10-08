using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using Gorm.Application.Context;
using Gorm.Application.History.Abstractions;
using Gorm.Application.History.Envelopes;
using Gorm.Application.Tracking;
using Gorm.Core.Configuration;
using Gorm.Core.Primitives;
using Gorm.Infrastructure.Persistence.Connections;
using Gorm;

namespace Gorm.Tests.Execution;

[TestClass]
public sealed class GraphSaveChangesExecutorBehaviorTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task SaveChanges_With_AcceptAllChangesOnSuccess_False_Keeps_Entry_State_Intact()
    {
        var connection = new FakeDbConnection();
        var factory = new FakeConnectionFactory(connection);
        var context = new SaveGraphContext(factory);
        var person = new SavePerson { Id = Guid.NewGuid(), Name = "Alice" };

        context.Add(person);

        await context.SaveChangesAsync(acceptAllChangesOnSuccess: false, cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(EntityState.Added, context.Entry(person).State);
    }

    [TestMethod]
    public async Task SaveChanges_With_Batch_History_Uses_Graph_Transaction()
    {
        var connection = new FakeDbConnection();
        var context = new SaveGraphContext(new FakeConnectionFactory(connection));
        var recorder = new RecordingBatchHistoryRecorder();
        context.UseHistoryRecorder(recorder);
        context.Add(new SavePerson { Id = Guid.NewGuid(), Name = "Alice" });

        await context.SaveChangesAsync(TestContext.CancellationToken);

        Assert.AreEqual(0, recorder.CaptureCount);
        Assert.AreEqual(1, recorder.PersistCount);
        Assert.AreSame(connection.LastTransaction, recorder.Transaction);
        Assert.HasCount(1, recorder.Envelopes);
    }

    [TestMethod]
    public async Task SaveChanges_When_Batch_History_Fails_Rolls_Back_Graph_Transaction()
    {
        var connection = new FakeDbConnection();
        var context = new SaveGraphContext(new FakeConnectionFactory(connection));
        var recorder = new RecordingBatchHistoryRecorder { PersistException = new InvalidOperationException("History failed.") };
        context.UseHistoryRecorder(recorder);
        context.Add(new SavePerson { Id = Guid.NewGuid(), Name = "Alice" });

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => context.SaveChangesAsync(TestContext.CancellationToken));

        Assert.AreEqual(1, connection.LastTransaction!.RollbackAsyncCount);
    }

    [TestMethod]
    public async Task SaveChanges_In_Explicit_Transaction_Uses_Active_Transaction_For_Batch_History()
    {
        var connection = new FakeDbConnection();
        var context = new SaveGraphContext(new FakeConnectionFactory(connection));
        var recorder = new RecordingBatchHistoryRecorder();
        context.UseHistoryRecorder(recorder);

        await using var transaction = await context.BeginTransactionAsync(TestContext.CancellationToken);
        context.Add(new SavePerson { Id = Guid.NewGuid(), Name = "Alice" });
        await context.SaveChangesAsync(TestContext.CancellationToken);

        Assert.AreSame(connection.LastTransaction, recorder.Transaction);
        Assert.AreEqual(0, connection.LastTransaction!.CommitAsyncCount);

        await transaction.RollbackAsync(TestContext.CancellationToken);
    }

    private sealed class FakeConnectionFactory : IGormDbConnectionFactory
    {
        private readonly DbConnection _connection;

        public FakeConnectionFactory(DbConnection connection)
        {
            _connection = connection;
        }

        public DbConnection CreateConnection() => _connection;
    }

    private sealed class SaveGraphContext : GraphContext
    {
        public SaveGraphContext(IGormDbConnectionFactory connectionFactory)
            : base(connectionFactory)
        {
        }

        protected override void OnModelCreating(GraphModelBuilder modelBuilder) => modelBuilder.Node<SavePerson>(node =>
                                                                                                          {
                                                                                                              node.ToTable("Person");
                                                                                                              node.HasKey(x => x.Id);
                                                                                                              node.Property(x => x.Name).HasMaxLength(200);
                                                                                                          });
    }

    private sealed class SavePerson : Node
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class FakeDbConnection : DbConnection
    {
        private ConnectionState _state = ConnectionState.Closed;

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
            _state = ConnectionState.Open;
            return Task.CompletedTask;
        }

        public FakeDbTransaction? LastTransaction { get; private set; }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
            LastTransaction = new FakeDbTransaction(this);

        protected override DbCommand CreateDbCommand() => new FakeDbCommand(this);
    }

    private sealed class FakeDbTransaction : DbTransaction
    {
        private readonly DbConnection _connection;

        public FakeDbTransaction(DbConnection connection)
        {
            _connection = connection;
        }

        public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
        protected override DbConnection DbConnection => _connection;
        public int CommitAsyncCount { get; private set; }
        public int RollbackAsyncCount { get; private set; }
        public override void Commit()
        { }
        public override void Rollback()
        { }
        public override Task CommitAsync(CancellationToken cancellationToken = default)
        {
            CommitAsyncCount++;
            return Task.CompletedTask;
        }
        public override Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            RollbackAsyncCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDbCommand : DbCommand
    {
        private readonly FakeDbParameterCollection _parameters = new();
        private readonly DbConnection _connection;

        public FakeDbCommand(DbConnection connection)
        {
            _connection = connection;
        }

        [AllowNull]
        public override string CommandText { get; set; } = string.Empty;
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; }
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }

        [AllowNull]
        protected override DbConnection DbConnection
        {
            get => _connection;
            set => _ = value;
        }
        protected override DbParameterCollection DbParameterCollection => _parameters;
        protected override DbTransaction? DbTransaction { get; set; }

        public override void Cancel()
        { }
        public override int ExecuteNonQuery() => 1;
        public override object? ExecuteScalar() => 1;
        public override void Prepare()
        { }
        protected override DbParameter CreateDbParameter() => new FakeDbParameter();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => new FakeDbDataReader();
        public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken) => Task.FromResult(1);
        public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken) => Task.FromResult<object?>(1);
        protected override Task<DbDataReader> ExecuteDbDataReaderAsync(
            CommandBehavior behavior,
            CancellationToken cancellationToken
        ) => Task.FromResult<DbDataReader>(new FakeDbDataReader());
    }

    private sealed class FakeDbParameter : DbParameter
    {
        public override DbType DbType { get; set; }
        public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;
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

    private sealed class FakeDbParameterCollection : DbParameterCollection
    {
        private readonly List<DbParameter> _items = [];
        public override int Count => _items.Count;
        public override object SyncRoot => ((ICollection)_items).SyncRoot.EnsureNotNull();
        public override int Add(object value)
        { _items.Add((DbParameter)value); return _items.Count - 1; }
        public override void AddRange(Array values)
        { foreach (var value in values) { Add(value.EnsureNotNull()); } }
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
        { var index = IndexOf(parameterName); if (index >= 0) { _items.RemoveAt(index); } }
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

    private sealed class FakeDbDataReader : DbDataReader
    {
        public override int FieldCount => 0;
        public override bool HasRows => false;
        public override bool IsClosed => false;
        public override int RecordsAffected => 0;
        public override int Depth => 0;
        public override object this[int ordinal] => throw new NotSupportedException();
        public override object this[string name] => throw new NotSupportedException();
        public override bool GetBoolean(int ordinal) => throw new NotSupportedException();
        public override byte GetByte(int ordinal) => throw new NotSupportedException();
        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();
        public override char GetChar(int ordinal) => throw new NotSupportedException();
        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();
        public override string GetDataTypeName(int ordinal) => string.Empty;
        public override DateTime GetDateTime(int ordinal) => throw new NotSupportedException();
        public override decimal GetDecimal(int ordinal) => throw new NotSupportedException();
        public override double GetDouble(int ordinal) => throw new NotSupportedException();
        public override Type GetFieldType(int ordinal) => typeof(object);
        public override float GetFloat(int ordinal) => throw new NotSupportedException();
        public override Guid GetGuid(int ordinal) => throw new NotSupportedException();
        public override short GetInt16(int ordinal) => throw new NotSupportedException();
        public override int GetInt32(int ordinal) => throw new NotSupportedException();
        public override long GetInt64(int ordinal) => throw new NotSupportedException();
        public override string GetName(int ordinal) => string.Empty;
        public override int GetOrdinal(string name) => -1;
        public override string GetString(int ordinal) => throw new NotSupportedException();
        public override object GetValue(int ordinal) => throw new NotSupportedException();
        public override int GetValues(object[] values) => 0;
        public override bool IsDBNull(int ordinal) => true;
        public override bool NextResult() => false;
        public override bool Read() => false;
        public override Task<bool> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(false);
        public override Task<bool> NextResultAsync(CancellationToken cancellationToken) => Task.FromResult(false);
        public override IEnumerator GetEnumerator() => Array.Empty<object>().GetEnumerator();
    }

    private sealed class RecordingBatchHistoryRecorder : IGraphHistoryBatchRecorder
    {
        public int CaptureCount { get; private set; }
        public int PersistCount { get; private set; }
        public IReadOnlyList<GraphHistoryEnvelope> Envelopes { get; private set; } = [];
        public DbTransaction? Transaction { get; private set; }
        public Exception? PersistException { get; init; }

        public Task CaptureAsync(GraphContext context, GraphChangeTracker changeTracker, DateTime capturedAtUtc, CancellationToken cancellationToken)
        {
            CaptureCount++;
            return Task.CompletedTask;
        }

        public Task PersistAsync(IReadOnlyList<GraphHistoryEnvelope> envelopes, DbConnection? connection, DbTransaction? transaction, CancellationToken cancellationToken)
        {
            PersistCount++;
            Envelopes = envelopes;
            Transaction = transaction;

            return PersistException is null
                ? Task.CompletedTask
                : Task.FromException(PersistException);
        }
    }
}