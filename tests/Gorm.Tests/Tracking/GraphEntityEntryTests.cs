using System.Reflection;
using Gorm.Application.Tracking;
using Gorm.Tests.TestSupport;

namespace Gorm.Tests.Tracking;

[TestClass]
public sealed class GraphEntityEntryTests
{

    [TestMethod]
    public void IsKeySet_Returns_True_When_Guid_Is_Set()
    {
        var person = new TestPerson { Id = Guid.NewGuid() };
        var entry = new GraphEntityEntry
        {
            Entity = person,
            ClrType = typeof(TestPerson),
            State = EntityState.Detached
        };

        Assert.IsTrue(entry.IsKeySet());
    }

    [TestMethod]
    public void IsKeySet_Returns_False_When_Guid_Is_Empty()
    {
        var person = new TestPerson { Id = Guid.Empty };
        var entry = new GraphEntityEntry
        {
            Entity = person,
            ClrType = typeof(TestPerson),
            State = EntityState.Detached
        };

        Assert.IsFalse(entry.IsKeySet());
    }

    [TestMethod]
    public void IsKeySet_Returns_True_For_Type_Without_Id_Property()
    {
        var obj = new NoIdEntity();
        var entry = new GraphEntityEntry
        {
            Entity = obj,
            ClrType = typeof(NoIdEntity),
            State = EntityState.Detached
        };

        Assert.IsTrue(entry.IsKeySet());
    }

    [TestMethod]
    public void CaptureSnapshot_Stores_Property_Values()
    {
        var person = new TestPerson { Id = Guid.NewGuid(), Name = "Alice", Age = 30 };
        var entry = new GraphEntityEntry
        {
            Entity = person,
            ClrType = typeof(TestPerson),
            State = EntityState.Unchanged
        };

        var properties = typeof(TestPerson).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        entry.CaptureSnapshot(properties);

        Assert.IsTrue(entry.TryGetOriginalValue(nameof(TestPerson.Name), out var name));
        Assert.AreEqual("Alice", name);
    }

    [TestMethod]
    public void CaptureSnapshot_Clears_Previous_Snapshot()
    {
        var person = new TestPerson { Id = Guid.NewGuid(), Name = "Alice", Age = 30 };
        var entry = new GraphEntityEntry
        {
            Entity = person,
            ClrType = typeof(TestPerson),
            State = EntityState.Unchanged
        };

        var properties = typeof(TestPerson).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        entry.CaptureSnapshot(properties);

        person.Name = "Bob";
        entry.CaptureSnapshot(properties);

        Assert.IsTrue(entry.TryGetOriginalValue(nameof(TestPerson.Name), out var name));
        Assert.AreEqual("Bob", name);
    }

    [TestMethod]
    public void CaptureSnapshot_Throws_For_Null_Properties()
    {
        var entry = new GraphEntityEntry
        {
            Entity = new TestPerson(),
            ClrType = typeof(TestPerson),
            State = EntityState.Unchanged
        };

        Assert.ThrowsExactly<ArgumentNullException>(() => entry.CaptureSnapshot((IEnumerable<PropertyInfo>)null!));
    }

    [TestMethod]
    public void DetectChanges_Sets_State_To_Modified_When_Property_Changed()
    {
        var person = new TestPerson { Id = Guid.NewGuid(), Name = "Alice", Age = 30 };
        var entry = new GraphEntityEntry
        {
            Entity = person,
            ClrType = typeof(TestPerson),
            State = EntityState.Unchanged
        };

        var properties = typeof(TestPerson).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        entry.CaptureSnapshot(properties);

        person.Name = "Bob";
        entry.DetectChanges(properties);

        Assert.AreEqual(EntityState.Modified, entry.State);
    }

    [TestMethod]
    public void DetectChanges_Keeps_State_Unchanged_When_No_Changes()
    {
        var person = new TestPerson { Id = Guid.NewGuid(), Name = "Alice", Age = 30 };
        var entry = new GraphEntityEntry
        {
            Entity = person,
            ClrType = typeof(TestPerson),
            State = EntityState.Unchanged
        };

        var properties = typeof(TestPerson).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        entry.CaptureSnapshot(properties);
        entry.DetectChanges(properties);

        Assert.AreEqual(EntityState.Unchanged, entry.State);
    }

    [TestMethod]
    public void DetectChanges_Throws_For_Null_Properties()
    {
        var entry = new GraphEntityEntry
        {
            Entity = new TestPerson(),
            ClrType = typeof(TestPerson),
            State = EntityState.Unchanged
        };

        Assert.ThrowsExactly<ArgumentNullException>(() => entry.DetectChanges((IEnumerable<PropertyInfo>)null!));
    }

    [TestMethod]
    public void TryGetOriginalValue_Returns_False_When_No_Snapshot()
    {
        var entry = new GraphEntityEntry
        {
            Entity = new TestPerson(),
            ClrType = typeof(TestPerson),
            State = EntityState.Unchanged
        };

        var found = entry.TryGetOriginalValue("Name", out var value);

        Assert.IsFalse(found);
        Assert.IsNull(value);
    }

    [TestMethod]
    public void GetOriginalValue_Throws_When_Property_Not_Snapshotted()
    {
        var entry = new GraphEntityEntry
        {
            Entity = new TestPerson(),
            ClrType = typeof(TestPerson),
            State = EntityState.Unchanged
        };

        Assert.ThrowsExactly<InvalidOperationException>(() => entry.GetOriginalValue("Name"));
    }

    [TestMethod]
    public void GetOriginalValue_Returns_Value_When_Present()
    {
        var person = new TestPerson { Id = Guid.NewGuid(), Name = "Alice", Age = 30 };
        var entry = new GraphEntityEntry
        {
            Entity = person,
            ClrType = typeof(TestPerson),
            State = EntityState.Unchanged
        };

        var properties = typeof(TestPerson).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        entry.CaptureSnapshot(properties);

        var value = entry.GetOriginalValue(nameof(TestPerson.Name));

        Assert.AreEqual("Alice", value);
    }

    [TestMethod]
    public void TryGetOriginalValue_Throws_For_Null_PropertyName()
    {
        var entry = new GraphEntityEntry
        {
            Entity = new TestPerson(),
            ClrType = typeof(TestPerson),
            State = EntityState.Unchanged
        };

        Assert.ThrowsExactly<ArgumentNullException>(() => entry.TryGetOriginalValue(null!, out _));
    }

    [TestMethod]
    public void GetOriginalValue_Throws_For_Null_PropertyName()
    {
        var entry = new GraphEntityEntry
        {
            Entity = new TestPerson(),
            ClrType = typeof(TestPerson),
            State = EntityState.Unchanged
        };

        Assert.ThrowsExactly<ArgumentNullException>(() => entry.GetOriginalValue(null!));
    }
    private sealed class NoIdEntity { }
}