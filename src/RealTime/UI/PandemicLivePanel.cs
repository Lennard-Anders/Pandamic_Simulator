// <copyright file="PandemicLivePanel.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.UI
{
    using System;
    using System.Globalization;
    using System.Text;
    using ColossalFramework.UI;
    using RealTime.Pandemic;
    using SkyTools.Tools;
    using UnityEngine;

    /// <summary>
    /// Shows a compact live monitor with pandemic metrics directly in-game.
    /// </summary>
    internal sealed class PandemicLivePanel
    {
        private const string PanelName = "RealTimePandemicLivePanel";
        private const float PanelWidth = 460f;
        private const float ExpandedPanelHeight = 428f;
        private const float DistrictsCollapsedPanelHeight = 332f;
        private const float CollapsedPanelHeight = 38f;
        private const float PanelMargin = 15f;
        private const float TopOffset = 105f;
        private const float HorizontalPadding = 12f;
        private const float ContentWidth = PanelWidth - (2f * HorizontalPadding);
        private const float SummaryY = 36f;
        private const float SummaryHeight = 110f;
        private const float AgeHeaderY = 150f;
        private const float AgeBodyY = 170f;
        private const float AgeBodyHeight = 88f;
        private const float DistrictHeaderY = 264f;
        private const float DistrictHeaderHeight = 24f;
        private const float DistrictScrollY = 294f;
        private const float DistrictScrollHeight = 92f;
        private const float DistrictScrollbarWidth = 10f;
        private const float DistrictContentWidth = ContentWidth - DistrictScrollbarWidth - 6f;
        private const float DistrictLineHeight = 18f;
        private const float ButtonsYExpanded = 390f;
        private const float ButtonsYCollapsed = 294f;
        private const float ButtonWidth = 140f;
        private const float ButtonHeight = 28f;
        private const float ButtonSpacing = 8f;

        private CultureInfo cultureInfo = CultureInfo.CurrentCulture;
        private bool collapsed;
        private bool districtsExpanded = true;
        private UIPanel panel;
        private UILabel titleLabel;
        private UILabel summaryLabel;
        private UILabel ageHeaderLabel;
        private UILabel ageBodyLabel;
        private UIButton districtToggleButton;
        private UIScrollablePanel districtScrollPanel;
        private UILabel districtBodyLabel;
        private UIScrollbar districtScrollbar;
        private UIButton maskButton;
        private UIButton quarantineButton;
        private UIButton lockdownButton;

        /// <summary>Enables the live panel.</summary>
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
            panel.opacity = 0.9f;
            panel.canFocus = false;
            panel.isInteractive = true;
            panel.clipChildren = true;
            PositionPanel(view);

            titleLabel = panel.AddUIComponent<UILabel>();
            titleLabel.text = "\u25bc Real Time Pandemic Monitor";
            titleLabel.textScale = 0.95f;
            titleLabel.relativePosition = new Vector3(HorizontalPadding, 10f);
            titleLabel.autoSize = false;
            titleLabel.width = ContentWidth;
            titleLabel.height = 20f;
            titleLabel.textAlignment = UIHorizontalAlignment.Left;
            titleLabel.isInteractive = true;
            titleLabel.tooltip = "Click to collapse / expand";
            titleLabel.eventClicked += (c, e) => ToggleCollapse();

            summaryLabel = CreateBodyLabel(SummaryY, ContentWidth, SummaryHeight, 0.78f);

            ageHeaderLabel = panel.AddUIComponent<UILabel>();
            ageHeaderLabel.text = "Infected by Age Group";
            ageHeaderLabel.textScale = 0.82f;
            ageHeaderLabel.relativePosition = new Vector3(HorizontalPadding, AgeHeaderY);
            ageHeaderLabel.autoSize = false;
            ageHeaderLabel.width = ContentWidth;
            ageHeaderLabel.height = 18f;

            ageBodyLabel = CreateBodyLabel(AgeBodyY, ContentWidth, AgeBodyHeight, 0.76f);
            ageBodyLabel.wordWrap = false;

            districtToggleButton = CreateSectionButton("District Infection Rates", DistrictHeaderY);
            districtToggleButton.eventClicked += (c, e) => ToggleDistricts();

            districtScrollPanel = panel.AddUIComponent<UIScrollablePanel>();
            districtScrollPanel.relativePosition = new Vector3(HorizontalPadding, DistrictScrollY);
            districtScrollPanel.width = DistrictContentWidth;
            districtScrollPanel.height = DistrictScrollHeight;
            districtScrollPanel.clipChildren = true;
            districtScrollPanel.builtinKeyNavigation = true;
            districtScrollPanel.scrollWheelAmount = 60;
            districtScrollPanel.scrollWheelDirection = UIOrientation.Vertical;
            districtScrollPanel.useTouchMouseScroll = true;
            districtScrollPanel.eventMouseWheel += OnDistrictScrollMouseWheel;

            districtBodyLabel = districtScrollPanel.AddUIComponent<UILabel>();
            districtBodyLabel.relativePosition = Vector3.zero;
            districtBodyLabel.autoSize = false;
            districtBodyLabel.width = DistrictContentWidth;
            districtBodyLabel.height = 4096f;
            districtBodyLabel.wordWrap = false;
            districtBodyLabel.textScale = 0.74f;
            districtBodyLabel.textAlignment = UIHorizontalAlignment.Left;

            districtScrollbar = CreateDistrictScrollbar();
            districtScrollPanel.verticalScrollbar = districtScrollbar;
            districtScrollbar.eventValueChanged += (c, value) => districtScrollPanel.scrollPosition = new Vector2(0f, value);

            float buttonsX = HorizontalPadding;
            maskButton = CreateToggleButton("Masks", buttonsX, ButtonsYExpanded, ButtonWidth);
            maskButton.eventClicked += (c, e) => { PandemicManager.Instance?.ToggleMasks(); RefreshButtons(); };

            quarantineButton = CreateToggleButton("Quarantine", buttonsX + ButtonWidth + ButtonSpacing, ButtonsYExpanded, ButtonWidth);
            quarantineButton.eventClicked += (c, e) => { PandemicManager.Instance?.ToggleQuarantine(); RefreshButtons(); };

            lockdownButton = CreateToggleButton("Social Dist.", buttonsX + ((ButtonWidth + ButtonSpacing) * 2f), ButtonsYExpanded, ButtonWidth);
            lockdownButton.eventClicked += (c, e) => { PandemicManager.Instance?.ToggleLockdown(); RefreshButtons(); };

            PandemicLivePanelUpdateBehavior updater = panel.gameObject.AddComponent<PandemicLivePanelUpdateBehavior>();
            updater.Owner = this;
            ApplyExpandedLayout();
            Refresh();
        }

        /// <summary>Disables and destroys the panel.</summary>
        public void Disable()
        {
            if (panel != null)
            {
                UnityEngine.Object.Destroy(panel.gameObject);
                panel = null;
                titleLabel = null;
                summaryLabel = null;
                ageHeaderLabel = null;
                ageBodyLabel = null;
                districtToggleButton = null;
                districtScrollPanel = null;
                districtBodyLabel = null;
                districtScrollbar = null;
                maskButton = null;
                quarantineButton = null;
                lockdownButton = null;
            }
        }

        /// <summary>Updates the panel culture (numbers/date format).</summary>
        /// <param name="culture">The culture to use.</param>
        public void Translate(CultureInfo culture)
        {
            cultureInfo = culture ?? CultureInfo.CurrentCulture;
            Refresh();
        }

        internal void Refresh()
        {
            if (panel == null || summaryLabel == null)
            {
                return;
            }

            UIView view = UIView.GetAView();
            if (view != null)
            {
                PositionPanel(view);
            }

            if (collapsed)
            {
                return;
            }

            PandemicManager manager = PandemicManager.Instance;
            if (manager == null)
            {
                summaryLabel.text = "Pandemic manager not available in this game session.";
                ageBodyLabel.text = "No age-group data available.";
                districtBodyLabel.text = "No district data available.";
                SyncDistrictScrollbar();
                return;
            }

            PandemicLiveSnapshot snapshot = manager.GetLiveSnapshot();
            if (snapshot == null)
            {
                summaryLabel.text = "Pandemic monitor unavailable.";
                ageBodyLabel.text = "No age-group data available.";
                districtBodyLabel.text = "No district data available.";
                SyncDistrictScrollbar();
                return;
            }

            summaryLabel.text = BuildSummaryText(snapshot);
            ageBodyLabel.text = BuildAgeGroupText(snapshot);
            districtBodyLabel.text = BuildDistrictText(snapshot);

            SyncDistrictScrollbar();
            RefreshDistrictToggle();
            RefreshButtons();
        }

        private UILabel CreateBodyLabel(float y, float width, float height, float textScale)
        {
            UILabel label = panel.AddUIComponent<UILabel>();
            label.relativePosition = new Vector3(HorizontalPadding, y);
            label.autoSize = false;
            label.wordWrap = true;
            label.width = width;
            label.height = height;
            label.textScale = textScale;
            label.textAlignment = UIHorizontalAlignment.Left;
            return label;
        }

        private UIButton CreateSectionButton(string label, float y)
        {
            UIButton button = panel.AddUIComponent<UIButton>();
            button.autoSize = false;
            button.width = ContentWidth;
            button.height = DistrictHeaderHeight;
            button.relativePosition = new Vector3(HorizontalPadding, y);
            button.text = label;
            button.textScale = 0.76f;
            button.textColor = new Color32(255, 255, 255, 255);
            button.normalBgSprite = "ButtonMenu";
            button.hoveredBgSprite = "ButtonMenuHovered";
            button.pressedBgSprite = "ButtonMenuPressed";
            button.textHorizontalAlignment = UIHorizontalAlignment.Left;
            return button;
        }

        private UIButton CreateToggleButton(string label, float x, float y, float width)
        {
            UIButton button = panel.AddUIComponent<UIButton>();
            button.autoSize = false;
            button.width = width;
            button.height = ButtonHeight;
            button.relativePosition = new Vector3(x, y);
            button.text = label;
            button.textScale = 0.75f;
            button.textColor = new Color32(255, 255, 255, 255);
            button.normalBgSprite = "ButtonMenu";
            button.hoveredBgSprite = "ButtonMenuHovered";
            button.pressedBgSprite = "ButtonMenuPressed";
            button.textHorizontalAlignment = UIHorizontalAlignment.Center;
            return button;
        }

        private UIScrollbar CreateDistrictScrollbar()
        {
            UIScrollbar scrollbar = panel.AddUIComponent<UIScrollbar>();
            scrollbar.orientation = UIOrientation.Vertical;
            scrollbar.width = DistrictScrollbarWidth;
            scrollbar.height = DistrictScrollHeight;
            scrollbar.relativePosition = new Vector3(
                HorizontalPadding + DistrictContentWidth + 6f,
                DistrictScrollY);
            scrollbar.minValue = 0f;
            scrollbar.value = 0f;
            scrollbar.incrementAmount = DistrictLineHeight;

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

        private string BuildSummaryText(PandemicLiveSnapshot snapshot)
        {
            string status = snapshot.IsInitialized ? (snapshot.IsActive ? "Running" : "Finished") : "Initializing";
            string timeValue = snapshot.SimulationTime == default(DateTime)
                ? "-"
                : snapshot.SimulationTime.ToString("g", cultureInfo);

            return
                $"Status: {status}\n"
                + $"Simulation time: {timeValue}\n"
                + $"SIRD: S {snapshot.Healthy.ToString("N0", cultureInfo)} | I {snapshot.Sick.ToString("N0", cultureInfo)} | R {snapshot.Recovered.ToString("N0", cultureInfo)} | D {snapshot.Dead.ToString("N0", cultureInfo)}\n"
                + $"Trend (last step): dI {FormatSigned(snapshot.DeltaSick)} | dR {FormatSigned(snapshot.DeltaRecovered)} | dD {FormatSigned(snapshot.DeltaDead)}\n"
                + $"Quarantine now: {snapshot.QuarantineCitizens.ToString("N0", cultureInfo)}\n"
                + $"Tests: positive {snapshot.PositiveTests.ToString("N0", cultureInfo)} | tracked {snapshot.TestedCitizens.ToString("N0", cultureInfo)}\n"
                + $"Contacts: citizens {snapshot.ContactsTrackedCitizens.ToString("N0", cultureInfo)} | pairs {snapshot.ContactsTrackedPairs.ToString("N0", cultureInfo)} | total {snapshot.ContactsRecordedTotal.ToString("N0", cultureInfo)}\n"
                + $"Transmissions: total {snapshot.TransmissionsTotal.ToString("N0", cultureInfo)} | indoor {snapshot.TransmissionsIndoor.ToString("N0", cultureInfo)} | outdoor {snapshot.TransmissionsOutdoor.ToString("N0", cultureInfo)} | vehicle {snapshot.TransmissionsVehicle.ToString("N0", cultureInfo)}";
        }

        private string BuildAgeGroupText(PandemicLiveSnapshot snapshot)
        {
            if (snapshot.AgeGroups == null || snapshot.AgeGroups.Count == 0)
            {
                return "No age-group data available.";
            }

            var builder = new StringBuilder();
            for (int i = 0; i < snapshot.AgeGroups.Count; i++)
            {
                PandemicAgeGroupSnapshot ageGroup = snapshot.AgeGroups[i];
                builder.Append(ageGroup.Label);
                builder.Append(" | ");
                builder.Append(FormatPercent(ageGroup.InfectedPercent));
                builder.Append(" | ");
                builder.Append(ageGroup.InfectedCount.ToString("N0", cultureInfo));

                if (i < snapshot.AgeGroups.Count - 1)
                {
                    builder.Append('\n');
                }
            }

            return builder.ToString();
        }

        private string BuildDistrictText(PandemicLiveSnapshot snapshot)
        {
            if (snapshot.Districts == null || snapshot.Districts.Count == 0)
            {
                return "No user-defined districts available.";
            }

            var builder = new StringBuilder();
            for (int i = 0; i < snapshot.Districts.Count; i++)
            {
                PandemicDistrictSnapshot district = snapshot.Districts[i];
                builder.Append(district.DistrictName);
                builder.Append(" | ");
                builder.Append(FormatPercent(district.InfectedPercent));
                builder.Append(" | ");
                builder.Append(district.InfectedResidents.ToString("N0", cultureInfo));
                builder.Append(" / ");
                builder.Append(district.ResidentCount.ToString("N0", cultureInfo));

                if (i < snapshot.Districts.Count - 1)
                {
                    builder.Append('\n');
                }
            }

            return builder.ToString();
        }

        private void ToggleCollapse()
        {
            collapsed = !collapsed;
            if (summaryLabel != null) summaryLabel.isVisible = !collapsed;
            if (ageHeaderLabel != null) ageHeaderLabel.isVisible = !collapsed;
            if (ageBodyLabel != null) ageBodyLabel.isVisible = !collapsed;
            if (districtToggleButton != null) districtToggleButton.isVisible = !collapsed;
            if (districtScrollPanel != null) districtScrollPanel.isVisible = !collapsed && districtsExpanded;
            if (districtScrollbar != null) districtScrollbar.isVisible = !collapsed && districtsExpanded;
            if (maskButton != null) maskButton.isVisible = !collapsed;
            if (quarantineButton != null) quarantineButton.isVisible = !collapsed;
            if (lockdownButton != null) lockdownButton.isVisible = !collapsed;

            panel.height = collapsed
                ? CollapsedPanelHeight
                : (districtsExpanded ? ExpandedPanelHeight : DistrictsCollapsedPanelHeight);

            titleLabel.text = (collapsed ? "\u25ba" : "\u25bc") + " Real Time Pandemic Monitor";
            if (!collapsed)
            {
                ApplyExpandedLayout();
            }
        }

        private void ToggleDistricts()
        {
            districtsExpanded = !districtsExpanded;
            RefreshDistrictToggle();
            ApplyExpandedLayout();
        }

        private void ApplyExpandedLayout()
        {
            if (panel == null || collapsed)
            {
                return;
            }

            bool showDistricts = districtsExpanded;
            panel.height = showDistricts ? ExpandedPanelHeight : DistrictsCollapsedPanelHeight;

            districtScrollPanel.isVisible = showDistricts;
            districtScrollbar.isVisible = showDistricts;

            float buttonsY = showDistricts ? ButtonsYExpanded : ButtonsYCollapsed;
            maskButton.relativePosition = new Vector3(maskButton.relativePosition.x, buttonsY);
            quarantineButton.relativePosition = new Vector3(quarantineButton.relativePosition.x, buttonsY);
            lockdownButton.relativePosition = new Vector3(lockdownButton.relativePosition.x, buttonsY);

            SyncDistrictScrollbar();
        }

        private void RefreshDistrictToggle()
        {
            if (districtToggleButton == null)
            {
                return;
            }

            districtToggleButton.text = (districtsExpanded ? "\u25bc " : "\u25ba ") + "District Infection Rates";
        }

        private void RefreshButtons()
        {
            if (maskButton == null)
            {
                return;
            }

            PandemicManager manager = PandemicManager.Instance;
            bool masksOn = manager != null && manager.IsMasksEnabled();
            bool quarantineOn = manager != null && manager.IsQuarantineEnabled();
            bool lockdownOn = manager != null && manager.IsLockdownEnabled();

            maskButton.text = "Masks: " + (masksOn ? "ON" : "OFF");
            maskButton.color = masksOn ? new Color32(30, 160, 30, 255) : new Color32(160, 30, 30, 255);
            quarantineButton.text = "Quarant: " + (quarantineOn ? "ON" : "OFF");
            quarantineButton.color = quarantineOn ? new Color32(30, 160, 30, 255) : new Color32(160, 30, 30, 255);
            lockdownButton.text = "Lock: " + (lockdownOn ? "ON" : "OFF");
            lockdownButton.color = lockdownOn ? new Color32(30, 160, 30, 255) : new Color32(160, 30, 30, 255);
        }

        private void SyncDistrictScrollbar()
        {
            if (districtScrollPanel == null || districtBodyLabel == null || districtScrollbar == null)
            {
                return;
            }

            int lineCount = CountLines(districtBodyLabel.text);
            float contentHeight = Mathf.Max(districtScrollPanel.height, (lineCount * DistrictLineHeight) + 6f);
            districtBodyLabel.height = contentHeight;
            districtScrollbar.maxValue = Mathf.Max(0f, contentHeight - districtScrollPanel.height);
            districtScrollbar.value = Mathf.Clamp(districtScrollbar.value, districtScrollbar.minValue, districtScrollbar.maxValue);
            districtScrollPanel.scrollPosition = new Vector2(0f, districtScrollbar.value);
        }

        private void OnDistrictScrollMouseWheel(UIComponent component, UIMouseEventParameter eventParam)
        {
            if (districtScrollbar == null || !districtsExpanded || collapsed)
            {
                return;
            }

            float nextValue = districtScrollbar.value - (eventParam.wheelDelta * districtScrollPanel.scrollWheelAmount);
            districtScrollbar.value = Mathf.Clamp(nextValue, districtScrollbar.minValue, districtScrollbar.maxValue);
        }

        private static int CountLines(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 1;
            }

            int lines = 1;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    lines++;
                }
            }

            return lines;
        }

        private string FormatPercent(float value)
        {
            return value.ToString("0.0", cultureInfo) + "%";
        }

        private static string FormatSigned(int value)
        {
            if (value > 0)
            {
                return "+" + value.ToString(CultureInfo.InvariantCulture);
            }

            return value.ToString(CultureInfo.InvariantCulture);
        }

        private void PositionPanel(UIView view)
        {
            float x = Mathf.Max(PanelMargin, view.fixedWidth - PanelWidth - PanelMargin);
            float y = Mathf.Max(PanelMargin, TopOffset);
            panel.relativePosition = new Vector3(x, y);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Created by Unity")]
        private sealed class PandemicLivePanelUpdateBehavior : MonoBehaviour
        {
            private float nextRefresh;

            public PandemicLivePanel Owner { get; set; }

            public void Update()
            {
                if (Time.unscaledTime < nextRefresh)
                {
                    return;
                }

                nextRefresh = Time.unscaledTime + 1f;
                Owner?.Refresh();
            }
        }
    }
}
