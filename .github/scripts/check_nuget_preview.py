#!/usr/bin/env python3
"""Check the exact GORM package ID/version via NuGet's registry API."""
import json
import sys
import urllib.error
import urllib.request

if len(sys.argv) not in (2, 3) or (len(sys.argv) == 3 and sys.argv[2] != "--reject-existing"):
    raise SystemExit("Usage: check_nuget_preview.py VERSION [--reject-existing]")

version = sys.argv[1].lower()
reject_existing = len(sys.argv) == 3
url = "https://api.nuget.org/v3-flatcontainer/gorm/index.json"

try:
    with urllib.request.urlopen(url, timeout=20) as response:
        versions = [entry.lower() for entry in json.load(response).get("versions", [])]
except urllib.error.HTTPError as error:
    if error.code == 404:
        versions = []
    else:
        raise

if version in versions:
    if reject_existing:
        raise SystemExit(f"GORM {version} is already registered on NuGet.org; do not republish.")
    print(f"WARNING: GORM {version} already exists on NuGet.org. Candidate is a local-only recheck.")
elif versions:
    print(f"WARNING: GORM already has {len(versions)} version(s) on NuGet.org. Confirm you own the ID before publishing.")
else:
    print("No registered GORM package versions found at the time of this check.")

print(f"NuGet version probe completed for GORM {version}")