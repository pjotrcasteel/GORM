# GORM

> **Model how things connect.**
>
> Strongly typed graph persistence for .NET, built around SQL Server Graph, explicit relationships and inspectable graph behavior.

**Website:** https://pjotrcasteel.github.io/GORM/

GORM treats nodes and edges as first-class persistence concepts. It combines a LINQ-style query API, relationship-aware loading, change tracking and graph mutations with in-memory execution for tests and temporal graph history.

> **Repository status:** the public product repository and documentation surface are established first. The existing implementation source will be migrated into this repository after its organization-specific identity and an internal namespace collision with the separate Forge product family have been cleaned up and validated. No NuGet publication is implied by this repository shell.

## When GORM is useful

GORM is a strong fit when relationships are part of the domain rather than incidental joins:

- graph-shaped domain models on SQL Server Graph;
- typed outgoing/incoming/chained traversal;
- relationship loading through `Include(...)` / `ThenInclude(...)`;
- tracked node, edge, connect and disconnect changes;
- graph mutations with explicit validation/apply behavior;
- in-memory graph execution for fast tests and Reqnroll scenarios;
- node/edge history and point-in-time graph inspection.

The current implementation also contains graph intelligence, digital-twin simulation, explainable GraphRAG, Helix query analysis/optimization and a ChangeSet & transaction compiler.

## Core model

```csharp
public sealed class PersonNode : Node
{
    public string Name { get; set; } = string.Empty;
    public List<ProjectNode> Projects { get; set; } = [];
}

public sealed class WorksOnEdge : Edge
{
    public bool IsPrimary { get; set; }
}
```

`Id` is the default key for nodes and edges. Public scalar properties are conventionally mapped; navigation properties remain relationships rather than scalar columns.

## Traverse relationships directly

```csharp
var projects = await context.Set<PersonNode>()
    .Where(x => x.Id == personId)
    .Outgoing<WorksOnEdge, ProjectNode>()
    .ToListAsync();
```

Relationship-aware loading is available when the object graph is the more useful shape:

```csharp
var person = await context.Set<PersonNode>()
    .Include(x => x.Projects)
    .SingleAsync();
```

## Production and test execution

The same configured graph model can target SQL Server Graph for production usage or an in-memory store for deterministic tests and BDD scenarios.

That does not mean the providers are identical implementations. Provider-specific integration tests remain part of a responsible production test strategy.

## Temporal graph

GORM can capture node and edge history and query connected state through operations such as `AsOf(...)`, `Current()` and range queries. This supports audit, replay and historical dependency inspection.

## Beyond persistence

Advanced capabilities consume ordinary GORM graph entities and projections rather than creating a second domain model:

- **Graph Intelligence** — centrality, routing, components, communities, impact and versioned projections;
- **Temporal Digital Twin** — isolated scenarios and simulation over historical/current graph state;
- **Explainable GraphRAG** — path-aware evidence and citations with explicit authorization boundaries;
- **Helix** — graph query analysis, SQL inspection and semantics-preserving optimization;
- **ChangeSet & transaction compiler** — preview/order graph writes, retain rollback guarantees and explain concurrency conflicts.

## Boundary

GORM owns graph mapping, query translation, change tracking, history and graph-persistence mechanics.

It does not own application business policy. Analysis and plan/preview APIs do not persist changes unless the application explicitly invokes a save or apply operation.

## Source migration

The implementation being prepared for this repository currently uses organization-specific naming. Before source is added here we will:

1. move the source into the public repository without changing behavior;
2. replace organization-specific project/package/namespace identity with the final GORM identity;
3. rename the internal ChangeSet area whose current namespace collides with the separate Forge product family;
4. run the complete .NET 10 build/test/performance suite;
5. review licensing and package metadata;
6. only then decide and publish the first public NuGet package.

See [SOURCE_MIGRATION.md](SOURCE_MIGRATION.md).

## Creator

GORM is an independent open-source project by **[PjotrCasteel](https://pjotrcasteel.github.io/)**.
