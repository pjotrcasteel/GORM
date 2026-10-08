using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using Gorm.Application.Context;
using Gorm.Application.Execution;
using Gorm.Application.Tracking;
using Gorm.Core.Configuration;
using Gorm.Infrastructure.Persistence.Connections;
using Gorm.Tests.TestSupport;

namespace Gorm.Tests.Execution;

[TestClass]
public sealed class SqlServerGraphExecutionEngineTests
{
    [TestMethod]
    public async Task ExecuteAsync_Throws_For_Null_Context()
    {
        var engine = new SqlServerGraphExecutionEngine();
        var query = new List<int>().AsQueryable();
        var request = new GraphQueryExecutionRequest { Mode = GraphQueryExecutionMode.List };

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => engine.ExecuteAsync<int>(null!, query, request, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ExecuteAsync_Throws_When_No_ConnectionFactory()
    {
        var engine = new SqlServerGraphExecutionEngine();
        var ctx = new TestGraphContext();
        var request = new GraphQueryExecutionRequest { Mode = GraphQueryExecutionMode.List };

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => engine.ExecuteAsync(ctx, ctx.People, request, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ExecuteAsync_Throws_For_NonGraphQueryProvider_When_ConnectionFactory_Configured()
    {
        var engine = new SqlServerGraphExecutionEngine();
        var ctx = new TestGraphContext();
        ctx.UseConnectionFactory(new EngineConnectionFactory(new EngineFakeConnection()));
        var request = new GraphQueryExecutionRequest { Mode = GraphQueryExecutionMode.List };

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => engine.ExecuteAsync(ctx, new List<int>().AsQueryable(), request, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ExecuteListAsync_Throws_When_No_ConnectionFactory()
    {
        var engine = new SqlServerGraphExecutionEngine();
        var ctx = new TestGraphContext();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => engine.ExecuteListAsync(ctx, ctx.People, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ExecuteAnyAsync_Throws_When_No_ConnectionFactory()
    {
        var engine = new SqlServerGraphExecutionEngine();
        var ctx = new TestGraphContext();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => engine.ExecuteAnyAsync(ctx, ctx.People, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ExecuteCountAsync_Throws_When_No_ConnectionFactory()
    {
        var engine = new SqlServerGraphExecutionEngine();
        var ctx = new TestGraphContext();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => engine.ExecuteCountAsync(ctx, ctx.People, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ExecuteLongCountAsync_Throws_When_No_ConnectionFactory()
    {
        var engine = new SqlServerGraphExecutionEngine();
        var ctx = new TestGraphContext();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => engine.ExecuteLongCountAsync(ctx, ctx.People, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task SaveChangesAsync_Throws_For_Null_Context()
    {
        var engine = new SqlServerGraphExecutionEngine();
        var tracker = new GraphChangeTracker();

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => engine.SaveChangesAsync(null!, tracker, acceptAllChangesOnSuccess: true, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task SaveChangesAsync_Throws_For_Null_ChangeTracker()
    {
        var engine = new SqlServerGraphExecutionEngine();
        var ctx = new TestGraphContext();

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => engine.SaveChangesAsync(ctx, null!, acceptAllChangesOnSuccess: true, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task SaveChangesAsync_Throws_When_No_ConnectionFactory()
    {
        var engine = new SqlServerGraphExecutionEngine();
        var ctx = new TestGraphContext();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => engine.SaveChangesAsync(ctx, ctx.ChangeTracker, acceptAllChangesOnSuccess: true, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task SaveChangesAsync_Returns_Zero_When_No_Tracked_Changes()
    {
        var engine = new SqlServerGraphExecutionEngine();
        var ctx = new EmptyModelContext();
        ctx.UseConnectionFactory(new EngineConnectionFactory(new EngineFakeConnection()));

        var result = await engine.SaveChangesAsync(ctx, ctx.ChangeTracker, acceptAllChangesOnSuccess: true, TestContext.CancellationToken);

        Assert.AreEqual(0, result);
    }

    private sealed class EmptyModelContext : GraphContext
    {
        protected override void OnModelCreating(GraphModelBuilder modelBuilder)
        {
        }
    }

    private sealed class EngineConnectionFactory : IGormDbConnectionFactory
    {
        private readonly DbConnection _connection;

        public EngineConnectionFactory(DbConnection connection)
        {
            _connection = connection;
        }

        public DbConnection CreateConnection() => _connection;
    }

    private sealed class EngineFakeConnection : DbConnection
    {
        private ConnectionState _state = ConnectionState.Closed;

        [AllowNull]
        public override string ConnectionString { get; set; } = "Fake";
        public override string Database => "Fake";
        public override string DataSource => "Fake";
        public override string ServerVersion => "1";
        public override ConnectionState State => _state;

        public override void ChangeDatabase(string databaseName)
        { }
        public override void Close() => _state = ConnectionState.Closed;
        public override void Open() => _state = ConnectionState.Open;
        public override Task OpenAsync(CancellationToken cancellationToken)
        {
            _state = ConnectionState.Open;
            return Task.CompletedTask;
        }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => new EngineFakeTransaction(this);
        protected override DbCommand CreateDbCommand() => new EngineFakeCommand(this);
    }

    private sealed class EngineFakeTransaction : DbTransaction
    {
        private readonly DbConnection _connection;

        public EngineFakeTransaction(DbConnection connection)
        {
            _connection = connection;
        }

        public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
        protected override DbConnection DbConnection => _connection;
        public override void Commit()
        { }
        public override void Rollback()
        { }
        public override Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public override Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class EngineFakeCommand : DbCommand
    {
        private readonly DbConnection _connection;

        public EngineFakeCommand(DbConnection connection)
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

        protected override DbParameterCollection DbParameterCollection => new EngineFakeParameterCollection();
        protected override DbTransaction? DbTransaction { get; set; }

        public override void Cancel()
        { }
        public override int ExecuteNonQuery() => 0;
        public override object? ExecuteScalar() => 0;
        public override void Prepare()
        { }
        protected override DbParameter CreateDbParameter() => new EngineFakeParameter();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotSupportedException();
    }

    private sealed class EngineFakeParameter : DbParameter
    {
        public override DbType DbType { get; set; }
        public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;
        public override bool IsNullable { get; set; }
        [AllowNull] public override string ParameterName { get; set; } = string.Empty;
        [AllowNull] public override string SourceColumn { get; set; } = string.Empty;
        public override object? Value { get; set; }
        public override bool SourceColumnNullMapping { get; set; }
        public override int Size { get; set; }
        public override void ResetDbType()
        { }
    }

    private sealed class EngineFakeParameterCollection : DbParameterCollection
    {
        public override int Count => 0;
        public override object SyncRoot => this;
        public override int Add(object value) => 0;
        public override void AddRange(Array values)
        { }
        public override void Clear()
        { }
        public override bool Contains(object value) => false;
        public override bool Contains(string value) => false;
        public override void CopyTo(Array array, int index)
        { }
        public override IEnumerator GetEnumerator() => Array.Empty<object>().GetEnumerator();
        public override int IndexOf(object value) => -1;
        public override int IndexOf(string parameterName) => -1;
        public override void Insert(int index, object value)
        { }
        public override void Remove(object value)
        { }
        public override void RemoveAt(int index)
        { }
        public override void RemoveAt(string parameterName)
        { }
        protected override DbParameter GetParameter(int index) => throw new IndexOutOfRangeException();
        protected override DbParameter GetParameter(string parameterName) => throw new IndexOutOfRangeException();
        protected override void SetParameter(int index, DbParameter value)
        { }
        protected override void SetParameter(string parameterName, DbParameter value)
        { }
    }

    public TestContext TestContext { get; set; }
}