# GORM branch protection and quality gates

The following are **GitHub administrator settings**, not enforced by the files in this repository. A policy document and a CODEOWNERS file do not create GitHub branch protection.

## Protect `main`

In [repository Settings > Rules > Rulesets](https://github.com/pjotrcasteel/GORM/settings/rules), create an active branch ruleset targeting the default branch (`main`). Alternatively use classic branch protection.

1. Require a pull request before merging (direct pushes should be restricted).
2. Require these always-running pull-request check contexts:
   - `validate` — **Validate public surface**
   - `test-and-pack` — **GORM .NET validation**
   - `sqlserver` — **SQL Server 2022 integration**
3. Require the branch to be up to date before merging if you want strict sequential validation (costs extra CI runs).
4. Include administrators in enforcement and disallow bypass except for a deliberate emergency policy.
5. Do **not** require an approving review by the same sole owner: GitHub does not allow an author to approve their own PR. Add a reviewer requirement once additional maintainers exist.
6. Keep GitHub Actions permitted to read repository contents and run Docker on GitHub-hosted Ubuntu.

The three required checks above are configured to run on **every pull request** (no PR path filters), so docs-only PRs do not become unmergeable due to a missing required job.

Do not make `Deploy GitHub Pages`, `GORM NuGet preview release`, or `GORM public NuGet verification` mandatory PR checks. These workflows are conditional, gated, or release-specific.

## Publish permissions and release boundary

- The `nuget-preview` GitHub environment permits only branch `main`.
- NuGet Trusted Publishing is scoped to the exact `GORM` package ID, this repository, the `nuget-preview.yml` workflow, and the `nuget-preview` environment.
- NuGet publishing is intentionally **manual**, with confirmation and a disposable OIDC credential.
- The one-time first GitHub prerelease workflow **never pushes a NuGet package**. It validates public installation and tags the exact published source commit. Subsequent releases must be deliberately configured separately.

The GitHub repository connector cannot enable repository rulesets in this session. **Administrator action is required to activate the branch ruleset**. Verify its status after saving; an empty rulesets listing is not proof that classic protection is absent.