using System.Data;
using Gorm.Infrastructure.Sql;

namespace Gorm.Tests.Infrastructure;

[TestClass]
public sealed class GraphSqlParameterTests
{
    [TestMethod]
    public void Create_WhenStringValueIsShort_UsesStringDbTypeAndExactSize()
    {
        var parameter = GraphSqlParameter.Create("@p0", "abc");

        Assert.AreEqual("@p0", parameter.Name);
        Assert.AreEqual("abc", parameter.Value);
        Assert.AreEqual(DbType.String, parameter.DbType);
        Assert.AreEqual(3, parameter.Size);
    }

    [TestMethod]
    public void Create_WhenStringValueIsLong_UsesMaxSize()
    {
        var parameter = GraphSqlParameter.Create("@p0", new string('a', 4001));

        Assert.AreEqual(DbType.String, parameter.DbType);
        Assert.AreEqual(-1, parameter.Size);
    }

    [TestMethod]
    public void Create_WhenGuidValue_UsesGuidDbType()
    {
        var id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        var parameter = GraphSqlParameter.Create("@id", id);

        Assert.AreEqual(id, parameter.Value);
        Assert.AreEqual(DbType.Guid, parameter.DbType);
    }

    [TestMethod]
    public void Create_WhenDateOnlyValue_UsesDateDbType()
    {
        var value = new DateOnly(2026, 7, 8);

        var parameter = GraphSqlParameter.Create("@date", value);

        Assert.AreEqual(value, parameter.Value);
        Assert.AreEqual(DbType.Date, parameter.DbType);
    }

    [TestMethod]
    public void Create_WhenTimeOnlyValue_UsesTimeDbType()
    {
        var value = new TimeOnly(9, 30);

        var parameter = GraphSqlParameter.Create("@time", value);

        Assert.AreEqual(value, parameter.Value);
        Assert.AreEqual(DbType.Time, parameter.DbType);
    }

    [TestMethod]
    public void Create_WhenTimeSpanValue_UsesTimeDbType()
    {
        var value = TimeSpan.FromMinutes(90);

        var parameter = GraphSqlParameter.Create("@duration", value);

        Assert.AreEqual(value, parameter.Value);
        Assert.AreEqual(DbType.Time, parameter.DbType);
    }

    [TestMethod]
    public void ToRedactedDebugString_WhenParametersHaveValues_DoesNotExposeParameterValues()
    {
        var query = new GraphSqlQuery
        {
            CommandText = "SELECT * FROM [dbo].[People] WHERE [Name] = @p0"
        };
        query.Parameters.Add(GraphSqlParameter.Create("@p0", "secret-name"));

        var debug = query.ToRedactedDebugString();

        Assert.Contains("SELECT * FROM", debug);
        Assert.Contains("@p0 = <String>", debug);
        Assert.DoesNotContain("secret-name", debug);
    }
}