import gzip
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from contact_reader import iter_contacts, iter_contact_chunks, contact_preview


class ContactReaderTests(unittest.TestCase):
    def test_preview_stops_at_limit_and_closes_stream(self):
        closed = []
        def source(*args):
            try:
                yield {"contact_id": "1"}
                yield {"contact_id": "2"}
                raise AssertionError("Preview read beyond requested limit")
            finally:
                closed.append(True)
        with patch("contact_reader.iter_contacts", source):
            self.assertEqual(len(contact_preview(Path("."), limit=2)), 2)
        self.assertEqual(closed, [True])
        with self.assertRaises(ValueError):
            contact_preview(Path("."), limit=1001)

    def test_legacy_traceability_filter_and_chunks(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            (root / "physical_contacts.csv").write_text("contact_id,traceable_by_app,traceable_by_manual\n1,1,0\n2,0,0\n3,0,1\n")
            self.assertEqual([r["contact_id"] for r in iter_contacts(root, traceable_only=True)], ["1", "3"])
            self.assertEqual([len(c) for c in iter_contact_chunks(root, chunk_size=2)], [2, 1])

    def test_new_modes_stream_partitions_and_summary_is_empty(self):
        for mode in ("Standard", "FullRaw", "SummaryOnly"):
            with self.subTest(mode=mode), tempfile.TemporaryDirectory() as temp:
                root = Path(temp)
                names = {"Standard": ["contact_episodes.csv.gz"], "FullRaw": ["physical_contacts/day_000.csv.gz", "physical_contacts/day_001.csv.gz"], "SummaryOnly": []}[mode]
                (root / "run_manifest.json").write_text(json.dumps({"Status": "Completed", "ScientificExportSchemaVersion": 2,
                    "ContactExportMode": mode, "ContactFiles": [{"RelativePath": n} for n in names]}))
                for i, name in enumerate(names):
                    path = root / name
                    path.parent.mkdir(exist_ok=True)
                    with gzip.open(path, "wt") as out:
                        out.write(f"citizen_a,citizen_b,traceable_by_app,traceable_by_manual\n{i},2,1,0\n")
                # Creating the iterator does not open any contact file.
                with patch("contact_reader.gzip.open", side_effect=AssertionError("eager read")):
                    rows = iter_contacts(root)
                self.assertEqual(len(list(rows)), len(names))

    def test_traversal_and_invalid_runs_fail_closed(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            for status, name in (("Invalid", "contact_episodes.csv.gz"), ("Completed", "../outside.csv")):
                (root / "run_manifest.json").write_text(json.dumps({"Status": status, "ScientificExportSchemaVersion": 2,
                    "ContactFiles": [{"RelativePath": name}]}))
                with self.assertRaises(ValueError):
                    list(iter_contacts(root))


if __name__ == "__main__":
    unittest.main()
