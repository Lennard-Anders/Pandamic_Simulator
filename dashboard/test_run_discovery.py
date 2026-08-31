"""Focused tests for recursive dashboard run discovery."""

import json
import os
import tempfile
import unittest
from pathlib import Path

try:
    from .run_discovery import (
        discover_run_csvs,
        is_transient_run_path,
        run_display_label,
    )
except ImportError:
    from run_discovery import discover_run_csvs, is_transient_run_path, run_display_label


class RunDiscoveryTests(unittest.TestCase):
    def _write_run(self, path: Path, modified_ns: int) -> Path:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text("[RUN METADATA]\nvalue\n1\n", encoding="utf-8")
        os.utime(path, ns=(modified_ns, modified_ns))
        return path

    def test_discovers_manual_and_nested_runs_newest_first(self):
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            manual = self._write_run(root / "pandemic_run_manual.csv", 1_000_000_000)
            nested = self._write_run(
                root
                / "Experiments"
                / "Batch_A"
                / "01_Masks"
                / "run_001"
                / "pandemic_run_masks.csv",
                3_000_000_000,
            )
            self._write_run(
                root
                / "Experiments"
                / "Batch_A"
                / "01_Masks"
                / "run_0002_seed_10002.in-progress"
                / "pandemic_run_partial.csv",
                5_000_000_000,
            )
            self._write_run(
                root / "Experiments" / "Batch_A.tmp" / "pandemic_run_temp.csv",
                4_000_000_000,
            )

            self.assertEqual([nested, manual], discover_run_csvs(root))

    def test_uses_path_as_a_deterministic_timestamp_tie_breaker(self):
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            run_b = self._write_run(root / "b" / "pandemic_run_b.csv", 2_000_000_000)
            run_a = self._write_run(root / "a" / "pandemic_run_a.csv", 2_000_000_000)

            self.assertEqual([run_a, run_b], discover_run_csvs(root))

    def test_excludes_invalid_or_malformed_manifest_runs(self):
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            completed = self._write_run(root / "completed" / "pandemic_run_a.csv", 1_000_000_000)
            invalid = self._write_run(root / "invalid" / "pandemic_run_b.csv", 2_000_000_000)
            malformed = self._write_run(root / "malformed" / "pandemic_run_c.csv", 3_000_000_000)
            completed.with_name("run_manifest.json").write_text(
                json.dumps({"Status": "Completed"}), encoding="utf-8"
            )
            invalid.with_name("run_manifest.json").write_text(
                json.dumps({"Status": "Invalid"}), encoding="utf-8"
            )
            malformed.with_name("run_manifest.json").write_text("{not-json", encoding="utf-8")

            self.assertEqual([completed], discover_run_csvs(root))

    def test_manifest_label_accepts_pascal_case_serialized_fields(self):
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            run = self._write_run(
                root
                / "Experiments"
                / "opaque_batch"
                / "opaque_scenario"
                / "run_007"
                / "pandemic_run_masks.csv",
                1_000_000_000,
            )
            manifest = {
                "BatchName": "MasterThesis_Main",
                "ScenarioName": "Masks",
                "RunNumber": 7,
            }
            run.with_name("run_manifest.json").write_text(json.dumps(manifest), encoding="utf-8")

            self.assertEqual("MasterThesis_Main / Masks / Run 7", run_display_label(run, root))

    def test_falls_back_without_crashing_on_missing_or_malformed_manifest(self):
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            manual = self._write_run(root / "pandemic_run_manual.csv", 1_000_000_000)
            nested = self._write_run(
                root / "Experiments" / "Batch_A" / "01_Masks" / "run_003" / "pandemic_run.csv",
                2_000_000_000,
            )
            nested.with_name("run_manifest.json").write_text("{not-json", encoding="utf-8")

            self.assertEqual("pandemic_run_manual.csv", run_display_label(manual, root))
            self.assertEqual("Batch_A / 01_Masks / Run 003", run_display_label(nested, root))

    def test_recognizes_documented_transient_path_forms(self):
        self.assertTrue(is_transient_run_path(Path("run_001.__inprogress/pandemic_run_a.csv")))
        self.assertTrue(is_transient_run_path(Path("run_0001_seed_7.in-progress/pandemic_run_a.csv")))
        self.assertTrue(is_transient_run_path(Path("run_001.partial/pandemic_run_a.csv")))
        self.assertTrue(is_transient_run_path(Path(".tmp/pandemic_run_a.csv")))
        self.assertFalse(is_transient_run_path(Path("run_001/pandemic_run_a.csv")))


if __name__ == "__main__":
    unittest.main()
