import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

import app
from run_discovery import discover_run_csvs
from run_aggregation import load_completed_run


class BatchImportTests(unittest.TestCase):
    def test_summary_discovery_excludes_invalid_temporary_and_duplicate_run(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            for name, status in (("valid", "Completed"), ("invalid", "Invalid"), ("x.__inprogress", "Completed")):
                directory = root / name
                directory.mkdir()
                (directory / "run_summary.csv").write_text("tracked_population\n10\n")
                (directory / "run_manifest.json").write_text(json.dumps({"Status": status}))
            summary = root / "valid" / "run_summary.csv"
            self.assertEqual(discover_run_csvs(root), [summary])
            self.assertIsNone(load_completed_run(root / "invalid" / "run_summary.csv"))
            self.assertIsNone(load_completed_run(root / "x.__inprogress" / "run_summary.csv"))
            rich = summary.with_name("pandemic_run_a.csv")
            rich.write_text("[RUN METADATA]\nvalue\n1\n")
            self.assertEqual(discover_run_csvs(root), [rich])

    def test_origins_include_events_after_preview_limit_and_map_exported_enums(self):
        with tempfile.TemporaryDirectory() as temp:
            directory = Path(temp)
            with (directory / "transmission_events.csv").open("w") as stream:
                stream.write("pandemic_day,origin_category,is_initial_seed\n")
                stream.write("0,InitialSeed,1\n")
                stream.write("1,WorkplaceOfficeIndustry,0\n" * 100005)
                stream.write("2,Bus,0\n2,ResidentialHome,0\n2,OutdoorStreet,0\n")
            origins, timeline = app._batch_transmission_origins(directory)
            counts = origins.set_index("origin")["count"].to_dict()
            self.assertEqual(counts, {"work": 100005, "transit": 1, "home": 1, "outdoor": 1})
            self.assertEqual(timeline["work"].sum(), 100005)

    def test_summary_import_does_not_open_raw_contacts_and_finds_true_peak(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            (root / "run_summary.csv").write_text("tracked_population,active_exposed,active_infectious,active_post_infectious_ill,recovered_total,deaths_total,secondary_transmissions_total,attack_rate_pct,resolved_case_fatality_ratio_pct\n10,1,1,1,1,0,1,40,0\n")
            (root / "state_timeseries.csv").write_text("simulation_time,pandemic_day,susceptible,exposed,infectious,post_infectious_ill,recovered,dead\n2030-01-01,0,7,0,3,0,0,0\n2030-01-02,1,6,1,1,1,1,0\n")
            original = app._read_batch_csv
            def guarded(directory, name, nrows=None):
                self.assertNotIn(name, ("physical_contacts.csv", "traceable_contacts.csv", "contact_episodes.csv.gz"))
                return original(directory, name, nrows)
            with patch.object(app, "_read_batch_csv", side_effect=guarded):
                data = app.load_csv(str(root / "run_summary.csv"))
            self.assertEqual(app.extract_kpis(data)["peak_sick"], "3")
            self.assertEqual(app.extract_kpis(data)["peak_day"], "Day 0")
            self.assertEqual(data["CORE METRICS"].iloc[0]["healthy"], 6)


if __name__ == "__main__":
    unittest.main()
