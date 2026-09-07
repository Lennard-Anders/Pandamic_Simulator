"""Tests for scientific multi-run and paired-scenario aggregation."""

import csv
import hashlib
import json
import tempfile
import unittest
from pathlib import Path

try:
    from .run_aggregation import (
        aggregate_paired_differences,
        aggregate_scenarios,
        describe,
        load_completed_runs,
    )
except ImportError:
    from run_aggregation import (
        aggregate_paired_differences,
        aggregate_scenarios,
        describe,
        load_completed_runs,
    )


class RunAggregationTests(unittest.TestCase):
    def test_pair_ids_cannot_match_different_seeds_or_batches(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            first = self._run(root, "baseline", "Baseline", 1, 1, 10, "Completed")
            second = self._run(root, "other", "Other", 1, 1, 5, "Completed")
            manifest_path = second.parent / "run_manifest.json"
            manifest = json.loads(manifest_path.read_text())
            manifest["MasterSeed"] = 99
            manifest_path.write_text(json.dumps(manifest))
            self.assertEqual({}, aggregate_paired_differences(load_completed_runs([first, second]), "baseline"))
            manifest["MasterSeed"] = 1
            manifest["BatchId"] = "another-batch"
            manifest_path.write_text(json.dumps(manifest))
            self.assertEqual({}, aggregate_paired_differences(load_completed_runs([first, second]), "baseline"))

    def test_unpublished_path_and_corrupted_manifest_file_are_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            transient = self._run(root / "__inprogress", "a", "A", 1, 1, 10, "Completed")
            self.assertEqual([], load_completed_runs([transient]))
            rich = self._run(root, "b", "B", 1, 1, 10, "Completed")
            summary = rich.parent / "run_summary.csv"
            manifest_path = rich.parent / "run_manifest.json"
            manifest = json.loads(manifest_path.read_text())
            manifest["OutputFiles"] = [{"RelativePath": summary.name, "LengthBytes": summary.stat().st_size, "Sha256": hashlib.sha256(summary.read_bytes()).hexdigest()}]
            manifest_path.write_text(json.dumps(manifest))
            self.assertEqual(1, len(load_completed_runs([rich, rich])))
            summary.write_text("corrupted")
            self.assertEqual([], load_completed_runs([rich]))

    def test_describe_contains_required_statistics(self):
        result = describe([1.0, 2.0, 3.0, 4.0])
        self.assertEqual(4.0, result["n"])
        self.assertEqual(2.5, result["mean"])
        self.assertEqual(2.5, result["median"])
        self.assertAlmostEqual(1.2909944487358056, result["standard_deviation"])
        self.assertEqual(1.0, result["minimum"])
        self.assertEqual(4.0, result["maximum"])
        self.assertAlmostEqual(1.75, result["p25"])
        self.assertAlmostEqual(3.25, result["p75"])
        self.assertAlmostEqual(1.5, result["iqr"])
        self.assertAlmostEqual(1.15, result["p05"])
        self.assertAlmostEqual(3.85, result["p95"])

    def test_completed_runs_aggregate_by_scenario_and_invalid_is_excluded(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            paths = [
                self._run(root, "a", "A", 1, 1, 10.0, "Completed"),
                self._run(root, "a", "A", 2, 2, 20.0, "Completed"),
                self._run(root, "b", "B", 1, 1, 15.0, "Completed"),
                self._run(root, "b", "B", 2, 2, 30.0, "Invalid"),
            ]

            records = load_completed_runs(paths)
            aggregate = aggregate_scenarios(records)

            self.assertEqual(3, len(records))
            self.assertEqual(2, aggregate["a"]["run_count"])
            self.assertEqual(15.0, aggregate["a"]["metrics"]["attack_rate_pct"]["mean"])
            self.assertEqual(1, aggregate["b"]["run_count"])

    def test_paired_differences_are_matched_by_pair_id(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            paths = [
                self._run(root, "baseline", "Baseline", 1, 1, 10.0, "Completed"),
                self._run(root, "baseline", "Baseline", 2, 2, 20.0, "Completed"),
                self._run(root, "masks", "Masks", 1, 1, 7.0, "Completed"),
                self._run(root, "masks", "Masks", 2, 2, 12.0, "Completed"),
            ]

            records = load_completed_runs(paths)
            paired = aggregate_paired_differences(records, "baseline")
            metric = paired["masks"]["metrics"]["attack_rate_pct"]

            self.assertEqual(
                [{"pair_id": 1, "difference": -3.0}, {"pair_id": 2, "difference": -8.0}],
                metric["pairs"],
            )
            self.assertEqual(-5.5, metric["statistics"]["mean"])
            self.assertEqual(-5.5, metric["statistics"]["median"])

    def test_extended_manifest_requires_and_hashes_every_extension(self):
        import hashlib
        from run_aggregation import _manifest_files_valid
        with tempfile.TemporaryDirectory() as temporary:
            directory = Path(temporary)
            base = {"run_summary.csv", "state_timeseries.csv", "transmission_events.csv", "physical_contacts.csv", "traceable_contacts.csv", "test_events.csv", "intervention_events.csv", "healthcare_timeseries.csv", "population_events.csv", "errors.json"}
            extensions = {"contact_network_summary.csv", "age_mixing_matrix.csv", "contact_degree_distribution.csv", "contact_duration_distribution.csv", "contacts_by_time_of_day.csv", "calibration_results.csv"}
            entries = []
            for name in sorted(base | extensions):
                content = b"header\nvalue\n"
                (directory / name).write_bytes(content)
                entries.append({"RelativePath": name, "LengthBytes": len(content), "Sha256": hashlib.sha256(content).hexdigest()})
            manifest = {"SchemaVersion": 4, "ScientificExtensionsVersion": 1, "OutputFiles": entries}
            self.assertTrue(_manifest_files_valid(directory, manifest))
            manifest["OutputFiles"] = [e for e in entries if e["RelativePath"] != "calibration_results.csv"]
            self.assertFalse(_manifest_files_valid(directory, manifest))
            manifest["OutputFiles"] = entries
            (directory / "age_mixing_matrix.csv").write_bytes(b"corrupted")
            self.assertFalse(_manifest_files_valid(directory, manifest))

    @staticmethod
    def _run(root, scenario_id, scenario_name, run_number, pair_id, attack_rate, status):
        directory = root / scenario_id / f"run_{run_number:03d}"
        directory.mkdir(parents=True)
        rich = directory / f"pandemic_run_{scenario_id}_{run_number:03d}.csv"
        rich.write_text("[RUN METADATA]\na,b\n1,2\n", encoding="utf-8")
        with (directory / "run_summary.csv").open("w", encoding="utf-8", newline="") as stream:
            writer = csv.DictWriter(stream, fieldnames=["attack_rate_pct", "deaths_total"])
            writer.writeheader()
            writer.writerow({"attack_rate_pct": attack_rate, "deaths_total": run_number})
        (directory / "run_manifest.json").write_text(
            json.dumps({
                "Status": status,
                "BatchId": "batch",
                "ScenarioId": scenario_id,
                "ScenarioName": scenario_name,
                "RunNumber": run_number,
                "PairId": pair_id,
                "MasterSeed": pair_id,
            }),
            encoding="utf-8",
        )
        return rich


if __name__ == "__main__":
    unittest.main()
