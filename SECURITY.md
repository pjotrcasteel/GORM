# Security policy

GORM is currently an early public preview for .NET 10. Security fixes may ship as newer preview releases; no stable-release support window is promised yet.

## Report a vulnerability

Please avoid posting exploit details in a public issue or pull request. On [GORM's Security tab](https://github.com/pjotrcasteel/GORM/security), use **Report a vulnerability** / private vulnerability reporting if enabled. If GitHub private reporting is unavailable, contact the maintainer privately via their GitHub profile and request a secure reporting channel before sharing technical details.

Include the affected package version, a minimal reproduction, severity/impact, and any suggested mitigation when possible. No guaranteed response SLA has been established.

## Security boundaries

GORM translates user-authored query expressions into SQL for a caller-supplied database. The application remains responsible for database credentials, least-privilege SQL permissions, tenant isolation and validating external input. The in-memory provider is not a substitute for SQL Server security/integration testing.

Do not commit NuGet publishing tokens or connection strings; release publishing uses GitHub OIDC and scoped NuGet Trusted Publishing.