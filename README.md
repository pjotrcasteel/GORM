# GORM

> **Model how things connect.**
>
> Strongly typed graph persistence for .NET, built around SQL Server Graph, explicit relationships and inspectable graph behavior.

**Website:** https://pjotrcasteel.github.io/GORM/

GORM treats nodes and edges as first-class persistence concepts. It combines a LINQ-style query API, relationship-aware loading, change tracking and graph mutations with in-memory execution for tests and temporal graph history.

> **Repository status:** GORM source, tests, samples and documentation are maintained together in this public repository. The library targets .NET 10, and GitHub Actions validates the public source. NuGet publication is a separate release decision.

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

## Build from source

The ORM source is under [`src/Gorm`](src/Gorm), with [MSTest tests](tests/Gorm.Tests) and [sample code](samples/Gorm.Demo). The C# API uses the `Gorm` namespace; the planned package ID is `GORM`.

```bash
dotnet restore Gorm.sln --configfile NuGet.Config
dotnet build Gorm.sln -c Release --no-restore
dotnet test Gorm.sln -c Release --no-build
```

The library has no RoutIT.Common dependency and uses public NuGet feeds. The public-source migration and validation status are documented in [SOURCE_MIGRATION.md](SOURCE_MIGRATION.md). The documentation playground remains a browser-side query preview rather than the compiled .NET runtime.

## Verify the NuGet package locally

The package identity is `GORM`, currently versioned at `3.1.0` from the repository-wide `SemanticVersion.props`. To build a package without publishing:

```bash
dotnet pack src/Gorm/Gorm.csproj -c Release -o ./artifacts
```

The CI workflow restores, builds, tests, packs, checks NuGet metadata and symbols, and runs a standalone .NET 10 consumer against the resulting `.nupkg`. SourceLink maps symbols back to this repository. SQL Server integration runs as a separate GitHub Actions quality gate.

Public source is licensed under the [MIT License](LICENSE). The [first NuGet preview release](RELEASING.md) uses `3.1.0-preview.1` and has an approval-gated workflow; inspect NuGet.org to confirm whether it has actually been published. The website's interactive Playground currently uses a browser-side query preview, not the compiled GORM runtime.

## SQL Server 2022 integration tests

A separate [Testcontainers integration suite](tests/Gorm.SqlServer.Tests/README.md) provisions disposable SQL Server 2022, applies the database-first sample schema and verifies live schema mapping, node/edge persistence, outgoing/incoming traversals, transaction rollback and history recording. It requires Docker and does not run during the fast `dotnet test Gorm.sln` workflow.

```bash
dotnet test tests/Gorm.SqlServer.Tests/Gorm.SqlServer.Tests.csproj -c Release
```

## First NuGet preview

The candidate version is `GORM 3.1.0-preview.1`. A separate [preview release workflow](.github/workflows/nuget-preview.yml) verifies a locally produced NuGet package, tests a clean consumer and gates release on real SQL Server 2022 integration tests. After NuGet accepts the package, the [public-feed verification workflow](.github/workflows/verify-public-nuget.yml) checks installability without attempting to publish again. Publishing is opt-in and uses NuGet.org Trusted Publishing with a short-lived GitHub OIDC credential.

See [RELEASING.md](RELEASING.md) for setup, safety gates and the publication procedure. Neither merging a PR nor generating a GitHub artifact publishes the package.

## Creator

GORM is an independent open-source project by **[PjotrCasteel](https://pjotrcasteel.github.io/)**.
