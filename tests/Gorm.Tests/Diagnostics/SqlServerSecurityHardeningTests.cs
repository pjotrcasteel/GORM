using Gorm.Application.Context;
using Gorm.Application.Querying;
using Gorm.Core.Configuration;
using Gorm.Core.Primitives;
using Gorm.Core.Sets;
using Gorm.Infrastructure.Providers.SqlServer;
using Gorm.Infrastructure.Providers.SqlServer.Helpers;

namespace Gorm.Tests.Diagnostics;

[TestClass]
public sealed class SqlServerSecurityHardeningTests
{
    [TestMethod]
    public void EscapeLikePattern_ValueContainsLikeWildcards_EscapesPatternCharacters()
    {
        var result = SqlGenerationHelpers.EscapeLikePattern("_%[]~");

        Assert.AreEqual("~_~%~[]~~", result);
    }

    [TestMethod]
    public void ToSql_StringContainsValueWithLikeWildcards_EscapesLikePatternAndUsesEscapeClause()
    {
        var ctx = new UnsafeIdentifierGraphContext();
        ctx.UseProvider(SqlServerGraphProvider.Instance);

        var sql = ctx.People.Where(x => x.Name.Contains("_%[]~")).ToSql();

        Assert.Contains("LIKE @p0 ESCAPE '~'", sql.CommandText);
        Assert.AreEqual("%~_~%~[]~~%", sql.Parameters.Single().Value);
    }

    [TestMethod]
    public void ToSql_StringStartsWithValueWithLikeWildcards_EscapesLikePatternAndUsesEscapeClause()
    {
        var ctx = new UnsafeIdentifierGraphContext();
        ctx.UseProvider(SqlServerGraphProvider.Instance);

        var sql = ctx.People.Where(x => x.Name.StartsWith("_%[]~")).ToSql();

        Assert.Contains("LIKE @p0 ESCAPE '~'", sql.CommandText);
        Assert.AreEqual("~_~%~[]~~%", sql.Parameters.Single().Value);
    }

    [TestMethod]
    public void ToSql_StringEndsWithValueWithLikeWildcards_EscapesLikePatternAndUsesEscapeClause()
    {
        var ctx = new UnsafeIdentifierGraphContext();
        ctx.UseProvider(SqlServerGraphProvider.Instance);

        var sql = ctx.People.Where(x => x.Name.EndsWith("_%[]~")).ToSql();

        Assert.Contains("LIKE @p0 ESCAPE '~'", sql.CommandText);
        Assert.AreEqual("%~_~%~[]~~", sql.Parameters.Single().Value);
    }

    [TestMethod]
    public void ToSql_TableMappingContainsClosingBracket_EscapesSchemaAndTableIdentifiers()
    {
        var ctx = new UnsafeIdentifierGraphContext();
        ctx.UseProvider(SqlServerGraphProvider.Instance);

        var sql = ctx.People.Where(x => x.Name == "Alice").ToSql();

        Assert.Contains("[dbo]]secure].[Person]]Audit]", sql.CommandText);
    }

    [TestMethod]
    public void ToSql_StringContainsCapturedMethodCall_ThrowsWithoutInvokingMethod()
    {
        _capturedMethodCallCount = 0;

        var ctx = new UnsafeIdentifierGraphContext();
        ctx.UseProvider(SqlServerGraphProvider.Instance);

        var query = ctx.People.Where(x => x.Name.Contains(ReadUnsafeSearchValue()));

        var exception = Assert.ThrowsExactly<NotSupportedException>(() => query.ToSql());

        Assert.Contains("Evaluate the value before building the query", exception.Message);
        Assert.AreEqual(0, _capturedMethodCallCount);
    }

    private static int _capturedMethodCallCount;

    private static string ReadUnsafeSearchValue()
    {
        _capturedMethodCallCount++;
        return "unsafe";
    }

    private sealed class UnsafeIdentifierGraphContext : GraphContext
    {
        public GraphSet<UnsafeIdentifierPerson> People => Set<UnsafeIdentifierPerson>();

        protected override void OnModelCreating(GraphModelBuilder modelBuilder)
        {
            modelBuilder.Node<UnsafeIdentifierPerson>(node =>
            {
                node.ToTable("Person]Audit", "dbo]secure");
                node.HasKey(x => x.Id);
                node.Property(x => x.Name).HasMaxLength(200);
            });
        }
    }

    private sealed class UnsafeIdentifierPerson : Node
    {
        public string Name { get; set; } = string.Empty;
    }
}