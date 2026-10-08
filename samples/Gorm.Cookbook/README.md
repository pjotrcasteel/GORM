# Verified GORM NuGet Cookbook

This .NET 10 console app compiles and executes against the **published GORM 3.1.0-preview.1 NuGet package**. It needs neither SQL Server nor paid infrastructure, and it has no project references to GORM source.

```bash
dotnet run --project samples/Gorm.Cookbook/Gorm.Cookbook.csproj -c Release
```

Every recipe includes runtime assertions:

| Recipe | Verified public-package methods |
|---|---|
| Filter, order, page and aggregate | `RunFilteringAndPagingAsync` |
| Directed outgoing and incoming graph traversal | `RunDirectedTraversalsAsync` |
| Eager navigation loading | `RunRelationshipLoadingAsync` |
| Typed two-hop traversal | `RunMultiHopAsync` |
| Actual GORM SQL translation | `RunExplain` |

The examples use a deterministic graph created with `GraphContext`, `Connect` and `SaveChangesAsync`, configured for the in-memory engine. `Explain()` generates SQL with GORM's actual translation engine but **does not execute SQL Server**. Real database coverage is separately reported in the [compatibility matrix](../../docs/compatibility.html), grounded in Testcontainers methods.

The website Playground is still a JavaScript preview. The future production-accurate browser Playground must remain free to host (client-side, no paid backend).
