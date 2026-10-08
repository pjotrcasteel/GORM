using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using Gorm.Application.Context;
using Gorm.Application.Execution;
using Gorm.Core.Configuration;
using Gorm.Core.Primitives;
using Gorm.Infrastructure.Persistence.Connections;

namespace Gorm.Tests.Execution;

[TestClass]
public sealed class GraphSaveChangesExecutorConcurrencyTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task SaveChangesAsync_Added_ConcurrencyEntity_Initializes_Version_To_One()
    {
        var connection = new ConcurrencyFakeDbConnection(_ => 1);
        var context = new ConcurrencyContext(new ConcurrencyFactory(connection));
        var person = new ConcurrencyPerson { Id = Guid.NewGuid(), Name = "Alice", Version = 0 };

        context.Add(person);
        await context.SaveChangesAsync(TestContext.CancellationToken);

        Assert.AreEqual(1, person.Version);
    }

    [TestMethod]
    public async Task SaveChangesAsync_Modified_ConcurrencyEntity_Throws_When_Update_Affects_Zero_Rows()
    {
        var connection = new ConcurrencyFakeDbConnection(commandText => commandText.StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase) ? 0 : 1);

        var context = new ConcurrencyContext(new ConcurrencyFactory(connection));
        var person = new ConcurrencyPerson { Id = Guid.NewGuid(), Name = "Alice", Version = 0 };

        context.Add(person);
        await context.SaveChangesAsync(TestContext.CancellationToken);

        person.Name = "Updated";

        await Assert.ThrowsExactlyAsync<GraphConcurrencyException>(() => context.SaveChangesAsync(TestContext.CancellationToken));

        Assert.AreEqual(1, person.Version);
    }

    [TestMethod]
    public async Task SaveChangesAsync_Modified_ConcurrencyEntity_Increments_Version_After_Success()
    {
        var connection = new ConcurrencyFakeDbConnection(_ => 1);
        var context = new ConcurrencyContext(new ConcurrencyFactory(connection));
        var person = new ConcurrencyPerson { Id = Guid.NewGuid(), Name = "Alice", Version = 0 };

        context.Add(person);
        await context.SaveChangesAsync(TestContext.CancellationToken);

        person.Name = "Updated";
        await context.SaveChangesAsync(TestContext.CancellationToken);

        Assert.AreEqual(2, person.Version);
    }

    [TestMethod]
    public async Task SaveChangesAsync_Deleted_ConcurrencyEntity_Throws_When_Delete_Affects_Zero_Rows()
    {
        var connection = new ConcurrencyFakeDbConnection(commandText => commandText.StartsWith("DELETE", StringComparison.OrdinalIgnoreCase) ? 0 : 1);

        var context = new ConcurrencyContext(new ConcurrencyFactory(connection));
        var person = new ConcurrencyPerson { Id = Guid.NewGuid(), Name = "Alice", Version = 0 };

        context.Add(person);
        await context.SaveChangesAsync(TestContext.CancellationToken);

        context.Remove(person);

        await Assert.ThrowsExactlyAsync<GraphConcurrencyException>(() => context.SaveChangesAsync(TestContext.CancellationToken));
    }

    private sealed class ConcurrencyContext : GraphContext
    {
        public ConcurrencyContext(IGormDbConnectionFactory connectionFactory)
            : base(connectionFactory)
        {
        }

        protected override void OnModelCreating(GraphModelBuilder modelBuilder)
        {
            modelBuilder.Node<ConcurrencyPerson>(node =>
            {
                node.ToTable("Person");
                node.HasKey(x => x.Id);
                node.Property(x => x.Name).HasMaxLength(200);
                node.Property(x => x.Version);
            });
        }
    }

    private sealed class ConcurrencyPerson : Node, IHasConcurrencyToken
    {
        public string Name { get; set; } = string.Empty;
        public int Version { get; set; }
    }

    private sealed class ConcurrencyFactory : IGormDbConnectionFactory
    {
        private readonly DbConnection _connection;

        public ConcurrencyFactory(DbConnection connection)
        {
            _connection = connection;
        }

        public DbConnection CreateConnection() => _connection;
    }

    private sealed class ConcurrencyFakeDbConnection : DbConnection
    {
        private readonly Func<string, int> _nonQueryResultResolver;
        private ConnectionState _state = ConnectionState.Closed;

        public ConcurrencyFakeDbConnection(Func<string, int> nonQueryResultResolver)
        {
            _nonQueryResultResolver = nonQueryResultResolver;
        }

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

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => new ConcurrencyFakeDbTransaction(this);

        protected override DbCommand CreateDbCommand() => new ConcurrencyFakeDbCommand(this, _nonQueryResultResolver);
    }

    private sealed class ConcurrencyFakeDbTransaction : DbTransaction
    {
        private readonly DbConnection _connection;

        public ConcurrencyFakeDbTransaction(DbConnection connection)
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

    private sealed class ConcurrencyFakeDbCommand : DbCommand
    {
        private readonly Func<string, int> _nonQueryResultResolver;
        private readonly ConcurrencyFakeDbParameterCollection _parameters = new();
        private readonly DbConnection _connection;

        public ConcurrencyFakeDbCommand(DbConnection connection, Func<string, int> nonQueryResultResolver)
        {
            _connection = connection;
            _nonQueryResultResolver = nonQueryResultResolver;
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
        {
        }

        public override int ExecuteNonQuery() => _nonQueryResultResolver(CommandText);

        public override object? ExecuteScalar() => 1;

        public override void Prepare()
        {
        }

        protected override DbParameter CreateDbParameter() => new ConcurrencyFakeDbParameter();

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotSupportedException();

        public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken) => Task.FromResult(_nonQueryResultResolver(CommandText));

        public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken) => Task.FromResult<object?>(1);
    }

    private sealed class ConcurrencyFakeDbParameter : DbParameter
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

    private sealed class ConcurrencyFakeDbParameterCollection : DbParameterCollection
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

        public override int IndexOf(string parameterName) => _items.FindIndex(x => x.ParameterName == parameterName);

        public override void Insert(int index, object value) => _items.Insert(index, (DbParameter)value);

        public override void Remove(object value) => _items.Remove((DbParameter)value);

        public override void RemoveAt(int index) => _items.RemoveAt(index);

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
            }
            else
            {
                _items.Add(value);
            }
        }
    }
}