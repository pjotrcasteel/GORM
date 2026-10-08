using Gorm.Application.Context;
using Gorm.Application.Querying;
using Gorm.Core.Configuration;
using Gorm.Core.Primitives;
using Gorm.Infrastructure.Providers.SqlServer;

namespace Gorm.Tests.Query;

[TestClass]
public sealed class GraphQueryProviderCacheTests
{
    [TestMethod]
    public void Translate_CapturedWherePredicateWithAsNoTrackingCalledTwice_DoesNotReuseFirstCapturedValue()
    {
        var context = new CachedPredicateTestGraphContext();
        var source = context.Set<CachedPredicateTestNode>();

        var firstQuery = BuildQuery(source, "first");
        var firstModel = GraphQueryProvider.Translate(firstQuery.Expression);
        var firstSql = SqlServerGraphQuerySqlGenerator.Generate(context.Model, firstModel);

        Assert.AreEqual("first", firstSql.Parameters.Single().Value);

        var secondQuery = BuildQuery(source, "second");
        var secondModel = GraphQueryProvider.Translate(secondQuery.Expression);
        var secondSql = SqlServerGraphQuerySqlGenerator.Generate(context.Model, secondModel);

        Assert.AreEqual("second", secondSql.Parameters.Single().Value);
    }

    private static IQueryable<CachedPredicateTestNode> BuildQuery(IQueryable<CachedPredicateTestNode> source, string externalId) =>
        source
            .Where(x => x.ExternalId == externalId)
            .AsNoTracking();

    private sealed class CachedPredicateTestGraphContext : GraphContext
    {
        protected override void OnModelCreating(GraphModelBuilder modelBuilder)
        {
            modelBuilder.Node<CachedPredicateTestNode>(node =>
            {
                node.ToTable("CachedPredicateTestNodes");
                node.HasKey(x => x.Id);
                node.Property(x => x.ExternalId);
            });
        }
    }

    private sealed class CachedPredicateTestNode : Node
    {
        public string ExternalId { get; set; } = string.Empty;
    }
}