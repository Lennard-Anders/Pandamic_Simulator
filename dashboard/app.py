"""
Pandemic Simulator ??? Analytics Dashboard
Run:  python app.py
Then open http://localhost:8050
"""

import os
import io
import csv
import base64
import math
from pathlib import Path

import pandas as pd
import numpy as np
import plotly.graph_objects as go
from plotly.subplots import make_subplots

import dash
from dash import dcc, html, Input, Output, State, callback_context
import dash_bootstrap_components as dbc

try:
    from .run_discovery import discover_run_csvs, run_display_label
    from .run_aggregation import (
        aggregate_paired_differences,
        aggregate_scenarios,
        load_completed_runs,
    )
except ImportError:
    from run_discovery import discover_run_csvs, run_display_label
    from run_aggregation import (
        aggregate_paired_differences,
        aggregate_scenarios,
        load_completed_runs,
    )

# ?????? Default data directory ????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????
DEFAULT_DATA_DIR = Path(
    os.environ.get(
        "PANDEMIC_DATA_DIR",
        Path.home()
        / "AppData/Local/Colossal Order/Cities_Skylines/Addons/Mods/RealTime/Pandemic Data",
    )
)

# ?????? Colour palette ????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????
C = {
    "bg":        "#0d1117",
    "surface":   "#161b22",
    "surface2":  "#21262d",
    "surface3":  "#2d333b",
    "border":    "#30363d",
    "text":      "#e6edf3",
    "muted":     "#8b949e",
    "accent":    "#1f6feb",
    "healthy":   "#58a6ff",
    "exposed":   "#ffa657",
    "sick":      "#f85149",
    "recovered": "#3fb950",
    "dead":      "#bc8cff",
    "hospital":  "#ffa657",
    "ambulance": "#ff7b72",
    "indoor":    "#f0883e",
    "outdoor":   "#79c0ff",
    "vehicle":   "#d2a8ff",
    "policy_masks":    "#d29922",
    "policy_lock":     "#f85149",
}

_AXIS = dict(gridcolor=C["border"], zerolinecolor=C["border"], color=C["muted"])
LAYOUT = dict(
    paper_bgcolor=C["surface"],
    plot_bgcolor=C["surface"],
    font=dict(color=C["text"], family="'Segoe UI', Arial, sans-serif", size=12),
    title_font=dict(size=13, color=C["text"]),
    legend=dict(bgcolor=C["surface2"], bordercolor=C["border"], borderwidth=1,
                font=dict(size=11)),
    margin=dict(l=50, r=24, t=44, b=40),
    hoverlabel=dict(bgcolor=C["surface2"], bordercolor=C["border"],
                    font=dict(color=C["text"])),
)
# Default axis style applied per-chart (not in base LAYOUT to avoid duplicate-kwarg)
_XY = dict(xaxis=_AXIS, yaxis=_AXIS)

# ?????? Numeric columns by section ????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????
NUMERIC_COLS = {
    "CORE METRICS": [
        "tracked_population", "healthy", "exposed", "sick", "recovered", "dead",
        "delta_sick", "delta_recovered", "delta_dead", "quarantine_citizens",
        "positive_tests", "tested_citizens", "contacts_tracked_citizens",
        "contacts_tracked_pairs", "contacts_recorded_total",
        "transmissions_total", "transmissions_indoor", "transmissions_outdoor",
        "transmissions_vehicle", "hotspot_buildings", "hub_buildings",
        "hospital_usage_pct", "ambulance_usage_pct", "observation_count",
    ],
    "AGE GROUPS":             ["infected_count", "population_count", "share_of_infections_pct",
                                 "infection_prevalence_within_age_group_pct", "infected_percent"],
    "LOCKDOWN FAMILIES":      ["is_closed", "metric_value_pct", "close_threshold_pct",
                                 "reopen_threshold_pct", "minimum_closure_days", "cooldown_days",
                                 "manual_closed", "infected_pct", "threshold_pct"],
    "INFECTION ORIGINS":      ["count", "percent"],
    "DISTRICT INFECTION RATES": ["district_id", "infected_residents", "resident_count", "infected_percent"],
    "TOP SPREADERS":          ["rank", "infection_count", "is_superspreader"],
    "TOP ORIGIN LOCATIONS":   ["rank", "infection_count", "is_superspreader"],
    "SEIRD TIME SERIES":      ["pandemic_day", "susceptible", "healthy", "exposed", "infectious",
                               "post_infectious_ill", "symptomatic", "sick", "recovered", "dead",
                               "total", "new_exposures", "delta_sick", "delta_dead"],
    "HEALTHCARE TIME SERIES": ["pandemic_day", "hospital_usage_pct", "ambulance_usage_pct"],
    "PEAK STATISTICS":        ["peak_sick_count", "peak_sick_day", "final_exposed",
                               "final_sick", "final_recovered", "final_dead", "total_tracked",
                               "attack_rate_pct", "case_fatality_rate_pct",
                               "resolved_case_fatality_ratio_pct"],
    "POLICY TIMELINE":        ["pandemic_day", "enabled"],
    "POLICY STATE":           ["enabled"],
    "INFECTION ORIGINS TIME SERIES": [
        "pandemic_day", "home", "work", "school",
        "healthcare", "commercial", "transit", "outdoor", "other",
    ],
    "CITIZEN LOCATIONS TIME SERIES": [
        "pandemic_day", "game_hour", "at_home", "at_work", "visiting", "in_transit", "on_foot",
    ],
}

# ?????? CSV parser ????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????

def _make_df(headers: list, rows: list) -> pd.DataFrame:
    parsed = []
    for row in rows:
        fields = next(csv.reader([row]))
        if len(fields) < len(headers):
            fields += [""] * (len(headers) - len(fields))
        parsed.append(fields[: len(headers)])
    return pd.DataFrame(parsed, columns=headers) if parsed else pd.DataFrame(columns=headers)


def parse_pandemic_csv(text: str) -> dict:
    sections: dict = {}
    current = None
    headers = None
    rows: list = []
    for raw in text.splitlines():
        line = raw.strip()
        if not line:
            continue
        if line.startswith("[") and line.endswith("]"):
            if current and headers is not None:
                sections[current] = _make_df(headers, rows)
            current = line[1:-1]
            headers = None
            rows = []
        elif current is not None:
            if headers is None:
                headers = [h.strip() for h in line.split(",")]
            else:
                rows.append(line)
    if current and headers is not None:
        sections[current] = _make_df(headers, rows)
    return sections


def _coerce(data: dict) -> dict:
    # Some runs export [SEIRD TIME SERIES], others [SIRD TIME SERIES]; normalise both.
    if "SEIRD TIME SERIES" in data and "SIRD TIME SERIES" not in data:
        data["SIRD TIME SERIES"] = data["SEIRD TIME SERIES"]
    elif "SIRD TIME SERIES" in data and "SEIRD TIME SERIES" not in data:
        data["SEIRD TIME SERIES"] = data["SIRD TIME SERIES"]

    for section, cols in NUMERIC_COLS.items():
        if section in data:
            df = data[section]
            for col in cols:
                if col in df.columns:
                    df[col] = pd.to_numeric(df[col], errors="coerce")
    for s in ("SEIRD TIME SERIES", "HEALTHCARE TIME SERIES", "POLICY TIMELINE", "INFECTION ORIGINS TIME SERIES", "CITIZEN LOCATIONS TIME SERIES"):
        if s in data and "sim_time" in data[s].columns:
            data[s]["sim_time"] = pd.to_datetime(
                data[s]["sim_time"], errors="coerce", utc=True
            )
    return data


def load_csv(path_or_text: str, is_text: bool = False) -> dict:
    if is_text:
        text = path_or_text
    else:
        with open(path_or_text, encoding="utf-8") as fh:
            text = fh.read()
    return _coerce(parse_pandemic_csv(text))


def _settings_dict(data: dict) -> dict:
    settings = data.get("PANDEMIC SETTINGS", pd.DataFrame())
    if settings.empty or "parameter" not in settings.columns or "value" not in settings.columns:
        return {}
    return {
        str(row["parameter"]).strip(): str(row["value"]).strip()
        for _, row in settings.iterrows()
    }


def list_csvs() -> list:
    return discover_run_csvs(DEFAULT_DATA_DIR)


def run_options(files: list) -> list:
    return [
        {"label": run_display_label(path, DEFAULT_DATA_DIR), "value": str(path)}
        for path in files
    ]


# ?????? KPI extraction ????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????

def extract_kpis(data: dict) -> dict:
    kpis = {k: "???" for k in [
        "attack_rate", "cfr", "peak_sick", "peak_day",
        "total_dead", "total_recovered", "total_pop",
        "run_duration", "game_days", "lifecycle", "transmissions",
    ]}
    meta = data.get("RUN METADATA", pd.DataFrame())
    if not meta.empty:
        r = meta.iloc[0]
        elapsed = r.get("elapsed_wall_seconds", "")
        if elapsed and elapsed not in ("", "unknown"):
            try:
                secs = float(elapsed)
                h, m, s = int(secs // 3600), int((secs % 3600) // 60), int(secs % 60)
                kpis["run_duration"] = f"{h:02d}h {m:02d}m {s:02d}s"
            except Exception:
                pass
        kpis["game_days"] = str(r.get("pandemic_day", "???"))
        kpis["lifecycle"] = str(r.get("lifecycle_state", "???"))

    peak = data.get("PEAK STATISTICS", pd.DataFrame())
    if not peak.empty:
        r = peak.iloc[0]
        def _pct(k):  return f"{float(r.get(k, 0)):.1f}%"
        def _int(k):  return f"{int(float(r.get(k, 0))):,}"
        kpis["attack_rate"]     = _pct("attack_rate_pct")
        cfr_column = "resolved_case_fatality_ratio_pct" \
            if "resolved_case_fatality_ratio_pct" in peak.columns \
            else "case_fatality_rate_pct"
        kpis["cfr"]             = _pct(cfr_column)
        kpis["peak_sick"]       = _int("peak_sick_count")
        kpis["peak_day"]        = f"Day {int(float(r.get('peak_sick_day', 0)))}"
        kpis["total_dead"]      = _int("final_dead")
        kpis["total_recovered"] = _int("final_recovered")
        kpis["total_pop"]       = _int("total_tracked")

    core = data.get("CORE METRICS", pd.DataFrame())
    if not core.empty:
        kpis["transmissions"] = f"{int(float(core.iloc[0].get('transmissions_total', 0))):,}"

    return kpis


# ?????? Chart helpers ???????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????

def _layout(**overrides) -> dict:
    """Return the base LAYOUT dict merged with any per-chart overrides.
    NOTE: never injects xaxis/yaxis ??? callers that need axis styling pass them
    explicitly so there is no duplicate-kwarg conflict in update_layout()."""
    base = {**LAYOUT}
    base.update(overrides)
    return base


def fig_epidemic_curve(data: dict) -> go.Figure:
    seird  = data.get("SEIRD TIME SERIES", pd.DataFrame())
    policy = data.get("POLICY TIMELINE",   pd.DataFrame())
    fig    = go.Figure()

    if seird.empty:
        fig.update_layout(title="No SEIRD data", **_layout())
        return fig

    x = seird["pandemic_day"]
    susceptible_col = "susceptible" if "susceptible" in seird.columns else "healthy"
    infectious_col = "infectious" if "infectious" in seird.columns else "sick"
    fig.add_trace(go.Scatter(
        x=x, y=seird[susceptible_col], name="Susceptible (S)",
        line=dict(color=C["healthy"], width=1.8),
        fill="tozeroy", fillcolor="rgba(88,166,255,0.07)",
        hovertemplate="Day %{x}: %{y:,.0f} Susceptible<extra></extra>",
    ))
    # SEIR: show Exposed (E) — latent / not yet infectious
    if "exposed" in seird.columns:
        fig.add_trace(go.Scatter(
            x=x, y=seird["exposed"].fillna(0), name="Exposed (E)",
            line=dict(color=C["exposed"], width=2, dash="dot"),
            fill="tozeroy", fillcolor="rgba(255,166,87,0.10)",
            hovertemplate="Day %{x}: %{y:,.0f} Exposed (latent)<extra></extra>",
        ))
    fig.add_trace(go.Scatter(
        x=x, y=seird[infectious_col], name="Infectious (I)",
        line=dict(color=C["sick"], width=2.5),
        fill="tozeroy", fillcolor="rgba(248,81,73,0.14)",
        hovertemplate="Day %{x}: %{y:,.0f} Infectious<extra></extra>",
    ))
    if "post_infectious_ill" in seird.columns:
        fig.add_trace(go.Scatter(
            x=x, y=seird["post_infectious_ill"].fillna(0), name="Post-infectious ill",
            line=dict(color=C["hospital"], width=1.7, dash="dash"),
            hovertemplate="Day %{x}: %{y:,.0f} Post-infectious ill<extra></extra>",
        ))
    fig.add_trace(go.Scatter(
        x=x, y=seird["recovered"], name="Recovered (R)",
        line=dict(color=C["recovered"], width=2),
        hovertemplate="Day %{x}: %{y:,.0f} Recovered<extra></extra>",
    ))
    fig.add_trace(go.Scatter(
        x=x, y=seird["dead"], name="Dead (D)",
        line=dict(color=C["dead"], width=2),
        hovertemplate="Day %{x}: %{y:,.0f} Dead<extra></extra>",
    ))

    if not policy.empty:
        for _, row in policy.iterrows():
            day = row.get("pandemic_day")
            if pd.isna(day):
                continue
            ptype = str(row.get("policy_type", "")).lower()
            col   = C["policy_lock"] if "lockdown" in ptype else C["policy_masks"]
            label = str(row.get("policy_type", "")) + (" ???" if row.get("enabled", 0) else " ???")
            fig.add_vline(
                x=float(day), line_dash="dash", line_color=col, line_width=1.2,
                annotation_text=label, annotation_font_color=col,
                annotation_font_size=10, annotation_position="top right",
            )

    fig.update_layout(
        title="Epidemic Curve (SEIRD) with Policy Events",
        xaxis=dict(**_AXIS, title="Pandemic Day"),
        yaxis=dict(**_AXIS, title="Citizens"),
        **_layout(),
    )
    return fig


def fig_delta(data: dict) -> go.Figure:
    seird = data.get("SEIRD TIME SERIES", pd.DataFrame())
    fig  = go.Figure()
    if seird.empty or "delta_sick" not in seird.columns:
        fig.update_layout(title="No delta data", **_layout())
        return fig
    x  = seird["pandemic_day"]
    ds = seird["delta_sick"].fillna(0)
    fig.add_trace(go.Bar(
        x=x, y=ds.clip(lower=0), name="Infectious Increase",
        marker_color=C["sick"], opacity=0.85,
        hovertemplate="Day %{x}: +%{y:,.0f}<extra></extra>",
    ))
    fig.add_trace(go.Bar(
        x=x, y=ds.clip(upper=0), name="Infectious Decrease",
        marker_color=C["recovered"], opacity=0.7,
        hovertemplate="Day %{x}: %{y:,.0f}<extra></extra>",
    ))
    fig.update_layout(
        title="Daily Net Infectious Change",
        barmode="relative",
        xaxis=dict(**_AXIS, title="Pandemic Day"),
        yaxis=dict(**_AXIS, title="Delta Citizens"),
        **_layout(),
    )
    return fig


def fig_healthcare(data: dict) -> go.Figure:
    hc    = data.get("HEALTHCARE TIME SERIES", pd.DataFrame())
    seird = data.get("SEIRD TIME SERIES",      pd.DataFrame())
    fig  = make_subplots(specs=[[{"secondary_y": True}]])

    if not hc.empty:
        xh = hc["pandemic_day"]
        fig.add_trace(go.Scatter(
            x=xh, y=hc["hospital_usage_pct"],  name="Hospital %",
            line=dict(color=C["hospital"],  width=2),
            hovertemplate="Day %{x}: Hospital %{y:.1f}%<extra></extra>",
        ), secondary_y=False)
        fig.add_trace(go.Scatter(
            x=xh, y=hc["ambulance_usage_pct"], name="Ambulance %",
            line=dict(color=C["ambulance"], width=2),
            hovertemplate="Day %{x}: Ambulance %{y:.1f}%<extra></extra>",
        ), secondary_y=False)
        cfg = _settings_dict(data)
        for key, label, color in (
            ("HealthcareWarningThresholdPercent", "Configured warning threshold", "rgba(240,173,78,0.50)"),
            ("HealthcareCriticalThresholdPercent", "Configured critical threshold", "rgba(248,81,73,0.50)"),
        ):
            try:
                threshold = float(cfg[key])
            except (KeyError, TypeError, ValueError):
                continue
            fig.add_hline(y=threshold, line_dash="dot", line_color=color,
                          annotation_text=f"{label}: {threshold:g}%", secondary_y=False,
                          annotation_font_color=color, annotation_font_size=10)

    if not seird.empty:
        infectious_col = "infectious" if "infectious" in seird.columns else "sick"
        fig.add_trace(go.Scatter(
            x=seird["pandemic_day"], y=seird[infectious_col],
            name="Infectious (I, ref)", line=dict(color=C["sick"], width=1.5, dash="dot"),
            opacity=0.45,
            hovertemplate="Day %{x}: %{y:,.0f} Infectious<extra></extra>",
        ), secondary_y=True)

    fig.update_layout(title="Healthcare Capacity vs Infectious (I)", **_layout())
    fig.update_yaxes(title_text="Usage %", secondary_y=False, **_AXIS)
    fig.update_yaxes(title_text="Infectious Citizens", secondary_y=True, **_AXIS)
    fig.update_xaxes(title_text="Pandemic Day", **_AXIS)
    return fig


def fig_seird_donut(data: dict) -> go.Figure:
    peak = data.get("PEAK STATISTICS", pd.DataFrame())
    fig  = go.Figure()
    if not peak.empty:
        r       = peak.iloc[0]
        total   = float(r.get("total_tracked", 0))
        exposed = float(r.get("final_exposed", 0))
        sick    = float(r.get("final_sick", 0))
        rec     = float(r.get("final_recovered", 0))
        dead    = float(r.get("final_dead", 0))
        healthy = max(0.0, total - exposed - sick - rec - dead)
        vals    = [healthy, exposed, sick, rec, dead]
        labels  = ["Healthy (S)", "Exposed (E)", "Infectious (I)", "Recovered (R)", "Dead (D)"]
    else:
        core = data.get("CORE METRICS", pd.DataFrame())
        if core.empty:
            fig.update_layout(title="No data", **_layout())
            return fig
        r    = core.iloc[0]
        vals = [float(r.get(k, 0)) for k in ("healthy", "exposed", "sick", "recovered", "dead")]
        labels = ["Healthy (S)", "Exposed (E)", "Infectious (I)", "Recovered (R)", "Dead (D)"]

    fig.add_trace(go.Pie(
        labels=labels, values=vals,
        marker_colors=[C["healthy"], C["exposed"], C["sick"], C["recovered"], C["dead"]],
        hole=0.60, textinfo="percent",
        hovertemplate="%{label}: %{value:,.0f}<extra></extra>",
        textfont=dict(size=11),
    ))
    fig.update_layout(title="Final SEIRD Population State", **_layout())
    return fig


def fig_transmission_donut(data: dict) -> go.Figure:
    core = data.get("CORE METRICS", pd.DataFrame())
    fig  = go.Figure()
    if core.empty:
        fig.update_layout(title="No data", **_layout())
        return fig
    r    = core.iloc[0]
    vals = [float(r.get(k, 0)) for k in
            ("transmissions_indoor", "transmissions_outdoor", "transmissions_vehicle")]
    fig.add_trace(go.Pie(
        labels=["Indoor", "Outdoor", "Vehicle"], values=vals,
        marker_colors=[C["indoor"], C["outdoor"], C["vehicle"]],
        hole=0.60, textinfo="percent+label",
        hovertemplate="%{label}: %{value:,.0f} transmissions<extra></extra>",
        textfont=dict(size=11),
    ))
    fig.update_layout(title="Transmission Routes", **_layout())
    return fig


def fig_age(data: dict) -> go.Figure:
    ages = data.get("AGE GROUPS", pd.DataFrame())
    fig  = go.Figure()
    if ages.empty:
        fig.update_layout(title="No age data", **_layout())
        return fig
    prevalence_col = (
        "infection_prevalence_within_age_group_pct"
        if "infection_prevalence_within_age_group_pct" in ages.columns
        else "infected_percent"
    )
    share_col = (
        "share_of_infections_pct"
        if "share_of_infections_pct" in ages.columns
        else prevalence_col
    )
    fig.add_trace(go.Bar(
        x=ages["infected_count"], y=ages["age_group"],
        orientation="h",
        marker=dict(
            color=ages[prevalence_col],
            colorscale=[[0, "rgba(248,81,73,0.35)"], [1, C["sick"]]],
            showscale=False,
        ),
        text=[
            f"{float(prevalence):.1f}% within age · {float(share):.1f}% of infections"
            for prevalence, share in zip(ages[prevalence_col], ages[share_col])
        ],
        textposition="outside",
        hovertemplate="%{y}: %{x:,.0f} ever infected<extra></extra>",
    ))
    fig.update_layout(
        title="Ever Infected by Age Group (prevalence and infection share separated)",
        xaxis=dict(**_AXIS, title="Infected Count"),
        yaxis=dict(**_AXIS),
        **_layout(),
    )
    return fig


def fig_origins(data: dict) -> go.Figure:
    origins = data.get("INFECTION ORIGINS", pd.DataFrame())
    fig     = go.Figure()
    if origins.empty:
        fig.update_layout(title="No origin data", **_layout())
        return fig
    origins = origins.copy()
    origins["count"]   = pd.to_numeric(origins["count"],   errors="coerce").fillna(0)
    origins["percent"] = pd.to_numeric(origins["percent"], errors="coerce").fillna(0)
    origins = origins.sort_values("count", ascending=True).tail(14)
    fig.add_trace(go.Bar(
        x=origins["count"], y=origins["origin"],
        orientation="h",
        marker=dict(
            color=origins["count"],
            colorscale=[[0, "rgba(240,136,62,0.3)"], [1, C["indoor"]]],
            showscale=False,
        ),
        text=origins["percent"].apply(lambda v: f"{float(v):.1f}%"),
        textposition="outside",
        hovertemplate="%{y}: %{x:,.0f} infections<extra></extra>",
    ))
    fig.update_layout(
        title="Infection Origins",
        xaxis=dict(**_AXIS, title="Infections"),
        yaxis=dict(**_AXIS),
        height=420,
        **_layout(),
    )
    return fig


def fig_lockdown(data: dict) -> go.Figure:
    ld  = data.get("LOCKDOWN FAMILIES", pd.DataFrame())
    fig = go.Figure()
    if ld.empty:
        fig.update_layout(title="No lockdown data", **_layout())
        return fig
    metric_col = "metric_value_pct" if "metric_value_pct" in ld.columns else "infected_pct"
    close_col = "close_threshold_pct" if "close_threshold_pct" in ld.columns else "threshold_pct"
    closed  = ld["is_closed"].fillna(0).astype(int)
    colors  = [C["sick"] if c == 1 else C["accent"] for c in closed]
    fig.add_trace(go.Bar(
        x=ld["family"], y=ld[metric_col],
        name="Policy metric %",
        marker_color=colors,
        hovertemplate="<b>%{x}</b><br>Infected: %{y:.1f}%<extra></extra>",
    ))
    fig.add_trace(go.Scatter(
        x=ld["family"], y=ld[close_col],
        name="Auto-close threshold",
        mode="markers",
        marker=dict(
            symbol="line-ew", size=22, color=C["policy_masks"],
            line=dict(width=3, color=C["policy_masks"]),
        ),
        hovertemplate="%{x} threshold: %{y:.1f}%<extra></extra>",
    ))
    if "reopen_threshold_pct" in ld.columns:
        fig.add_trace(go.Scatter(
            x=ld["family"], y=ld["reopen_threshold_pct"],
            name="Auto-reopen threshold",
            mode="markers",
            marker=dict(symbol="line-ew", size=18, color=C["recovered"],
                        line=dict(width=3, color=C["recovered"])),
            hovertemplate="%{x} reopen threshold: %{y:.1f}%<extra></extra>",
        ))
    fig.update_layout(
        title="Lockdown Families ??? Infected % vs Threshold  (red = closed)",
        xaxis=dict(**_AXIS, title="", tickangle=-18),
        yaxis=dict(**_AXIS, title="Infected %"),
        **_layout(),
    )
    return fig


def fig_districts(data: dict) -> go.Figure:
    dist = data.get("DISTRICT INFECTION RATES", pd.DataFrame())
    fig  = go.Figure()
    if dist.empty:
        fig.update_layout(title="No district data ??? no user districts defined", **_layout())
        return fig
    dist = dist.copy()
    dist["infected_percent"] = pd.to_numeric(dist["infected_percent"], errors="coerce").fillna(0)
    dist = dist.sort_values("infected_percent", ascending=False)
    fig.add_trace(go.Bar(
        x=dist["district_name"], y=dist["infected_percent"],
        marker=dict(
            color=dist["infected_percent"],
            colorscale="Reds",
            showscale=True,
            colorbar=dict(title="%", tickfont=dict(color=C["text"])),
        ),
        hovertemplate="<b>%{x}</b><br>%{y:.1f}% infected<extra></extra>",
    ))
    fig.update_layout(
        title="District Infection Rates",
        xaxis=dict(**_AXIS, title="", tickangle=-18),
        yaxis=dict(**_AXIS, title="Infected %"),
        **_layout(),
    )
    return fig


def fig_spreaders(data: dict) -> go.Figure:
    sp  = data.get("TOP SPREADERS", pd.DataFrame())
    fig = go.Figure()
    if sp.empty:
        fig.update_layout(title="No spreader data", **_layout())
        return fig
    sp = sp.copy()
    sp["infection_count"]  = pd.to_numeric(sp["infection_count"],  errors="coerce").fillna(0)
    sp["is_superspreader"] = pd.to_numeric(sp["is_superspreader"], errors="coerce").fillna(0).astype(int)
    colors = [C["sick"] if v == 1 else C["accent"] for v in sp["is_superspreader"]]
    fig.add_trace(go.Bar(
        x=sp["infection_count"], y=sp["label"],
        orientation="h",
        marker_color=colors,
        text=["??? Superspreader" if v == 1 else "" for v in sp["is_superspreader"]],
        textposition="outside",
        textfont=dict(color=C["sick"], size=10),
        hovertemplate="%{y}: %{x:,.0f} infections caused<extra></extra>",
    ))
    fig.update_layout(
        title="Top Spreaders  (red = superspreader)",
        xaxis=dict(**_AXIS, title="Infections Caused"),
        yaxis=dict(**_AXIS),
        **_layout(),
    )
    return fig


def fig_locations(data: dict) -> go.Figure:
    loc = data.get("TOP ORIGIN LOCATIONS", pd.DataFrame())
    fig = go.Figure()
    if loc.empty:
        fig.update_layout(title="No location data", **_layout())
        return fig
    loc = loc.copy()
    loc["infection_count"]  = pd.to_numeric(loc["infection_count"],  errors="coerce").fillna(0)
    loc["is_superspreader"] = pd.to_numeric(loc["is_superspreader"], errors="coerce").fillna(0).astype(int)
    colors = [C["sick"] if v == 1 else C["indoor"] for v in loc["is_superspreader"]]
    fig.add_trace(go.Bar(
        x=loc["infection_count"], y=loc["label"],
        orientation="h",
        marker_color=colors,
        hovertemplate="%{y}: %{x:,.0f} infections originated<extra></extra>",
    ))
    fig.update_layout(
        title="Top Origin Locations  (red = hotspot)",
        xaxis=dict(**_AXIS, title="Infections Originated"),
        yaxis=dict(**_AXIS),
        **_layout(),
    )
    return fig


def fig_movement_origins(data: dict) -> go.Figure:
    """Stacked area: where new infections happen each observation, with policy markers."""
    orig_ts = data.get("INFECTION ORIGINS TIME SERIES", pd.DataFrame())
    fig = go.Figure()
    if orig_ts.empty:
        fig.update_layout(
            title="No movement origin data  (run a new simulation to capture)",
            **_layout()
        )
        return fig

    orig_ts = orig_ts.sort_values("pandemic_day").reset_index(drop=True)

    ORIGIN_COLS = [
        ("home",       "Home",         "rgba(224,92,92,0.75)"),
        ("work",       "Workplace",    "rgba(91,141,232,0.75)"),
        ("school",     "School",       "rgba(240,192,64,0.75)"),
        ("commercial", "Commercial",   "rgba(76,175,135,0.75)"),
        ("transit",    "Transit",      "rgba(155,89,182,0.75)"),
        ("healthcare", "Healthcare",   "rgba(26,188,156,0.75)"),
        ("outdoor",    "Outdoor",      "rgba(232,148,78,0.75)"),
        ("other",      "Other / Seed", "rgba(127,140,141,0.60)"),
    ]

    for col, label, color in ORIGIN_COLS:
        if col not in orig_ts.columns:
            continue
        fig.add_trace(go.Scatter(
            x=orig_ts["pandemic_day"],
            y=orig_ts[col].fillna(0),
            name=label,
            stackgroup="one",
            line=dict(width=0.5, color=color),
            fillcolor=color,
            hovertemplate=f"{label}: %{{y}}<extra></extra>",
        ))

    # Policy event markers
    policy_tl = data.get("POLICY TIMELINE", pd.DataFrame())
    if not policy_tl.empty and "pandemic_day" in policy_tl.columns:
        seen_days: set = set()
        for _, row in policy_tl.iterrows():
            try:
                day = float(row["pandemic_day"])
            except (ValueError, TypeError):
                continue
            if day in seen_days:
                continue
            seen_days.add(day)
            enabled = str(row.get("enabled", "")).strip().lower() in ("true", "1", "yes")
            line_color = C["sick"] if enabled else C["muted"]
            label_text = str(row.get("short_label", row.get("policy_type", "")))[:14]
            fig.add_vline(
                x=day, line_dash="dot", line_color=line_color, line_width=1,
                annotation_text=label_text,
                annotation_font_color=line_color,
                annotation_font_size=9,
                annotation_textangle=-90,
            )

    fig.update_layout(
        title="Where Infections Happen Over Time  (with policy event markers)",
        xaxis=dict(**_AXIS, title="Pandemic Day"),
        yaxis=dict(**_AXIS, title="New Infections per Observation"),
        **_layout(legend=dict(
            bgcolor=C["surface2"], bordercolor=C["border"],
            font=dict(color=C["text"], size=10),
            orientation="h", yanchor="bottom", y=1.02, x=0,
        )),
    )
    return fig


def fig_citizen_daily_rhythm(data: dict) -> go.Figure:
    """Stacked area showing where citizens are at each hour of the day (averaged across simulation)."""
    loc_ts = data.get("CITIZEN LOCATIONS TIME SERIES", pd.DataFrame())
    fig = go.Figure()
    if loc_ts.empty or "game_hour" not in loc_ts.columns:
        fig.update_layout(
            title="No citizen location data  (run a new simulation to capture)",
            **_layout()
        )
        return fig

    loc_ts = loc_ts.copy()
    # Average counts per game hour across the whole simulation
    hour_avg = (
        loc_ts.groupby("game_hour")[["at_home", "at_work", "visiting", "in_transit", "on_foot"]]
        .mean()
        .reset_index()
        .sort_values("game_hour")
    )

    LOC_COLS = [
        ("at_home",    "Home",        "rgba(224,92,92,0.80)"),
        ("at_work",    "Work",        "rgba(91,141,232,0.80)"),
        ("visiting",   "Visiting",    "rgba(76,175,135,0.80)"),
        ("in_transit", "In Transit",  "rgba(155,89,182,0.80)"),
        ("on_foot",    "On Foot",     "rgba(232,148,78,0.80)"),
    ]

    for col, label, color in LOC_COLS:
        if col not in hour_avg.columns:
            continue
        fig.add_trace(go.Scatter(
            x=hour_avg["game_hour"],
            y=hour_avg[col].fillna(0),
            name=label,
            stackgroup="one",
            line=dict(width=0.5, color=color),
            fillcolor=color,
            hovertemplate=f"{label}: %{{y:.0f}}<extra></extra>",
        ))

    fig.update_layout(
        title="Where Are Citizens During the Day  (average across simulation)",
        xaxis=dict(
            **_AXIS,
            title="Hour of Day",
            tickmode="array",
            tickvals=list(range(0, 24, 2)),
            ticktext=[f"{h:02d}:00" for h in range(0, 24, 2)],
        ),
        yaxis=dict(**_AXIS, title="Citizens (average)"),
        **_layout(legend=dict(
            bgcolor=C["surface2"], bordercolor=C["border"],
            font=dict(color=C["text"], size=10),
            orientation="h", yanchor="bottom", y=1.02, x=0,
        )),
    )
    return fig


def fig_r_number(data: dict) -> go.Figure:
    """Report Rt as unavailable until a documented generation-interval model exists."""
    fig = go.Figure()
    fig.add_annotation(
        x=0.5,
        y=0.55,
        xref="paper",
        yref="paper",
        showarrow=False,
        align="center",
        text=(
            "Rt unavailable<br>"
            "<span style='font-size:11px'>No generation-interval distribution is configured. "
            "TENUS does not relabel infectious-count growth as Rt.</span>"
        ),
        font=dict(color=C["muted"], size=16),
    )
    fig.update_layout(
        title="Reproduction Number Rt — unavailable",
        xaxis=dict(visible=False),
        yaxis=dict(visible=False),
        **_layout(),
    )
    return fig


# ?????? UI helpers ????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????

def kpi_card(title: str, value: str, sub: str = "", color: str = C["accent"]) -> dbc.Col:
    return dbc.Col(dbc.Card(
        dbc.CardBody([
            html.P(title, style={
                "color": C["muted"], "fontSize": "10px",
                "textTransform": "uppercase", "letterSpacing": "0.1em",
                "marginBottom": "4px",
            }),
            html.H3(value, style={
                "color": color, "fontSize": "26px", "fontWeight": "700",
                "lineHeight": "1.1", "marginBottom": "4px",
            }),
            html.P(sub, style={"color": C["muted"], "fontSize": "11px", "marginBottom": 0}),
        ]),
        style={
            "backgroundColor": C["surface2"],
            "border":     f"1px solid {C['border']}",
            "borderLeft": f"3px solid {color}",
            "borderRadius": "8px",
        },
    ), width=2)


def section_title(text: str) -> html.H5:
    return html.H5(text, style={
        "color": C["text"], "borderBottom": f"1px solid {C['border']}",
        "paddingBottom": "8px", "marginBottom": "18px", "marginTop": "30px",
        "fontWeight": "600", "letterSpacing": "0.04em", "fontSize": "14px",
    })


def graph(fig, height: int = 340) -> dcc.Graph:
    fig.update_layout(height=height)
    return dcc.Graph(figure=fig, config={"displayModeBar": True,
                                          "modeBarButtonsToRemove": ["lasso2d", "select2d"]})


# ?????? Pandemic settings labels & grouping ??????????????????????????????????????????????????????????????????????????????????????????????????????????????????
_SETTINGS_GROUPS = [
    ("???? Disease Properties", [
        ("InitialInfectionRatio",           "Initial Infection %",                  "pct",   True),
        ("DiseaseDuration",                 "Disease Duration",                     "days",  True),
        ("DetectionTime",                   "Detection Time",                       "days",  False),
        ("StartSymptoms",                   "Symptom Start Day",                    "day",   False),
        ("EndSymptoms",                     "Symptom End Day",                      "day",   False),
        ("StartInfection",                  "Infectious Start Day",                 "day",   True),
        ("EndInfection",                    "Infectious End Day",                   "day",   True),
        ("IndoorTransmissionProbability",   "Indoor Transmission Probability",      "x",     True),
        ("OutdoorTransmissionProbability",  "Outdoor Transmission Probability",     "x",     True),
        ("TransmissionRange",               "Transmission Range",                   "m",     True),
    ]),
    ("???? Death Rates by Age", [
        ("DeathRateChild",   "Child",   "pct", True),
        ("DeathRateTeen",    "Teen",    "pct", True),
        ("DeathRateYoung",   "Young",   "pct", True),
        ("DeathRateAdult",   "Adult",   "pct", True),
        ("DeathRateSenior",  "Senior",  "pct", True),
        ("SymptomProbability", "Symptom Probability", "pct", False),
    ]),
    ("???? Mask Settings", [
        ("MaskBehavior",                    "Mask Behaviour",                       "",    True),
        ("TransmissionProbabilityReduction","Transmission Reduction Factor",        "x",   False),
        ("RatioIgnoreMasks",                "Citizens Ignoring Masks",              "%",   False),
        ("RatioOtherProtectionMask",        "Other-Protection Mask Ratio",          "%",   False),
        ("RatioOwnProtectionMask",          "Own-Protection Mask Ratio",            "%",   False),
    ]),
    ("???? Contact Tracing", [
        ("BuildingContactTracingProbability", "Building Contact Tracing %",         "%",   False),
        ("AppBasedContactTracingProbability", "App-Based Contact Tracing %",        "%",   False),
    ]),
    ("???? Testing", [
        ("RelativeTestCapacity",             "Relative Test Capacity",              "%",   False),
        ("PercentOfTestsForSick",            "Tests Reserved for Sick",             "%",   False),
        ("MinimumTestDuration",              "Min Test Duration",                   "days",False),
        ("MaximumTestDuration",              "Max Test Duration",                   "days",False),
        ("TestSensitivityPercent",           "Test Sensitivity",                    "pct", False),
        ("TestSpecificityPercent",           "Test Specificity",                    "pct", False),
        ("RetestIntervalDays",                "Retest Interval",                     "days",False),
    ]),
    ("Scientific Disease Model", [
        ("ExposedDurationDistributionType",  "Exposed Duration Distribution",       "",    False),
        ("InfectiousStartDistributionType",  "Infectious Start Distribution",       "",    False),
        ("InfectiousEndDistributionType",    "Infectious End Distribution",         "",    False),
        ("SymptomStartDistributionType",     "Symptom Start Distribution",          "",    False),
        ("SymptomEndDistributionType",       "Symptom End Distribution",            "",    False),
        ("RecoveryDistributionType",         "Recovery Distribution",               "",    False),
        ("InfectiousnessProfileType",        "Infectiousness Profile",              "",    True),
        ("InitialSeedSamplingStrategy",      "Initial Seed Sampling",               "",    True),
        ("InitialInfectionAgeMode",          "Initial Infection Age Mode",          "",    False),
        ("EpidemicStepMinutes",              "Epidemic Step",                       "min", True),
    ]),
    ("Mortality and Healthcare Model", [
        ("AsymptomaticMortalityMultiplier",          "Asymptomatic Hazard Multiplier", "x", False),
        ("HealthcareWarningThresholdPercent",        "Healthcare Warning Threshold",   "pct", False),
        ("HealthcareCriticalThresholdPercent",       "Healthcare Critical Threshold",  "pct", True),
        ("HealthcareWarningMortalityMultiplier",     "Warning Hazard Multiplier",      "x", False),
        ("HealthcareCriticalMortalityMultiplier",    "Critical Hazard Multiplier",     "x", True),
    ]),
    ("???? Quarantine & Lockdown", [
        ("QuarantineBehavior",              "Quarantine Behaviour",                 "",    True),
        ("OnlyTestedCitizensToQuarantine",  "Only Tested ??? Quarantine",             "",    False),
        ("LockdownBehavior",                "Lockdown Behaviour",                   "",    True),
    ]),
    ("???? Superspreader Thresholds", [
        ("HubHighlightThreshold",           "Hub Highlight Min Infections",         "",    False),
        ("SuperspreaderCitizenThreshold",   "Citizen Superspreader Min",            "",    False),
        ("SuperspreaderLocationThreshold",  "Location Superspreader Min",           "",    False),
    ]),
]

_HIGH_THRESH = {
    "IndoorTransmissionProbability":  2.0,
    "OutdoorTransmissionProbability": 0.5,
    "InitialInfectionRatio":          20.0,
    "DeathRateChild":    1.0,
    "DeathRateTeen":     1.0,
    "DeathRateYoung":    1.0,
    "DeathRateAdult":    2.0,
    "DeathRateSenior":   5.0,
}


def _fmt_val(raw: str, unit: str) -> str:
    """Format a raw config string value with unit suffix."""
    try:
        v = float(raw)
        if unit == "pct" or unit == "%":
            return f"{v:.1f}%"
        elif unit == "days" or unit == "day":
            return f"{int(v)} day{'s' if v != 1 else ''}"
        elif unit == "x":
            return f"{v:.2f}??"
        elif unit == "m":
            return f"{v:.1f} m"
        elif unit == "min":
            return f"{v:g} min"
        else:
            return raw
    except (ValueError, TypeError):
        if unit == "" and raw in ("0", "1"):
            return "Yes ???" if raw == "1" else "No"
        return raw


def summary_table(data: dict) -> html.Div:
    meta     = data.get("RUN METADATA",     pd.DataFrame())
    core     = data.get("CORE METRICS",     pd.DataFrame())
    policy   = data.get("POLICY STATE",     pd.DataFrame())
    settings = data.get("PANDEMIC SETTINGS",pd.DataFrame())

    # Build a flat key???value dict from pandemic settings
    cfg = _settings_dict(data)

    def _td(text, muted=False, bold=False, color=None, width=None):
        style = {
            "padding": "7px 14px",
            "fontSize": "12px",
            "color": color or (C["muted"] if muted else C["text"]),
            "fontWeight": "700" if bold else "400",
            "borderBottom": f"1px solid {C['border']}",
            "verticalAlign": "middle",
        }
        if width:
            style["width"] = width
        return html.Td(text, style=style)

    def data_row(label, val, highlight=False, important=False):
        val_color = C["sick"] if highlight else (C["hospital"] if important else C["text"])
        return html.Tr([
            _td(label, muted=True, width="280px"),
            _td(str(val), bold=important or highlight, color=val_color),
        ], style={"backgroundColor": C["surface2"]})

    def section_header(title):
        return html.Tr(
            html.Td(title, colSpan=2, style={
                "backgroundColor": C["surface3"],
                "color": C["text"],
                "fontWeight": "700",
                "fontSize": "11px",
                "padding": "10px 14px 8px",
                "textTransform": "uppercase",
                "letterSpacing": "0.08em",
                "borderBottom": f"2px solid {C['border']}",
            })
        )

    rows = []

    # ?????? Run Timing ???????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????
    rows.append(section_header("??? Run Timing"))
    if not meta.empty:
        r = meta.iloc[0]
        rows += [
            data_row("Start (wall clock)", str(r.get("start_wall_time", "???"))[:19].replace("T", " ")),
            data_row("End (wall clock)",   str(r.get("end_wall_time",   "???"))[:19].replace("T", " ")),
            data_row("Wall Duration",      str(r.get("elapsed_wall_seconds", "???")) + " s"),
            data_row("Game Start",         str(r.get("game_start_time", "???"))[:19].replace("T", " ")),
            data_row("Game End",           str(r.get("game_end_time",   "???"))[:19].replace("T", " ")),
            data_row("Pandemic Day (final)", r.get("pandemic_day", "???")),
            data_row("Lifecycle State",    r.get("lifecycle_state", "???")),
        ]

    # ?????? Outcome Metrics ????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????
    rows.append(section_header("???? Outcome Metrics"))
    if not core.empty:
        r = core.iloc[0]
        def fi(k): return f"{int(float(r.get(k, 0))):,}"
        def fp(k): return f"{float(r.get(k, 0)):.1f}%"
        hosp_pct = float(r.get("hospital_usage_pct", 0))
        amb_pct  = float(r.get("ambulance_usage_pct", 0))
        try:
            critical_hospital_pct = float(cfg.get("HealthcareCriticalThresholdPercent", "nan"))
        except (TypeError, ValueError):
            critical_hospital_pct = float("nan")
        hospital_is_critical = not math.isnan(critical_hospital_pct) and hosp_pct >= critical_hospital_pct
        rows += [
            data_row("Tracked Population",       fi("tracked_population")),
            data_row("Total Transmissions",      fi("transmissions_total"), important=True),
            data_row("  ?? Indoor",               fi("transmissions_indoor")),
            data_row("  ?? Outdoor",              fi("transmissions_outdoor")),
            data_row("  ?? Vehicle",              fi("transmissions_vehicle")),
            data_row("Citizens Quarantined",     fi("quarantine_citizens")),
            data_row("Positive Tests",           fi("positive_tests")),
            data_row("Contacts Tracked (pairs)", fi("contacts_tracked_pairs")),
            data_row("Hotspot Buildings",        fi("hotspot_buildings")),
            data_row("Hub Buildings",            fi("hub_buildings")),
            data_row("Hospital Usage (final)",   fp("hospital_usage_pct"),  highlight=hospital_is_critical, important=hosp_pct > 50),
            data_row("Ambulance Usage (final)",  fp("ambulance_usage_pct"), important=amb_pct > 50),
        ]

    # ?????? Active Policies ????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????
    rows.append(section_header("???? Active Policies at Stop"))
    if not policy.empty:
        for _, pr in policy.iterrows():
            enabled = int(float(pr.get("enabled", 0)))
            rows.append(data_row(
                str(pr.get("policy", "")),
                "ON ???" if enabled else "OFF ???",
                important=bool(enabled),
            ))

    # ?????? Pandemic Settings (from config) ?????????????????????????????????????????????????????????????????????????????????????????????????????????????????????
    if cfg:
        for group_title, params in _SETTINGS_GROUPS:
            rows.append(section_header(group_title))
            for key, label, unit, is_important in params:
                raw = cfg.get(key)
                if raw is None:
                    continue
                formatted = _fmt_val(raw, unit)
                # Highlight if value exceeds a known danger threshold
                try:
                    fv = float(raw)
                    highlight = key in _HIGH_THRESH and fv > _HIGH_THRESH[key]
                except (ValueError, TypeError):
                    highlight = False
                rows.append(data_row(label, formatted, highlight=highlight, important=is_important and not highlight))
    else:
        rows.append(section_header("??? Pandemic Settings"))
        rows.append(data_row("(Not available)",
            "Run a new simulation to capture settings"))

    if not rows:
        return html.Div("No data available.", style={"color": C["muted"]})

    table = html.Table(
        rows,
        style={
            "width": "100%",
            "borderCollapse": "collapse",
            "tableLayout": "fixed",
        },
    )

    return html.Div(
        table,
        style={
            "backgroundColor": C["surface2"],
            "border": f"1px solid {C['border']}",
            "borderRadius": "8px",
            "overflowY": "auto",
            "maxHeight": "640px",
            "overflowX": "hidden",
        },
    )


# ?????? App layout ????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????

app = dash.Dash(
    __name__,
    external_stylesheets=[dbc.themes.BOOTSTRAP],
    title="Pandemic Simulator ??? Dashboard",
    suppress_callback_exceptions=True,
)

def serve_layout():
    """Called on every page request so dropdown options stay fresh."""
    files = list_csvs()
    opts = run_options(files)
    return html.Div([
        dcc.Store(id="csv-store"),

        # ?????? Top nav bar ?????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????
        html.Div([
            html.Div([
                html.Span("????", style={"marginRight": "8px", "fontSize": "20px"}),
                html.Span("Pandemic Simulator", style={
                    "fontWeight": "700", "fontSize": "18px", "color": C["text"],
                }),
                html.Span("Analytics Dashboard", style={
                    "color": C["muted"], "fontSize": "12px", "marginLeft": "10px",
                }),
            ], style={"display": "flex", "alignItems": "center"}),
            html.Div(id="nav-badge", style={"color": C["muted"], "fontSize": "12px"}),
        ], style={
            "backgroundColor": C["surface"],
            "borderBottom": f"1px solid {C['border']}",
            "padding": "12px 28px",
            "display": "flex",
            "justifyContent": "space-between",
            "alignItems": "center",
            "position": "sticky", "top": "0", "zIndex": "999",
        }),

        # ?????? Page body ???????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????
        html.Div([

            section_title("???? Load Simulation Run"),
            dbc.Row([
                dbc.Col(
                    dcc.Dropdown(
                        id="file-dropdown",
                        options=opts,
                        placeholder="Select a pandemic_run_*.csv ???" if opts else "No CSVs found ??? use drag & drop below",
                        style={
                            "backgroundColor": C["surface2"],
                            "color": C["text"],
                            "border": f"1px solid {C['border']}",
                        },
                    ), width=8),
                dbc.Col(
                    dcc.Upload(
                        id="csv-upload",
                        children=html.Div([
                            "Drag & Drop  or  ",
                            html.A("Browse file", style={"color": C["accent"]}),
                        ]),
                        style={
                            "border": f"1px dashed {C['border']}",
                            "borderRadius": "6px",
                            "padding": "9px 12px",
                            "color": C["muted"],
                            "cursor": "pointer",
                            "fontSize": "13px",
                            "textAlign": "center",
                        },
                    ), width=4),
            ], className="mb-4"),

            # KPI row
            html.Div(id="kpi-row"),
            # Main charts
            html.Div(id="main-charts"),
            # Detail charts
            html.Div(id="detail-charts"),
            # Run summary
            html.Div(id="run-summary"),

            # ?????? Multi-run comparison ??????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????
            section_title("?????? Multi-Run Comparison"),
            dbc.Row([
                dbc.Col(
                    dcc.Dropdown(
                        id="compare-dropdown",
                        options=opts,
                        placeholder="Select pandemic run CSVs to compare ???",
                        multi=True,
                        style={
                            "backgroundColor": C["surface2"],
                            "color": C["text"],
                            "border": f"1px solid {C['border']}",
                        },
                    ), width=10),
                dbc.Col(
                    dbc.Button("Compare", id="compare-btn", color="primary", size="sm",
                               style={"width": "100%"}),
                    width=2),
            ]),
            html.Div(id="compare-charts", style={"marginTop": "20px", "marginBottom": "60px"}),

        ], style={"padding": "20px 28px", "maxWidth": "1640px", "margin": "0 auto"}),

    ], style={
        "backgroundColor": C["bg"],
        "minHeight": "100vh",
        "fontFamily": "'Segoe UI', Arial, sans-serif",
    })

app.layout = serve_layout


# ?????? Callbacks ???????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????

@app.callback(
    Output("csv-store", "data"),
    Input("file-dropdown", "value"),
    Input("csv-upload",    "contents"),
    State("csv-upload",    "filename"),
)
def load_data(filepath, upload_contents, _fname):
    ctx = callback_context.triggered_id
    if ctx == "csv-upload" and upload_contents:
        _, content_string = upload_contents.split(",")
        decoded = base64.b64decode(content_string).decode("utf-8")
        data = load_csv(decoded, is_text=True)
    elif filepath:
        data = load_csv(filepath)
    else:
        return None
    # Serialize DataFrames ??? JSON-safe dicts
    return {k: v.astype(str).to_dict("records") for k, v in data.items()}


def _restore(stored) -> dict:
    """Deserialise stored dicts back to DataFrames and re-coerce numerics."""
    if not stored:
        return {}
    data = {k: pd.DataFrame(v) for k, v in stored.items()}
    return _coerce(data)


@app.callback(
    Output("nav-badge",    "children"),
    Output("kpi-row",      "children"),
    Output("main-charts",  "children"),
    Output("detail-charts","children"),
    Output("run-summary",  "children"),
    Input("csv-store", "data"),
)
def render_dashboard(stored):
    empty_placeholder = html.Div(
        "???  Load a pandemic run CSV above to begin.",
        style={"color": C["muted"], "padding": "60px", "textAlign": "center", "fontSize": "15px"},
    )
    if not stored:
        return "", empty_placeholder, "", "", ""

    data = _restore(stored)
    kpis = extract_kpis(data)

    # Nav badge
    meta  = data.get("RUN METADATA", pd.DataFrame())
    badge = ""
    if not meta.empty:
        r = meta.iloc[0]
        end = str(r.get("end_wall_time", ""))
        badge = (
            f"Run ended: {end[:19].replace('T',' ')}  ??  "
            f"{kpis['lifecycle']}  ??  "
            f"{kpis['game_days']} pandemic days  ??  "
            f"Wall clock: {kpis['run_duration']}"
        )

    # ?????? KPI cards ???????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????
    kpi_row = dbc.Row([
        kpi_card("Population",       kpis["total_pop"],       "Tracked citizens",             C["healthy"]),
        kpi_card("Attack Rate",      kpis["attack_rate"],      "Ever infected / total pop",    C["sick"]),
        kpi_card("Case Fatality",    kpis["cfr"],              "Deaths / resolved cases",      C["dead"]),
        kpi_card("Peak Infectious",  kpis["peak_sick"],        kpis["peak_day"],               C["hospital"]),
        kpi_card("Recovered",        kpis["total_recovered"],  "Final recovered count",        C["recovered"]),
        kpi_card("Total Deaths",     kpis["total_dead"],       "Final pandemic death toll",    C["dead"]),
    ], className="g-3 mb-2")

    # ?????? Main charts ?????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????
    main_charts = html.Div([
        section_title("???? Epidemic Timeline"),
        dbc.Row([
            dbc.Col(graph(fig_epidemic_curve(data), 360), width=8),
            dbc.Col(graph(fig_seird_donut(data),    360), width=4),
        ], className="g-3 mb-3"),
        dbc.Row([
            dbc.Col(graph(fig_delta(data),     310), width=4),
            dbc.Col(graph(fig_healthcare(data),310), width=5),
            dbc.Col(graph(fig_r_number(data),  310), width=3),
        ], className="g-3 mb-3"),
    ])

    # ?????? Detail charts ???????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????
    detail_charts = html.Div([
        section_title("???? Demographics & Spread"),
        dbc.Row([
            dbc.Col(graph(fig_age(data),                 340), width=4),
            dbc.Col(graph(fig_origins(data),             340), width=5),
            dbc.Col(graph(fig_transmission_donut(data),  340), width=3),
        ], className="g-3 mb-3"),

        section_title("???? Policy & Infrastructure"),
        dbc.Row([
            dbc.Col(graph(fig_lockdown(data),  320), width=6),
            dbc.Col(graph(fig_districts(data), 320), width=6),
        ], className="g-3 mb-3"),

        section_title("???? Superspreaders & Hotspots"),
        dbc.Row([
            dbc.Col(graph(fig_spreaders(data),  320), width=6),
            dbc.Col(graph(fig_locations(data),  320), width=6),
        ], className="g-3 mb-3"),

        section_title("???? Movement Patterns & Intervention Impact"),
        dbc.Row([
            dbc.Col(graph(fig_movement_origins(data), 420), width=12),
        ], className="g-3 mb-3"),
        dbc.Row([
            dbc.Col(graph(fig_citizen_daily_rhythm(data), 380), width=12),
        ], className="g-3 mb-3"),
    ])

    # ?????? Run summary table ??????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????
    run_summary = html.Div([
        section_title("???? Full Run Summary"),
        summary_table(data),
    ])

    return badge, kpi_row, main_charts, detail_charts, run_summary


# ?????? Multi-run comparison callback ???????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????

AGGREGATE_METRICS = [
    ("attack_rate_pct", "Attack rate %"),
    ("final_prevalence_pct", "Final prevalence %"),
    ("deaths_total", "Deaths"),
    ("hospitalizations_total", "Hospitalizations"),
    ("empirical_secondary_infections_per_infector", "Secondary infections / infector"),
    ("actual_mask_usage_pct", "Actual mask usage %"),
]


def _stat_cell(value):
    try:
        return f"{float(value):,.3f}"
    except (TypeError, ValueError):
        return "—"


def scenario_statistics_table(aggregate):
    rows = []
    for scenario_id, scenario in aggregate.items():
        for metric, label in AGGREGATE_METRICS:
            statistics = scenario.get("metrics", {}).get(metric)
            if not statistics:
                continue
            rows.append(html.Tr([
                html.Td(scenario.get("scenario_name", scenario_id)),
                html.Td(label),
                html.Td(str(int(statistics["n"]))),
                html.Td(_stat_cell(statistics["mean"])),
                html.Td(_stat_cell(statistics["median"])),
                html.Td(_stat_cell(statistics["standard_deviation"])),
                html.Td(_stat_cell(statistics["minimum"])),
                html.Td(_stat_cell(statistics["p05"])),
                html.Td(_stat_cell(statistics["p25"])),
                html.Td(_stat_cell(statistics["p75"])),
                html.Td(_stat_cell(statistics["p95"])),
                html.Td(_stat_cell(statistics["maximum"])),
                html.Td(_stat_cell(statistics["iqr"])),
            ]))
    if not rows:
        return html.Div(
            "No completed scientific run_summary.csv + manifest pairs were found in this selection.",
            style={"color": C["muted"]},
        )
    headers = ["Scenario", "Metric", "n", "Mean", "Median", "SD (sample)",
               "Min", "P5", "P25", "P75", "P95", "Max", "IQR"]
    return html.Div(
        html.Table(
            [html.Thead(html.Tr([html.Th(value) for value in headers])), html.Tbody(rows)],
            style={"width": "100%", "fontSize": "11px", "color": C["text"]},
        ),
        style={"overflowX": "auto", "backgroundColor": C["surface"], "padding": "12px"},
    )


def paired_statistics_table(paired):
    rows = []
    for scenario_id, comparison in paired.items():
        for metric, label in AGGREGATE_METRICS:
            block = comparison.get("metrics", {}).get(metric)
            if not block:
                continue
            statistics = block["statistics"]
            rows.append(html.Tr([
                html.Td(comparison.get("scenario_name", scenario_id)),
                html.Td(comparison.get("reference_scenario_name", comparison.get("reference_scenario_id"))),
                html.Td(label),
                html.Td(str(int(statistics["n"]))),
                html.Td(_stat_cell(statistics["mean"])),
                html.Td(_stat_cell(statistics["median"])),
                html.Td(_stat_cell(statistics["standard_deviation"])),
                html.Td(", ".join(
                    f"Pair {item['pair_id']}: {_stat_cell(item['difference'])}"
                    for item in block["pairs"]
                )),
            ]))
    if not rows:
        return html.Div(
            "No matched positive pair_id values are available for the selected scenarios.",
            style={"color": C["muted"]},
        )
    headers = ["Scenario", "Reference", "Metric (scenario − reference)", "Pairs",
               "Mean difference", "Median difference", "SD", "Per-pair differences"]
    return html.Div(
        html.Table(
            [html.Thead(html.Tr([html.Th(value) for value in headers])), html.Tbody(rows)],
            style={"width": "100%", "fontSize": "11px", "color": C["text"]},
        ),
        style={"overflowX": "auto", "backgroundColor": C["surface"], "padding": "12px"},
    )

@app.callback(
    Output("compare-charts", "children"),
    Input("compare-btn",      "n_clicks"),
    State("compare-dropdown", "value"),
    prevent_initial_call=True,
)
def render_comparison(_, filepaths):
    if not filepaths:
        return html.Div("Select at least one run.", style={"color": C["muted"]})

    runs = []
    for fp in filepaths:
        try:
            d = _coerce(load_csv(fp))
            runs.append((run_display_label(Path(fp), DEFAULT_DATA_DIR), d))
        except Exception:
            pass

    if not runs:
        return html.Div("Could not load selected files.", style={"color": C["muted"]})

    completed_records = load_completed_runs(Path(filepath) for filepath in filepaths)
    scenario_aggregate = aggregate_scenarios(completed_records)
    reference_id = completed_records[0].scenario_id if completed_records else None
    paired_aggregate = aggregate_paired_differences(completed_records, reference_id)

    labels        = [r[0] for r in runs]
    attack_rates  = []
    cfrs          = []
    peak_sicks    = []
    total_deads   = []

    for _, d in runs:
        kpis = extract_kpis(d)
        def _f(k, strip=""):
            try:   return float(kpis[k].replace("%", "").replace(",", "").strip())
            except: return 0.0
        attack_rates.append(_f("attack_rate"))
        cfrs.append(         _f("cfr"))
        peak_sicks.append(   _f("peak_sick"))
        total_deads.append(  _f("total_dead"))

    def bar(y_vals, title, color):
        fig = go.Figure(go.Bar(
            x=labels, y=y_vals, marker_color=color,
            hovertemplate="%{x}: %{y:,.1f}<extra></extra>",
        ))
        fig.update_layout(
            title=title, height=260,
            xaxis=dict(**_AXIS, tickangle=-18),
            yaxis=dict(**_AXIS),
            **_layout(),
        )
        return fig

    # Overlay epidemic curves
    overlay = go.Figure()
    palette = [C["sick"], C["hospital"], C["dead"], C["recovered"], C["vehicle"], C["indoor"]]
    for i, (name, d) in enumerate(runs):
        seird = d.get("SEIRD TIME SERIES", pd.DataFrame())
        if not seird.empty and "pandemic_day" in seird.columns:
            infectious_col = "infectious" if "infectious" in seird.columns else "sick"
            overlay.add_trace(go.Scatter(
                x=seird["pandemic_day"], y=seird[infectious_col],
                name=name, line=dict(color=palette[i % len(palette)], width=2),
                hovertemplate=f"{name} - Day %{{x}}: %{{y:,.0f}} infectious<extra></extra>",
            ))
    overlay.update_layout(
        title="Epidemic Curves ??? All Runs Overlaid",
        xaxis=dict(**_AXIS, title="Pandemic Day"),
        yaxis=dict(**_AXIS, title="Active Infectious (I)"),
        height=340,
        **_layout(),
    )

    return html.Div([
        dbc.Row([
            dbc.Col(dcc.Graph(figure=overlay, config={"displayModeBar": True}), width=12),
        ], className="g-3 mb-3"),
        dbc.Row([
            dbc.Col(dcc.Graph(figure=bar(attack_rates, "Attack Rate % per Run",    C["sick"])),    width=3),
            dbc.Col(dcc.Graph(figure=bar(cfrs,         "Case Fatality % per Run",  C["dead"])),    width=3),
            dbc.Col(dcc.Graph(figure=bar(peak_sicks,   "Peak Infectious per Run",  C["hospital"])),width=3),
            dbc.Col(dcc.Graph(figure=bar(total_deads,  "Total Deaths per Run",     C["dead"])),    width=3),
        ], className="g-3"),
        section_title("Scenario statistics across completed runs"),
        html.P(
            "Standard deviation is the sample SD. Percentiles use linear interpolation. Invalid and failed runs are excluded by manifest status.",
            style={"color": C["muted"], "fontSize": "11px"},
        ),
        scenario_statistics_table(scenario_aggregate),
        section_title("Paired-seed differences"),
        html.P(
            "Differences are scenario minus the first selected reference scenario and are matched strictly by pair_id.",
            style={"color": C["muted"], "fontSize": "11px"},
        ),
        paired_statistics_table(paired_aggregate),
    ])


# ?????? Entry point ?????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????

if __name__ == "__main__":
    print()
    print("  Pandemic Simulator ??? Analytics Dashboard")
    print("  ???????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????")
    if DEFAULT_DATA_DIR.exists():
        files = list_csvs()
        print(f"  Data dir : {DEFAULT_DATA_DIR}")
        print(f"  CSV runs : {len(files)} found")
    else:
        print(f"  Data dir not found: {DEFAULT_DATA_DIR}")
        print("  You can drag & drop CSV files into the dashboard.")
    print()
    print("  Opening at ???  http://localhost:8050")
    print("  Press Ctrl+C to stop")
    print()
    app.run(debug=False, host="0.0.0.0", port=8050)
