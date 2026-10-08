# GORM release process

**The first preview, GORM 3.1.0-preview.1, was uploaded to NuGet.org and independently installed from the public feed.** See [verification run #37825476545](https://github.com/pjotrcasteel/GORM/actions/runs/37825476545). The repository's `SemanticVersion.props` is being advanced to the **unpublished source baseline `3.2.0`**; prerelease suffixes are supplied as explicit build-time versions. The project is MIT licensed and targets .NET 10.

## 3.2.0 candidate freeze (8 October 2026)

The latest **public** package remains `GORM 3.1.0-preview.1`. Updating `SemanticVersion.props` to `3.2.0` changes source builds and CI artifacts, **not** NuGet.org or the GitHub release list.

- Candidate target: `3.2.0-preview.1`, with release notes in [3.2.0-preview.1.md](.github/releases/3.2.0-preview.1.md).
- Complete the final SQL Server, change-tracker, concurrency, package, documentation and compatibility tests on the release source SHA. Record any remaining unsupported behaviors explicitly, rather than holding the milestone for Playground or Visual Studio features.
- First use the **GORM NuGet preview release** manual workflow with `version=3.2.0-preview.1` and `publish=false`. It validates and uploads an Actions candidate artifact but **never publishes** to NuGet.
- Publishing the immutable preview requires deliberate package-owner approval and the existing guarded `nuget-preview` Trusted Publishing configuration. Set `publish=true` only after inspecting the candidate; confirmation must exactly match `publish GORM 3.2.0-preview.1`.
- Independently verify the **public** preview from NuGet.org before creating tag `v3.2.0-preview.1` and matching GitHub prerelease at the **exact source commit used for packaging**.
- Stable `3.2.0` uses the separate [GORM NuGet stable release](.github/workflows/nuget-stable.yml) workflow, **not** the preview workflow. Run it on `main` first with `version=3.2.0` and `publish=false`. A later `publish=true` dispatch additionally requires the exact confirmation `publish GORM 3.2.0`, both release gates, and the `nuget-stable` protected environment. Publishing is always a manual, irreversible action.

The 3.2 release candidate is not blocked by the opt-in WebAssembly download size, arbitrary C# Playground editing, a future Visual Studio extension, or unverified experimental graph-intelligence capabilities not promised by the stable core package.

## One-time stable NuGet Trusted Publishing setup

A stable package uses a **different GitHub Actions workflow and environment** than the existing preview. Before an authorized stable publish:

1. Configure a separate NuGet.org Trusted Publishing policy for GitHub owner `pjotrcasteel`, repo `GORM`, workflow filename `nuget-stable.yml`, environment `nuget-stable` and account/package owner `PjotrCasteel`.
2. Configure GitHub's `nuget-stable` environment with required manual review and `main` deployment-branch restriction. GitHub and NuGet.org credentials are not stored in source control.
3. Verify the `GORM NuGet stable release` workflow passes both local-package/clean-consumer and real SQL Server 2022 gates with `publish=false`. Validate the exact source SHA before approving `publish=true`.
4. After publication, run the [GORM public NuGet verification](.github/workflows/verify-public-nuget.yml) with `version=3.2.0` and confirm an independent clean restore. Only then create GitHub release/tag `v3.2.0` at the exact package source SHA.

Do **not** run a stable NuGet publication automatically after a merge. The workflow's publish job is dispatch-only and requires explicit version-matching confirmation.

## Quality gates

The [preview workflow](.github/workflows/nuget-preview.yml) runs these independent gates:

- full solution restore, Release build and MSTest suite;
- a versioned `.nupkg` and `.snupkg`, with MIT license, README, repository metadata and SourceLink symbols;
- a clean external .NET 10 consumer referencing the packaged artifact via `PackageReference`;
- five real SQL Server 2022 integration tests using Testcontainers;
- a version lookup against NuGet's registry API (not search indexing).

Pull requests run the checks, **never publish**, and provide an artifact. A manual workflow run with `publish = false` builds a candidate without publishing. The workflow never releases automatically on a merge.

## Trusted Publishing configuration (already configured for this repository)

1. Sign in at [NuGet.org](https://www.nuget.org/) as the existing package owner `PjotrCasteel` and check the `GORM` package details.
2. Under the NuGet.org account's **Trusted Publishing** settings, create a GitHub Actions policy with repository owner `pjotrcasteel`, repository `GORM`, workflow filename `nuget-preview.yml`, and environment `nuget-preview`.
3. Under the GitHub repository **Settings > Environments**, create `nuget-preview`, preferably with required-reviewer approval and a deployment branch restriction to `main`.
4. The workflow already uses the public NuGet.org profile name `PjotrCasteel`; no `NUGET_USER` variable or persistent API key is necessary.
5. Ensure the trusted-publishing policy's package owner matches the NuGet account that owns the `GORM` package ID.

The publish job uses [NuGet/login@v1](https://github.com/NuGet/login) to exchange a GitHub OIDC identity for a temporary scoped NuGet API credential.

## Release procedure

1. Merge the release preparation PR after all GitHub Actions checks pass.
2. Go to GitHub > **Actions** > **GORM NuGet preview release** > **Run workflow**, using `main`, `version = 3.2.0-preview.1`, and `publish = false`. Inspect the uploaded packages and checks.
3. When NuGet Trusted Publishing and the `nuget-preview` GitHub environment have been configured, rerun with `publish = true`. Type exactly `publish GORM 3.2.0-preview.1` in the confirmation field.
4. Publication runs only after both the full package gate and the live SQL Server gate pass. It checks the version is not already registered, obtains short-lived credentials and pushes the immutable package and symbols. A **successful publish job confirms that NuGet accepted the upload**, not that public-feed indexing has completed.
5. Run the separate [GORM public NuGet verification](.github/workflows/verify-public-nuget.yml) workflow from `main` for the published version. It polls the NuGet flat-container registry and restores/runs an independent .NET 10 consumer **using only NuGet.org**. It can be rerun safely: it never publishes anything.
6. After successful public verification, create the matching GitHub prerelease/tag. For the **first preview only**, the guarded [first GitHub prerelease workflow](.github/workflows/first-github-prerelease.yml) creates `v3.1.0-preview.1` pointing to the exact source commit that produced the published package (`082c1b878fc9ab896d874f1940ccc45b58752b52`), not to a later documentation/CI commit. Subsequent versions require an explicit release workflow or maintainer action.

NuGet versions are immutable: **publishing is irreversible**. The public package name may already be owned; only NuGet.org can definitively accept or reject the attempted claim. Do not publish from a branch other than `main`.

The verified public preview can be installed using:

```bash
dotnet add package GORM --version 3.1.0-preview.1
```

A GitHub Actions candidate artifact is **not** a NuGet.org publication. If publication receives HTTP `201 Created` but public verification reports `NU1101`, NuGet.org is likely still validating/indexing the package. **Do not rerun the publishing workflow for the same version.** Use the verification-only workflow and check NuGet.org's package-management status.

## Future work

Before stable `3.2.0`, finish the specifically supported SQL Server and packaging gates, and document unverified advanced areas. The opt-in browser Playground already uses the real GORM runtime for four bounded presets; generic editable C# and runtime-size improvements are post-release milestones.

## Published preview record

- NuGet.org: https://www.nuget.org/packages/GORM/3.1.0-preview.1
- Release package/source commit: `082c1b878fc9ab896d874f1940ccc45b58752b52`
- NuGet push: HTTP 201 for both package and symbols (the first publishing workflow timed out only while waiting for indexing)
- Public consumer verification: https://github.com/pjotrcasteel/GORM/actions/runs/37825476545 — succeeded
- Matching GitHub prerelease: tag `v3.1.0-preview.1`, created by the one-time GitHub release workflow upon merge of its workflow into `main` (only if source and public install checks pass). Do not re-tag or publish the NuGet package.

The first publication's original GitHub workflow conclusion was **failure**, because NuGet.org indexing exceeded its original wait window. The two HTTP 201 uploads were successful and the later independent verification passed. This is why upload and public verification are now distinct workflows.