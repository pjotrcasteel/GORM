using Gorm.Application.Context.Extensions;
using Gorm.Application.Execution.InMemory;
using Gorm.Tests.TestSupport;

namespace Gorm.Tests.Context;

[TestClass]
public sealed class GraphNavigationFixupTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task LoadRelationship_Applies_Navigation_Fixup_On_Collection()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var alice = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        var proj = new TestProject { Id = Guid.NewGuid(), Title = "Apollo" };

        ctx.Add(alice);
        ctx.Add(proj);
        ctx.Connect<TestWorksOn, TestPerson, TestProject>(alice, proj);
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        await ctx.LoadRelationshipAsync(alice, "Projects", TestContext.CancellationToken);

        Assert.HasCount(1, alice.Projects);
        Assert.IsInstanceOfType<TestProject>(alice.Projects[0]);
    }

    [TestMethod]
    public async Task LoadRelationship_Fills_Existing_Collection_Via_Fixup()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var alice = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        var proj1 = new TestProject { Id = Guid.NewGuid(), Title = "Apollo" };
        var proj2 = new TestProject { Id = Guid.NewGuid(), Title = "Ares" };

        ctx.Add(alice);
        ctx.Add(proj1);
        ctx.Add(proj2);
        ctx.Connect<TestWorksOn, TestPerson, TestProject>(alice, proj1);
        ctx.Connect<TestWorksOn, TestPerson, TestProject>(alice, proj2);
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        await ctx.LoadRelationshipAsync(alice, "Projects", TestContext.CancellationToken);

        Assert.HasCount(2, alice.Projects);
    }

    [TestMethod]
    public async Task LoadRelationship_By_Navigation_Applies_Navigation_Fixup()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var alice = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        var proj = new TestProject { Id = Guid.NewGuid(), Title = "Apollo" };

        ctx.Add(alice);
        ctx.Add(proj);
        ctx.Connect<TestWorksOn, TestPerson, TestProject>(alice, proj);
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        await ctx.LoadRelationshipAsync<TestPerson, TestProject>(alice, x => x.Projects, cancellationToken: TestContext.CancellationToken);

        Assert.HasCount(1, alice.Projects);
    }

    [TestMethod]
    public async Task ApplyNavigationFixup_Via_LoadRelationship_Throws_For_Null_Owner()
    {
        var ctx = new TestGraphContext().UseInMemory();
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => ctx.LoadRelationshipAsync(null!, "Projects", TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ApplyNavigationFixup_Via_Navigation_Throws_For_Null_NavigationExpression()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var alice = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => ctx.LoadRelationshipAsync<TestPerson, TestProject>(
                alice,
                (System.Linq.Expressions.Expression<System.Func<TestPerson, System.Collections.Generic.IEnumerable<TestProject>>>)null!,
                cancellationToken: TestContext.CancellationToken
            ));
    }
}