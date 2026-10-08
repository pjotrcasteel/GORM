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
public sealed class GraphSaveChangesExecutorGuardTests
{
    [TestMethod]
    public void Constructor_Throws_For_Null_ConnectionFactory()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new GraphSaveChangesExecutor(null!));
    }

    [TestMethod]
    public async Task Instance_SaveChangesAsync_Throws_For_Null_Context()
    {
        var executor = new GraphSaveChangesExecutor(new GuardConnectionFactory(new GuardConnection()));
        var tracker = new GraphChangeTracker();

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => executor.SaveChangesAsync(null!, tracker, cancellationToken: TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task Instance_SaveChangesAsync_Throws_For_Null_Tracker()
    {
        var executor = new GraphSaveChangesExecutor(new GuardConnectionFactory(new GuardConnection()));
        var context = new GuardContext();

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => executor.SaveChangesAsync(context, null!, cancellationToken: TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task Static_SaveChangesAsync_Rolls_Back_When_OwnsTransaction_True_And_Failure_Occurs()
    {
        var context = new GuardContext();
        var edge = new TestWorksOn { Id = Guid.NewGuid() };
        context.Add(edge);
        var tracker = context.ChangeTracker;

        var connection = new GuardConnection();
        var transaction = (GuardTransaction)connection.BeginTransaction();

        await Assert.ThrowsExactlyAsync<NotSupportedException>(() =>
            GraphSaveChangesExecutor.SaveChangesAsync(
                new GraphSaveChangesExecutor.SaveChangesAsyncParameters
                {
                    Context = context,
                    ChangeTracker = tracker,
                    Connection = connection,
                    Transaction = transaction,
                    OwnsTransaction = true,
                    CancellationToken = default
                }
            ));

        Assert.AreEqual(1, transaction.RollbackAsyncCount);
    }

    [TestMethod]
    public async Task Static_SaveChangesAsync_Does_Not_Roll_Back_When_OwnsTransaction_False_And_Failure_Occurs()
    {
        var context = new GuardContext();
        var edge = new TestWorksOn { Id = Guid.NewGuid() };
        context.Add(edge);
        var tracker = context.ChangeTracker;

        var connection = new GuardConnection();
        var transaction = (GuardTransaction)connection.BeginTransaction();

        await Assert.ThrowsExactlyAsync<NotSupportedException>(() =>
            GraphSaveChangesExecutor.SaveChangesAsync(
                new GraphSaveChangesExecutor.SaveChangesAsyncParameters
                {
                    Context = context,
                    ChangeTracker = tracker,
                    Connection = connection,
                    Transaction = transaction,
                    OwnsTransaction = false,
                    CancellationToken = default
                }
            ));

        Assert.AreEqual(0, transaction.RollbackAsyncCount);
    }

    [TestMethod]
    public async Task Static_SaveChangesAsync_Throws_For_Null_Context()
    {
        var context = new GuardContext();
        var tracker = context.ChangeTracker;
        var connection = new GuardConnection();
        var transaction = connection.BeginTransaction();

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() =>
            GraphSaveChangesExecutor.SaveChangesAsync(
                new GraphSaveChangesExecutor.SaveChangesAsyncParameters
                {
                    Context = null!,
                    ChangeTracker = tracker,
                    Connection = connection,
                    Transaction = transaction,
                    OwnsTransaction = false,
                    CancellationToken = default
                }
            ));
    }

    [TestMethod]
    public async Task Static_SaveChangesAsync_Throws_For_Null_ChangeTracker()
    {
        var context = new GuardContext();
        var connection = new GuardConnection();
        var transaction = connection.BeginTransaction();

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() =>
            GraphSaveChangesExecutor.SaveChangesAsync(
                new GraphSaveChangesExecutor.SaveChangesAsyncParameters
                {
                    Context = context,
                    ChangeTracker = null!,
                    Connection = connection,
                    Transaction = transaction,
                    OwnsTransaction = false,
                    CancellationToken = default
                }
            ));
    }

    [TestMethod]
    public async Task Static_SaveChangesAsync_Throws_For_Null_Connection()
    {
        var context = new GuardContext();
        var tracker = new GraphChangeTracker();
        var transaction = new GuardTransaction(new GuardConnection());

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() =>
            GraphSaveChangesExecutor.SaveChangesAsync(
                new GraphSaveChangesExecutor.SaveChangesAsyncParameters
                {
                    Context = context,
                    ChangeTracker = tracker,
                    Connection = null!,
                    Transaction = transaction,
                    OwnsTransaction = false,
                    CancellationToken = default
                }
            ));
    }

    [TestMethod]
    public async Task Static_SaveChangesAsync_Throws_For_Null_Transaction()
    {
        var context = new GuardContext();
        var tracker = new GraphChangeTracker();
        var connection = new GuardConnection();

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() =>
            GraphSaveChangesExecutor.SaveChangesAsync(
                new GraphSaveChangesExecutor.SaveChangesAsyncParameters
                {
                    Context = context,
                    ChangeTracker = tracker,
                    Connection = connection,
                    Transaction = null!,
                    OwnsTransaction = false,
                    CancellationToken = default
                }
            ));
    }

    private sealed class GuardContext : GraphContext
    {
        protected override void OnModelCreating(GraphModelBuilder modelBuilder)
        {
        }
    }

    private sealed class GuardConnectionFactory : IGormDbConnectionFactory
    {
        private readonly DbConnection _connection;

        public GuardConnectionFactory(DbConnection connection)
        {
            _connection = connection;
        }

        public DbConnection CreateConnection() => _connection;
    }

    private sealed class GuardConnection : DbConnection
    {
        [AllowNull]
        public override string ConnectionString { get; set; } = "Fake";
        public override string Database => "Fake";
        public override string DataSource => "Fake";
        public override string ServerVersion => "1";
        public override System.Data.ConnectionState State => System.Data.ConnectionState.Open;
        public override void ChangeDatabase(string databaseName)
        { }
        public override void Open()
        { }
        public override void Close()
        { }
        protected override DbTransaction BeginDbTransaction(System.Data.IsolationLevel isolationLevel) => new GuardTransaction(this);
        protected override DbCommand CreateDbCommand() => throw new NotSupportedException();
    }

    private sealed class GuardTransaction : DbTransaction
    {
        private readonly DbConnection _connection;
        public GuardTransaction(DbConnection connection) => _connection = connection;
        public int RollbackAsyncCount { get; private set; }
        public override System.Data.IsolationLevel IsolationLevel => System.Data.IsolationLevel.ReadCommitted;
        protected override DbConnection DbConnection => _connection;
        public override void Commit()
        { }
        public override void Rollback()
        { }
        public override Task RollbackAsync(CancellationToken cancellationToken)
        {
            RollbackAsyncCount++;
            return Task.CompletedTask;
        }
    }

    public TestContext TestContext { get; set; }
}