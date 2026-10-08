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
- Reads SQL Server node history across deterministic create/update/delete capture times using `History<T>().AsOf(...)`
- Restores historical incoming/outgoing graph connections from persisted node and edge envelopes (evaluated in memory after SQL history reads)
- Proves rolled-back SQL transactions do not persist either node changes or their history records

Test data uses unique identifiers; the entire SQL Server container is destroyed after the fixture. No private connection strings, external database or NuGet release are involved.

See the [compatibility matrix](../../docs/compatibility.html) for a method-by-method evidence list and explicit outstanding gaps. The integration tests are not yet a comprehensive provider-parity or performance suite and do not certify all temporal/GraphRAG behavior.

## Historical edge lifecycle (SQL Server 2022)

The live integration suite verifies persisted connection, disconnection, explicit deletion, reconnection, parallel edge identities and transaction rollback with history. History queries reconstruct state from SQL Server's stored envelopes **in memory**, not via native temporal SQL graph queries. Node removal now explicitly deletes incident edges in mapped SQL graph tables within the same transaction and records terminal history for edges that already have recorded history. The integration suite verifies incoming/outgoing, parallel and self-referencing edges, unrelated-edge preservation, complete transaction rollback, and cleanup of edges created before history recording was enabled. GORM cannot reconstruct history that was never recorded. Concurrent edge inserts during node removal, unmapped edge tables and savepoint-scoped cascade rollback require further verification.

## Bitemporal SQL history (SQL Server 2022)

Nine live tests separate **business-valid time** (`ValidFromUtc`/`ValidToUtc`, half-open intervals) from **recorded-knowledge time** (`CapturedAtUtc`). They verify retrospectively recorded node corrections, time-bounded node and edge state, detached repeatable evidence, rollback and invalid-window rejection.

Use `SqlServerGraphHistoryReader.CaptureBitemporalDatasetAsync<TNode, TEdge>(nodeIds, edgeIds)` to capture a detached, explicitly scoped dataset, then call `Project` or `Compare` with `GraphBitemporalCoordinate`. History records with independent validity windows can be written using `SqlServerGraphHistoryRecorder.PersistAsync`. SQL history rows are read and resolved in memory; node and edge reads now share one SQL Server `SNAPSHOT` transaction. The database **must have `ALLOW_SNAPSHOT_ISOLATION ON`** (the disposable test fixture enables this). Concurrent commits after the snapshot begins are excluded from both reads. The selected identities are currently filtered in memory after reading type-wide history tables, and backdated evidence-only corrections do not automatically mutate live graph tables. A deterministic concurrent-writer test commits node and edge history between the two reads, asserting that neither part of the newer commit appears in the earlier snapshot.
