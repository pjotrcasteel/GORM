using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using Gorm.Application.Context;
using Gorm.Application.Diagnostics;
using Gorm.Core.Configuration;
using Gorm.Core.Primitives;
using Gorm.Infrastructure.Persistence.Connections;

namespace Gorm.Tests.Diagnostics;

[TestClass]
public sealed class GraphContextDiagnosticsExtensionsAdditionalTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task AssertValidSchemaAsync_Does_Not_Throw_When_Schema_Is_Valid()
    {
        var connection = new DiagnosticsFakeConnection();
        connection.AddTable("dbo", "NodeTable", isNode: true, isEdge: false, ["Id", "Name"]);

        var context = new DiagnosticsContext();
        context.UseConnectionFactory(new DiagnosticsFactory(connection));

        await context.AssertValidSchemaAsync(TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task AssertValidSchemaAsync_Throws_When_Schema_Is_Invalid()
    {
        var connection = new DiagnosticsFakeConnection();
        connection.AddTable("dbo", "NodeTable", isNode: false, isEdge: false, ["Id"]);

        var context = new DiagnosticsContext();
        context.UseConnectionFactory(new DiagnosticsFactory(connection));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => context.AssertValidSchemaAsync(TestContext.CancellationToken));
    }

    private sealed class DiagnosticsContext : GraphContext
    {
        protected override void OnModelCreating(GraphModelBuilder modelBuilder)
        {
            modelBuilder.Node<DiagnosticsNode>(node =>
            {
                node.ToTable("NodeTable");
                node.HasKey(x => x.Id);
                node.Property(x => x.Name);
            });
        }
    }

    private sealed class DiagnosticsNode : Node
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class DiagnosticsFactory : IGormDbConnectionFactory
    {
        private readonly DbConnection _connection;

        public DiagnosticsFactory(DbConnection connection)
        {
            _connection = connection;
        }

        public DbConnection CreateConnection() => _connection;
    }

    private sealed class DiagnosticsFakeConnection : DbConnection
    {
        private readonly Dictionary<(string Schema, string Table), (bool IsNode, bool IsEdge, HashSet<string> Columns)> _tables = [];
        private ConnectionState _state = ConnectionState.Closed;

        public void AddTable(string schema, string table, bool isNode, bool isEdge, string[] columns)
        {
            _tables[(schema, table)] = (isNode, isEdge, [.. columns]);
        }

        internal bool TryGetTable(string schema, string table, out (bool IsNode, bool IsEdge, HashSet<string> Columns) tableInfo) =>
            _tables.TryGetValue((schema, table), out tableInfo);

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

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
        protected override DbCommand CreateDbCommand() => new DiagnosticsFakeCommand(this);
    }

    private sealed class DiagnosticsFakeCommand : DbCommand
    {
        private readonly DiagnosticsFakeConnection _connection;
        private readonly DiagnosticsFakeParameterCollection _parameters = new();

        public DiagnosticsFakeCommand(DiagnosticsFakeConnection connection)
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
        protected override DbConnection DbConnection { get => _connection; set => _ = value; }
        protected override DbParameterCollection DbParameterCollection => _parameters;
        protected override DbTransaction? DbTransaction { get; set; }

        public override void Cancel()
        { }
        public override int ExecuteNonQuery() => 0;
        public override object? ExecuteScalar() => null;
        public override void Prepare()
        { }
        protected override DbParameter CreateDbParameter() => new DiagnosticsFakeParameter();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => ExecuteReaderCore();
        protected override Task<DbDataReader> ExecuteDbDataReaderAsync(
            CommandBehavior behavior,
            CancellationToken cancellationToken
        ) => Task.FromResult<DbDataReader>(ExecuteReaderCore());

        private DiagnosticsFakeReader ExecuteReaderCore()
        {
            var schema = _parameters.GetValue("@schema")?.ToString() ?? string.Empty;
            var table = _parameters.GetValue("@table")?.ToString() ?? string.Empty;

            if (CommandText.Contains("FROM sys.tables", StringComparison.OrdinalIgnoreCase))
            {
                if (_connection.TryGetTable(schema, table, out var tableInfo))
                {
                    return new DiagnosticsFakeReader([[tableInfo.IsNode, tableInfo.IsEdge]], ["is_node", "is_edge"]);
                }

                return new DiagnosticsFakeReader([], ["is_node", "is_edge"]);
            }

            if (CommandText.Contains("INFORMATION_SCHEMA.COLUMNS", StringComparison.OrdinalIgnoreCase))
            {
                if (_connection.TryGetTable(schema, table, out var tableInfo))
                {
                    var rows = tableInfo.Columns.Select(x => new object?[] { x }).ToArray();
                    return new DiagnosticsFakeReader(rows, ["COLUMN_NAME"]);
                }

                return new DiagnosticsFakeReader([], ["COLUMN_NAME"]);
            }

            return new DiagnosticsFakeReader([], ["col"]);
        }
    }

    private sealed class DiagnosticsFakeParameter : DbParameter
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

    private sealed class DiagnosticsFakeParameterCollection : DbParameterCollection
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

        public object? GetValue(string name)
        {
            var i = IndexOf(name);
            return i >= 0 ? _items[i].Value : null;
        }
    }

    private sealed class DiagnosticsFakeReader(object?[][] rows, string[] columns) : DbDataReader
    {
        private int _index = -1;
        public override int FieldCount => columns.Length;
        public override bool HasRows => rows.Length > 0;
        public override bool IsClosed => false;
        public override int RecordsAffected => 0;
        public override int Depth => 0;
        public override object this[int ordinal] => GetValue(ordinal);
        public override object this[string name] => GetValue(GetOrdinal(name));
        public override bool Read()
        {
            if (_index + 1 >= rows.Length)
            {
                return false;
            }

            _index++;
            return true;
        }
        public override Task<bool> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(Read());
        public override bool NextResult() => false;
        public override Task<bool> NextResultAsync(CancellationToken cancellationToken) => Task.FromResult(false);
        public override string GetName(int ordinal) => columns[ordinal];
        public override int GetOrdinal(string name) => Array.FindIndex(columns, x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase));
        public override object GetValue(int ordinal) => rows[_index][ordinal]!;
        public override bool IsDBNull(int ordinal) => GetValue(ordinal) is null or DBNull;
        public override bool GetBoolean(int ordinal) => (bool)GetValue(ordinal);
        public override string GetString(int ordinal) => (string)GetValue(ordinal);
        public override int GetValues(object[] values)
        {
            var c = Math.Min(values.Length, columns.Length);
            for (var i = 0; i < c; i++)
            {
                values[i] = GetValue(i);
            }

            return c;
        }
        public override Type GetFieldType(int ordinal) => typeof(object);
        public override string GetDataTypeName(int ordinal) => "object";
        public override IEnumerator GetEnumerator() => rows.GetEnumerator();
        public override byte GetByte(int ordinal) => throw new NotSupportedException();
        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();
        public override char GetChar(int ordinal) => throw new NotSupportedException();
        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();
        public override DateTime GetDateTime(int ordinal) => throw new NotSupportedException();
        public override decimal GetDecimal(int ordinal) => throw new NotSupportedException();
        public override double GetDouble(int ordinal) => throw new NotSupportedException();
        public override float GetFloat(int ordinal) => throw new NotSupportedException();
        public override Guid GetGuid(int ordinal) => throw new NotSupportedException();
        public override short GetInt16(int ordinal) => throw new NotSupportedException();
        public override int GetInt32(int ordinal) => throw new NotSupportedException();
        public override long GetInt64(int ordinal) => throw new NotSupportedException();
    }
}