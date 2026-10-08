using Gorm.Application.Execution.InMemory;
using Gorm.Application.Tracking;
using Gorm.Tests.TestSupport;

namespace Gorm.Tests.Tracking;

[TestClass]
public sealed class GraphChangeTrackerExtendedTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void TryGetTrackedEntityByKey_Returns_Tracked_Entity()
    {
        var ctx = new TestGraphContext();
        var person = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        ctx.Add(person);

        var found = ctx.ChangeTracker.TryGetTrackedEntityByKey(ctx.Model, typeof(TestPerson), person.Id);

        Assert.AreSame(person, found);
    }

    [TestMethod]
    public void TryGetTrackedEntityByKey_Returns_Null_When_Not_Found()
    {
        var ctx = new TestGraphContext();
        ctx.Add(new TestPerson { Id = Guid.NewGuid(), Name = "Alice" });

        var found = ctx.ChangeTracker.TryGetTrackedEntityByKey(ctx.Model, typeof(TestPerson), Guid.NewGuid());

        Assert.IsNull(found);
    }

    [TestMethod]
    public void TryGetTrackedEntityByKey_Returns_Null_For_Deleted_Entries()
    {
        var ctx = new TestGraphContext();
        var person = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        ctx.Attach(person);
        ctx.Remove(person);

        var found = ctx.ChangeTracker.TryGetTrackedEntityByKey(ctx.Model, typeof(TestPerson), person.Id);

        Assert.IsNull(found);
    }

    [TestMethod]
    public void TryGetTrackedEntityByKey_Throws_For_Null_Model()
    {
        var ctx = new TestGraphContext();
        Assert.ThrowsExactly<ArgumentNullException>(() => ctx.ChangeTracker.TryGetTrackedEntityByKey(null!, typeof(TestPerson), Guid.NewGuid()));
    }

    [TestMethod]
    public void TryGetTrackedEntityByKey_Throws_For_Null_ClrType()
    {
        var ctx = new TestGraphContext();
        Assert.ThrowsExactly<ArgumentNullException>(() => ctx.ChangeTracker.TryGetTrackedEntityByKey(ctx.Model, null!, Guid.NewGuid()));
    }

    [TestMethod]
    public void TryGetTrackedEntityByKey_Throws_For_Null_KeyValue()
    {
        var ctx = new TestGraphContext();
        Assert.ThrowsExactly<ArgumentNullException>(() => ctx.ChangeTracker.TryGetTrackedEntityByKey(ctx.Model, typeof(TestPerson), null!));
    }

    [TestMethod]
    public void HasChanges_Returns_False_When_No_Entries()
    {
        var tracker = new GraphChangeTracker();
        Assert.IsFalse(tracker.HasChanges());
    }

    [TestMethod]
    public void HasChanges_Returns_True_For_Added_Entry()
    {
        var tracker = new GraphChangeTracker();
        tracker.Add(new TestPerson { Id = Guid.NewGuid(), Name = "Alice" });
        Assert.IsTrue(tracker.HasChanges());
    }

    [TestMethod]
    public void Remove_Added_Entity_Detaches_It()
    {
        var ctx = new TestGraphContext();
        var person = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        ctx.Add(person);

        ctx.Remove(person);

        Assert.AreEqual(EntityState.Detached, ctx.Entry(person).State);
    }

    [TestMethod]
    public void MarkModified_On_Unchanged_Sets_Modified()
    {
        var ctx = new TestGraphContext();
        var person = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        ctx.Attach(person);

        ctx.ChangeTracker.MarkModified(person);

        Assert.AreEqual(EntityState.Modified, ctx.Entry(person).State);
    }

    [TestMethod]
    public void MarkModified_Already_Modified_Stays_Modified()
    {
        var ctx = new TestGraphContext();
        var person = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        ctx.Attach(person);
        ctx.ChangeTracker.MarkModified(person);
        ctx.ChangeTracker.MarkModified(person);

        Assert.AreEqual(EntityState.Modified, ctx.Entry(person).State);
    }

    [TestMethod]
    public async Task AddEdgeConnection_Tracked_In_PendingEdgeConnections()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var person = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        var project = new TestProject { Id = Guid.NewGuid(), Title = "Apollo" };
        ctx.Add(person);
        ctx.Add(project);
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        ctx.Connect<TestWorksOn, TestPerson, TestProject>(person, project);

        Assert.HasCount(1, ctx.ChangeTracker.PendingEdgeConnections);
    }

    [TestMethod]
    public async Task AddEdgeDisconnection_Tracked_In_PendingEdgeDisconnections()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var person = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        var project = new TestProject { Id = Guid.NewGuid(), Title = "Apollo" };
        ctx.Add(person);
        ctx.Add(project);
        ctx.Connect<TestWorksOn, TestPerson, TestProject>(person, project);
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        ctx.Disconnect<TestWorksOn, TestPerson, TestProject>(person, project);

        Assert.HasCount(1, ctx.ChangeTracker.PendingEdgeDisconnections);
    }

    [TestMethod]
    public async Task Connect_Then_Disconnect_Before_Save_Cancels_Each_Other()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var person = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        var project = new TestProject { Id = Guid.NewGuid(), Title = "Apollo" };
        ctx.Add(person);
        ctx.Add(project);
        await ctx.SaveChangesAsync(TestContext.CancellationToken);

        ctx.Connect<TestWorksOn, TestPerson, TestProject>(person, project);
        ctx.Disconnect<TestWorksOn, TestPerson, TestProject>(person, project);

        Assert.IsEmpty(ctx.ChangeTracker.PendingEdgeConnections);
        Assert.IsEmpty(ctx.ChangeTracker.PendingEdgeDisconnections);
    }

    [TestMethod]
    public void AcceptAllChanges_Marks_Added_As_Unchanged()
    {
        var ctx = new TestGraphContext();
        var person = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        ctx.Add(person);

        ctx.AcceptAllChanges();

        Assert.AreEqual(EntityState.Unchanged, ctx.Entry(person).State);
    }

    [TestMethod]
    public void AcceptAllChanges_Removes_Deleted_Entries()
    {
        var ctx = new TestGraphContext();
        var person = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        ctx.Attach(person);
        ctx.Remove(person);

        ctx.AcceptAllChanges();

        Assert.AreEqual(EntityState.Detached, ctx.Entry(person).State);
        Assert.IsEmpty(ctx.ChangeTracker.Entries);
    }

    [TestMethod]
    public void Attach_Already_Tracked_Entity_Does_Not_Change_State()
    {
        var ctx = new TestGraphContext();
        var person = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        ctx.Attach(person);
        ctx.MarkModified(person);

        ctx.Attach(person);

        Assert.AreEqual(EntityState.Modified, ctx.Entry(person).State);
    }

    [TestMethod]
    public void ChangeTracker_Add_Throws_For_Null()
    {
        var tracker = new GraphChangeTracker();
        Assert.ThrowsExactly<ArgumentNullException>(() => tracker.Add(null!));
    }

    [TestMethod]
    public void ChangeTracker_Remove_Throws_For_Null()
    {
        var tracker = new GraphChangeTracker();
        Assert.ThrowsExactly<ArgumentNullException>(() => tracker.Remove(null!));
    }

    [TestMethod]
    public void ChangeTracker_MarkModified_Throws_For_Null()
    {
        var tracker = new GraphChangeTracker();
        Assert.ThrowsExactly<ArgumentNullException>(() => tracker.MarkModified(null!));
    }

    [TestMethod]
    public void ChangeTracker_Attach_Throws_For_Null_Entity()
    {
        var ctx = new TestGraphContext();
        Assert.ThrowsExactly<ArgumentNullException>(() => ctx.ChangeTracker.Attach(null!, ctx.Model));
    }

    [TestMethod]
    public void ChangeTracker_Attach_Throws_For_Null_Model()
    {
        var tracker = new GraphChangeTracker();
        var person = new TestPerson { Id = Guid.NewGuid() };
        Assert.ThrowsExactly<ArgumentNullException>(() => tracker.Attach(person, null!));
    }

    [TestMethod]
    public void ChangeTracker_DetectChanges_Throws_For_Null_Model()
    {
        var tracker = new GraphChangeTracker();
        Assert.ThrowsExactly<ArgumentNullException>(() => tracker.DetectChanges(null!));
    }
}