# GORM source import

GORM's independently developed implementation was moved into the public repository with minimal behavioral changes.

- Public namespace: `Gorm`
- Library project: `src/Gorm/Gorm.csproj`
- Tests: `tests/Gorm.Tests/Gorm.Tests.csproj`
- Demo: `samples/Gorm.Demo/Gorm.Demo.csproj`
- The internal RoutIT.Common package and private feed were removed.
- A local `EnsureNotNull` implementation replaces the dependency's assertion helper.
- The library remains .NET 10; no NuGet release is made by this import.

Validation is provided by GitHub Actions on the pull request. SQL Server integration coverage may require dedicated infrastructure.
