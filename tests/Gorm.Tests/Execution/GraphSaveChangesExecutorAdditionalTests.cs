using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using Gorm.Application.Context;
using Gorm.Core.Configuration;
using Gorm.Core.Primitives;
using Gorm.Infrastructure.Persistence.Connections;
using Gorm.Infrastructure.Providers.SqlServer;

namespace Gorm.Tests.Execution;

[TestClass]
public sealed class GraphSaveChangesExecutorAdditionalTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task SaveChangesAsync_Throws_For_Standalone_Added_Edge()
    {
        var connection = new ExecutorFakeConnection();
        var context = new ExecutorGraphContext(new ExecutorFactory(connection));
        var edge = new ExecutorWorksOnEdge { Id = Guid.NewGuid(), Weight = 1 };

        context.Add(edge);

        await Assert.ThrowsExactlyAsync<NotSupportedException>(() => context.SaveChangesAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task SaveChangesAsync_AddEdge_Generates_Id_And_Persists_Connection()
    {
        var connection = new ExecutorFakeConnection();
        var context = new ExecutorGraphContext(new ExecutorFactory(connection));

        var from = new ExecutorPersonNode { Id = Guid.NewGuid(), Name = "Alice" };
        var to = new ExecutorPersonNode { Id = Guid.NewGuid(), Name = "Project" };
        context.Attach(from);
        context.Attach(to);

        var edge = new ExecutorWorksOnEdge { Id = Guid.Empty, Weight = 2 };

        context.AddEdge<ExecutorWorksOnEdge, ExecutorPersonNode, ExecutorPersonNode>(from, to, edge);

        var affected = await context.SaveChangesAsync(TestContext.CancellationToken);

        Assert.AreNotEqual(Guid.Empty, edge.Id);
        Assert.IsGreaterThan(0, affected);
        Assert.IsTrue(connection.ExecutedCommandTexts.Any(x => x.Contains("INSERT INTO [dbo].[WorksOn]", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public async Task SaveChangesAsync_Disconnect_Issues_Delete_By_NodeIds()
    {
        var connection = new ExecutorFakeConnection();
        var context = new ExecutorGraphContext(new ExecutorFactory(connection));

        var from = new ExecutorPersonNode { Id = Guid.NewGuid(), Name = "A" };
        var to = new ExecutorPersonNode { Id = Guid.NewGuid(), Name = "B" };
        context.Attach(from);
        context.Attach(to);

        context.Disconnect<ExecutorWorksOnEdge, ExecutorPersonNode, ExecutorPersonNode>(from, to);

        var affected = await context.SaveChangesAsync(TestContext.CancellationToken);

        Assert.IsGreaterThan(0, affected);
        Assert.IsTrue(connection.ExecutedCommandTexts.Any(x => x.Contains("DELETE FROM [dbo].[WorksOn]", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public async Task SaveChangesAsync_SqlHistoryRecorder_Uses_Graph_Connection()
    {
        var connection = new ExecutorFakeConnection();
        var context = new ExecutorGraphContext(new ExecutorFactory(connection));
        context.UseHistoryRecorder(new SqlServerGraphHistoryRecorder(new ThrowingConnectionFactory()));
        context.Add(new ExecutorPersonNode { Id = Guid.NewGuid(), Name = "Alice" });

        await context.SaveChangesAsync(TestContext.CancellationToken);

        var historyCommandIndex = connection.ExecutedCommandTexts.FindIndex(command =>
            command.Contains("[GormNodeHistory]", StringComparison.OrdinalIgnoreCase));
        Assert.IsGreaterThanOrEqualTo(0, historyCommandIndex);
        Assert.AreSame(connection.LastTransaction, connection.ExecutedTransactions[historyCommandIndex]);
    }

    private sealed class ExecutorGraphContext : GraphContext
    {
        public ExecutorGraphContext(IGormDbConnectionFactory connectionFactory)
            : base(connectionFactory)
        {
        }

        protected override void OnModelCreating(GraphModelBuilder modelBuilder)
        {
            modelBuilder.Node<ExecutorPersonNode>(node =>
            {
                node.ToTable("Person");
                node.HasKey(x => x.Id);
                node.Property(x => x.Name).HasMaxLength(100);
            });

            modelBuilder.Edge<ExecutorWorksOnEdge>(edge =>
            {
                edge.ToTable("WorksOn");
                edge.HasKey(x => x.Id);
                edge.From<ExecutorPersonNode>();
                edge.To<ExecutorPersonNode>();
                edge.Property(x => x.Weight);
            });
        }
    }

    private sealed class ExecutorPersonNode : Node
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class ExecutorWorksOnEdge : Edge
    {
        public int Weight { get; set; }
    }

    private sealed class ExecutorFactory : IGormDbConnectionFactory
    {
        private readonly DbConnection _connection;

        public ExecutorFactory(DbConnection connection)
        {
            _connection = connection;
        }

        public DbConnection CreateConnection() => _connection;
    }

    private sealed class ThrowingConnectionFactory : IGormDbConnectionFactory
    {
        public DbConnection CreateConnection() => throw new InvalidOperationException("The history recorder must reuse the graph connection.");
    }

    private sealed class ExecutorFakeConnection : DbConnection
    {
        private ConnectionState _state = ConnectionState.Closed;

        public List<string> ExecutedCommandTexts { get; } = [];
        public List<DbTransaction?> ExecutedTransactions { get; } = [];
        public DbTransaction? LastTransaction { get; private set; }

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

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
            LastTransaction = new ExecutorFakeTransaction(this);

        protected override DbCommand CreateDbCommand() => new ExecutorFakeCommand(this);
    }

    private sealed class ExecutorFakeTransaction : DbTransaction
    {
        private readonly DbConnection _connection;

        public ExecutorFakeTransaction(DbConnection connection)
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

    private sealed class ExecutorFakeCommand : DbCommand
    {
        private readonly ExecutorFakeParameterCollection _parameters = new();
        private readonly ExecutorFakeConnection _connection;

        public ExecutorFakeCommand(ExecutorFakeConnection connection)
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

        public override int ExecuteNonQuery()
        {
            _connection.ExecutedCommandTexts.Add(CommandText);
            _connection.ExecutedTransactions.Add(DbTransaction);
            return 1;
        }

        public override object? ExecuteScalar()
        {
            _connection.ExecutedCommandTexts.Add(CommandText);
            _connection.ExecutedTransactions.Add(DbTransaction);
            return 1;
        }

        public override void Prepare()
        { }
        protected override DbParameter CreateDbParameter() => new ExecutorFakeParameter();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotSupportedException();

        public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
        {
            _connection.ExecutedCommandTexts.Add(CommandText);
            _connection.ExecutedTransactions.Add(DbTransaction);
            return Task.FromResult(1);
        }

        public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken)
        {
            _connection.ExecutedCommandTexts.Add(CommandText);
            _connection.ExecutedTransactions.Add(DbTransaction);
            return Task.FromResult<object?>(1);
        }
    }

    private sealed class ExecutorFakeParameter : DbParameter
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

    private sealed class ExecutorFakeParameterCollection : DbParameterCollection
    {
        private readonly List<DbParameter> _items = [];
        public override int Count => _items.Count;
        public override object SyncRoot => ((ICollection)_items).SyncRoot;
        public override int Add(object value)
        { _items.Add((DbParameter)value); return _items.Count - 1; }

        public override void AddRange(Array values)
        {
            foreach (var value in values)
            {
                Add(value);
            }
        }

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
        {
            var i = IndexOf(parameterName);
            if (i >= 0)
            {
                _items.RemoveAt(i);
            }
        }

        protected override DbParameter GetParameter(int index) => _items[index];
        protected override DbParameter GetParameter(string parameterName) => _items[IndexOf(parameterName)];
        protected override void SetParameter(int index, DbParameter value) => _items[index] = value;

        protected override void SetParameter(string parameterName, DbParameter value)
        {
            var i = IndexOf(parameterName);
            if (i >= 0)
            {
                _items[i] = value;
            }
            else
            {
                _items.Add(value);
            }
        }
    }
}