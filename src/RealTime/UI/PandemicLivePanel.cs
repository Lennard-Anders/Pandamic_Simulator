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
        private const string InfectedCurveName = "PandemicInfected";
        private const float PanelWidth = 700f;
        private const float ExpandedPanelHeight = 760f;
        private const float CollapsedPanelHeight = 38f;
        private const float PanelMargin = 15f;
        private const float TopOffset = 105f;
        private const float HorizontalPadding = 12f;
        private const float HeaderTop = 36f;
        private const float HeaderHeight = 246f;
        private const float HeaderWidth = PanelWidth - (HorizontalPadding * 2f);
        private const float ButtonHeight = 28f;
        private const float ButtonSpacing = 8f;
        private const float SummaryHeight = 64f;
        private const float ChartTitleHeight = 18f;
        private const float ChartHeight = 136f;
        private const float DetailTop = HeaderTop + HeaderHeight + 8f;
        private const float DetailScrollHeight = ExpandedPanelHeight - DetailTop - 10f;
        private const float ScrollbarWidth = 10f;
        private const float DetailWidth = PanelWidth - (HorizontalPadding * 2f) - ScrollbarWidth - 6f;
        private const float CardGap = 10f;
        private const float CardPadding = 8f;
        private const float CardHeaderHeight = 18f;
        private const float CardRowHeight = 18f;
        private const float CardRowSpacing = 2f;
        private const float CardButtonHeight = 22f;
        private const float CardMinHeight = 62f;
        private const float SettingsLabelWidth = 258f;
        private const float MarkerLabelSpacing = 18f;
        private static readonly FieldInfo GraphMinField = typeof(UIGraph).GetField("m_Min", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo GraphMaxField = typeof(UIGraph).GetField("m_Max", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly List<PandemicSettingControl> settingControls = new List<PandemicSettingControl>();
        private readonly List<UILabel> settingsHeadings = new List<UILabel>();
        private readonly List<UILabel> ageRows = new List<UILabel>();
        private readonly List<UILabel> originRows = new List<UILabel>();
        private readonly List<UILabel> districtRows = new List<UILabel>();
        private readonly List<UILabel> lockdownRows = new List<UILabel>();
        private readonly UIButton[] spreaderButtons = new UIButton[5];
        private readonly UIButton[] locationButtons = new UIButton[5];
        private readonly List<UISprite> chartMarkerLines = new List<UISprite>();
        private readonly List<UILabel> chartMarkerLabels = new List<UILabel>();

        private CultureInfo cultureInfo = CultureInfo.CurrentCulture;
        private bool collapsed;
        private bool settingsExpanded;
        private bool settingsBuilt;
        private bool layoutDirty = true;
        private int ageVisibleRows;
        private int originVisibleRows;
        private int districtVisibleRows;
        private int lockdownVisibleRows;
        private int spreaderVisibleRows;
        private int locationVisibleRows;

        private UIPanel panel;
        private UILabel titleLabel;
        private UIPanel headerPanel;
        private UIButton startRestartButton;
        private UIButton maskButton;
        private UIButton quarantineButton;
        private UIButton lockdownButton;
        private UIButton overlayButton;
        private UIButton xrayButton;
        private UILabel summaryLabel;
        private UILabel chartTitleLabel;
        private UIGraph infectedGraph;
        private UIPanel chartOverlay;
        private UILabel chartEmptyLabel;
        private UIScrollablePanel detailScroll;
        private UIPanel detailContentPanel;
        private UIScrollbar detailScrollbar;
        private UIPanel ageCard;
        private UILabel ageHeaderLabel;
        private UIPanel lockdownCard;
        private UILabel lockdownHeaderLabel;
        private UIPanel spreaderCard;
        private UILabel spreaderHeaderLabel;
        private UIPanel locationCard;
        private UILabel locationHeaderLabel;
        private UIPanel originCard;
        private UILabel originHeaderLabel;
        private UIPanel districtCard;
        private UILabel districtHeaderLabel;
        private UIPanel settingsCard;
        private UIButton settingsToggleButton;
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
            infectedGraph = null;
            chartOverlay = null;
            currentSnapshot = null;
            settingsBuilt = false;
            layoutDirty = true;
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
            chartMarkerLines.Clear();
            chartMarkerLabels.Clear();
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
            RefreshChart(currentSnapshot);
            RefreshSettingsControls();
            RefreshLayoutIfDirty();
        }

        private void CreateHeader()
        {
            headerPanel = panel.AddUIComponent<UIPanel>();
            headerPanel.width = HeaderWidth;
            headerPanel.height = HeaderHeight;
            headerPanel.relativePosition = new Vector3(HorizontalPadding, HeaderTop);
            headerPanel.autoLayout = false;

            float buttonWidth = (HeaderWidth - (ButtonSpacing * 2f)) / 3f;
            float secondRowY = ButtonHeight + ButtonSpacing;

            startRestartButton = CreateActionButton(headerPanel, "Start", 0f, 0f, buttonWidth);
            startRestartButton.eventClicked += (c, e) => OnStartRestartClicked();

            maskButton = CreateActionButton(headerPanel, "Masks", buttonWidth + ButtonSpacing, 0f, buttonWidth);
            maskButton.eventClicked += (c, e) => { PandemicManager.Instance?.ToggleMasks(); Refresh(); };

            quarantineButton = CreateActionButton(headerPanel, "Quarantine", (buttonWidth * 2f) + (ButtonSpacing * 2f), 0f, buttonWidth);
            quarantineButton.eventClicked += (c, e) => { PandemicManager.Instance?.ToggleQuarantine(); Refresh(); };

            lockdownButton = CreateActionButton(headerPanel, "Lockdown", 0f, secondRowY, buttonWidth);
            lockdownButton.eventClicked += (c, e) => { PandemicManager.Instance?.ToggleLockdown(); Refresh(); };

            overlayButton = CreateActionButton(headerPanel, "Overlays", buttonWidth + ButtonSpacing, secondRowY, buttonWidth);
            overlayButton.eventClicked += (c, e) => { PandemicManager.Instance?.ToggleWorldOverlays(); Refresh(); };

            xrayButton = CreateActionButton(headerPanel, "X-Ray", (buttonWidth * 2f) + (ButtonSpacing * 2f), secondRowY, buttonWidth);
            xrayButton.eventClicked += (c, e) => { PandemicManager.Instance?.CycleXRayMode(); Refresh(); };

            summaryLabel = headerPanel.AddUIComponent<UILabel>();
            summaryLabel.autoSize = false;
            summaryLabel.width = HeaderWidth;
            summaryLabel.height = SummaryHeight;
            summaryLabel.relativePosition = new Vector3(0f, (ButtonHeight * 2f) + ButtonSpacing + 10f);
            summaryLabel.textScale = 0.72f;
            summaryLabel.textAlignment = UIHorizontalAlignment.Left;

            chartTitleLabel = headerPanel.AddUIComponent<UILabel>();
            chartTitleLabel.autoSize = false;
            chartTitleLabel.width = HeaderWidth;
            chartTitleLabel.height = ChartTitleHeight;
            chartTitleLabel.relativePosition = new Vector3(0f, summaryLabel.relativePosition.y + SummaryHeight + 6f);
            chartTitleLabel.textScale = 0.8f;
            chartTitleLabel.text = "Infected Over Time";
            chartTitleLabel.textColor = new Color32(245, 245, 245, 255);

            infectedGraph = headerPanel.AddUIComponent<UIGraph>();
            infectedGraph.autoSize = false;
            infectedGraph.width = HeaderWidth;
            infectedGraph.height = ChartHeight;
            infectedGraph.relativePosition = new Vector3(0f, chartTitleLabel.relativePosition.y + ChartTitleHeight + 4f);
            infectedGraph.graphRect = new Rect(0.06f, 0.08f, 0.90f, 0.76f);
            infectedGraph.AxesColor = new Color32(120, 120, 120, 255);
            infectedGraph.HelpAxesColor = new Color32(60, 60, 60, 255);
            infectedGraph.AxesWidth = 1f;
            infectedGraph.HelpAxesWidth = 1f;
            infectedGraph.TextColor = new Color32(220, 220, 220, 255);
            infectedGraph.isInteractive = false;

            chartOverlay = headerPanel.AddUIComponent<UIPanel>();
            chartOverlay.autoSize = false;
            chartOverlay.width = infectedGraph.width;
            chartOverlay.height = infectedGraph.height;
            chartOverlay.relativePosition = infectedGraph.relativePosition;
            chartOverlay.isInteractive = false;
            chartOverlay.canFocus = false;

            chartEmptyLabel = chartOverlay.AddUIComponent<UILabel>();
            chartEmptyLabel.autoSize = false;
            chartEmptyLabel.width = infectedGraph.width - 16f;
            chartEmptyLabel.height = 24f;
            chartEmptyLabel.relativePosition = new Vector3(8f, (infectedGraph.height / 2f) - 12f);
            chartEmptyLabel.textScale = 0.75f;
            chartEmptyLabel.textAlignment = UIHorizontalAlignment.Center;
            chartEmptyLabel.textColor = new Color32(220, 220, 220, 255);
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
            detailScroll.eventMouseWheel += OnContentMouseWheel;

            detailContentPanel = detailScroll.AddUIComponent<UIPanel>();
            detailContentPanel.relativePosition = Vector3.zero;
            detailContentPanel.width = DetailWidth;
            detailContentPanel.height = DetailScrollHeight;
            detailContentPanel.autoLayout = false;

            detailScrollbar = CreateScrollbar();
            detailScroll.verticalScrollbar = detailScrollbar;
            detailScrollbar.eventValueChanged += (c, value) => detailScroll.scrollPosition = new Vector2(0f, value);
        }

        private void CreateCards()
        {
            ageCard = CreateCardPanel(detailContentPanel);
            ageHeaderLabel = CreateCardHeader(ageCard, "Infected by Age Group");

            lockdownCard = CreateCardPanel(detailContentPanel);
            lockdownHeaderLabel = CreateCardHeader(lockdownCard, "Lockdown Families");

            spreaderCard = CreateCardPanel(detailContentPanel);
            spreaderHeaderLabel = CreateCardHeader(spreaderCard, "Top Spreaders");
            for (int i = 0; i < spreaderButtons.Length; i++)
            {
                int captured = i;
                spreaderButtons[i] = CreateCardListButton(spreaderCard);
                spreaderButtons[i].eventClicked += (c, e) => FocusSpreader(captured);
            }

            locationCard = CreateCardPanel(detailContentPanel);
            locationHeaderLabel = CreateCardHeader(locationCard, "Top Origin Locations");
            for (int i = 0; i < locationButtons.Length; i++)
            {
                int captured = i;
                locationButtons[i] = CreateCardListButton(locationCard);
                locationButtons[i].eventClicked += (c, e) => FocusLocation(captured);
            }

            originCard = CreateCardPanel(detailContentPanel);
            originHeaderLabel = CreateCardHeader(originCard, "Origin Distribution");

            districtCard = CreateCardPanel(detailContentPanel);
            districtHeaderLabel = CreateCardHeader(districtCard, "District Infection Rates");

            settingsCard = CreateCardPanel(detailContentPanel);
            settingsToggleButton = CreateSectionButton(settingsCard, "Pandemic Settings");
            settingsToggleButton.eventClicked += (c, e) =>
            {
                settingsExpanded = !settingsExpanded;
                layoutDirty = true;
                Refresh();
            };

            settingsPanel = settingsCard.AddUIComponent<UIPanel>();
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
                maskButton.text = "Masks";
                quarantineButton.text = "Quarantine";
                lockdownButton.text = "Lockdown";
                overlayButton.text = "Overlays";
                xrayButton.text = "X-Ray: Off";
                return;
            }

            startRestartButton.text = snapshot.CanStart ? "Start" : "Restart";
            startRestartButton.color = snapshot.CanStart ? new Color32(30, 140, 200, 255) : new Color32(70, 110, 170, 255);

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

            xrayButton.text = "X-Ray: " + GetXRayModeLabel(snapshot.XRayMode);
            xrayButton.color = snapshot.XRayMode == PandemicXRayMode.Off
                ? new Color32(100, 100, 100, 255)
                : new Color32(180, 80, 40, 255);
        }

        private void RefreshSummary(PandemicLiveSnapshot snapshot)
        {
            if (summaryLabel == null)
            {
                return;
            }

            if (snapshot == null)
            {
                summaryLabel.text = "Pandemic manager not available in this game session.";
                return;
            }

            string timeValue = snapshot.SimulationTime == default(DateTime)
                ? "-"
                : snapshot.SimulationTime.ToString("g", cultureInfo);

            summaryLabel.text =
                "Status: " + snapshot.LifecycleState + " | Time: " + timeValue + " | Observations: " + snapshot.ObservationCount.ToString("N0", cultureInfo) + "\n"
                + "S " + snapshot.Healthy.ToString("N0", cultureInfo)
                + " | I " + snapshot.Sick.ToString("N0", cultureInfo)
                + " | R " + snapshot.Recovered.ToString("N0", cultureInfo)
                + " | D " + snapshot.Dead.ToString("N0", cultureInfo)
                + " | dI " + FormatSigned(snapshot.DeltaSick)
                + " | dR " + FormatSigned(snapshot.DeltaRecovered)
                + " | dD " + FormatSigned(snapshot.DeltaDead) + "\n"
                + "Quarantine " + snapshot.QuarantineCitizens.ToString("N0", cultureInfo)
                + " | Positive tests " + snapshot.PositiveTests.ToString("N0", cultureInfo)
                + " | Tested " + snapshot.TestedCitizens.ToString("N0", cultureInfo)
                + " | Contacts " + snapshot.ContactsTrackedCitizens.ToString("N0", cultureInfo)
                + " / " + snapshot.ContactsTrackedPairs.ToString("N0", cultureInfo) + "\n"
                + "Transmissions " + snapshot.TransmissionsTotal.ToString("N0", cultureInfo)
                + " | Indoor " + snapshot.TransmissionsIndoor.ToString("N0", cultureInfo)
                + " | Outdoor " + snapshot.TransmissionsOutdoor.ToString("N0", cultureInfo)
                + " | Vehicle " + snapshot.TransmissionsVehicle.ToString("N0", cultureInfo)
                + " | Overlays " + (snapshot.WorldOverlaysEnabled ? "ON" : "OFF");
        }

        private void RefreshAnalytics(PandemicLiveSnapshot snapshot)
        {
            RefreshAgeRows(snapshot);
            RefreshLockdownRows(snapshot);
            RefreshOriginRows(snapshot);
            RefreshDistrictRows(snapshot);
            RefreshSpreaderButtons(snapshot);
            RefreshLocationButtons(snapshot);
            settingsToggleButton.text = (settingsExpanded ? "\u25bc " : "\u25ba ") + "Pandemic Settings";
        }

        private void RefreshAgeRows(PandemicLiveSnapshot snapshot)
        {
            List<string> texts = snapshot == null || snapshot.AgeGroups.Count == 0
                ? new List<string> { "No age-group data available." }
                : snapshot.AgeGroups
                    .Select(age => age.Label + " | " + FormatPercent(age.InfectedPercent) + " | " + age.InfectedCount.ToString("N0", cultureInfo))
                    .ToList();

            SetLabelRows(ageCard, ageRows, texts, ref ageVisibleRows);
        }

        private void RefreshLockdownRows(PandemicLiveSnapshot snapshot)
        {
            List<string> texts = snapshot == null || snapshot.LockdownFamilies.Count == 0
                ? new List<string> { "No lockdown family data available." }
                : snapshot.LockdownFamilies
                    .Select(family => family.Label + " | " + (family.IsClosed ? "Closed" : "Open")
                        + " | infected " + FormatPercent(family.CurrentInfectedPercent)
                        + " | threshold " + FormatPercent(family.AutoCloseThresholdPercent)
                        + " | manual " + (family.ManualClosed ? "ON" : "OFF"))
                    .ToList();

            SetLabelRows(lockdownCard, lockdownRows, texts, ref lockdownVisibleRows);
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

            SetLabelRows(originCard, originRows, texts, ref originVisibleRows);
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

            SetLabelRows(districtCard, districtRows, texts, ref districtVisibleRows);
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
                }
                else
                {
                    button.text = "No spreader data available.";
                    button.isEnabled = false;
                }
            }
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
                }
                else
                {
                    button.text = "No origin location data available.";
                    button.isEnabled = false;
                }
            }
        }

        private void RefreshChart(PandemicLiveSnapshot snapshot)
        {
            if (infectedGraph == null || chartOverlay == null || chartEmptyLabel == null)
            {
                return;
            }

            ClearGraphCurves();
            HideChartMarkers();

            if (snapshot == null || !snapshot.HasChartData)
            {
                chartEmptyLabel.isVisible = true;
                chartEmptyLabel.text = snapshot != null && snapshot.LifecycleState == PandemicLifecycleState.Dormant
                    ? "Pandemic not started yet."
                    : "Waiting for chart data.";
                infectedGraph.Invalidate();
                return;
            }

            List<PandemicChartPointSnapshot> points = snapshot.ChartPoints
                .OrderBy(point => point.SimulationTime)
                .ToList();
            if (points.Count == 0)
            {
                chartEmptyLabel.isVisible = true;
                chartEmptyLabel.text = "Waiting for chart data.";
                infectedGraph.Invalidate();
                return;
            }

            DateTime startTime = points[0].SimulationTime;
            DateTime endTime = points[points.Count - 1].SimulationTime;
            if (snapshot.PolicyMarkers.Count > 0)
            {
                DateTime markerStart = snapshot.PolicyMarkers.Min(marker => marker.SimulationTime);
                DateTime markerEnd = snapshot.PolicyMarkers.Max(marker => marker.SimulationTime);
                if (markerStart < startTime)
                {
                    startTime = markerStart;
                }

                if (markerEnd > endTime)
                {
                    endTime = markerEnd;
                }
            }

            float[] graphData;
            if (points.Count == 1)
            {
                graphData = new[] { (float)points[0].InfectedCount, (float)points[0].InfectedCount };
                if (endTime <= startTime)
                {
                    endTime = startTime.AddHours(1);
                }
            }
            else
            {
                graphData = points.Select(point => (float)point.InfectedCount).ToArray();
            }

            if (endTime <= startTime)
            {
                endTime = startTime.AddHours(1);
            }

            float maxValue = Mathf.Max(10f, Mathf.Ceil(Mathf.Max(graphData.Max(), 0f) * 1.1f));
            infectedGraph.StartTime = startTime;
            infectedGraph.EndTime = endTime;
            infectedGraph.AddCurve(InfectedCurveName, string.Empty, graphData, 2f, new Color32(235, 96, 72, 255), 0f);
            GraphMinField?.SetValue(infectedGraph, 0f);
            GraphMaxField?.SetValue(infectedGraph, maxValue);
            chartEmptyLabel.isVisible = false;
            RefreshChartMarkers(snapshot.PolicyMarkers, startTime, endTime);
            infectedGraph.Invalidate();
        }

        private void RefreshChartMarkers(IList<PandemicPolicyMarkerSnapshot> markers, DateTime startTime, DateTime endTime)
        {
            if (markers == null || markers.Count == 0 || chartOverlay == null || infectedGraph == null)
            {
                HideChartMarkers();
                return;
            }

            List<PandemicPolicyMarkerSnapshot> orderedMarkers = markers
                .OrderBy(marker => marker.SimulationTime)
                .ToList();
            EnsureMarkerWidgets(orderedMarkers.Count);

            Rect graphRect = infectedGraph.graphRect;
            float plotX = graphRect.x * infectedGraph.width;
            float plotY = graphRect.y * infectedGraph.height;
            float plotWidth = graphRect.width * infectedGraph.width;
            float plotHeight = graphRect.height * infectedGraph.height;
            double rangeTicks = Math.Max(1d, (double)(endTime.Ticks - startTime.Ticks));
            float lastLabelX = float.MinValue;

            for (int i = 0; i < orderedMarkers.Count; i++)
            {
                PandemicPolicyMarkerSnapshot marker = orderedMarkers[i];
                float ratio = (float)((marker.SimulationTime.Ticks - startTime.Ticks) / rangeTicks);
                float x = plotX + (Mathf.Clamp01(ratio) * plotWidth);

                UISprite line = chartMarkerLines[i];
                line.isVisible = true;
                line.relativePosition = new Vector3(x - 1f, plotY, 0f);
                line.width = 2f;
                line.height = plotHeight;
                line.color = GetMarkerColor(marker);

                UILabel label = chartMarkerLabels[i];
                label.text = marker.ShortLabel;
                label.textColor = GetMarkerColor(marker);
                label.relativePosition = new Vector3(Mathf.Clamp(x - 8f, 0f, chartOverlay.width - 18f), Mathf.Max(0f, plotY - 14f), 0f);
                bool showLabel = x - lastLabelX >= MarkerLabelSpacing;
                label.isVisible = showLabel;
                if (showLabel)
                {
                    lastLabelX = x;
                }
            }

            for (int i = orderedMarkers.Count; i < chartMarkerLines.Count; i++)
            {
                chartMarkerLines[i].isVisible = false;
                chartMarkerLabels[i].isVisible = false;
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
                    control.ValueButton.text = FormatNumericValue(value, control.Slider);
                    control.ValueButton.color = new Color32(70, 110, 170, 255);
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
        }

        private void RefreshLayoutIfDirty()
        {
            if (!layoutDirty || detailContentPanel == null || detailScroll == null)
            {
                return;
            }

            float preservedScroll = detailScrollbar != null ? detailScrollbar.value : 0f;
            LayoutCards();
            SyncScrollbar(preservedScroll);
            layoutDirty = false;
        }

        private void LayoutCards()
        {
            float halfWidth = (DetailWidth - CardGap) / 2f;
            float y = 0f;

            float leftHeight = LayoutTextCard(ageCard, ageHeaderLabel, ageRows, 0f, y, halfWidth);
            float rightHeight = LayoutTextCard(lockdownCard, lockdownHeaderLabel, lockdownRows, halfWidth + CardGap, y, halfWidth);
            y += Mathf.Max(leftHeight, rightHeight) + CardGap;

            leftHeight = LayoutButtonCard(spreaderCard, spreaderHeaderLabel, spreaderButtons, 0f, y, halfWidth);
            rightHeight = LayoutButtonCard(locationCard, locationHeaderLabel, locationButtons, halfWidth + CardGap, y, halfWidth);
            y += Mathf.Max(leftHeight, rightHeight) + CardGap;

            y += LayoutTextCard(originCard, originHeaderLabel, originRows, 0f, y, DetailWidth) + CardGap;
            y += LayoutTextCard(districtCard, districtHeaderLabel, districtRows, 0f, y, DetailWidth) + CardGap;
            y += LayoutSettingsCard(y) + CardGap;

            detailContentPanel.height = Mathf.Max(DetailScrollHeight, y);
        }

        private float LayoutTextCard(UIPanel card, UILabel header, IList<UILabel> rows, float x, float y, float width)
        {
            card.relativePosition = new Vector3(x, y);
            card.width = width;

            float innerWidth = width - (CardPadding * 2f);
            header.relativePosition = new Vector3(CardPadding, CardPadding);
            header.width = innerWidth;

            float nextY = CardPadding + CardHeaderHeight + 4f;
            for (int i = 0; i < rows.Count; i++)
            {
                UILabel row = rows[i];
                if (!row.isVisible)
                {
                    continue;
                }

                row.relativePosition = new Vector3(CardPadding, nextY);
                row.width = innerWidth;
                nextY += CardRowHeight + CardRowSpacing;
            }

            card.height = Mathf.Max(CardMinHeight, nextY + CardPadding - CardRowSpacing);
            return card.height;
        }

        private float LayoutButtonCard(UIPanel card, UILabel header, IList<UIButton> buttons, float x, float y, float width)
        {
            card.relativePosition = new Vector3(x, y);
            card.width = width;

            float innerWidth = width - (CardPadding * 2f);
            header.relativePosition = new Vector3(CardPadding, CardPadding);
            header.width = innerWidth;

            float nextY = CardPadding + CardHeaderHeight + 4f;
            for (int i = 0; i < buttons.Count; i++)
            {
                UIButton button = buttons[i];
                if (!button.isVisible)
                {
                    continue;
                }

                button.relativePosition = new Vector3(CardPadding, nextY);
                button.width = innerWidth;
                nextY += CardButtonHeight + 4f;
            }

            card.height = Mathf.Max(CardMinHeight, nextY + CardPadding - 4f);
            return card.height;
        }

        private float LayoutSettingsCard(float y)
        {
            settingsCard.relativePosition = new Vector3(0f, y);
            settingsCard.width = DetailWidth;

            float innerWidth = DetailWidth - (CardPadding * 2f);
            settingsToggleButton.relativePosition = new Vector3(CardPadding, CardPadding);
            settingsToggleButton.width = innerWidth;

            float nextY = CardPadding + settingsToggleButton.height + 6f;
            settingsPanel.relativePosition = new Vector3(CardPadding, nextY);
            settingsPanel.isVisible = settingsExpanded;
            settingsPanel.width = innerWidth;

            settingsCard.height = settingsExpanded
                ? nextY + settingsPanel.height + CardPadding
                : nextY + CardPadding;

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
            detailScrollbar.value = clamped;
            detailScroll.scrollPosition = new Vector2(0f, clamped);
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
                FocusOnLocation(0, entry.FocusPosition);
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
                FocusOnLocation(entry.BuildingId, entry.FocusPosition);
            }
        }

        private void FocusOnLocation(ushort buildingId, Vector3 position)
        {
            CameraController cameraController = UnityEngine.Object.FindObjectOfType<CameraController>();
            if (cameraController == null)
            {
                return;
            }

            var instance = new InstanceID();
            if (buildingId != 0)
            {
                instance.Building = buildingId;
            }

            cameraController.SetTarget(instance, position, true);
        }

        private void ToggleCollapse()
        {
            collapsed = !collapsed;
            layoutDirty = true;
            Refresh();
        }

        private void ApplyCollapsedState()
        {
            if (panel == null)
            {
                return;
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

        private void AdjustNumericSetting(PandemicSettingControl control, int direction)
        {
            PandemicManager manager = PandemicManager.Instance;
            RealTimeConfig config = manager?.RuntimeConfig;
            if (config == null || control?.Slider == null)
            {
                return;
            }

            object currentValue = control.Property.GetValue(config, null);
            if (control.Property.PropertyType == typeof(int))
            {
                int next = (int)currentValue + (int)(control.Slider.Step * direction);
                next = Mathf.Clamp(next, (int)control.Slider.Min, (int)control.Slider.Max);
                control.Property.SetValue(config, next, null);
            }
            else if (control.Property.PropertyType == typeof(uint))
            {
                int next = (int)(uint)currentValue + (int)(control.Slider.Step * direction);
                next = Mathf.Clamp(next, (int)control.Slider.Min, (int)control.Slider.Max);
                control.Property.SetValue(config, (uint)next, null);
            }
            else if (control.Property.PropertyType == typeof(float))
            {
                float next = (float)currentValue + (control.Slider.Step * direction);
                next = Mathf.Clamp(next, control.Slider.Min, control.Slider.Max);
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
                control.MinusButton.eventClicked += (c, e) => AdjustNumericSetting(control, -1);
                control.PlusButton.eventClicked += (c, e) => AdjustNumericSetting(control, 1);
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
            return card;
        }

        private UILabel CreateCardHeader(UIComponent parent, string text)
        {
            UILabel label = parent.AddUIComponent<UILabel>();
            label.autoSize = false;
            label.height = CardHeaderHeight;
            label.textScale = 0.8f;
            label.text = text;
            label.textColor = new Color32(245, 245, 245, 255);
            return label;
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
            return button;
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
            return button;
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

            UISlicedSprite track = scrollbar.AddUIComponent<UISlicedSprite>();
            track.relativePosition = Vector3.zero;
            track.autoSize = false;
            track.width = scrollbar.width;
            track.height = scrollbar.height;
            track.spriteName = "ScrollbarTrack";
            scrollbar.trackObject = track;

            UISlicedSprite thumb = track.AddUIComponent<UISlicedSprite>();
            thumb.relativePosition = Vector3.zero;
            thumb.autoSize = false;
            thumb.width = track.width;
            thumb.height = 24f;
            thumb.spriteName = "ScrollbarThumb";
            scrollbar.thumbObject = thumb;

            return scrollbar;
        }

        private void OnContentMouseWheel(UIComponent component, UIMouseEventParameter eventParam)
        {
            if (detailScrollbar == null || collapsed)
            {
                return;
            }

            float nextValue = detailScrollbar.value - (eventParam.wheelDelta * detailScroll.scrollWheelAmount);
            detailScrollbar.value = Mathf.Clamp(nextValue, detailScrollbar.minValue, detailScrollbar.maxValue);
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

        private void ClearGraphCurves()
        {
            if (infectedGraph == null)
            {
                return;
            }

            while (infectedGraph.curveCount > 0)
            {
                var curve = infectedGraph.GetCurve(0);
                if (curve == null || string.IsNullOrEmpty(curve.name))
                {
                    infectedGraph.Clear();
                    break;
                }

                infectedGraph.RemoveCurve(curve.name);
            }
        }

        private void EnsureMarkerWidgets(int count)
        {
            while (chartMarkerLines.Count < count)
            {
                UISprite line = chartOverlay.AddUIComponent<UISprite>();
                line.spriteName = "ScrollbarTrack";
                line.isVisible = false;
                chartMarkerLines.Add(line);

                UILabel label = chartOverlay.AddUIComponent<UILabel>();
                label.autoSize = false;
                label.width = 18f;
                label.height = 12f;
                label.textScale = 0.58f;
                label.textAlignment = UIHorizontalAlignment.Center;
                label.isVisible = false;
                chartMarkerLabels.Add(label);
            }
        }

        private void HideChartMarkers()
        {
            for (int i = 0; i < chartMarkerLines.Count; i++)
            {
                chartMarkerLines[i].isVisible = false;
                chartMarkerLabels[i].isVisible = false;
            }
        }

        private static Color32 GetMarkerColor(PandemicPolicyMarkerSnapshot marker)
        {
            if (marker == null)
            {
                return new Color32(180, 180, 180, 255);
            }

            switch (marker.Type)
            {
                case PandemicPolicyMarkerType.Masks:
                    return marker.Enabled ? new Color32(48, 172, 212, 255) : new Color32(20, 118, 168, 255);
                case PandemicPolicyMarkerType.Lockdown:
                    return marker.Enabled ? new Color32(228, 124, 36, 255) : new Color32(186, 66, 32, 255);
                default:
                    return new Color32(180, 180, 180, 255);
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

        private static string GetXRayModeLabel(PandemicXRayMode mode)
        {
            switch (mode)
            {
                case PandemicXRayMode.LivePositions:
                    return "Live positions";
                case PandemicXRayMode.HomeLocations:
                    return "Home locations";
                default:
                    return "Off";
            }
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

                nextRefreshTime = Time.unscaledTime + 1f;
                Owner?.Refresh();
            }
        }
    }
}
