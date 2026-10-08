using Gorm.Infrastructure.Providers.SqlServer;
using Gorm.Infrastructure.Sql;
using Gorm.Tests.TestSupport;

namespace Gorm.Tests.Performance;

[TestClass]
public sealed class SqlPredicatePerformanceHotPathTests
{
    [TestMethod]
    public void ToSql_ContainsWithDuplicateValues_AddsParametersForDistinctValuesOnly()
    {
        var ctx = new TestGraphContext();
        ctx.UseProvider(SqlServerGraphProvider.Instance);

        int[] ages = [10, 10, 20];

        var sql = ctx.People.Where(x => ages.Contains(x.Age)).ToSql(ctx);

        Assert.Contains(" IN ", sql.CommandText);
        Assert.HasCount(2, sql.Parameters);
        Assert.AreEqual(10, sql.Parameters[0].Value);
        Assert.AreEqual(20, sql.Parameters[1].Value);
    }

    [TestMethod]
    public void ToSql_ContainsWithNullValue_AddsNullPredicate()
    {
        var ctx = new TestGraphContext();
        ctx.UseProvider(SqlServerGraphProvider.Instance);

        string?[] names = ["Alice", "Alice", null];

        var sql = ctx.People.Where(x => names.Contains(x.Name)).ToSql(ctx);

        Assert.Contains(" IN ", sql.CommandText);
        Assert.Contains("IS NULL", sql.CommandText);
        Assert.HasCount(1, sql.Parameters);
        Assert.AreEqual("Alice", sql.Parameters[0].Value);
    }

    [TestMethod]
    public void ToSql_ContainsWithEmptyArray_WritesAlwaysFalsePredicate()
    {
        var ctx = new TestGraphContext();
        ctx.UseProvider(SqlServerGraphProvider.Instance);

        var ages = Array.Empty<int>();

        var sql = ctx.People.Where(x => ages.Contains(x.Age)).ToSql(ctx);

        Assert.Contains("1 = 0", sql.CommandText);
        Assert.IsEmpty(sql.Parameters);
    }

    [TestMethod]
    public void ToSql_StringStartsWithCapturedProperty_UsesEscapedLikeParameter()
    {
        var ctx = new TestGraphContext();
        ctx.UseProvider(SqlServerGraphProvider.Instance);

        var filter = new SearchFilter
        {
            Prefix = "Al%"
        };

        var sql = ctx.People.Where(x => x.Name.StartsWith(filter.Prefix)).ToSql(ctx);

        Assert.Contains("LIKE", sql.CommandText);
        Assert.Contains("ESCAPE '~'", sql.CommandText);
        Assert.HasCount(1, sql.Parameters);
        Assert.AreEqual("Al~%%", sql.Parameters[0].Value);
    }

    private sealed class SearchFilter
    {
        public string Prefix { get; init; } = string.Empty;
    }
}