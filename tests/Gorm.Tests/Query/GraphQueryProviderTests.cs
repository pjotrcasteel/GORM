using System.Linq.Expressions;
using Gorm.Application.Execution.InMemory;
using Gorm.Application.Querying;
using Gorm.Tests.TestSupport;

namespace Gorm.Tests.Query;

[TestClass]
public sealed class GraphQueryProviderTests
{
    [TestMethod]
    public void Constructor_Throws_For_Null_Context()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new GraphQueryProvider(null!));
    }

    [TestMethod]
    public void CreateQuery_Throws_For_Null_Expression()
    {
        var provider = new GraphQueryProvider(new TestGraphContext());
        Assert.ThrowsExactly<ArgumentNullException>(() => provider.CreateQuery(null!));
    }

    [TestMethod]
    public void CreateQuery_Generic_Throws_For_Null_Expression()
    {
        var provider = new GraphQueryProvider(new TestGraphContext());
        Assert.ThrowsExactly<ArgumentNullException>(() => provider.CreateQuery<TestPerson>(null!));
    }

    [TestMethod]
    public void CreateQuery_Returns_GraphQueryable_For_Expression()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var provider = (GraphQueryProvider)ctx.People.Provider;

        var query = provider.CreateQuery(ctx.People.Expression);

        Assert.IsInstanceOfType<GraphQueryable<TestPerson>>(query);
    }

    [TestMethod]
    public void Execute_Object_Throws_For_Null_Expression()
    {
        var provider = new GraphQueryProvider(new TestGraphContext());
        Assert.ThrowsExactly<ArgumentNullException>(() => provider.Execute(null!));
    }

    [TestMethod]
    public void Execute_Generic_Throws_For_Null_Expression()
    {
        var provider = new GraphQueryProvider(new TestGraphContext());
        Assert.ThrowsExactly<ArgumentNullException>(() => provider.Execute<int>(null!));
    }

    [TestMethod]
    public void Execute_Generic_Throws_NotSupported_For_Unsupported_Expression()
    {
        var provider = new GraphQueryProvider(new TestGraphContext());
        var expression = Expression.Constant(42);

        Assert.ThrowsExactly<NotSupportedException>(() => provider.Execute<int>(expression));
    }

    [TestMethod]
    public void Translate_Throws_For_Null_Expression()
    {
        var provider = new GraphQueryProvider(new TestGraphContext());
        Assert.ThrowsExactly<ArgumentNullException>(() => GraphQueryProvider.Translate(null!));
    }
}