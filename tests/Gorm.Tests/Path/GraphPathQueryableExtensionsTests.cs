using Gorm.Application.Execution;
using Gorm.Application.Execution.InMemory;
using Gorm.Application.Querying;
using Gorm.Application.Querying.Models;
using Gorm.Core.Paths;
using Gorm.Tests.TestSupport;

namespace Gorm.Tests.Path;

[TestClass]
public sealed class GraphPathQueryableExtensionsTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Path_WithSingleOutgoingStep_Returns_TargetNodes()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var alice = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        var project = new TestProject { Id = Guid.NewGuid(), Title = "Apollo" };
        ctx.Add(alice);
        ctx.Add(project);
        ctx.Connect<TestWorksOn, TestPerson, TestProject>(alice, project);
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        var path = GraphPath.From<TestPerson>().Outgoing<TestWorksOn, TestProject>();

        var result = await ctx.People.Where(x => x.Id == alice.Id).Path(path).ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(1, result);
        Assert.AreEqual(project.Id, result[0].Id);
    }

    [TestMethod]
    public async Task Path_WithBuildDelegate_Returns_TargetNodes()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var alice = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        var project = new TestProject { Id = Guid.NewGuid(), Title = "Apollo" };
        ctx.Add(alice);
        ctx.Add(project);
        ctx.Connect<TestWorksOn, TestPerson, TestProject>(alice, project);
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        var result = await ctx.People.Where(x => x.Id == alice.Id).Path(root => root.Outgoing<TestWorksOn, TestProject>()).ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(1, result);
    }

    [TestMethod]
    public async Task Path_WithDistinctResults_Removes_Duplicates()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var alice = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        var bob = new TestPerson { Id = Guid.NewGuid(), Name = "Bob" };
        var project = new TestProject { Id = Guid.NewGuid(), Title = "Apollo" };
        ctx.Add(alice);
        ctx.Add(bob);
        ctx.Add(project);
        ctx.Connect<TestWorksOn, TestPerson, TestProject>(alice, project);
        ctx.Connect<TestWorksOn, TestPerson, TestProject>(bob, project);
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        var path = GraphPath.From<TestPerson>().Outgoing<TestWorksOn, TestProject>().DistinctResults();

        var result = await ctx.People.Path(path).ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(1, result);
    }

    [TestMethod]
    public async Task Path_WithEmptyPathAndSameType_Returns_Source()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var alice = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        ctx.Add(alice);
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        var path = GraphPath.From<TestPerson>();

        var result = await ctx.People.Path(path).ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(1, result);
    }

    [TestMethod]
    public void Path_Throws_For_Null_Source()
    {
        IQueryable<TestPerson> source = null!;
        var path = GraphPath.From<TestPerson>();
        Assert.ThrowsExactly<ArgumentNullException>(() => source.Path(path));
    }

    [TestMethod]
    public void Path_Throws_For_Null_Path()
    {
        var ctx = new TestGraphContext().UseInMemory();
        Assert.ThrowsExactly<ArgumentNullException>(() => ctx.People.Path((GraphPath<TestPerson, TestPerson>)null!));
    }

    [TestMethod]
    public void Path_Builder_Throws_For_Null_BuildDelegate()
    {
        var ctx = new TestGraphContext().UseInMemory();
        Assert.ThrowsExactly<ArgumentNullException>(
            () => ctx.People.Path<TestPerson, TestProject>((Func<GraphPath<TestPerson, TestPerson>, GraphPath<TestPerson, TestProject>>)null!));
    }

    [TestMethod]
    public async Task Path_WithSafety_Throws_NotSupported_InMemory()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var alice = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        var project = new TestProject { Id = Guid.NewGuid(), Title = "Apollo" };
        ctx.Add(alice);
        ctx.Add(project);
        ctx.Connect<TestWorksOn, TestPerson, TestProject>(alice, project);
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        var path = GraphPath.From<TestPerson>().Outgoing<TestWorksOn, TestProject>(GraphTraversalSafeties.PreventImmediateCycles);

        await Assert.ThrowsExactlyAsync<NotSupportedException>(() => ctx.People.Path(path).ToListAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task Path_WithPredicateAndSafety_Throws_NotSupported_InMemory()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var alice = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        var project = new TestProject { Id = Guid.NewGuid(), Title = "Apollo" };
        ctx.Add(alice);
        ctx.Add(project);
        ctx.Connect<TestWorksOn, TestPerson, TestProject>(alice, project, e => e.Role = "Dev");
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        var path = GraphPath.From<TestPerson>().Outgoing<TestWorksOn, TestProject>(e => e.Role == "Dev", GraphTraversalSafeties.PreventNodeRevisit);

        await Assert.ThrowsExactlyAsync<NotSupportedException>(() => ctx.People.Path(path).ToListAsync(TestContext.CancellationToken));
    }
}