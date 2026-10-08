using Gorm.Application.Execution;
using Gorm.Application.Execution.InMemory;
using Gorm.Application.Querying;
using Gorm.Tests.TestSupport;

namespace Gorm.Tests.Query;

[TestClass]
public sealed class GraphQueryTranslatorTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Outgoing_Traversal_Returns_Related_Nodes()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var alice = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        var proj = new TestProject { Id = Guid.NewGuid(), Title = "Alpha" };
        ctx.Add(alice);
        ctx.Add(proj);
        ctx.Connect<TestWorksOn, TestPerson, TestProject>(alice, proj);
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        var result = await ctx.People.Where(x => x.Id == alice.Id).Outgoing<TestWorksOn, TestProject>().ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(1, result);
        Assert.AreEqual(proj.Id, result[0].Id);
    }

    [TestMethod]
    public async Task Incoming_Traversal_Returns_Related_Nodes()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var alice = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        var proj = new TestProject { Id = Guid.NewGuid(), Title = "Beta" };
        ctx.Add(alice);
        ctx.Add(proj);
        ctx.Connect<TestWorksOn, TestPerson, TestProject>(alice, proj);
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        var result = await ctx.Projects.Where(x => x.Id == proj.Id).Incoming<TestWorksOn, TestPerson>().ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(1, result);
        Assert.AreEqual(alice.Id, result[0].Id);
    }

    [TestMethod]
    public async Task Outgoing_With_Predicate_Filters_Related_Nodes()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var alice = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        var proj1 = new TestProject { Id = Guid.NewGuid(), Title = "Alpha" };
        var proj2 = new TestProject { Id = Guid.NewGuid(), Title = "Beta" };
        ctx.Add(alice);
        ctx.Add(proj1);
        ctx.Add(proj2);
        ctx.Connect<TestWorksOn, TestPerson, TestProject>(alice, proj1, e => e.Role = "Dev");
        ctx.Connect<TestWorksOn, TestPerson, TestProject>(alice, proj2, e => e.Role = "PM");
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        var result = await ctx.People.Where(x => x.Id == alice.Id).Outgoing<TestWorksOn, TestProject>(x => x.Role == "Dev").ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(1, result);
    }

    [TestMethod]
    public async Task ThenOutgoing_Traversal_Returns_Second_Level_Nodes()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var alice = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        var bob = new TestPerson { Id = Guid.NewGuid(), Name = "Bob" };
        var proj = new TestProject { Id = Guid.NewGuid(), Title = "Alpha" };

        ctx.Add(alice);
        ctx.Add(bob);
        ctx.Add(proj);
        ctx.Connect<TestKnows, TestPerson, TestPerson>(alice, bob);
        ctx.Connect<TestWorksOn, TestPerson, TestProject>(bob, proj);
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        var result = await ctx.People
            .Where(x => x.Id == alice.Id)
            .Outgoing<TestKnows, TestPerson>()
            .ThenOutgoing<TestWorksOn, TestProject>()
            .ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(1, result);
        Assert.AreEqual(proj.Id, result[0].Id);
    }

    [TestMethod]
    public async Task OrderBy_Ascending_Sorts_Results()
    {
        var ctx = new TestGraphContext().UseInMemory();
        ctx.Add(new TestPerson { Id = Guid.NewGuid(), Name = "Zara" });
        ctx.Add(new TestPerson { Id = Guid.NewGuid(), Name = "Alice" });
        ctx.Add(new TestPerson { Id = Guid.NewGuid(), Name = "Mike" });
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        var result = await ctx.People.OrderBy(x => x.Name).ToListAsync(TestContext.CancellationToken);

        Assert.AreEqual("Alice", result[0].Name);
        Assert.AreEqual("Mike", result[1].Name);
        Assert.AreEqual("Zara", result[2].Name);
    }

    [TestMethod]
    public async Task OrderByDescending_Sorts_Results()
    {
        var ctx = new TestGraphContext().UseInMemory();
        ctx.Add(new TestPerson { Id = Guid.NewGuid(), Name = "Zara" });
        ctx.Add(new TestPerson { Id = Guid.NewGuid(), Name = "Alice" });
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        var result = await ctx.People.OrderByDescending(x => x.Name).ToListAsync(TestContext.CancellationToken);

        Assert.AreEqual("Zara", result[0].Name);
        Assert.AreEqual("Alice", result[1].Name);
    }

    [TestMethod]
    public async Task ThenBy_Applies_Secondary_Sort()
    {
        var ctx = new TestGraphContext().UseInMemory();
        ctx.Add(new TestPerson { Id = Guid.NewGuid(), Name = "Alice", Age = 30 });
        ctx.Add(new TestPerson { Id = Guid.NewGuid(), Name = "Alice", Age = 20 });
        ctx.Add(new TestPerson { Id = Guid.NewGuid(), Name = "Bob", Age = 25 });
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        var result = await ctx.People.OrderBy(x => x.Name).ThenBy(x => x.Age).ToListAsync(TestContext.CancellationToken);

        Assert.AreEqual("Alice", result[0].Name);
        Assert.AreEqual(20, result[0].Age);
    }

    [TestMethod]
    public async Task ThenByDescending_Applies_Secondary_Sort()
    {
        var ctx = new TestGraphContext().UseInMemory();
        ctx.Add(new TestPerson { Id = Guid.NewGuid(), Name = "Alice", Age = 20 });
        ctx.Add(new TestPerson { Id = Guid.NewGuid(), Name = "Alice", Age = 30 });
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        var result = await ctx.People.OrderBy(x => x.Name).ThenByDescending(x => x.Age).ToListAsync(TestContext.CancellationToken);

        Assert.AreEqual(30, result[0].Age);
    }

    [TestMethod]
    public async Task Distinct_Returns_Unique_Results()
    {
        var ctx = new TestGraphContext().UseInMemory();
        ctx.Add(new TestPerson { Id = Guid.NewGuid(), Name = "Alice" });
        ctx.Add(new TestPerson { Id = Guid.NewGuid(), Name = "Bob" });
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        var result = await ctx.People.Distinct().ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(2, result);
    }

    [TestMethod]
    public async Task AsNoTracking_Query_Does_Not_Track_Entities()
    {
        var store = new InMemoryGraphStore();
        var seedCtx = new TestGraphContext().UseInMemory(store);
        seedCtx.Add(new TestPerson { Id = Guid.NewGuid(), Name = "Alice" });
        await seedCtx.SaveChangesAsync(TestContext.CancellationToken);

        var ctx = new TestGraphContext().UseInMemory(store);
        var result = await ctx.People.AsNoTracking().ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(1, result);
        Assert.IsEmpty(ctx.ChangeTracker.Entries);
    }

    [TestMethod]
    public async Task AsTracking_Query_Tracks_Entities()
    {
        var ctx = new TestGraphContext().UseInMemory();
        ctx.Add(new TestPerson { Id = Guid.NewGuid(), Name = "Alice" });
        await ctx.SaveChangesAsync(TestContext.CancellationToken);
        ctx.AcceptAllChanges();

        var result = await ctx.People.AsTracking().ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(1, result);
        Assert.IsNotEmpty(ctx.ChangeTracker.Entries);
    }

    [TestMethod]
    public async Task Skip_And_Take_Paginate_Results()
    {
        var ctx = new TestGraphContext().UseInMemory();
        for (var i = 1; i <= 5; i++)
        {
            ctx.Add(new TestPerson { Id = Guid.NewGuid(), Name = $"Person{i:D2}", Age = i });
        }

        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        var result = await ctx.People.OrderBy(x => x.Name).Skip(2).Take(2).ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(2, result);
        Assert.AreEqual("Person03", result[0].Name);
        Assert.AreEqual("Person04", result[1].Name);
    }
}