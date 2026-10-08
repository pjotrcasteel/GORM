using Gorm.Application.Context;
using Gorm.Application.Diagnostics;
using Gorm.Application.Execution;
using Gorm.Application.Execution.InMemory;
using Gorm.Application.Querying.Models;
using Gorm.Application.Tracking;
using Gorm.Tests.TestSupport;

namespace Gorm.Tests.Context;

[TestClass]
public sealed class GraphContextStateTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task UseInMemory_SharedStore_Shares_Data_Between_Contexts()
    {
        var store = new InMemoryGraphStore();

        var ctx1 = new TestGraphContext().UseInMemory(store);
        ctx1.Add(new TestPerson { Id = Guid.NewGuid(), Name = "Shared" });
        await ctx1.SaveChangesAsync(TestContext.CancellationToken);

        var ctx2 = new TestGraphContext().UseInMemory(store);
        var list = await ctx2.People.ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(1, list);
        Assert.AreEqual("Shared", list[0].Name);
    }

    [TestMethod]
    public void UseInMemory_WithStore_Throws_For_Null_Context()
    {
        TestGraphContext ctx = null!;
        Assert.ThrowsExactly<ArgumentNullException>(() => ctx.UseInMemory(new InMemoryGraphStore()));
    }

    [TestMethod]
    public void UseInMemory_WithStore_Throws_For_Null_Store()
    {
        var ctx = new TestGraphContext();
        Assert.ThrowsExactly<ArgumentNullException>(() => ctx.UseInMemory((InMemoryGraphStore)null!));
    }

    [TestMethod]
    public void RelationshipState_Contains_Returns_False_When_Not_Set()
    {
        var state = new GraphRelationshipState();
        var owner = new TestPerson { Id = Guid.NewGuid() };

        var result = state.Contains(owner, GraphTraversalDirection.Outgoing, typeof(TestWorksOn), typeof(TestProject), false);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void RelationshipState_Set_And_Contains_Returns_True()
    {
        var state = new GraphRelationshipState();
        var owner = new TestPerson { Id = Guid.NewGuid() };
        var key = GraphRelationshipState.CreateKey(owner, GraphTraversalDirection.Outgoing, typeof(TestWorksOn), typeof(TestProject), false);

        state.Set(key, new List<TestProject>());

        var result = state.Contains(owner, GraphTraversalDirection.Outgoing, typeof(TestWorksOn), typeof(TestProject), false);
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void RelationshipState_TryGet_Returns_Stored_Value()
    {
        var state = new GraphRelationshipState();
        var owner = new TestPerson { Id = Guid.NewGuid() };
        var key = GraphRelationshipState.CreateKey(owner, GraphTraversalDirection.Outgoing, typeof(TestWorksOn), typeof(TestProject), false);
        var projects = new List<TestProject> { new() { Id = Guid.NewGuid(), Title = "P1" } };

        state.Set(key, projects);
        var found = state.TryGet<List<TestProject>>(owner, GraphTraversalDirection.Outgoing, typeof(TestWorksOn), typeof(TestProject), false, out var value);

        Assert.IsTrue(found);
        Assert.AreSame(projects, value);
    }

    [TestMethod]
    public void RelationshipState_TryGet_Returns_False_For_Missing_Key()
    {
        var state = new GraphRelationshipState();
        var owner = new TestPerson { Id = Guid.NewGuid() };

        var found = state.TryGet<List<TestProject>>(owner, GraphTraversalDirection.Outgoing, typeof(TestWorksOn), typeof(TestProject), false, out var value);

        Assert.IsFalse(found);
        Assert.IsNull(value);
    }

    [TestMethod]
    public void RelationshipState_TryGet_By_Key_Returns_False_For_Wrong_Type()
    {
        var state = new GraphRelationshipState();
        var owner = new TestPerson { Id = Guid.NewGuid() };
        var key = GraphRelationshipState.CreateKey(owner, GraphTraversalDirection.Outgoing, typeof(TestWorksOn), typeof(TestProject), false);
        state.Set(key, "wrong_type_value");

        var found = state.TryGet<List<TestProject>>(key, out var value);

        Assert.IsFalse(found);
        Assert.IsNull(value);
    }

    [TestMethod]
    public void RelationshipState_Clear_Removes_All_Entries()
    {
        var state = new GraphRelationshipState();
        var owner = new TestPerson { Id = Guid.NewGuid() };
        var key = GraphRelationshipState.CreateKey(owner, GraphTraversalDirection.Outgoing, typeof(TestWorksOn), typeof(TestProject), false);
        state.Set(key, new List<TestProject>());

        state.Clear();

        var result = state.Contains(owner, GraphTraversalDirection.Outgoing, typeof(TestWorksOn), typeof(TestProject), false);
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void RelationshipState_CreateKey_Returns_Consistent_Key()
    {
        var owner = new TestPerson { Id = Guid.NewGuid() };
        var key1 = GraphRelationshipState.CreateKey(owner, GraphTraversalDirection.Outgoing, typeof(TestWorksOn), typeof(TestProject), false);
        var key2 = GraphRelationshipState.CreateKey(owner, GraphTraversalDirection.Outgoing, typeof(TestWorksOn), typeof(TestProject), false);

        Assert.AreEqual(key1, key2);
    }

    [TestMethod]
    public void ChangeTrackerDebugView_Format_With_Disconnections_Contains_Disconnection_Info()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var person = new TestPerson { Id = Guid.NewGuid(), Name = "Frank" };
        var project = new TestProject { Id = Guid.NewGuid(), Title = "Atlas" };
        ctx.Attach(person);
        ctx.Attach(project);

        ctx.ChangeTracker.AddEdgeDisconnection(new PendingEdgeDisconnection
        {
            EdgeType = typeof(TestWorksOn),
            FromNode = person,
            ToNode = project
        });

        var output = GraphChangeTrackerDebugView.Format(ctx.ChangeTracker);

        Assert.IsNotNull(output);
        Assert.Contains("Pending disconnections", output, $"Expected 'Pending disconnections' in output:\n{output}");
        Assert.Contains("TestWorksOn", output, $"Expected 'TestWorksOn' in output:\n{output}");
    }

    [TestMethod]
    public void ChangeTrackerDebugView_Format_Empty_Tracker_Has_No_Disconnections_Section()
    {
        var ctx = new TestGraphContext();
        var output = GraphChangeTrackerDebugView.Format(ctx.ChangeTracker);

        Assert.DoesNotContain("Pending disconnections", output);
    }

    [TestMethod]
    public async Task ValidateSchemaAsync_Throws_Without_ConnectionFactory()
    {
        var ctx = new TestGraphContext().UseInMemory();
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => ctx.ValidateSchemaAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task AssertValidSchemaAsync_Throws_Without_ConnectionFactory()
    {
        var ctx = new TestGraphContext().UseInMemory();
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => ctx.AssertValidSchemaAsync(TestContext.CancellationToken));
    }
}