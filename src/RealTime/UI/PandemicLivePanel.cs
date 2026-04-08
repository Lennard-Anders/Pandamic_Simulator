// <copyright file="PandemicLivePanel.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.UI
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using ColossalFramework;
    using ColossalFramework.UI;
    using RealTime.Config;
    using RealTime.Pandemic;
    using SkyTools.Configuration;
    using SkyTools.Tools;
    using SkyTools.UI;
    using UnityEngine;

    internal sealed class PandemicLivePanel
    {
        private const string PanelName = "RealTimePandemicLivePanel";
        private const string RestartDialogTitle = "Restart Pandemic";
        private const string RestartDialogMessage = "Discard the current pandemic run and restart it with the current in-game settings?";
        private const float PanelWidth = 760f;
        private const float ExpandedPanelHeight = 840f;
        private const float CollapsedPanelHeight = 38f;
        private const float PanelMargin = 15f;
        private const float TopOffset = 105f;
        private const float HorizontalPadding = 12f;
        private const float HeaderTop = 36f;
        private const float HeaderHeight = 334f;
        private const float HeaderWidth = PanelWidth - (HorizontalPadding * 2f);
        private const float ButtonHeight = 28f;
        private const float ButtonSpacing = 8f;
        private const float MetricsTop = (ButtonHeight * 2f) + ButtonSpacing + 10f;
        private const float MetricCardHeight = 56f;
        private const float MetricCardGap = 8f;
        private const float ChartTop = MetricsTop + MetricCardHeight + 12f;
        private const float ChartHeight = 186f;
        private const float ChartGap = 8f;
        private const float SidrChartWidth = 212f;
        private const float TrendChartWidth = HeaderWidth - SidrChartWidth - ChartGap;
        private const float DetailTop = HeaderTop + HeaderHeight + 8f;
        private const float DetailScrollHeight = ExpandedPanelHeight - DetailTop - 10f;
        private const float ScrollbarWidth = 10f;
        private const float DetailWidth = PanelWidth - (HorizontalPadding * 2f) - ScrollbarWidth - 6f;
        private const float CardGap = 10f;
        private const float CardPadding = 8f;
        private const float CardSummaryHeight = 16f;
        private const float CardRowHeight = 18f;
        private const float CardRowSpacing = 2f;
        private const float CardButtonHeight = 22f;
        private const float CardMinHeight = 62f;
        private const float SettingsLabelWidth = 312f;

        private readonly List<PandemicSettingControl> settingControls = new List<PandemicSettingControl>();
        private readonly List<UILabel> settingsHeadings = new List<UILabel>();
        private readonly List<UILabel> ageRows = new List<UILabel>();
        private readonly List<UILabel> originRows = new List<UILabel>();
        private readonly List<UILabel> districtRows = new List<UILabel>();
        private readonly List<UILabel> lockdownRows = new List<UILabel>();
        private readonly UIButton[] spreaderButtons = new UIButton[5];
        private readonly UIButton[] locationButtons = new UIButton[5];
        private readonly Dictionary<DashboardSection, bool> sectionExpanded = new Dictionary<DashboardSection, bool>
        {
            { DashboardSection.Age, true },
            { DashboardSection.Lockdown, true },
            { DashboardSection.Spreaders, true },
            { DashboardSection.Locations, true },
            { DashboardSection.Origins, false },
            { DashboardSection.Districts, false },
            { DashboardSection.Settings, false },
        };
        private readonly UIPanel[] metricPanels = new UIPanel[5];
        private readonly UILabel[] metricTitles = new UILabel[5];
        private readonly UILabel[] metricValues = new UILabel[5];

        private CultureInfo cultureInfo = CultureInfo.CurrentCulture;
        private bool collapsed;
        private bool settingsBuilt;
        private bool layoutDirty = true;
        private bool suppressScrollbarEvent;
        private int ageVisibleRows;
        private int originVisibleRows;
        private int districtVisibleRows;
        private int lockdownVisibleRows;
        private int spreaderVisibleRows;
        private int locationVisibleRows;
        private PandemicSettingControl activeEditingControl;

        private UIPanel panel;
        private UILabel titleLabel;
        private UIPanel headerPanel;
        private UIButton startRestartButton;
        private UIButton stopButton;
        private UIButton maskButton;
        private UIButton quarantineButton;
        private UIButton lockdownButton;
        private UIButton overlayButton;
        private UIButton xrayToggleButton;
        private UIButton xrayTypeButton;
        private UIButton xrayBasisButton;
        private PandemicTrendChartView trendChart;
        private PandemicSidrBarView sidrBarView;
        private UIScrollablePanel detailScroll;
        private UIPanel detailContentPanel;
        private UIScrollbar detailScrollbar;
        private UIPanel ageCard;
        private UIButton ageToggleButton;
        private UILabel ageSummaryLabel;
        private UIPanel ageContentPanel;
        private UIPanel lockdownCard;
        private UIButton lockdownToggleButton;
        private UILabel lockdownSummaryLabel;
        private UIPanel lockdownContentPanel;
        private UIPanel spreaderCard;
        private UIButton spreaderToggleButton;
        private UILabel spreaderSummaryLabel;
        private UIPanel spreaderContentPanel;
        private UIPanel locationCard;
        private UIButton locationToggleButton;
        private UILabel locationSummaryLabel;
        private UIPanel locationContentPanel;
        private UIPanel originCard;
        private UIButton originToggleButton;
        private UILabel originSummaryLabel;
        private UIPanel originContentPanel;
        private UIPanel districtCard;
        private UIButton districtToggleButton;
        private UILabel districtSummaryLabel;
        private UIPanel districtContentPanel;
        private UIPanel settingsCard;
        private UIButton settingsToggleButton;
        private UILabel settingsSummaryLabel;
        private UIPanel settingsContentPanel;
        private UIPanel settingsPanel;

        private PandemicLiveSnapshot currentSnapshot;

        public void Enable()
        {
            UIView view = UIView.GetAView();
            if (view == null)
            {
                Log.Warning("The 'Real Time' pandemic live panel could not be created because UIView is missing.");
                return;
            }

            UIPanel existing = view.FindUIComponent<UIPanel>(PanelName);
            if (existing != null)
            {
                UnityEngine.Object.Destroy(existing.gameObject);
            }

            panel = view.AddUIComponent(typeof(UIPanel)) as UIPanel;
            if (panel == null)
            {
                Log.Warning("The 'Real Time' pandemic live panel could not create UIPanel.");
                return;
            }

            panel.name = PanelName;
            panel.width = PanelWidth;
            panel.height = ExpandedPanelHeight;
            panel.backgroundSprite = "MenuPanel2";
            panel.opacity = 0.92f;
            panel.canFocus = false;
            panel.isInteractive = true;
            panel.clipChildren = true;
            PositionPanel(view);

            titleLabel = panel.AddUIComponent<UILabel>();
            titleLabel.autoSize = false;
            titleLabel.width = PanelWidth - (HorizontalPadding * 2f);
            titleLabel.height = 20f;
            titleLabel.relativePosition = new Vector3(HorizontalPadding, 10f);
            titleLabel.textScale = 0.95f;
            titleLabel.textAlignment = UIHorizontalAlignment.Left;
            titleLabel.isInteractive = true;
            titleLabel.tooltip = "Click to collapse / expand";
            titleLabel.eventClicked += (c, e) => ToggleCollapse();

            CreateHeader();
            CreateDetailScroll();
            CreateCards();

            var updater = panel.gameObject.AddComponent<PandemicLivePanelUpdateBehavior>();
            updater.Owner = this;

            ApplyCollapsedState();
            Refresh();
        }

        public void Disable()
        {
            if (panel != null)
            {
                UnityEngine.Object.Destroy(panel.gameObject);
            }

            panel = null;
            headerPanel = null;
            detailScroll = null;
            detailContentPanel = null;
            detailScrollbar = null;
            trendChart = null;
            sidrBarView = null;
            currentSnapshot = null;
            settingsBuilt = false;
            layoutDirty = true;
            suppressScrollbarEvent = false;
            ageVisibleRows = 0;
            originVisibleRows = 0;
            districtVisibleRows = 0;
            lockdownVisibleRows = 0;
            spreaderVisibleRows = 0;
            locationVisibleRows = 0;
            settingControls.Clear();
            settingsHeadings.Clear();
            ageRows.Clear();
            originRows.Clear();
            districtRows.Clear();
            lockdownRows.Clear();
        }

        public void Translate(CultureInfo culture)
        {
            cultureInfo = culture ?? CultureInfo.CurrentCulture;
            Refresh();
        }

        internal void Refresh()
        {
            if (panel == null)
            {
                return;
            }

            UIView view = UIView.GetAView();
            if (view != null)
            {
                PositionPanel(view);
            }

            UpdateTitle();
            ApplyCollapsedState();
            if (collapsed)
            {
                return;
            }

            PandemicManager manager = PandemicManager.Instance;
            currentSnapshot = manager?.GetLiveSnapshot();
            EnsureSettingsControls();
            RefreshButtons(manager, currentSnapshot);
            RefreshSummary(currentSnapshot);
            RefreshAnalytics(currentSnapshot);
            RefreshSettingsControls();
            RefreshLayoutIfDirty();
            RefreshLiveData();
        }

        private void CreateHeader()
        {
            headerPanel = panel.AddUIComponent<UIPanel>();
            headerPanel.width = HeaderWidth;
            headerPanel.height = HeaderHeight;
            headerPanel.relativePosition = new Vector3(HorizontalPadding, HeaderTop);
            headerPanel.autoLayout = false;

            float buttonWidth = (HeaderWidth - (ButtonSpacing * 4f)) / 5f;
            float secondRowY = ButtonHeight + ButtonSpacing;

            startRestartButton = CreateActionButton(headerPanel, "Start", 0f, 0f, buttonWidth);
            startRestartButton.eventClicked += (c, e) => OnStartRestartClicked();

            stopButton = CreateActionButton(headerPanel, "Stop", buttonWidth + ButtonSpacing, 0f, buttonWidth);
            stopButton.eventClicked += (c, e) => OnStopClicked();

            maskButton = CreateActionButton(headerPanel, "Masks", (buttonWidth * 2f) + (ButtonSpacing * 2f), 0f, buttonWidth);
            maskButton.eventClicked += (c, e) => { PandemicManager.Instance?.ToggleMasks(); Refresh(); };

            quarantineButton = CreateActionButton(headerPanel, "Quarantine", (buttonWidth * 3f) + (ButtonSpacing * 3f), 0f, buttonWidth);
            quarantineButton.eventClicked += (c, e) => { PandemicManager.Instance?.ToggleQuarantine(); Refresh(); };

            lockdownButton = CreateActionButton(headerPanel, "Lockdown", (buttonWidth * 4f) + (ButtonSpacing * 4f), 0f, buttonWidth);
            lockdownButton.eventClicked += (c, e) => { PandemicManager.Instance?.ToggleLockdown(); Refresh(); };

            overlayButton = CreateActionButton(headerPanel, "Overlays", 0f, secondRowY, buttonWidth);
            overlayButton.eventClicked += (c, e) => { PandemicManager.Instance?.ToggleWorldOverlays(); Refresh(); };

            xrayToggleButton = CreateActionButton(headerPanel, "X-Ray", buttonWidth + ButtonSpacing, secondRowY, buttonWidth);
            xrayToggleButton.eventClicked += (c, e) => { PandemicManager.Instance?.ToggleXRayEnabled(); Refresh(); };

            xrayTypeButton = CreateActionButton(headerPanel, "Type", (buttonWidth * 2f) + (ButtonSpacing * 2f), secondRowY, buttonWidth);
            xrayTypeButton.eventClicked += (c, e) => { PandemicManager.Instance?.CycleXRayMetric(); Refresh(); };

            xrayBasisButton = CreateActionButton(headerPanel, "Basis", (buttonWidth * 3f) + (ButtonSpacing * 3f), secondRowY, buttonWidth);
            xrayBasisButton.eventClicked += (c, e) => { PandemicManager.Instance?.CycleXRayLocationMode(); Refresh(); };

            CreateMetricCards();

            trendChart = new PandemicTrendChartView();
            trendChart.Initialize(headerPanel, TrendChartWidth, ChartHeight);
            trendChart.Component.relativePosition = new Vector3(0f, ChartTop);

            sidrBarView = new PandemicSidrBarView();
            sidrBarView.Initialize(headerPanel, SidrChartWidth, ChartHeight);
            sidrBarView.Component.relativePosition = new Vector3(TrendChartWidth + ChartGap, ChartTop);
        }

        private void CreateDetailScroll()
        {
            detailScroll = panel.AddUIComponent<UIScrollablePanel>();
            detailScroll.relativePosition = new Vector3(HorizontalPadding, DetailTop);
            detailScroll.width = DetailWidth;
            detailScroll.height = DetailScrollHeight;
            detailScroll.clipChildren = true;
            detailScroll.scrollWheelAmount = 60;
            detailScroll.scrollWheelDirection = UIOrientation.Vertical;
            detailScroll.useTouchMouseScroll = true;
            AttachDetailMouseWheel(detailScroll);

            detailContentPanel = detailScroll.AddUIComponent<UIPanel>();
            detailContentPanel.relativePosition = Vector3.zero;
            detailContentPanel.width = DetailWidth;
            detailContentPanel.height = DetailScrollHeight;
            detailContentPanel.autoLayout = false;
            detailContentPanel.isInteractive = true;
            AttachDetailMouseWheel(detailContentPanel);

            detailScrollbar = CreateScrollbar();
            detailScroll.verticalScrollbar = detailScrollbar;
            detailScrollbar.eventValueChanged += (c, value) =>
            {
                if (!suppressScrollbarEvent)
                {
                    detailScroll.scrollPosition = new Vector2(0f, value);
                }
            };
        }

        private void CreateCards()
        {
            ageCard = CreateCardPanel(detailContentPanel);
            ageToggleButton = CreateSectionButton(ageCard, "Infected by Age Group");
            ageToggleButton.eventClicked += (c, e) => ToggleSection(DashboardSection.Age);
            ageSummaryLabel = CreateCardSummary(ageCard);
            ageContentPanel = CreateCardContent(ageCard);

            lockdownCard = CreateCardPanel(detailContentPanel);
            lockdownToggleButton = CreateSectionButton(lockdownCard, "Lockdown Families");
            lockdownToggleButton.eventClicked += (c, e) => ToggleSection(DashboardSection.Lockdown);
            lockdownSummaryLabel = CreateCardSummary(lockdownCard);
            lockdownContentPanel = CreateCardContent(lockdownCard);

            spreaderCard = CreateCardPanel(detailContentPanel);
            spreaderToggleButton = CreateSectionButton(spreaderCard, "Top Spreaders");
            spreaderToggleButton.eventClicked += (c, e) => ToggleSection(DashboardSection.Spreaders);
            spreaderSummaryLabel = CreateCardSummary(spreaderCard);
            spreaderContentPanel = CreateCardContent(spreaderCard);
            for (int i = 0; i < spreaderButtons.Length; i++)
            {
                int captured = i;
                spreaderButtons[i] = CreateCardListButton(spreaderContentPanel);
                spreaderButtons[i].eventClicked += (c, e) => FocusSpreader(captured);
            }

            locationCard = CreateCardPanel(detailContentPanel);
            locationToggleButton = CreateSectionButton(locationCard, "Top Origin Locations");
            locationToggleButton.eventClicked += (c, e) => ToggleSection(DashboardSection.Locations);
            locationSummaryLabel = CreateCardSummary(locationCard);
            locationContentPanel = CreateCardContent(locationCard);
            for (int i = 0; i < locationButtons.Length; i++)
            {
                int captured = i;
                locationButtons[i] = CreateCardListButton(locationContentPanel);
                locationButtons[i].eventClicked += (c, e) => FocusLocation(captured);
            }

            originCard = CreateCardPanel(detailContentPanel);
            originToggleButton = CreateSectionButton(originCard, "Origin Distribution");
            originToggleButton.eventClicked += (c, e) => ToggleSection(DashboardSection.Origins);
            originSummaryLabel = CreateCardSummary(originCard);
            originContentPanel = CreateCardContent(originCard);

            districtCard = CreateCardPanel(detailContentPanel);
            districtToggleButton = CreateSectionButton(districtCard, "District Infection Rates");
            districtToggleButton.eventClicked += (c, e) => ToggleSection(DashboardSection.Districts);
            districtSummaryLabel = CreateCardSummary(districtCard);
            districtContentPanel = CreateCardContent(districtCard);

            settingsCard = CreateCardPanel(detailContentPanel);
            settingsToggleButton = CreateSectionButton(settingsCard, "Pandemic Settings");
            settingsToggleButton.eventClicked += (c, e) => ToggleSection(DashboardSection.Settings);
            settingsSummaryLabel = CreateCardSummary(settingsCard);
            settingsContentPanel = CreateCardContent(settingsCard);

            settingsPanel = settingsContentPanel.AddUIComponent<UIPanel>();
            settingsPanel.autoLayout = false;
            settingsPanel.width = DetailWidth - (CardPadding * 2f);
        }

        private void EnsureSettingsControls()
        {
            if (settingsBuilt)
            {
                return;
            }

            PandemicManager manager = PandemicManager.Instance;
            RealTimeConfig config = manager?.RuntimeConfig;
            if (config == null || settingsPanel == null)
            {
                return;
            }

            float y = 0f;
            var properties = config.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => new
                {
                    Property = p,
                    Item = GetCustomItemAttribute<ConfigItemAttribute>(p),
                    Slider = GetCustomItemAttribute<ConfigItemSliderAttribute>(p),
                    Check = GetCustomItemAttribute<ConfigItemCheckBoxAttribute>(p),
                    Combo = GetCustomItemAttribute<ConfigItemComboBoxAttribute>(p),
                })
                .Where(p => p.Item != null && IsPandemicTab(p.Item.TabId))
                .OrderBy(p => p.Item.TabId, StringComparer.Ordinal)
                .ThenBy(p => p.Item.GroupId, StringComparer.Ordinal)
                .ThenBy(p => p.Item.Order);

            string currentTab = null;
            string currentGroup = null;
            foreach (var item in properties)
            {
                if (currentTab != item.Item.TabId)
                {
                    currentTab = item.Item.TabId;
                    currentGroup = null;
                    UILabel heading = CreateSettingsHeading(GetFriendlyName(item.Item.TabId), 0.86f);
                    heading.relativePosition = new Vector3(0f, y);
                    y += 22f;
                    settingsHeadings.Add(heading);
                }

                if (!string.IsNullOrEmpty(item.Item.GroupId) && currentGroup != item.Item.GroupId)
                {
                    currentGroup = item.Item.GroupId;
                    UILabel groupHeading = CreateSettingsHeading(GetFriendlyName(item.Item.GroupId), 0.78f);
                    groupHeading.relativePosition = new Vector3(0f, y);
                    y += 20f;
                    settingsHeadings.Add(groupHeading);
                }

                PandemicSettingControl control = CreateSettingControl(item.Property, item.Item, item.Slider, item.Check, item.Combo, y);
                if (control != null)
                {
                    settingControls.Add(control);
                    y += control.Height + 4f;
                }
            }

            settingsPanel.height = y;
            settingsBuilt = true;
            layoutDirty = true;
        }

        private void RefreshButtons(PandemicManager manager, PandemicLiveSnapshot snapshot)
        {
            if (manager == null || snapshot == null)
            {
                startRestartButton.text = "Start";
                stopButton.text = "Stop";
                stopButton.isEnabled = false;
                maskButton.text = "Masks";
                quarantineButton.text = "Quarantine";
                lockdownButton.text = "Lockdown";
                overlayButton.text = "Overlays";
                xrayToggleButton.text = "X-Ray: OFF";
                xrayTypeButton.text = "Type: Infected";
                xrayBasisButton.text = "Basis: Live";
                xrayTypeButton.isEnabled = false;
                xrayBasisButton.isEnabled = false;
                return;
            }

            startRestartButton.text = snapshot.CanStart ? "Start" : "Restart";
            startRestartButton.color = snapshot.CanStart ? new Color32(30, 140, 200, 255) : new Color32(70, 110, 170, 255);

            stopButton.text = "Stop";
            stopButton.isEnabled = snapshot.CanStop;
            stopButton.color = snapshot.CanStop ? new Color32(180, 40, 40, 255) : new Color32(84, 84, 84, 255);

            bool masksOn = manager.IsMasksEnabled();
            bool quarantineOn = manager.IsQuarantineEnabled();
            bool lockdownOn = manager.IsLockdownEnabled();
            bool overlaysOn = manager.AreWorldOverlaysEnabled();

            maskButton.text = "Masks: " + (masksOn ? "ON" : "OFF");
            maskButton.color = masksOn ? new Color32(30, 160, 30, 255) : new Color32(160, 30, 30, 255);

            quarantineButton.text = "Quarantine: " + (quarantineOn ? "ON" : "OFF");
            quarantineButton.color = quarantineOn ? new Color32(30, 160, 30, 255) : new Color32(160, 30, 30, 255);

            lockdownButton.text = "Lock: " + (lockdownOn ? "ON" : "OFF");
            lockdownButton.color = lockdownOn ? new Color32(30, 160, 30, 255) : new Color32(160, 30, 30, 255);

            overlayButton.text = "Overlays: " + (overlaysOn ? "ON" : "OFF");
            overlayButton.color = overlaysOn ? new Color32(30, 160, 30, 255) : new Color32(160, 30, 30, 255);

            xrayToggleButton.text = "X-Ray: " + (snapshot.XRayEnabled ? "ON" : "OFF");
            xrayToggleButton.color = snapshot.XRayEnabled ? new Color32(180, 80, 40, 255) : new Color32(100, 100, 100, 255);

            xrayTypeButton.text = "Type: " + GetXRayMetricLabel(snapshot.XRayMetric);
            xrayTypeButton.color = snapshot.XRayEnabled ? GetXRayMetricColor(snapshot.XRayMetric) : new Color32(84, 84, 84, 255);
            xrayTypeButton.isEnabled = true;

            xrayBasisButton.text = "Basis: " + GetXRayLocationLabel(snapshot.XRayLocationMode);
            xrayBasisButton.color = snapshot.XRayEnabled ? new Color32(70, 110, 170, 255) : new Color32(84, 84, 84, 255);
            xrayBasisButton.isEnabled = true;
        }

        private void RefreshSummary(PandemicLiveSnapshot snapshot)
        {
            if (metricValues[0] == null)
            {
                return;
            }

            if (snapshot == null)
            {
                metricTitles[0].text = "Lifecycle";
                metricValues[0].text = "Manager unavailable";
                metricTitles[1].text = "SIRD";
                metricValues[1].text = "-";
                metricTitles[2].text = "Change";
                metricValues[2].text = "-";
                metricTitles[3].text = "Healthcare";
                metricValues[3].text = "-";
                metricTitles[4].text = "Operations";
                metricValues[4].text = "-";
                for (int i = 0; i < metricPanels.Length; i++)
                {
                    if (metricPanels[i] != null)
                    {
                        metricPanels[i].color = new Color32(72, 72, 72, 255);
                    }
                }

                return;
            }

            string timeValue = snapshot.SimulationTime == default(DateTime)
                ? "-"
                : snapshot.SimulationTime.ToString("g", cultureInfo);

            metricTitles[0].text = "Lifecycle";
            metricValues[0].text = FormatLifecycleMetricValue(snapshot) + "\n" + timeValue;
            metricPanels[0].color = snapshot.LifecycleState == PandemicLifecycleState.Running
                ? new Color32(34, 120, 74, 255)
                : snapshot.LifecycleState == PandemicLifecycleState.Finished
                    ? new Color32(124, 84, 34, 255)
                    : new Color32(72, 72, 72, 255);

            metricTitles[1].text = "SIRD";
            metricValues[1].text =
                "S " + snapshot.Healthy.ToString("N0", cultureInfo) + " | I " + snapshot.Sick.ToString("N0", cultureInfo) + "\n"
                + "R " + snapshot.Recovered.ToString("N0", cultureInfo) + " | D " + snapshot.Dead.ToString("N0", cultureInfo);
            metricPanels[1].color = new Color32(82, 82, 82, 255);

            metricTitles[2].text = "Change";
            metricValues[2].text =
                "dI " + FormatSigned(snapshot.DeltaSick) + " | dR " + FormatSigned(snapshot.DeltaRecovered) + "\n"
                + "dD " + FormatSigned(snapshot.DeltaDead) + " | Obs " + snapshot.ObservationCount.ToString("N0", cultureInfo);
            metricPanels[2].color = new Color32(82, 82, 82, 255);

            metricTitles[3].text = "Healthcare";
            metricValues[3].text =
                "Hosp " + FormatPercent(snapshot.HospitalUsagePercent)
                + " " + FormatUsageDelta(snapshot.HospitalUsageDeltaPercent) + "\n"
                + "Amb " + FormatPercent(snapshot.AmbulanceUsagePercent)
                + " " + FormatUsageDelta(snapshot.AmbulanceUsageDeltaPercent);
            metricPanels[3].color = GetHealthcareMetricColor(snapshot.HospitalUsagePercent, snapshot.AmbulanceUsagePercent);

            metricTitles[4].text = "Operations";
            metricValues[4].text =
                "Q " + snapshot.QuarantineCitizens.ToString("N0", cultureInfo)
                + " | T+ " + snapshot.PositiveTests.ToString("N0", cultureInfo) + "\n"
                + "PT " + GetPublicTransportStateLabel(snapshot.PublicTransportState)
                + " | Trx " + snapshot.TransmissionsTotal.ToString("N0", cultureInfo);
            metricPanels[4].color = new Color32(82, 82, 82, 255);
        }

        private static string FormatLifecycleMetricValue(PandemicLiveSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return "Manager unavailable";
            }

            switch (snapshot.LifecycleState)
            {
                case PandemicLifecycleState.Running:
                    return "Running · Day " + Math.Max(1, snapshot.PandemicDay);
                case PandemicLifecycleState.Finished:
                    return "Finished · " + Math.Max(1, snapshot.PandemicDay) + "d";
                default:
                    return "Dormant";
            }
        }

        private void RefreshAnalytics(PandemicLiveSnapshot snapshot)
        {
            RefreshAgeRows(snapshot);
            RefreshLockdownRows(snapshot);
            RefreshOriginRows(snapshot);
            RefreshDistrictRows(snapshot);
            RefreshSpreaderButtons(snapshot);
            RefreshLocationButtons(snapshot);
            UpdateCardHeaders();
        }

        private void RefreshAgeRows(PandemicLiveSnapshot snapshot)
        {
            List<string> texts = snapshot == null || snapshot.AgeGroups.Count == 0
                ? new List<string> { "No age-group data available." }
                : snapshot.AgeGroups
                    .Select(age => age.Label + " | " + FormatPercent(age.InfectedPercent) + " | " + age.InfectedCount.ToString("N0", cultureInfo))
                    .ToList();

            SetLabelRows(ageContentPanel, ageRows, texts, ref ageVisibleRows);
            ageSummaryLabel.text = snapshot == null
                ? "No age breakdown."
                : "Tracked groups: " + snapshot.AgeGroups.Count.ToString("N0", cultureInfo)
                + " | Active infected: " + snapshot.Sick.ToString("N0", cultureInfo);
        }

        private void RefreshLockdownRows(PandemicLiveSnapshot snapshot)
        {
            List<string> texts = snapshot == null || snapshot.LockdownFamilies.Count == 0
                ? new List<string> { "No lockdown family data available." }
                : new[]
                    {
                        "Public transport | " + GetPublicTransportStateLabel(snapshot.PublicTransportState)
                        + " | lines " + snapshot.PublicTransportTrackedLines.ToString("N0", cultureInfo)
                        + " | returning " + snapshot.PublicTransportReturningVehicles.ToString("N0", cultureInfo)
                        + " | depots " + snapshot.PublicTransportClosedDepots.ToString("N0", cultureInfo),
                    }
                    .Concat(snapshot.LockdownFamilies
                    .Select(family => family.Label + " | " + (family.IsClosed ? "Closed" : "Open")
                        + " | infected " + FormatPercent(family.CurrentInfectedPercent)
                        + " | threshold " + FormatPercent(family.AutoCloseThresholdPercent)
                        + " | manual " + (family.ManualClosed ? "ON" : "OFF")))
                    .ToList();

            SetLabelRows(lockdownContentPanel, lockdownRows, texts, ref lockdownVisibleRows);
            int closedFamilies = snapshot?.LockdownFamilies.Count(family => family.IsClosed) ?? 0;
            int totalFamilies = snapshot?.LockdownFamilies.Count ?? 0;
            lockdownSummaryLabel.text = "Closed: " + closedFamilies.ToString("N0", cultureInfo)
                + " / " + totalFamilies.ToString("N0", cultureInfo)
                + " | PT: " + GetPublicTransportStateLabel(snapshot?.PublicTransportState ?? PandemicPublicTransportShutdownState.Open);
        }

        private void RefreshOriginRows(PandemicLiveSnapshot snapshot)
        {
            List<string> texts = snapshot == null || snapshot.Origins.Count == 0
                ? new List<string> { "No origin data available." }
                : snapshot.Origins
                    .OrderByDescending(origin => origin.Count)
                    .ThenBy(origin => origin.Label, StringComparer.OrdinalIgnoreCase)
                    .Select(origin => origin.Label + " | " + FormatPercent(origin.Percent) + " | " + origin.Count.ToString("N0", cultureInfo))
                    .ToList();

            SetLabelRows(originContentPanel, originRows, texts, ref originVisibleRows);
            int totalOrigins = snapshot?.Origins.Sum(origin => origin.Count) ?? 0;
            originSummaryLabel.text = "Tracked infections: " + totalOrigins.ToString("N0", cultureInfo);
        }

        private void RefreshDistrictRows(PandemicLiveSnapshot snapshot)
        {
            List<string> texts = snapshot == null || snapshot.Districts.Count == 0
                ? new List<string> { "No user-defined districts available." }
                : snapshot.Districts
                    .Select(district => district.DistrictName + " | " + FormatPercent(district.InfectedPercent)
                        + " | " + district.InfectedResidents.ToString("N0", cultureInfo)
                        + " / " + district.ResidentCount.ToString("N0", cultureInfo))
                    .ToList();

            SetLabelRows(districtContentPanel, districtRows, texts, ref districtVisibleRows);
            districtSummaryLabel.text = snapshot == null
                ? "No district data."
                : "Districts: " + snapshot.Districts.Count.ToString("N0", cultureInfo);
        }

        private void RefreshSpreaderButtons(PandemicLiveSnapshot snapshot)
        {
            int count = snapshot == null ? 0 : Math.Min(spreaderButtons.Length, snapshot.TopSpreaders.Count);
            int visibleCount = Math.Max(1, count);
            if (spreaderVisibleRows != visibleCount)
            {
                spreaderVisibleRows = visibleCount;
                layoutDirty = true;
            }

            for (int i = 0; i < spreaderButtons.Length; i++)
            {
                UIButton button = spreaderButtons[i];
                if (i >= visibleCount)
                {
                    button.isVisible = false;
                    continue;
                }

                button.isVisible = true;
                if (i < count)
                {
                    PandemicSuperspreaderCitizenSnapshot entry = snapshot.TopSpreaders[i];
                    button.text = entry.Label + " | " + entry.InfectionCount.ToString("N0", cultureInfo)
                        + (entry.IsSuperspreader ? " | Superspreader" : string.Empty);
                    button.isEnabled = entry.CanFocus;
                    button.tooltip = "Jump to citizen";
                }
                else
                {
                    button.text = "No spreader data available.";
                    button.isEnabled = false;
                    button.tooltip = string.Empty;
                }
            }

            int topCount = snapshot?.TopSpreaders.Count ?? 0;
            int topInfections = snapshot?.TopSpreaders.Count > 0 ? snapshot.TopSpreaders[0].InfectionCount : 0;
            spreaderSummaryLabel.text = topCount == 0
                ? "No spreader data."
                : "Top spreader: " + topInfections.ToString("N0", cultureInfo) + " infections";
        }

        private void RefreshLocationButtons(PandemicLiveSnapshot snapshot)
        {
            int count = snapshot == null ? 0 : Math.Min(locationButtons.Length, snapshot.TopOriginLocations.Count);
            int visibleCount = Math.Max(1, count);
            if (locationVisibleRows != visibleCount)
            {
                locationVisibleRows = visibleCount;
                layoutDirty = true;
            }

            for (int i = 0; i < locationButtons.Length; i++)
            {
                UIButton button = locationButtons[i];
                if (i >= visibleCount)
                {
                    button.isVisible = false;
                    continue;
                }

                button.isVisible = true;
                if (i < count)
                {
                    PandemicSuperspreaderLocationSnapshot entry = snapshot.TopOriginLocations[i];
                    button.text = entry.Label + " | " + entry.InfectionCount.ToString("N0", cultureInfo)
                        + (entry.IsSuperspreader ? " | Superspreader" : string.Empty);
                    button.isEnabled = entry.CanFocus;
                    button.tooltip = "Jump to location";
                }
                else
                {
                    button.text = "No origin location data available.";
                    button.isEnabled = false;
                    button.tooltip = string.Empty;
                }
            }

            int locationCount = snapshot?.TopOriginLocations.Count ?? 0;
            int topLocationInfections = snapshot?.TopOriginLocations.Count > 0 ? snapshot.TopOriginLocations[0].InfectionCount : 0;
            locationSummaryLabel.text = locationCount == 0
                ? "No hotspot locations."
                : "Top location: " + topLocationInfections.ToString("N0", cultureInfo) + " infections";
        }

        private void RefreshLiveData()
        {
            if (trendChart != null)
            {
                trendChart.Refresh(currentSnapshot, cultureInfo);
            }

            if (sidrBarView != null)
            {
                sidrBarView.Refresh(currentSnapshot, cultureInfo);
            }
        }

        private void RefreshSettingsControls()
        {
            PandemicManager manager = PandemicManager.Instance;
            RealTimeConfig config = manager?.RuntimeConfig;
            if (config == null)
            {
                return;
            }

            foreach (PandemicSettingControl control in settingControls)
            {
                object value = control.Property.GetValue(config, null);
                if (control.Slider != null)
                {
                    if (!control.IsEditing)
                    {
                        control.LastDisplayValue = FormatNumericValue(value, control.Slider);
                        control.ValueButton.text = control.LastDisplayValue;
                        control.ValueButton.color = new Color32(70, 110, 170, 255);
                        control.ValueButton.isVisible = true;
                        if (control.Editor != null)
                        {
                            control.Editor.isVisible = false;
                        }
                    }
                }
                else if (control.Property.PropertyType == typeof(bool))
                {
                    bool flag = value is bool current && current;
                    control.ValueButton.text = flag ? "ON" : "OFF";
                    control.ValueButton.color = flag ? new Color32(30, 160, 30, 255) : new Color32(160, 30, 30, 255);
                }
                else if (control.Property.PropertyType.IsEnum)
                {
                    control.ValueButton.text = GetFriendlyName(value.ToString());
                    control.ValueButton.color = new Color32(70, 110, 170, 255);
                }
            }

            settingsSummaryLabel.text = settingControls.Count.ToString("N0", cultureInfo) + " live settings available";
        }

        private void RefreshLayoutIfDirty()
        {
            if (!layoutDirty || detailContentPanel == null || detailScroll == null)
            {
                return;
            }

            float preservedScroll = detailScroll.scrollPosition.y;
            LayoutCards();
            SyncScrollbar(preservedScroll);
            layoutDirty = false;
        }

        private void LayoutCards()
        {
            float halfWidth = (DetailWidth - CardGap) / 2f;
            float y = 0f;

            float leftHeight = LayoutTextCard(ageCard, ageToggleButton, ageSummaryLabel, ageContentPanel, ageRows, DashboardSection.Age, 0f, y, halfWidth);
            float rightHeight = LayoutTextCard(lockdownCard, lockdownToggleButton, lockdownSummaryLabel, lockdownContentPanel, lockdownRows, DashboardSection.Lockdown, halfWidth + CardGap, y, halfWidth);
            y += Mathf.Max(leftHeight, rightHeight) + CardGap;

            leftHeight = LayoutButtonCard(spreaderCard, spreaderToggleButton, spreaderSummaryLabel, spreaderContentPanel, spreaderButtons, DashboardSection.Spreaders, 0f, y, halfWidth);
            rightHeight = LayoutButtonCard(locationCard, locationToggleButton, locationSummaryLabel, locationContentPanel, locationButtons, DashboardSection.Locations, halfWidth + CardGap, y, halfWidth);
            y += Mathf.Max(leftHeight, rightHeight) + CardGap;

            y += LayoutTextCard(originCard, originToggleButton, originSummaryLabel, originContentPanel, originRows, DashboardSection.Origins, 0f, y, DetailWidth) + CardGap;
            y += LayoutTextCard(districtCard, districtToggleButton, districtSummaryLabel, districtContentPanel, districtRows, DashboardSection.Districts, 0f, y, DetailWidth) + CardGap;
            y += LayoutSettingsCard(y) + CardGap;

            detailContentPanel.height = Mathf.Max(DetailScrollHeight, y);
        }

        private float LayoutTextCard(
            UIPanel card,
            UIButton toggleButton,
            UILabel summaryLabel,
            UIPanel contentPanel,
            IList<UILabel> rows,
            DashboardSection section,
            float x,
            float y,
            float width)
        {
            card.relativePosition = new Vector3(x, y);
            card.width = width;

            float innerWidth = width - (CardPadding * 2f);
            toggleButton.relativePosition = new Vector3(CardPadding, CardPadding);
            toggleButton.width = innerWidth;
            summaryLabel.relativePosition = new Vector3(CardPadding, CardPadding + toggleButton.height + 2f);
            summaryLabel.width = innerWidth;

            float nextY = CardPadding + toggleButton.height + 4f + CardSummaryHeight + 4f;
            contentPanel.relativePosition = new Vector3(CardPadding, nextY);
            contentPanel.width = innerWidth;
            contentPanel.isVisible = IsSectionExpanded(section);
            if (IsSectionExpanded(section))
            {
                float bodyY = 0f;
                for (int i = 0; i < rows.Count; i++)
                {
                    UILabel row = rows[i];
                    if (!row.isVisible)
                    {
                        continue;
                    }

                    row.relativePosition = new Vector3(0f, bodyY);
                    row.width = innerWidth;
                    bodyY += CardRowHeight + CardRowSpacing;
                }

                contentPanel.height = Mathf.Max(0f, bodyY - CardRowSpacing);
                nextY += contentPanel.height + 4f;
            }
            else
            {
                contentPanel.height = 0f;
            }

            card.height = Mathf.Max(CardMinHeight, nextY + CardPadding);
            return card.height;
        }

        private float LayoutButtonCard(
            UIPanel card,
            UIButton toggleButton,
            UILabel summaryLabel,
            UIPanel contentPanel,
            IList<UIButton> buttons,
            DashboardSection section,
            float x,
            float y,
            float width)
        {
            card.relativePosition = new Vector3(x, y);
            card.width = width;

            float innerWidth = width - (CardPadding * 2f);
            toggleButton.relativePosition = new Vector3(CardPadding, CardPadding);
            toggleButton.width = innerWidth;
            summaryLabel.relativePosition = new Vector3(CardPadding, CardPadding + toggleButton.height + 2f);
            summaryLabel.width = innerWidth;

            float nextY = CardPadding + toggleButton.height + 4f + CardSummaryHeight + 4f;
            contentPanel.relativePosition = new Vector3(CardPadding, nextY);
            contentPanel.width = innerWidth;
            contentPanel.isVisible = IsSectionExpanded(section);
            if (IsSectionExpanded(section))
            {
                float bodyY = 0f;
                for (int i = 0; i < buttons.Count; i++)
                {
                    UIButton button = buttons[i];
                    if (!button.isVisible)
                    {
                        continue;
                    }

                    button.relativePosition = new Vector3(0f, bodyY);
                    button.width = innerWidth;
                    bodyY += CardButtonHeight + 4f;
                }

                contentPanel.height = Mathf.Max(0f, bodyY - 4f);
                nextY += contentPanel.height + 4f;
            }
            else
            {
                contentPanel.height = 0f;
            }

            card.height = Mathf.Max(CardMinHeight, nextY + CardPadding);
            return card.height;
        }

        private float LayoutSettingsCard(float y)
        {
            settingsCard.relativePosition = new Vector3(0f, y);
            settingsCard.width = DetailWidth;

            float innerWidth = DetailWidth - (CardPadding * 2f);
            settingsToggleButton.relativePosition = new Vector3(CardPadding, CardPadding);
            settingsToggleButton.width = innerWidth;
            settingsSummaryLabel.relativePosition = new Vector3(CardPadding, CardPadding + settingsToggleButton.height + 2f);
            settingsSummaryLabel.width = innerWidth;

            float nextY = CardPadding + settingsToggleButton.height + 4f + CardSummaryHeight + 4f;
            settingsContentPanel.relativePosition = new Vector3(CardPadding, nextY);
            settingsContentPanel.width = innerWidth;
            settingsContentPanel.isVisible = IsSectionExpanded(DashboardSection.Settings);
            settingsPanel.relativePosition = Vector3.zero;
            settingsPanel.width = innerWidth;
            settingsPanel.isVisible = IsSectionExpanded(DashboardSection.Settings);

            settingsContentPanel.height = IsSectionExpanded(DashboardSection.Settings) ? settingsPanel.height : 0f;
            settingsCard.height = IsSectionExpanded(DashboardSection.Settings)
                ? nextY + settingsPanel.height + CardPadding
                : Mathf.Max(CardMinHeight, nextY + CardPadding);

            return settingsCard.height;
        }

        private void SyncScrollbar(float preservedScroll)
        {
            if (detailScroll == null || detailScrollbar == null || detailContentPanel == null)
            {
                return;
            }

            detailScrollbar.maxValue = Mathf.Max(0f, detailContentPanel.height - detailScroll.height);
            detailScrollbar.isVisible = !collapsed && detailScrollbar.maxValue > 0.1f;
            float clamped = Mathf.Clamp(preservedScroll, detailScrollbar.minValue, detailScrollbar.maxValue);
            suppressScrollbarEvent = true;
            detailScrollbar.value = clamped;
            detailScroll.scrollPosition = new Vector2(0f, clamped);
            suppressScrollbarEvent = false;
        }

        private void OnStopClicked()
        {
            PandemicManager manager = PandemicManager.Instance;
            if (manager == null)
            {
                return;
            }

            ConfirmPanel.ShowModal("Stop Pandemic", "Stop the pandemic simulation and heal all infected citizens?", (component, result) =>
            {
                if (result == 1)
                {
                    manager.StopPandemic();
                    Refresh();
                }
            });
        }

        private void OnStartRestartClicked()
        {
            PandemicManager manager = PandemicManager.Instance;
            if (manager == null)
            {
                return;
            }

            if (currentSnapshot != null && currentSnapshot.CanStart)
            {
                manager.StartPandemic();
                Refresh();
                return;
            }

            ConfirmPanel.ShowModal(RestartDialogTitle, RestartDialogMessage, (component, result) =>
            {
                if (result == 1)
                {
                    manager.RestartSimulation();
                    Refresh();
                }
            });
        }

        private void FocusSpreader(int index)
        {
            if (currentSnapshot == null || index < 0 || index >= currentSnapshot.TopSpreaders.Count)
            {
                return;
            }

            PandemicSuperspreaderCitizenSnapshot entry = currentSnapshot.TopSpreaders[index];
            if (entry != null && entry.CanFocus)
            {
                FocusCitizen(entry);
            }
        }

        private void FocusLocation(int index)
        {
            if (currentSnapshot == null || index < 0 || index >= currentSnapshot.TopOriginLocations.Count)
            {
                return;
            }

            PandemicSuperspreaderLocationSnapshot entry = currentSnapshot.TopOriginLocations[index];
            if (entry != null && entry.CanFocus)
            {
                FocusOnLocation(entry.BuildingId, entry.FocusPosition, showBuildingInfo: true);
            }
        }

        private void FocusCitizen(PandemicSuperspreaderCitizenSnapshot entry)
        {
            if (entry == null)
            {
                return;
            }

            var instance = new InstanceID();
            if (entry.CitizenInstanceId != 0)
            {
                instance.CitizenInstance = entry.CitizenInstanceId;
            }
            else
            {
                instance.Citizen = entry.CitizenId;
            }

            CameraController cameraController = UnityEngine.Object.FindObjectOfType<CameraController>();
            if (cameraController != null)
            {
                cameraController.SetTarget(instance, entry.FocusPosition, true);
            }

            try
            {
                WorldInfoPanel.Show<CitizenWorldInfoPanel>(entry.FocusPosition, instance);
            }
            catch
            {
            }
        }

        private void FocusOnLocation(ushort buildingId, Vector3 position, bool showBuildingInfo = false)
        {
            CameraController cameraController = UnityEngine.Object.FindObjectOfType<CameraController>();
            var instance = new InstanceID();
            if (buildingId != 0)
            {
                instance.Building = buildingId;
            }

            if (cameraController != null)
            {
                cameraController.SetTarget(instance, position, true);
            }

            if (showBuildingInfo && buildingId != 0)
            {
                try
                {
                    WorldInfoPanel.Show<ZonedBuildingWorldInfoPanel>(position, instance);
                }
                catch
                {
                }
            }
        }

        private void ToggleCollapse()
        {
            collapsed = !collapsed;
            layoutDirty = true;
            Refresh();
        }

        private void ToggleSection(DashboardSection section)
        {
            sectionExpanded[section] = !IsSectionExpanded(section);
            if (section == DashboardSection.Settings && !IsSectionExpanded(section))
            {
                CloseActiveNumericEditor(applyChanges: true);
            }

            layoutDirty = true;
            Refresh();
        }

        private void ApplyCollapsedState()
        {
            if (panel == null)
            {
                return;
            }

            if (collapsed)
            {
                CloseActiveNumericEditor(applyChanges: true);
            }

            panel.height = collapsed ? CollapsedPanelHeight : ExpandedPanelHeight;

            if (headerPanel != null)
            {
                headerPanel.isVisible = !collapsed;
            }

            if (detailScroll != null)
            {
                detailScroll.isVisible = !collapsed;
            }

            if (detailScrollbar != null)
            {
                detailScrollbar.isVisible = !collapsed && detailScrollbar.maxValue > 0.1f;
            }
        }

        private void UpdateTitle()
        {
            if (titleLabel == null)
            {
                return;
            }

            titleLabel.text = (collapsed ? "\u25ba" : "\u25bc") + " Real Time Pandemic Monitor";
        }

        private void BeginNumericEdit(PandemicSettingControl control)
        {
            if (control?.Slider == null || control.Editor == null)
            {
                return;
            }

            if (activeEditingControl != null && activeEditingControl != control)
            {
                CloseActiveNumericEditor(applyChanges: true);
            }

            PandemicManager manager = PandemicManager.Instance;
            RealTimeConfig config = manager?.RuntimeConfig;
            if (config == null)
            {
                return;
            }

            object value = control.Property.GetValue(config, null);
            control.IsEditing = true;
            control.EditorText = FormatEditorValue(value, control.Slider);
            control.LastDisplayValue = FormatNumericValue(value, control.Slider);
            control.Editor.text = control.EditorText;
            control.ValueButton.isVisible = false;
            control.Editor.isVisible = true;
            control.Editor.isEnabled = true;
            activeEditingControl = control;
            control.Editor.Focus();
        }

        private void CloseActiveNumericEditor(bool applyChanges)
        {
            if (activeEditingControl == null)
            {
                return;
            }

            CommitOrCancelNumericEdit(activeEditingControl, applyChanges);
        }

        private void CommitOrCancelNumericEdit(PandemicSettingControl control, bool applyChanges)
        {
            if (control?.Slider == null || control.Editor == null || !control.IsEditing)
            {
                return;
            }

            bool applied = false;
            if (applyChanges)
            {
                applied = TryApplyNumericInput(control, control.Editor.text);
            }

            control.IsEditing = false;
            control.EditorText = string.Empty;
            control.Editor.isVisible = false;
            control.ValueButton.isVisible = true;
            activeEditingControl = activeEditingControl == control ? null : activeEditingControl;

            if (!applied)
            {
                RefreshSettingsControls();
                RefreshLayoutIfDirty();
            }
        }

        private bool TryApplyNumericInput(PandemicSettingControl control, string rawText)
        {
            PandemicManager manager = PandemicManager.Instance;
            RealTimeConfig config = manager?.RuntimeConfig;
            if (config == null || control?.Slider == null)
            {
                return false;
            }

            if (!TryParseNumericValue(rawText, control, out object parsed))
            {
                return false;
            }

            control.Property.SetValue(config, parsed, null);
            config.Validate();
            Refresh();
            return true;
        }

        private bool TryParseNumericValue(string rawText, PandemicSettingControl control, out object parsed)
        {
            parsed = null;
            if (control?.Slider == null || rawText == null || rawText.Trim().Length == 0)
            {
                return false;
            }

            string normalized = rawText.Trim();
            if (control.Slider.ValueType == SliderValueType.Percentage)
            {
                normalized = normalized.Replace("%", string.Empty).Trim();
            }

            Type propertyType = control.Property.PropertyType;
            if (propertyType == typeof(int))
            {
                if (!TryParseDouble(normalized, out double parsedDouble))
                {
                    return false;
                }

                parsed = Mathf.Clamp(Mathf.RoundToInt((float)parsedDouble), Mathf.RoundToInt(control.Slider.Min), Mathf.RoundToInt(control.Slider.Max));
                return true;
            }

            if (propertyType == typeof(uint))
            {
                if (!TryParseDouble(normalized, out double parsedDouble))
                {
                    return false;
                }

                int clamped = Mathf.Clamp(Mathf.RoundToInt((float)parsedDouble), Mathf.RoundToInt(control.Slider.Min), Mathf.RoundToInt(control.Slider.Max));
                parsed = (uint)Mathf.Max(0, clamped);
                return true;
            }

            if (propertyType == typeof(float))
            {
                if (!TryParseDouble(normalized, out double parsedDouble))
                {
                    return false;
                }

                parsed = Mathf.Clamp((float)parsedDouble, control.Slider.Min, control.Slider.Max);
                return true;
            }

            if (propertyType == typeof(double))
            {
                if (!TryParseDouble(normalized, out double parsedDouble))
                {
                    return false;
                }

                parsed = Math.Min(control.Slider.Max, Math.Max(control.Slider.Min, parsedDouble));
                return true;
            }

            return false;
        }

        private bool TryParseDouble(string value, out double parsed)
        {
            return double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, cultureInfo, out parsed)
                || double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out parsed);
        }

        private string FormatEditorValue(object value, ConfigItemSliderAttribute slider)
        {
            if (value == null)
            {
                return string.Empty;
            }

            if (value is float floatValue)
            {
                return slider != null && slider.Step < 1f
                    ? floatValue.ToString("0.###", cultureInfo)
                    : floatValue.ToString("0.#", cultureInfo);
            }

            if (value is double doubleValue)
            {
                return slider != null && slider.Step < 1f
                    ? doubleValue.ToString("0.###", cultureInfo)
                    : doubleValue.ToString("0.#", cultureInfo);
            }

            return Convert.ToString(value, cultureInfo);
        }

        private float GetNumericStep(PandemicSettingControl control)
        {
            if (control?.Slider == null)
            {
                return 1f;
            }

            if (control.Slider.ValueType == SliderValueType.Percentage)
            {
                return 5f;
            }

            return control.Slider.Step > 0f ? control.Slider.Step : 1f;
        }

        private void AdjustNumericSetting(PandemicSettingControl control, int direction)
        {
            PandemicManager manager = PandemicManager.Instance;
            RealTimeConfig config = manager?.RuntimeConfig;
            if (config == null || control?.Slider == null)
            {
                return;
            }

            if (control.IsEditing)
            {
                CommitOrCancelNumericEdit(control, applyChanges: true);
            }

            object currentValue = control.Property.GetValue(config, null);
            float step = GetNumericStep(control);
            if (control.Property.PropertyType == typeof(int))
            {
                int next = (int)currentValue + Mathf.RoundToInt(step * direction);
                next = Mathf.Clamp(next, (int)control.Slider.Min, (int)control.Slider.Max);
                control.Property.SetValue(config, next, null);
            }
            else if (control.Property.PropertyType == typeof(uint))
            {
                int next = (int)(uint)currentValue + Mathf.RoundToInt(step * direction);
                next = Mathf.Clamp(next, (int)control.Slider.Min, (int)control.Slider.Max);
                control.Property.SetValue(config, (uint)next, null);
            }
            else if (control.Property.PropertyType == typeof(float))
            {
                float next = (float)currentValue + (step * direction);
                next = control.Slider.ValueType == SliderValueType.Percentage ? Mathf.Round(next) : next;
                next = Mathf.Clamp(next, control.Slider.Min, control.Slider.Max);
                control.Property.SetValue(config, next, null);
            }
            else if (control.Property.PropertyType == typeof(double))
            {
                double next = (double)currentValue + (step * direction);
                if (control.Slider.ValueType == SliderValueType.Percentage)
                {
                    next = Math.Round(next, 0, MidpointRounding.AwayFromZero);
                }

                next = Math.Min(control.Slider.Max, Math.Max(control.Slider.Min, next));
                control.Property.SetValue(config, next, null);
            }

            config.Validate();
            Refresh();
        }

        private void ToggleSetting(PandemicSettingControl control)
        {
            PandemicManager manager = PandemicManager.Instance;
            RealTimeConfig config = manager?.RuntimeConfig;
            if (config == null || control == null)
            {
                return;
            }

            bool current = (bool)control.Property.GetValue(config, null);
            control.Property.SetValue(config, !current, null);
            config.Validate();
            Refresh();
        }

        private void CycleSetting(PandemicSettingControl control)
        {
            PandemicManager manager = PandemicManager.Instance;
            RealTimeConfig config = manager?.RuntimeConfig;
            if (config == null || control == null)
            {
                return;
            }

            Array values = Enum.GetValues(control.Property.PropertyType);
            object current = control.Property.GetValue(config, null);
            int index = Array.IndexOf(values, current);
            int nextIndex = (index + 1) % values.Length;
            control.Property.SetValue(config, values.GetValue(nextIndex), null);
            config.Validate();
            Refresh();
        }

        private PandemicSettingControl CreateSettingControl(
            PropertyInfo property,
            ConfigItemAttribute item,
            ConfigItemSliderAttribute slider,
            ConfigItemCheckBoxAttribute checkBox,
            ConfigItemComboBoxAttribute comboBox,
            float y)
        {
            UILabel label = settingsPanel.AddUIComponent<UILabel>();
            label.autoSize = false;
            label.width = SettingsLabelWidth;
            label.height = 20f;
            label.textScale = 0.72f;
            label.relativePosition = new Vector3(0f, y + 4f);
            label.text = GetFriendlyName(property.Name);

            var control = new PandemicSettingControl
            {
                Property = property,
                Item = item,
                Slider = slider,
                Label = label,
                Height = 28f,
            };

            if (checkBox != null && property.PropertyType == typeof(bool))
            {
                control.ValueButton = CreateSettingButton(274f, y, 220f);
                control.ValueButton.eventClicked += (c, e) => ToggleSetting(control);
                return control;
            }

            if (comboBox != null && property.PropertyType.IsEnum)
            {
                control.ValueButton = CreateSettingButton(274f, y, 220f);
                control.ValueButton.eventClicked += (c, e) => CycleSetting(control);
                return control;
            }

            if (slider != null && property.PropertyType.IsPrimitive)
            {
                control.MinusButton = CreateSettingButton(274f, y, 32f, "-");
                control.ValueButton = CreateSettingButton(310f, y, 148f);
                control.PlusButton = CreateSettingButton(462f, y, 32f, "+");
                control.Editor = CreateSettingEditor(310f, y, 148f);
                control.Editor.isVisible = false;
                control.ValueButton.eventClicked += (c, e) => BeginNumericEdit(control);
                control.MinusButton.eventClicked += (c, e) => AdjustNumericSetting(control, -1);
                control.PlusButton.eventClicked += (c, e) => AdjustNumericSetting(control, 1);
                control.Editor.eventLostFocus += (c, e) => CommitOrCancelNumericEdit(control, applyChanges: true);
                control.Editor.eventKeyDown += (c, e) =>
                {
                    if (e.keycode == KeyCode.Return || e.keycode == KeyCode.KeypadEnter)
                    {
                        CommitOrCancelNumericEdit(control, applyChanges: true);
                        e.Use();
                    }
                    else if (e.keycode == KeyCode.Escape)
                    {
                        CommitOrCancelNumericEdit(control, applyChanges: false);
                        e.Use();
                    }
                };
                return control;
            }

            return null;
        }

        private UIPanel CreateCardPanel(UIComponent parent)
        {
            UIPanel card = parent.AddUIComponent<UIPanel>();
            card.autoSize = false;
            card.width = DetailWidth;
            card.height = CardMinHeight;
            card.backgroundSprite = "MenuPanel2";
            card.opacity = 0.7f;
            card.clipChildren = true;
            card.isInteractive = true;
            AttachDetailMouseWheel(card);
            return card;
        }

        private void CreateMetricCards()
        {
            float width = (HeaderWidth - (MetricCardGap * 4f)) / 5f;
            for (int i = 0; i < metricPanels.Length; i++)
            {
                UIPanel metricPanel = headerPanel.AddUIComponent<UIPanel>();
                metricPanel.autoSize = false;
                metricPanel.width = width;
                metricPanel.height = MetricCardHeight;
                metricPanel.relativePosition = new Vector3((width + MetricCardGap) * i, MetricsTop);
                metricPanel.backgroundSprite = "MenuPanel2";
                metricPanel.opacity = 0.68f;
                metricPanel.clipChildren = true;
                metricPanels[i] = metricPanel;

                UILabel title = metricPanel.AddUIComponent<UILabel>();
                title.autoSize = false;
                title.width = width - 12f;
                title.height = 14f;
                title.relativePosition = new Vector3(6f, 6f);
                title.textScale = 0.64f;
                title.textColor = new Color32(220, 220, 220, 255);
                metricTitles[i] = title;

                UILabel value = metricPanel.AddUIComponent<UILabel>();
                value.autoSize = false;
                value.width = width - 12f;
                value.height = MetricCardHeight - 24f;
                value.relativePosition = new Vector3(6f, 20f);
                value.textScale = 0.68f;
                value.textColor = new Color32(245, 245, 245, 255);
                metricValues[i] = value;
            }
        }

        private UIButton CreateActionButton(UIComponent parent, string label, float x, float y, float width)
        {
            UIButton button = parent.AddUIComponent<UIButton>();
            button.autoSize = false;
            button.width = width;
            button.height = ButtonHeight;
            button.relativePosition = new Vector3(x, y);
            button.text = label;
            button.textScale = 0.72f;
            button.textColor = new Color32(255, 255, 255, 255);
            button.normalBgSprite = "ButtonMenu";
            button.hoveredBgSprite = "ButtonMenuHovered";
            button.pressedBgSprite = "ButtonMenuPressed";
            button.textHorizontalAlignment = UIHorizontalAlignment.Center;
            AttachDetailMouseWheel(button);
            return button;
        }

        private UIButton CreateSectionButton(UIComponent parent, string text)
        {
            UIButton button = parent.AddUIComponent<UIButton>();
            button.autoSize = false;
            button.height = 24f;
            button.text = "\u25ba " + text;
            button.textScale = 0.76f;
            button.textColor = new Color32(255, 255, 255, 255);
            button.normalBgSprite = "ButtonMenu";
            button.hoveredBgSprite = "ButtonMenuHovered";
            button.pressedBgSprite = "ButtonMenuPressed";
            button.textHorizontalAlignment = UIHorizontalAlignment.Left;
            AttachDetailMouseWheel(button);
            return button;
        }

        private void UpdateCardHeaders()
        {
            ageToggleButton.text = BuildSectionHeader(DashboardSection.Age, "Infected by Age Group");
            lockdownToggleButton.text = BuildSectionHeader(DashboardSection.Lockdown, "Lockdown Families");
            spreaderToggleButton.text = BuildSectionHeader(DashboardSection.Spreaders, "Top Spreaders");
            locationToggleButton.text = BuildSectionHeader(DashboardSection.Locations, "Top Origin Locations");
            originToggleButton.text = BuildSectionHeader(DashboardSection.Origins, "Origin Distribution");
            districtToggleButton.text = BuildSectionHeader(DashboardSection.Districts, "District Infection Rates");
            settingsToggleButton.text = BuildSectionHeader(DashboardSection.Settings, "Pandemic Settings");

            if (settingsSummaryLabel != null && string.IsNullOrEmpty(settingsSummaryLabel.text))
            {
                settingsSummaryLabel.text = "Live configuration in this save";
            }
        }

        private string BuildSectionHeader(DashboardSection section, string title)
        {
            return (IsSectionExpanded(section) ? "\u25bc " : "\u25ba ") + title;
        }

        private bool IsSectionExpanded(DashboardSection section)
        {
            return sectionExpanded.TryGetValue(section, out bool expanded) && expanded;
        }

        private UILabel CreateCardSummary(UIComponent parent)
        {
            UILabel label = parent.AddUIComponent<UILabel>();
            label.autoSize = false;
            label.height = CardSummaryHeight;
            label.textScale = 0.66f;
            label.textColor = new Color32(216, 216, 216, 255);
            label.isInteractive = true;
            AttachDetailMouseWheel(label);
            return label;
        }

        private UIPanel CreateCardContent(UIComponent parent)
        {
            UIPanel content = parent.AddUIComponent<UIPanel>();
            content.autoLayout = false;
            content.autoSize = false;
            content.clipChildren = true;
            content.isInteractive = true;
            AttachDetailMouseWheel(content);
            return content;
        }

        private UIButton CreateCardListButton(UIComponent parent)
        {
            UIButton button = parent.AddUIComponent<UIButton>();
            button.autoSize = false;
            button.height = CardButtonHeight;
            button.textScale = 0.7f;
            button.textColor = new Color32(255, 255, 255, 255);
            button.normalBgSprite = "ButtonMenu";
            button.hoveredBgSprite = "ButtonMenuHovered";
            button.pressedBgSprite = "ButtonMenuPressed";
            button.textHorizontalAlignment = UIHorizontalAlignment.Left;
            button.isVisible = false;
            AttachDetailMouseWheel(button);
            return button;
        }

        private UILabel CreateCardRowLabel(UIComponent parent)
        {
            UILabel label = parent.AddUIComponent<UILabel>();
            label.autoSize = false;
            label.height = CardRowHeight;
            label.textScale = 0.72f;
            label.textAlignment = UIHorizontalAlignment.Left;
            label.isVisible = false;
            label.isInteractive = true;
            AttachDetailMouseWheel(label);
            return label;
        }

        private UILabel CreateSettingsHeading(string text, float textScale)
        {
            UILabel label = settingsPanel.AddUIComponent<UILabel>();
            label.autoSize = false;
            label.width = settingsPanel.width;
            label.height = 18f;
            label.text = text;
            label.textScale = textScale;
            label.textColor = new Color32(230, 230, 230, 255);
            label.isInteractive = true;
            AttachDetailMouseWheel(label);
            return label;
        }

        private UIButton CreateSettingButton(float x, float y, float width, string text = "")
        {
            UIButton button = settingsPanel.AddUIComponent<UIButton>();
            button.autoSize = false;
            button.width = width;
            button.height = 26f;
            button.relativePosition = new Vector3(x, y);
            button.text = text;
            button.textScale = 0.68f;
            button.textColor = new Color32(255, 255, 255, 255);
            button.normalBgSprite = "ButtonMenu";
            button.hoveredBgSprite = "ButtonMenuHovered";
            button.pressedBgSprite = "ButtonMenuPressed";
            button.textHorizontalAlignment = UIHorizontalAlignment.Center;
            AttachDetailMouseWheel(button);
            return button;
        }

        private UITextField CreateSettingEditor(float x, float y, float width)
        {
            UITextField field = settingsPanel.AddUIComponent<UITextField>();
            field.autoSize = false;
            field.width = width;
            field.height = 26f;
            field.relativePosition = new Vector3(x, y);
            field.textScale = 0.68f;
            field.textColor = new Color32(255, 255, 255, 255);
            field.color = new Color32(60, 92, 140, 255);
            field.horizontalAlignment = UIHorizontalAlignment.Center;
            field.verticalAlignment = UIVerticalAlignment.Middle;
            field.padding = new RectOffset(6, 6, 4, 4);
            field.normalBgSprite = "TextFieldPanel";
            field.hoveredBgSprite = "TextFieldPanelHovered";
            field.focusedBgSprite = "TextFieldPanelHovered";
            field.selectionSprite = "EmptySprite";
            field.builtinKeyNavigation = true;
            field.isInteractive = true;
            AttachDetailMouseWheel(field);
            return field;
        }

        private UIScrollbar CreateScrollbar()
        {
            UIScrollbar scrollbar = panel.AddUIComponent<UIScrollbar>();
            scrollbar.orientation = UIOrientation.Vertical;
            scrollbar.width = ScrollbarWidth;
            scrollbar.height = DetailScrollHeight;
            scrollbar.relativePosition = new Vector3(HorizontalPadding + DetailWidth + 6f, DetailTop);
            scrollbar.minValue = 0f;
            scrollbar.value = 0f;
            scrollbar.incrementAmount = 24f;
            AttachDetailMouseWheel(scrollbar);

            UISlicedSprite track = scrollbar.AddUIComponent<UISlicedSprite>();
            track.relativePosition = Vector3.zero;
            track.autoSize = false;
            track.width = scrollbar.width;
            track.height = scrollbar.height;
            track.spriteName = "ScrollbarTrack";
            scrollbar.trackObject = track;
            AttachDetailMouseWheel(track);

            UISlicedSprite thumb = track.AddUIComponent<UISlicedSprite>();
            thumb.relativePosition = Vector3.zero;
            thumb.autoSize = false;
            thumb.width = track.width;
            thumb.height = 24f;
            thumb.spriteName = "ScrollbarThumb";
            scrollbar.thumbObject = thumb;
            AttachDetailMouseWheel(thumb);

            return scrollbar;
        }

        private void AttachDetailMouseWheel(UIComponent component)
        {
            if (component == null)
            {
                return;
            }

            component.eventMouseWheel -= OnContentMouseWheel;
            component.eventMouseWheel += OnContentMouseWheel;
        }

        private void OnContentMouseWheel(UIComponent component, UIMouseEventParameter eventParam)
        {
            if (detailScrollbar == null || detailScroll == null || collapsed)
            {
                return;
            }

            if (activeEditingControl != null && activeEditingControl.Editor != null && ReferenceEquals(component, activeEditingControl.Editor))
            {
                return;
            }

            float nextValue = detailScrollbar.value - (eventParam.wheelDelta * detailScroll.scrollWheelAmount);
            float clamped = Mathf.Clamp(nextValue, detailScrollbar.minValue, detailScrollbar.maxValue);
            suppressScrollbarEvent = true;
            detailScrollbar.value = clamped;
            detailScroll.scrollPosition = new Vector2(0f, clamped);
            suppressScrollbarEvent = false;
        }

        private void SetLabelRows(UIPanel parent, List<UILabel> rows, IList<string> texts, ref int visibleCount)
        {
            while (rows.Count < texts.Count)
            {
                rows.Add(CreateCardRowLabel(parent));
                layoutDirty = true;
            }

            if (visibleCount != texts.Count)
            {
                visibleCount = texts.Count;
                layoutDirty = true;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                UILabel row = rows[i];
                if (i < texts.Count)
                {
                    row.text = texts[i];
                    row.isVisible = true;
                }
                else
                {
                    row.isVisible = false;
                }
            }
        }

        private string FormatNumericValue(object value, ConfigItemSliderAttribute slider)
        {
            if (value == null)
            {
                return "-";
            }

            if (value is float floatValue)
            {
                return slider != null && slider.ValueType == SliderValueType.Percentage
                    ? floatValue.ToString("0.0", cultureInfo) + "%"
                    : floatValue.ToString("0.0", cultureInfo);
            }

            if (slider != null && slider.ValueType == SliderValueType.Percentage)
            {
                return Convert.ToSingle(value, CultureInfo.InvariantCulture).ToString("0.0", cultureInfo) + "%";
            }

            return Convert.ToString(value, cultureInfo);
        }

        private string FormatPercent(float value)
        {
            return value.ToString("0.0", cultureInfo) + "%";
        }

        private static string FormatSigned(int value)
        {
            return value > 0 ? "+" + value.ToString(CultureInfo.InvariantCulture) : value.ToString(CultureInfo.InvariantCulture);
        }

        private void PositionPanel(UIView view)
        {
            float x = Mathf.Max(PanelMargin, view.fixedWidth - PanelWidth - PanelMargin);
            float y = Mathf.Max(PanelMargin, TopOffset);
            panel.relativePosition = new Vector3(x, y);
        }

        private static T GetCustomItemAttribute<T>(PropertyInfo property)
            where T : Attribute
        {
            return (T)property.GetCustomAttributes(typeof(T), false).FirstOrDefault();
        }

        private static bool IsPandemicTab(string tabId)
        {
            switch (tabId)
            {
                case "Quarantine":
                case "Pandemic":
                case "DiseaseProperties":
                case "Symptoms":
                case "PandemicMonitor":
                case "PandemicLockdown":
                    return true;
                default:
                    return false;
            }
        }

        private static string GetFriendlyName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];
                if (i > 0 && char.IsUpper(current) && !char.IsUpper(value[i - 1]) && value[i - 1] != ' ')
                {
                    builder.Append(' ');
                }

                builder.Append(current);
            }

            return builder.ToString();
        }

        private static string GetXRayMetricLabel(PandemicXRayMetric metric)
        {
            switch (metric)
            {
                case PandemicXRayMetric.Recovered:
                    return "Recovered";
                case PandemicXRayMetric.Dead:
                    return "Dead";
                default:
                    return "Infected";
            }
        }

        private static string GetXRayLocationLabel(PandemicXRayLocationMode locationMode)
        {
            return locationMode == PandemicXRayLocationMode.HomeLocations ? "Home" : "Live";
        }

        private static Color32 GetXRayMetricColor(PandemicXRayMetric metric)
        {
            switch (metric)
            {
                case PandemicXRayMetric.Recovered:
                    return new Color32(42, 160, 118, 255);
                case PandemicXRayMetric.Dead:
                    return new Color32(124, 66, 142, 255);
                default:
                    return new Color32(196, 106, 34, 255);
            }
        }

        private static Color32 GetHealthcareMetricColor(float hospitalPercent, float ambulancePercent)
        {
            float peak = Mathf.Max(hospitalPercent, ambulancePercent);
            if (peak >= 85f)
            {
                return new Color32(142, 58, 42, 255);
            }

            if (peak >= 60f)
            {
                return new Color32(148, 104, 44, 255);
            }

            return new Color32(54, 110, 92, 255);
        }

        private string FormatUsageDelta(float value)
        {
            if (Mathf.Abs(value) < 0.05f)
            {
                return "\u2192 0";
            }

            string arrow = value > 0f ? "\u2191" : "\u2193";
            return arrow + Mathf.Abs(value).ToString("0", cultureInfo);
        }

        private static string GetPublicTransportStateLabel(PandemicPublicTransportShutdownState state)
        {
            switch (state)
            {
                case PandemicPublicTransportShutdownState.Draining:
                    return "Draining";
                case PandemicPublicTransportShutdownState.Closed:
                    return "Closed";
                default:
                    return "Open";
            }
        }

        private enum DashboardSection
        {
            Age,
            Lockdown,
            Spreaders,
            Locations,
            Origins,
            Districts,
            Settings,
        }

        private sealed class PandemicSettingControl
        {
            public PropertyInfo Property;
            public ConfigItemAttribute Item;
            public ConfigItemSliderAttribute Slider;
            public UILabel Label;
            public UIButton MinusButton;
            public UIButton ValueButton;
            public UIButton PlusButton;
            public UITextField Editor;
            public bool IsEditing;
            public string EditorText;
            public string LastDisplayValue;
            public float Height;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Created by Unity")]
        private sealed class PandemicLivePanelUpdateBehavior : MonoBehaviour
        {
            private float nextRefreshTime;

            public PandemicLivePanel Owner { get; set; }

            public void Update()
            {
                if (Time.unscaledTime < nextRefreshTime)
                {
                    return;
                }

                PandemicManager manager = PandemicManager.Instance;
                nextRefreshTime = Time.unscaledTime + (manager?.GetPanelRefreshIntervalSeconds() ?? 1f);
                Owner?.Refresh();
            }
        }
    }
}
