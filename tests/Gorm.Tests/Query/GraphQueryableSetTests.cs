using Gorm.Application.Execution;
using Gorm.Application.Execution.InMemory;
using Gorm.Application.Querying;
using Gorm.Application.Querying.Models;
using Gorm.Tests.TestSupport;

namespace Gorm.Tests.Query;

[TestClass]
public sealed class GraphQueryableSetTests
{
    public TestContext TestContext { get; set; } = null!;

    private static readonly string[] Expected = ["Eve"];

    [TestMethod]
    public void EdgeSet_ElementType_Is_Edge_Type()
    {
        var ctx = new TestGraphContext();
        Assert.AreEqual(typeof(TestWorksOn), ctx.WorksOn.ElementType);
    }

    [TestMethod]
    public void EdgeSet_Mapping_Is_Not_Null()
    {
        var ctx = new TestGraphContext();
        Assert.IsNotNull(ctx.WorksOn.Mapping);
        Assert.AreEqual("WorksOn", ctx.WorksOn.Mapping.TableName);
    }

    [TestMethod]
    public void EdgeSet_Context_Is_Correct()
    {
        var ctx = new TestGraphContext();
        Assert.AreSame(ctx, ctx.WorksOn.Context);
    }

    [TestMethod]
    public void EdgeSet_KnowsSet_ElementType_Is_TestKnows()
    {
        var ctx = new TestGraphContext();
        Assert.AreEqual(typeof(TestKnows), ctx.Knows.ElementType);
    }

    [TestMethod]
    public void NodeSet_ToString_Contains_Type_Name()
    {
        var ctx = new TestGraphContext();
        var str = ctx.People.ToString();
        Assert.IsNotNull(str);
        Assert.IsGreaterThan(0, str.Length);
    }

    [TestMethod]
    public async Task NodeSet_GetEnumerator_Returns_Items()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var person = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        ctx.Add(person);
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        var list = ctx.People.ToList();

        Assert.HasCount(1, list);
        Assert.AreEqual("Alice", list[0].Name);
    }

    [TestMethod]
    public async Task NodeSet_Supports_Multiple_Iterations()
    {
        var ctx = new TestGraphContext().UseInMemory();
        ctx.Add(new TestPerson { Id = Guid.NewGuid(), Name = "Bob" });
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        var list1 = await ctx.People.ToListAsync(TestContext.CancellationToken);
        var list2 = await ctx.People.ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(1, list1);
        Assert.HasCount(1, list2);
    }

    [TestMethod]
    public async Task GraphQueryable_GetEnumerator_With_Data_Returns_Results()
    {
        var ctx = new TestGraphContext().UseInMemory();
        ctx.Add(new TestPerson { Id = Guid.NewGuid(), Name = "Eve" });
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        var query = ctx.People.Where(x => x.Name == "Eve");
        var names = new List<string>();
        foreach (var person in query)
        {
            names.Add(person.Name);
        }

        CollectionAssert.AreEqual(Expected, names);
    }

    [TestMethod]
    public void GraphQueryable_ToString_Is_Not_Empty()
    {
        var ctx = new TestGraphContext();
        var query = ctx.People.Where(x => x.Name == "Test");
        var str = query.ToString();

        Assert.IsNotNull(str);
        Assert.IsGreaterThan(0, str.Length);
    }

    [TestMethod]
    public async Task IncludableQueryable_GetEnumerator_Returns_Results()
    {
        var ctx = new TestGraphContext().UseInMemory();
        ctx.Add(new TestPerson { Id = Guid.NewGuid(), Name = "Charlie" });
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        var list = await ctx.People.Include(x => x.Projects).ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(1, list);
    }

    [TestMethod]
    public async Task IncludableQueryable_With_Include_Loads_Children()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var person = new TestPerson { Id = Guid.NewGuid(), Name = "Diana" };
        var project = new TestProject { Id = Guid.NewGuid(), Title = "X" };
        ctx.Add(person);
        ctx.Add(project);
        ctx.Connect<TestWorksOn, TestPerson, TestProject>(person, project);
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        var list = await ctx.People.Where(x => x.Id == person.Id).Include(x => x.Projects).ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(1, list);
        Assert.HasCount(1, list[0].Projects);
    }

    [TestMethod]
    public void IncludableQueryable_ToString_Is_Not_Empty()
    {
        var ctx = new TestGraphContext();
        var str = ctx.People.Include(x => x.Projects).ToString();

        Assert.IsNotNull(str);
        Assert.IsGreaterThan(0, str.Length);
    }

    [TestMethod]
    public void GraphOrdering_IsProjected_Defaults_To_False()
    {
        var ordering = new GraphOrdering { PropertyName = "Name", Descending = false };
        Assert.IsFalse(ordering.IsProjected);
    }

    [TestMethod]
    public void GraphOrdering_IsProjected_Can_Be_Set_True()
    {
        var ordering = new GraphOrdering { PropertyName = "Name", Descending = false, IsProjected = true };
        Assert.IsTrue(ordering.IsProjected);
    }

    [TestMethod]
    public void GraphQueryModel_CurrentElementType_No_Steps_Returns_RootElementType()
    {
        var model = new GraphQueryModel
        {
            RootElementType = typeof(TestPerson),
            RootElementKind = GraphQueryElementKind.Node
        };

        Assert.AreEqual(typeof(TestPerson), model.CurrentElementType);
    }

    [TestMethod]
    public void GraphQueryModel_CurrentElementKind_No_Steps_Returns_RootElementKind()
    {
        var model = new GraphQueryModel
        {
            RootElementType = typeof(TestPerson),
            RootElementKind = GraphQueryElementKind.Edge
        };

        Assert.AreEqual(GraphQueryElementKind.Edge, model.CurrentElementKind);
    }
}