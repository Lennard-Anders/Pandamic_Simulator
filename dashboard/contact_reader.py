"""Explicit, bounded-memory contact analysis for legacy and schema-2 exports.

No caller needs to load contacts for scenario comparisons. Iteration opens one
partition at a time, and gzip is decompressed incrementally by the standard library.
"""

from __future__ import annotations

import csv
import gzip
import json
from pathlib import Path
from typing import Iterator

try:
    from .run_discovery import _manifest_value
except ImportError:
    from run_discovery import _manifest_value


def contact_paths(directory: Path) -> list[Path]:
    root = Path(directory).resolve()
    manifest_path = root / "run_manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8-sig")) if manifest_path.exists() else {}
    status = _manifest_value(manifest, "status")
    if status is not None and status != "Completed":
        raise ValueError("Individual contact analysis requires a completed run")
    version = int(_manifest_value(manifest, "scientific_export_schema_version") or 0)
    if version not in (0, 1, 2):
        raise ValueError("Unsupported scientific contact schema")
    if version == 2:
        entries = _manifest_value(manifest, "contact_files")
        if not isinstance(entries, list):
            raise ValueError("Contact file inventory missing")
        names = [str(_manifest_value(entry, "relative_path") or "") for entry in entries]
    else:
        names = ["physical_contacts.csv"] if (root / "physical_contacts.csv").exists() else []
    result = []
    for name in names:
        path = (root / name).resolve()
        if not path.is_relative_to(root) or not name.endswith((".csv", ".csv.gz")) or not path.is_file():
            raise ValueError("Invalid contact file path")
        if path in result:
            raise ValueError("Duplicate contact partition")
        result.append(path)
    return sorted(result)


def iter_contacts(directory: Path, *, traceable_only: bool = False) -> Iterator[dict[str, str]]:
    """Yield authoritative step/episode rows, preserving the on-disk schema.

    In Standard mode duration is an episode duration, in FullRaw/legacy it is a
    step duration. SummaryOnly yields no individual rows. No duplicate traceability
    file is needed: flags on the authoritative row define the filtered dataset.
    """
    for path in contact_paths(directory):
        opener = gzip.open if path.name.endswith(".gz") else open
        with opener(path, "rt", encoding="utf-8-sig", newline="") as stream:
            for row in csv.DictReader(stream):
                if not traceable_only or row.get("traceable_by_app") == "1" or row.get("traceable_by_manual") == "1":
                    yield row


def iter_contact_chunks(directory: Path, *, chunk_size: int = 10000, traceable_only: bool = False) -> Iterator[list[dict[str, str]]]:
    if chunk_size < 1:
        raise ValueError("chunk_size must be positive")
    chunk = []
    for row in iter_contacts(directory, traceable_only=traceable_only):
        chunk.append(row)
        if len(chunk) == chunk_size:
            yield chunk
            chunk = []
    if chunk:
        yield chunk


def contact_preview(directory: Path, *, limit: int = 100) -> list[dict[str, str]]:
    """Read only a bounded prefix, never scan a large export to count its rows."""
    if not 1 <= limit <= 1000:
        raise ValueError("Preview limit must be between 1 and 1000")
    rows = iter_contacts(directory)
    try:
        result = []
        for row in rows:
            result.append(row)
            if len(result) == limit:
                break
        return result
    finally:
        rows.close()
