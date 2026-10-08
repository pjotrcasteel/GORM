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

namespace Gorm.Tests.Execution;

[TestClass]
public sealed class GraphQueryExecutorReaderBehaviorTests
{
    [TestMethod]
    public async Task ExecuteAsync_Uses_Default_CommandBehavior_For_List_Queries()
    {
        var connection = new ReaderBehaviorConnection();
        var executor = new GraphQueryExecutor(new ReaderBehaviorConnectionFactory(connection), new ReaderBehaviorSqlGenerator());
        var context = new TestGraphContext();
        var request = new GraphQueryExecutionRequest { Mode = GraphQueryExecutionMode.List };

        var result = await executor.ExecuteAsync(context, context.People, request, CancellationToken.None);

        Assert.IsEmpty(result);
        Assert.AreEqual(CommandBehavior.Default, connection.LastCommandBehavior);
    }

    private sealed class ReaderBehaviorConnectionFactory(DbConnection connection) : IGormDbConnectionFactory
    {
        public DbConnection CreateConnection() => connection;
    }

    private sealed class ReaderBehaviorSqlGenerator : IGraphQuerySqlGenerator
    {
        private static GraphSqlQuery Create() => new()
        {
            CommandText = "SELECT [Id], [Name], [Age] FROM [dbo].[Person]"
        };

        public GraphSqlQuery Generate(GraphModel model, GraphQueryModel queryModel) => Create();

        public GraphSqlQuery Generate(GraphModel model, GraphQueryModel queryModel, int? effectiveTakeOverride) => Create();

        public GraphSqlQuery GenerateExists(GraphModel model, GraphQueryModel queryModel, int? effectiveTakeOverride) => Create();

        public GraphSqlQuery GenerateAggregate(GraphModel model, GraphQueryModel queryModel, GraphAggregateKind aggregateKind) => Create();

        public GraphSqlQuery GenerateCount(GraphModel model, GraphQueryModel queryModel) => Create();

        public GraphSqlQuery GenerateLongCount(GraphModel model, GraphQueryModel queryModel) => Create();
    }

    private sealed class ReaderBehaviorConnection : DbConnection
    {
        private ConnectionState _state = ConnectionState.Closed;

        [AllowNull]
        public override string ConnectionString { get; set; } = "Fake";

        public override string Database => "Fake";
        public override string DataSource => "Fake";
        public override string ServerVersion => "1";
        public override ConnectionState State => _state;
        public CommandBehavior? LastCommandBehavior { get; private set; }

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
            throw new NotSupportedException();

        protected override DbCommand CreateDbCommand() => new ReaderBehaviorCommand(this);

        public void Capture(CommandBehavior behavior) => LastCommandBehavior = behavior;
    }

    private sealed class ReaderBehaviorCommand(ReaderBehaviorConnection connection) : DbCommand
    {
        private readonly ReaderBehaviorParameterCollection _parameters = new();

        [AllowNull]
        public override string CommandText { get; set; } = string.Empty;

        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; }
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }

        [AllowNull]
        protected override DbConnection DbConnection { get; set; } = connection;

        protected override DbParameterCollection DbParameterCollection => _parameters;
        protected override DbTransaction? DbTransaction { get; set; }

        public override void Cancel()
        {
        }

        public override int ExecuteNonQuery() => 0;

        public override object? ExecuteScalar() => throw new NotSupportedException();

        public override void Prepare()
        {
        }

        protected override DbParameter CreateDbParameter() => new ReaderBehaviorParameter();

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => CreateReader(behavior);

        protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken) =>
            Task.FromResult<DbDataReader>(CreateReader(behavior));

        private ReaderBehaviorReader CreateReader(CommandBehavior behavior)
        {
            connection.Capture(behavior);
            return new ReaderBehaviorReader(["Id", "Name", "Age"]);
        }
    }

    private sealed class ReaderBehaviorReader(string[] columns) : DbDataReader
    {
        public override int FieldCount => columns.Length;
        public override bool HasRows => false;
        public override bool IsClosed => false;
        public override int RecordsAffected => 0;
        public override int Depth => 0;

        public override object this[int ordinal] => throw new InvalidOperationException();

        public override object this[string name] => throw new InvalidOperationException();

        public override bool Read() => false;

        public override Task<bool> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(false);

        public override bool NextResult() => false;

        public override Task<bool> NextResultAsync(CancellationToken cancellationToken) => Task.FromResult(false);

        public override string GetName(int ordinal) => columns[ordinal];

        public override int GetOrdinal(string name) =>
            Array.FindIndex(columns, x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase));

        public override object GetValue(int ordinal) => throw new InvalidOperationException();

        public override int GetValues(object[] values) => 0;

        public override bool IsDBNull(int ordinal) => throw new InvalidOperationException();

        public override Type GetFieldType(int ordinal) => typeof(object);

        public override string GetDataTypeName(int ordinal) => "object";

        public override bool GetBoolean(int ordinal) => throw new InvalidOperationException();

        public override byte GetByte(int ordinal) => throw new InvalidOperationException();

        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) =>
            throw new NotSupportedException();

        public override char GetChar(int ordinal) => throw new InvalidOperationException();

        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) =>
            throw new NotSupportedException();

        public override DateTime GetDateTime(int ordinal) => throw new InvalidOperationException();

        public override decimal GetDecimal(int ordinal) => throw new InvalidOperationException();

        public override double GetDouble(int ordinal) => throw new InvalidOperationException();

        public override float GetFloat(int ordinal) => throw new InvalidOperationException();

        public override Guid GetGuid(int ordinal) => throw new InvalidOperationException();

        public override short GetInt16(int ordinal) => throw new InvalidOperationException();

        public override int GetInt32(int ordinal) => throw new InvalidOperationException();

        public override long GetInt64(int ordinal) => throw new InvalidOperationException();

        public override string GetString(int ordinal) => throw new InvalidOperationException();

        public override IEnumerator GetEnumerator() => Array.Empty<object>().GetEnumerator();
    }

    private sealed class ReaderBehaviorParameter : DbParameter
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
        {
        }
    }

    private sealed class ReaderBehaviorParameterCollection : DbParameterCollection
    {
        private readonly List<DbParameter> _items = [];

        public override int Count => _items.Count;

        public override object SyncRoot => ((ICollection)_items).SyncRoot;

        public override int Add(object value)
        {
            _items.Add((DbParameter)value);
            return _items.Count - 1;
        }

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

        public override int IndexOf(string parameterName) =>
            _items.FindIndex(x => x.ParameterName == parameterName);

        public override void Insert(int index, object value) =>
            _items.Insert(index, (DbParameter)value);

        public override void Remove(object value) =>
            _items.Remove((DbParameter)value);

        public override void RemoveAt(int index) =>
            _items.RemoveAt(index);

        public override void RemoveAt(string parameterName)
        {
            var index = IndexOf(parameterName);

            if (index >= 0)
            {
                _items.RemoveAt(index);
            }
        }

        protected override DbParameter GetParameter(int index) => _items[index];

        protected override DbParameter GetParameter(string parameterName) => _items[IndexOf(parameterName)];

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
}