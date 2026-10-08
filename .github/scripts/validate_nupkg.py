#!/usr/bin/env python3
"""Check packed NuGet metadata and symbol content; no external packages required."""
from pathlib import Path
from sys import argv
from xml.etree import ElementTree
from zipfile import ZipFile

if len(argv) != 4:
    raise SystemExit("Usage: validate_nupkg.py package.nupkg symbols.snupkg version")

package, symbols, expected_version = Path(argv[1]), Path(argv[2]), argv[3]

def find_text(root, name):
    for node in root.iter():
        if node.tag.rsplit("}", 1)[-1] == name:
            return (node.text or "").strip()
    return ""

with ZipFile(package) as archive:
    names = archive.namelist()
    nuspecs = [name for name in names if name.endswith(".nuspec")]
    assert len(nuspecs) == 1, f"Expected exactly one nuspec, got {nuspecs}"
    xml = ElementTree.fromstring(archive.read(nuspecs[0]))
    required = {
        "id": "GORM",
        "version": expected_version,
        "license": "MIT",
        "authors": "Pjotr Casteel",
        "readme": "README.md",
    }
    for field, expected in required.items():
        actual = find_text(xml, field)
        assert actual == expected, f"Package {field}: {actual!r}, expected {expected!r}"
    assert any(name.lower() == "readme.md" for name in names), "Package README is missing"
    assert "lib/net10.0/Gorm.dll" in names, "GORM .NET 10 assembly is missing"
    repository = next((node for node in xml.iter() if node.tag.rsplit("}", 1)[-1] == "repository"), None)
    assert repository is not None, "Repository metadata is missing"
    assert repository.attrib.get("type") == "git", "Repository type must be git"
    assert repository.attrib.get("url") == "https://github.com/pjotrcasteel/GORM", "Incorrect repository URL"
    assert "routit" not in archive.read(nuspecs[0]).decode("utf-8").lower(), "Internal dependency in nuspec"

with ZipFile(symbols) as archive:
    assert any(name.endswith("Gorm.pdb") for name in archive.namelist()), "Portable symbols are missing"

print(f"Validated GORM {expected_version} NuGet metadata, assembly, README and symbols")
