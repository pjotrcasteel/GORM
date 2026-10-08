# Gorm

`Gorm` is a graph ORM for SQL Server Graph with a LINQ-style query API, relationship-aware loading, change tracking for nodes and edges, and temporal history support.

It supports three main runtime capabilities:

- **SQL Server Graph** for production usage
- **In-memory execution** for unit tests and Reqnroll scenarios
- **History / temporal graph support** for audit, replay, and point-in-time graph inspection

## What you get

- Strongly typed `Node` and `Edge` models
- Convention-based property mapping for public scalar properties
- `Id` as the default key for nodes and edges, with `HasKey(...)` override support
- Optional explicit property configuration through `Property(...)`
- Model configuration through `GraphContext` and `GraphModelBuilder`
- EF-style configuration classes through `IGraphNodeTypeConfiguration<TNode>` and `IGraphEdgeTypeConfiguration<TEdge>`
- Assembly scanning through `ApplyConfigurationsFromAssembly(...)`
- `Set<TNode>()` and `EdgeSet<TEdge>()`
- Key-based lookup helpers such as `WhereId(...)`, `WhereKey(...)`, `FindAsync(...)`, and `FindByKeyAsync(...)`
- Graph traversal queries such as `Outgoing(...)`, `Incoming(...)`, `ThenOutgoing(...)`, and `ThenIncoming(...)`
- Relationship loading through `Include(...)` and `ThenInclude(...)`
- Change tracking for nodes, edges, connects, and disconnects
- Graph mutation support for intent-based graph changes
- SQL Server execution for real persistence
- In-memory execution for fast tests without SQL Server
- History capture for nodes and edges
- Temporal queries such as `AsOf(...)`, `Current()`, and `Between(...)`
- Temporal in-memory graph traversal for historical graph inspection

---

## Core concepts

### Nodes

A node inherits from `Node`. By convention, `Id` is used as the key.

Public scalar properties with a getter and setter are mapped automatically. Navigation properties are not mapped as scalar columns.

```csharp
using Gorm.Core.Primitives;

public sealed class PersonNode : Node
{
    public string Name { get; set; } = string.Empty;

    public int Age { get; set; }

    public List<ProjectNode> Projects { get; set; } = [];
}
```

In this example, `Id`, `Name`, and `Age` are mapped automatically. `Projects` is treated as a navigation property.

### Edges

An edge inherits from `Edge`. By convention, `Id` is used as the key.

Edges also have `FromId` and `ToId`, which represent the SQL Server Graph endpoints. These endpoint properties are handled by GORM and should not be configured as normal scalar properties.

```csharp
using Gorm.Core.Primitives;

public sealed class WorksOnEdge : Edge
{
    public bool IsPrimary { get; set; }
}
```

In this example, `Id` and `IsPrimary` are mapped automatically. `FromId` and `ToId` are handled by the edge relationship itself.

### Key convention

`Id` is the default key for every node and edge. No explicit key registration is needed for the default case.

```csharp
public sealed class PersonNode : Node
{
    public string Name { get; set; } = string.Empty;
}
```

Use `HasKey(...)` only when another property should be used as the key.

```csharp
modelBuilder.Node<CustomerNode>(node =>
{
    node.HasKey(x => x.CustomerNumber);
});

public sealed class CustomerNode : Node
{
    public string CustomerNumber { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}
```

The configured key property is always included in the mapping, even when it was not explicitly configured with `Property(...)`.

---

## Defining a graph context

Create a context by inheriting from `GraphContext` and configuring nodes and edges in `OnModelCreating`.

Scalar properties are mapped automatically. Use `Property(...)` only when you want to override or enrich the default mapping, for example with `IsRequired()` or `HasMaxLength(...)`.

```csharp
using Gorm.Application.Context;
using Gorm.Core.Configuration;
using Gorm.Core.Metadata;
using Gorm.Core.Primitives;

public sealed class DemoGraphContext : GraphContext
{
    protected override void OnModelCreating(GraphModelBuilder modelBuilder)
    {
        modelBuilder.Node<PersonNode>(node =>
        {
            node.ToTable("Persons");

            node.HasOutgoingRelationship<WorksOnEdge, ProjectNode>(
                relationshipName: nameof(PersonNode.Projects),
                multiplicity: GraphRelationshipMultiplicity.Many);

            node.HasNavigation(x => x.Projects, nameof(PersonNode.Projects));
        });

        modelBuilder.Node<ProjectNode>(node =>
        {
            node.ToTable("Projects");

            node.HasIncomingRelationship<WorksOnEdge, PersonNode>(
                relationshipName: nameof(ProjectNode.People),
                multiplicity: GraphRelationshipMultiplicity.Many);

            node.HasNavigation(x => x.People, nameof(ProjectNode.People));
        });

        modelBuilder.Edge<WorksOnEdge>(edge =>
        {
            edge.ToTable("WorksOn");
            edge.From<PersonNode>();
            edge.To<ProjectNode>();
        });
    }
}

public sealed class PersonNode : Node
{
    public string Name { get; set; } = string.Empty;

    public List<ProjectNode> Projects { get; set; } = [];
}

public sealed class ProjectNode : Node
{
    public string Code { get; set; } = string.Empty;

    public List<PersonNode> People { get; set; } = [];
}

public sealed class WorksOnEdge : Edge
{
    public bool IsPrimary { get; set; }
}
```

### Explicit property configuration

Use `Property(...)` when convention-based mapping is not enough.

```csharp
modelBuilder.Node<PersonNode>(node =>
{
    node.Property(x => x.Name)
        .IsRequired()
        .HasMaxLength(200);
});
```

The explicit configuration overrides the convention-discovered property mapping.


---


## Configuration classes and assembly scanning

For small models, inline configuration in `OnModelCreating(...)` is fine. For larger graph models, prefer configuration classes so each node and edge owns its own mapping.

This keeps the graph context small and makes mappings easier to review, test, and maintain.

### Node configuration class

Create a class that implements `IGraphNodeTypeConfiguration<TNode>`.

```csharp
using Gorm.Core.Configuration;
using Gorm.Core.Metadata;

public sealed class PersonNodeConfiguration : IGraphNodeTypeConfiguration<PersonNode>
{
    public void Configure(NodeTypeBuilder<PersonNode> builder)
    {
        builder.ToTable("Persons");

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasOutgoingRelationship<WorksOnEdge, ProjectNode>(
            relationshipName: nameof(PersonNode.Projects),
            multiplicity: GraphRelationshipMultiplicity.Many);

        builder.HasNavigation(x => x.Projects, nameof(PersonNode.Projects));
    }
}
```

Because scalar properties are mapped by convention, `Property(...)` is only needed when you want to configure metadata such as `IsRequired()` or `HasMaxLength(...)`.

### Edge configuration class

Create a class that implements `IGraphEdgeTypeConfiguration<TEdge>`.

```csharp
using Gorm.Core.Configuration;

public sealed class WorksOnEdgeConfiguration : IGraphEdgeTypeConfiguration<WorksOnEdge>
{
    public void Configure(EdgeTypeBuilder<WorksOnEdge> builder)
    {
        builder.ToTable("WorksOn");
        builder.From<PersonNode>();
        builder.To<ProjectNode>();

        builder.Property(x => x.IsPrimary);
    }
}
```

`From<TNode>()` and `To<TNode>()` are required for edge mappings. `FromId` and `ToId` are still handled by GORM and should not be configured as normal scalar properties.

### Apply configurations explicitly

Use `ApplyConfiguration(...)` when you want full control over which mappings are applied.

```csharp
protected override void OnModelCreating(GraphModelBuilder modelBuilder)
{
    modelBuilder.ApplyConfiguration(new PersonNodeConfiguration());
    modelBuilder.ApplyConfiguration(new ProjectNodeConfiguration());
    modelBuilder.ApplyConfiguration(new WorksOnEdgeConfiguration());
}
```

### Apply all configurations from an assembly

Use `ApplyConfigurationsFromAssembly(...)` when all node and edge configuration classes should be discovered automatically.

```csharp
protected override void OnModelCreating(GraphModelBuilder modelBuilder) =>
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(DemoGraphContext).Assembly);
```

This scans the assembly for concrete classes implementing:

- `IGraphNodeTypeConfiguration<TNode>`
- `IGraphEdgeTypeConfiguration<TEdge>`

Both public and non-public configuration classes can be created as long as they have a parameterless constructor.

### Filter assembly scanning

Use the optional predicate when only part of an assembly should be applied.

```csharp
protected override void OnModelCreating(GraphModelBuilder modelBuilder) =>
    modelBuilder.ApplyConfigurationsFromAssembly(
        typeof(DemoGraphContext).Assembly,
        type => type.Namespace == "Demo.Graph.Configuration");
```

### Duplicate configuration behavior

GORM expects one configuration per node type and one configuration per edge type.

If multiple configuration classes are found for the same node or edge type, `ApplyConfigurationsFromAssembly(...)` throws an `InvalidOperationException`. This prevents accidental double registration and keeps the model deterministic.

---

## SQL Server usage

Use `SqlConnectionFactory` for production or integration scenarios backed by SQL Server Graph.

```csharp
using Gorm.Application.Context;
using Gorm.Infrastructure.Persistence.Connections;
using Gorm.Infrastructure.Providers.SqlServer;

var options = new GormSqlServerOptions
{
    ConnectionString = connectionString
};

var context = new DemoGraphContext(
    new SqlConnectionFactory(options));
```

You can also configure the connection factory later:

```csharp
var context = new DemoGraphContext();
context.UseConnectionFactory(new SqlConnectionFactory(options));
```

---

## Writing data

### Add nodes

```csharp
var person = new PersonNode
{
    Name = "Alice"
};

var project = new ProjectNode
{
    Code = "PRJ-001"
};

context.Add(person);
context.Add(project);

await context.SaveChangesAsync();
```

When a new node or edge is added with `Id == Guid.Empty`, GORM assigns a new `Guid` automatically before it is tracked. This is the default provider-neutral behavior; there is no separate execution path to configure for id generation. An explicitly supplied id is preserved, which is useful in tests and integration flows where stable ids make assertions easier.

### Connect nodes

```csharp
context.Connect<WorksOnEdge, PersonNode, ProjectNode>(
    person,
    project,
    edge => edge.IsPrimary = true);

await context.SaveChangesAsync();
```

### Add an existing edge instance

```csharp
var edge = new WorksOnEdge
{
    IsPrimary = true
};

context.AddEdge<WorksOnEdge, PersonNode, ProjectNode>(person, project, edge);

await context.SaveChangesAsync();
```

### Add multiple nodes and edges in one transaction

Multiple nodes and edges can be persisted in one transaction by staging all changes and calling `SaveChangesAsync(...)` once.

```csharp
var alice = new PersonNode
{
    Name = "Alice"
};

var bob = new PersonNode
{
    Name = "Bob"
};

var project = new ProjectNode
{
    Code = "PRJ-001"
};

context.Add(alice);
context.Add(bob);
context.Add(project);

context.Connect<WorksOnEdge, PersonNode, ProjectNode>(
    alice,
    project,
    edge => edge.IsPrimary = true);

context.Connect<WorksOnEdge, PersonNode, ProjectNode>(
    bob,
    project,
    edge => edge.IsPrimary = false);

await context.SaveChangesAsync();
```

For SQL Server this is persisted inside the existing GORM save transaction. If a node or edge insert fails, the transaction is rolled back.

Relationship mutation APIs use the generic type order `<TEdge, TFrom, TTo>` and endpoint-first argument order. The source and target nodes therefore always come first; an existing edge instance or edge configuration follows them.

### Update or remove individual entities

For an isolated node or edge change, prefer the direct EF-style change-tracking API.

```csharp
person.Name = "Alice Smith";
context.Update(person);

context.Remove(project);

await context.SaveChangesAsync();
```

Use a `GraphMutation` instead when the node change is part of one larger logical graph operation containing multiple nodes and/or edges.

---

## Graph mutations

Use `GraphMutation` when a service receives a full graph change as one logical operation, for example from a Service Specification activation event. For isolated entity changes, use `context.Update(...)`, `context.Remove(...)`, `context.Connect(...)`, or `context.Disconnect(...)` directly.

A graph mutation is an intent-based description of the graph change:

- add, attach, update, remove, or upsert nodes;
- add, remove, or upsert edges;
- validate the complete logical operation before touching the change tracker;
- optionally preview the mutation through `PlanMutation(...)`;
- execute and save through the normal `SaveChangesAsync(...)` transaction boundary.

### New graph mutation

```csharp
var serviceSpecification = new ServiceSpecificationNode
{
    ExternalId = "spec-internet-voice",
    Name = "Internet + Voice"
};

var cfsInternet = new CfsNode
{
    ExternalId = "cfs-internet",
    Name = "CFS Internet"
};

var cfsVoice = new CfsNode
{
    ExternalId = "cfs-voice",
    Name = "CFS Voice"
};

var rfsAccess = new RfsNode
{
    ExternalId = "rfs-access",
    Name = "RFS Access"
};

var rfsRouter = new RfsNode
{
    ExternalId = "rfs-router",
    Name = "RFS Router"
};

var mutation = GraphMutation.Create("service-specification-activation")
    .WithCorrelationId(correlationId)
    .WithSourceEventId(eventId)
    .UpsertNode(serviceSpecification, x => x.Key(serviceSpecification.ExternalId))
    .UpsertNode(cfsInternet, x => x.Key(cfsInternet.ExternalId))
    .UpsertNode(cfsVoice, x => x.Key(cfsVoice.ExternalId))
    .UpsertNode(rfsAccess, x => x.Key(rfsAccess.ExternalId))
    .UpsertNode(rfsRouter, x => x.Key(rfsRouter.ExternalId))
    .UpsertEdge<ServiceSpecificationContainsCfsEdge, ServiceSpecificationNode, CfsNode>(
        serviceSpecification,
        cfsInternet,
        configure: x => x.Key($"{serviceSpecification.ExternalId}:contains:{cfsInternet.ExternalId}"))
    .UpsertEdge<ServiceSpecificationContainsCfsEdge, ServiceSpecificationNode, CfsNode>(
        serviceSpecification,
        cfsVoice,
        configure: x => x.Key($"{serviceSpecification.ExternalId}:contains:{cfsVoice.ExternalId}"))
    .UpsertEdge<CfsDependsOnRfsEdge, CfsNode, RfsNode>(
        cfsInternet,
        rfsAccess,
        configure: x => x.Key($"{cfsInternet.ExternalId}:depends-on:{rfsAccess.ExternalId}"))
    .UpsertEdge<CfsDependsOnRfsEdge, CfsNode, RfsNode>(
        cfsInternet,
        rfsRouter,
        configure: x => x.Key($"{cfsInternet.ExternalId}:depends-on:{rfsRouter.ExternalId}"))
    .UpsertEdge<CfsDependsOnRfsEdge, CfsNode, RfsNode>(
        cfsVoice,
        rfsAccess,
        configure: x => x.Key($"{cfsVoice.ExternalId}:depends-on:{rfsAccess.ExternalId}"));

await context.ApplyMutationAsync(mutation, cancellationToken);
```

### Existing graph with a new node and edge

Use `AttachNode(...)` for existing unchanged nodes and `UpsertNode(...)` for nodes that should be inserted or updated.

```csharp
var newCfs = new CfsNode
{
    ExternalId = "cfs-tv",
    Name = "CFS TV"
};

var mutation = GraphMutation.Create("add-cfs-to-existing-service-specification")
    .WithCorrelationId(correlationId)
    .WithSourceEventId(eventId)
    .AttachNode(existingServiceSpecification, x => x.Key(existingServiceSpecification.ExternalId))
    .UpsertNode(newCfs, x => x.Key(newCfs.ExternalId))
    .UpsertEdge<ServiceSpecificationContainsCfsEdge, ServiceSpecificationNode, CfsNode>(
        existingServiceSpecification,
        newCfs,
        configure: x => x.Key($"{existingServiceSpecification.ExternalId}:contains:{newCfs.ExternalId}"));

await context.ApplyMutationAsync(mutation, cancellationToken);
```

### Existing graph with an updated node and new edge

Use `UpdateNode(...)` when the node already exists and must be changed.

```csharp
existingCfsInternet.Name = "CFS Internet Updated";

var newRfsFirewall = new RfsNode
{
    ExternalId = "rfs-firewall",
    Name = "RFS Firewall"
};

var mutation = GraphMutation.Create("update-cfs-and-add-rfs")
    .WithCorrelationId(correlationId)
    .WithSourceEventId(eventId)
    .UpdateNode(existingCfsInternet, x => x.Key(existingCfsInternet.ExternalId))
    .UpsertNode(newRfsFirewall, x => x.Key(newRfsFirewall.ExternalId))
    .UpsertEdge<CfsDependsOnRfsEdge, CfsNode, RfsNode>(
        existingCfsInternet,
        newRfsFirewall,
        configure: x => x.Key($"{existingCfsInternet.ExternalId}:depends-on:{newRfsFirewall.ExternalId}"));

await context.ApplyMutationAsync(mutation, cancellationToken);
```

### Strict add mutation

Use `AddNode(...)` and `AddEdge(...)` when duplicates should fail instead of becoming an update or no-op.

```csharp
var person = new PersonNode
{
    Name = "Alice"
};

var project = new ProjectNode
{
    Code = "PRJ-001"
};

var mutation = GraphMutation.Create("strict-create-person-project")
    .AddNode(person, x => x.Key("person-alice"))
    .AddNode(project, x => x.Key("project-alpha"))
    .AddEdge<WorksOnEdge, PersonNode, ProjectNode>(
        person,
        project,
        configure: x => x.Key("alice-works-on-alpha"));

await context.ApplyMutationAsync(mutation, cancellationToken);
```

### Remove edge mutation

Use `RemoveEdge(...)` when an existing relationship must be removed.

```csharp
var mutation = GraphMutation.Create("remove-person-project-assignment")
    .AttachNode(existingPerson, x => x.Key("person-alice"))
    .AttachNode(existingProject, x => x.Key("project-alpha"))
    .RemoveEdge<WorksOnEdge, PersonNode, ProjectNode>(
        existingPerson,
        existingProject,
        x => x.Key("alice-works-on-alpha"));

await context.ApplyMutationAsync(mutation, cancellationToken);
```

### Plan only / dry run

Use `PlanMutation(...)` when application/client code explicitly needs to validate, inspect, or preview a mutation without applying it. It is not a required step before `ApplyMutation(...)` or `ApplyMutationAsync(...)`; applying a mutation performs its own planning and validation.

```csharp
var mutation = GraphMutation.Create("preview-only")
    .UpsertNode(person, x => x.Key("person-alice"))
    .UpsertNode(project, x => x.Key("project-alpha"))
    .UpsertEdge<WorksOnEdge, PersonNode, ProjectNode>(
        person,
        project,
        configure: x => x.Key("alice-works-on-alpha"));

var plan = context.PlanMutation(mutation);

Console.WriteLine(plan.ToDebugString());

if (!plan.Validation.IsValid)
{
    Console.WriteLine(plan.Validation.ToMessage());
}
```

### Apply without throwing on validation errors

By default, invalid mutation plans throw before execution. You can disable that if you want to inspect the validation result yourself.

```csharp
var plan = context.ApplyMutation(
    mutation,
    new GraphMutationOptions
    {
        ThrowOnValidationError = false
    });

if (!plan.Validation.IsValid)
{
    logger.LogWarning("Graph mutation was not applied: {Message}", plan.Validation.ToMessage());
}
```

For event-driven activation flows, following at-least-once-delivery, prefer:

```csharp
.UpsertNode(...)
.UpsertEdge(...)
```

Use strict operations only when you explicitly want failures:

```csharp
.AddNode(...)
.AddEdge(...)
.UpdateNode(...)
.RemoveNode(...)
.RemoveEdge(...)
```

### Disconnect nodes

```csharp
context.Disconnect<WorksOnEdge, PersonNode, ProjectNode>(person, project);

await context.SaveChangesAsync();
```

---

## Querying nodes

### Root node queries

```csharp
var people = await context.Set<PersonNode>()
    .Where(x => x.Name.StartsWith("A"))
    .OrderBy(x => x.Name)
    .ToListAsync();
```

### Count / Any / First / Single

```csharp
var count = await context.Set<PersonNode>().CountAsync();
var any = await context.Set<PersonNode>().AnyAsync();
var first = await context.Set<PersonNode>().FirstAsync();
var single = await context.Set<PersonNode>()
    .Where(x => x.Id == person.Id)
    .SingleAsync();
```

### Lookup by default id

Use `WhereId(...)` when the node uses the default `Id` key.

```csharp
var person = await context.Set<PersonNode>()
    .WhereId(personId)
    .SingleAsync();
```

Use `FindAsync(...)` for a nullable single-node lookup by `Id`.

```csharp
var person = await context.Set<PersonNode>()
    .FindAsync(personId, cancellationToken);
```

### Lookup by configured key

Use `WhereKey(...)` and `FindByKeyAsync(...)` when the node may use a custom key configured with `HasKey(...)`.

```csharp
modelBuilder.Node<CustomerNode>(node =>
{
    node.HasKey(x => x.CustomerNumber);
});

var customer = await context.Set<CustomerNode>()
    .FindByKeyAsync("CUST-001", cancellationToken);
```

This keeps lookup code aligned with the model configuration. If the key later changes from `Id` to another property, the lookup can stay key-based instead of property-name based.

---

## Traversing the graph

### Outgoing traversal

```csharp
var projects = await context.Set<PersonNode>()
    .Where(x => x.Id == person.Id)
    .Outgoing<WorksOnEdge, ProjectNode>()
    .ToListAsync();
```

### Incoming traversal

```csharp
var people = await context.Set<ProjectNode>()
    .Where(x => x.Id == project.Id)
    .Incoming<WorksOnEdge, PersonNode>()
    .ToListAsync();
```

### Chained traversal

```csharp
var tasks = await context.Set<PersonNode>()
    .Where(x => x.Id == person.Id)
    .Outgoing<WorksOnEdge, ProjectNode>()
    .ThenOutgoing<ProjectHasTaskEdge, TaskNode>()
    .ToListAsync();
```

Chained traversal is explicit and fixed-depth: every `ThenOutgoing(...)` or `ThenIncoming(...)` represents one known hop. GORM does not implicitly recurse over an unknown number of intermediate nodes. If a domain needs both direct relationships and “all reachable relationships”, keep those semantics explicit; variable-depth recursive traversal is a separate query capability rather than an implicit `Include(...)` behavior.

### Select with edge data

```csharp
var assignments = await context.Set<PersonNode>()
    .Where(x => x.Id == person.Id)
    .Outgoing<WorksOnEdge, ProjectNode>()
    .SelectWithEdge<WorksOnEdge, ProjectNode, ProjectAssignmentDto>(
        (node, edge) => new ProjectAssignmentDto
        {
            ProjectCode = node.Code,
            IsPrimary = edge.IsPrimary
        })
    .ToListAsync();

public sealed class ProjectAssignmentDto
{
    public string ProjectCode { get; set; } = string.Empty;

    public bool IsPrimary { get; set; }
}
```

---

## Relationship loading

### Include

```csharp
var people = await context.Set<PersonNode>()
    .Include(x => x.Projects)
    .ToListAsync();
```

### Filtered include

```csharp
var people = await context.Set<PersonNode>()
    .Include(
        x => x.Projects,
        x => x
            .Where(project => project.Code.StartsWith("PRJ"))
            .OrderBy(project => project.Code)
            .Take(10))
    .ToListAsync();
```

### ThenInclude

```csharp
var people = await context.Set<PersonNode>()
    .Include(x => x.Projects)
    .ThenInclude(x => x.Tasks)
    .ToListAsync();
```

`Include(...)` and `ThenInclude(...)` load only the relationships explicitly described by the query. They do not recursively load all reachable nodes.

### Include with edge filters or edge ordering

```csharp
var people = await context.Set<PersonNode>()
    .IncludeRelationshipWithEdges<PersonNode, ProjectNode>(
        x => x.Projects,
        x => x
            .WhereEdge<WorksOnEdge>(edge => edge.IsPrimary)
            .OrderByEdge<WorksOnEdge, bool>(edge => edge.IsPrimary))
    .ToListAsync();
```

---

## Change tracking helpers

```csharp
var hasChanges = context.HasChanges();
var entry = context.Entry(person);
var debugView = context.ChangeTrackerDebugView;

context.DetectChanges();
context.AcceptAllChanges();
context.ClearTracking();
```

---

## In-memory execution

The in-memory execution engine is intended for:

- unit tests
- Reqnroll scenarios
- fast graph seeding
- test assertions without SQL Server

### Use a new in-memory store

```csharp
using Gorm.Application.Context;

var context = new DemoGraphContext()
    .UseInMemory();
```

### Share one store across multiple contexts

```csharp
using Gorm.Application.Context;
using Gorm.Application.Execution;

var store = new InMemoryGraphStore();

var writeContext = new DemoGraphContext().UseInMemory(store);
var readContext = new DemoGraphContext().UseInMemory(store);
```

### In-memory example

```csharp
var context = new DemoGraphContext().UseInMemory();

var person = new PersonNode
{
    Id = Guid.NewGuid(),
    Name = "Alice"
};

var project = new ProjectNode
{
    Id = Guid.NewGuid(),
    Code = "PRJ-001"
};

context.Add(person);
context.Add(project);
context.Connect<WorksOnEdge, PersonNode, ProjectNode>(
    person,
    project,
    edge => edge.IsPrimary = true);

await context.SaveChangesAsync();

var loadedProjects = await context.Set<PersonNode>()
    .Where(x => x.Id == person.Id)
    .Outgoing<WorksOnEdge, ProjectNode>()
    .ToListAsync();

var loadedPerson = await context.Set<PersonNode>()
    .Include(x => x.Projects)
    .SingleAsync();
```

---
## Graph Intelligence Engine

### What is the Intelligence Engine?

The Intelligence Engine is the graph-analysis layer on top of the normal GORM query API.

Normal GORM queries are intended for cases where the data or relationship path you want to retrieve is already known. For example, loading a node by id, filtering nodes, following a known relationship, or including related entities.

The Intelligence Engine is intended for questions where the answer depends on the structure of a larger part of the graph.

It materializes normal GORM queries into a detached, heterogeneous graph projection. Graph algorithms can then analyse that projection without changing tracked entities or persisted graph data.

The same API works with SQL Server and the in-memory provider.

### When to use the Intelligence Engine

Use normal GORM queries when you want to:

- load one or more known nodes;
- filter, order, or project graph entities;
- follow a known relationship using `Outgoing(...)` or `Incoming(...)`;
- follow a predefined multi-step traversal;
- load related entities using `Include(...)` and `ThenInclude(...)`.

Use the Intelligence Engine when you want to answer questions about the topology or behaviour of a larger graph, for example:

- Which services or resources are indirectly impacted when a component fails?
- What is the shortest or cheapest path between two nodes?
- Which nodes or edges form structural bottlenecks?
- Does the dependency graph contain cycles?
- Which nodes are the most central or influential?
- Which connected components or communities exist in the graph?
- What alternative routes remain when a dependency becomes unavailable?
- What is the critical path through a dependency graph?

A useful rule of thumb is:

**If the relationship path is known in advance, use normal GORM querying. If the answer must be discovered by analysing the graph structure, use the Intelligence Engine.**

The Intelligence Engine does not replace normal GORM querying. Normal GORM queries define which nodes and edges are included in the projection; the Intelligence Engine then analyses that detached snapshot.

### Creating a graph projection

```csharp
using Gorm.Application.Intelligence;
using Gorm.Application.Intelligence.Algorithms;
using Gorm.Application.Intelligence.Caching;
using Gorm.Application.Intelligence.Compute;
using Gorm.Application.Intelligence.Projection;

var graph = await context.ProjectGraphAsync(
    projection => projection
        .Nodes(context.Set<Service>().Where(service => service.IsActive))
        .Nodes(context.Set<Resource>())
        .Edges(context.Set<DependsOn>())
        .Edges(context.Set<ImplementedBy>()),
    cancellationToken);

Projection queries are automatically executed with `AsNoTracking()`. Building the projection validates identifiers and endpoints, and creates compact incoming and outgoing adjacency indexes.

### PageRank and degree centrality

```csharp
var pageRank = graph.PageRank(cancellationToken: cancellationToken);
var centrality = graph.DegreeCentrality(cancellationToken);

var mostImportant = pageRank.Scores.Take(20).ToArray();
```

### Connected components

```csharp
var weakComponents = graph.ConnectedComponents(
    GraphComponentKind.Weak,
    cancellationToken);

var stronglyConnectedComponents = graph.ConnectedComponents(
    GraphComponentKind.Strong,
    cancellationToken);
```

### Weighted shortest path

```csharp
var path = graph.ShortestPath(
    source.Id,
    destination.Id,
    new GraphShortestPathOptions
    {
        Direction = GraphAlgorithmTraversalDirection.Both,
        WeightSelector = edge => ((DependsOn)edge).Cost,
        EdgePredicate = edge => ((DependsOn)edge).IsAvailable
    },
    cancellationToken);
```

The result contains nodes and edges in traversal order, total weight and hop count. A missing path returns `null`.

### Structural bottlenecks, cycles and critical paths

```csharp
var bridgeScores = graph.BetweennessCentrality(
    new GraphBetweennessCentralityOptions
    {
        WeightSelector = edge => ((DependsOn)edge).Cost
    },
    cancellationToken);

var cycles = graph.DetectCycles(
    new GraphCycleDetectionOptions
    {
        MaximumCycles = 500,
        MaximumDepth = 50
    },
    cancellationToken);

var schedule = graph.CriticalPath(
    new GraphCriticalPathOptions
    {
        DurationSelector = node => ((Service)node).ImplementationDays,
        LagSelector = edge => ((DependsOn)edge).LeadTimeDays
    },
    cancellationToken);
```

Betweenness centrality identifies structural bridges. Cycle detection is explicitly bounded and reports a truncated result
when a safety limit stops enumeration. Critical Path Method results include earliest/latest timing, slack, critical nodes,
critical edges and one deterministic primary path.

### Communities, anomalies and explainable link prediction

```csharp
var communities = graph.DetectCommunities(
    new GraphCommunityDetectionOptions
    {
        EdgeMode = GraphCommunityEdgeMode.Directed,
        WeightSelector = edge => ((DependsOn)edge).Importance,
        Resolution = 1,
        RandomSeed = 42
    },
    cancellationToken);

var predictedLinks = graph.PredictLinks(
    new GraphLinkPredictionOptions
    {
        Directed = true,
        CandidateScope = GraphLinkPredictionCandidateScope.WithinCommunity,
        MinimumConfidence = 0.7,
        MaximumResults = 100,
        MaximumCandidatePairs = 250_000
    },
    communities,
    cancellationToken);
```

Community results include hierarchy levels, modularity, conductance, density, boundary edges, bridge nodes and explainable
cross-community anomalies. Each link prediction reports its shared evidence nodes, individual topology metrics, combined
confidence and explanation. Candidate and top-K limits make expensive searches explicit and bounded.

### Optimization and decisioning

```csharp
var optimal = graph.AStarPath(source.Id, destination.Id, new GraphAStarOptions
{
    CostSelector = edge => ((DependsOn)edge).Cost,
    Heuristic = (current, target) => EstimateRemainingCost(current, target)
}, cancellationToken);

var alternatives = graph.KShortestPaths(source.Id, destination.Id, new GraphKShortestPathsOptions
{
    CostSelector = edge => ((DependsOn)edge).Cost,
    MaximumPaths = 5
}, cancellationToken);

var flow = graph.MaximumFlow(source.Id, destination.Id, new GraphMaximumFlowOptions
{
    CapacitySelector = edge => ((DependsOn)edge).Capacity
}, cancellationToken);

var criteria = new GraphParetoRouteOptions();
criteria.Criteria.Add(new GraphRouteCriterion
{
    Name = "cost",
    Selector = edge => ((DependsOn)edge).Cost
});
criteria.Criteria.Add(new GraphRouteCriterion
{
    Name = "risk",
    Selector = edge => ((DependsOn)edge).Risk
});
var tradeoffs = graph.ParetoRoutes(source.Id, destination.Id, criteria, cancellationToken);

var impact = new GraphImpactReroutingOptions
{
    CostSelector = edge => ((DependsOn)edge).Cost
};
impact.FailedEdgeIds.Add(failedDependency.Id);
var reroute = graph.RerouteAroundImpact(source.Id, destination.Id, impact, cancellationToken);
```

A* returns one optimal route; Yen returns bounded loopless alternatives. Maximum flow includes the matching minimum cut and
saturated bottlenecks. Pareto decisioning preserves non-dominated cost/risk trade-offs and explains rejected complete routes.
Impact-aware rerouting compares the baseline with the best surviving alternatives without changing graph entities or data.

### Versioned projections and incremental deltas

```csharp
var key = new GraphProjectionKey("active-services:production");
var snapshot = await context.ProjectGraphSnapshotAsync(
    key,
    sourceVersion,
    projection => projection
        .Nodes(context.Set<Service>().Where(service => service.IsActive))
        .Edges(context.Set<DependsOn>().Where(edge => edge.IsEnabled)),
    cancellationToken);

var delta = new GraphProjectionDeltaBuilder(key, sourceVersion, nextVersion)
    .AddNodes(addedServices)
    .UpdateNodes(changedServices)
    .RemoveNodes(removedServiceIds)
    .AddEdges(addedDependencies)
    .UpdateEdges(changedDependencies)
    .RemoveEdges(removedDependencyIds)
    .Build();

var update = await snapshot.ApplyDeltaOrRebuildAsync(
    delta,
    token => RebuildCurrentSnapshotAsync(key, token),
    cancellationToken);
snapshot = update.Snapshot;
```

The key and exact base version prevent out-of-order or cross-scope deltas. Removing a node automatically removes unchanged
incident edges, and `AffectedNodeIds`/`AffectedEdgeIds` expose the invalidation scope. If incremental application is unsafe,
the explicit rebuild is accepted only with the same key and a sufficiently current version. Neither path writes to GORM.

For reuse across requests, add the bounded version-aware cache:

```csharp
var cache = new GraphProjectionCache(new GraphProjectionCacheOptions
{
    TimeToLive = TimeSpan.FromMinutes(5),
    MaximumEntries = 128
});

var cached = await context.ProjectGraphCachedAsync(
    cache,
    key,
    sourceVersion,
    projection => projection
        .Nodes(context.Set<Service>().Where(service => service.IsActive))
        .Edges(context.Set<DependsOn>().Where(edge => edge.IsEnabled)),
    cancellationToken);

var update = await cache.ApplyDeltaOrRebuildAsync(
    delta,
    token => RebuildCurrentSnapshotAsync(key, token),
    cancellationToken);
```

Concurrent misses for the same key share one load. Every caller supplies a minimum source version. TTL expiry, explicit
invalidation and deterministic LRU eviction are generation-safe: an older in-flight load cannot publish after a newer cache
change. `Statistics` and `GetEntries()` expose cache behaviour and current versions.

### Incremental algorithms and deterministic parallelism

```csharp
var previousDegree = cached.Snapshot.DegreeCentralityVersioned(
    new GraphDegreeCentralityOptions
    {
        DegreeOfParallelism = Environment.ProcessorCount,
        MinimumNodesPerPartition = 1_000
    },
    cancellationToken);

var projectionUpdate = await cache.ApplyDeltaOrRebuildAsync(
    delta,
    token => RebuildCurrentSnapshotAsync(key, token),
    cancellationToken);

var degreeUpdate = projectionUpdate.UpdateDegreeCentrality(
    previousDegree,
    cancellationToken: cancellationToken);
```

Degree centrality reuses only unaffected raw degree values from the exact delta base version. It safely performs a full
recomputation after skipped versions or full projection rebuilds. `PageRank`, `DegreeCentrality` and custom `Compute` support
the same stable contiguous partition planner through `DegreeOfParallelism` and `MinimumNodesPerPartition`; merge and
floating-point reduction order remain deterministic.

### Live projection streams

Applications can adapt SQL Server Change Tracking, CDC, an outbox or an in-process producer to the provider-neutral
`IGraphProjectionChangeFeed`. The built-in bounded channel feed provides backpressure and resumable monotonic cursors:

```csharp
var feed = new GraphChannelProjectionChangeFeed(capacity: 256);
var liveEngine = new GraphLiveProjectionEngine(cache);

await foreach (var change in liveEngine.WatchAsync(
    key,
    feed,
    token => RebuildCurrentSnapshotAsync(key, token),
    after: lastCommittedCursor,
    cancellationToken))
{
    lastCommittedCursor = change.Cursor;
    var graph = change.ProjectionUpdate.Snapshot.Projection;
    var currentScores = graph.PageRank(cancellationToken: cancellationToken);
}
```

Provider cursors establish delivery order; projection versions independently prove whether a delta can be applied. Wrong-key
or regressing changes fail before publication, and an incompatible base can only invoke the explicit rebuild callback. This
stream changes cache state only and never persists graph entities.

### Snapshot metadata, registry and desktop GormStudio hooks

Every versioned projection carries immutable creation/origin metadata and a deterministic topology fingerprint. Dynamic tools
can discover and execute the same built-ins through the registry while ordinary applications keep using pure typed GORM calls:

```csharp
var registry = GraphAlgorithmRegistry.CreateBuiltIn();
var studio = new GraphStudioIntelligenceBridge(registry);
var catalog = studio.GetCatalog();

var execution = studio.Execute(
    snapshot,
    new GraphAlgorithmInvocation(
        "centrality.pagerank",
        new GraphPageRankOptions { MaximumIterations = 50 }),
    cancellationToken);

var scores = execution.GetResult<GraphPageRankResult>();
var analysedVersion = execution.Snapshot.Version;
```

The catalog exposes stable ids, categories, result types, complexity, parameters and editable scalar/enum option fields for a
desktop client. Delegate-based domain selectors remain custom/non-editable. The bridge is in-process, returns data only and
does not introduce a web server or implicit graph mutations.

### Unified live runtime and explicit outputs

Use `GraphIntelligenceRuntime` when one-shot and live registry executions should share a timeout, cancellation, diagnostics and
failure model. Live updates keep the committed change cursor, projection update and exact versioned result together:

```csharp
var runtime = new GraphIntelligenceRuntime(
    registry,
    cache,
    new GraphIntelligenceRuntimeOptions
    {
        ExecutionTimeout = TimeSpan.FromSeconds(30),
        MaximumLiveExecutions = 10_000
    });

await foreach (var update in runtime.WatchAsync(
    key,
    feed,
    token => RebuildCurrentSnapshotAsync(key, token),
    _ => new GraphAlgorithmInvocation("centrality.degree"),
    cancellationToken: cancellationToken))
{
    var degree = update.Execution.GetResult<GraphDegreeCentralityResult>();
}
```

Analysis never invokes output on its own. Create adapters and issue an explicit dispatch only for results that should leave
the runtime:

```csharp
var stream = new GraphChannelIntelligenceOutputAdapter("studio-stream", capacity: 256);
var output = new GraphIntelligenceOutputDispatcher([stream]);

var execution = runtime.Execute(snapshot, invocation, cancellationToken);
var receipts = await output.DispatchAsync(execution, cancellationToken);
```

Applications can implement `IGraphIntelligenceOutputAdapter` or wrap an explicitly selected persistence delegate. GORM itself
does not create mutations or call `SaveChangesAsync` for Intelligence results.

### Intelligence diagnostics and performance baselines

All built-in algorithms emit activities and metrics through the existing `Gorm` source/meter. PageRank, degree
centrality and compute can report deterministic progress through `IProgress<GraphAlgorithmProgress>`. Large temporary numeric
and score buffers are pooled while immutable result data remains caller-owned.

`GraphIntelligenceBenchmarkRunner` exposes explicit `Small`, `Medium` and `Large` profiles without adding a benchmark package.
Treat its results as regression evidence rather than a production SLO; incremental reuse is not automatically faster on small graphs.

### Custom vertex-centric algorithms

Implement `IGraphComputeProgram<TState, TMessage>` to execute a domain-specific algorithm in synchronized supersteps. Every node owns state, consumes messages, sends messages through incoming or outgoing edges and votes to halt when it becomes inactive.

```csharp
var impact = graph.Compute(
    new ServiceImpactProgram(failedService.Id),
    new GraphComputeOptions
    {
        MaximumSupersteps = 100,
        MaximumMessagesPerSuperstep = 1_000_000,
        DegreeOfParallelism = Environment.ProcessorCount
    },
    messageReducer: (left, right) =>
        new ImpactMessage(Math.Max(left.Impact, right.Impact)),
    cancellationToken);

var customerImpact = impact.GetState(customerService.Id);
```

The compute engine stops when every node has voted to halt and no messages remain. Safety limits prevent accidental
non-terminating message explosions. Parallel execution is opt-in; message reduction remains deterministic, and the supplied
compute-program instance must be thread-safe when `DegreeOfParallelism` is greater than one.

The complete Intelligence Engine API, usage guidance, operational behaviour and examples are documented in this README.

## Temporal Digital Twin

### What is the Temporal Digital Twin?

The Temporal Digital Twin builds reproducible graph worlds on top of ordinary GORM queries, GORM history and the Intelligence Engine. It does not introduce another query language and never writes snapshots, scenarios or analysis results to the database implicitly.

Use it when you need to:

- reconstruct what the graph looked like at a business or recorded-time coordinate;
- compare graph versions or produce a deterministic change timeline;
- branch an isolated what-if scenario without changing production state;
- replay scenario changes and verify them with fingerprints;
- simulate events, failures, uncertainty, sensitivity or policy choices;
- compare expected and observed graph worlds for operational drift;
- produce warnings, threshold predictions and verifiable evidence reports.

Use ordinary GORM queries when you only need current data or a known traversal path. Use the Temporal layer when time, alternatives, replay or expected-versus-observed state is part of the question.

### Example: evaluate a plan before execution

A planning service can use DT after it has loaded and captured the relevant current graph. Each candidate plan becomes an isolated scenario containing
only proposed changes. The service compares the result with the unchanged baseline and decides whether to execute the plan through the normal GORM
write path. Evaluating the plan itself never changes the live graph.

The following example deliberately uses only generic GORM and Temporal types:

```csharp
using Gorm.Application.Temporal.Scenarios;
using Gorm.Application.Temporal.Scenarios.Analysis;
using Gorm.Application.Temporal.Snapshots;

static GraphScenarioComparisonResult EvaluatePlan(
    GraphWorldSnapshot currentWorld,
    GraphScenarioId planId,
    IReadOnlyList<GraphScenarioMutation> proposedChanges,
    CancellationToken cancellationToken)
{
    var scenario = GraphScenario.Fork(currentWorld, planId);

    foreach (var proposedChange in proposedChanges)
    {
        scenario = scenario.Apply(proposedChange, cancellationToken).Scenario;
    }

    return GraphScenarioComparer.Compare(scenario, cancellationToken: cancellationToken);
}

var evaluatedPlan = EvaluatePlan(
    currentWorld,
    new GraphScenarioId("candidate-plan-a"),
    [
        GraphScenarioMutation.RemoveNode(
            candidateNodeId,
            effectiveAt,
            timeProvider.GetUtcNow(),
            "Evaluate the plan without the selected node")
    ],
    cancellationToken);

var changedEntities = evaluatedPlan.TopologyDifference.Changes;
var metricDeltas = evaluatedPlan.Metrics;
var explanation = evaluatedPlan.Explanation;
```

Use this pattern when a service must answer “what happens if we execute this plan?” before sending commands or persisting mutations. Multiple candidate
plans can be evaluated from the same `currentWorld`; because every scenario preserves that baseline, their evidence remains directly comparable.

### Capture an immutable world

Create a normal versioned Intelligence projection and capture it as a detached `GraphWorldSnapshot`:

```csharp
var projection = await db.ProjectGraphSnapshotAsync(
    new GraphProjectionKey("fulfilment/eu"),
    sourceVersion,
    graph =>
    {
        graph.Nodes(db.Services.Where(service => service.IsActive));
        graph.Edges(db.Dependencies.Where(edge => edge.IsEnabled));
    },
    cancellationToken);

var world = GraphWorldSnapshot.Capture(
    projection,
    validAt: new DateTimeOffset(2026, 8, 20, 8, 0, 0, TimeSpan.Zero),
    cancellationToken: cancellationToken);

var graphAtTime = world.CreateProjection(cancellationToken);
var versionedGraphAtTime = world.CreateIntelligenceSnapshot(cancellationToken);
```

The snapshot stores detached node and edge state. Every materialization returns fresh entities, so changing an original query result or a previously materialized copy cannot alter the captured world.

Each snapshot contains:

- a stable `GraphProjectionKey` and monotonic source version;
- `ValidAt`, representing the business instant;
- `RecordedAt`, representing when that knowledge became available;
- a deterministic content fingerprint;
- a deterministic snapshot identifier.

### History, differences and timelines

`GraphWorldHistoryProjector` resolves ordinary node and edge history envelopes at explicit valid and recorded times. This allows a later correction to change what was known about an earlier business state without changing that earlier valid time.

```csharp
var history = db.History<ServiceNode>()
    .Concat(db.EdgeHistory<DependencyEdge>());

var result = GraphWorldHistoryProjector.Project(
    new GraphProjectionKey("fulfilment/eu"),
    sourceVersion,
    validAt,
    recordedAt,
    history,
    cancellationToken: cancellationToken);

var difference = GraphWorldSnapshotDiffer.Compare(
    previousWorld,
    result.Snapshot,
    cancellationToken: cancellationToken);

var timeline = GraphWorldTimeline.Create(
    [previousWorld, result.Snapshot],
    cancellationToken: cancellationToken);
```

Differences classify added, removed and modified nodes and edges. They retain before/after fingerprints and distinguish topology changes from property-only changes.

### Isolated scenarios and deterministic replay

`GraphScenario.Fork` creates a what-if branch while preserving the immutable baseline. Every mutation creates a new scenario revision and an exact world difference.

```csharp
var scenario = GraphScenario.Fork(
    world,
    new GraphScenarioId("primary-service-failure"));

var applied = scenario.Apply(
    GraphScenarioMutation.RemoveNode(
        failedServiceId,
        validAt,
        recordedAt,
        "Simulate loss of the primary service"),
    cancellationToken);

scenario = applied.Scenario;

var stream = GraphScenarioEventStream.Create(
    scenario.Id,
    scenario.Mutations,
    cancellationToken: cancellationToken);

var replay = stream.Replay(world, cancellationToken: cancellationToken);
```

The event stream is a contiguous SHA-256 hash chain. Replay rejects gaps, tampering, backwards recorded time and configured safety-limit violations.

### Simulation and risk analysis

DT supports deterministic, bounded analysis through:

- `GraphDiscreteEventSimulator` for clock-, priority- and identifier-ordered events;
- `GraphCascadingFailureSimulator` for demand, capacity and maximum-flow impact;
- `GraphMonteCarloSimulator` for seeded, reproducible uncertainty experiments;
- `GraphSensitivityAnalyzer` for one-at-a-time parameter sensitivity;
- `GraphPolicyComparator` for explainable weighted policy ranking;
- `GraphScenarioComparer` for topology, components, critical paths, communities and domain metrics.

```csharp
var uncertainty = GraphMonteCarloSimulator.Run(
    world,
    (snapshot, random) => CalculateUnservedDemand(
        snapshot,
        primaryFails: random.NextBoolean(0.02)),
    new GraphMonteCarloOptions
    {
        Runs = 25_000,
        Seed = 20260820,
        ConfidenceLevel = 0.95
    },
    cancellationToken);

var p95 = uncertainty.Percentile(0.95);
var confidenceInterval = uncertainty.ConfidenceInterval;
```

Equal evidence, options and seeds produce deterministic results. Simulations operate only on detached worlds and never mutate provider state.

### Live drift, warnings and predictions

Compare an expected world with an independently observed world:

```csharp
var drift = GraphTwinDriftDetector.Compare(
    expectedWorld,
    observedWorld,
    new GraphTwinDriftOptions
    {
        HighChangeRatio = 0.10,
        CriticalChangeRatio = 0.30,
        MaximumChanges = 100_000
    },
    cancellationToken);

var warnings = GraphTwinWarningEngine.Evaluate(
    drift,
    [
        new GraphTwinWarningRule
        {
            Id = "drift-ratio-critical",
            Severity = GraphTwinWarningSeverity.Critical,
            MetricSelector = report => report.EntityChangeRatio,
            Comparison = GraphThresholdComparison.GreaterThanOrEqual,
            Threshold = 0.30
        }
    ],
    cancellationToken: cancellationToken);
```

`GraphTwinSynchronizer.WatchAsync` processes provider-neutral observation streams and enforces increasing sequences, non-decreasing observation times, explicit bounds and cancellation. `GraphThresholdPredictor` provides transparent least-squares trend evidence, including slope, R-squared, predicted crossing time and horizon status.

### Fingerprinted reports and GormStudio

`GraphTwinReportBuilder` combines previously produced drift, warnings, predictions and scenario evidence into canonical report schema `gorm.temporal.report/1`:

```csharp
var report = GraphTwinReportBuilder.Create(
    new GraphTwinReportBuilder.CreateParameters
    {
        ReportId = "operations-2026-08-20T08:00Z",
        CreatedAt = timeProvider.GetUtcNow(),
        Synchronization = synchronization,
        Warnings = warnings,
        Predictions = predictions,
        Scenarios = scenarioEvidence,
        CancellationToken = cancellationToken
    });

var json = report.ToJson(indented: true);
var verified = GraphTwinReportSerializer.DeserializeAndVerify(json);
```

The SHA-256 report fingerprint protects canonical content against accidental or intentional modification. `GraphTwinStudioBridge` maps the same evidence to desktop DTOs without adding a web host, browser or UI-framework dependency.

### Production and migration rules

The stable Temporal product contract is exposed by `GraphTemporalProductContract` with API version `3.0`. Its operational rules are:

1. Materialize expected and observed worlds through ordinary GORM queries or history.
2. Keep provider cursors and observation creation in application code.
3. Configure maximum changes, observations, rules, samples, scenarios and report evidence for the service SLO.
4. Pass request or host cancellation tokens to every potentially expensive operation.
5. Treat safety-limit exceptions as incomplete evidence rather than an all-clear.
6. Persist or dispatch results only through explicit application code.
7. Retain snapshot identifiers, rule definitions, scenario streams, random seeds and report fingerprints for incident replay.

Adoption does not require a database migration, schema change, provider change, DI registration or additional package. Existing GORM queries remain unchanged; applications opt into DT only by creating temporal worlds and invoking the Temporal APIs.

The built-in `GraphTwinBenchmarkRunner` supplies bounded development regression evidence for drift and canonical report creation. Treat benchmark output as a regression signal, not as a production SLO.

## Reqnroll usage

A common pattern is to register a shared `InMemoryGraphStore` per scenario and create a fresh context for each scope.

```csharp
using Gorm.Application.Context;
using Gorm.Application.Execution;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;

[ScenarioDependencies]
public static class Dependencies
{
    public static IServiceCollection CreateServices()
    {
        var services = new ServiceCollection();

        services.AddScoped<InMemoryGraphStore>();

        services.AddScoped<DemoGraphContext>(
            provider => new DemoGraphContext()
                .UseInMemory(provider.GetRequiredService<InMemoryGraphStore>()));

        return services;
    }
}
```

This gives you:

- isolated scenario state
- no SQL dependency
- fast setup and teardown
- realistic graph traversal and include behavior in tests

---

## History and temporal graph support

GORM supports history capture for nodes and edges and point-in-time inspection of the graph.

This is useful for:

- audit and traceability
- replay scenarios
- point-in-time validation
- historical dependency analysis
- historical graph decomposition

### History abstractions

GORM captures history as `GraphHistoryEnvelope` records containing:

- entity type
- entity id
- operation kind
- captured timestamp
- validity window
- snapshot payload
- edge endpoint ids for edge history

### Operation kinds

History records can represent:

- `Created`
- `Updated`
- `Deleted`
- `Connected`
- `Disconnected`

---

## In-memory history

In-memory history is intended for:

- deterministic unit tests
- Reqnroll scenarios
- temporal graph experimentation
- validating graph evolution over time

### Use in-memory graph + history

```csharp
using Gorm.Application.Context;
using Gorm.Application.Execution;
using Gorm.Application.History;

var store = new InMemoryGraphStore();
var historyStore = new InMemoryGraphHistoryStore();

var context = new DemoGraphContext()
    .UseInMemory(store, historyStore);
```

### Deterministic time in tests

`UseHistoryClock(...)` configures the UTC time source used when GORM captures history records during `SaveChangesAsync(...)`. This is especially useful in tests, where a fixed clock makes temporal history deterministic and easy to assert.

```csharp
using Gorm.Application.History;

var clock = new FixedGraphHistoryClock(new DateTime(2026, 4, 19, 12, 0, 0, DateTimeKind.Utc));
context.UseHistoryClock(clock);
```

### Basic history example

```csharp
var store = new InMemoryGraphStore();
var historyStore = new InMemoryGraphHistoryStore();
var clock = new FixedGraphHistoryClock(new DateTime(2026, 4, 19, 12, 0, 0, DateTimeKind.Utc));

var context = new DemoGraphContext()
    .UseInMemory(store, historyStore);

context.UseHistoryClock(clock);

var person = new PersonNode
{
    Id = Guid.NewGuid(),
    Name = "Alice"
};

context.Add(person);
await context.SaveChangesAsync();

clock.UtcNow = clock.UtcNow.AddMinutes(1);

person.Name = "Alice Updated";
context.Update(person);
await context.SaveChangesAsync();

var history = context.History<PersonNode>()
    .OrderByCapturedAt()
    .ToList();
```

---

## SQL Server history capture

GORM can also persist history to SQL Server generic history tables.

### Configure SQL history

```csharp
using Gorm.Application.Context;
using Gorm.Infrastructure.Persistence.Connections;

var connectionFactory = new SqlConnectionFactory(new GormSqlServerOptions
{
    ConnectionString = connectionString
});

var context = new DemoGraphContext();
context.UseConnectionFactory(connectionFactory);
context.UseSqlServerHistory(connectionFactory);
```

### History tables

The SQL Server history recorder writes to:

```text
dbo.GormNodeHistory
dbo.GormEdgeHistory
```

These tables are not created automatically by GORM. In a database-first setup they should be provisioned through your normal governed database deployment process outside of GORM.

These are intended as generic history tables for snapshot-based history persistence.

---

## Querying history

### Node history

```csharp
var nodeHistory = context.History<PersonNode>()
    .OrderByCapturedAt()
    .ToList();
```

### Edge history

```csharp
var edgeHistory = context.EdgeHistory<WorksOnEdge>()
    .OrderByCapturedAt()
    .ToList();
```

### Typed snapshots

```csharp
var personSnapshots = context.History<PersonNode>()
    .SelectNodeSnapshot<PersonNode>()
    .ToList();

var edgeSnapshots = context.EdgeHistory<WorksOnEdge>()
    .SelectEdgeSnapshot<WorksOnEdge>()
    .ToList();
```

### Access snapshot from one envelope

```csharp
var envelope = context.History<PersonNode>()
    .OrderByCapturedAt()
    .First();

var snapshot = envelope.GetSnapshot<PersonNode>();
```

---

## Temporal queries

### As of a moment in time

```csharp
var snapshot = context.History<PersonNode>()
    .AsOf(instantUtc)
    .SelectNodeSnapshot<PersonNode>()
    .Single(x => x.Id == personId);
```

### Current version

```csharp
var current = context.History<PersonNode>()
    .Current()
    .SelectNodeSnapshot<PersonNode>()
    .ToList();
```

### Range query

```csharp
var changes = context.History<PersonNode>()
    .Between(fromUtc, toUtc)
    .OrderByCapturedAt()
    .ToList();
```

### Edge history at a point in time

```csharp
var activeAssignments = context.EdgeHistory<WorksOnEdge>()
    .AsOf(instantUtc)
    .SelectEdgeSnapshot<WorksOnEdge>()
    .ToList();
```

---

## Temporal graph traversal (in-memory)

In-memory history supports temporal traversal of the historical graph.

### Temporal outgoing traversal

```csharp
var projects = context.History<PersonNode>()
    .AsOf(instantUtc)
    .Where(x => x.EntityId == personId)
    .TemporalOutgoing<WorksOnEdge, ProjectNode>(context)
    .ToList();
```

### Temporal incoming traversal

```csharp
var people = context.History<ProjectNode>()
    .AsOf(instantUtc)
    .Where(x => x.EntityId == projectId)
    .TemporalIncoming<WorksOnEdge, PersonNode>(context)
    .ToList();
```

### Temporal projection with edge snapshot

```csharp
var assignments = context.History<PersonNode>()
    .AsOf(instantUtc)
    .Where(x => x.EntityId == personId)
    .TemporalSelectWithEdge<WorksOnEdge, ProjectNode, ProjectAssignmentDto>(
        context,
        (node, edge) => new ProjectAssignmentDto
        {
            ProjectCode = node.Code,
            IsPrimary = edge.IsPrimary
        })
    .ToList();
```

### Temporal incoming projection with edge snapshot

```csharp
var assignments = context.History<ProjectNode>()
    .AsOf(instantUtc)
    .Where(x => x.EntityId == projectId)
    .TemporalSelectIncomingWithEdge<WorksOnEdge, PersonNode, PersonAssignmentDto>(
        context,
        (node, edge) => new PersonAssignmentDto
        {
            PersonName = node.Name,
            IsPrimary = edge.IsPrimary
        })
    .ToList();

public sealed class PersonAssignmentDto
{
    public string PersonName { get; set; } = string.Empty;

    public bool IsPrimary { get; set; }
}
```

---

## Current in-memory scope

The in-memory execution engine supports:

- `SaveChangesAsync()`
- root node queries
- LINQ composition such as `Where`, `OrderBy`, `Skip`, `Take`, `Select`, and `Distinct`
- `CountAsync`, `LongCountAsync`, `AnyAsync`
- `FirstAsync`, `FirstOrDefaultAsync`, `SingleAsync`, `SingleOrDefaultAsync`
- `Outgoing(...)`, `Incoming(...)`, `ThenOutgoing(...)`, `ThenIncoming(...)`
- `Include(...)` and `ThenInclude(...)`
- `SelectWithEdge(...)` directly after traversal
- graph mutations
- graph mutation planning and validation
- history capture
- temporal queries
- temporal in-memory traversal
- temporal in-memory projection with historical edge metadata

The in-memory execution engine does **not** currently target full SQL parity. Advanced scenarios such as full edge-root querying, SQL temporal traversal, provider-translated historical traversal queries, and database-backed mutation upsert resolution may still require SQL execution or future enhancements.

---

## When to use which mode

Use **SQL Server Graph** when you need:

- real persistence
- SQL-backed integration tests
- schema deployment
- production execution

Use **in-memory execution** when you need:

- unit tests
- Reqnroll scenarios
- fast local verification
- deterministic graph test setup

Use **history / temporal support** when you need:

- audit and traceability
- point-in-time graph inspection
- replay or reconstruction
- historical validation
- historical traversal of relationships

Use **graph mutations** when you need:

- one logical graph change containing multiple nodes and edges
- service activation or decomposition event handling
- plan/validate/preview before saving
- one `SaveChangesAsync(...)` transaction boundary for the complete graph change

---

## Example SQL Server Graph smoke test

```csharp
[TestMethod]
public async Task Person_can_load_projects_in_memory()
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
        Code = "PRJ-001"
    };

    context.Add(person);
    context.Add(project);
    context.Connect<WorksOnEdge, PersonNode, ProjectNode>(person, project);

    await context.SaveChangesAsync();

    var loaded = await context.Set<PersonNode>()
        .Where(x => x.Id == person.Id)
        .Include(x => x.Projects)
        .SingleAsync();

    Assert.AreEqual(1, loaded.Projects.Count);
    Assert.AreEqual("PRJ-001", loaded.Projects[0].Code);
}
```

### Example graph mutation smoke test

```csharp
[TestMethod]
public async Task GraphMutation_can_persist_multiple_nodes_and_edges_in_memory()
{
    var context = new DemoGraphContext().UseInMemory();

    var alice = new PersonNode
    {
        Name = "Alice"
    };

    var alpha = new ProjectNode
    {
        Code = "PRJ-ALPHA"
    };

    var beta = new ProjectNode
    {
        Code = "PRJ-BETA"
    };

    var mutation = GraphMutation.Create("person-project-seed")
        .WithCorrelationId("correlation-1")
        .WithSourceEventId("event-1")
        .UpsertNode(alice, x => x.Key("person-alice"))
        .UpsertNode(alpha, x => x.Key("project-alpha"))
        .UpsertNode(beta, x => x.Key("project-beta"))
        .UpsertEdge<WorksOnEdge, PersonNode, ProjectNode>(
            alice,
            alpha,
            edge => edge.IsPrimary = true,
            x => x.Key("alice-alpha"))
        .UpsertEdge<WorksOnEdge, PersonNode, ProjectNode>(
            alice,
            beta,
            edge => edge.IsPrimary = false,
            x => x.Key("alice-beta"));

    await context.ApplyMutationAsync(mutation);

    var projects = await context.Set<PersonNode>()
        .Where(x => x.Id == alice.Id)
        .Outgoing<WorksOnEdge, ProjectNode>()
        .ToListAsync();

    Assert.AreEqual(2, projects.Count);
}
```

### Example temporal smoke test

```csharp
[TestMethod]
public async Task Person_can_load_projects_at_a_point_in_time()
{
    var store = new InMemoryGraphStore();
    var historyStore = new InMemoryGraphHistoryStore();
    var clock = new FixedGraphHistoryClock(new DateTime(2026, 4, 19, 12, 0, 0, DateTimeKind.Utc));

    var context = new DemoGraphContext()
        .UseInMemory(store, historyStore);

    context.UseHistoryClock(clock);

    var person = new PersonNode
    {
        Id = Guid.NewGuid(),
        Name = "Alice"
    };

    var project = new ProjectNode
    {
        Id = Guid.NewGuid(),
        Code = "PRJ-001"
    };

    context.Add(person);
    context.Add(project);
    await context.SaveChangesAsync();

    clock.UtcNow = clock.UtcNow.AddMinutes(1);

    context.Connect<WorksOnEdge, PersonNode, ProjectNode>(person, project);
    await context.SaveChangesAsync();

    var afterConnect = clock.UtcNow;

    var projects = context.History<PersonNode>()
        .AsOf(afterConnect)
        .Where(x => x.EntityId == person.Id)
        .TemporalOutgoing<WorksOnEdge, ProjectNode>(context)
        .ToList();

    Assert.AreEqual(1, projects.Count);
    Assert.AreEqual("PRJ-001", projects[0].Code);
}
```

---

## Summary

`Gorm` lets you work with graph-shaped domain models through a strongly typed API.

- Use SQL Server Graph for production
- Use the in-memory execution engine for tests
- Use graph mutations for full graph changes that should be planned, validated, and saved as one logical operation
- Use history and temporal support for audit, replay, and point-in-time graph inspection
- Keep the same `GraphContext`, graph model, and core concepts across current-state and historical graph usage
- Use EF-style configuration classes for nodes and edges with assembly scanning when `OnModelCreating(...)` becomes too large
