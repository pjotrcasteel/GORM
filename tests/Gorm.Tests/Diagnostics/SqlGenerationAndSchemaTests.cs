using Gorm.Application.Diagnostics;
using Gorm.Application.Execution;
using Gorm.Application.Querying;
using Gorm.Infrastructure.Providers.SqlServer;
using Gorm.Infrastructure.Sql;
using Gorm.Tests.TestSupport;

namespace Gorm.Tests.Diagnostics;

[TestClass]
public sealed class SqlGenerationAndSchemaTests
{

    [TestMethod]
    public void SchemaValidationIssue_Column_Property_Is_Accessible()
    {
        var issue = new GraphSchemaValidationIssue
        {
            Code = "Schema.Column.Missing",
            Message = "Column 'Name' is missing",
            Schema = "dbo",
            Table = "Person",
            Column = "Name"
        };

        Assert.AreEqual("Name", issue.Column);
        Assert.AreEqual("dbo", issue.Schema);
        Assert.AreEqual("Person", issue.Table);
        Assert.AreEqual("Schema.Column.Missing", issue.Code);
    }

    [TestMethod]
    public void SchemaValidationIssue_Column_Can_Be_Null()
    {
        var issue = new GraphSchemaValidationIssue
        {
            Code = "Schema.Table.Missing",
            Message = "Table missing",
            Schema = "dbo",
            Table = "Person"
        };

        Assert.IsNull(issue.Column);
    }

    [TestMethod]
    public void SchemaValidationReport_IsValid_When_No_Issues()
    {
        var report = new GraphSchemaValidationReport { Issues = [] };
        Assert.IsTrue(report.IsValid);
    }

    [TestMethod]
    public void SchemaValidationReport_IsNotValid_When_Issues_Present()
    {
        var report = new GraphSchemaValidationReport
        {
            Issues =
            [
                new GraphSchemaValidationIssue
                {
                    Code = "X",
                    Message = "Error"
                }
            ]
        };

        Assert.IsFalse(report.IsValid);
    }

    [TestMethod]
    public void SchemaValidationReport_ToString_Contains_Issue_Message()
    {
        var report = new GraphSchemaValidationReport
        {
            Issues =
            [
                new GraphSchemaValidationIssue
                {
                    Code = "ABC",
                    Message = "Something is broken"
                }
            ]
        };

        var str = report.ToString();
        Assert.IsNotNull(str);
        Assert.IsGreaterThan(0, str.Length);
    }

    [TestMethod]
    public void ToSql_Simple_Where_Returns_Sql()
    {
        var ctx = new TestGraphContext();
        ctx.UseProvider(SqlServerGraphProvider.Instance);

        var sql = ctx.People.Where(x => x.Name == "Alice").ToSql(ctx);

        Assert.IsNotNull(sql);
        Assert.IsNotNull(sql.CommandText);
        Assert.Contains("SELECT", sql.CommandText);
    }

    [TestMethod]
    public void ToSql_With_OrderBy_And_Paging_Contains_ORDER_BY()
    {
        var ctx = new TestGraphContext();
        ctx.UseProvider(SqlServerGraphProvider.Instance);

        var sql = ctx.People.OrderBy(x => x.Name).Skip(5).Take(10).ToSql(ctx);

        Assert.IsNotNull(sql.CommandText);
        var upperSql = sql.CommandText.ToUpperInvariant();
        Assert.Contains("ORDER", upperSql);
    }

    [TestMethod]
    public void ToSql_Throws_For_Null_Query()
    {
        var ctx = new TestGraphContext();
        IQueryable<TestPerson> query = null!;
        Assert.ThrowsExactly<ArgumentNullException>(() => query.ToSql(ctx));
    }

    [TestMethod]
    public void ToSql_Throws_For_Null_Context()
    {
        var ctx = new TestGraphContext();
        Assert.ThrowsExactly<ArgumentNullException>(() => ctx.People.ToSql(null!));
    }

    private static readonly int[] SourceArray = [1, 2, 3];

    [TestMethod]
    public void ToSql_Throws_For_Non_Graph_Provider()
    {
        var query = SourceArray.AsQueryable();
        var ctx = new TestGraphContext();
        Assert.ThrowsExactly<InvalidOperationException>(() => query.ToSql(ctx));
    }

    [TestMethod]
    public void ToSql_Traversal_Query_Returns_SQL()
    {
        var ctx = new TestGraphContext();
        ctx.UseProvider(SqlServerGraphProvider.Instance);

        var sql = ctx.People.Outgoing<TestWorksOn, TestProject>().ToSql(ctx);

        Assert.IsNotNull(sql.CommandText);
        Assert.IsGreaterThan(0, sql.CommandText.Length);
    }

    [TestMethod]
    public void ToSql_OrderByDescending_Returns_SQL()
    {
        var ctx = new TestGraphContext();
        ctx.UseProvider(SqlServerGraphProvider.Instance);

        var sql = ctx.People.OrderByDescending(x => x.Name).ToSql(ctx);

        Assert.IsNotNull(sql.CommandText);
        var upperSql2 = sql.CommandText.ToUpperInvariant();
        Assert.Contains("DESC", upperSql2);
    }

    [TestMethod]
    public void GenerateCount_With_OrderBy_And_No_Paging_Does_Not_Emit_OrderBy_In_Wrapper()
    {
        var ctx = new TestGraphContext();
        var queryModel = GraphQueryProvider.Translate(ctx.People.OrderBy(x => x.Name).Expression);

        var sql = SqlServerGraphQuerySqlGenerator.GenerateCount(ctx.Model, queryModel);

        Assert.DoesNotContain("ORDER BY", sql.CommandText.ToUpperInvariant());
    }

    [TestMethod]
    public void GenerateCount_With_OrderBy_And_Take_Preserves_OrderBy_In_Wrapper()
    {
        var ctx = new TestGraphContext();
        var queryModel = GraphQueryProvider.Translate(ctx.People.OrderBy(x => x.Name).Take(5).Expression);

        var sql = SqlServerGraphQuerySqlGenerator.GenerateCount(ctx.Model, queryModel);
        var upperSql = sql.CommandText.ToUpperInvariant();

        Assert.Contains("TOP (5)", upperSql);
        Assert.Contains("ORDER BY", upperSql);
    }

    [TestMethod]
    public void GenerateAggregate_With_OrderBy_And_No_Paging_Does_Not_Emit_OrderBy_In_Wrapper()
    {
        var ctx = new TestGraphContext();
        var queryModel = GraphQueryProvider.Translate(ctx.People.OrderBy(x => x.Name).Select(x => x.Age).Expression);

        var sql = SqlServerGraphQuerySqlGenerator.GenerateAggregate(ctx.Model, queryModel, GraphAggregateKind.Sum);

        Assert.DoesNotContain("ORDER BY", sql.CommandText.ToUpperInvariant());
    }
}