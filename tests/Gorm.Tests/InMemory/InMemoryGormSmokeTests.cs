using Gorm.Application.Context;
using Gorm.Application.Execution;
using Gorm.Application.Execution.InMemory;
using Gorm.Application.Querying;
using Gorm.Core.Configuration;
using Gorm.Core.Metadata;
using Gorm.Core.Primitives;

namespace Gorm.Tests.Inmemory;

[TestClass]
public sealed class InMemoryGormSmokeTests
{
    public TestContext TestContext { get; set; } = null!;

    private static readonly string[] Expected = ["Build"];

    [TestMethod]
    public async Task SelectWithEdge_AfterOutgoing_Works_InMemory()
    {
        var context = new DemoGraphContext().UseInMemory();

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
        context.Connect<WorksOnEdge, PersonNode, ProjectNode>(person, project, edge => edge.IsPrimary = true);

        await context.SaveChangesAsync(TestContext.CancellationToken);

        var result = await context.Set<PersonNode>()
            .Where(x => x.Id == person.Id)
            .Outgoing<WorksOnEdge, ProjectNode>()
            .SelectWithEdge<WorksOnEdge, ProjectNode, ProjectAssignmentDto>(
                (node, edge) => new ProjectAssignmentDto
                {
                    ProjectCode = node.Code,
                    IsPrimary = edge.IsPrimary
                })
            .SingleAsync(TestContext.CancellationToken);

        Assert.AreEqual("P1", result.ProjectCode);
        Assert.IsTrue(result.IsPrimary);
    }

    [TestMethod]
    public async Task Include_Loads_Related_Nodes_InMemory()
    {
        var context = new DemoGraphContext().UseInMemory();

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
        context.Connect<WorksOnEdge, PersonNode, ProjectNode>(person, project);

        await context.SaveChangesAsync(TestContext.CancellationToken);

        var loaded = await context.Set<PersonNode>().Where(x => x.Id == person.Id).Include(x => x.Projects).SingleAsync(TestContext.CancellationToken);

        Assert.HasCount(1, loaded.Projects);
        Assert.AreEqual("P1", loaded.Projects[0].Code);
    }

    [TestMethod]
    public async Task ThenOutgoing_Works_InMemory()
    {
        var context = new DemoGraphContext().UseInMemory();

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

        var task = new TaskNode
        {
            Id = Guid.NewGuid(),
            Title = "Build"
        };

        context.Add(person);
        context.Add(project);
        context.Add(task);

        context.Connect<WorksOnEdge, PersonNode, ProjectNode>(person, project);
        context.Connect<ProjectHasTaskEdge, ProjectNode, TaskNode>(project, task);

        await context.SaveChangesAsync(TestContext.CancellationToken);

        var titles = await context.Set<PersonNode>()
            .Where(x => x.Id == person.Id)
            .Outgoing<WorksOnEdge, ProjectNode>()
            .ThenOutgoing<ProjectHasTaskEdge, TaskNode>()
            .Select(x => x.Title)
            .ToListAsync(TestContext.CancellationToken);

        CollectionAssert.AreEqual(Expected, titles);
    }

    private sealed class DemoGraphContext : GraphContext
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
                node.Property(x => x.Name);
                node.HasIncomingRelationship<WorksOnEdge, PersonNode>(relationshipName: nameof(ProjectNode.People), multiplicity: GraphRelationshipMultiplicity.Many);
                node.HasNavigation(x => x.People, nameof(ProjectNode.People));

                node.HasOutgoingRelationship<ProjectHasTaskEdge, TaskNode>(relationshipName: nameof(ProjectNode.Tasks), multiplicity: GraphRelationshipMultiplicity.Many);
                node.HasNavigation(x => x.Tasks, nameof(ProjectNode.Tasks));
            });

            modelBuilder.Node<TaskNode>(node =>
            {
                node.ToTable("Tasks");
                node.Property(x => x.Title);
                node.HasIncomingRelationship<ProjectHasTaskEdge, ProjectNode>(relationshipName: nameof(TaskNode.Projects), multiplicity: GraphRelationshipMultiplicity.Many);
                node.HasNavigation(x => x.Projects, nameof(TaskNode.Projects));
            });

            modelBuilder.Edge<WorksOnEdge>(edge =>
            {
                edge.ToTable("WorksOn");
                edge.From<PersonNode>();
                edge.To<ProjectNode>();
                edge.Property(x => x.IsPrimary);
            });

            modelBuilder.Edge<ProjectHasTaskEdge>(edge =>
            {
                edge.ToTable("ProjectHasTask");
                edge.From<ProjectNode>();
                edge.To<TaskNode>();
            });
        }
    }

    private sealed class PersonNode : Node
    {
        public string Name { get; set; } = string.Empty;

        public List<ProjectNode> Projects { get; set; } = [];
    }

    private sealed class ProjectNode : Node
    {
        public string Code { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public List<PersonNode> People { get; set; } = [];

        public List<TaskNode> Tasks { get; set; } = [];
    }

    private sealed class TaskNode : Node
    {
        public string Title { get; set; } = string.Empty;

        public List<ProjectNode> Projects { get; set; } = [];
    }

    private sealed class WorksOnEdge : Edge
    {
        public bool IsPrimary { get; set; }
    }

    private sealed class ProjectHasTaskEdge : Edge
    {
    }

    private sealed class ProjectAssignmentDto
    {
        public string ProjectCode { get; set; } = string.Empty;

        public bool IsPrimary { get; set; }
    }
}