using System.Data.Common;
using Gorm.Application.Execution;
using Gorm.Application.Querying.Models;
using Gorm.Core.Models;
using Gorm.Infrastructure.Persistence.Connections;
using Gorm.Infrastructure.Providers.Abstractions;
using Gorm.Infrastructure.Sql;
using Gorm.Tests.TestSupport;

namespace Gorm.Tests.Execution;

[TestClass]
public sealed class GraphQueryExecutorGuardTests
{
    [TestMethod]
    public void Constructor_Throws_For_Null_ConnectionFactory()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new GraphQueryExecutor(null!, new StubSqlGenerator()));
    }

    [TestMethod]
    public void Constructor_Throws_For_Null_SqlGenerator()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new GraphQueryExecutor(new StubConnectionFactory(), null!));
    }

    [TestMethod]
    public async Task ExecuteAsync_Throws_For_Null_Context()
    {
        var executor = new GraphQueryExecutor(new StubConnectionFactory(), new StubSqlGenerator());
        var query = new List<TestPerson>().AsQueryable();
        var request = new GraphQueryExecutionRequest { Mode = GraphQueryExecutionMode.List };

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => executor.ExecuteAsync(null!, query, request, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ExecuteAsync_Throws_For_Existence_Request()
    {
        var executor = new GraphQueryExecutor(new StubConnectionFactory(), new StubSqlGenerator());
        var context = new TestGraphContext();
        var request = new GraphQueryExecutionRequest { Mode = GraphQueryExecutionMode.Any };

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => executor.ExecuteAsync(context, context.People, request, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ExecuteAsync_Throws_For_Non_Graph_Provider()
    {
        var executor = new GraphQueryExecutor(new StubConnectionFactory(), new StubSqlGenerator());
        var context = new TestGraphContext();
        var request = new GraphQueryExecutionRequest { Mode = GraphQueryExecutionMode.List };
        var query = new List<TestPerson>().AsQueryable();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => executor.ExecuteAsync(context, query, request, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ExecuteAnyAsync_Throws_For_Non_Graph_Provider()
    {
        var executor = new GraphQueryExecutor(new StubConnectionFactory(), new StubSqlGenerator());
        var context = new TestGraphContext();
        var query = new List<TestPerson>().AsQueryable();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => executor.ExecuteAnyAsync(context, query, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ExecuteCountAsync_Throws_For_Non_Graph_Provider()
    {
        var executor = new GraphQueryExecutor(new StubConnectionFactory(), new StubSqlGenerator());
        var context = new TestGraphContext();
        var query = new List<TestPerson>().AsQueryable();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => executor.ExecuteCountAsync(context, query, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ExecuteLongCountAsync_Throws_For_Non_Graph_Provider()
    {
        var executor = new GraphQueryExecutor(new StubConnectionFactory(), new StubSqlGenerator());
        var context = new TestGraphContext();
        var query = new List<TestPerson>().AsQueryable();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => executor.ExecuteLongCountAsync(context, query, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ExecuteSumAsync_Throws_For_Non_Graph_Provider()
    {
        var executor = new GraphQueryExecutor(new StubConnectionFactory(), new StubSqlGenerator());
        var context = new TestGraphContext();
        var query = new List<int>().AsQueryable();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => executor.ExecuteSumAsync<int, int>(context, query, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ExecuteAverageAsync_Throws_For_Non_Graph_Provider()
    {
        var executor = new GraphQueryExecutor(new StubConnectionFactory(), new StubSqlGenerator());
        var context = new TestGraphContext();
        var query = new List<int>().AsQueryable();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => executor.ExecuteAverageAsync(context, query, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ExecuteMinAsync_Throws_For_Non_Graph_Provider()
    {
        var executor = new GraphQueryExecutor(new StubConnectionFactory(), new StubSqlGenerator());
        var context = new TestGraphContext();
        var query = new List<int>().AsQueryable();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => executor.ExecuteMinAsync<int, int>(context, query, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ExecuteMaxAsync_Throws_For_Non_Graph_Provider()
    {
        var executor = new GraphQueryExecutor(new StubConnectionFactory(), new StubSqlGenerator());
        var context = new TestGraphContext();
        var query = new List<int>().AsQueryable();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => executor.ExecuteMaxAsync<int, int>(context, query, TestContext.CancellationToken));
    }

    private sealed class StubConnectionFactory : IGormDbConnectionFactory
    {
        public DbConnection CreateConnection() => throw new NotSupportedException();
    }

    private sealed class StubSqlGenerator : IGraphQuerySqlGenerator
    {
        public GraphSqlQuery Generate(GraphModel model, GraphQueryModel queryModel) => new() { CommandText = "SELECT 1" };
        public GraphSqlQuery Generate(GraphModel model, GraphQueryModel queryModel, int? effectiveTakeOverride) => new() { CommandText = "SELECT 1" };
        public GraphSqlQuery GenerateExists(GraphModel model, GraphQueryModel queryModel, int? effectiveTakeOverride) => new() { CommandText = "SELECT 1" };
        public GraphSqlQuery GenerateAggregate(GraphModel model, GraphQueryModel queryModel, GraphAggregateKind aggregateKind) => new() { CommandText = "SELECT 1" };
        public GraphSqlQuery GenerateCount(GraphModel model, GraphQueryModel queryModel) => new() { CommandText = "SELECT 1" };
        public GraphSqlQuery GenerateLongCount(GraphModel model, GraphQueryModel queryModel) => new() { CommandText = "SELECT 1" };
    }

    public TestContext TestContext { get; set; }
}