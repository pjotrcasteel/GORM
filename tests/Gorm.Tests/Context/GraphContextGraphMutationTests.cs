using Gorm.Application.Execution;
using Gorm.Application.Execution.InMemory;
using Gorm.Application.Mutations;
using Gorm.Application.Mutations.Edges;
using Gorm.Application.Mutations.Nodes;
using Gorm.Application.Mutations.Options;
using Gorm.Application.Querying;
using Gorm.Core.Primitives;
using Gorm.Tests.TestSupport;

namespace Gorm.Tests.Context;

[TestClass]
public sealed class GraphContextGraphMutationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void PlanMutation_UpsertNewNode_PlansInsert()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var person = new TestPerson { Name = "Alice" };

        var mutation = GraphMutation.Create().UpsertNode(person, x => x.Key("person-alice"));

        var plan = ctx.PlanMutation(mutation);

        Assert.IsTrue(plan.Validation.IsValid, plan.Validation.ToMessage());
        Assert.HasCount(1, plan.Nodes);
        Assert.AreEqual(GraphMutationPlanAction.Insert, plan.Nodes[0].Action);
        Assert.AreEqual(GraphMutationNodeOperation.Upsert, plan.Nodes[0].Operation);
    }

    [TestMethod]
    public void PlanMutation_UpsertExistingNode_PlansUpdate()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var person = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };

        var mutation = GraphMutation.Create().UpsertNode(person, x => x.Key("person-alice"));

        var plan = ctx.PlanMutation(mutation);

        Assert.IsTrue(plan.Validation.IsValid, plan.Validation.ToMessage());
        Assert.HasCount(1, plan.Nodes);
        Assert.AreEqual(GraphMutationPlanAction.Update, plan.Nodes[0].Action);
        Assert.AreEqual(GraphMutationNodeOperation.Upsert, plan.Nodes[0].Operation);
    }

    [TestMethod]
    public void PlanMutation_AttachExistingNode_PlansAttach()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var person = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };

        var mutation = GraphMutation.Create().AttachNode(person, x => x.Key("person-alice"));

        var plan = ctx.PlanMutation(mutation);

        Assert.IsTrue(plan.Validation.IsValid, plan.Validation.ToMessage());
        Assert.HasCount(1, plan.Nodes);
        Assert.AreEqual(GraphMutationPlanAction.Attach, plan.Nodes[0].Action);
    }

    [TestMethod]
    public void PlanMutation_RemoveExistingNode_PlansDelete()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var person = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };

        var mutation = GraphMutation.Create().RemoveNode(person, x => x.Key("person-alice"));

        var plan = ctx.PlanMutation(mutation);

        Assert.IsTrue(plan.Validation.IsValid, plan.Validation.ToMessage());
        Assert.HasCount(1, plan.Nodes);
        Assert.AreEqual(GraphMutationPlanAction.Delete, plan.Nodes[0].Action);
    }

    [TestMethod]
    public void PlanMutation_UpdateNodeWithoutId_ReturnsValidationError()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var person = new TestPerson { Name = "Alice" };

        var mutation = GraphMutation.Create().UpdateNode(person, x => x.Key("person-alice"));

        var plan = ctx.PlanMutation(mutation);

        Assert.IsFalse(plan.Validation.IsValid);
        Assert.IsTrue(plan.Validation.Errors.Any(x => x.Code == "GraphMutation.Node.MissingId"));
    }

    [TestMethod]
    public void PlanMutation_UnknownNodeType_ReturnsValidationError()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var node = new UnmappedNode { Id = Guid.NewGuid() };

        var mutation = GraphMutation.Create().AttachNode(node, x => x.Key("unknown-node"));

        var plan = ctx.PlanMutation(mutation);

        Assert.IsFalse(plan.Validation.IsValid);
        Assert.IsTrue(plan.Validation.Errors.Any(x => x.Code == "GraphMutation.Node.UnknownType"));
    }

    [TestMethod]
    public void PlanMutation_DuplicateNodeKey_ReturnsValidationError()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var person1 = new TestPerson { Name = "Alice" };
        var person2 = new TestPerson { Name = "Alice Duplicate" };

        var mutation = GraphMutation.Create().UpsertNode(person1, x => x.Key("person-alice")).UpsertNode(person2, x => x.Key("person-alice"));

        var plan = ctx.PlanMutation(mutation);

        Assert.IsFalse(plan.Validation.IsValid);
        Assert.IsTrue(plan.Validation.Errors.Any(x => x.Code == "GraphMutation.Node.DuplicateKey"));
    }

    [TestMethod]
    public void PlanMutation_DuplicateNodeId_ReturnsValidationError()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var id = Guid.NewGuid();
        var person1 = new TestPerson { Id = id, Name = "Alice" };
        var person2 = new TestPerson { Id = id, Name = "Alice Duplicate" };

        var mutation = GraphMutation.Create().UpdateNode(person1).UpdateNode(person2);

        var plan = ctx.PlanMutation(mutation);

        Assert.IsFalse(plan.Validation.IsValid);
        Assert.IsTrue(plan.Validation.Errors.Any(x => x.Code == "GraphMutation.Node.DuplicateId"));
    }

    [TestMethod]
    public void PlanMutation_AddEdge_PlansInsert()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var person = new TestPerson { Name = "Alice" };
        var project = new TestProject { Title = "Alpha" };

        var mutation = GraphMutation.Create()
            .UpsertNode(person, x => x.Key("person-alice"))
            .UpsertNode(project, x => x.Key("project-alpha"))
            .AddEdge<TestWorksOn, TestPerson, TestProject>(person, project, configure: x => x.Key("alice-works-on-alpha"));

        var plan = ctx.PlanMutation(mutation);

        Assert.IsTrue(plan.Validation.IsValid, plan.Validation.ToMessage());
        Assert.HasCount(1, plan.Edges);
        Assert.AreEqual(GraphMutationPlanAction.Insert, plan.Edges[0].Action);
        Assert.AreEqual(GraphMutationEdgeOperation.Add, plan.Edges[0].Operation);
    }

    [TestMethod]
    public void PlanMutation_UpsertNewEdge_PlansInsert()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var person = new TestPerson { Name = "Alice" };
        var project = new TestProject { Title = "Alpha" };

        var mutation = GraphMutation.Create()
            .UpsertNode(person, x => x.Key("person-alice"))
            .UpsertNode(project, x => x.Key("project-alpha"))
            .UpsertEdge<TestWorksOn, TestPerson, TestProject>(person, project, configure: x => x.Key("alice-works-on-alpha"));

        var plan = ctx.PlanMutation(mutation);

        Assert.IsTrue(plan.Validation.IsValid, plan.Validation.ToMessage());
        Assert.HasCount(1, plan.Edges);
        Assert.AreEqual(GraphMutationPlanAction.Insert, plan.Edges[0].Action);
        Assert.AreEqual(GraphMutationEdgeOperation.Upsert, plan.Edges[0].Operation);
    }

    [TestMethod]
    public void PlanMutation_UpsertExistingEdge_PlansUpdate()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var person = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        var project = new TestProject { Id = Guid.NewGuid(), Title = "Alpha" };
        var edge = new TestWorksOn { Id = Guid.NewGuid(), Role = "Developer" };

        var mutation = GraphMutation.Create()
            .AttachNode(person, x => x.Key("person-alice"))
            .AttachNode(project, x => x.Key("project-alpha"))
            .UpsertEdge(person, project, edge, x => x.Key("alice-works-on-alpha"));

        var plan = ctx.PlanMutation(mutation);

        Assert.IsTrue(plan.Validation.IsValid, plan.Validation.ToMessage());
        Assert.HasCount(1, plan.Edges);
        Assert.AreEqual(GraphMutationPlanAction.Update, plan.Edges[0].Action);
    }

    [TestMethod]
    public void PlanMutation_RemoveEdge_PlansDelete()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var person = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        var project = new TestProject { Id = Guid.NewGuid(), Title = "Alpha" };

        var mutation = GraphMutation.Create()
            .AttachNode(person, x => x.Key("person-alice"))
            .AttachNode(project, x => x.Key("project-alpha"))
            .RemoveEdge<TestWorksOn, TestPerson, TestProject>(person, project, x => x.Key("alice-works-on-alpha"));

        var plan = ctx.PlanMutation(mutation);

        Assert.IsTrue(plan.Validation.IsValid, plan.Validation.ToMessage());
        Assert.HasCount(1, plan.Edges);
        Assert.AreEqual(GraphMutationPlanAction.Delete, plan.Edges[0].Action);
    }

    [TestMethod]
    public void PlanMutation_RemoveEdgeWithoutEndpointIds_ReturnsValidationError()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var person = new TestPerson { Name = "Alice" };
        var project = new TestProject { Title = "Alpha" };

        var mutation = GraphMutation.Create().RemoveEdge<TestWorksOn, TestPerson, TestProject>(person, project, x => x.Key("alice-works-on-alpha"));

        var plan = ctx.PlanMutation(mutation);

        Assert.IsFalse(plan.Validation.IsValid);
        Assert.IsTrue(plan.Validation.Errors.Any(x => x.Code == "GraphMutation.Edge.RemoveMissingEndpointId"));
    }

    [TestMethod]
    public void PlanMutation_UnknownEdgeType_ReturnsValidationError()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var person = new TestPerson { Name = "Alice" };
        var project = new TestProject { Title = "Alpha" };

        var mutation = GraphMutation.Create()
            .UpsertNode(person, x => x.Key("person-alice"))
            .UpsertNode(project, x => x.Key("project-alpha"))
            .UpsertEdge<UnmappedEdge, TestPerson, TestProject>(person, project, configure: x => x.Key("unknown-edge"));

        var plan = ctx.PlanMutation(mutation);

        Assert.IsFalse(plan.Validation.IsValid);
        Assert.IsTrue(plan.Validation.Errors.Any(x => x.Code == "GraphMutation.Edge.UnknownType"));
    }

    [TestMethod]
    public void PlanMutation_InvalidSourceNodeType_ReturnsValidationError()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var invalidSource = new TestProject { Title = "Not a person" };
        var target = new TestProject { Title = "Alpha" };

        var mutation = GraphMutation.Create()
            .UpsertNode(invalidSource, x => x.Key("project-source"))
            .UpsertNode(target, x => x.Key("project-target"))
            .UpsertEdge<TestWorksOn, TestProject, TestProject>(invalidSource, target, configure: x => x.Key("invalid-source"));

        var plan = ctx.PlanMutation(mutation);

        Assert.IsFalse(plan.Validation.IsValid);
        Assert.IsTrue(plan.Validation.Errors.Any(x => x.Code == "GraphMutation.Edge.InvalidSourceType"));
    }

    [TestMethod]
    public void PlanMutation_InvalidTargetNodeType_ReturnsValidationError()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var source = new TestPerson { Name = "Alice" };
        var invalidTarget = new TestPerson { Name = "Bob" };

        var mutation = GraphMutation.Create()
            .UpsertNode(source, x => x.Key("person-alice"))
            .UpsertNode(invalidTarget, x => x.Key("person-bob"))
            .UpsertEdge<TestWorksOn, TestPerson, TestPerson>(source, invalidTarget, configure: x => x.Key("invalid-target"));

        var plan = ctx.PlanMutation(mutation);

        Assert.IsFalse(plan.Validation.IsValid);
        Assert.IsTrue(plan.Validation.Errors.Any(x => x.Code == "GraphMutation.Edge.InvalidTargetType"));
    }

    [TestMethod]
    public void PlanMutation_DuplicateUpsertEdgeIdentity_ReturnsValidationError()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var person = new TestPerson { Name = "Alice" };
        var project = new TestProject { Title = "Alpha" };

        var mutation = GraphMutation.Create()
            .UpsertNode(person, x => x.Key("person-alice"))
            .UpsertNode(project, x => x.Key("project-alpha"))
            .UpsertEdge<TestWorksOn, TestPerson, TestProject>(person, project)
            .UpsertEdge<TestWorksOn, TestPerson, TestProject>(person, project);

        var plan = ctx.PlanMutation(mutation);

        Assert.IsFalse(plan.Validation.IsValid);
        Assert.IsTrue(plan.Validation.Errors.Any(x => x.Code == "GraphMutation.Edge.DuplicateUpsertIdentity"));
    }

    [TestMethod]
    public void PlanMutation_DuplicateEdgeInstance_ReturnsValidationError()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var person = new TestPerson { Name = "Alice" };
        var project1 = new TestProject { Title = "Alpha" };
        var project2 = new TestProject { Title = "Beta" };
        var edge = new TestWorksOn();

        var mutation = GraphMutation.Create();

        mutation.UpsertNode(person, x => x.Key("person-alice"));
        mutation.UpsertNode(project1, x => x.Key("project-alpha"));
        mutation.UpsertNode(project2, x => x.Key("project-beta"));
        mutation.UpsertEdge(person, project1, edge, x => x.Key("edge-1"));

        Assert.ThrowsExactly<InvalidOperationException>(() => mutation.UpsertEdge(person, project2, edge, x => x.Key("edge-2")));
    }

    [TestMethod]
    public void ExecuteMutationPlan_InvalidPlanWithThrowOnValidationError_ThrowsInvalidOperationException()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var node = new UnmappedNode { Id = Guid.NewGuid() };

        var mutation = GraphMutation.Create().AttachNode(node, x => x.Key("unknown-node"));

        var plan = ctx.PlanMutation(mutation);

        Assert.ThrowsExactly<InvalidOperationException>(() => ctx.ExecuteMutationPlan(plan));
    }

    [TestMethod]
    public void ExecuteMutationPlan_InvalidPlanWithoutThrowOnValidationError_DoesNotTouchChangeTracker()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var node = new UnmappedNode { Id = Guid.NewGuid() };

        var mutation = GraphMutation.Create().AttachNode(node, x => x.Key("unknown-node"));

        var plan = ctx.PlanMutation(mutation);

        ctx.ExecuteMutationPlan(plan, new GraphMutationOptions { ThrowOnValidationError = false });

        Assert.IsEmpty(ctx.Entries);
    }

    [TestMethod]
    public async Task ApplyMutationAsync_NewGraph_PersistsNodesAndEdges()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var person = new TestPerson { Name = "Alice" };
        var project1 = new TestProject { Title = "Alpha" };
        var project2 = new TestProject { Title = "Beta" };

        var mutation = GraphMutation.Create("new-graph")
            .WithCorrelationId("correlation-1")
            .WithSourceEventId("event-1")
            .UpsertNode(person, x => x.Key("person-alice"))
            .UpsertNode(project1, x => x.Key("project-alpha"))
            .UpsertNode(project2, x => x.Key("project-beta"))
            .UpsertEdge<TestWorksOn, TestPerson, TestProject>(person, project1, configure: x => x.Key("alice-alpha"))
            .UpsertEdge<TestWorksOn, TestPerson, TestProject>(person, project2, configure: x => x.Key("alice-beta"));

        await ctx.ApplyMutationAsync(mutation, TestContext.CancellationToken);

        var projects = await ctx.People.Where(x => x.Id == person.Id).Outgoing<TestWorksOn, TestProject>().ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(2, projects);
        Assert.IsTrue(projects.Any(x => x.Title == "Alpha"));
        Assert.IsTrue(projects.Any(x => x.Title == "Beta"));
    }

    [TestMethod]
    public async Task ApplyMutationAsync_ExistingGraphWithNewEdge_PersistsNewEdge()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var person = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };
        var existingProject = new TestProject { Id = Guid.NewGuid(), Title = "Existing" };
        var newProject = new TestProject { Title = "New" };

        ctx.Add(person);
        ctx.Add(existingProject);
        ctx.Connect<TestWorksOn, TestPerson, TestProject>(person, existingProject);
        await ctx.SaveChangesAsync(TestContext.CancellationToken);
        ctx.AcceptAllChanges();

        var mutation = GraphMutation.Create("existing-graph-new-edge")
            .AttachNode(person, x => x.Key("person-alice"))
            .AttachNode(existingProject, x => x.Key("project-existing"))
            .UpsertNode(newProject, x => x.Key("project-new"))
            .UpsertEdge<TestWorksOn, TestPerson, TestProject>(person, newProject, configure: x => x.Key("alice-new"));

        await ctx.ApplyMutationAsync(mutation, TestContext.CancellationToken);

        var projects = await ctx.People.Where(x => x.Id == person.Id).Outgoing<TestWorksOn, TestProject>().ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(2, projects);
        Assert.IsTrue(projects.Any(x => x.Title == "Existing"));
        Assert.IsTrue(projects.Any(x => x.Title == "New"));
    }

    [TestMethod]
    public async Task ApplyMutationAsync_UpdateExistingNode_PersistsUpdate()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var person = new TestPerson { Id = Guid.NewGuid(), Name = "Alice" };

        ctx.Add(person);
        await ctx.SaveChangesAsync(TestContext.CancellationToken);
        ctx.AcceptAllChanges();

        person.Name = "Alice Updated";

        var mutation = GraphMutation.Create("update-existing-node").UpdateNode(person, x => x.Key("person-alice"));

        await ctx.ApplyMutationAsync(mutation, TestContext.CancellationToken);

        var updatedPerson = await ctx.People.Where(x => x.Id == person.Id).SingleAsync(TestContext.CancellationToken);

        Assert.AreEqual("Alice Updated", updatedPerson.Name);
    }

    [TestMethod]
    public void PlanMutation_ToDebugString_ContainsMutationSummary()
    {
        var ctx = new TestGraphContext().UseInMemory();
        var person = new TestPerson { Name = "Alice" };
        var project = new TestProject { Title = "Alpha" };

        var mutation = GraphMutation.Create("debug-test")
            .WithCorrelationId("correlation-1")
            .WithSourceEventId("event-1")
            .UpsertNode(person, x => x.Key("person-alice"))
            .UpsertNode(project, x => x.Key("project-alpha"))
            .UpsertEdge<TestWorksOn, TestPerson, TestProject>(person, project, configure: x => x.Key("alice-alpha"));

        var plan = ctx.PlanMutation(mutation);
        var debugView = plan.ToDebugString();

        Assert.Contains("Graph mutation: debug-test", debugView);
        Assert.Contains("CorrelationId: correlation-1", debugView);
        Assert.Contains("SourceEventId: event-1", debugView);
        Assert.Contains("Nodes:", debugView);
        Assert.Contains("Edges:", debugView);
    }

    private sealed class UnmappedNode : Node
    {
    }

    private sealed class UnmappedEdge : Edge
    {
    }
}