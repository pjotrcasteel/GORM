using Gorm.Application.Execution.InMemory;
using Gorm.Tests.TestSupport;

namespace Gorm.Tests.Context;

[TestClass]
public sealed class GraphContextLoadApiTests
{
    public TestContext TestContext { get; set; } = null!;

    private static TestGraphContext CreatePopulatedContext(out TestPerson alice, out TestProject proj)
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
    public async Task LoadOutgoingAsync_Returns_Related_Nodes()
    {
        var ctx = CreatePopulatedContext(out var alice, out _);

        var result = await ctx.LoadOutgoingAsync<TestWorksOn, TestPerson, TestProject>(alice, TestContext.CancellationToken);

        Assert.IsNotNull(result);
        Assert.HasCount(1, result!);
        Assert.IsInstanceOfType<TestProject>(result[0]);
    }

    [TestMethod]
    public async Task LoadOutgoingAsync_Returns_Cached_On_Second_Call()
    {
        var ctx = CreatePopulatedContext(out var alice, out _);

        var first = await ctx.LoadOutgoingAsync<TestWorksOn, TestPerson, TestProject>(alice, TestContext.CancellationToken);
        var second = await ctx.LoadOutgoingAsync<TestWorksOn, TestPerson, TestProject>(alice, TestContext.CancellationToken);

        Assert.AreSame(first, second);
    }

    [TestMethod]
    public async Task LoadOutgoingAsync_Throws_For_Null_From()
    {
        var ctx = new TestGraphContext().UseInMemory();
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => ctx.LoadOutgoingAsync<TestWorksOn, TestPerson, TestProject>(null!, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task LoadOutgoingAsync_WithEdgePredicate_Filters_Results()
    {
        var ctx = CreatePopulatedContext(out var alice, out _);

        var result = await ctx.LoadOutgoingAsync<TestWorksOn, TestPerson, TestProject>(alice, e => e.Role == "Dev", TestContext.CancellationToken);

        Assert.HasCount(1, result);
    }

    [TestMethod]
    public async Task LoadOutgoingAsync_WithEdgePredicate_Returns_Empty_When_No_Match()
    {
        var ctx = CreatePopulatedContext(out var alice, out _);

        var result = await ctx.LoadOutgoingAsync<TestWorksOn, TestPerson, TestProject>(alice, e => e.Role == "Manager", TestContext.CancellationToken);

        Assert.IsEmpty(result);
    }

    [TestMethod]
    public async Task LoadOutgoingAsync_WithEdgePredicate_Throws_For_Null_From()
    {
        var ctx = new TestGraphContext().UseInMemory();
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => ctx.LoadOutgoingAsync<TestWorksOn, TestPerson, TestProject>(null!, e => e.Role == "Dev", TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task LoadOutgoingAsync_WithEdgePredicate_Throws_For_Null_Predicate()
    {
        var ctx = CreatePopulatedContext(out var alice, out _);
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => ctx.LoadOutgoingAsync<TestWorksOn, TestPerson, TestProject>(alice, null!, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task LoadIncomingAsync_Returns_Related_Nodes()
    {
        var ctx = CreatePopulatedContext(out _, out var proj);

        var result = await ctx.LoadIncomingAsync<TestWorksOn, TestPerson, TestProject>(proj, TestContext.CancellationToken);

        Assert.IsNotNull(result);
        Assert.HasCount(1, result!);
        Assert.IsInstanceOfType<TestPerson>(result[0]);
    }

    [TestMethod]
    public async Task LoadIncomingAsync_Returns_Cached_On_Second_Call()
    {
        var ctx = CreatePopulatedContext(out _, out var proj);

        var first = await ctx.LoadIncomingAsync<TestWorksOn, TestPerson, TestProject>(proj, TestContext.CancellationToken);
        var second = await ctx.LoadIncomingAsync<TestWorksOn, TestPerson, TestProject>(proj, TestContext.CancellationToken);

        Assert.AreSame(first, second);
    }

    [TestMethod]
    public async Task LoadIncomingAsync_Throws_For_Null_To()
    {
        var ctx = new TestGraphContext().UseInMemory();
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => ctx.LoadIncomingAsync<TestWorksOn, TestPerson, TestProject>(null!, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task LoadIncomingAsync_WithEdgePredicate_Filters_Results()
    {
        var ctx = CreatePopulatedContext(out _, out var proj);

        var result = await ctx.LoadIncomingAsync<TestWorksOn, TestPerson, TestProject>(proj, e => e.Role == "Dev", TestContext.CancellationToken);

        Assert.HasCount(1, result);
    }

    [TestMethod]
    public async Task LoadIncomingAsync_WithEdgePredicate_Throws_For_Null_To()
    {
        var ctx = new TestGraphContext().UseInMemory();
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => ctx.LoadIncomingAsync<TestWorksOn, TestPerson, TestProject>(null!, e => e.Role == "Dev", TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task LoadIncomingAsync_WithEdgePredicate_Throws_For_Null_Predicate()
    {
        var ctx = CreatePopulatedContext(out _, out var proj);
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => ctx.LoadIncomingAsync<TestWorksOn, TestPerson, TestProject>(proj, null!, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task LoadOutgoingWithEdgesAsync_Returns_Related_Nodes_With_Edges()
    {
        var ctx = CreatePopulatedContext(out var alice, out _);

        var result = await ctx.LoadOutgoingWithEdgesAsync<TestWorksOn, TestPerson, TestProject>(alice, TestContext.CancellationToken);

        Assert.IsNotNull(result);
        Assert.HasCount(1, result!);
        Assert.IsNotNull(result[0].Edge);
        Assert.IsInstanceOfType<TestProject>(result[0].Node);
    }

    [TestMethod]
    public async Task LoadOutgoingWithEdgesAsync_Returns_Cached_On_Second_Call()
    {
        var ctx = CreatePopulatedContext(out var alice, out _);

        var first = await ctx.LoadOutgoingWithEdgesAsync<TestWorksOn, TestPerson, TestProject>(alice, TestContext.CancellationToken);
        var second = await ctx.LoadOutgoingWithEdgesAsync<TestWorksOn, TestPerson, TestProject>(alice, TestContext.CancellationToken);

        Assert.AreSame(first, second);
    }

    [TestMethod]
    public async Task LoadOutgoingWithEdgesAsync_Throws_For_Null_From()
    {
        var ctx = new TestGraphContext().UseInMemory();
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => ctx.LoadOutgoingWithEdgesAsync<TestWorksOn, TestPerson, TestProject>(null!, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task LoadOutgoingWithEdgesAsync_WithEdgePredicate_Filters_Results()
    {
        var ctx = CreatePopulatedContext(out var alice, out _);

        var result = await ctx.LoadOutgoingWithEdgesAsync<TestWorksOn, TestPerson, TestProject>(alice, e => e.Role == "Dev", TestContext.CancellationToken);

        Assert.HasCount(1, result);
        Assert.AreEqual("Dev", result[0].Edge.Role);
    }

    [TestMethod]
    public async Task LoadOutgoingWithEdgesAsync_WithEdgePredicate_Throws_For_Null_From()
    {
        var ctx = new TestGraphContext().UseInMemory();
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => ctx.LoadOutgoingWithEdgesAsync<TestWorksOn, TestPerson, TestProject>(null!, e => e.Role == "Dev", TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task LoadOutgoingWithEdgesAsync_WithEdgePredicate_Throws_For_Null_Predicate()
    {
        var ctx = CreatePopulatedContext(out var alice, out _);
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => ctx.LoadOutgoingWithEdgesAsync<TestWorksOn, TestPerson, TestProject>(alice, null!, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task LoadIncomingWithEdgesAsync_Returns_Related_Nodes_With_Edges()
    {
        var ctx = CreatePopulatedContext(out _, out var proj);

        var result = await ctx.LoadIncomingWithEdgesAsync<TestWorksOn, TestPerson, TestProject>(proj, TestContext.CancellationToken);

        Assert.IsNotNull(result);
        Assert.HasCount(1, result!);
        Assert.IsInstanceOfType<TestPerson>(result[0].Node);
    }

    [TestMethod]
    public async Task LoadIncomingWithEdgesAsync_Returns_Cached_On_Second_Call()
    {
        var ctx = CreatePopulatedContext(out _, out var proj);

        var first = await ctx.LoadIncomingWithEdgesAsync<TestWorksOn, TestPerson, TestProject>(proj, TestContext.CancellationToken);
        var second = await ctx.LoadIncomingWithEdgesAsync<TestWorksOn, TestPerson, TestProject>(proj, TestContext.CancellationToken);

        Assert.AreSame(first, second);
    }

    [TestMethod]
    public async Task LoadIncomingWithEdgesAsync_Throws_For_Null_To()
    {
        var ctx = new TestGraphContext().UseInMemory();
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => ctx.LoadIncomingWithEdgesAsync<TestWorksOn, TestPerson, TestProject>(null!, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task LoadIncomingWithEdgesAsync_WithEdgePredicate_Filters_Results()
    {
        var ctx = CreatePopulatedContext(out _, out var proj);

        var result = await ctx.LoadIncomingWithEdgesAsync<TestWorksOn, TestPerson, TestProject>(proj, e => e.Role == "Dev", TestContext.CancellationToken);

        Assert.HasCount(1, result);
    }

    [TestMethod]
    public async Task LoadIncomingWithEdgesAsync_WithEdgePredicate_Throws_For_Null_To()
    {
        var ctx = new TestGraphContext().UseInMemory();
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => ctx.LoadIncomingWithEdgesAsync<TestWorksOn, TestPerson, TestProject>(null!, e => e.Role == "Dev", TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task LoadIncomingWithEdgesAsync_WithEdgePredicate_Throws_For_Null_Predicate()
    {
        var ctx = CreatePopulatedContext(out _, out var proj);
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => ctx.LoadIncomingWithEdgesAsync<TestWorksOn, TestPerson, TestProject>(proj, null!, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task IsOutgoingLoaded_Returns_False_Before_Load()
    {
        var ctx = CreatePopulatedContext(out var alice, out _);
        Assert.IsFalse(ctx.IsOutgoingLoaded<TestWorksOn, TestPerson, TestProject>(alice));
    }

    [TestMethod]
    public async Task IsOutgoingLoaded_Returns_True_After_Load()
    {
        var ctx = CreatePopulatedContext(out var alice, out _);
        await ctx.LoadOutgoingAsync<TestWorksOn, TestPerson, TestProject>(alice, TestContext.CancellationToken);
        Assert.IsTrue(ctx.IsOutgoingLoaded<TestWorksOn, TestPerson, TestProject>(alice));
    }

    [TestMethod]
    public void IsOutgoingLoaded_Throws_For_Null()
    {
        var ctx = new TestGraphContext().UseInMemory();
        Assert.ThrowsExactly<ArgumentNullException>(() => ctx.IsOutgoingLoaded<TestWorksOn, TestPerson, TestProject>(null!));
    }

    [TestMethod]
    public async Task IsIncomingLoaded_Returns_False_Before_Load()
    {
        var ctx = CreatePopulatedContext(out _, out var proj);
        Assert.IsFalse(ctx.IsIncomingLoaded<TestWorksOn, TestPerson, TestProject>(proj));
    }

    [TestMethod]
    public async Task IsIncomingLoaded_Returns_True_After_Load()
    {
        var ctx = CreatePopulatedContext(out _, out var proj);
        await ctx.LoadIncomingAsync<TestWorksOn, TestPerson, TestProject>(proj, TestContext.CancellationToken);
        Assert.IsTrue(ctx.IsIncomingLoaded<TestWorksOn, TestPerson, TestProject>(proj));
    }

    [TestMethod]
    public void IsIncomingLoaded_Throws_For_Null()
    {
        var ctx = new TestGraphContext().UseInMemory();
        Assert.ThrowsExactly<ArgumentNullException>(() => ctx.IsIncomingLoaded<TestWorksOn, TestPerson, TestProject>(null!));
    }

    [TestMethod]
    public async Task IsOutgoingWithEdgesLoaded_Returns_True_After_Load()
    {
        var ctx = CreatePopulatedContext(out var alice, out _);
        await ctx.LoadOutgoingWithEdgesAsync<TestWorksOn, TestPerson, TestProject>(alice, TestContext.CancellationToken);
        Assert.IsTrue(ctx.IsOutgoingWithEdgesLoaded<TestWorksOn, TestPerson, TestProject>(alice));
    }

    [TestMethod]
    public void IsOutgoingWithEdgesLoaded_Throws_For_Null()
    {
        var ctx = new TestGraphContext().UseInMemory();
        Assert.ThrowsExactly<ArgumentNullException>(() => ctx.IsOutgoingWithEdgesLoaded<TestWorksOn, TestPerson, TestProject>(null!));
    }

    [TestMethod]
    public async Task IsIncomingWithEdgesLoaded_Returns_True_After_Load()
    {
        var ctx = CreatePopulatedContext(out _, out var proj);
        await ctx.LoadIncomingWithEdgesAsync<TestWorksOn, TestPerson, TestProject>(proj, TestContext.CancellationToken);
        Assert.IsTrue(ctx.IsIncomingWithEdgesLoaded<TestWorksOn, TestPerson, TestProject>(proj));
    }

    [TestMethod]
    public void IsIncomingWithEdgesLoaded_Throws_For_Null()
    {
        var ctx = new TestGraphContext().UseInMemory();
        Assert.ThrowsExactly<ArgumentNullException>(() => ctx.IsIncomingWithEdgesLoaded<TestWorksOn, TestPerson, TestProject>(null!));
    }

    [TestMethod]
    public async Task TryGetLoadedOutgoing_Returns_True_And_Items_After_Load()
    {
        var ctx = CreatePopulatedContext(out var alice, out _);
        await ctx.LoadOutgoingAsync<TestWorksOn, TestPerson, TestProject>(alice, TestContext.CancellationToken);

        var found = ctx.TryGetLoadedOutgoing<TestWorksOn, TestPerson, TestProject>(alice, out var related);

        Assert.IsTrue(found);
        Assert.IsNotNull(related);
        Assert.HasCount(1, related!);
    }

    [TestMethod]
    public void TryGetLoadedOutgoing_Returns_False_Before_Load()
    {
        var ctx = CreatePopulatedContext(out var alice, out _);

        var found = ctx.TryGetLoadedOutgoing<TestWorksOn, TestPerson, TestProject>(alice, out var related);

        Assert.IsFalse(found);
        Assert.IsNull(related);
    }

    [TestMethod]
    public void TryGetLoadedOutgoing_Throws_For_Null()
    {
        var ctx = new TestGraphContext().UseInMemory();
        Assert.ThrowsExactly<ArgumentNullException>(() => ctx.TryGetLoadedOutgoing<TestWorksOn, TestPerson, TestProject>(null!, out _));
    }

    [TestMethod]
    public async Task TryGetLoadedIncoming_Returns_True_And_Items_After_Load()
    {
        var ctx = CreatePopulatedContext(out _, out var proj);
        await ctx.LoadIncomingAsync<TestWorksOn, TestPerson, TestProject>(proj, TestContext.CancellationToken);

        var found = ctx.TryGetLoadedIncoming<TestWorksOn, TestPerson, TestProject>(proj, out var related);

        Assert.IsTrue(found);
        Assert.IsNotNull(related);
        Assert.HasCount(1, related!);
    }

    [TestMethod]
    public void TryGetLoadedIncoming_Returns_False_Before_Load()
    {
        var ctx = CreatePopulatedContext(out _, out var proj);

        var found = ctx.TryGetLoadedIncoming<TestWorksOn, TestPerson, TestProject>(proj, out var related);

        Assert.IsFalse(found);
        Assert.IsNull(related);
    }

    [TestMethod]
    public void TryGetLoadedIncoming_Throws_For_Null()
    {
        var ctx = new TestGraphContext().UseInMemory();
        Assert.ThrowsExactly<ArgumentNullException>(() => ctx.TryGetLoadedIncoming<TestWorksOn, TestPerson, TestProject>(null!, out _));
    }

    [TestMethod]
    public async Task TryGetLoadedOutgoingWithEdges_Returns_True_After_Load()
    {
        var ctx = CreatePopulatedContext(out var alice, out _);
        await ctx.LoadOutgoingWithEdgesAsync<TestWorksOn, TestPerson, TestProject>(alice, TestContext.CancellationToken);

        var found = ctx.TryGetLoadedOutgoingWithEdges<TestWorksOn, TestPerson, TestProject>(alice, out var related);

        Assert.IsTrue(found);
        Assert.IsNotNull(related);
        Assert.HasCount(1, related!);
    }

    [TestMethod]
    public void TryGetLoadedOutgoingWithEdges_Returns_False_Before_Load()
    {
        var ctx = CreatePopulatedContext(out var alice, out _);

        var found = ctx.TryGetLoadedOutgoingWithEdges<TestWorksOn, TestPerson, TestProject>(alice, out var related);

        Assert.IsFalse(found);
        Assert.IsNull(related);
    }

    [TestMethod]
    public void TryGetLoadedOutgoingWithEdges_Throws_For_Null()
    {
        var ctx = new TestGraphContext().UseInMemory();
        Assert.ThrowsExactly<ArgumentNullException>(() => ctx.TryGetLoadedOutgoingWithEdges<TestWorksOn, TestPerson, TestProject>(null!, out _));
    }

    [TestMethod]
    public async Task TryGetLoadedIncomingWithEdges_Returns_True_After_Load()
    {
        var ctx = CreatePopulatedContext(out _, out var proj);
        await ctx.LoadIncomingWithEdgesAsync<TestWorksOn, TestPerson, TestProject>(proj, TestContext.CancellationToken);

        var found = ctx.TryGetLoadedIncomingWithEdges<TestWorksOn, TestPerson, TestProject>(proj, out var related);

        Assert.IsTrue(found);
        Assert.IsNotNull(related);
    }

    [TestMethod]
    public void TryGetLoadedIncomingWithEdges_Returns_False_Before_Load()
    {
        var ctx = CreatePopulatedContext(out _, out var proj);

        var found = ctx.TryGetLoadedIncomingWithEdges<TestWorksOn, TestPerson, TestProject>(proj, out var related);

        Assert.IsFalse(found);
        Assert.IsNull(related);
    }

    [TestMethod]
    public void TryGetLoadedIncomingWithEdges_Throws_For_Null()
    {
        var ctx = new TestGraphContext().UseInMemory();
        Assert.ThrowsExactly<ArgumentNullException>(() => ctx.TryGetLoadedIncomingWithEdges<TestWorksOn, TestPerson, TestProject>(null!, out _));
    }

    [TestMethod]
    public async Task AddEdge_Saves_Edge_With_Explicit_Instance()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var alice = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        var proj = new TestProject { Id = Guid.NewGuid(), Title = "Apollo" };
        var edge = new TestWorksOn { Id = Guid.NewGuid(), Role = "Lead" };
        ctx.Add(alice);
        ctx.Add(proj);

        ctx.AddEdge(alice, proj, edge);
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        var result = await ctx.LoadOutgoingAsync<TestWorksOn, TestPerson, TestProject>(alice, TestContext.CancellationToken);

        Assert.HasCount(1, result!);
    }

    [TestMethod]
    public void AddEdge_Assigns_Id_When_Empty()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var alice = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        var proj = new TestProject { Id = Guid.NewGuid(), Title = "Apollo" };
        var edge = new TestWorksOn { Id = Guid.Empty };
        ctx.Add(alice);
        ctx.Add(proj);

        ctx.AddEdge<TestWorksOn, TestPerson, TestProject>(alice, proj, edge);

        Assert.AreNotEqual(Guid.Empty, edge.Id);
    }

    [TestMethod]
    public void AddEdge_Throws_For_Null_Edge()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var alice = new TestPerson { Id = Guid.NewGuid() };
        var proj = new TestProject { Id = Guid.NewGuid() };
        Assert.ThrowsExactly<ArgumentNullException>(() => ctx.AddEdge<TestWorksOn, TestPerson, TestProject>(alice, proj, null!));
    }

    [TestMethod]
    public void AddEdge_Throws_For_Null_From()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var proj = new TestProject { Id = Guid.NewGuid() };
        var edge = new TestWorksOn { Id = Guid.NewGuid() };
        Assert.ThrowsExactly<ArgumentNullException>(() => ctx.AddEdge<TestWorksOn, TestPerson, TestProject>(null!, proj, edge));
    }

    [TestMethod]
    public void AddEdge_Throws_For_Null_To()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var alice = new TestPerson { Id = Guid.NewGuid() };
        var edge = new TestWorksOn { Id = Guid.NewGuid() };
        Assert.ThrowsExactly<ArgumentNullException>(() => ctx.AddEdge<TestWorksOn, TestPerson, TestProject>(alice, null!, edge));
    }

    [TestMethod]
    public void ClearTracking_Removes_All_Tracked_Entities()
    {
        var ctx = new TestGraphContext().UseInMemory();
        ctx.Add(new TestPerson { Id = Guid.NewGuid(), Name = "Alice" });
        ctx.Add(new TestProject { Id = Guid.NewGuid(), Title = "Apollo" });

        ctx.ClearTracking();

        Assert.IsEmpty(ctx.Entries);
    }

    [TestMethod]
    public async Task ClearTracking_Clears_Relationship_Cache()
    {
        var ctx = CreatePopulatedContext(out var alice, out _);
        await ctx.LoadOutgoingAsync<TestWorksOn, TestPerson, TestProject>(alice, TestContext.CancellationToken);

        ctx.ClearTracking();

        Assert.IsFalse(ctx.IsOutgoingLoaded<TestWorksOn, TestPerson, TestProject>(alice));
    }

    [TestMethod]
    public void GenerateCreateScript_Returns_Non_Empty_String()
    {
        var ctx = new TestGraphContext();
        var script = ctx.GenerateCreateScript();
        Assert.IsNotNull(script);
        Assert.IsGreaterThan(0, script.Length);
    }

    [TestMethod]
    public void ChangeTrackerDebugView_Returns_String()
    {
        var ctx = new TestGraphContext().UseInMemory();
        ctx.Add(new TestPerson { Id = Guid.NewGuid(), Name = "Alice" });
        var view = ctx.ChangeTrackerDebugView;
        Assert.IsNotNull(view);
        Assert.IsGreaterThan(0, view.Length);
    }

    [TestMethod]
    public async Task BeginTransactionAsync_Throws_Without_ConnectionFactory()
    {
        var ctx = new TestGraphContext();
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => ctx.BeginTransactionAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task EnsureCreatedAsync_WhenNoConnectionFactory_ThrowsInvalidOperationException()
    {
        var ctx = new TestGraphContext();
#pragma warning disable CS0618
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => ctx.EnsureCreatedAsync(TestContext.CancellationToken));
#pragma warning restore CS0618
    }

    [TestMethod]
    public void TryGetRelated_Throws_For_Null_Owner()
    {
        var ctx = new TestGraphContext().UseInMemory();
        Assert.ThrowsExactly<ArgumentNullException>(() => ctx.TryGetRelated<TestProject>(null!, "Projects", out _));
    }

    [TestMethod]
    public void TryGetRelatedWithEdges_Throws_For_Null_Owner()
    {
        var ctx = new TestGraphContext().UseInMemory();
        Assert.ThrowsExactly<ArgumentNullException>(() => ctx.TryGetRelatedWithEdges(null!, "Projects", out _));
    }

    [TestMethod]
    public void GetRelated_Returns_Empty_When_Not_Loaded()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var alice = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        var result = ctx.GetRelated<TestProject>(alice, "Projects");
        Assert.IsEmpty(result);
    }

    [TestMethod]
    public void GetRelated_Throws_For_Null_Owner()
    {
        var ctx = new TestGraphContext().UseInMemory();
        Assert.ThrowsExactly<ArgumentNullException>(() => ctx.GetRelated<TestProject>(null!, "Projects"));
    }

    [TestMethod]
    public void HasChanges_Returns_True_When_Entity_Added()
    {
        var ctx = new TestGraphContext().UseInMemory();
        ctx.Add(new TestPerson { Id = Guid.NewGuid(), Name = "Alice" });
        Assert.IsTrue(ctx.HasChanges());
    }

    [TestMethod]
    public async Task HasChanges_Returns_False_After_Save()
    {
        var ctx = new TestGraphContext().UseInMemory();
        ctx.Add(new TestPerson { Id = Guid.NewGuid(), Name = "Alice" });
        await ctx.SaveChangesAsync(TestContext.CancellationToken);
        Assert.IsFalse(ctx.HasChanges());
    }

    [TestMethod]
    public void AcceptAllChanges_Clears_Changes()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var alice = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        ctx.Attach(alice);
        ctx.MarkModified(alice);

        ctx.AcceptAllChanges();

        Assert.IsFalse(ctx.HasChanges());
    }

    [TestMethod]
    public void UpdateRange_Marks_All_Entities_Modified()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var alice = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        var bob = new TestPerson { Id = Guid.NewGuid(), Name = "Bob" };
        ctx.AttachRange([alice, bob]);

        ctx.UpdateRange([alice, bob]);

        Assert.AreEqual(Gorm.Application.Tracking.EntityState.Modified, ctx.Entry(alice).State);
        Assert.AreEqual(Gorm.Application.Tracking.EntityState.Modified, ctx.Entry(bob).State);
    }

    [TestMethod]
    public void UpdateRange_Throws_For_Null()
    {
        var ctx = new TestGraphContext().UseInMemory();
        Assert.ThrowsExactly<ArgumentNullException>(() => ctx.UpdateRange(null!));
    }

    [TestMethod]
    public void AddRange_Params_Throws_For_Null()
    {
        var ctx = new TestGraphContext().UseInMemory();
        Assert.ThrowsExactly<ArgumentNullException>(() => ctx.AddRange((object[])null!));
    }

    [TestMethod]
    public async Task Disconnect_Invalidates_Relationship_Cache()
    {
        var ctx = CreatePopulatedContext(out var alice, out var proj);
        await ctx.LoadOutgoingAsync<TestWorksOn, TestPerson, TestProject>(alice, TestContext.CancellationToken);

        ctx.Disconnect<TestWorksOn, TestPerson, TestProject>(alice, proj);

        Assert.IsFalse(ctx.IsOutgoingLoaded<TestWorksOn, TestPerson, TestProject>(alice));
    }

    [TestMethod]
    public void Disconnect_Throws_For_Null_From()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var proj = new TestProject { Id = Guid.NewGuid() };
        Assert.ThrowsExactly<ArgumentNullException>(() => ctx.Disconnect<TestWorksOn, TestPerson, TestProject>(null!, proj));
    }

    [TestMethod]
    public void Disconnect_Throws_For_Null_To()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var alice = new TestPerson { Id = Guid.NewGuid() };
        Assert.ThrowsExactly<ArgumentNullException>(() => ctx.Disconnect<TestWorksOn, TestPerson, TestProject>(alice, null!));
    }
}