"""Run-file discovery and display labels for the analytics dashboard.

This module intentionally uses only the Python standard library so its path and
manifest behavior can be tested without importing Dash, Pandas, or Plotly.
"""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any, Iterable, Mapping, Optional


RUN_FILE_PATTERN = "pandemic_run_*.csv"
RUN_MANIFEST_NAME = "run_manifest.json"


def _is_transient_part(part: str) -> bool:
    """Return whether a path component denotes unpublished run output."""
    normalized = part.strip().casefold()
    return (
        "__inprogress" in normalized
        or normalized in {
            ".inprogress",
            "inprogress",
            ".in-progress",
            "in-progress",
            ".tmp",
            "tmp",
            ".temp",
            "temp",
        }
        or normalized.endswith((".inprogress", ".in-progress", ".partial", ".tmp", ".temp"))
    )


def is_transient_run_path(path: Path) -> bool:
    """Return whether *path* lives in a temporary or in-progress location."""
    return any(_is_transient_part(part) for part in Path(path).parts)


def _modified_time_ns(path: Path) -> int:
    try:
        stat = path.stat()
        return getattr(stat, "st_mtime_ns", int(stat.st_mtime * 1_000_000_000))
    except OSError:
        return 0


def discover_run_csvs(data_dir: Path) -> list[Path]:
    """Recursively discover published rich run CSVs, newest first.

    Modification time is the primary key. The normalized path is an ascending
    tie-breaker so results remain deterministic when files share a timestamp.
    """
    root = Path(data_dir)
    if not root.exists() or not root.is_dir():
        return []

    files: Iterable[Path] = root.rglob(RUN_FILE_PATTERN)
    published = [
        path
        for path in files
        if path.is_file()
        and not is_transient_run_path(path.relative_to(root))
        and _has_publishable_manifest_status(path)
    ]
    return sorted(
        published,
        key=lambda path: (
            -_modified_time_ns(path),
            str(path).casefold(),
            str(path),
        ),
    )


def _has_publishable_manifest_status(run_path: Path) -> bool:
    """Accept manual runs, but require Completed when a sibling manifest exists."""
    manifest_path = Path(run_path).with_name(RUN_MANIFEST_NAME)
    if not manifest_path.exists():
        return True
    manifest = _read_manifest(manifest_path)
    status = _manifest_value(manifest, "status")
    return str(status or "").strip().casefold() == "completed"


def _normalized_key(value: Any) -> str:
    return "".join(character for character in str(value).casefold() if character.isalnum())


def _manifest_value(manifest: Mapping[str, Any], *names: str) -> Optional[Any]:
    wanted = {_normalized_key(name) for name in names}
    for key, value in manifest.items():
        if _normalized_key(key) in wanted and value not in (None, ""):
            return value
    return None


def _read_manifest(path: Path) -> Mapping[str, Any]:
    try:
        with path.open("r", encoding="utf-8-sig") as stream:
            value = json.load(stream)
        return value if isinstance(value, dict) else {}
    except (OSError, UnicodeError, ValueError, TypeError):
        return {}


def _relative_parts(path: Path, data_dir: Path) -> tuple[str, ...]:
    try:
        return path.relative_to(data_dir).parts
    except ValueError:
        return path.parts


def _batch_path_defaults(
    path: Path,
    data_dir: Path,
) -> tuple[Optional[str], Optional[str], Optional[str]]:
    """Infer readable identifiers from the documented Experiments layout."""
    parts = _relative_parts(path, data_dir)
    experiment_index = next(
        (index for index, part in enumerate(parts) if part.casefold() == "experiments"),
        None,
    )
    if experiment_index is None or len(parts) < experiment_index + 5:
        return None, None, None

    batch_name = parts[experiment_index + 1]
    scenario_name = parts[experiment_index + 2]
    run_name = parts[experiment_index + 3]
    return batch_name, scenario_name, run_name


def _run_label(value: Any) -> Optional[str]:
    if value in (None, ""):
        return None

    if isinstance(value, bool):
        return None

    try:
        number = int(value)
        return f"Run {number}"
    except (TypeError, ValueError):
        text = str(value).strip()

    if not text:
        return None
    if text.casefold().startswith("run"):
        suffix = text[3:].lstrip(" _-")
        return f"Run {suffix}" if suffix else "Run"
    return text


def run_display_label(path: Path, data_dir: Path) -> str:
    """Build a concise selector label, preferring the sibling run manifest."""
    run_path = Path(path)
    root = Path(data_dir)
    manifest = _read_manifest(run_path.with_name(RUN_MANIFEST_NAME))
    path_batch, path_scenario, path_run = _batch_path_defaults(run_path, root)

    batch_name = _manifest_value(manifest, "batch_name", "batch") or path_batch
    scenario_name = _manifest_value(manifest, "scenario_name", "scenario") or path_scenario
    run_number = _manifest_value(manifest, "run_number", "scenario_run_number", "run")
    if run_number in (None, ""):
        run_index = _manifest_value(manifest, "run_index", "scenario_run_index")
        try:
            run_number = int(run_index) + 1 if run_index not in (None, "") else None
        except (TypeError, ValueError):
            run_number = None
    run_number = run_number or path_run
    formatted_run = _run_label(run_number)

    if batch_name and scenario_name and formatted_run:
        return f"{batch_name} / {scenario_name} / {formatted_run}"

    relative_parts = _relative_parts(run_path, root)
    if len(relative_parts) == 1:
        return run_path.name
    return " / ".join(relative_parts)
