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
13. [Data Export Files](#13-data-export-files)
14. [Frequently Asked Questions](#14-frequently-asked-questions)

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

The mod groups citizens by where they are:
- **Outdoors** — Citizens walking on streets are grouped into a spatial grid. Any infectious citizen within a configurable distance of a susceptible citizen has a probability of transmitting the disease, calculated once per simulation step.
- **Indoors (buildings)** — Citizens who are in the same building at the same time are considered in direct contact. The indoor transmission probability is typically higher than outdoor.
- **Vehicles** — Citizens sharing a bus, tram, metro, train, ferry, or plane are treated like a shared indoor space with its own probability.
- **Household** — Citizens who share a home face an additional chronic low-level exposure even when not in the same room, modelling cohabitation risk.

All probabilities are adjusted by:
- Mask wearing (the `MaskManager` applies customisable reductions per citizen pair based on whether each person uses a mask that protects others, a mask that protects themselves, or no mask at all)
- The simulation step duration (probabilities are mathematically scaled so that longer or shorter steps produce consistent cumulative risk)

### 2.3 Disease Timeline

Each infected citizen goes through a personal timeline (all durations are configurable):
1. **Exposure** — infected but not yet infectious
2. **Infectious window** — can spread to others; starts before symptoms appear (asymptomatic spread)
3. **Symptomatic window** — citizen feels sick; triggers testing and quarantine logic
4. **Outcome** — either recovers (gains immunity) or dies, based on age-group-specific death rates

### 2.4 Policy Systems

| System | Manager class | What it does |
|---|---|---|
| Masks | `MaskManager` | Assigns each citizen a permanent random mask-wearing behaviour; adjusts transmission matrices |
| Quarantine | `QuarantineManager` | Tracks citizens under isolation orders and enforces them via the citizen AI |
| Testing | `TestManager` | Schedules tests, applies configurable delays, handles positives and contact notifications |
| Contact Tracing | `ContactManager` | Records every physical contact between pairs of citizens (building or app based); allows targeted quarantine of exposed contacts |
| Lockdown | `QuarantineManager.InLockDown` + per-family thresholds | Shuts down buildings by sector category and reroutes/withdraws public transport |

### 2.5 Analytics & Snapshots

A `PandemicObserver` records SIDR (Susceptible / Infected / Dead / Recovered) counts at every observation tick. A `PandemicLiveSnapshot` is generated on demand for the UI so that the dashboard always reflects the most current state without blocking the simulation thread.

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
│  [Overlays] [X-Ray] [Type] [Basis]                (row2)│
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

These four buttons control how the outbreak is displayed visually in the city view.

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

> The three ratio sliders (Ignore, Other Protection, Own Protection) should together represent 100% of the population. The simulation normalises them automatically if they do not add up perfectly.

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
| **Relative Test Capacity** | Maximum daily tests as a % of the city population. 5% = 5 tests per 100 citizens per 7 days. Range: 0–100. |
| **% of Tests Reserved for Sick Citizens** | How many of the available tests are prioritised for symptomatic citizens. The rest go to asymptomatic screening. Range: 0–100. |
| **Maximum Test Duration** | Maximum number of days that can pass between a test being requested and its result being delivered. Range: 0–28. |
| **Minimum Test Duration** | Minimum delay (in days) before a test result comes back. Simulates lab processing time. Range: 0–28. |

---

### Quarantine — Citizen Behavior

| Setting | What it does |
|---|---|
| **Quarantine Behavior** | Scope of isolation orders: **None** (no quarantine), **Self** (only the sick person), **Family** (sick person + no mixing with housemates), **Contacts** (sick person + all traced contacts) |
| **Only Tested Citizens to Quarantine** | If checked, only citizens with a confirmed positive test are sent to quarantine. If unchecked, citizens with visible symptoms can also be quarantined even without a positive test. |

---

### Quarantine — Lockdown Behavior

| Setting | What it does |
|---|---|
| **Lockdown Behavior** | Sets citizen movement restrictions during lockdown: **None** (no restrictions), **Work** (only work trips allowed), **Full** (all non-essential movement forbidden) |

---

### Pandemic Lockdown — Families

For each of the nine sector families (Education, Public Transport, Commercial, Leisure/Tourism/Parks, Office, Industry, Government/Other Public, Essential Services, Healthcare) there are two settings:

| Setting | What it does |
|---|---|
| **Close [Family] During Lockdown** | Checkbox. When ticked and lockdown is active, buildings in this family are forcibly closed. |
| **Close [Family] Threshold %** | If greater than 0, buildings in this family automatically close once the infection rate among their occupants/workers exceeds this percentage, even *without* a full lockdown being declared. Set to 0 to disable automatic closure. |

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

## 13. Data Export Files

When a pandemic run finishes (or is stopped), the mod writes two CSV files to its mod folder:

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

---

## 14. Frequently Asked Questions

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

- `data.csv` and `contacts.csv` are written to the mod's installation folder:
  `C:\Users\<YourUsername>\AppData\Local\Colossal Order\Cities_Skylines\Addons\Mods\RealTime\`
- They are overwritten each time a pandemic run ends, so copy them elsewhere if you want to keep them.

### "Can I run multiple pandemic scenarios and compare them?"

- Yes. After each run ends (or you click Stop), copy `data.csv` and `contacts.csv` to a different folder with a descriptive name.
- Then change your settings, click Restart, run the scenario again, and copy the new files.
- Compare the SIDR time series across runs in Excel or any analysis tool.

---

*Documentation written for the Pandemic Simulator mod — Cities: Skylines — April 2026*
