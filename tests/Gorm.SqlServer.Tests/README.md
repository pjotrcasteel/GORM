# SQL Server 2022 integration tests

This suite runs GORM against **real SQL Server Graph** in a disposable [Testcontainers](https://dotnet.testcontainers.org/modules/mssql/) instance (2022 release).

Unlike the fast MSTest project, it requires a reachable Docker daemon. GORM stays database-first: the tests provision an isolated database, apply the checked-in demo's SQL graph schema **outside the GORM runtime**, and exercise the public APIs with fresh connections.

## Run

```bash
dotnet test tests/Gorm.SqlServer.Tests/Gorm.SqlServer.Tests.csproj -c Release
```

Requires .NET 10, Docker and enough memory for SQL Server 2022. GitHub Actions uses `.github/workflows/sqlserver-integration.yml` and uploads TRX results.

## Coverage

- Validates mapped SQL Server columns and nullability against a DBA-style schema
- Persists and reloads nodes through separate GORM contexts
- Persists SQL Server Graph edges; traverses outgoing and incoming relationships
- Rolls back a GORM transaction and verifies no data was committed
- Persists and reads node history via SQL Server history tables
- Executes server-side `StartsWith` filters, deterministic `OrderBy/Skip/Take` paging, `CountAsync`, `LongCountAsync` and `AnyAsync`
- Traverses two stored graph edges using `ThenOutgoing` and verifies the second-hop result
- Commits an explicit SQL transaction and reloads the written node from a new connection
- Verifies nested transactions roll back to SQL Server savepoints without discarding outer writes
- Verifies explicit named savepoint rollback with additional writes after rollback
- Verifies optimistic concurrency on a dedicated versioned SQL Graph table using independent contexts: stale update/delete and sequential version increments

Test data uses unique identifiers; the entire SQL Server container is destroyed after the fixture. No private connection strings, external database or NuGet release are involved.

See the [compatibility matrix](../../docs/compatibility.html) for a method-by-method evidence list and explicit outstanding gaps. The integration tests are not yet a comprehensive provider-parity or performance suite and do not certify all temporal/GraphRAG behavior.
