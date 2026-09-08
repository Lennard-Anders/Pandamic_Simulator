"""Deterministic scientific aggregation for completed TENUS batch runs.

Completed batch runs are accepted, and diagnostic ``run_summary.csv`` files from
invalid batch runs are also accepted when their manifest hashes validate.
Ordinary invalid, failed, temporary, or malformed runs remain excluded.
The module uses only the standard library so it can be tested without Dash.
"""

from __future__ import annotations

import csv
import hashlib
import json
import math
import statistics
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterable, Mapping, Optional
from functools import lru_cache

try:
    from .run_discovery import is_transient_run_path
except ImportError:
    from run_discovery import is_transient_run_path


SUMMARY_FILE_NAME = "run_summary.csv"
MANIFEST_FILE_NAME = "run_manifest.json"


def _normalized_key(value: Any) -> str:
    return "".join(character for character in str(value).casefold() if character.isalnum())


def _value(mapping: Mapping[str, Any], *names: str) -> Optional[Any]:
    wanted = {_normalized_key(name) for name in names}
    for key, value in mapping.items():
        if _normalized_key(key) in wanted and value not in (None, ""):
            return value
    return None


def _read_json(path: Path) -> Mapping[str, Any]:
    try:
        with path.open("r", encoding="utf-8-sig") as stream:
            value = json.load(stream)
        return value if isinstance(value, dict) else {}
    except (OSError, UnicodeError, ValueError, TypeError):
        return {}


def _read_summary(path: Path) -> Mapping[str, str]:
    try:
        with path.open("r", encoding="utf-8-sig", newline="") as stream:
            reader = csv.DictReader(stream)
            row = next(reader, None)
        return row if isinstance(row, dict) else {}
    except (OSError, UnicodeError, csv.Error, StopIteration):
        return {}


def _finite_number(value: Any) -> Optional[float]:
    try:
        number = float(value)
    except (TypeError, ValueError):
        return None
    return number if math.isfinite(number) else None


def _integer(value: Any, default: int = 0) -> int:
    number = _finite_number(value)
    return int(number) if number is not None else default


@dataclass(frozen=True)
class RunMetricRecord:
    path: Path
    batch_id: str
    scenario_id: str
    scenario_name: str
    run_number: int
    pair_id: int
    master_seed: int
    metrics: Mapping[str, float]
    preset_id: str = ""
    sensitivity_parameter: str = ""


@lru_cache(maxsize=2048)
def _file_digest(path: str, size: int, modified_ns: int) -> str:
    digest = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _manifest_files_valid(directory: Path, manifest: Mapping[str, Any]) -> bool:
    entries = _value(manifest, "output_files")
    extension_version = _integer(_value(manifest, "scientific_extensions_version"))
    if extension_version not in (0, 1):
        return False
    if entries is None:
        return extension_version == 0 and _integer(_value(manifest, "schema_version")) < 4
    if not isinstance(entries, list) or not entries:
        return False
    try:
        root = directory.resolve()
        names = set()
        for entry in entries:
            if not isinstance(entry, dict):
                return False
            relative = str(_value(entry, "relative_path") or "")
            file = (root / relative).resolve()
            if not file.is_relative_to(root) or file == root or relative in names:
                return False
            names.add(relative)
            stat = file.stat()
            length = _value(entry, "length_bytes")
            if length is None or stat.st_size != int(length):
                return False
            if _file_digest(str(file), stat.st_size, stat.st_mtime_ns).casefold() != str(_value(entry, "sha256") or "").casefold():
                return False
        required = {"run_summary.csv", "state_timeseries.csv", "transmission_events.csv", "physical_contacts.csv", "traceable_contacts.csv", "test_events.csv", "intervention_events.csv", "healthcare_timeseries.csv", "population_events.csv", "errors.json"}
        if extension_version == 1:
            required |= {"contact_network_summary.csv", "age_mixing_matrix.csv", "contact_degree_distribution.csv", "contact_duration_distribution.csv", "contacts_by_time_of_day.csv", "calibration_results.csv"}
        return (extension_version == 0 and _integer(_value(manifest, "schema_version")) < 4) or required <= names
    except (OSError, ValueError, TypeError):
        return False


def load_completed_run(path: Path) -> Optional[RunMetricRecord]:
    """Load one completed or validated diagnostic batch run."""
    candidate = Path(path)
    directory = candidate if candidate.is_dir() else candidate.parent
    diagnostic = candidate.name == SUMMARY_FILE_NAME
    # An OS Temp ancestor is a legitimate analysis root; inspect run markers rather than
    # rejecting all files underneath the user's temporary directory.
    marked_parts = [part for part in directory.parts if part.casefold() not in {"temp", "tmp"}]
    if not diagnostic and (is_transient_run_path(Path(*marked_parts)) or is_transient_run_path(Path(directory.name)) or any("__invalid" in part.casefold() for part in directory.parts)):
        return None
    manifest = _read_json(directory / MANIFEST_FILE_NAME)
    status = str(_value(manifest, "status") or "").strip().casefold()
    if status != "completed" and not (diagnostic and status == "invalid"):
        return None
    if not _manifest_files_valid(directory, manifest):
        return None

    summary = _read_summary(directory / SUMMARY_FILE_NAME)
    if not summary:
        return None

    metrics: dict[str, float] = {}
    for key, value in summary.items():
        number = _finite_number(value)
        if number is not None:
            metrics[str(key)] = number

    network_path = directory / "contact_network_summary.csv"
    if network_path.exists():
        try:
            days = {}
            with network_path.open(encoding="utf-8-sig", newline="") as stream:
                for row in csv.DictReader(stream):
                    days.setdefault(row["day"], row)  # global daily totals repeat on context rows
            person_days = sum(float(row["tracked_person_days"]) for row in days.values())
            events = sum(float(row["contact_events"]) for row in days.values())
            if person_days > 0:
                metrics["mean_contacts_per_person_day"] = 2 * events / person_days
        except (OSError, ValueError, KeyError, TypeError, csv.Error):
            return None

    scenario_id = str(_value(manifest, "scenario_id") or "").strip()
    scenario_name = str(_value(manifest, "scenario_name") or scenario_id).strip()
    if not scenario_id or not scenario_name or not metrics:
        return None

    scenario = _value(manifest, "scenario") or {}
    sensitivity = (_value(scenario, "sensitivity") or {}) if isinstance(scenario, dict) else {}
    return RunMetricRecord(
        path=candidate,
        batch_id=str(_value(manifest, "batch_id") or "").strip(),
        scenario_id=scenario_id,
        scenario_name=scenario_name,
        run_number=_integer(_value(manifest, "run_number")),
        pair_id=_integer(_value(manifest, "pair_id")),
        master_seed=_integer(_value(manifest, "master_seed")),
        metrics=metrics,
        preset_id=str(_value(scenario, "preset_id") or "") if isinstance(scenario, dict) else "",
        sensitivity_parameter=str(_value(sensitivity, "parameter") or "") if isinstance(sensitivity, dict) else "",
    )


def load_completed_runs(paths: Iterable[Path]) -> list[RunMetricRecord]:
    unique = {}
    for path in paths:
        candidate = Path(path)
        directory = candidate if candidate.is_dir() else candidate.parent
        unique.setdefault(directory.resolve(), candidate)
    records = [load_completed_run(path) for path in unique.values()]
    return [record for record in records if record is not None]


def percentile(values: Iterable[float], probability: float) -> float:
    """Linear-interpolation percentile equivalent to common scientific tools."""
    ordered = sorted(float(value) for value in values)
    if not ordered:
        raise ValueError("At least one value is required.")
    if probability < 0.0 or probability > 1.0:
        raise ValueError("Percentile probability must be between zero and one.")
    if len(ordered) == 1:
        return ordered[0]
    position = (len(ordered) - 1) * probability
    lower = int(math.floor(position))
    upper = int(math.ceil(position))
    if lower == upper:
        return ordered[lower]
    fraction = position - lower
    return ordered[lower] + ((ordered[upper] - ordered[lower]) * fraction)


def describe(values: Iterable[float]) -> Mapping[str, float]:
    ordered = sorted(float(value) for value in values if math.isfinite(float(value)))
    if not ordered:
        raise ValueError("At least one finite value is required.")
    p25 = percentile(ordered, 0.25)
    p75 = percentile(ordered, 0.75)
    return {
        "n": float(len(ordered)),
        "mean": statistics.fmean(ordered),
        "median": statistics.median(ordered),
        "standard_deviation": statistics.stdev(ordered) if len(ordered) > 1 else 0.0,
        "minimum": ordered[0],
        "maximum": ordered[-1],
        "p05": percentile(ordered, 0.05),
        "p25": p25,
        "p75": p75,
        "p95": percentile(ordered, 0.95),
        "iqr": p75 - p25,
    }


def aggregate_scenarios(records: Iterable[RunMetricRecord]) -> Mapping[str, Mapping[str, Any]]:
    """Return per-scenario descriptive statistics for every shared numeric metric."""
    grouped: dict[str, list[RunMetricRecord]] = {}
    for record in records:
        grouped.setdefault(record.scenario_id, []).append(record)

    result: dict[str, Mapping[str, Any]] = {}
    for scenario_id in sorted(grouped, key=lambda value: value.casefold()):
        runs = grouped[scenario_id]
        metric_names = sorted({name for run in runs for name in run.metrics})
        metric_stats = {
            name: describe(run.metrics[name] for run in runs if name in run.metrics)
            for name in metric_names
        }
        result[scenario_id] = {
            "scenario_name": runs[0].scenario_name,
            "run_count": len(runs),
            "preset_id": runs[0].preset_id,
            "sensitivity_parameter": runs[0].sensitivity_parameter,
            "metrics": metric_stats,
        }
    return result


def aggregate_paired_differences(
    records: Iterable[RunMetricRecord],
    reference_scenario_id: Optional[str] = None,
) -> Mapping[str, Mapping[str, Any]]:
    """Compare each scenario with the reference using matched positive pair IDs.

    Differences are ``scenario - reference`` and include every per-pair value as
    well as mean/median and the full descriptive-statistics block.
    """
    runs = list(records)
    scenario_ids = sorted({run.scenario_id for run in runs}, key=lambda value: value.casefold())
    if len(scenario_ids) < 2:
        return {}
    reference = reference_scenario_id or scenario_ids[0]
    if reference not in scenario_ids:
        raise ValueError("The paired reference scenario is not present.")

    by_scenario: dict[str, dict[tuple[str, int, int], RunMetricRecord]] = {}
    for run in runs:
        if run.pair_id > 0:
            key = (run.batch_id, run.pair_id, run.master_seed)
            target = by_scenario.setdefault(run.scenario_id, {})
            if key in target:
                raise ValueError("Duplicate paired run; refusing to silently overwrite a repetition.")
            target[key] = run
    reference_pairs = by_scenario.get(reference, {})
    result: dict[str, Mapping[str, Any]] = {}
    for scenario_id in scenario_ids:
        if scenario_id == reference:
            continue
        comparison_pairs = by_scenario.get(scenario_id, {})
        pair_ids = sorted(set(reference_pairs) & set(comparison_pairs))
        metrics: dict[str, Any] = {}
        for metric in sorted({name for pair in pair_ids for name in reference_pairs[pair].metrics}):
            differences = [
                {
                    "pair_id": pair[1],
                    "difference": comparison_pairs[pair].metrics[metric] - reference_pairs[pair].metrics[metric],
                }
                for pair in pair_ids
                if metric in reference_pairs[pair].metrics and metric in comparison_pairs[pair].metrics
            ]
            if differences:
                metrics[metric] = {
                    "pairs": differences,
                    "statistics": describe(item["difference"] for item in differences),
                }
        if metrics:
            result[scenario_id] = {
                "scenario_name": comparison_pairs[pair_ids[0]].scenario_name if pair_ids else scenario_id,
                "reference_scenario_id": reference,
                "reference_scenario_name": reference_pairs[pair_ids[0]].scenario_name if pair_ids else reference,
                "metrics": metrics,
            }
    return result
