using Gorm.Application.Context;
using Gorm.Application.Diagnostics;
using Gorm.Application.Execution;
using Gorm.Application.Execution.InMemory;
using Gorm.Application.Querying;
using Gorm.Core.Configuration;
using Gorm.Core.Primitives;

var db = new CookbookContext().UseInMemory();
var alice = new DeveloperNode { Id = Guid.NewGuid(), Name = "Alice" };
var bob = new DeveloperNode { Id = Guid.NewGuid(), Name = "Bob" };
var gorm = new ProjectNode { Id = Guid.NewGuid(), Name = "GORM" };
var other = new ProjectNode { Id = Guid.NewGuid(), Name = "Other" };
var task = new TaskNode { Id = Guid.NewGuid(), Title = "Ship a preview" };

db.AddRange(alice, bob, gorm, other, task);
db.Connect<WorksOnEdge, DeveloperNode, ProjectNode>(alice, gorm);
db.Connect<WorksOnEdge, DeveloperNode, ProjectNode>(bob, other);
db.Connect<HasTaskEdge, ProjectNode, TaskNode>(gorm, task);
await db.SaveChangesAsync();

await RunFilteringAndPagingAsync(db);
await RunDirectedTraversalsAsync(db, alice.Id, gorm.Id);
await RunRelationshipLoadingAsync(db, alice.Id);
await RunMultiHopAsync(db, alice.Id);
RunExplain(db, alice.Id);

Console.WriteLine("GORM NuGet cookbook passed all recipes.");

static async Task RunFilteringAndPagingAsync(CookbookContext db)
{
    var developers = await db.Set<DeveloperNode>().Where(x => x.Name != "").OrderBy(x => x.Name).Take(1).ToListAsync();
    Require(developers.Count == 1 && developers[0].Name == "Alice", "LINQ filtering and paging");
    var count = await db.Set<DeveloperNode>().CountAsync();
    Require(count == 2, "CountAsync aggregate");
    Console.WriteLine("PASS LINQ filtering, paging and count");
}

static async Task RunDirectedTraversalsAsync(CookbookContext db, Guid developerId, Guid projectId)
{
    var projects = await db.Set<DeveloperNode>().Where(x => x.Id == developerId)
        .Outgoing<WorksOnEdge, ProjectNode>().ToListAsync();
    Require(projects.Count == 1 && projects[0].Id == projectId, "Outgoing traversal");

    var contributors = await db.Set<ProjectNode>().Where(x => x.Id == projectId)
        .Incoming<WorksOnEdge, DeveloperNode>().ToListAsync();
    Require(contributors.Count == 1 && contributors[0].Id == developerId, "Incoming traversal");
    Console.WriteLine("PASS outgoing and incoming traversals");
}

static async Task RunRelationshipLoadingAsync(CookbookContext db, Guid developerId)
{
    var developer = await db.Set<DeveloperNode>().Where(x => x.Id == developerId)
        .Include(x => x.Projects).SingleAsync();
    Require(developer.Projects.Count == 1 && developer.Projects[0].Name == "GORM", "Include loading");
    Console.WriteLine("PASS Include relationship materialization");
}

static async Task RunMultiHopAsync(CookbookContext db, Guid developerId)
{
    var tasks = await db.Set<DeveloperNode>().Where(x => x.Id == developerId)
        .Outgoing<WorksOnEdge, ProjectNode>().ThenOutgoing<HasTaskEdge, TaskNode>().ToListAsync();
    Require(tasks.Count == 1 && tasks[0].Title == "Ship a preview", "Two-hop traversal");
    Console.WriteLine("PASS two-hop traversal");
}

static void RunExplain(CookbookContext db, Guid developerId)
{
    var sql = db.Set<DeveloperNode>().Where(x => x.Id == developerId)
        .Outgoing<WorksOnEdge, ProjectNode>().Explain().Sql;
    Require(!string.IsNullOrWhiteSpace(sql) && sql.Contains("MATCH", StringComparison.OrdinalIgnoreCase), "SQL translation");
    Console.WriteLine("PASS real GORM Explain() translation (SQL not executed)");
}

static void Require(bool valid, string operation)
{
    if (!valid) throw new InvalidOperationException($"GORM cookbook failed: {operation}");
}

internal sealed class CookbookContext : GraphContext
{
    protected override void OnModelCreating(GraphModelBuilder modelBuilder)
    {
        modelBuilder.Node<DeveloperNode>(node =>
        {
            node.ToTable("Developers");
            node.Property(x => x.Name);
            node.HasOutgoingRelationship<WorksOnEdge, ProjectNode>(nameof(DeveloperNode.Projects));
            node.HasNavigation(x => x.Projects, nameof(DeveloperNode.Projects));
        });
        modelBuilder.Node<ProjectNode>(node =>
        {
            node.ToTable("Projects");
            node.Property(x => x.Name);
            node.HasIncomingRelationship<WorksOnEdge, DeveloperNode>("Contributors");
            node.HasOutgoingRelationship<HasTaskEdge, TaskNode>("Tasks");
        });
        modelBuilder.Node<TaskNode>(node =>
        {
            node.ToTable("Tasks");
            node.Property(x => x.Title);
            node.HasIncomingRelationship<HasTaskEdge, ProjectNode>("Projects");
        });
        modelBuilder.Edge<WorksOnEdge>(edge =>
        {
            edge.ToTable("WorksOn");
            edge.From<DeveloperNode>();
            edge.To<ProjectNode>();
        });
        modelBuilder.Edge<HasTaskEdge>(edge =>
        {
            edge.ToTable("HasTask");
            edge.From<ProjectNode>();
            edge.To<TaskNode>();
        });
    }
}

internal sealed class DeveloperNode : Node
{
    public string Name { get; set; } = string.Empty;
    public List<ProjectNode> Projects { get; set; } = [];
}

internal sealed class ProjectNode : Node
{
    public string Name { get; set; } = string.Empty;
}

internal sealed class TaskNode : Node
{
    public string Title { get; set; } = string.Empty;
}

internal sealed class WorksOnEdge : Edge { }

internal sealed class HasTaskEdge : Edge { }
