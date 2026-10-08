using System.Data.Common;
using Gorm.Application.Execution;
using Gorm.Application.Execution.InMemory;
using Gorm.Application.Tracking;
using Gorm.Infrastructure.Persistence.Connections;
using Gorm.Infrastructure.Providers.SqlServer;
using Gorm.Tests.TestSupport;

namespace Gorm.Tests.Context;

[TestClass]
public sealed class GraphContextExtendedApiTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void UseProvider_Sets_Provider()
    {
        var ctx = new TestGraphContext();
        var provider = SqlServerGraphProvider.Instance;
        ctx.UseProvider(provider);
        Assert.AreSame(provider, ctx.Provider);
    }

    [TestMethod]
    public void UseProvider_Throws_For_Null()
    {
        var ctx = new TestGraphContext();
        Assert.ThrowsExactly<ArgumentNullException>(() => ctx.UseProvider(null!));
    }

    [TestMethod]
    public void UseExecutionEngine_Sets_Engine()
    {
        var ctx = new TestGraphContext();
        var engine = new InMemoryGraphExecutionEngine(new InMemoryGraphStore());
        ctx.UseExecutionEngine(engine);
        Assert.AreSame(engine, ctx.ExecutionEngine);
    }

    [TestMethod]
    public void UseExecutionEngine_Throws_For_Null()
    {
        var ctx = new TestGraphContext();
        Assert.ThrowsExactly<ArgumentNullException>(() => ctx.UseExecutionEngine(null!));
    }

    [TestMethod]
    public void UseConnectionFactory_Sets_Factory()
    {
        var ctx = new TestGraphContext();
        var factory = new StubConnectionFactory();
        ctx.UseConnectionFactory(factory);
        Assert.AreSame(factory, ctx.ConnectionFactory);
    }

    [TestMethod]
    public void UseConnectionFactory_Throws_For_Null()
    {
        var ctx = new TestGraphContext();
        Assert.ThrowsExactly<ArgumentNullException>(() => ctx.UseConnectionFactory(null!));
    }

    [TestMethod]
    public void Model_Returns_Non_Null_Model()
    {
        var ctx = new TestGraphContext();
        Assert.IsNotNull(ctx.Model);
    }

    [TestMethod]
    public void GetRelationships_Returns_Relationships_For_Node()
    {
        var ctx = new TestGraphContext();
        var relationships = ctx.GetRelationships<TestPerson>();
        Assert.IsNotNull(relationships);
        Assert.IsNotEmpty(relationships);
    }

    [TestMethod]
    public void GetRelationship_Returns_Relationship_By_Name()
    {
        var ctx = new TestGraphContext();
        var rel = ctx.GetRelationship<TestPerson>("Projects");
        Assert.IsNotNull(rel);
        Assert.AreEqual("Projects", rel.Name);
    }

    [TestMethod]
    public void Attach_Sets_State_To_Unchanged()
    {
        var ctx = new TestGraphContext();
        var person = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };

        ctx.Attach(person);

        Assert.AreEqual(EntityState.Unchanged, ctx.Entry(person).State);
    }

    [TestMethod]
    public void AttachRange_Attaches_Multiple_Entities()
    {
        var ctx = new TestGraphContext();
        var people = new object[]
        {
            new TestPerson { Id = Guid.NewGuid(), Name = "Alice" },
            new TestPerson { Id = Guid.NewGuid(), Name = "Bob" }
        };

        ctx.AttachRange(people);

        Assert.HasCount(2, ctx.ChangeTracker.Entries);
    }

    [TestMethod]
    public void Attach_Throws_For_Null()
    {
        var ctx = new TestGraphContext();
        Assert.ThrowsExactly<ArgumentNullException>(() => ctx.Attach(null!));
    }

    [TestMethod]
    public void AttachRange_Throws_For_Null()
    {
        var ctx = new TestGraphContext();
        Assert.ThrowsExactly<ArgumentNullException>(() => ctx.AttachRange(null!));
    }

    [TestMethod]
    public void Remove_Marks_Entity_As_Deleted()
    {
        var ctx = new TestGraphContext();
        var person = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        ctx.Attach(person);

        ctx.Remove(person);

        Assert.AreEqual(EntityState.Deleted, ctx.Entry(person).State);
    }

    [TestMethod]
    public void Remove_Throws_For_Null()
    {
        var ctx = new TestGraphContext();
        Assert.ThrowsExactly<ArgumentNullException>(() => ctx.Remove(null!));
    }

    [TestMethod]
    public void RemoveRange_Removes_All_Entities()
    {
        var ctx = new TestGraphContext();
        var p1 = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        var p2 = new TestPerson { Id = Guid.NewGuid(), Name = "Bob" };
        ctx.AttachRange([p1, p2]);

        ctx.RemoveRange([p1, p2]);

        Assert.AreEqual(EntityState.Deleted, ctx.Entry(p1).State);
        Assert.AreEqual(EntityState.Deleted, ctx.Entry(p2).State);
    }

    [TestMethod]
    public void RemoveRange_Throws_For_Null()
    {
        var ctx = new TestGraphContext();
        Assert.ThrowsExactly<ArgumentNullException>(() => ctx.RemoveRange(null!));
    }

    [TestMethod]
    public void MarkModified_Sets_State_To_Modified()
    {
        var ctx = new TestGraphContext();
        var person = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        ctx.Attach(person);

        ctx.MarkModified(person);

        Assert.AreEqual(EntityState.Modified, ctx.Entry(person).State);
    }

    [TestMethod]
    public void MarkModified_Throws_For_Null()
    {
        var ctx = new TestGraphContext();
        Assert.ThrowsExactly<ArgumentNullException>(() => ctx.MarkModified(null!));
    }

    [TestMethod]
    public void UpdateRange_Marks_All_As_Modified()
    {
        var ctx = new TestGraphContext();
        var p1 = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        var p2 = new TestPerson { Id = Guid.NewGuid(), Name = "Bob" };

        ctx.UpdateRange([p1, p2]);

        Assert.AreEqual(EntityState.Modified, ctx.Entry(p1).State);
        Assert.AreEqual(EntityState.Modified, ctx.Entry(p2).State);
    }

    [TestMethod]
    public void UpdateRange_Throws_For_Null()
    {
        var ctx = new TestGraphContext();
        Assert.ThrowsExactly<ArgumentNullException>(() => ctx.UpdateRange(null!));
    }

    [TestMethod]
    public async Task Connect_Adds_Pending_Edge_Connection()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var person = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        var project = new TestProject { Id = Guid.NewGuid(), Title = "Apollo" };

        ctx.Add(person);
        ctx.Add(project);

        ctx.Connect<TestWorksOn, TestPerson, TestProject>(person, project);

        Assert.IsTrue(ctx.HasChanges());
    }

    [TestMethod]
    public void Connect_With_Configure_Applies_Edge_Properties()
    {
        // InMemory engine does not support edge root queries.
        // Verify the pending edge connection is tracked with the configured property.
        var ctx = new TestGraphContext().UseInMemory();
        var person = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        var project = new TestProject { Id = Guid.NewGuid(), Title = "Apollo" };

        ctx.Add(person);
        ctx.Add(project);

        ctx.Connect<TestWorksOn, TestPerson, TestProject>(person, project, edge => edge.Role = "Lead");

        Assert.HasCount(1, ctx.ChangeTracker.PendingEdgeConnections);
        Assert.AreEqual("Lead", ((TestWorksOn)ctx.ChangeTracker.PendingEdgeConnections[0].Edge).Role);
    }

    [TestMethod]
    public void Disconnect_Removes_Connection()
    {
        // InMemory engine does not support edge root queries.
        // Verify the pending edge disconnection is tracked via the change tracker.
        var ctx = new TestGraphContext().UseInMemory();
        var person = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        var project = new TestProject { Id = Guid.NewGuid(), Title = "Apollo" };

        ctx.Add(person);
        ctx.Add(project);
        ctx.Connect<TestWorksOn, TestPerson, TestProject>(person, project);
        ctx.ChangeTracker.AcceptAllChanges(ctx.Model);

        ctx.Disconnect<TestWorksOn, TestPerson, TestProject>(person, project);

        Assert.HasCount(1, ctx.ChangeTracker.PendingEdgeDisconnections);
    }

    [TestMethod]
    public void AcceptAllChanges_Clears_Pending_Changes()
    {
        var ctx = new TestGraphContext();
        ctx.Add(new TestPerson { Id = Guid.NewGuid(), Name = "Alice" });

        Assert.IsTrue(ctx.HasChanges());

        ctx.AcceptAllChanges();

        Assert.IsFalse(ctx.HasChanges());
    }

    [TestMethod]
    public void Entries_Property_Returns_Tracked_Entries()
    {
        var ctx = new TestGraphContext();
        ctx.Add(new TestPerson { Id = Guid.NewGuid(), Name = "Alice" });

        var entries = ctx.Entries.ToList();

        Assert.HasCount(1, entries);
    }

    [TestMethod]
    public void DetectChanges_Marks_Modified_When_Property_Changed()
    {
        var ctx = new TestGraphContext();
        var person = new TestPerson { Id = Guid.NewGuid(), Name = "Alice", Age = 30 };
        ctx.Attach(person);

        person.Name = "Bob";
        ctx.DetectChanges();

        Assert.AreEqual(EntityState.Modified, ctx.Entry(person).State);
    }

    [TestMethod]
    public async Task SaveChangesAsync_Persists_Added_Nodes()
    {
        var ctx = new TestGraphContext().UseInMemory();
        ctx.Add(new TestPerson { Id = Guid.NewGuid(), Name = "Alice" });

        var affected = await ctx.SaveChangesAsync(TestContext.CancellationToken);

        Assert.AreEqual(1, affected);
    }

    [TestMethod]
    public async Task SaveChangesAsync_Returns_Zero_When_No_Changes()
    {
        var ctx = new TestGraphContext().UseInMemory();

        var affected = await ctx.SaveChangesAsync(TestContext.CancellationToken);

        Assert.AreEqual(0, affected);
    }

    [TestMethod]
    public void ChangeTracker_EntriesFor_Returns_Only_Matching_Type()
    {
        var ctx = new TestGraphContext();
        ctx.Add(new TestPerson { Id = Guid.NewGuid(), Name = "Alice" });
        ctx.Add(new TestProject { Id = Guid.NewGuid(), Title = "Apollo" });

        var personEntries = ctx.ChangeTracker.EntriesFor<TestPerson>().ToList();

        Assert.HasCount(1, personEntries);
        Assert.IsInstanceOfType<TestPerson>(personEntries[0].Entity);
    }

    [TestMethod]
    public void ChangeTracker_EntriesFor_ByType_Returns_Matching()
    {
        var ctx = new TestGraphContext();
        ctx.Add(new TestPerson { Id = Guid.NewGuid(), Name = "Alice" });

        var entries = ctx.ChangeTracker.EntriesFor<TestPerson>().ToList();

        Assert.HasCount(1, entries);
    }

    [TestMethod]
    public void ChangeTracker_EntriesFor_Throws_For_Null_Type()
    {
        var ctx = new TestGraphContext();
        Assert.ThrowsExactly<ArgumentNullException>(() => ctx.ChangeTracker.EntriesFor(null!).ToList());
    }

    [TestMethod]
    public void Add_Assigns_Id_When_Empty()
    {
        var ctx = new TestGraphContext();
        var person = new TestPerson { Id = Guid.Empty, Name = "Alice" };

        ctx.Add(person);

        Assert.AreNotEqual(Guid.Empty, person.Id);
    }

    [TestMethod]
    public void Add_Preserves_Explicit_Id()
    {
        var ctx = new TestGraphContext();
        var id = Guid.NewGuid();
        var person = new TestPerson { Id = id, Name = "Alice" };

        ctx.Add(person);

        Assert.AreEqual(id, person.Id);
    }

    [TestMethod]
    public void Add_Throws_For_Null()
    {
        var ctx = new TestGraphContext();
        Assert.ThrowsExactly<ArgumentNullException>(() => ctx.Add(null!));
    }

    [TestMethod]
    public void AddRange_Throws_For_Null_Enumerable()
    {
        var ctx = new TestGraphContext();
        Assert.ThrowsExactly<ArgumentNullException>(() => ctx.AddRange((IEnumerable<object>)null!));
    }

    [TestMethod]
    public void AddRange_Params_Throws_For_Null()
    {
        var ctx = new TestGraphContext();
        Assert.ThrowsExactly<ArgumentNullException>(() => ctx.AddRange((object[])null!));
    }

    [TestMethod]
    public void PropertySnapshot_Stores_Name_And_Value()
    {
        var snapshot = new PropertySnapshot { PropertyName = "Name", Value = "Alice" };

        Assert.AreEqual("Name", snapshot.PropertyName);
        Assert.AreEqual("Alice", snapshot.Value);
    }

    private sealed class StubConnectionFactory : IGormDbConnectionFactory
    {
        public DbConnection CreateConnection() => throw new NotImplementedException();
    }
}