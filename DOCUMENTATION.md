# Pandemic Simulator — Complete Documentation

> **Version:** Latest (see `src/GitVersion.yml` for exact build)  
> **Base Game Required:** Cities: Skylines (via Steam)  
> **Platform:** Windows (the game and mod are Windows-only)

---

## Table of Contents

1. [What Is This Mod? — Full Overview](#1-what-is-this-mod--full-overview)
2. [How Does It Work? — The Science Behind the Scenes](#2-how-does-it-work--the-science-behind-the-scenes)
3. [Installing the Mod on a New System (Play-Only)](#3-installing-the-mod-on-a-new-system-play-only)
4. [Setting Up for Code Editing (Developer Mode)](#4-setting-up-for-code-editing-developer-mode)
5. [The Pandemic Dashboard — Every Button Explained](#5-the-pandemic-dashboard--every-button-explained)
6. [Control Buttons — Row 1](#6-control-buttons--row-1)
7. [Visualization Buttons — Row 2](#7-visualization-buttons--row-2)
8. [Live Metric Cards](#8-live-metric-cards)
9. [Charts](#9-charts)
10. [Detail Sections (Scrollable Area)](#10-detail-sections-scrollable-area)
11. [In-Dashboard Settings — Every Setting Explained](#11-in-dashboard-settings--every-setting-explained)
12. [Visual Overlays in the City](#12-visual-overlays-in-the-city)
13. [Automated Experiment Batch Runner](#13-automated-experiment-batch-runner)
14. [Data Export Files](#14-data-export-files)
15. [Optional Analytics Dashboard](#15-optional-analytics-dashboard)
16. [In-Game Verification Checklist](#16-in-game-verification-checklist)
17. [Frequently Asked Questions](#17-frequently-asked-questions)
18. [Scientific Kernel and Reproducibility Reference](#18-scientific-kernel-and-reproducibility-reference)

---

## 1. What Is This Mod? — Full Overview

The **Pandemic Simulator** is a mod for the city-building game **Cities: Skylines**. It turns your city into a live epidemiological laboratory. Instead of just building roads and zoning land, you now have to manage an outbreak spreading through your own population — the same citizens who live, work, commute, shop, and sleep in your city.

### What happens when you activate it?

A disease appears in your city. It silently infects a small number of citizens at first. From that moment on, every time an infected person stands near a healthy person — on the pavement, in a shop, on a bus, at school — there is a real mathematical chance that the disease spreads. The mod checks this thousands of times each in-game minute, for every pair of citizens who are close to each other.

As mayor, you have a set of **real-world policy tools** at your fingertips:

- **Masks** — reduce how easily the disease spreads between people
- **Quarantine** — isolate sick citizens (and their families or contacts) at home
- **Lockdown** — close entire sectors of the city (schools, shops, offices, transit, etc.)
- **Testing** — identify who is sick so they can be quarantined

While the outbreak unfolds, you watch it in real time through a **live dashboard** that shows:
- Total infected, recovered, and dead citizens
- A trend chart showing how the outbreak is growing or shrinking
- A pie-style SIDR distribution (Susceptible / Infected / Dead / Recovered)
- Age-group breakdown of who is getting sick
- Which buildings and locations are spreading the most infections
- Which individual citizens are superspreaders
- A city-wide heatmap glowing red over the worst-hit areas

The goal is to understand, contain, and ultimately end the outbreak — just like a real public-health authority would.

---

## 2. How Does It Work? — The Science Behind the Scenes

You do not need to understand this section to play. It is here for people who are curious about what the mod is doing under the hood.

### 2.1 The Citizen Simulation

Cities: Skylines already simulates tens of thousands of individual citizens ("Cims") moving around your city. Each Cim has a home building, a workplace or school, and a daily routine. The mod hooks into this existing simulation. Every few game-minutes it scans every citizen's current physical position and location type (indoors, outdoors, in a vehicle, at home, etc.).

### 2.2 Transmission

The epidemiological kernel runs at the configured `EpidemicStepMinutes`, measured in game simulation time. Outdoors it retains spatial buckets. Vehicles and non-residential buildings use deterministic, context-specific contact caps rather than connecting every occupant to every other occupant. Household pairs remain explicit, while residential shared-area encounters use their own named multiplier.

Only a living citizen in the personal half-open infectious interval may be a source, and only a living `Susceptible` citizen may be a target. For simultaneous exposures to one target, TENUS combines all probabilities as `1 - product(1 - p_i)`, performs one infection draw, and attributes a successful infection using source hazards `-ln(1 - p_i)`. Dictionary or citizen iteration order therefore does not award duplicate infections or choose the infector.

Probabilities are converted from the configured hourly hazard to the exact step duration. A configurable `Flat` or `PiecewiseLinear` infectiousness profile then scales the hazard. Source-control masks affect the source; personal-protection masks affect the target. Persistent mask and tracing traits are derived from the master seed and citizen ID, not from first-access order.

### 2.3 Disease Timeline

Every infection owns one `DiseaseCourse`. The mutually exclusive primary states are `Susceptible`, `Exposed`, `Infectious`, `PostInfectiousIll`, `Recovered`, `Dead`, plus `Removed` for population lifecycle. Symptoms are an independent attribute (`None`, `PreSymptomatic`, `Symptomatic`, `PostSymptomatic`) and are never used as a substitute for infectiousness.

Intervals are half-open: `InfectiousStart <= now < InfectiousEnd` and `SymptomStart <= now < SymptomEnd`. Recovery is terminal and prevents reinfection; death is terminal. Timeline offsets support `Deterministic`, `Normal`, `LogNormal`, and `Gamma` distributions with explicit means, standard deviations and bounds. Invalid draws are resampled and then deterministically bounded. Migrated configurations use deterministic legacy values; TENUS does not introduce an undocumented disease curve.

### 2.4 Policy Systems

| System | Core component | What it does |
|---|---|---|
| Masks | `MaskEngine` | Stable citizen assignment; separate source and wearer protection; context-aware hazard reduction |
| Isolation / quarantine | `IsolationQuarantineEngine` | Separates case isolation from contact or pending-test quarantine without changing disease outcomes |
| Testing | `TestingEngine` | Explicit request, queue, sample, pending, available, expired and cancelled states with capacity, sensitivity and specificity |
| Physical contacts | `ContactEngine` + `ContactSamplingEngine` | Records canonical symmetric encounters independently of tracing and bounds mixing in large contexts |
| Contact tracing | `ContactTracingEngine` | Derives app and manual traceability after a physical contact; app tracing requires both citizens to use the app |
| Lockdown | `LockdownEngine` | Per-family close/reopen thresholds, minimum closure time and cooldown against a stable denominator |
| Healthcare | `HealthcareEngine` | Applies explicit warning/critical saturation multipliers only to mortality hazard |

Isolation and quarantine restrict external contacts only once the affected citizen has reached home. Household contact remains possible. Neither intervention heals, kills, shortens disease, or changes mortality by itself.

### 2.5 Analytics & Snapshots

The scientific recorder exports disjoint SEIRD compartments, symptom counts, contacts, transmissions, tests, interventions, population changes and full healthcare history. `S + E + I + PostInfectiousIll + R + D` equals the tracked epidemiological population. The legacy observer and live panel remain available for interactive compatibility, but batch analysis should use the scientific files described below.

---

## 3. Installing the Mod on a New System (Play-Only)

These instructions are for someone who just wants to **play** with the mod — no coding involved.

### Step 1 — Install Cities: Skylines

1. Open **Steam** (download from https://store.steampowered.com if you do not have it).
2. Purchase and install **Cities: Skylines**.
3. Launch the game at least once so it creates its folder structure, then close it.

### Step 2 — Locate the Mods folder

The game stores user-installed mods here:

```
C:\Users\<YourWindowsUsername>\AppData\Local\Colossal Order\Cities_Skylines\Addons\Mods\
```

> **Tip:** `AppData` is a hidden folder. In Windows Explorer, type the path above directly into the address bar, or press `Win + R` and paste it.

### Step 3 — Create the mod subfolder

Inside the `Mods` folder, create a new folder called exactly:

```
RealTime
```

The full path will be:

```
C:\Users\<YourWindowsUsername>\AppData\Local\Colossal Order\Cities_Skylines\Addons\Mods\RealTime\
```

### Step 4 — Copy the mod files

You need to copy these files **into** the `RealTime` folder you just created:

| File | Where to find it |
|---|---|
| `RealTime.dll` | `Pandamic_Simulator\src\bin\Release\RealTime.dll` |
| All files from `build_deploy\RealTimeEvents\` | Copy the entire folder contents into a subfolder named `Events` inside `RealTime` |
| All files from `build_deploy\RealTimeLocalization\` | Copy the entire folder contents into a subfolder named `Localization` inside `RealTime` |

After copying, your `RealTime` folder should look like this:

```
RealTime\
    RealTime.dll
    Events\
        Aquarium.xml
        ArtMuseum.xml
        ExpoCenter.xml
        Library.xml
        Opera.xml
        PoshMall.xml
        Theater.xml
    Localization\
        de.xml
        en.xml
        es.xml
        fr.xml
        it.xml
        ja.xml
        ko.xml
        pl.xml
        pt.xml
        ru.xml
        zh.xml
```

### Step 5 — Enable the mod in the game

1. Open **Cities: Skylines**.
2. From the main menu click **Content Manager**.
3. Click the **Mods** tab on the left.
4. Find **Real Time** in the list and click the toggle to enable it (it should turn green/on).
5. Close Content Manager.

### Step 6 — Start playing

Load or start a new city. Once the map loads, the **Pandemic Live Panel** will appear automatically in the top-right area of the screen.

---

## 4. Setting Up for Code Editing (Developer Mode)

Follow these steps on a machine where you want to **edit the source code** and rebuild the mod yourself.

### 4.1 Prerequisites

Install all of the following before you start:

| Tool | Purpose | Where to get it |
|---|---|---|
| **Git** | Version control, cloning the repo | https://git-scm.com |
| **.NET SDK 3.5 + .NET 6/7 SDK** | Building a .NET 3.5 project (C# 7.2) | Visual Studio includes this; or install separately from Microsoft |
| **Visual Studio 2022** (recommended) OR **VS Code** | Code editor | https://visualstudio.microsoft.com |
| **Cities: Skylines** (Steam) | The game libraries that the mod depends on | Steam |

### 4.2 Required Environment Variables

The build system needs to know where Cities: Skylines is installed. You must set these **before** building:

| Variable | Purpose | Example value |
|---|---|---|
| `CITIES_SKYLINES_BINARIES` | Path to the game's managed DLL folder | `C:\Program Files (x86)\Steam\steamapps\common\Cities_Skylines\Cities_Data\Managed` |
| `CITIES_SKYLINES_MOD_DIR` | Path where the built mod should be automatically deployed | `C:\Users\<YourUsername>\AppData\Local\Colossal Order\Cities_Skylines\Addons\Mods\RealTime` |

#### Setting variables permanently in Windows

1. Press `Win + S`, search for **"Environment Variables"** and open **"Edit the system environment variables"**.
2. Click **"Environment Variables…"**.
3. Under **"User variables"**, click **"New"** and add each variable above.
4. Click OK all the way through and **restart any open terminal windows**.

#### Setting variables temporarily in a PowerShell session

```powershell
$env:CITIES_SKYLINES_BINARIES = "C:\Program Files (x86)\Steam\steamapps\common\Cities_Skylines\Cities_Data\Managed"
$env:CITIES_SKYLINES_MOD_DIR  = "C:\Users\<YourUsername>\AppData\Local\Colossal Order\Cities_Skylines\Addons\Mods\RealTime"
```

> These only last for the current PowerShell window. Set them permanently (see above) if you do not want to type them every time.

### 4.3 Opening the project in Visual Studio

1. Open **Visual Studio 2022**.
2. Click **"Open a project or solution"**.
3. Navigate to `Pandamic_Simulator\src\` and open `RealTime.sln`.
4. Visual Studio will restore NuGet packages automatically. Wait for this to finish (the progress bar at the bottom).
5. Confirm that the **Solution Configuration** dropdown at the top is set to **Release** (not Debug).

### 4.4 Building the mod

#### Option A — Inside Visual Studio

- Press `Ctrl + Shift + B` or go to **Build → Build Solution**.
- The compiled `RealTime.dll` will appear in `src\bin\Release\`.
- If `CITIES_SKYLINES_MOD_DIR` is set, the project will **automatically copy** the DLL to your mods folder after a successful build.

#### Option B — PowerShell command line

Open a PowerShell window in the `Pandamic_Simulator` folder and run:

```powershell
cd "C:\Users\<YourUsername>\Pandamic_Simulator\src"
dotnet build RealTime.sln -c Release
```

Or run the provided build script from the repo root:

```powershell
cd "C:\Users\<YourUsername>\Pandamic_Simulator"
.\build.ps1
```

The build script sets the environment variables, cleans, builds, and reports which DLL files were produced and deployed.

### 4.5 After editing: deploy and test

1. Build the project (Step 4.4).
2. Make sure Cities: Skylines is **closed** before deploying (the game locks the DLL while running).
3. The built DLL is automatically copied to the mods directory if the environment variable is set.
4. If it is not copied automatically, copy `src\bin\Release\RealTime.dll` manually to:
   ```
   C:\Users\<YourUsername>\AppData\Local\Colossal Order\Cities_Skylines\Addons\Mods\RealTime\RealTime.dll
   ```
5. Open Cities: Skylines, load a city, and test your changes.

### 4.6 Key source file locations

| What you want to change | File to edit |
|---|---|
| Pandemic spread / disease engine | `src\RealTime\Pandemic\PandemicManager.cs` |
| Mask system | `src\RealTime\Pandemic\MaskManager.cs` |
| Quarantine logic | `src\RealTime\Pandemic\QuarantineManager.cs` |
| Testing system | `src\RealTime\Pandemic\TestManager.cs` |
| Contact tracing | `src\RealTime\Pandemic\ContactManager.cs` |
| All configuration fields and defaults | `src\RealTime\Config\RealTimeConfig.cs` |
| The live dashboard panel UI | `src\RealTime\UI\PandemicLivePanel.cs` |
| The trend chart | `src\RealTime\UI\PandemicTrendChartView.cs` |
| The SIDR bar view | `src\RealTime\UI\PandemicSidrBarView.cs` |
| X-Ray heatmap overlay | `src\RealTime\Pandemic\PandemicXRayOverlayBehavior.cs` |
| Biohazard icon above infected citizens | `src\RealTime\Pandemic\InfectedCitizenIconBehavior.cs` |
| Red movement trails behind infected | `src\RealTime\Pandemic\InfectedCitizenTrailBehavior.cs` |
| Building icons (hotspot / hub markers) | `src\RealTime\Pandemic\QuarantineBuildingIconBehavior.cs` |
| Citizen AI behaviour modifications | `src\RealTime\CustomAI\RealTimeResidentAI.cs` (and sibling files) |
| Mod entry point (enable, disable, load) | `src\RealTime\Core\RealTimeMod.cs` |
| Data recording + CSV export | `src\RealTime\Pandemic\PandemicObserver.cs` |

---

## 5. The Pandemic Dashboard — Every Button Explained

When you load a city with the mod enabled, a **floating panel** appears in the upper-right area of the screen. Its title bar shows the current pandemic state and the in-game date. **Click the title bar at any time to collapse or expand the panel.**

The panel is divided into three main zones:

```
┌─────────────────────────────────────────────────────────┐
│  TITLE BAR  (click to collapse/expand)                  │
├──────────────────────── HEADER ─────────────────────────┤
│  [Start] [Stop] [Masks] [Quarantine] [Lockdown]   (row1)│
│  [Overlays] [X-Ray] [Type] [Basis] [Experiments]  (row2)│
│  ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐         │
│  │Metric│ │Metric│ │Metric│ │Metric│ │Metric│   (cards) │
│  └──────┘ └──────┘ └──────┘ └──────┘ └──────┘         │
│  ┌── Trend Chart ───────────┐ ┌─ SIDR Bar ─┐           │
│  │                          │ │            │           │
│  └──────────────────────────┘ └────────────┘           │
├─────────────────── SCROLLABLE DETAIL ───────────────────┤
│  ▸ Infected by Age Group                                │
│  ▸ Lockdown Families                                    │
│  ▸ Top Spreaders                                        │
│  ▸ Top Origin Locations                                 │
│  ▸ Origin Distribution                                  │
│  ▸ District Infection Rates                             │
│  ▸ Pandemic Settings                                    │
└─────────────────────────────────────────────────────────┘
```

---

## 6. Control Buttons — Row 1

These five buttons are the main pandemic policy controls. They are always at the top of the header.

---

### `Start` / `Restart`

**What it does:**  
- When no pandemic is running, this button reads **"Start"** (blue). Clicking it seeds the initial infections into the population and begins the simulation.  
- When a pandemic is already running, the button changes to **"Restart"** (darker blue). Clicking it shows a confirmation dialog: *"Discard the current pandemic run and restart it with the current in-game settings?"* Confirming wipes all current infection data and begins a fresh run.

**When to use it:**  
Click **Start** once after loading your city to begin the pandemic. Use **Restart** if you have changed settings and want to see how differently the outbreak evolves.

> **Note:** On restart, all citizens are healed, the observation log is cleared, and the simulation begins again with a fresh seed based on the `DiseaseStartInfectionRatio` setting.

---

### `Stop`

**What it does:**  
Ends the pandemic run immediately. All currently sick citizens are healed, public transport is restored to its normal state, and the dashboard metrics are frozen at their final values.

**When to use it:**  
Use this to end a simulation run cleanly before starting a new one, or to reset the city to a healthy state.

> The button is **greyed out and unclickable** when no pandemic is running.

---

### `Masks`

**What it does:**  
Toggles the city-wide mask mandate **on** or **off**.

- When **ON**: Each citizen's mask-wearing behaviour is looked up and applied. The `MaskManager` uses three sub-populations you configure (mask wearers who protect others, mask wearers who only protect themselves, and non-mask wearers) to compute adjusted infection probabilities for every encounter.
- When **OFF**: Mask effects are disabled regardless of individual citizen assignments. Everyone behaves as if unmasked.

The button label changes to show the current state, e.g. **"Masks: ON"** with a green/highlighted colour.

**What it affects in practice:**  
Activating masks can dramatically slow the spread, especially indoors and in vehicles — but the actual effect depends on how you have set the mask compliance ratios and the `TransmissionProbabilityReduction` slider in Settings.

---

### `Quarantine`

**What it does:**  
Toggles the city-wide quarantine policy **on** or **off**.

When **ON**, the quarantine system activates according to the `QuarantineBehavior` setting:
- **None** — quarantine is not enforced even if toggled on
- **Self** — only the diagnosed citizen is ordered to stay home
- **Family** — the diagnosed citizen is quarantined and housemates no longer mix inside the building
- **Contacts** — the diagnosed citizen *and* everyone the contact-tracing system has recorded as a close contact are quarantined

Citizens who test positive are automatically sent to quarantine. The quarantine lasts for the number of days defined by the quarantine duration (10 days by default).

---

### `Lockdown`

**What it does:**  
Toggles the city-wide **lockdown** on or off.

When **ON**, building categories that you have enabled in Settings are forced to close. Citizens who would normally go to those buildings are instead told to stay home. The level of restriction depends on the `LockdownBehavior` setting:
- **None** — no movement restrictions even if lockdown is toggled
- **Work** — only work trips are allowed; leisure and shopping are forbidden
- **Full** — almost all movement other than essential services is forbidden

Public transport is also affected. When a lockdown is activated, bus/tram/metro/train/ferry/plane lines begin an orderly shutdown: vehicles finish their current routes, depots deactivate, and eventually all transit stops accepting new passengers.

The per-family threshold sliders in Settings let you set automatic triggers — for example, "automatically close Education buildings when 5% of that sector's workers are infected."

---

## 7. Visualization Buttons — Row 2

The first four buttons control how the outbreak is displayed visually in the city view. The fifth opens the separate Experiment Batch Runner.

---

### `Overlays`

**What it does:**  
Toggles all in-world visual overlays **on** or **off** as a single master switch.

When overlays are **ON**, you will see:
- Biohazard icons (☣) floating above infected citizens' heads
- Warning icons (⚠) above hotspot buildings with many current infections
- Red movement trails behind walking infected citizens

When overlays are **OFF**, the city view is clean — useful for regular city-building work or if overlays cause visual clutter.

---

### `X-Ray`

**What it does:**  
Toggles the **X-Ray heatmap** on or off. The heatmap is a transparent coloured overlay that stretches across the entire city map and follows the terrain height. It lights up in areas where infected (or recovered, or dead — depending on the Type setting) citizens are concentrated.

- Red/warm areas = high concentration of the tracked metric
- No colour = none of the tracked metric in that area

This gives you an instant city-wide epidemiological view without needing to zoom in.

> The heatmap and the world overlays (icons, trails) are independent. You can have one on without the other.

---

### `Type` (X-Ray Type)

**What it does:**  
Cycles through what the X-Ray heatmap is showing. Press it repeatedly to rotate between:
- **Infected** — shows where currently sick people are (or were, depending on Basis)
- **Recovered** — shows where people who have already had the disease are located
- **Dead** — shows where deaths have occurred

The current mode is displayed in the button label, e.g. **"Type: Infected"**.

---

### `Basis` (X-Ray Basis)

**What it does:**  
Cycles through the **location basis** used for the heatmap:
- **Live Positions** — plots citizen locations based on where they physically are *right now* in the city (live movement)
- **Home Locations** — plots based on citizens' *home building addresses* regardless of where they currently are

**Live** is better for seeing active hotspots. **Home** is better for understanding which *residential neighbourhood* is hardest hit.

The current mode is displayed in the button label, e.g. **"Basis: Live"**.

> The `Type` and `Basis` buttons are **greyed out** when X-Ray is off.

---

### `Experiments`

Opens the dedicated Experiment Batch Runner. The experiment editor is a separate, draggable window so the live monitor keeps its existing layout. Closing the experiment window does **not** pause, abort, or otherwise cancel an active batch; click **Experiments** again to reopen it.

While a batch owns the current run, controls that could change experimental state are locked. Charts, metrics, overlays, X-Ray, camera-focus actions, scrolling, and panel collapse remain available. See Section 13 for the complete workflow.

---

## 8. Live Metric Cards

Below the two button rows, five small metric cards show the most important numbers at a glance. They update every few in-game minutes.

| Card | What it shows |
|---|---|
| **Sick** | Total number of currently infected citizens. The delta (change since last update) is shown in small text. |
| **Recovered** | Citizens who had the disease and survived. These are now immune. |
| **Dead** | Total deaths caused by the pandemic. |
| **Quarantine** | Number of citizens currently under a quarantine or isolation order. |
| **Hospital / Ambulance** | Current hospital capacity usage % and ambulance usage %, with their deltas. High values warn of healthcare system overload. |

---

## 9. Charts

### Trend Chart (large, left side)

This is the main epidemic curve. The X axis is simulation time; the Y axis is the count of sick citizens. A red line traces the daily infected count, with a warm area fill underneath it.

**Interactive features:**
- Hover over the chart to see a tooltip with exact values at that point in time
- **Four time-range buttons** sit above the chart:
  - `24h` — zoom to the last 24 in-game hours
  - `7d` — zoom to the last 7 in-game days
  - `30d` — zoom to the last 30 in-game days
  - `All` — show the entire pandemic run from the beginning
- **Click and drag** on the chart to define a custom zoom range; click again to reset

**Policy markers** are drawn as vertical lines on the chart whenever you toggle Masks or Lockdown on or off. This lets you see at a glance whether your interventions had an effect.

### SIDR Bar Chart (right side)

Shows the disease compartment breakdown as a vertical stacked bar and percentage bars:
- **S** (blue) — Susceptible: healthy citizens who have never been infected
- **I** (red/orange) — Infected: currently sick
- **D** (purple) — Dead
- **R** (green) — Recovered and immune

The stacked bar gives a proportional view of where the whole population sits in the disease lifecycle. An expanding red segment means the outbreak is growing; a shrinking red and growing green segment means it is resolving.

---

## 10. Detail Sections (Scrollable Area)

The lower half of the panel is a scrollable area with seven collapsible section cards. Click any section header to expand or collapse it.

---

### Infected by Age Group

Shows a breakdown of current infections by citizen age:
- **Child**
- **Teen**
- **Young Adult**
- **Adult**
- **Senior**

Each row shows the count and percentage. This tells you whether the disease disproportionately affects a particular generation (which is controlled by the age-specific death rate settings).

---

### Lockdown Families

Shows the nine building-sector categories and how many of their workers/visitors are currently infected. The categories are:

| Family | Examples of buildings included |
|---|---|
| **Education** | Schools, universities, player-owned education |
| **Public Transport** | Bus depots, train stations, metros, ferry terminals |
| **Commercial** | Shops, restaurants |
| **Leisure / Tourism / Parks** | Entertainment venues, tourist attractions, parks |
| **Office** | Office blocks, IT campuses |
| **Industry / Player Industry** | Factories, warehouses, specialised industry |
| **Government / Other Public** | City hall, police, fire, libraries |
| **Essential Services** | Hospitals, power plants, water treatment |
| **Healthcare** | Hospitals, clinics, medical facilities |

For each family you can see the infection rate and whether it is currently closed by lockdown.

---

### Top Spreaders

Lists up to **5 individual citizens** who have infected the highest total number of other people during this pandemic run. These are the superspreaders.

Each entry shows the citizen's name, how many people they have infected, and a **clickable button** that instantly moves the game camera to that citizen's current location in the city so you can watch them in real time.

> A citizen must have infected at least `SuperspreaderCitizenThreshold` (configurable) others to appear in this list.

---

### Top Origin Locations

Lists up to **5 buildings** where the highest number of new infections have originated. These are the hotspot locations.

Each entry shows the building name, infection count, and a **clickable button** that moves the camera to that building.

> A building must have triggered at least `SuperspreaderLocationThreshold` (configurable) infections to appear in this list.

---

### Origin Distribution

Shows a breakdown of *how* infections are spreading — the infection vector categories:

| Category | What it means |
|---|---|
| Initial Seed | The first infections placed by the simulation at startup |
| Residential Home | Spread within a shared household |
| Workplace / Office / Industry | Spread at a place of work |
| School / University | Spread in an education building |
| Healthcare | Spread in a hospital or clinic |
| Commercial / Leisure / Tourism | Spread in shops, entertainment, tourist sites |
| Bus / Tram / Metro / Train / Ship / Plane / Taxi | Spread in specific transit vehicle types |
| Outdoor / Street | Spread while walking in the open air |
| Stop / Platform | Spread while waiting at a transit stop |
| Other / Unknown | Anything not in the above categories |

This tells you which settings and policies to focus on. If most infections are happening on public transport, shutting transit has high impact.

---

### District Infection Rates

If your city has districts defined, this section shows the infection rate (infected as a % of population) for each district. This helps you identify geographic hotspots and potentially apply targeted policy thinking.

---

### Pandemic Settings

This section exposes all configurable parameters of the pandemic engine directly in the dashboard. You do not need to go to the main mod settings menu. Changes take effect for the *next* simulation run (use Restart to apply them). For details on every setting, see Section 11 below.

---

## 11. In-Dashboard Settings — Every Setting Explained

Settings are grouped by topic. They are all accessible in the **Pandemic Settings** card at the bottom of the scrollable area.

---

### Disease Properties

These settings define the biological characteristics of the disease.

| Setting | What it does | Default range |
|---|---|---|
| **Disease Duration** | How many in-game days a citizen stays infected before recovering or dying | 0–28 days |
| **Detection Time** | How many days after infection until the disease can be detected by a test | 0–28 days |
| **Start Symptoms** | Day number after infection when symptoms first appear | 0–28 days |
| **End Symptoms** | Day number after infection when symptoms resolve | 0–28 days |
| **Start Infection** | Day number after infection when the citizen becomes *infectious* to others | 0–28 days |
| **End Infection** | Day number after infection when the citizen is no longer able to infect others | 0–28 days |
| **Indoor Transmission Probability** | Base % chance per step that an infected person passes the disease to someone they share a building with | 0.0–10.0 |
| **Outdoor Transmission Probability** | Base % chance per step that an infected person passes the disease to someone nearby outdoors | 0.0–10.0 |
| **Disease Transmission Range** | How many in-game metres of outdoor proximity count as potential exposure | 0–5 |
| **Disease Start Infection Ratio** | What % of the population starts the simulation already infected | 0–100% |

---

### Symptoms — Death Rates

Age-group-specific probability that an infected citizen of that age will die (rather than recover).

| Setting | Target group |
|---|---|
| **Death: Child** | Citizens in the child age group |
| **Death: Teen** | Teenage citizens |
| **Death: Young** | Young adult citizens |
| **Death: Adult** | Adult citizens |
| **Death: Senior** | Senior citizens |
| **Symptom Probability** | General probability (0–100%) that a sick citizen will display visible symptoms (which triggers testing) |

Raising the Senior rate and lowering others simulates a disease that preferentially kills the elderly.

---

### Masks

| Setting | What it does |
|---|---|
| **Transmission Probability Reduction** | The multiplier applied to infection probability per "mask level." A value of 5 means each mask worn (by infector or receiver) divides the chance by 5. Range: 1–20. |
| **Ratio: Ignore Masks** | % of citizens who will never wear a mask. These people never benefit from or contribute to mask protection. |
| **Ratio: Other Protection Mask** | % of citizens who wear a mask that protects *other people* (i.e., they reduce the chance of spreading, like a surgical mask worn correctly). |
| **Ratio: Own Protection Mask** | % of citizens who wear a mask that protects *themselves* (reduces their own chance of being infected). |
| **Mask Behavior** | Sets *where* masks are enforced when the mandate is active. Options: **None** (no effect), **Building** (only indoors), **Vehicle** (indoors + transit), **Full** (everywhere including outdoors). |

> The three ratio sliders (Ignore, Other Protection, Own Protection) must sum to exactly 100%. Batch preflight rejects any other sum; it is never silently normalised. Legacy `30/50/50` weights are migrated to the nearest integer effective percentages `23/39/38`, preserving the old weighted assignment as closely as integer sliders allow.

---

### Contact Tracing

| Setting | What it does |
|---|---|
| **Building Contact Tracing Probability** | % of citizens who have their in-building contacts logged (simulates businesses that keep visitor registers). Range: 0–100. |
| **App-Based Contact Tracing Probability** | % of citizens using a phone contact-tracing app. These citizens have all their contacts logged regardless of location. Range: 0–100. |

Higher values mean more contacts can be identified and quarantined when someone tests positive, reducing onward spread.

---

### Testing

| Setting | What it does |
|---|---|
| **Relative Test Capacity** | Test slots per seven simulation days as a percentage of the tracked population. Range: 0–100. |
| **% of Tests Reserved for Sick Citizens** | How many of the available tests are prioritised for symptomatic citizens. The rest go to asymptomatic screening. Range: 0–100. |
| **Maximum Test Duration** | Maximum request-to-sample wait. A request that cannot be sampled within this many days expires. Range: 0–28. |
| **Minimum Test Duration** | Sample-to-result delay in simulation days. Until that timestamp the result remains `Unknown`. Range: 0–28. |
| **Sensitivity** | Probability that a detectable infected sample produces a positive result. Before `Detection Time`, sensitivity is not applied. |
| **Specificity** | Probability that an uninfected sample produces a negative result. |
| **Quarantine while awaiting result** | Applies the same precautionary rule to all pending samples; the engine does not inspect their future result. |
| **Retest interval** | Minimum interval after sampling before another request for that citizen is accepted. |
| **Epidemic step minutes** | Fixed simulation-time step for the epidemiological kernel. A missed step invalidates a scientific batch run. |

---

### Quarantine — Citizen Behavior

| Setting | What it does |
|---|---|
| **Quarantine Behavior** | Scope of restrictions: **None**, the case itself, household contacts, or traceable contacts according to the selected mode. Household transmission remains possible while external contacts are restricted. |
| **Only Tested Citizens to Quarantine** | If checked, only citizens with a confirmed positive test are sent to quarantine. If unchecked, citizens with visible symptoms can also be quarantined even without a positive test. |

---

### Quarantine — Lockdown Behavior

| Setting | What it does |
|---|---|
| **Lockdown Behavior** | Sets citizen movement restrictions during lockdown: **None** (no restrictions), **Work** (only work trips allowed), **Full** (all non-essential movement forbidden) |

---

### Pandemic Lockdown — Families

For each of the eight controlled sector families (Education, Public Transport, Commercial, Leisure/Tourism/Parks, Office, Industry, Government/Other Public and Essential Services), the policy contains:

| Setting | What it does |
|---|---|
| **Close [Family] During Lockdown** | Checkbox. When ticked and lockdown is active, buildings in this family are forcibly closed. |
| **Close [Family] Threshold %** | If greater than 0, buildings in this family automatically close once the infection rate among their occupants/workers exceeds this percentage, even *without* a full lockdown being declared. Set to 0 to disable automatic closure. |
| **Reopen [Family] Threshold %** | Reopens only at or below this threshold; it must not exceed the close threshold. |
| **Minimum Closure Duration** | Minimum simulation days before reopening is allowed. |
| **Cooldown Duration** | Minimum simulation days after a state change before another change. |

This lets you design nuanced policies: for example, close schools and transit during lockdown, but leave essential services and healthcare open always.

---

### Pandemic Monitor — Overlays

| Setting | What it does |
|---|---|
| **Hub Highlight Threshold** | How many concurrent infected citizens must be present in a building at once for it to receive the warning icon (⚠) overhead. Range: 2–30. Lower = more buildings highlighted. |

---

### Pandemic Monitor — Superspreaders

| Setting | What it does |
|---|---|
| **Superspreader Citizen Threshold** | Minimum number of people a citizen must have personally infected to appear in the "Top Spreaders" list. Range: 2–20. |
| **Superspreader Location Threshold** | Minimum number of infections triggered at a building for it to appear in the "Top Origin Locations" list. Range: 2–50. |

---

## 12. Visual Overlays in the City

When **Overlays** are enabled, three distinct visual layers are drawn over the normal city view:

### Biohazard Icons above Citizens (☣)

Each infected citizen who is currently walking around the city has a small yellow biohazard symbol floating above their head. The icons face the camera (they billboard). They are only visible within approximately **900 metres** of the current camera position.

### Warning Icons above Buildings (⚠)

Buildings that are considered **hotspots** (currently containing a large number of infected citizens, above the Hub Highlight Threshold) display an orange warning icon overhead. Ordinary infected residential buildings show a biohazard icon (☣) scaled by the number of infected residents inside. These are visible within approximately **1800 metres** of the camera.

### Red Movement Trails

Infected citizens who are walking leave a semi-transparent bright red trail behind them (up to 30 points of path history). The trail fades as they move. This makes it visually easy to see the movement paths of spreaders. Trails are visible within **1000 metres** of the camera.

### X-Ray Heatmap

A terrain-following semi-transparent mesh covers the entire city map. It glows red/warm in cells where the selected metric (Infected / Recovered / Dead) is concentrated, based on either live positions or home addresses. The heatmap is divided into a 128×128 cell grid across the 17 km² map. Cells with no matching citizens are fully transparent.

> All overlays automatically disable when the Overlays button is toggled off, or when no pandemic run is active.

---

## 13. Automated Experiment Batch Runner

The Experiment Batch Runner executes repeated scientific scenarios entirely inside the mod. It does not require the Python dashboard, a command-line script, or another controller process. Each repetition starts by loading the same selected baseline save, applies a captured scenario and deterministic seed, runs for the configured amount of simulation time, exports into a unique directory, and then reloads the baseline for the next repetition.

### Baseline safety

- Select the baseline from the in-game save catalog; never type a filesystem path.
- The selector shows identifying save metadata and stores the save asset's stable identity, not only its display name.
- **Run 1 is reloaded from disk too.** The currently loaded in-memory city is never assumed to equal the saved baseline.
- Starting a batch warns that unsaved changes in the current city will be discarded and requires explicit confirmation.
- TENUS never calls `SaveLevel` for an experiment and never overwrites or silently updates the baseline `.crp`.
- If the exact stored asset or its validated fingerprint cannot be resolved, the batch stops. It never falls back to the latest save or another save with the same visible name.

The stored identity includes the metadata asset `fullName`, checksum, size, asset type and enabled state; package name, path, version and published-file ID; city timestamp, environment and map theme; the corresponding data-asset identity; and, for a local file, length, last-write time in UTC and SHA-256. These values let TENUS detect a missing or changed baseline before mixing results from different city states.

### Exact save catalog and load path

The normal Cities: Skylines saved-game API is used. TENUS enumerates metadata assets with:

```csharp
PackageManager.FilterAssets(new[] { UserAssetType.SaveGameMetaData })
```

It resolves the selected asset on every run by its stored full name:

```csharp
PackageManager.FindAssetByName(fullName, UserAssetType.SaveGameMetaData)
```

After validating the metadata, package, data asset and available file fingerprints, it requests the normal game load with this API shape:

```csharp
LoadingManager.instance.LoadLevel(
    metadataAsset,
    "Game",
    "InGame",
    new SimulationMetaData
    {
        m_CityName = saveMetadata.cityName,
        m_updateMode = SimulationManager.UpdateMode.LoadGame,
        m_MapThemeMetaData = resolvedMapThemeMetadata,
        m_disableAchievements = disableAchievementsForPublishedPackage
    },
    false);
```

The map-theme metadata and published-package achievement flag are resolved in the same way as the stock Load Game panel. TENUS does not call `UnloadLevel` first, does not use editor modes, and does not use `GetLatestSaveGame()`.

### Create a batch

1. Configure TENUS and the pandemic policies for the first scenario in the normal UI.
2. Click **Experiments** and enter a batch name.
3. Select a baseline save and use **Refresh** if the save list changed. There is deliberately no “currently loaded city” shortcut because the game does not expose a reliable originating asset or unsaved-change check.
4. Click **Add current settings as scenario**. This captures values, not a reference to the live configuration.
5. Give the scenario a name and choose its run count, duration, end mode and seed strategy.
6. Repeat for additional scenarios. Scenarios can be duplicated, removed, updated from current settings, or moved up and down.
7. Review the scenario count, total runs, total configured simulation days, output path, execution speed and **Return to baseline after completion** option.
8. Click **Start Batch**, pass preflight validation, read the unsaved-city warning, and confirm.

Scenario order is deterministic and never interleaved: all runs of Scenario 1 execute before Scenario 2. Run numbers shown in the UI and output are one-based.

### What a scenario captures

A scenario is an immutable typed snapshot of simulation-affecting RealTime and Pandemic settings. It includes disease timing and transmission, initial infection ratio, masks, testing, contact tracing, quarantine, lockdown and family closures, transport intervention state, healthcare parameters, movement/time/weekend behavior, stable-city controls, and the initial runtime policy state. Presentation preferences such as language, notifications, panel layout, chart range, overlay selection and X-Ray selection are not experimental inputs.

After each baseline reload, TENUS waits for the level, `RealTimeCore`, `PandemicManager`, `SimulationManager` and required game connections to become ready. It pauses the simulation, applies the scenario, reinitializes cached manager values, applies runtime policies and seeds, and verifies every effective value before starting. A mismatch fails safely; the pandemic is not started with a partial scenario.

Temporary scenario values are experiment overrides. The original user configuration is captured before the batch, is not saved as the user's default during reloads, and is restored on completion or abort.

### Duration and end modes

Duration is Cities: Skylines simulation time, not wall-clock time or frame count. The target is calculated from the actual run start:

```text
target game time = run start game time + configured simulation days
```

Positive decimal durations are accepted, which makes short integration tests such as `0.05` day practical. The default is 30 days.

- **Fixed duration** (default): runs to the target time even if exposed and infectious counts reach zero early. Observation data continues through the configured endpoint.
- **Duration or epidemic extinction**: finishes when either the target time is reached or the epidemic becomes extinct.

The legacy manual workflow keeps its existing approximately 30-day limit and natural-burnout behavior. Batch duration policy does not replace manual policy.

The execution-speed setting either preserves the current supported game-speed behavior or requests a supported Cities: Skylines simulation speed after each reload. Speed is recorded as execution metadata, not treated as an epidemiological parameter. Preparation, finalization, export and reload happen while paused.

### Seeds and reproducibility

- **Fixed** uses the configured master seed for every repetition.
- **Sequential** uses `first seed + zero-based run offset`, so a first seed of `10001` produces `10001`, `10002`, and so on.
- **Paired seed mode** gives the same run index in every scenario the same master seed and `pair_id`, enabling within-pair scenario differences.
- FNV-1a v1 derives independent streams named `initial-population`, `disease-progression`, `transmission`, `symptom`, `mortality`, `testing`, `contact-tracing`, and `intervention`. Legacy `pandemic`, `mask`, `contact`, and `test` derivations remain in the manifest for schema compatibility.
- Stable per-citizen mask, tracing-app and manual-tracing assignments hash the master seed, citizen ID and feature namespace. Query order cannot alter these baseline traits.
- Retrying an incomplete run reuses the same scenario/run index and seed.

Completed manifests record the master and every component seed. TENUS never reseeds the game randomizer, Unity's global RNG, Real Time's game randomizer, or another mod. Identical inputs reproduce TENUS-owned decisions; bit-identical whole-game execution is **not** guaranteed because the host game and other mods have independent state and ordering.

### Progress, pause, abort and recovery

The panel shows batch/scenario/run progress, simulation day versus target, seed, baseline, output path, last completed run and any error. Closing the panel never stops the controller.

- **Pause Batch** pauses the game and preserves the current run exactly; **Resume Batch** continues it.
- **Abort Batch** requires confirmation, preserves completed output, leaves the current run incomplete, restores the original configuration and control availability, and follows the selected return-to-baseline behavior.
- A failed export leaves the game paused and offers **Retry export** or **Abort batch**. A failed baseline load offers **Retry load** or **Abort batch**. Runs are never skipped silently.
- A planned reload is identified by a persisted reload token/generation. Loading another city, returning to the menu, or another unexpected unload marks the batch **Interrupted** instead of applying experiment settings to the wrong city.
- After a normal batch-requested reload, execution resumes automatically when the exact baseline is ready.
- After the whole application restarts, an unfinished batch is detected but does not run automatically. The recovery prompt offers **Resume from baseline** or **Abort**; resuming restarts the incomplete run from the baseline with the same seed.
- Corrupt or incompatible persisted state is reported and never guessed. Existing completed result directories remain untouched.

While a batch owns a run, Start/Restart, Stop, intervention toggles, per-citizen mutation actions and simulation-affecting settings are locked with a `Controlled by active experiment batch` explanation. Read-only analytics and visual controls remain usable. Normal controls are restored when the batch completes or is aborted.

---

## 14. Data Export Files

Manual runs keep the established rich and raw export formats. Batch runs use the same scientific data in isolated per-run directories and add versioned manifests and a batch summary.

### Manual rich CSV

Each completed or manually stopped run writes a uniquely timestamped file beneath:

```text
...\Addons\Mods\RealTime\Pandemic Data\pandemic_run_<timestamp>.csv
```

The sectioned CSV remains compatible with existing analysis. Its source-authoritative sections include `RUN METADATA`, `CORE METRICS`, `POLICY STATE`, `AGE GROUPS`, `LOCKDOWN FAMILIES`, `INFECTION ORIGINS`, `DISTRICT INFECTION RATES`, `TOP SPREADERS`, `TOP ORIGIN LOCATIONS`, `SEIRD TIME SERIES`, `POLICY TIMELINE`, `HEALTHCARE TIME SERIES`, `PEAK STATISTICS`, `PANDEMIC SETTINGS`, `INFECTION ORIGINS TIME SERIES`, and `CITIZEN LOCATIONS TIME SERIES`. Additive experiment metadata does not rename or remove these sections.

### Legacy raw manual files

The following root-level files remain available for backward compatibility.

### `data.csv`

Located at: `...\Addons\Mods\RealTime\data.csv`

Contains two sections:

**SIDR time series** — one row per observation tick:
```
time_ms;healthy;sick;recovered;dead
```
Each row is a snapshot of the four population counts at a given simulation time (in milliseconds).

**Infection chain log** — for each citizen who caused infections while sick:
```
<citizen_id>
;<infected_citizen_id>;<infection_type>;<origin_category>;<time_ms>;<position>;<building_type>;<building_id>;<vehicle_id>
```
This represents the full transmission tree: who infected whom, when, where, in what type of building or vehicle.

### `contacts.csv`

Located at: `...\Addons\Mods\RealTime\contacts.csv`

Contains the contact tracing log — every recorded contact pair:
```
citizen_id;contact_id;last_contact_time_ms
```
Plus summary statistics at the bottom:
```
#tracked_citizens;N
#tracked_pairs;N
#total_recorded_contacts;N
#recorded_building_contacts;N
#recorded_non_building_contacts;N
```

These files can be opened in Excel, Google Sheets, or analysed with Python/R to study the spread patterns after a run.

### Batch output layout

Every batch has a unique ID in addition to its readable name. Every scenario also has an internal ID, and every run has its own directory. Internal IDs, rather than timestamps alone, provide collision resistance.

```text
Pandemic Data/
└── Experiments/
    └── <chosen-folder>_<UTC timestamp>_<short-batch-id>/
        ├── batch_manifest.json
        ├── batch_state.json
        ├── batch_runs.csv
        ├── 001_Baseline_<short-scenario-id>/
        │   ├── scenario.json
        │   ├── run_001/
        │   │   ├── run_manifest.json
        │   │   ├── pandemic_run_Baseline_001.csv
        │   │   ├── data.csv
        │   │   ├── contacts.csv
        │   │   ├── run_summary.csv
        │   │   ├── state_timeseries.csv
        │   │   ├── transmission_events.csv
        │   │   ├── physical_contacts.csv
        │   │   ├── traceable_contacts.csv
        │   │   ├── test_events.csv
        │   │   ├── intervention_events.csv
        │   │   ├── healthcare_timeseries.csv
        │   │   ├── population_events.csv
        │   │   └── errors.json
        │   └── run_002/
        └── 002_Masks_<short-scenario-id>/
            └── ...
```

The readable folder prefix is editable before confirmation; sanitisation and the timestamp/short ID preserve uniqueness. Each scenario gets its own ordered folder, so three scenarios with ten repetitions produce three scenario folders containing ten run folders each. The rich CSV remains available, while the scientific files are authoritative for reproducible analysis. No run overwrites another run, scenario or older batch.

`batch_manifest.json`, `scenario.json`, `batch_state.json` and `run_manifest.json` are versioned machine-readable records. A completed run manifest identifies the baseline and validated fingerprints, batch and scenario, run numbers, settings and initial policy state, configured duration and end mode, master/component seeds, simulation and UTC times, mod/game/config versions, execution speed, status and generated files. `batch_runs.csv` receives one row only after a run exports successfully and provides compact outcomes and the relative rich-CSV path for later statistics.

Important state and metadata files are written through a temporary file and atomically published. A run stays in `run_NNN.__inprogress_<attempt-id>` until every mandatory export is closed, hashed and verified. A crash or export error therefore cannot make a partial run look completed. Recovery completes a valid interrupted commit or repeats an incomplete run with the same seed; it never overwrites a completed directory.

---

## 15. Optional Analytics Dashboard

The external Dash application is optional analysis software. It is never required to create, start, pause, resume, recover or complete a batch.

From the repository's `dashboard` directory, install the pinned packages in `requirements.txt` and run:

```powershell
.\run_dashboard.ps1
```

Then open `http://localhost:8050`. By default the application reads:

```text
%LOCALAPPDATA%\Colossal Order\Cities_Skylines\Addons\Mods\RealTime\Pandemic Data
```

Set `PANDEMIC_DATA_DIR` before launch to analyse a different root. The dashboard recursively discovers both top-level manual `pandemic_run_*.csv` files and completed files nested under `Experiments`. Temporary and in-progress paths are ignored. When a sibling `run_manifest.json` is available, selectors use a label such as `MasterThesis_Main / Masks / Run 7`; malformed or missing optional label metadata falls back to a readable relative path.

Direct CSV drag-and-drop/upload remains supported, as do the existing charts and full summary. Multi-run scenario aggregation reports mean, median, standard deviation, minimum, maximum, 5th/25th/75th/95th percentiles and IQR. When valid `pair_id` values exist, it additionally reports per-pair differences plus their mean and median. Invalid/failed runs and in-progress directories are excluded. The parser continues to accept old manual files without batch metadata.

---

## 16. In-Game Verification Checklist

Because saved-game loading, Unity lifecycle callbacks and other mods exist only inside Cities: Skylines, complete acceptance includes an in-game pass.

### Legacy/manual regression

- Load a normal city and verify the Experiment button is the only intended live-panel layout addition.
- Start, restart and stop a manual pandemic; toggle masks, quarantine and lockdown.
- Exercise overlays, X-Ray, chart ranges, citizen/location focus and the in-panel settings editor.
- Confirm the existing manual 30-day endpoint and natural-burnout behavior.
- Confirm the manual rich CSV and legacy `data.csv`/`contacts.csv` load in existing analysis workflows.

### Short deterministic batch

Create Scenario A and Scenario B with two runs each and a duration of `0.05` simulation day. Verify this exact order:

```text
A / Run 1 -> baseline reload
A / Run 2 -> baseline reload
B / Run 1 -> baseline reload
B / Run 2 -> complete
```

Confirm that Run 1 also began with a fresh baseline load, all four runs began from the same saved city state, A settings were reapplied for A2, B settings were applied for B1/B2, seeds match the configured strategy, and four unique completed run directories exist.

### Safety and recovery

- Close and reopen the Experiment panel during a run; execution must continue exactly once.
- Pause and resume without advancing or resetting the run.
- Attempt every locked mutation control and verify read-only analytics remain usable.
- Abort midway and verify completed runs remain valid while the current run is not counted.
- Interrupt a run with an unexpected load and verify **Interrupted**, then resume from the designated baseline.
- Restart the whole game and verify the recovery prompt appears without automatically loading or starting a city.
- Test a missing or changed baseline and verify no latest/name-only fallback occurs.
- Cause an export failure where practical; verify the game stays paused and Retry/Abort is offered.
- Complete a batch with return-to-baseline enabled and verify no pandemic starts after the final clean reload.
- Load both manual and nested batch CSVs in the optional dashboard; confirm in-progress output is absent.

---

## 17. Frequently Asked Questions

### "I can't see the Pandemic panel in the game."

- Make sure the mod is enabled in **Content Manager → Mods** before loading a city.
- The panel only appears during an active game (not in the main menu or map editor).
- If the panel was collapsed, click the title bar area at the top-right of the screen to re-expand it.

### "The Start button is greyed out / does nothing."

- Wait a moment for the city to fully load before clicking Start. The mod initialises shortly after the map finishes loading.
- Check the game's output log for any error messages from the mod (see `output_log.txt` in the repo root, or the game's own output log in `AppData\Local\Colossal Order\Cities_Skylines\`).

### "The build fails with 'Assembly-CSharp.dll not found'."

- The `CITIES_SKYLINES_BINARIES` environment variable is not set, or points to the wrong folder.
- The correct folder is inside your Steam Cities: Skylines installation: `Cities_Skylines\Cities_Data\Managed\`
- See Section 4.2 for how to set environment variables.

### "The mod built fine but the changes are not showing in the game."

- Make sure the game is **closed** before copying or building, as the game holds a lock on the DLL.
- Verify the DLL was copied to the correct path: `Addons\Mods\RealTime\RealTime.dll`
- Disable and re-enable the mod in Content Manager, then reload the city.

### "Quarantine doesn't seem to do anything."

- The `QuarantineBehavior` setting must be set to something other than **None**.
- Citizens only get quarantined once they test positive (if "Only Tested Citizens to Quarantine" is on) or show symptoms.
- The Testing capacity slider controls how quickly tests are processed. If it is near 0, no one is being tested.

### "The outbreak isn't growing at all."

- Check that `DiseaseStartInfectionRatio` is greater than 0 — this controls how many citizens start infected.
- Check that `IndoorDiseaseTransmissionProbability` and `OutdoorDiseaseTransmissionProbability` are greater than 0.
- If Masks is ON and `TransmissionProbabilityReduction` is very high, the disease may be effectively contained from the start.

### "Where are the CSV files after a run?"

- Manual rich CSVs are stored in `RealTime\Pandemic Data\` with unique timestamped names.
- Legacy manual `data.csv` and `contacts.csv` remain in the `RealTime` mod root and may be replaced by a later manual run.
- Batch results are isolated below `RealTime\Pandemic Data\Experiments\<batch>\<scenario>\run_NNNN_seed_<seed>\` and are never intentionally overwritten.

### "Can I run multiple pandemic scenarios and compare them?"

- Yes. Capture each configuration as a scenario in **Experiments**, choose its repetitions, duration and seeds, and press **Start Batch** once.
- TENUS reloads the exact baseline, applies settings, runs and exports every repetition automatically.
- Use `batch_runs.csv`, the per-run rich CSVs, or the optional dashboard's multi-run comparison afterward.

### "Will a batch overwrite my baseline save?"

No. The Batch Runner only loads the selected baseline. It does not silently save the current city, call `SaveLevel`, autosave experiment endpoints into the baseline, or replace the `.crp`. The confirmation warning exists because unsaved in-memory changes are discarded by the first reload.

### "Why did my unfinished batch not resume automatically after restarting the game?"

Automatic continuation is limited to reloads deliberately requested by the running batch. After a full application restart, TENUS requires an explicit choice in the recovery prompt. Choose **Resume from baseline** to repeat the incomplete run with its original seed, or **Abort** to preserve completed output and restore normal configuration.

---

## 18. Scientific Kernel and Reproducibility Reference

### Scope and scientific status

TENUS is a reproducible agent-based scenario simulator, not a validated forecasting model. Defaults introduced for compatibility reproduce prior deterministic behaviour where practical. Contact caps, shared-area multipliers, disease distributions, infectiousness profiles, mortality multipliers and calibration targets remain explicit **uncalibrated model parameters** until compared with an identified external data source. A plausible implementation is not evidence of empirical validity.

### Disease, mortality and healthcare contracts

- `DiseaseStateEngine` is the authority for one primary state per logical citizen. Only `S -> E -> I -> PostInfectiousIll/R/D` forward progress is possible; recovered and dead citizens cannot be exposed again.
- A successful non-seed `S -> E` transition produces exactly one transmission event. Initial seeds have their own count and never increment secondary transmissions, origin transmissions or superspreader counts.
- `DiseaseProgressionEngine` samples each personal timeline from the dedicated progression stream. All final boundaries are finite, nonnegative and ordered. Initial seed age can be fixed or distributed and is clamped before recovery.
- Mortality is a step hazard derived from the configured age-course probability over the unresolved mortality window. `AsymptomaticMortalityMultiplier` and healthcare warning/critical multipliers are explicit. Test or quarantine state never decides death.
- Hospitalization totals count actual admissions (currently hospitalized or discharged), not merely seeking care or care that was unavailable.

### Testing workflow

The state sequence is `Requested -> Queued -> Scheduled -> SampleTaken -> ResultPending -> ResultAvailable`, with `Expired` and `Cancelled` terminal alternatives. Capacity is split into symptomatic-reserved and routine slots. `MaximumTestDuration` is request-to-sample expiry; `MinimumTestDuration` is the sample-to-result delay. The hidden pending result remains inaccessible until `ResultAvailableAt`. Detection time, sensitivity and specificity are evaluated from the disease state at sample time. Negative tests may be repeated after `RetestIntervalDays`; an old positive stops blocking after its configured isolation window.

### Contact and tracing semantics

Physical contact, traceability and transmission are distinct layers. Contact pairs are canonical (`min(id), max(id)`) and multiple encounters at different simulation steps are preserved. App traceability requires both citizens to be stable app users. Manual traceability is evaluated independently and cannot combine one app-only citizen with one manual-only citizen. Household mixing is explicit; school, university, workplace, healthcare, commercial, leisure, transit and residential shared-area mixing is deterministically bounded. Outdoor contacts use spatial buckets, not a population-wide loop.

When an intervention prevents a sampled external encounter, `contacts_prevented_by_intervention` counts that directly suppressed modeled pair. It is not an estimate of real-world counterfactual contacts. Household pairs are never suppressed merely because a member is isolated or quarantined.

### RNG architecture and paired runs

Seed derivation is FNV-1a 32-bit with prefix `TENUS-RNG-v1\0`, four little-endian master-seed bytes and the exact ASCII component tag. The result is masked to a nonnegative `Int32`. Independent streams isolate initial population sampling, disease progression, transmission, symptoms, mortality, testing, contact tracing and intervention decisions. Retrying a run reuses the same seed set. Paired mode aligns `pair_id` and the master-seed list across scenarios. It supports controlled comparisons but does not make Cities: Skylines or third-party mods deterministic.

### New scientific configuration parameters

The versioned configuration and immutable scenario snapshot include:

- Testing: `TestSensitivityPercent`, `TestSpecificityPercent`, `QuarantineWhileAwaitingTestResult`, `RetestIntervalDays`, `EpidemicStepMinutes`.
- Population lifecycle: `StrictPopulationIntegrity` (default `false`). Game-observed Tourist/Commuter departures and resident emigration are recorded and allowed; when enabled, only a reconciliation-only disappearance or reused slot without an observed lifecycle release invalidates a scientific batch run.
- Contact model: `MaxContactsPerPersonPerStepSchool`, `...Workplace`, `...Commercial`, `...Healthcare`, `...Transit`, `...ResidentialSharedArea`, and `ResidentialSharedAreaTransmissionMultiplier`.
- For each lockdown family: `Reopen...ThresholdPercent`, `Minimum...ClosureDurationDays`, and `...LockdownCooldownDurationDays` in addition to the existing close threshold.
- Each of exposed duration, infectious start, infectious end, symptom start, symptom end and recovery: distribution type, arithmetic mean days, standard-deviation days, minimum days, maximum days and deterministic fixed days.
- Infectiousness: `InfectiousnessProfileType`, start multiplier, peak-time fraction, peak multiplier and end multiplier.
- Initial cases: `InitialSeedSamplingStrategy`, `InitialInfectionAgeMode`, plus distribution type/mean/standard deviation/minimum/maximum/fixed days for infection age.
- Mortality/healthcare: `AsymptomaticMortalityMultiplier`, warning and critical usage thresholds, and warning and critical mortality-hazard multipliers.
- Batch: `PairedSeedMode`, `StopBatchOnRunFailure`, readable `OutputFolderName`, configuration hash and embedded Git identity.

All probability percentages accept a genuine zero. Batch preflight rejects non-finite values, invalid timelines or bounds, unsupported enums, mask ratios not summing to 100, invalid lockdown hysteresis, zero step size, zero contact caps and seed overflow. It does not reinterpret zero as “missing”.

### Scientific run files and columns

Every successful batch run contains one rich `pandemic_run_*.csv`, legacy `data.csv`/`contacts.csv`, `run_manifest.json`, and these mandatory scientific files:

```text
run_summary.csv
initial_seed_count,secondary_transmissions_total,cumulative_infections,active_exposed,active_infectious,active_post_infectious_ill,active_symptomatic,recovered_total,deaths_total,hospitalizations_total,tracked_population,final_incidence_per_100000_per_interval,final_prevalence_pct,attack_rate_pct,resolved_case_fatality_ratio_pct,rt,rt_method,empirical_secondary_infections_per_infector,actual_mask_usage_pct,total_isolation_person_days,total_quarantine_person_days,tests_requested,tests_performed,tests_positive,tests_negative,mean_test_wait_days,median_test_wait_days,contacts_prevented_by_intervention,physical_contacts_total,traceable_contacts_total,household_contacts_total,work_contacts_total,school_contacts_total,transit_contacts_total,added_population,removed_population

state_timeseries.csv
simulation_time,pandemic_day,susceptible,exposed,infectious,post_infectious_ill,symptomatic,recovered,dead,removed,tracked_population,initial_seed_count,secondary_transmissions_total,new_exposures_per_interval,hospitalizations_total,isolated_citizens,quarantined_citizens,incidence_per_100000_per_interval,prevalence_pct,attack_rate_pct

transmission_events.csv
event_id,simulation_time,pandemic_day,source_citizen_id,target_citizen_id,source_infection_age_days,target_previous_state,target_new_state,context,origin_category,building_id,vehicle_id,district_id,source_mask_type,target_mask_type,transmission_probability,source_probability,infectiousness_multiplier,is_initial_seed

physical_contacts.csv
contact_id,start_time,end_time,duration_minutes,citizen_a,citizen_b,context,building_id,vehicle_id,district_id,position_x,position_y,position_z,distance,traceable_by_app,traceable_by_manual

traceable_contacts.csv
contact_id,start_time,end_time,duration_minutes,citizen_a,citizen_b,context,building_id,vehicle_id,district_id,traceable_by_app,traceable_by_manual

test_events.csv
test_id,citizen_id,request_time,scheduled_time,sample_time,result_available_time,state,result,reason,priority,wait_duration_days,result_duration_days,citizen_disease_state_at_sample,infection_age_at_sample_days

intervention_events.csv
event_id,simulation_time,pandemic_day,citizen_id,intervention_type,action,reason,context

healthcare_timeseries.csv
simulation_time,pandemic_day,hospital_usage_pct,ambulance_usage_pct

population_events.csv
event_id,simulation_time,pandemic_day,citizen_id,action,population_category,count,reason
```

`errors.json` contains `schemaVersion`, `status`, and the structured `errors` list. `run_manifest.json` contains the full baseline identity/fingerprint, scenario snapshot, zero- and one-based run positions, pair ID, master and derived seeds, algorithms, configuration SHA-256, Git commit/branch, mod/game/schema versions, timing, final metrics, output root and a length/SHA-256 entry for every generated file.

The rich CSV keeps all legacy sections and adds `[BATCH METADATA]`. Its age section separates `share_of_infections_pct` from `infection_prevalence_within_age_group_pct`; they are not interchangeable. `resolved_case_fatality_ratio_pct` means `dead / (recovered + dead)`. TENUS does not currently have an explicit generation-interval distribution, so `rt` is empty and `rt_method` states that it is unavailable; `empirical_secondary_infections_per_infector` is exported instead.

### Invalidation and recovery rules

NaN/infinite probabilities, illegal state transitions, disease/transmission/testing/contact/intervention/metrics exceptions, unexpected population drift while `StrictPopulationIntegrity` is enabled, time moving backwards, missed strict epidemic steps, configuration drift and export-integrity failures cannot become normal successful runs. Core failures freeze diagnostics into a `run_NNN.__invalid_<attempt-id>` directory with `Status = Invalid`; they are excluded from `batch_runs.csv` and dashboard aggregation. Depending on `StopBatchOnRunFailure`, TENUS reloads the exact baseline and either stops or proceeds to the next descriptor. A failed archive/export is reported as failed and never counted as completed.

### Population and calibration

The tracked epidemiological population includes every living, non-empty citizen exposed by the game, including residents, tourists and commuters. Newly eligible citizens receive new logical IDs and start susceptible; reused game-buffer slots never inherit disease state. Additions and removals are exported with Resident, Immigrant, Emigrant, Tourist and Commuter categories where the game exposes sufficient flags. Ordinary Tourist/Commuter departures and game-observed resident emigration are recorded as `Removed` population events and never invalidate a run. Releases of already-dead citizens are recorded as `Detached` and do not inflate `removed_population`. With the default `StrictPopulationIntegrity = false`, reconciliation-only departures are also recorded and the run continues. When strict mode is explicitly enabled, a citizen disappearing or a slot being reused without the exact game lifecycle release notification invalidates the scientific batch run.

`CalibrationTargetSet` supports explicit targets, tolerances and weights for attack rate, peak prevalence, time to peak, Rt, hospitalization rate, mortality rate and household secondary attack rate. Missing observations remain unavailable and cannot pass calibration. The synthetic, Unity-independent core can execute explicit contact graphs for behavior tests and later calibration harnesses; no bundled target set is presented as medically validated.

---

*Documentation updated for the TENUS scientific kernel and Experiment Batch Runner — Cities: Skylines — August 2026*
