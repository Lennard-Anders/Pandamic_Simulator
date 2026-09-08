import gzip
import hashlib
import tempfile
import unittest
from pathlib import Path

from run_aggregation import _manifest_files_valid


class ContactManifestTests(unittest.TestCase):
    def fixture(self, root, mode):
        required = {"run_summary.csv", "state_timeseries.csv", "transmission_events.csv", "test_events.csv",
                    "intervention_events.csv", "healthcare_timeseries.csv", "population_events.csv", "errors.json",
                    "contact_step_summary.csv", "contact_episode_summary.csv", "contact_episode_duration_distribution.csv"}
        names = {"Standard": ["contact_episodes.csv.gz"], "FullRaw": ["physical_contacts/day_000.csv.gz", "physical_contacts/day_001.csv.gz"], "SummaryOnly": []}[mode]
        entries = []
        contacts = []
        for name in sorted(required | set(names)):
            data = gzip.compress(b"header\nrow\n", mtime=0) if name in names else b"header\nrow\n"
            path = root / name
            path.parent.mkdir(exist_ok=True)
            path.write_bytes(data)
            entry = {"RelativePath": name, "LengthBytes": len(data), "Sha256": hashlib.sha256(data).hexdigest()}
            if name in names:
                entry.update(RowCount=1, SimulationStartTime="2030-01-01T00:00:00", SimulationEndTime="2030-01-01T00:05:00")
                contacts.append(dict(entry))
            entries.append(entry)
        return {"SchemaVersion": 4, "ScientificExportSchemaVersion": 2, "ScientificExtensionsVersion": 0,
                "ContactExportMode": mode, "ContactRepresentation": {"Standard": "episodes", "FullRaw": "epidemiological_steps", "SummaryOnly": "summaries"}[mode],
                "Compression": "gzip", "Partitioning": "simulation_day" if mode == "FullRaw" else "none",
                "ContactFiles": contacts, "OutputFiles": entries}

    def test_all_modes_validate_and_reject_unlisted_partitions(self):
        for mode in ("Standard", "FullRaw", "SummaryOnly"):
            with self.subTest(mode=mode), tempfile.TemporaryDirectory() as temp:
                root = Path(temp)
                manifest = self.fixture(root, mode)
                self.assertTrue(_manifest_files_valid(root, manifest))
                self.assertTrue(_manifest_files_valid(root, manifest, verify_contact_hashes=True))
                extra = root / "physical_contacts" / "day_999.csv.gz"
                extra.parent.mkdir(exist_ok=True)
                extra.write_bytes(b"unlisted")
                self.assertFalse(_manifest_files_valid(root, manifest))

    def test_metadata_disagreement_and_explicit_hash_check(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            manifest = self.fixture(root, "Standard")
            manifest["ContactFiles"][0]["RowCount"] = 2
            self.assertFalse(_manifest_files_valid(root, manifest))
            manifest["ContactFiles"][0]["RowCount"] = 1
            path = root / "contact_episodes.csv.gz"
            data = bytearray(path.read_bytes())
            data[-1] ^= 1
            path.write_bytes(data)
            self.assertTrue(_manifest_files_valid(root, manifest))  # Discovery intentionally checks raw size only.
            self.assertFalse(_manifest_files_valid(root, manifest, verify_contact_hashes=True))


if __name__ == "__main__":
    unittest.main()
