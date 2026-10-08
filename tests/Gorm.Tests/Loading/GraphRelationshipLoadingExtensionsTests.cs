using Gorm.Application.Context;
using Gorm.Application.Context.Extensions;
using Gorm.Application.Execution.InMemory;
using Gorm.Core.Loading;
using Gorm.Tests.TestSupport;

namespace Gorm.Tests.Loading;

[TestClass]
public sealed class GraphRelationshipLoadingExtensionsTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task LoadRelationshipAsync_ByName_Returns_Related_Nodes()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var person = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        var project = new TestProject { Id = Guid.NewGuid(), Title = "Apollo" };

        ctx.Add(person);
        ctx.Add(project);
        ctx.Connect<TestWorksOn, TestPerson, TestProject>(person, project);
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        var loaded = await ctx.LoadRelationshipAsync(person, "Projects", TestContext.CancellationToken);

        Assert.HasCount(1, loaded);
        Assert.IsInstanceOfType<TestProject>(loaded[0]);
    }

    [TestMethod]
    public async Task LoadRelationshipAsync_ByName_Returns_Empty_When_No_Relations()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var person = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        ctx.Add(person);
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        var loaded = await ctx.LoadRelationshipAsync(person, "Projects", TestContext.CancellationToken);

        Assert.IsEmpty(loaded);
    }

    [TestMethod]
    public async Task LoadRelationshipAsync_ByName_Throws_For_Null_Context()
    {
        GraphContext ctx = null!;
        var person = new TestPerson { Id = Guid.NewGuid() };
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => ctx.LoadRelationshipAsync(person, "Projects", TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task LoadRelationshipAsync_ByName_Throws_For_Null_Owner()
    {
        var ctx = new TestGraphContext().UseInMemory();
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => ctx.LoadRelationshipAsync((TestPerson)null!, "Projects", TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task LoadRelationshipAsync_ByNavigation_Returns_Related_Nodes()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var person = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        var project = new TestProject { Id = Guid.NewGuid(), Title = "Apollo" };

        ctx.Add(person);
        ctx.Add(project);
        ctx.Connect<TestWorksOn, TestPerson, TestProject>(person, project);
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        var loaded = await ctx.LoadRelationshipAsync(person, p => p.Projects, cancellationToken: TestContext.CancellationToken);

        Assert.HasCount(1, loaded);
    }

    [TestMethod]
    public async Task LoadRelationshipAsync_ByNavigation_Throws_For_Null_Context()
    {
        GraphContext ctx = null!;
        var person = new TestPerson { Id = Guid.NewGuid() };
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => ctx.LoadRelationshipAsync(person, p => p.Projects, cancellationToken: TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task LoadRelationshipAsync_ByNavigation_Throws_For_Null_Owner()
    {
        var ctx = new TestGraphContext().UseInMemory();
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => ctx.LoadRelationshipAsync((TestPerson)null!, p => p.Projects, cancellationToken: TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task LoadRelationshipAsync_ByNavigation_Throws_For_Null_Expression()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var person = new TestPerson { Id = Guid.NewGuid() };
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => ctx.LoadRelationshipAsync(
                person,
                (System.Linq.Expressions.Expression<Func<TestPerson, IEnumerable<TestProject>>>)null!, cancellationToken: TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task LoadRelationshipAsync_WithRequest_Returns_Loaded()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var person = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        var project = new TestProject { Id = Guid.NewGuid(), Title = "Apollo" };

        ctx.Add(person);
        ctx.Add(project);
        ctx.Connect<TestWorksOn, TestPerson, TestProject>(person, project);
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        var request = new GraphIncludeRequest
        {
            Name = "Projects",
            NameKind = GraphIncludeNameKind.Relationship,
            IncludeEdge = false
        };

        var loaded = await ctx.LoadRelationshipAsync(person, request, TestContext.CancellationToken);

        Assert.HasCount(1, loaded);
    }

    [TestMethod]
    public async Task AddRelationship_Creates_Connection_And_Fixup()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var person = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        var project = new TestProject { Id = Guid.NewGuid(), Title = "Apollo" };
        ctx.Add(person);
        ctx.Add(project);
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        ctx.AddRelationship(person, p => p.Projects, project);
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        var loaded = await ctx.LoadRelationshipAsync(person, p => p.Projects, cancellationToken: TestContext.CancellationToken);

        Assert.HasCount(1, loaded);
    }

    [TestMethod]
    public void AddRelationship_Throws_For_Null_Context()
    {
        GraphContext ctx = null!;
        var person = new TestPerson { Id = Guid.NewGuid() };
        var project = new TestProject { Id = Guid.NewGuid() };
        Assert.ThrowsExactly<ArgumentNullException>(() => ctx.AddRelationship(person, p => p.Projects, project));
    }

    [TestMethod]
    public void RemoveRelationship_Throws_For_Null_Context()
    {
        GraphContext ctx = null!;
        var person = new TestPerson { Id = Guid.NewGuid() };
        var project = new TestProject { Id = Guid.NewGuid() };
        Assert.ThrowsExactly<ArgumentNullException>(() => ctx.RemoveRelationship(person, p => p.Projects, project));
    }
}