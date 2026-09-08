import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch

import app


class ContactPreviewUiTests(unittest.TestCase):
    def test_run_selection_does_not_read_contacts(self):
        with patch.object(app, "callback_context", SimpleNamespace(triggered_id="file-dropdown")), \
                patch.object(app, "contact_preview", side_effect=AssertionError("eager read")):
            self.assertEqual(app.preview_contacts(None, "run.csv"), "")

    def test_explicit_preview_resolves_selected_run_and_renders(self):
        path = Path("fixture/pandemic_run_1.csv")
        with patch.object(app, "callback_context", SimpleNamespace(triggered_id="contact-preview-btn")), \
                patch.object(app, "list_csvs", return_value=[path]), \
                patch.object(app, "contact_preview", return_value=[{"episode_id": "1"}]) as reader:
            result = app.preview_contacts(1, str(path))
            reader.assert_called_once_with(path.parent)
            self.assertIn("Showing 1 rows", result.children[0].children)

    def test_layout_and_dash_endpoints(self):
        client = app.app.server.test_client()
        self.assertEqual(client.get("/").status_code, 200)
        response = client.get("/_dash-layout")
        self.assertEqual(response.status_code, 200)
        self.assertIn(b"contact-preview-btn", response.data)


if __name__ == "__main__":
    unittest.main()
