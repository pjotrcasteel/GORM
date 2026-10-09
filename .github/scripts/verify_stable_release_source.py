#!/usr/bin/env python3
"""Verify the source SHA embedded in an immutable public GORM NuGet package."""

from pathlib import Path
from sys import argv
from xml.etree import ElementTree
from zipfile import ZipFile

if len(argv) != 4:
    raise SystemExit("Usage: verify_stable_release_source.py PACKAGE.nupkg VERSION SOURCE_SHA")

package, version, source_sha = Path(argv[1]), argv[2], argv[3].lower()
if len(source_sha) != 40 or any(ch not in "0123456789abcdef" for ch in source_sha):
    raise SystemExit("Expected a complete 40-character Git source SHA.")
if version != "3.2.0":
    raise SystemExit("Stable release provenance verification is pinned to GORM 3.2.0.")


def nodes(root, name):
    return [node for node in root.iter() if node.tag.rsplit("}", 1)[-1] == name]


with ZipFile(package) as archive:
    nuspecs = [name for name in archive.namelist() if name.lower().endswith(".nuspec")]
    if len(nuspecs) != 1:
        raise SystemExit(f"Expected exactly one nuspec, found {len(nuspecs)}.")

    root = ElementTree.fromstring(archive.read(nuspecs[0]))
    ids, versions, repositories = (nodes(root, key) for key in ("id", "version", "repository"))

    if len(ids) != 1 or (ids[0].text or "").strip() != "GORM":
        raise SystemExit("The downloaded NuGet package does not identify itself as GORM.")
    if len(versions) != 1 or (versions[0].text or "").strip() != version:
        raise SystemExit(f"Public package does not have the requested version {version}.")
    if len(repositories) != 1:
        raise SystemExit("Expected one repository provenance element in the public nuspec.")

    repository = repositories[0]
    if repository.attrib.get("url") != "https://github.com/pjotrcasteel/GORM":
        raise SystemExit("Public package repository URL is incorrect.")
    embedded_sha = repository.attrib.get("commit", "").lower()
    if embedded_sha != source_sha:
        raise SystemExit(f"Public package source SHA {embedded_sha or '<missing>'} does not match requested tag SHA {source_sha}.")

print(f"Verified public GORM {version} package provenance matches commit {source_sha}.")
