# GORM

> **Model how things connect.**
>
> Strongly typed graph persistence for .NET, built around SQL Server Graph, explicit relationships and inspectable graph behavior.

**Website:** https://pjotrcasteel.github.io/GORM/

GORM treats nodes and edges as first-class persistence concepts. It combines a LINQ-style query API, relationship-aware loading, change tracking and graph mutations with in-memory execution for tests and temporal graph history.

> **Repository status:** GORM is an MIT-licensed .NET 10 project. The first public preview, `GORM 3.1.0-preview.1`, is the latest **published** package and has passed independent public-feed verification. The source tree is preparing `3.2.0-preview.1`; neither that candidate nor stable `3.2.0` is published yet.

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

The ORM source is under [`src/Gorm`](src/Gorm), with [MSTest tests](tests/Gorm.Tests) and [sample code](samples/Gorm.Demo). The C# API uses the `Gorm` namespace and the public package ID is `GORM`.

```bash
dotnet restore Gorm.sln --configfile NuGet.Config
dotnet build Gorm.sln -c Release --no-restore
dotnet test Gorm.sln -c Release --no-build
```

The library has no RoutIT.Common dependency and uses public NuGet feeds. The public-source migration and validation status are documented in [SOURCE_MIGRATION.md](SOURCE_MIGRATION.md). The documentation Playground offers opt-in real GORM `Explain()` via client-side .NET WebAssembly for four verified presets; user edits and unsupported shapes remain visibly illustrative previews.

## Verify the NuGet package locally

The package identity is `GORM`, now versioned at the **unpublished `3.2.0` source baseline** from the repository-wide `SemanticVersion.props`. To build a package without publishing:

```bash
dotnet pack src/Gorm/Gorm.csproj -c Release -o ./artifacts
```

The CI workflow restores, builds, tests, packs, checks NuGet metadata and symbols, and runs a standalone .NET 10 consumer against the resulting `.nupkg`. SourceLink maps symbols back to this repository. SQL Server integration runs as a separate GitHub Actions quality gate.

Public source is licensed under the [MIT License](LICENSE). The verified first public preview is [GORM 3.1.0-preview.1 on NuGet.org](https://www.nuget.org/packages/GORM/3.1.0-preview.1). The website's Playground runs actual GORM `Explain()` for four opt-in .NET WebAssembly presets and labels all other JavaScript translations as illustrative.

## SQL Server 2022 integration tests

A separate [Testcontainers integration suite](tests/Gorm.SqlServer.Tests/README.md) provisions disposable SQL Server 2022, applies the database-first sample schema and verifies live schema mapping, node/edge persistence, outgoing/incoming traversals, transaction rollback and history recording. It requires Docker and does not run during the fast `dotnet test Gorm.sln` workflow.

```bash
dotnet test tests/Gorm.SqlServer.Tests/Gorm.SqlServer.Tests.csproj -c Release
```

## Your first GORM graph (no SQL Server required)

The fastest verified way to try real GORM is the [NuGet-only getting-started sample](samples/Gorm.GettingStarted/README.md). It creates two nodes and an edge, saves them in memory, follows a typed relationship and prints SQL from GORM's actual `Explain()` implementation. It is run automatically by GitHub Actions for every pull request.

```bash
dotnet run --project samples/Gorm.GettingStarted/Gorm.GettingStarted.csproj -c Release
```

The sample runs the real GORM package without a hosted service, paid infrastructure, SQL Server or a project reference. For database-backed persistence, continue to the [SQL Server demo](samples/Gorm.Demo/README.md).

## Verified cookbook and compatibility

The [public-NuGet cookbook](samples/Gorm.Cookbook/README.md) is an executable collection of real GORM examples: typed outgoing/incoming traversals, multi-hop paths, `Include`, LINQ paging and the compiled `Explain()` SQL translator.

```bash
dotnet run --project samples/Gorm.Cookbook/Gorm.Cookbook.csproj -c Release
```

The [evidence-backed compatibility matrix](docs/compatibility.html) separates **tested SQL Server 2022 behavior** from **public-package in-memory behavior** and explicitly lists missing test coverage. Both the cookbook and SQL Server integration tests are part of PR CI.

## Install the public preview

Install [GORM 3.1.0-preview.1](https://www.nuget.org/packages/GORM/3.1.0-preview.1) in a .NET 10 project:

```bash
dotnet add package GORM --version 3.1.0-preview.1
```

The package and portable symbols have been accepted by NuGet.org, and a separate GitHub Actions run **successfully restored and executed a clean consumer from the public feed**: [public NuGet verification](https://github.com/pjotrcasteel/GORM/actions/runs/37825476545). The package is still a preview, not a stable release.

For future versions the [NuGet preview pipeline](.github/workflows/nuget-preview.yml) validates the package and real SQL Server tests before explicitly authorized publishing. A separate [public-feed check](.github/workflows/verify-public-nuget.yml) verifies installation without publishing again. See [RELEASING.md](RELEASING.md) and [CONTRIBUTING.md](CONTRIBUTING.md) for details.

## Repository governance

Pull requests are checked by public-surface validation, .NET package/build/tests, and live SQL Server Graph integration. The [repository governance guide](.github/BRANCH_PROTECTION.md) documents the GitHub settings that must be enabled by a repository administrator; a committed policy alone does not enforce branch protection.

## Creator

GORM is an independent open-source project by **[PjotrCasteel](https://pjotrcasteel.github.io/)**.
