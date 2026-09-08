"""Check actual C# exporter fixtures; pass TENUS_DASHBOARD_CONTRACT_ROOT as argv[1]."""
import sys
from pathlib import Path
from unittest.mock import patch

import app
from contact_reader import contact_preview


def verify(root):
    for mode, expected_rows in (("Standard", 1), ("FullRaw", 4), ("SummaryOnly", 0)):
        directory = Path(root) / mode
        real_reader = app._read_batch_csv
        def guarded(directory, name, nrows=None):
            assert "physical_contacts" not in name and "traceable_contacts" not in name and "contact_episodes" not in name
            return real_reader(directory, name, nrows)
        with patch.object(app, "_read_batch_csv", side_effect=guarded):
            data = app.load_csv(str(directory / "run_summary.csv"))
        kpis = app.extract_kpis(data)
        assert kpis["peak_sick"] == "3", kpis
        assert kpis["peak_day"] == "Day 0", kpis
        assert kpis["transmissions"] == "1", kpis
        assert data["CORE METRICS"].iloc[0]["healthy"] == 6
        origins = data["INFECTION ORIGINS"].set_index("origin")
        assert origins.loc["work", "count"] == 1
        assert len(contact_preview(directory)) == expected_rows
        stored = {key: frame.astype(str).to_dict("records") for key, frame in data.items()}
        app.render_dashboard(stored)
        print(f"PASS {mode}: C# CSV -> dashboard KPIs/charts + contact preview ({expected_rows} rows)")


if __name__ == "__main__":
    verify(sys.argv[1])
