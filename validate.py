from pathlib import Path

ROOT = Path(__file__).resolve().parent
DOCS = ROOT / "docs"

required = [
    "README.md",
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

if "Application.Forge" not in (ROOT / "SOURCE_MIGRATION.md").read_text(encoding="utf-8"):
    raise SystemExit("Source migration must track the historical Application.Forge rename.")

# Public product copy must not market the internal historical capability as a Forge sub-brand.
public_text = "\n".join([
    (ROOT / "README.md").read_text(encoding="utf-8"),
    (DOCS / "index.html").read_text(encoding="utf-8"),
    (DOCS / "llms.txt").read_text(encoding="utf-8"),
])
if 'GORM "Forge"' in public_text or "Forge compiler" in public_text:
    raise SystemExit("Do not expose the historical GORM-internal Forge sub-brand in public product copy.")

if "dotnet nuget push" in text or "nuget.org/api" in text:
    raise SystemExit("NuGet publishing must remain absent from the repository shell.")

if "Disallow: /" in (DOCS / "robots.txt").read_text(encoding="utf-8"):
    raise SystemExit("Public GORM site must be crawlable.")

print("GORM repository shell validated: public site live; source/NuGet migration intentionally deferred.")
