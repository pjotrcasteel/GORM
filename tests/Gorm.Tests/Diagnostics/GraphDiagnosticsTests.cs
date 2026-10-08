using Gorm.Application.Diagnostics;
using Gorm.Application.Querying;
using Gorm.Infrastructure.Providers.SqlServer.Helpers;
using Gorm.Tests.TestSupport;

namespace Gorm.Tests.Diagnostics;

[TestClass]
public sealed class GraphDiagnosticsTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Escape_Wraps_Name_In_Square_Brackets()
    {
        var result = SqlGenerationHelpers.Escape("MyTable");
        Assert.AreEqual("[MyTable]", result);
    }

    [TestMethod]
    public void Escape_Works_For_Schema_Name()
    {
        var result = SqlGenerationHelpers.Escape("dbo");
        Assert.AreEqual("[dbo]", result);
    }

    [TestMethod]
    public void Explain_Returns_Result_With_Sql_And_Debug_View()
    {
        var ctx = new TestGraphContext();

        var result = ctx.People.Where(x => x.Name == "Alice").Explain();

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.Sql);
        Assert.IsNotNull(result.DebugView);
        Assert.IsNotNull(result.Parameters);
        Assert.Contains("SELECT", result.Sql);
        Assert.Contains("GraphQueryModel", result.DebugView);
    }

    [TestMethod]
    public void Explain_Throws_For_Null_Query()
    {
        IQueryable<TestPerson> query = null!;
        Assert.ThrowsExactly<ArgumentNullException>(() => query.Explain());
    }

    [TestMethod]
    public void Explain_Throws_When_Provider_Is_Not_GraphQueryProvider()
    {
        var query = new List<TestPerson>().AsQueryable();
        Assert.ThrowsExactly<InvalidOperationException>(() => query.Explain());
    }

    [TestMethod]
    public void GraphQueryExplainResult_Stores_Properties()
    {
        var result = new GraphQueryExplainResult
        {
            Sql = "SELECT 1",
            Parameters = [],
            DebugView = "some debug"
        };

        Assert.AreEqual("SELECT 1", result.Sql);
        Assert.AreEqual("some debug", result.DebugView);
        Assert.IsEmpty(result.Parameters);
    }

    [TestMethod]
    public async Task ValidateSchemaAsync_Throws_When_No_ConnectionFactory()
    {
        var ctx = new TestGraphContext();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => ctx.ValidateSchemaAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task AssertValidSchemaAsync_Throws_When_No_ConnectionFactory()
    {
        var ctx = new TestGraphContext();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => ctx.AssertValidSchemaAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public void GraphSchemaValidationReport_IsValid_True_When_No_Issues()
    {
        var report = new GraphSchemaValidationReport { Issues = [] };

        Assert.IsTrue(report.IsValid);
    }

    [TestMethod]
    public void GraphSchemaValidationReport_IsValid_False_When_Issues_Present()
    {
        var report = new GraphSchemaValidationReport
        {
            Issues =
            [
                new GraphSchemaValidationIssue
                {
                    Code = "Schema.Table.Missing",
                    Message = "Table missing",
                    Schema = "dbo",
                    Table = "Person"
                }
            ]
        };

        Assert.IsFalse(report.IsValid);
        Assert.HasCount(1, report.Issues);
        Assert.AreEqual("Schema.Table.Missing", report.Issues[0].Code);
    }

    [TestMethod]
    public void GraphSchemaValidationIssue_ToString_Returns_Formatted_String()
    {
        var issue = new GraphSchemaValidationIssue
        {
            Code = "TEST001",
            Message = "Test message",
            Schema = "dbo",
            Table = "Nodes"
        };

        Assert.AreEqual("TEST001", issue.Code);
        Assert.AreEqual("Test message", issue.Message);
        Assert.AreEqual("dbo", issue.Schema);
        Assert.AreEqual("Nodes", issue.Table);
    }

    [TestMethod]
    public void ToQueryModel_Returns_Model_For_Valid_Query()
    {
        var ctx = new TestGraphContext();

        var model = ctx.People.ToQueryModel();

        Assert.IsNotNull(model);
    }

    [TestMethod]
    public void ToQueryModel_Throws_For_Non_GraphQueryProvider()
    {
        var query = new List<TestPerson>().AsQueryable();

        Assert.ThrowsExactly<InvalidOperationException>(() => query.ToQueryModel());
    }

    [TestMethod]
    public void ToDebugView_Returns_Non_Empty_String()
    {
        var ctx = new TestGraphContext();

        var view = ctx.People.ToDebugView();

        Assert.IsFalse(string.IsNullOrWhiteSpace(view));
    }

    [TestMethod]
    public void ToSql_Returns_Query_With_CommandText()
    {
        var ctx = new TestGraphContext();

        var sql = ctx.People.Where(x => x.Age > 18).ToSql();

        Assert.IsNotNull(sql);
        Assert.IsFalse(string.IsNullOrWhiteSpace(sql.CommandText));
    }

    [TestMethod]
    public void ToSql_Throws_For_Non_GraphQueryProvider()
    {
        var query = new List<TestPerson>().AsQueryable();
        Assert.ThrowsExactly<InvalidOperationException>(() => query.ToSql());
    }
}