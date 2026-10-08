using Gorm.Core.Loading;
using Gorm.Tests.TestSupport;

namespace Gorm.Tests.Loading;

[TestClass]
public sealed class GraphRelationshipIncludeBuilderTests
{
    [TestMethod]
    public void Build_returns_all_accumulated_configuration()
    {
        var result = new GraphRelationshipIncludeBuilder<TestProject>("Projects", GraphIncludeNameKind.Relationship, includeEdge: true)
            .Where(x => x.Title.StartsWith('A'))
            .WhereEdge<TestWorksOn>(x => x.Role == "Lead")
            .OrderBy(x => x.Title)
            .OrderByDescending(x => x.Title)
            .OrderByEdge<TestWorksOn, string>(x => x.Role)
            .OrderByEdgeDescending<TestWorksOn, string>(x => x.Role)
            .Skip(3)
            .Take(4)
            .Build();

        Assert.AreEqual("Projects", result.Name);
        Assert.AreEqual(GraphIncludeNameKind.Relationship, result.NameKind);
        Assert.IsTrue(result.IncludeEdge);
        Assert.IsNotNull(result.RelatedPredicate);
        Assert.IsNotNull(result.EdgePredicate);
        Assert.IsNotNull(result.RelatedOrderBy);
        Assert.IsTrue(result.RelatedOrderDescending);
        Assert.IsNotNull(result.EdgeOrderBy);
        Assert.IsTrue(result.EdgeOrderDescending);
        Assert.AreEqual(3, result.Skip);
        Assert.AreEqual(4, result.Take);
    }

    [TestMethod]
    public void Constructor_throws_for_blank_name()
    {
        var ex = Assert.ThrowsExactly<ArgumentException>(() => new GraphRelationshipIncludeBuilder<TestProject>(" ", GraphIncludeNameKind.Relationship, includeEdge: false));

        Assert.Contains("Include name cannot be null or whitespace", ex.Message);
    }

    [TestMethod]
    public void Where_throws_for_null_predicate()
    {
        var builder = new GraphRelationshipIncludeBuilder<TestProject>("Projects", GraphIncludeNameKind.Relationship, includeEdge: false);

        Assert.ThrowsExactly<ArgumentNullException>(() => builder.Where(null!));
    }

    [TestMethod]
    public void WhereEdge_throws_for_null_predicate()
    {
        var builder = new GraphRelationshipIncludeBuilder<TestProject>("Projects", GraphIncludeNameKind.Relationship, includeEdge: true);

        Assert.ThrowsExactly<ArgumentNullException>(() => builder.WhereEdge<TestWorksOn>(null!));
    }

    [TestMethod]
    public void Order_methods_throw_for_null_selector()
    {
        var builder = new GraphRelationshipIncludeBuilder<TestProject>("Projects", GraphIncludeNameKind.Relationship, includeEdge: true);

        Assert.ThrowsExactly<ArgumentNullException>(() => builder.OrderBy<string>(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => builder.OrderByDescending<string>(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => builder.OrderByEdge<TestWorksOn, string>(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => builder.OrderByEdgeDescending<TestWorksOn, string>(null!));
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(-5)]
    public void Skip_throws_for_negative_count(int count)
    {
        var builder = new GraphRelationshipIncludeBuilder<TestProject>("Projects", GraphIncludeNameKind.Relationship, includeEdge: false);

        var ex = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => builder.Skip(count));

        Assert.Contains("Skip count cannot be negative", ex.Message);
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(-10)]
    public void Take_throws_for_negative_count(int count)
    {
        var builder = new GraphRelationshipIncludeBuilder<TestProject>("Projects", GraphIncludeNameKind.Relationship, includeEdge: false);

        var ex = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => builder.Take(count));

        Assert.Contains("Take count cannot be negative", ex.Message);
    }
}