using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Gorm.Application.Diagnostics;
using Gorm.Application.Execution;
using Gorm.Application.Querying.Models;
using Gorm.Core.Models;
using Gorm.Infrastructure.Persistence.Connections;
using Gorm.Infrastructure.Providers.Abstractions;
using Gorm.Infrastructure.Sql;
using Gorm.Tests.TestSupport;

namespace Gorm.Tests.Execution;

[TestClass]
public sealed class GraphQueryExecutorScalarTests
{
    [TestMethod]
    public async Task ExecuteCountAsync_Converts_Decimal_To_Int()
    {
        var executor = CreateExecutor(5m, null);
        var ctx = new TestGraphContext();

        var result = await executor.ExecuteCountAsync(ctx, ctx.People, TestContext.CancellationToken);

        Assert.AreEqual(5, result);
    }

    [TestMethod]
    public async Task ExecuteCountAsync_Returns_Zero_For_Null()
    {
        var executor = CreateExecutor(null, null);
        var ctx = new TestGraphContext();

        var result = await executor.ExecuteCountAsync(ctx, ctx.People, TestContext.CancellationToken);

        Assert.AreEqual(0, result);
    }

    [TestMethod]
    public async Task ExecuteCountAsync_Converts_Long_To_Int()
    {
        var executor = CreateExecutor(7L, null);
        var ctx = new TestGraphContext();

        var result = await executor.ExecuteCountAsync(ctx, ctx.People, TestContext.CancellationToken);

        Assert.AreEqual(7, result);
    }

    [TestMethod]
    public async Task ExecuteLongCountAsync_Converts_Decimal_To_Long()
    {
        var executor = CreateExecutor(9m, null);
        var ctx = new TestGraphContext();

        var result = await executor.ExecuteLongCountAsync(ctx, ctx.People, TestContext.CancellationToken);

        Assert.AreEqual(9L, result);
    }

    [TestMethod]
    public async Task ExecuteLongCountAsync_Returns_Zero_For_DbNull()
    {
        var executor = CreateExecutor(DBNull.Value, null);
        var ctx = new TestGraphContext();

        var result = await executor.ExecuteLongCountAsync(ctx, ctx.People, TestContext.CancellationToken);

        Assert.AreEqual(0L, result);
    }

    [TestMethod]
    public async Task ExecuteSumAsync_Returns_Zero_Default_For_Null_NonNullable_Result()
    {
        var executor = CreateExecutor(DBNull.Value, null);
        var ctx = new TestGraphContext();

        var result = await executor.ExecuteSumAsync<int, int>(ctx, ctx.People.Select(x => x.Age), TestContext.CancellationToken);

        Assert.AreEqual(0, result);
    }

    [TestMethod]
    public async Task ExecuteSumAsync_Returns_Null_Default_For_Nullable_Result()
    {
        var executor = CreateExecutor(DBNull.Value, null);
        var ctx = new TestGraphContext();

        var result = await executor.ExecuteSumAsync<int, int?>(ctx, ctx.People.Select(x => x.Age), TestContext.CancellationToken);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task ExecuteAverageAsync_Throws_InvalidOperation_When_No_Elements()
    {
        var executor = CreateExecutor(null, null);
        var ctx = new TestGraphContext();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => executor.ExecuteAverageAsync(ctx, ctx.People.Select(x => x.Age), TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ExecuteAverageAsync_Converts_Int_To_Decimal()
    {
        var executor = CreateExecutor(12, null);
        var ctx = new TestGraphContext();

        var result = await executor.ExecuteAverageAsync(ctx, ctx.People.Select(x => x.Age), TestContext.CancellationToken);

        Assert.AreEqual(12m, result);
    }

    [TestMethod]
    public async Task ExecuteMinAsync_Throws_InvalidOperation_When_No_Elements()
    {
        var executor = CreateExecutor(DBNull.Value, null);
        var ctx = new TestGraphContext();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => executor.ExecuteMinAsync<int, int>(ctx, ctx.People.Select(x => x.Age), TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ExecuteMinAsync_Converts_Value_Type_Result()
    {
        var executor = CreateExecutor(3m, null);
        var ctx = new TestGraphContext();

        var result = await executor.ExecuteMinAsync<int, int>(ctx, ctx.People.Select(x => x.Age), TestContext.CancellationToken);

        Assert.AreEqual(3, result);
    }

    [TestMethod]
    public async Task ExecuteMaxAsync_Throws_InvalidOperation_When_No_Elements()
    {
        var executor = CreateExecutor(DBNull.Value, null);
        var ctx = new TestGraphContext();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => executor.ExecuteMaxAsync<int, int>(ctx, ctx.People.Select(x => x.Age), TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ExecuteMaxAsync_Converts_Value_Type_Result()
    {
        var executor = CreateExecutor(11m, null);
        var ctx = new TestGraphContext();

        var result = await executor.ExecuteMaxAsync<int, int>(ctx, ctx.People.Select(x => x.Age), TestContext.CancellationToken);

        Assert.AreEqual(11, result);
    }

    [TestMethod]
    public async Task ExecuteAnyAsync_Returns_False_For_DbNull()
    {
        var executor = CreateExecutor(DBNull.Value, null);
        var ctx = new TestGraphContext();

        var result = await executor.ExecuteAnyAsync(ctx, ctx.People, TestContext.CancellationToken);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task ExecuteAnyAsync_Returns_True_For_NonNull_Result()
    {
        var executor = CreateExecutor(0, null);
        var ctx = new TestGraphContext();

        var result = await executor.ExecuteAnyAsync(ctx, ctx.People, TestContext.CancellationToken);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task ExecuteCountAsync_Emits_Scalar_Query_Activity()
    {
        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == GormDiagnostics.ActivitySourceName,
            Sample = (ref _) => ActivitySamplingResult.AllData,
            ActivityStopped = activities.Add
        };

        ActivitySource.AddActivityListener(listener);

        var executor = CreateExecutor(5, null);
        var ctx = new TestGraphContext();

        var result = await executor.ExecuteCountAsync(ctx, ctx.People, TestContext.CancellationToken);

        Assert.AreEqual(5, result);
        var activity = activities.Single(x => x.OperationName == GormDiagnostics.ActivityNames.QueryScalar);
        Assert.AreEqual(ActivityStatusCode.Ok, activity.Status);
        Assert.AreEqual(typeof(int).FullName, activity.GetTagItem(GormDiagnostics.TagNames.ResultType));
        Assert.IsNotNull(activity.GetTagItem(GormDiagnostics.TagNames.DurationMilliseconds));
    }

    [TestMethod]
    public async Task ExecuteCountAsync_Wraps_Execution_Exception_With_Sql_Details()
    {
        var executor = CreateExecutor(null, new InvalidOperationException("boom"));
        var ctx = new TestGraphContext();

        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => executor.ExecuteCountAsync(ctx, ctx.People, TestContext.CancellationToken));

        Assert.Contains("Gorm SQL execution failed.", ex.Message);
        Assert.IsNotNull(ex.InnerException);
        Assert.AreEqual("boom", ex.InnerException.Message);
    }

    [TestMethod]
    public async Task ExecuteAnyAsync_Wraps_Execution_Exception_With_Sql_Details()
    {
        var executor = CreateExecutor(null, new InvalidOperationException("boom-any"));
        var ctx = new TestGraphContext();

        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => executor.ExecuteAnyAsync(ctx, ctx.People, TestContext.CancellationToken));

        Assert.Contains("Gorm SQL execution failed.", ex.Message);
        Assert.IsNotNull(ex.InnerException);
        Assert.AreEqual("boom-any", ex.InnerException.Message);
    }

    [TestMethod]
    public async Task ExecuteAverageAsync_Wraps_Execution_InvalidOperation_With_Sql_Details()
    {
        var executor = CreateExecutor(null, new InvalidOperationException("boom-average"));
        var ctx = new TestGraphContext();

        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => executor.ExecuteAverageAsync(ctx, ctx.People.Select(x => x.Age), TestContext.CancellationToken));

        Assert.Contains("Gorm SQL execution failed.", ex.Message);
        Assert.IsNotNull(ex.InnerException);
        Assert.AreEqual("boom-average", ex.InnerException.Message);
    }

    [TestMethod]
    public async Task ExecuteAverageAsync_Empty_Sequence_InvalidOperation_Is_Not_Wrapped_As_Sql_Execution_Failure()
    {
        var executor = CreateExecutor(DBNull.Value, null);
        var ctx = new TestGraphContext();

        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => executor.ExecuteAverageAsync(ctx, ctx.People.Select(x => x.Age), TestContext.CancellationToken));

        Assert.AreEqual("Sequence contains no elements.", ex.Message);
        Assert.IsNull(ex.InnerException);
    }

    private static GraphQueryExecutor CreateExecutor(object? scalarResult, Exception? throwOnScalar)
    {
        var connection = new ScalarFakeConnection(scalarResult, throwOnScalar);
        var factory = new ScalarFakeConnectionFactory(connection);
        var sqlGenerator = new ScalarFakeSqlGenerator();
        return new GraphQueryExecutor(factory, sqlGenerator);
    }

    private sealed class ScalarFakeConnectionFactory(DbConnection connection) : IGormDbConnectionFactory
    {
        public DbConnection CreateConnection() => connection;
    }

    private sealed class ScalarFakeSqlGenerator : IGraphQuerySqlGenerator
    {
        private static GraphSqlQuery Create(string sql) => new()
        {
            CommandText = sql,
            Parameters = { new GraphSqlParameter { Name = "@p0", Value = 42 } }
        };

        public GraphSqlQuery Generate(GraphModel model, GraphQueryModel queryModel) => Create("SELECT 1");
        public GraphSqlQuery Generate(GraphModel model, GraphQueryModel queryModel, int? effectiveTakeOverride) => Create("SELECT 1");
        public GraphSqlQuery GenerateExists(GraphModel model, GraphQueryModel queryModel, int? effectiveTakeOverride) => Create("SELECT 1");
        public GraphSqlQuery GenerateAggregate(GraphModel model, GraphQueryModel queryModel, GraphAggregateKind aggregateKind) => Create("SELECT 1");
        public GraphSqlQuery GenerateCount(GraphModel model, GraphQueryModel queryModel) => Create("SELECT 1");
        public GraphSqlQuery GenerateLongCount(GraphModel model, GraphQueryModel queryModel) => Create("SELECT 1");
    }

    private sealed class ScalarFakeConnection(object? scalarResult, Exception? throwOnScalar) : DbConnection
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

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
        protected override DbCommand CreateDbCommand() => new ScalarFakeCommand(this, scalarResult, throwOnScalar);
    }

    private sealed class ScalarFakeCommand(DbConnection connection, object? scalarResult, Exception? throwOnScalar) : DbCommand
    {
        private readonly ScalarFakeParameterCollection _parameters = new();

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
        { }
        public override int ExecuteNonQuery() => 0;
        public override object? ExecuteScalar() => throw new NotSupportedException();
        public override void Prepare()
        { }
        protected override DbParameter CreateDbParameter() => new ScalarFakeParameter();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotSupportedException();

        public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken)
        {
            if (throwOnScalar is not null)
            {
                throw throwOnScalar;
            }

            return Task.FromResult(scalarResult);
        }
    }

    private sealed class ScalarFakeParameter : DbParameter
    {
        public override DbType DbType { get; set; }
        public override ParameterDirection Direction { get; set; }
        public override bool IsNullable { get; set; }
        [AllowNull] public override string ParameterName { get; set; } = string.Empty;
        [AllowNull] public override string SourceColumn { get; set; } = string.Empty;
        public override object? Value { get; set; }
        public override bool SourceColumnNullMapping { get; set; }
        public override int Size { get; set; }
        public override void ResetDbType()
        { }
    }

    private sealed class ScalarFakeParameterCollection : DbParameterCollection
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

    public TestContext TestContext { get; set; }
}