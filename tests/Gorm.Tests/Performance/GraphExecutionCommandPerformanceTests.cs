using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using Gorm.Application.Execution;
using Gorm.Application.Querying.Models;
using Gorm.Core.Models;
using Gorm.Infrastructure.Persistence.Connections;
using Gorm.Infrastructure.Providers.Abstractions;
using Gorm.Infrastructure.Sql;
using Gorm.Tests.TestSupport;

namespace Gorm.Tests.Performance;

[TestClass]
public sealed class GraphExecutionCommandPerformanceTests
{
    [TestMethod]
    public async Task ExecuteCountAsync_InsideActiveTransaction_ReusesTransactionConnection()
    {
        var connection = new RecordingConnection(7);
        var factory = new RecordingConnectionFactory(connection);
        var context = new TestGraphContext();

        context.UseConnectionFactory(factory);

        await using var transaction = await context.BeginTransactionAsync(TestContext.CancellationToken);

        var executor = new GraphQueryExecutor(factory, new CountSqlGenerator());

        var result = await executor.ExecuteCountAsync(context, context.People, TestContext.CancellationToken);

        Assert.AreEqual(7, result);
        Assert.AreEqual(1, factory.CreateConnectionCount);
        Assert.AreEqual(1, connection.OpenCount);
        Assert.HasCount(1, connection.Commands);
        Assert.AreSame(connection.ActiveTransaction, connection.Commands[0].Transaction);
    }

    [TestMethod]
    public void CreateCommand_WithParameters_AppliesParameterMetadataWithoutExtraSqlParameterCreation()
    {
        var connection = new RecordingConnection(0);

        var sql = new GraphSqlQuery
        {
            CommandText = "SELECT @p0, @p1"
        };

        sql.Parameters.Add(GraphSqlParameter.Create("@p0", "abc"));
        sql.Parameters.Add(GraphSqlParameter.Create("@p1", 42));

        using var command = GraphSqlCommandFactory.CreateCommand(connection, sql);

        Assert.AreEqual("SELECT @p0, @p1", command.CommandText);
        Assert.AreEqual(CommandType.Text, command.CommandType);
        Assert.HasCount(2, command.Parameters);

        var stringParameter = (RecordingParameter)command.Parameters[0];
        var intParameter = (RecordingParameter)command.Parameters[1];

        Assert.AreEqual("@p0", stringParameter.ParameterName);
        Assert.AreEqual(DbType.String, stringParameter.DbType);
        Assert.AreEqual(3, stringParameter.Size);
        Assert.AreEqual("abc", stringParameter.Value);

        Assert.AreEqual("@p1", intParameter.ParameterName);
        Assert.AreEqual(DbType.Int32, intParameter.DbType);
        Assert.AreEqual(42, intParameter.Value);
    }

    [TestMethod]
    public void AddParameter_DirectValue_InfersStringMetadata()
    {
        using var command = new RecordingCommand(new RecordingConnection(0));

        GraphSqlCommandFactory.AddParameter(command, "@p0", "hello");

        var parameter = (RecordingParameter)command.Parameters[0];

        Assert.AreEqual("@p0", parameter.ParameterName);
        Assert.AreEqual(DbType.String, parameter.DbType);
        Assert.AreEqual(5, parameter.Size);
        Assert.AreEqual("hello", parameter.Value);
    }

    private sealed class CountSqlGenerator : IGraphQuerySqlGenerator
    {
        public GraphSqlQuery Generate(GraphModel model, GraphQueryModel queryModel) => new() { CommandText = "SELECT 7" };
        public GraphSqlQuery Generate(GraphModel model, GraphQueryModel queryModel, int? effectiveTakeOverride) => new() { CommandText = "SELECT 7" };
        public GraphSqlQuery GenerateExists(GraphModel model, GraphQueryModel queryModel, int? effectiveTakeOverride) => new() { CommandText = "SELECT 7" };
        public GraphSqlQuery GenerateAggregate(GraphModel model, GraphQueryModel queryModel, GraphAggregateKind aggregateKind) => new() { CommandText = "SELECT 7" };
        public GraphSqlQuery GenerateCount(GraphModel model, GraphQueryModel queryModel) => new() { CommandText = "SELECT 7" };
        public GraphSqlQuery GenerateLongCount(GraphModel model, GraphQueryModel queryModel) => new() { CommandText = "SELECT 7" };
    }

    private sealed class RecordingConnectionFactory : IGormDbConnectionFactory
    {
        private readonly RecordingConnection _connection;

        public RecordingConnectionFactory(RecordingConnection connection)
        {
            _connection = connection;
        }

        public int CreateConnectionCount { get; private set; }

        public DbConnection CreateConnection()
        {
            CreateConnectionCount++;
            return _connection;
        }
    }

    private sealed class RecordingConnection : DbConnection
    {
        private readonly object? _scalarResult;
        private ConnectionState _state = ConnectionState.Closed;

        public RecordingConnection(object? scalarResult)
        {
            _scalarResult = scalarResult;
        }

        public int OpenCount { get; private set; }

        public List<RecordingCommand> Commands { get; } = [];

        public RecordingTransaction? ActiveTransaction { get; private set; }

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

        public override void Open()
        {
            _state = ConnectionState.Open;
            OpenCount++;
        }

        public override Task OpenAsync(CancellationToken cancellationToken)
        {
            Open();
            return Task.CompletedTask;
        }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        {
            ActiveTransaction = new RecordingTransaction(this);
            return ActiveTransaction;
        }

        protected override DbCommand CreateDbCommand()
        {
            var command = new RecordingCommand(this)
            {
                ScalarResult = _scalarResult
            };

            Commands.Add(command);

            return command;
        }
    }

    private sealed class RecordingTransaction : DbTransaction
    {
        private readonly DbConnection _connection;

        public RecordingTransaction(DbConnection connection)
        {
            _connection = connection;
        }

        public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;

        protected override DbConnection DbConnection => _connection;

        public override void Commit()
        {
        }

        public override void Rollback()
        {
        }

        public override Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public override Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class RecordingCommand : DbCommand
    {
        private readonly DbConnection _connection;
        private readonly RecordingParameterCollection _parameters = new();

        public RecordingCommand(DbConnection connection)
        {
            _connection = connection;
        }

        public object? ScalarResult { get; init; }

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
        {
        }

        public override int ExecuteNonQuery() => 0;

        public override object? ExecuteScalar() => ScalarResult;

        public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken) => Task.FromResult(ScalarResult);

        public override void Prepare()
        {
        }

        protected override DbParameter CreateDbParameter() => new RecordingParameter();

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotSupportedException();
    }

    private sealed class RecordingParameter : DbParameter
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
        {
        }
    }

    private sealed class RecordingParameterCollection : DbParameterCollection
    {
        private readonly List<object> _items = [];

        public override int Count => _items.Count;

        public override object SyncRoot => this;

        public override int Add(object value)
        {
            _items.Add(value);
            return _items.Count - 1;
        }

        public override void AddRange(Array values)
        {
            foreach (var value in values)
            {
                _items.Add(value);
            }
        }

        public override void Clear() => _items.Clear();

        public override bool Contains(object value) => _items.Contains(value);

        public override bool Contains(string value) => IndexOf(value) >= 0;

        public override void CopyTo(Array array, int index) => _items.ToArray().CopyTo(array, index);

        public override IEnumerator GetEnumerator() => _items.GetEnumerator();

        public override int IndexOf(object value) => _items.IndexOf(value);

        public override int IndexOf(string parameterName)
        {
            for (var i = 0; i < _items.Count; i++)
            {
                if (_items[i] is DbParameter parameter &&
                    string.Equals(parameter.ParameterName, parameterName, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        public override void Insert(int index, object value) => _items.Insert(index, value);

        public override void Remove(object value) => _items.Remove(value);

        public override void RemoveAt(int index) => _items.RemoveAt(index);

        public override void RemoveAt(string parameterName)
        {
            var index = IndexOf(parameterName);

            if (index >= 0)
            {
                RemoveAt(index);
            }
        }

        protected override DbParameter GetParameter(int index) => (DbParameter)_items[index];

        protected override DbParameter GetParameter(string parameterName) => (DbParameter)_items[IndexOf(parameterName)];

        protected override void SetParameter(int index, DbParameter value) => _items[index] = value;

        protected override void SetParameter(string parameterName, DbParameter value)
        {
            var index = IndexOf(parameterName);

            if (index >= 0)
            {
                _items[index] = value;
                return;
            }

            _items.Add(value);
        }
    }

    public TestContext TestContext { get; set; } = null!;
}