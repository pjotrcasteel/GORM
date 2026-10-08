# First GORM NuGet preview

The first preview candidate is **GORM 3.1.0-preview.1** targeting **.NET 10**. The project is MIT licensed. The repository's `SemanticVersion.props` remains `3.1.0`; the preview suffix is applied as an explicit build-time version, without changing the normal stable version.

## Quality gates

The [preview workflow](.github/workflows/nuget-preview.yml) runs these independent gates:

- full solution restore, Release build and MSTest suite;
- a versioned `.nupkg` and `.snupkg`, with MIT license, README, repository metadata and SourceLink symbols;
- a clean external .NET 10 consumer referencing the packaged artifact via `PackageReference`;
- five real SQL Server 2022 integration tests using Testcontainers;
- a version lookup against NuGet's registry API (not search indexing).

Pull requests run the checks, **never publish**, and provide an artifact. A manual workflow run with `publish = false` builds a candidate without publishing. The workflow never releases automatically on a merge.

## One-time NuGet.org setup

1. Sign in at [NuGet.org](https://www.nuget.org/) (create an account if needed). Confirm package ID `GORM` is available or owned by you. **If it belongs to someone else, don't attempt publication.**
2. Under the NuGet.org account's **Trusted Publishing** settings, create a GitHub Actions policy with repository owner `pjotrcasteel`, repository `GORM`, workflow filename `nuget-preview.yml`, and environment `nuget-preview`.
3. Under the GitHub repository **Settings > Environments**, create `nuget-preview`, preferably with required-reviewer approval and a deployment branch restriction to `main`.
4. The workflow already uses the public NuGet.org profile name `PjotrCasteel`; no `NUGET_USER` variable or persistent API key is necessary.
5. Ensure the trusted-publishing policy's package owner matches the NuGet account that owns the `GORM` package ID.

The publish job uses [NuGet/login@v1](https://github.com/NuGet/login) to exchange a GitHub OIDC identity for a temporary scoped NuGet API credential.

## Release procedure

1. Merge the release preparation PR after all GitHub Actions checks pass.
2. Go to GitHub > **Actions** > **GORM NuGet preview release** > **Run workflow**, using `main`, `version = 3.1.0-preview.1`, and `publish = false`. Inspect the uploaded packages and checks.
3. When NuGet Trusted Publishing and the `nuget-preview` GitHub environment have been configured, rerun with `publish = true`. Type exactly `publish GORM 3.1.0-preview.1` in the confirmation field.
4. Publication runs only after both the full package gate and the live SQL Server gate pass. It checks the version is not already registered, obtains short-lived credentials and pushes the immutable package and symbols. A **successful publish job confirms that NuGet accepted the upload**, not that public-feed indexing has completed.
5. Run the separate [GORM public NuGet verification](.github/workflows/verify-public-nuget.yml) workflow from `main` for the published version. It polls the NuGet flat-container registry and restores/runs an independent .NET 10 consumer **using only NuGet.org**. It can be rerun safely: it never publishes anything.
6. Once public verification succeeds, create a corresponding GitHub prerelease/tag. This is not automated by the workflow.

NuGet versions are immutable: **publishing is irreversible**. The public package name may already be owned; only NuGet.org can definitively accept or reject the attempted claim. Do not publish from a branch other than `main`.

After the preview is actually published:

```bash
dotnet add package GORM --version 3.1.0-preview.1
```

A GitHub Actions candidate artifact is **not** a NuGet.org publication. If publication receives HTTP `201 Created` but public verification reports `NU1101`, NuGet.org is likely still validating/indexing the package. **Do not rerun the publishing workflow for the same version.** Use the verification-only workflow and check NuGet.org's package-management status.

## Future work

Before a stable release, expand SQL Server provider-parity coverage, security and performance testing, provenance/attestation, and real-engine website Playground integration.