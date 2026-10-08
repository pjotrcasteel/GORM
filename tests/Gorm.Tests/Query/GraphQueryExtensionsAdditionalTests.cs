using Gorm.Application.Execution;
using Gorm.Application.Execution.InMemory;
using Gorm.Application.Querying;
using Gorm.Application.Querying.Models;
using Gorm.Core.Loading;
using Gorm.Tests.TestSupport;

namespace Gorm.Tests.Query;

[TestClass]
public sealed class GraphQueryExtensionsAdditionalTests
{
    public TestContext TestContext { get; set; } = null!;

    private static TestGraphContext CreatePopulated(out TestPerson alice, out TestProject proj)
    {
        var ctx = new TestGraphContext().UseInMemory();
        alice = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        proj = new TestProject { Id = Guid.NewGuid(), Title = "Apollo" };
        ctx.Add(alice);
        ctx.Add(proj);
        ctx.Connect<TestWorksOn, TestPerson, TestProject>(alice, proj, e => e.Role = "Dev");
        ctx.SaveChangesAsync().GetAwaiter().GetResult();
        return ctx;
    }

    [TestMethod]
    public async Task OutgoingWhere_Returns_Filtered_Related_Nodes()
    {
        var ctx = CreatePopulated(out var alice, out _);

        var result = await ctx.Set<TestPerson>()
            .Where(x => x.Id == alice.Id)
            .OutgoingWhere<TestWorksOn, TestProject>(e => e.Role == "Dev")
            .ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(1, result);
        Assert.IsInstanceOfType<TestProject>(result[0]);
    }

    [TestMethod]
    public async Task OutgoingWhere_Returns_Empty_When_Predicate_Does_Not_Match()
    {
        var ctx = CreatePopulated(out var alice, out _);

        var result = await ctx.Set<TestPerson>()
            .Where(x => x.Id == alice.Id)
            .OutgoingWhere<TestWorksOn, TestProject>(e => e.Role == "Manager")
            .ToListAsync(TestContext.CancellationToken);

        Assert.IsEmpty(result);
    }

    [TestMethod]
    public async Task IncomingWhere_Returns_Filtered_Related_Nodes()
    {
        var ctx = CreatePopulated(out _, out var proj);

        var result = await ctx.Set<TestProject>()
            .Where(x => x.Id == proj.Id)
            .IncomingWhere<TestWorksOn, TestPerson>(e => e.Role == "Dev")
            .ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(1, result);
        Assert.IsInstanceOfType<TestPerson>(result[0]);
    }

    [TestMethod]
    public async Task IncomingWhere_Returns_Empty_When_Predicate_Does_Not_Match()
    {
        var ctx = CreatePopulated(out _, out var proj);

        var result = await ctx.Set<TestProject>()
            .Where(x => x.Id == proj.Id)
            .IncomingWhere<TestWorksOn, TestPerson>(e => e.Role == "Manager")
            .ToListAsync(TestContext.CancellationToken);

        Assert.IsEmpty(result);
    }

    [TestMethod]
    public async Task SelectWithEdge_Returns_Projected_Results()
    {
        var ctx = CreatePopulated(out var alice, out _);

        var result = await ctx.Set<TestPerson>()
            .Where(x => x.Id == alice.Id)
            .Outgoing<TestWorksOn, TestProject>()
            .SelectWithEdge<TestWorksOn, TestProject, GraphRelatedEdgeResult<TestWorksOn, TestProject>>((node, edge) => new GraphRelatedEdgeResult<TestWorksOn, TestProject>
            {
                Node = node,
                Edge = edge
            })
            .ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(1, result);
        Assert.AreEqual("Apollo", result[0].Node.Title);
        Assert.AreEqual("Dev", result[0].Edge.Role);
    }

    [TestMethod]
    public void SelectWithEdge_Throws_For_Null_Source()
    {
        IQueryable<TestProject> source = null!;
        Assert.ThrowsExactly<ArgumentNullException>(() => source.SelectWithEdge<TestWorksOn, TestProject, string>((n, e) => n.Title));
    }

    [TestMethod]
    public void SelectWithEdge_Throws_For_Null_Selector()
    {
        var ctx = CreatePopulated(out var alice, out _);
        var query = ctx.Set<TestPerson>().Where(x => x.Id == alice.Id).Outgoing<TestWorksOn, TestProject>();

        Assert.ThrowsExactly<ArgumentNullException>(() => query.SelectWithEdge<TestWorksOn, TestProject, string>(null!));
    }

    [TestMethod]
    public async Task FromNode_Returns_NotSupported_InMemory_For_EdgeRoot_Query()
    {
        var ctx = CreatePopulated(out _, out var proj);

        await Assert.ThrowsExactlyAsync<NotSupportedException>(
            () => ctx.EdgeSet<TestWorksOn>()
                .Where(e => e.ToId == proj.Id)
                .FromNode<TestPerson>()
                .ToListAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ToNode_Returns_NotSupported_InMemory_For_EdgeRoot_Query()
    {
        var ctx = CreatePopulated(out var alice, out _);

        await Assert.ThrowsExactlyAsync<NotSupportedException>(
            () => ctx.EdgeSet<TestWorksOn>()
                .Where(e => e.FromId == alice.Id)
                .ToNode<TestProject>()
                .ToListAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task Outgoing_WithSafety_Returns_Related_Nodes()
    {
        var ctx = CreatePopulated(out var alice, out _);

        var result = await ctx.Set<TestPerson>()
            .Where(x => x.Id == alice.Id)
            .Outgoing<TestWorksOn, TestProject>(GraphTraversalSafeties.None)
            .ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(1, result);
    }

    [TestMethod]
    public async Task Outgoing_WithPredicateAndSafety_Returns_Related_Nodes()
    {
        var ctx = CreatePopulated(out var alice, out _);

        var result = await ctx.Set<TestPerson>()
            .Where(x => x.Id == alice.Id)
            .Outgoing<TestWorksOn, TestProject>(e => e.Role == "Dev", GraphTraversalSafeties.None)
            .ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(1, result);
    }

    [TestMethod]
    public async Task Incoming_WithSafety_Returns_Related_Nodes()
    {
        var ctx = CreatePopulated(out _, out var proj);

        var result = await ctx.Set<TestProject>()
            .Where(x => x.Id == proj.Id)
            .Incoming<TestWorksOn, TestPerson>(GraphTraversalSafeties.None)
            .ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(1, result);
    }

    [TestMethod]
    public async Task Incoming_WithPredicateAndSafety_Returns_Related_Nodes()
    {
        var ctx = CreatePopulated(out _, out var proj);

        var result = await ctx.Set<TestProject>()
            .Where(x => x.Id == proj.Id)
            .Incoming<TestWorksOn, TestPerson>(e => e.Role == "Dev", GraphTraversalSafeties.None)
            .ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(1, result);
    }

    [TestMethod]
    public async Task ThenOutgoing_Returns_Second_Level_Nodes()
    {
        // Alice --WorksOn--> Project <--WorksOn-- Bob, but here Alice --Knows--> Bob --WorksOn--> Project
        var ctx = new TestGraphContext().UseInMemory();
        var alice = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        var bob = new TestPerson { Id = Guid.NewGuid(), Name = "Bob" };
        var proj = new TestProject { Id = Guid.NewGuid(), Title = "Apollo" };

        ctx.Add(alice);
        ctx.Add(bob);
        ctx.Add(proj);
        ctx.Connect<TestKnows, TestPerson, TestPerson>(alice, bob);
        ctx.Connect<TestWorksOn, TestPerson, TestProject>(bob, proj);
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        var result = await ctx.Set<TestPerson>()
            .Where(x => x.Id == alice.Id)
            .Outgoing<TestKnows, TestPerson>()
            .ThenOutgoing<TestWorksOn, TestProject>()
            .ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(1, result);
        Assert.AreEqual("Apollo", result[0].Title);
    }

    [TestMethod]
    public async Task ThenIncoming_Returns_Incoming_Second_Level()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var alice = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        var bob = new TestPerson { Id = Guid.NewGuid(), Name = "Bob" };
        var proj = new TestProject { Id = Guid.NewGuid(), Title = "Apollo" };

        ctx.Add(alice);
        ctx.Add(bob);
        ctx.Add(proj);
        ctx.Connect<TestKnows, TestPerson, TestPerson>(alice, bob);
        ctx.Connect<TestWorksOn, TestPerson, TestProject>(alice, proj);
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        // bob -> Knows (incoming from alice) -> then project (via incoming worksOn to alice's proj)
        var result = await ctx.Set<TestPerson>()
            .Where(x => x.Id == bob.Id)
            .Incoming<TestKnows, TestPerson>()
            .ThenOutgoing<TestWorksOn, TestProject>()
            .ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(1, result);
    }

    [TestMethod]
    public async Task ThenOutgoing_WithPredicate_Filters_Results()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var alice = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        var bob = new TestPerson { Id = Guid.NewGuid(), Name = "Bob" };
        var proj = new TestProject { Id = Guid.NewGuid(), Title = "Apollo" };

        ctx.Add(alice);
        ctx.Add(bob);
        ctx.Add(proj);
        ctx.Connect<TestKnows, TestPerson, TestPerson>(alice, bob);
        ctx.Connect<TestWorksOn, TestPerson, TestProject>(bob, proj, e => e.Role = "Lead");
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        var result = await ctx.Set<TestPerson>()
            .Where(x => x.Id == alice.Id)
            .Outgoing<TestKnows, TestPerson>()
            .ThenOutgoing<TestWorksOn, TestProject>(e => e.Role == "Lead")
            .ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(1, result);
    }

    [TestMethod]
    public async Task IncludeRelationship_By_Name_Returns_Nodes_With_Navigation_Loaded()
    {
        var ctx = CreatePopulated(out _, out _);

        var people = await ctx.People.IncludeRelationship("Projects").ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(1, people);
        Assert.HasCount(1, people[0].Projects);
    }

    [TestMethod]
    public void IncludeRelationship_By_Name_Throws_For_Null_Source()
    {
        IQueryable<TestPerson> source = null!;
        Assert.ThrowsExactly<ArgumentNullException>(() => source.IncludeRelationship("Projects"));
    }

    [TestMethod]
    public void IncludeRelationship_By_Name_Throws_For_Empty_Name()
    {
        var ctx = new TestGraphContext().UseInMemory();
        Assert.ThrowsExactly<ArgumentException>(() => ctx.People.IncludeRelationship(""));
    }

    [TestMethod]
    public async Task IncludeRelationship_By_Navigation_Returns_Nodes_With_Navigation_Loaded()
    {
        var ctx = CreatePopulated(out _, out _);

        var people = await ctx.People.IncludeRelationship<TestPerson, TestProject>(x => x.Projects).ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(1, people);
        Assert.HasCount(1, people[0].Projects);
    }

    [TestMethod]
    public async Task IncludeRelationship_By_Navigation_With_Configure_Returns_Nodes()
    {
        var ctx = CreatePopulated(out _, out _);

        var people = await ctx.People.IncludeRelationship<TestPerson, TestProject>(x => x.Projects, b => b.OrderBy(x => x.Title)).ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(1, people);
        Assert.HasCount(1, people[0].Projects);
    }

    [TestMethod]
    public async Task IncludeRelationshipWithEdges_Loads_Navigation_With_Edges()
    {
        var ctx = CreatePopulated(out _, out _);

        var people = await ctx.People.IncludeRelationshipWithEdges<TestPerson, TestProject>(x => x.Projects).ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(1, people);
    }

    [TestMethod]
    public void IncludeRelationshipWithEdges_By_Name_Throws_For_Null_Source()
    {
        IQueryable<TestPerson> source = null!;
        Assert.ThrowsExactly<ArgumentNullException>(() => source.IncludeRelationshipWithEdges("Projects"));
    }

    [TestMethod]
    public void IncludeRelationshipWithEdges_By_Name_Throws_For_Empty_Name()
    {
        var ctx = new TestGraphContext().UseInMemory();
        Assert.ThrowsExactly<ArgumentException>(() => ctx.People.IncludeRelationshipWithEdges(""));
    }

    [TestMethod]
    public void ThenInclude_AfterCollection_Throws_For_Null_Source()
    {
        IGraphIncludableQueryable<TestPerson, IEnumerable<TestPerson>> source = null!;
        Assert.ThrowsExactly<ArgumentNullException>(() => source.ThenInclude<TestPerson, TestPerson, TestProject>(x => x.Projects));
    }

    [TestMethod]
    public void ThenInclude_AfterCollection_Throws_For_Null_Expression()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var source = ctx.People.Include(x => x.Projects);
        System.Linq.Expressions.Expression<Func<TestProject, IEnumerable<TestProject>>> expr = null!;

        Assert.ThrowsExactly<ArgumentNullException>(() => source.ThenInclude<TestPerson, TestProject, TestProject>(expr));
    }

    [TestMethod]
    public void ThenInclude_AfterCollection_WithConfigure_Throws_For_Null_Configure()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var source = ctx.People.Include(x => x.Projects);

        Assert.ThrowsExactly<ArgumentNullException>(() => source.ThenInclude<TestPerson, TestProject, TestProject>(x => new List<TestProject>(), null!));
    }

    [TestMethod]
    public void ThenInclude_AfterReference_Throws_For_Null_Source()
    {
        IGraphIncludableQueryable<TestPerson, TestPerson> source = null!;
        Assert.ThrowsExactly<ArgumentNullException>(() => source.ThenInclude<TestPerson, TestPerson, TestProject>(x => x.Projects));
    }

    [TestMethod]
    public void MarkerMethods_Throw_NotSupported_When_Called_Directly()
    {
        var ctx = new TestGraphContext();
        var request = new GraphIncludeRequest { Name = "Projects", NameKind = GraphIncludeNameKind.Navigation };

        Assert.ThrowsExactly<NotSupportedException>(() => GraphQueryExtensions.IncludeCore<TestPerson, TestProject>(ctx.People, x => x.Projects, request));

        Assert.ThrowsExactly<NotSupportedException>(() => GraphQueryExtensions.IncludeReferenceCore<TestPerson, TestPerson>(ctx.People, x => x, request));

        Assert.ThrowsExactly<NotSupportedException>(() => GraphQueryExtensions.ThenIncludeCore<TestPerson, TestPerson, TestProject>(ctx.People, x => x.Projects, request));

        Assert.ThrowsExactly<NotSupportedException>(() => GraphQueryExtensions.ThenIncludeReferenceCore<TestPerson, TestPerson, TestPerson>(ctx.People, x => x, request));

        Assert.ThrowsExactly<NotSupportedException>(() =>
            GraphQueryExtensions.ThenIncludeAfterReferenceCore<TestPerson, TestPerson, TestProject>(ctx.People, x => x.Projects, request));

        Assert.ThrowsExactly<NotSupportedException>(() =>
            GraphQueryExtensions.ThenIncludeReferenceAfterReferenceCore<TestPerson, TestPerson, TestPerson>(ctx.People, x => x, request));

        Assert.ThrowsExactly<NotSupportedException>(() => GraphQueryExtensions.IncludeRelationshipCore<TestPerson, TestProject>(ctx.People, x => x.Projects, request));

        Assert.ThrowsExactly<NotSupportedException>(() => GraphQueryExtensions.IncludeRelationshipWithEdgesCore<TestPerson, TestProject>(ctx.People, x => x.Projects, request));
    }
}