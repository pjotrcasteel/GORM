from pathlib import Path

ROOT = Path(__file__).resolve().parent
DOCS = ROOT / "docs"

required = [
    "README.md",
    "LICENSE",
    "RELEASING.md",
    "CONTRIBUTING.md",
    "SECURITY.md",
    ".github/BRANCH_PROTECTION.md",
    ".github/pull_request_template.md",
    ".github/releases/3.1.0-preview.1.md",
    ".github/workflows/first-github-prerelease.yml",
    ".github/workflows/verify-public-nuget.yml",
    ".github/workflows/nuget-preview.yml",
    ".github/scripts/verify_package_consumer.sh",
    ".github/scripts/check_nuget_preview.py",
    "SemanticVersion.props",
    ".github/scripts/validate_nupkg.py",
    "SOURCE_MIGRATION.md",
    ".gitignore",
    "docs/index.html",
    "docs/styles.css",
    "docs/script.js",
    "docs/logo.svg",
    "docs/social-preview.svg",
    "docs/llms.txt",
    "docs/robots.txt",
    "docs/site.webmanifest",
    "docs/sitemap.xml",
    ".github/workflows/validate.yml",
    ".github/workflows/pages.yml",
    "src/Gorm/Gorm.csproj",
    "tests/Gorm.Tests/Gorm.Tests.csproj",
    "samples/Gorm.Demo/Gorm.Demo.csproj",
    "Gorm.sln",
    "NuGet.Config",
    ".github/workflows/build-gorm.yml",
]
for relative in required:
    if not (ROOT / relative).exists():
        raise SystemExit(f"Missing required repository-shell file: {relative}")

text = "\n".join(
    p.read_text(encoding="utf-8", errors="ignore")
    for p in ROOT.rglob("*")
    if p.is_file()
    and p.name != "validate.py"
    and p.suffix.lower() in {".md", ".txt", ".html", ".yml", ".yaml"}
)

for value in ("GORM", "Model how things connect.", "ChangeSet & transaction compiler"):
    if value not in text:
        raise SystemExit(f"Missing required public-surface concept: {value}")

# The public .NET source must not depend on the historic internal namespaces or feeds.
source_text = "\n".join(
    p.read_text(encoding="utf-8-sig", errors="ignore")
    for folder in ("src", "tests", "samples")
    for p in (ROOT / folder).rglob("*")
    if p.is_file() and p.suffix.lower() in {".cs", ".csproj", ".props", ".json"}
)
for forbidden in ("KPN.IRMA", "RoutIT.Common", "Application.Forge", "pkgs.dev.azure.com"):
    if forbidden.lower() in source_text.lower():
        raise SystemExit(f"Internal dependency or namespace remains: {forbidden}")

# Public product copy must not market the internal historical capability as a Forge sub-brand.
public_text = "\n".join([
    (ROOT / "README.md").read_text(encoding="utf-8"),
    (DOCS / "index.html").read_text(encoding="utf-8"),
    (DOCS / "llms.txt").read_text(encoding="utf-8"),
])
if 'GORM "Forge"' in public_text or "Forge compiler" in public_text:
    raise SystemExit("Do not expose the historical GORM-internal Forge sub-brand in public product copy.")

# Publishing must be confined to the manually approved, main-only OIDC workflow.
release_workflow = (ROOT / ".github/workflows/nuget-preview.yml").read_text(encoding="utf-8")
release_guards = (
    "github.event_name == 'workflow_dispatch'",
    "inputs.publish == true",
    "github.ref == 'refs/heads/main'",
    "environment: nuget-preview",
    "id-token: write",
    "NuGet/login@v1",
    "dotnet nuget push",
)
for guard in release_guards:
    if guard not in release_workflow:
        raise SystemExit(f"Missing guarded NuGet release policy: {guard}")

for path in (ROOT / ".github/workflows").glob("*.yml"):
    if path.name != "nuget-preview.yml" and "dotnet nuget push" in path.read_text(encoding="utf-8"):
        raise SystemExit(f"NuGet publication outside the approved release workflow: {path.name}")

if "Disallow: /" in (DOCS / "robots.txt").read_text(encoding="utf-8"):
    raise SystemExit("Public GORM site must be crawlable.")

# Package and license assertions are intentionally statically checkable as well as CI-tested.
project = (ROOT / "src/Gorm/Gorm.csproj").read_text(encoding="utf-8")
version_props = (ROOT / "SemanticVersion.props").read_text(encoding="utf-8")
if "<SemanticVersion>3.1.0</SemanticVersion>" not in version_props:
    raise SystemExit("Expected single repository-wide GORM version 3.1.0.")
if (ROOT / "src/Gorm/SemanticVersion.props").exists():
    raise SystemExit("Project-specific semantic version overrides are not allowed.")
for token in ("<PackageId>GORM</PackageId>", "<PackageLicenseExpression>MIT</PackageLicenseExpression>", "Microsoft.SourceLink.GitHub", "<PackageReadmeFile>README.md</PackageReadmeFile>"):
    if token not in project:
        raise SystemExit(f"Missing GORM package metadata: {token}")
if "MIT License" not in (ROOT / "LICENSE").read_text(encoding="utf-8"):
    raise SystemExit("Missing MIT license text.")
# The one-time GitHub release may create a public source tag, but must never republish NuGet.
first_release = (ROOT / ".github/workflows/first-github-prerelease.yml").read_text(encoding="utf-8")
for guard in ("gh release create", "--target", "--prerelease", "contents: write", "verify_package_consumer.sh"):
    if guard not in first_release:
        raise SystemExit(f"GitHub prerelease provenance guard missing: {guard}")
if "dotnet nuget push" in first_release or "nuget push" in first_release:
    raise SystemExit("GitHub prerelease workflow must not publish NuGet packages.")
print("GORM public site, release governance, package metadata and independent source layout validated.")
