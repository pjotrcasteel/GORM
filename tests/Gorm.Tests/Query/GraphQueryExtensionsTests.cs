using Gorm.Application.Execution;
using Gorm.Application.Execution.InMemory;
using Gorm.Application.Querying;
using Gorm.Tests.TestSupport;

namespace Gorm.Tests.Query;

[TestClass]
public sealed class GraphQueryExtensionsTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void AsNoTracking_Throws_For_Null_Source()
    {
        IQueryable<TestPerson> query = null!;
        Assert.ThrowsExactly<ArgumentNullException>(() => query.AsNoTracking());
    }

    [TestMethod]
    public void AsTracking_Throws_For_Null_Source()
    {
        IQueryable<TestPerson> query = null!;
        Assert.ThrowsExactly<ArgumentNullException>(() => query.AsTracking());
    }

    [TestMethod]
    public void Include_Collection_Throws_For_Null_Source()
    {
        IQueryable<TestPerson> query = null!;
        Assert.ThrowsExactly<ArgumentNullException>(() => query.Include(x => x.Projects));
    }

    [TestMethod]
    public void Include_Collection_Throws_For_Null_Expression()
    {
        var ctx = new TestGraphContext();
        System.Linq.Expressions.Expression<Func<TestPerson, IEnumerable<TestProject>>> expr = null!;
        Assert.ThrowsExactly<ArgumentNullException>(() => ctx.People.Include(expr));
    }

    [TestMethod]
    public void Include_Collection_With_Configure_Throws_For_Null_Source()
    {
        IQueryable<TestPerson> query = null!;
        Assert.ThrowsExactly<ArgumentNullException>(() => query.Include(x => x.Projects, x => x));
    }

    [TestMethod]
    public void Include_Collection_With_Configure_Throws_For_Null_Configure()
    {
        var ctx = new TestGraphContext();
        Assert.ThrowsExactly<ArgumentNullException>(() => ctx.People.Include(x => x.Projects, null!));
    }

    [TestMethod]
    public async Task IncludeRelationship_ByName_Loads_Related_Nodes()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var person = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        var project = new TestProject { Id = Guid.NewGuid(), Title = "Apollo" };
        ctx.Add(person);
        ctx.Add(project);
        ctx.Connect<TestWorksOn, TestPerson, TestProject>(person, project);
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        var result = await ctx.People.Where(x => x.Id == person.Id).IncludeRelationship("Projects").ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(1, result);
        Assert.HasCount(1, result[0].Projects);
    }

    [TestMethod]
    public void IncludeRelationship_ByName_Throws_For_Null_Source()
    {
        IQueryable<TestPerson> query = null!;
        Assert.ThrowsExactly<ArgumentNullException>(() => query.IncludeRelationship("Projects"));
    }

    [TestMethod]
    public void IncludeRelationshipWithEdges_ByName_Throws_For_Null_Source()
    {
        IQueryable<TestPerson> query = null!;
        Assert.ThrowsExactly<ArgumentNullException>(() => query.IncludeRelationshipWithEdges("Projects"));
    }

    [TestMethod]
    public async Task IncludeRelationship_ByNavigation_Loads_Related_Nodes()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var person = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        var project = new TestProject { Id = Guid.NewGuid(), Title = "Apollo" };
        ctx.Add(person);
        ctx.Add(project);
        ctx.Connect<TestWorksOn, TestPerson, TestProject>(person, project);
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        var result = await ctx.People.Where(x => x.Id == person.Id).IncludeRelationship(x => x.Projects).ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(1, result);
        Assert.HasCount(1, result[0].Projects);
    }

    [TestMethod]
    public void IncludeRelationship_ByNavigation_Throws_For_Null_Source()
    {
        IQueryable<TestPerson> query = null!;
        Assert.ThrowsExactly<ArgumentNullException>(() => query.IncludeRelationship(x => x.Projects));
    }

    [TestMethod]
    public void IncludeRelationshipWithEdges_ByNavigation_Throws_For_Null_Source()
    {
        IQueryable<TestPerson> query = null!;
        Assert.ThrowsExactly<ArgumentNullException>(() => query.IncludeRelationshipWithEdges(x => x.Projects));
    }

    [TestMethod]
    public void IncludeRelationship_ByNavigation_With_Configure_Throws_For_Null_Source()
    {
        IQueryable<TestPerson> query = null!;
        Assert.ThrowsExactly<ArgumentNullException>(() => query.IncludeRelationship(x => x.Projects, x => x));
    }

    [TestMethod]
    public void IncludeRelationshipWithEdges_ByNavigation_With_Configure_Throws_For_Null_Source()
    {
        IQueryable<TestPerson> query = null!;
        Assert.ThrowsExactly<ArgumentNullException>(() => query.IncludeRelationshipWithEdges(x => x.Projects, x => x));
    }

    [TestMethod]
    public async Task Distinct_Deduplicates_Results()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var id = Guid.NewGuid();
        var person = new TestPerson { Id = id, Name = "Alice" };
        ctx.Add(person);
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        var result = await ctx.People.Where(x => x.Id == id).Distinct().ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(1, result);
    }

    [TestMethod]
    public async Task Select_Projects_To_Different_Type()
    {
        var ctx = new TestGraphContext().UseInMemory();
        ctx.Add(new TestPerson { Id = Guid.NewGuid(), Name = "Alice" });
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        var names = await ctx.People.Select(x => x.Name).ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(1, names);
        Assert.AreEqual("Alice", names[0]);
    }

    [TestMethod]
    public async Task Where_Filters_Results()
    {
        var ctx = new TestGraphContext().UseInMemory();
        ctx.Add(new TestPerson { Id = Guid.NewGuid(), Name = "Alice", Age = 30 });
        ctx.Add(new TestPerson { Id = Guid.NewGuid(), Name = "Bob", Age = 20 });
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        var result = await ctx.People.Where(x => x.Age > 25).ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(1, result);
        Assert.AreEqual("Alice", result[0].Name);
    }

    [TestMethod]
    public async Task OrderBy_Sorts_Ascending()
    {
        var ctx = new TestGraphContext().UseInMemory();
        ctx.Add(new TestPerson { Id = Guid.NewGuid(), Name = "Charlie", Age = 30 });
        ctx.Add(new TestPerson { Id = Guid.NewGuid(), Name = "Alice", Age = 25 });
        ctx.Add(new TestPerson { Id = Guid.NewGuid(), Name = "Bob", Age = 35 });
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        var result = await ctx.People.OrderBy(x => x.Name).ToListAsync(TestContext.CancellationToken);

        Assert.AreEqual("Alice", result[0].Name);
        Assert.AreEqual("Bob", result[1].Name);
        Assert.AreEqual("Charlie", result[2].Name);
    }

    [TestMethod]
    public async Task OrderByDescending_Sorts_Descending()
    {
        var ctx = new TestGraphContext().UseInMemory();
        ctx.Add(new TestPerson { Id = Guid.NewGuid(), Name = "Alice", Age = 25 });
        ctx.Add(new TestPerson { Id = Guid.NewGuid(), Name = "Bob", Age = 35 });
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        var result = await ctx.People.OrderByDescending(x => x.Age).ToListAsync(TestContext.CancellationToken);

        Assert.AreEqual("Bob", result[0].Name);
    }

    [TestMethod]
    public async Task Skip_Skips_Items()
    {
        var ctx = new TestGraphContext().UseInMemory();
        ctx.Add(new TestPerson { Id = Guid.NewGuid(), Name = "Alice", Age = 25 });
        ctx.Add(new TestPerson { Id = Guid.NewGuid(), Name = "Bob", Age = 35 });
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        var result = await ctx.People.OrderBy(x => x.Name).Skip(1).ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(1, result);
        Assert.AreEqual("Bob", result[0].Name);
    }

    [TestMethod]
    public async Task Take_Limits_Items()
    {
        var ctx = new TestGraphContext().UseInMemory();
        ctx.Add(new TestPerson { Id = Guid.NewGuid(), Name = "Alice" });
        ctx.Add(new TestPerson { Id = Guid.NewGuid(), Name = "Bob" });
        ctx.Add(new TestPerson { Id = Guid.NewGuid(), Name = "Charlie" });
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        var result = await ctx.People.Take(2).ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(2, result);
    }
}