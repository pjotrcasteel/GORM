# Contributing to GORM

Thank you for helping improve [GORM](https://github.com/pjotrcasteel/GORM), a database-first SQL Server Graph ORM for .NET 10.

## Development prerequisites

- .NET 10 SDK
- A recent Docker Engine / Docker Desktop, when running SQL Server integration tests
- Node.js 24, when changing the documentation website

## Build and test

```bash
dotnet restore Gorm.sln --configfile NuGet.Config
dotnet build Gorm.sln -c Release --no-restore
dotnet test Gorm.sln -c Release --no-build
dotnet test tests/Gorm.SqlServer.Tests/Gorm.SqlServer.Tests.csproj -c Release
```

SQL Server integration tests use Testcontainers, a disposable SQL Server 2022 instance, and the schema in `samples/Gorm.Demo/Scripts/001_schema.sql`. No private database is required. For documentation changes, run `node docs/tests/validate-docs.cjs` and `python validate.py`.

## Pull requests

- Target `main` and keep changes focused.
- Include MSTest coverage for new behavior; use `Method_State_Expected` names and cancellation tokens where applicable.
- For SQL Server provider changes, add real integration tests instead of relying only on generated SQL assertions.
- Describe user-facing changes, validation results, compatibility implications and documentation updates.
- Preserve the public `Gorm` namespaces and `GORM` NuGet package identity; avoid company-specific source dependencies.
- Wait for the required CI checks in [branch governance](.github/BRANCH_PROTECTION.md). Contributors cannot approve their own PRs.
- Do not manually re-push already released NuGet versions. Version numbers are immutable.

## Releases

Preview publishing requires human authorization, the `nuget-preview` GitHub environment, and NuGet.org Trusted Publishing. See [RELEASING.md](RELEASING.md). PR CI and normal merges do not publish packages.

For vulnerability reports, see [SECURITY.md](SECURITY.md).