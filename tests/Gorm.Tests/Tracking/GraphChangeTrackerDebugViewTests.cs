using Gorm.Tests.TestSupport;

namespace Gorm.Tests.Tracking;

[TestClass]
public sealed class GraphChangeTrackerDebugViewTests
{
    [TestMethod]
    public void Debug_View_Includes_Tracked_Entries_And_Pending_Edges()
    {
        var context = new TestGraphContext();
        var alice = new TestPerson { Id = Guid.NewGuid(), Name = "Alice", Age = 30 };
        var bob = new TestPerson { Id = Guid.NewGuid(), Name = "Bob", Age = 31 };

        context.Add(alice);
        context.Connect<TestKnows, TestPerson, TestPerson>(alice, bob, edge => edge.Strength = 5);

        var text = context.ChangeTrackerDebugView;

        Assert.Contains("GraphChangeTracker", text);
        Assert.Contains("TestPerson [Added]", text);
        Assert.Contains("Pending connections:", text);
        Assert.Contains("TestKnows", text);
    }

    [TestMethod]
    public void Clear_Removes_All_Tracked_State()
    {
        var context = new TestGraphContext();
        var alice = new TestPerson { Id = Guid.NewGuid(), Name = "Alice", Age = 30 };
        var bob = new TestPerson { Id = Guid.NewGuid(), Name = "Bob", Age = 31 };

        context.Add(alice);
        context.Connect<TestKnows, TestPerson, TestPerson>(alice, bob);
        context.ClearTracking();

        Assert.IsEmpty(context.ChangeTracker.Entries);
        Assert.IsEmpty(context.ChangeTracker.PendingEdgeConnections);
        Assert.IsEmpty(context.ChangeTracker.PendingEdgeDisconnections);
    }
}