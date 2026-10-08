using Gorm.Application.Context;
using Gorm.Application.Execution.InMemory;
using Gorm.Application.History;
using Gorm.Application.History.Querying;
using Gorm.Application.History.Storage;
using Gorm.Core.Configuration;
using Gorm.Core.Metadata;
using Gorm.Core.Primitives;
using Microsoft.Extensions.Time.Testing;

namespace Gorm.Tests.History;

[TestClass]
public sealed class InMemoryGraphHistorySmokeTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task History_Captures_Create_Update_And_Delete_For_Node()
    {
        var store = new InMemoryGraphStore();
        var historyStore = new InMemoryGraphHistoryStore();
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 4, 19, 12, 0, 0, TimeSpan.Zero));

        var context = new DemoHistoryGraphContext().UseInMemory(store, historyStore);

        context.UseTimeProvider(timeProvider);

        var person = new PersonNode
        {
            Id = Guid.NewGuid(),
            Name = "Alice"
        };

        context.Add(person);
        await context.SaveChangesAsync(TestContext.CancellationToken);

        timeProvider.Advance(TimeSpan.FromMinutes(1));

        person.Name = "Alice Updated";
        context.Update(person);
        await context.SaveChangesAsync(TestContext.CancellationToken);

        timeProvider.Advance(TimeSpan.FromMinutes(1));

        context.Remove(person);
        await context.SaveChangesAsync(TestContext.CancellationToken);

        var history = context.History<PersonNode>().OrderBy(x => x.CapturedAtUtc).ToList();

        Assert.HasCount(4, history);
        Assert.AreEqual(GraphHistoryOperationKind.Created, history[0].OperationKind);
        Assert.AreEqual(GraphHistoryOperationKind.Updated, history[1].OperationKind);
        Assert.AreEqual(GraphHistoryOperationKind.Updated, history[2].OperationKind);
        Assert.AreEqual(GraphHistoryOperationKind.Deleted, history[3].OperationKind);

        var created = history[0].GetSnapshotOfType<PersonNode>();
        var updatedOld = history[1].GetSnapshotOfType<PersonNode>();
        var updatedNew = history[2].GetSnapshotOfType<PersonNode>();
        var deleted = history[3].GetSnapshotOfType<PersonNode>();

        Assert.AreEqual("Alice", created.Name);
        Assert.AreEqual("Alice", updatedOld.Name);
        Assert.AreEqual("Alice Updated", updatedNew.Name);
        Assert.AreEqual("Alice Updated", deleted.Name);
    }

    [TestMethod]
    public async Task History_Does_Not_Capture_When_InMemory_Save_Is_Cancelled()
    {
        var store = new InMemoryGraphStore();
        var historyStore = new InMemoryGraphHistoryStore();
        var context = new DemoHistoryGraphContext().UseInMemory(store, historyStore);
        context.Add(new PersonNode { Id = Guid.NewGuid(), Name = "Alice" });
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => context.SaveChangesAsync(cancellation.Token));

        Assert.IsEmpty(historyStore.Entries);
    }

    [TestMethod]
    public async Task History_Captures_Mapped_Properties_Without_Serializing_Reciprocal_Navigations()
    {
        var store = new InMemoryGraphStore();
        var historyStore = new InMemoryGraphHistoryStore();
        var context = new DemoHistoryGraphContext().UseInMemory(store, historyStore);
        var person = new PersonNode { Id = Guid.NewGuid(), Name = "Alice" };
        var project = new ProjectNode { Id = Guid.NewGuid(), Code = "P1" };
        person.Projects.Add(project);
        project.People.Add(person);
        context.Add(person);
        context.Add(project);

        await context.SaveChangesAsync(TestContext.CancellationToken);

        var snapshot = context.History<PersonNode>().Single().GetSnapshotOfType<PersonNode>();
        Assert.AreEqual("Alice", snapshot.Name);
        Assert.IsEmpty(snapshot.Projects);
    }

    [TestMethod]
    public async Task EdgeHistory_Captures_Connect_And_Disconnect()
    {
        var store = new InMemoryGraphStore();
        var historyStore = new InMemoryGraphHistoryStore();
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 4, 19, 12, 0, 0, TimeSpan.Zero));

        var context = new DemoHistoryGraphContext().UseInMemory(store, historyStore);

        context.UseTimeProvider(timeProvider);

        var person = new PersonNode
        {
            Id = Guid.NewGuid(),
            Name = "Alice"
        };

        var project = new ProjectNode
        {
            Id = Guid.NewGuid(),
            Code = "P1"
        };

        context.Add(person);
        context.Add(project);
        await context.SaveChangesAsync(TestContext.CancellationToken);

        timeProvider.Advance(TimeSpan.FromMinutes(1));

        context.Connect<WorksOnEdge, PersonNode, ProjectNode>(person, project);
        await context.SaveChangesAsync(TestContext.CancellationToken);

        timeProvider.Advance(TimeSpan.FromMinutes(1));

        context.Disconnect<WorksOnEdge, PersonNode, ProjectNode>(person, project);
        await context.SaveChangesAsync(TestContext.CancellationToken);

        var edgeHistory = context.EdgeHistory<WorksOnEdge>().OrderBy(x => x.CapturedAtUtc).ToList();

        Assert.HasCount(2, edgeHistory);
        Assert.AreEqual(GraphHistoryOperationKind.Connected, edgeHistory[0].OperationKind);
        Assert.AreEqual(GraphHistoryOperationKind.Disconnected, edgeHistory[1].OperationKind);
    }

    [TestMethod]
    public async Task AsOf_Returns_The_Node_Snapshot_Valid_At_The_Specified_Time()
    {
        var store = new InMemoryGraphStore();
        var historyStore = new InMemoryGraphHistoryStore();
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 4, 19, 12, 0, 0, TimeSpan.Zero));

        var context = new DemoHistoryGraphContext().UseInMemory(store, historyStore);

        context.UseTimeProvider(timeProvider);

        var person = new PersonNode
        {
            Id = Guid.NewGuid(),
            Name = "Alice"
        };

        context.Add(person);
        await context.SaveChangesAsync(TestContext.CancellationToken);

        var afterCreate = timeProvider.GetUtcNow().UtcDateTime;

        timeProvider.Advance(TimeSpan.FromMinutes(1));

        person.Name = "Alice Updated";
        context.Update(person);
        await context.SaveChangesAsync(TestContext.CancellationToken);

        var afterUpdate = timeProvider.GetUtcNow().UtcDateTime;

        var atCreate = context.History<PersonNode>().AsOf(afterCreate).SelectNodeSnapshot<PersonNode>().Single(x => x.Id == person.Id);

        var atUpdate = context.History<PersonNode>().AsOf(afterUpdate).SelectNodeSnapshot<PersonNode>().Single(x => x.Id == person.Id);

        Assert.AreEqual("Alice", atCreate.Name);
        Assert.AreEqual("Alice Updated", atUpdate.Name);
    }

    [TestMethod]
    public async Task Current_Returns_Only_The_Open_History_Record()
    {
        var store = new InMemoryGraphStore();
        var historyStore = new InMemoryGraphHistoryStore();
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 4, 19, 12, 0, 0, TimeSpan.Zero));

        var context = new DemoHistoryGraphContext().UseInMemory(store, historyStore);

        context.UseTimeProvider(timeProvider);

        var person = new PersonNode
        {
            Id = Guid.NewGuid(),
            Name = "Alice"
        };

        context.Add(person);
        await context.SaveChangesAsync(TestContext.CancellationToken);

        timeProvider.Advance(TimeSpan.FromMinutes(1));

        person.Name = "Alice Updated";
        context.Update(person);
        await context.SaveChangesAsync(TestContext.CancellationToken);

        var currentEntries = context.History<PersonNode>().Current().Where(x => x.EntityId == person.Id).ToList();

        Assert.HasCount(1, currentEntries);

        var currentSnapshot = currentEntries[0].GetSnapshotOfType<PersonNode>();
        Assert.AreEqual("Alice Updated", currentSnapshot.Name);
    }

    [TestMethod]
    public async Task Between_Returns_History_That_Overlaps_The_Specified_Range()
    {
        var store = new InMemoryGraphStore();
        var historyStore = new InMemoryGraphHistoryStore();
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 4, 19, 12, 0, 0, TimeSpan.Zero));

        var context = new DemoHistoryGraphContext().UseInMemory(store, historyStore);

        context.UseTimeProvider(timeProvider);

        var person = new PersonNode
        {
            Id = Guid.NewGuid(),
            Name = "Alice"
        };

        context.Add(person);
        await context.SaveChangesAsync(TestContext.CancellationToken);

        var t1 = timeProvider.GetUtcNow().UtcDateTime;

        timeProvider.Advance(TimeSpan.FromMinutes(1));

        person.Name = "Alice Updated";
        context.Update(person);
        await context.SaveChangesAsync(TestContext.CancellationToken);

        var t2 = timeProvider.GetUtcNow().UtcDateTime;

        var entries = context.History<PersonNode>().Between(t1, t2).Where(x => x.EntityId == person.Id).OrderByCapturedAt().ToList();

        Assert.IsGreaterThanOrEqualTo(2, entries.Count);
    }

    [TestMethod]
    public async Task TemporalOutgoing_Returns_Target_Node_Valid_At_The_Specified_Time()
    {
        var store = new InMemoryGraphStore();
        var historyStore = new InMemoryGraphHistoryStore();
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 4, 19, 12, 0, 0, TimeSpan.Zero));

        var context = new DemoHistoryGraphContext().UseInMemory(store, historyStore);

        context.UseTimeProvider(timeProvider);

        var person = new PersonNode
        {
            Id = Guid.NewGuid(),
            Name = "Alice"
        };

        var project = new ProjectNode
        {
            Id = Guid.NewGuid(),
            Code = "P1"
        };

        context.Add(person);
        context.Add(project);
        await context.SaveChangesAsync(TestContext.CancellationToken);

        timeProvider.Advance(TimeSpan.FromMinutes(1));

        context.Connect<WorksOnEdge, PersonNode, ProjectNode>(person, project);
        await context.SaveChangesAsync(TestContext.CancellationToken);

        var afterConnect = timeProvider.GetUtcNow().UtcDateTime;

        var projects = context.History<PersonNode>().AsOf(afterConnect).Where(x => x.EntityId == person.Id).TemporalOutgoing<WorksOnEdge, ProjectNode>(context).ToList();

        Assert.HasCount(1, projects);
        Assert.AreEqual("P1", projects[0].Code);
    }

    [TestMethod]
    public async Task TemporalIncoming_Returns_Source_Node_Valid_At_The_Specified_Time()
    {
        var store = new InMemoryGraphStore();
        var historyStore = new InMemoryGraphHistoryStore();
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 4, 19, 12, 0, 0, TimeSpan.Zero));

        var context = new DemoHistoryGraphContext().UseInMemory(store, historyStore);

        context.UseTimeProvider(timeProvider);

        var person = new PersonNode
        {
            Id = Guid.NewGuid(),
            Name = "Alice"
        };

        var project = new ProjectNode
        {
            Id = Guid.NewGuid(),
            Code = "P1"
        };

        context.Add(person);
        context.Add(project);
        await context.SaveChangesAsync(TestContext.CancellationToken);

        timeProvider.Advance(TimeSpan.FromMinutes(1));

        context.Connect<WorksOnEdge, PersonNode, ProjectNode>(person, project);
        await context.SaveChangesAsync(TestContext.CancellationToken);

        var afterConnect = timeProvider.GetUtcNow().UtcDateTime;

        var people = context.History<ProjectNode>().AsOf(afterConnect).Where(x => x.EntityId == project.Id).TemporalIncoming<WorksOnEdge, PersonNode>(context).ToList();

        Assert.HasCount(1, people);
        Assert.AreEqual("Alice", people[0].Name);
    }

    [TestMethod]
    public async Task TemporalOutgoing_Returns_Empty_After_Disconnect()
    {
        var store = new InMemoryGraphStore();
        var historyStore = new InMemoryGraphHistoryStore();
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 4, 19, 12, 0, 0, TimeSpan.Zero));

        var context = new DemoHistoryGraphContext().UseInMemory(store, historyStore);

        context.UseTimeProvider(timeProvider);

        var person = new PersonNode
        {
            Id = Guid.NewGuid(),
            Name = "Alice"
        };

        var project = new ProjectNode
        {
            Id = Guid.NewGuid(),
            Code = "P1"
        };

        context.Add(person);
        context.Add(project);
        await context.SaveChangesAsync(TestContext.CancellationToken);

        timeProvider.Advance(TimeSpan.FromMinutes(1));

        context.Connect<WorksOnEdge, PersonNode, ProjectNode>(person, project);
        await context.SaveChangesAsync(TestContext.CancellationToken);

        timeProvider.Advance(TimeSpan.FromMinutes(1));

        context.Disconnect<WorksOnEdge, PersonNode, ProjectNode>(person, project);
        await context.SaveChangesAsync(TestContext.CancellationToken);

        var afterDisconnect = timeProvider.GetUtcNow().UtcDateTime;

        var projects = context.History<PersonNode>().AsOf(afterDisconnect).Where(x => x.EntityId == person.Id).TemporalOutgoing<WorksOnEdge, ProjectNode>(context).ToList();

        Assert.IsEmpty(projects);
    }

    [TestMethod]
    public async Task TemporalSelectWithEdge_Returns_Node_And_Historical_Edge_Data()
    {
        var store = new InMemoryGraphStore();
        var historyStore = new InMemoryGraphHistoryStore();
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 4, 19, 12, 0, 0, TimeSpan.Zero));

        var context = new DemoHistoryGraphContext().UseInMemory(store, historyStore);

        context.UseTimeProvider(timeProvider);

        var person = new PersonNode
        {
            Id = Guid.NewGuid(),
            Name = "Alice"
        };

        var project = new ProjectNode
        {
            Id = Guid.NewGuid(),
            Code = "P1"
        };

        context.Add(person);
        context.Add(project);
        await context.SaveChangesAsync(TestContext.CancellationToken);

        timeProvider.Advance(TimeSpan.FromMinutes(1));

        context.Connect<WorksOnEdge, PersonNode, ProjectNode>(person, project, edge => edge.IsPrimary = true);

        await context.SaveChangesAsync(TestContext.CancellationToken);

        var afterConnect = timeProvider.GetUtcNow().UtcDateTime;

        var assignments = context.History<PersonNode>()
            .AsOf(afterConnect)
            .Where(x => x.EntityId == person.Id)
            .TemporalSelectWithEdge<WorksOnEdge, ProjectNode, ProjectAssignmentDto>(
                context,
                (node, edge) => new ProjectAssignmentDto
                {
                    ProjectCode = node.Code,
                    IsPrimary = edge.IsPrimary
                })
            .ToList();

        Assert.HasCount(1, assignments);
        Assert.AreEqual("P1", assignments[0].ProjectCode);
        Assert.IsTrue(assignments[0].IsPrimary);
    }

    [TestMethod]
    public async Task TemporalSelectIncomingWithEdge_Returns_Node_And_Historical_Edge_Data()
    {
        var store = new InMemoryGraphStore();
        var historyStore = new InMemoryGraphHistoryStore();
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 4, 19, 12, 0, 0, TimeSpan.Zero));

        var context = new DemoHistoryGraphContext().UseInMemory(store, historyStore);

        context.UseTimeProvider(timeProvider);

        var person = new PersonNode
        {
            Id = Guid.NewGuid(),
            Name = "Alice"
        };

        var project = new ProjectNode
        {
            Id = Guid.NewGuid(),
            Code = "P1"
        };

        context.Add(person);
        context.Add(project);
        await context.SaveChangesAsync(TestContext.CancellationToken);

        timeProvider.Advance(TimeSpan.FromMinutes(1));

        context.Connect<WorksOnEdge, PersonNode, ProjectNode>(person, project, edge => edge.IsPrimary = true);

        await context.SaveChangesAsync(TestContext.CancellationToken);

        var afterConnect = timeProvider.GetUtcNow().UtcDateTime;

        var assignments = context.History<ProjectNode>()
            .AsOf(afterConnect)
            .Where(x => x.EntityId == project.Id)
            .TemporalSelectIncomingWithEdge<WorksOnEdge, PersonNode, PersonAssignmentDto>(
                context,
                (node, edge) => new PersonAssignmentDto
                {
                    PersonName = node.Name,
                    IsPrimary = edge.IsPrimary
                })
            .ToList();

        Assert.HasCount(1, assignments);
        Assert.AreEqual("Alice", assignments[0].PersonName);
        Assert.IsTrue(assignments[0].IsPrimary);
    }

    private sealed class DemoHistoryGraphContext : GraphContext
    {
        protected override void OnModelCreating(GraphModelBuilder modelBuilder)
        {
            modelBuilder.Node<PersonNode>(node =>
            {
                node.ToTable("Persons");
                node.Property(x => x.Name);
                node.HasOutgoingRelationship<WorksOnEdge, ProjectNode>(relationshipName: nameof(PersonNode.Projects), multiplicity: GraphRelationshipMultiplicity.Many);
                node.HasNavigation(x => x.Projects, nameof(PersonNode.Projects));
            });

            modelBuilder.Node<ProjectNode>(node =>
            {
                node.ToTable("Projects");
                node.Property(x => x.Code);
                node.HasIncomingRelationship<WorksOnEdge, PersonNode>(relationshipName: nameof(ProjectNode.People), multiplicity: GraphRelationshipMultiplicity.Many);
                node.HasNavigation(x => x.People, nameof(ProjectNode.People));
            });

            modelBuilder.Edge<WorksOnEdge>(edge =>
            {
                edge.ToTable("WorksOn");
                edge.From<PersonNode>();
                edge.To<ProjectNode>();
                edge.Property(x => x.IsPrimary);
            });
        }
    }

    private sealed class ProjectAssignmentDto
    {
        public string ProjectCode { get; set; } = string.Empty;

        public bool IsPrimary { get; set; }
    }

    private sealed class PersonAssignmentDto
    {
        public string PersonName { get; set; } = string.Empty;

        public bool IsPrimary { get; set; }
    }

    private sealed class PersonNode : Node
    {
        public string Name { get; set; } = string.Empty;

        public List<ProjectNode> Projects { get; set; } = [];
    }

    private sealed class ProjectNode : Node
    {
        public string Code { get; set; } = string.Empty;

        public List<PersonNode> People { get; set; } = [];
    }

    private sealed class WorksOnEdge : Edge
    {
        public bool IsPrimary { get; set; }
    }
}