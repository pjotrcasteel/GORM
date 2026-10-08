using Gorm.Application.Context;
using Gorm.Application.Execution;
using Gorm.Application.Execution.InMemory;
using Gorm.Application.Querying;
using Gorm.Tests.TestSupport;

namespace Gorm.Tests.Context;

[TestClass]
public sealed class GraphContextRelationshipMutationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task AddRelationship_Via_Navigation_Adds_Connection_And_Saves()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var alice = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        var proj = new TestProject { Id = Guid.NewGuid(), Title = "Alpha" };
        ctx.Add(alice);
        ctx.Add(proj);
        await ctx.SaveChangesAsync(TestContext.CancellationToken);
        ctx.AcceptAllChanges();

        ctx.AddRelationship(alice, x => x.Projects, proj);
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        var result = await ctx.People.Where(x => x.Id == alice.Id).Outgoing<TestWorksOn, TestProject>().ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(1, result);
        Assert.AreEqual(proj.Id, result[0].Id);
    }

    [TestMethod]
    public void AddRelationship_Throws_For_Null_Context()
    {
        GraphContext ctx = null!;
        var alice = new TestPerson { Id = Guid.NewGuid() };
        var proj = new TestProject { Id = Guid.NewGuid() };
        Assert.ThrowsExactly<ArgumentNullException>(() => ctx.AddRelationship(alice, x => x.Projects, proj));
    }

    [TestMethod]
    public void AddRelationship_Throws_For_Null_Owner()
    {
        var ctx = new TestGraphContext().UseInMemory();
        TestPerson owner = null!;
        var proj = new TestProject { Id = Guid.NewGuid() };
        Assert.ThrowsExactly<ArgumentNullException>(() => ctx.AddRelationship(owner, x => x.Projects, proj));
    }

    [TestMethod]
    public void AddRelationship_Throws_For_Null_Related()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var alice = new TestPerson { Id = Guid.NewGuid() };
        TestProject related = null!;
        Assert.ThrowsExactly<ArgumentNullException>(() => ctx.AddRelationship(alice, x => x.Projects, related));
    }

    [TestMethod]
    public async Task RemoveRelationship_Via_Navigation_Removes_Connection()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var alice = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        var proj = new TestProject { Id = Guid.NewGuid(), Title = "Alpha" };
        ctx.Add(alice);
        ctx.Add(proj);
        ctx.Connect<TestWorksOn, TestPerson, TestProject>(alice, proj);
        await ctx.SaveChangesAsync(TestContext.CancellationToken);
        ctx.AcceptAllChanges();

        ctx.RemoveRelationship(alice, x => x.Projects, proj);
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        var result = await ctx.People.Where(x => x.Id == alice.Id).Outgoing<TestWorksOn, TestProject>().ToListAsync(TestContext.CancellationToken);

        Assert.IsEmpty(result);
    }

    [TestMethod]
    public void RemoveRelationship_Throws_For_Null_Owner()
    {
        var ctx = new TestGraphContext().UseInMemory();
        TestPerson owner = null!;
        var proj = new TestProject { Id = Guid.NewGuid() };
        Assert.ThrowsExactly<ArgumentNullException>(() => ctx.RemoveRelationship(owner, x => x.Projects, proj));
    }

    [TestMethod]
    public void RemoveRelationship_Throws_For_Null_Related()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var alice = new TestPerson { Id = Guid.NewGuid() };
        TestProject related = null!;
        Assert.ThrowsExactly<ArgumentNullException>(() => ctx.RemoveRelationship(alice, x => x.Projects, related));
    }

    [TestMethod]
    public void AddRelationship_Applies_Navigation_Fixup_On_Owner()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var alice = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        var proj = new TestProject { Id = Guid.NewGuid(), Title = "Alpha" };
        ctx.Add(alice);
        ctx.Add(proj);

        ctx.AddRelationship(alice, x => x.Projects, proj);

        Assert.Contains(proj, alice.Projects);
    }
}