using Gorm.Application.Querying;
using Gorm.Tests.TestSupport;

namespace Gorm.Tests.Query;

[TestClass]
public sealed class GraphNodeLookupExtensionsTests
{
    [TestMethod]
    public void WhereId_WhenCalled_AddsIdPredicateToQueryModel()
    {
        var context = new TestGraphContext();
        var query = context.People.WhereId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));

        var model = query.ToQueryModel();

        Assert.HasCount(1, model.Steps);
        Assert.Contains("Id", query.ToDebugView());
    }

    [TestMethod]
    public void WhereIds_WhenCalled_GeneratesInPredicateSql()
    {
        var context = new TestGraphContext();
        var ids = new[]
        {
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")
        };

        var sql = context.People.WhereIds(ids).ToSql();

        Assert.Contains(" IN ", sql.CommandText);
        Assert.HasCount(2, sql.Parameters);
    }

    [TestMethod]
    public void WhereIds_WhenEmpty_GeneratesAlwaysFalsePredicate()
    {
        var context = new TestGraphContext();

        var sql = context.People.WhereIds([]).ToSql();

        Assert.Contains("1 = 0", sql.CommandText);
        Assert.IsEmpty(sql.Parameters);
    }
}