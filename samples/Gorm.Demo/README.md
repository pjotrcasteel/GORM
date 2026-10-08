# Gorm.Demo

This demo is **strictly database-first**.

GORM does not create, migrate, or upgrade the database. The schema is owned by your normal database deployment process. The demo validates the existing SQL Server Graph schema before it inserts or queries graph data.

## What this demo shows

- validates SQL Server Graph schema drift before running
- checks node/edge tables, mapped columns, store types, nullability, and mapped indexes
- inserts nodes
- connects edges
- traverses outgoing and incoming relationships
- eager loads with `Include` and `ThenInclude`
- uses no-tracking queries
- produces query diagnostics via `Explain()`
- exercises release-grade operators such as `AllAsync`, `ElementAtAsync`, and `ToDictionaryAsync`
- builds a `GraphDocument` for future visualization

## What this demo does not use

- no DbUp
- no generated migrations
- no automatic database creation
- no stored procedures
- no table-valued parameters

## Run

Apply the SQL in `Scripts/001_schema.sql` through your normal database-first deployment flow first.

Set a connection string:

```powershell
$env:GORM_SQLSERVER_CONNECTION="Server=localhost,1433;Database=GraphDemo;User Id=sa;Password=YourStrongPassw0rd;TrustServerCertificate=True;"
```

Then run:

```powershell
dotnet run --project .\Gorm.Demo
```

## Schema source of truth

The database is the source of truth. `Scripts/001_schema.sql` is only a demo/reference script. In a production flow this should live in the DBA/database repository, not be generated or executed by GORM.
