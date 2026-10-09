#!/usr/bin/env python3
"""Unit-test public NuGet source provenance validation without network access."""

from pathlib import Path
from tempfile import TemporaryDirectory
from subprocess import run
from zipfile import ZipFile
import sys
import unittest

SCRIPT = Path(__file__).with_name("verify_stable_release_source.py")
SOURCE_SHA = "a" * 40


class StableSourceProvenanceTests(unittest.TestCase):
    def run_check(self, package_id="GORM", version="3.2.0", repository="https://github.com/pjotrcasteel/GORM", commit=SOURCE_SHA, expected=SOURCE_SHA):
        with TemporaryDirectory() as directory:
            package = Path(directory) / "GORM.3.2.0.nupkg"
            nuspec = f"""<package><metadata><id>{package_id}</id><version>{version}</version><repository type="git" url="{repository}" commit="{commit}" /></metadata></package>"""
            with ZipFile(package, "w") as archive:
                archive.writestr("GORM.nuspec", nuspec)
            return run([sys.executable, str(SCRIPT), str(package), "3.2.0", expected], capture_output=True, text=True, check=False)

    def test_exact_matching_package_passes(self):
        self.assertEqual(self.run_check().returncode, 0)

    def test_mismatched_source_fails_closed(self):
        self.assertNotEqual(self.run_check(expected="b" * 40).returncode, 0)

    def test_missing_commit_fails_closed(self):
        self.assertNotEqual(self.run_check(commit="").returncode, 0)

    def test_wrong_nuget_package_fails_closed(self):
        self.assertNotEqual(self.run_check(package_id="Other").returncode, 0)

    def test_wrong_repository_fails_closed(self):
        self.assertNotEqual(self.run_check(repository="https://github.com/other/repo").returncode, 0)

    def test_wrong_version_fails_closed(self):
        self.assertNotEqual(self.run_check(version="3.1.0").returncode, 0)


if __name__ == "__main__":
    unittest.main()
