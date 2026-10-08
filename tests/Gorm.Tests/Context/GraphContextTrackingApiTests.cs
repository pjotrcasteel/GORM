using Gorm.Application.Tracking;
using Gorm.Tests.TestSupport;

namespace Gorm.Tests.Context;

[TestClass]
public sealed class GraphContextTrackingApiTests
{
    [TestMethod]
    public void Update_Attaches_Then_Marks_Modified_When_Entity_Is_Detached()
    {
        var context = new TestGraphContext();
        var person = new TestPerson { Id = Guid.NewGuid(), Name = "Alice", Age = 30 };

        context.Update(person);

        Assert.AreEqual(EntityState.Modified, context.Entry(person).State);
    }

    [TestMethod]
    public void AddRange_And_RemoveRange_Work_With_Enumerable()
    {
        var context = new TestGraphContext();
        var people = new object[]
        {
            new TestPerson { Id = Guid.NewGuid(), Name = "Alice", Age = 30 },
            new TestPerson { Id = Guid.NewGuid(), Name = "Bob", Age = 31 }
        };

        context.AddRange(people);
        Assert.HasCount(2, context.ChangeTracker.Entries);

        context.RemoveRange(people);
        Assert.IsEmpty(context.ChangeTracker.Entries);
    }

    [TestMethod]
    public void HasChanges_Reflects_Tracked_State()
    {
        var context = new TestGraphContext();
        Assert.IsFalse(context.HasChanges());

        context.Add(new TestPerson { Id = Guid.NewGuid(), Name = "Alice", Age = 30 });

        Assert.IsTrue(context.HasChanges());
    }
}