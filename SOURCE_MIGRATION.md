# GORM public source migration

GORM is independently developed source code published under its own identity. The owner has confirmed that it was built independently on private time.

## Public layout

- `src/Gorm/` — .NET 10 core implementation, namespace `Gorm`, NuGet ID `GORM`
- `tests/Gorm.Tests/` — MSTest test suite
- `samples/Gorm.Demo/` — SQL Server Graph demo
- `docs/` — interactive public documentation and browser-side query preview

The old organization-specific namespaces, project identifiers, and internal RoutIT.Common package reference have been removed. Public package restore uses nuget.org. The prior dependency's one non-null assertion extension has a self-contained GORM implementation.

## Validation

The source-import PR runs restore, build and test with .NET 10 in GitHub Actions. SQL Server integration/performance validation and final NuGet packaging need separate verification. Public-source publication does **not** constitute a NuGet release.

## Licensing and packaging

The independently developed GORM source and documentation are licensed under the [MIT License](LICENSE). Package metadata, deterministic SourceLink symbols and an isolated NuGet consumer check are verified in CI. Publishing to NuGet.org still requires a separate explicit release decision.
