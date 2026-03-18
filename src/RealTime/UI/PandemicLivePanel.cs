// <copyright file="PandemicLivePanel.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.UI
{
    using System.Globalization;
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
        private const float PanelWidth = 420f;
        private const float PanelHeight = 290f;
        private const float PanelMargin = 15f;
        private const float TopOffset = 105f;

        private CultureInfo cultureInfo = CultureInfo.CurrentCulture;
        private UIPanel panel;
        private UILabel titleLabel;
        private UILabel bodyLabel;
        private UIButton maskButton;
        private UIButton quarantineButton;
        private UIButton lockdownButton;

        /// <summary>Enables the live panel.</summary>
        public void Enable()
        {
            var view = UIView.GetAView();
            if (view == null)
            {
                Log.Warning("The 'Real Time' pandemic live panel could not be created because UIView is missing.");
                return;
            }

            var existing = view.FindUIComponent<UIPanel>(PanelName);
            if (existing != null)
            {
                Object.Destroy(existing.gameObject);
            }

            panel = view.AddUIComponent(typeof(UIPanel)) as UIPanel;
            if (panel == null)
            {
                Log.Warning("The 'Real Time' pandemic live panel could not create UIPanel.");
                return;
            }
            panel.name = PanelName;
            panel.width = PanelWidth;
            panel.height = PanelHeight;
            panel.backgroundSprite = "MenuPanel2";
            panel.opacity = 0.9f;
            panel.canFocus = false;
            panel.isInteractive = true;
            panel.clipChildren = true;
            PositionPanel(view);

            titleLabel = panel.AddUIComponent<UILabel>();
            titleLabel.text = "Real Time Pandemic Monitor";
            titleLabel.textScale = 0.95f;
            titleLabel.relativePosition = new Vector3(12f, 10f);
            titleLabel.autoSize = false;
            titleLabel.width = PanelWidth - 24f;
            titleLabel.height = 20f;
            titleLabel.textAlignment = UIHorizontalAlignment.Left;

            bodyLabel = panel.AddUIComponent<UILabel>();
            bodyLabel.relativePosition = new Vector3(12f, 36f);
            bodyLabel.autoSize = false;
            bodyLabel.wordWrap = true;
            bodyLabel.width = PanelWidth - 24f;
            bodyLabel.height = PanelHeight - 46f - 58f;
            bodyLabel.textScale = 0.8f;
            bodyLabel.textAlignment = UIHorizontalAlignment.Left;

            float btnY = PanelHeight - 40f;
            float btnW = 118f;
            maskButton = CreateToggleButton(panel, "Masks", 12f, btnY, btnW);
            maskButton.eventClicked += (c, e) => { PandemicManager.Instance?.ToggleMasks(); RefreshButtons(); };

            quarantineButton = CreateToggleButton(panel, "Quarantine", 148f, btnY, btnW);
            quarantineButton.eventClicked += (c, e) => { PandemicManager.Instance?.ToggleQuarantine(); RefreshButtons(); };

            lockdownButton = CreateToggleButton(panel, "Social Dist.", 284f, btnY, btnW);
            lockdownButton.eventClicked += (c, e) => { PandemicManager.Instance?.ToggleLockdown(); RefreshButtons(); };

            var updater = panel.gameObject.AddComponent<PandemicLivePanelUpdateBehavior>();
            updater.Owner = this;
            Refresh();
        }

        /// <summary>Disables and destroys the panel.</summary>
        public void Disable()
        {
            if (panel != null)
            {
                Object.Destroy(panel.gameObject);
                panel = null;
                titleLabel = null;
                bodyLabel = null;
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
            if (panel == null || bodyLabel == null)
            {
                return;
            }

            var view = UIView.GetAView();
            if (view != null)
            {
                PositionPanel(view);
            }

            var manager = PandemicManager.Instance;
            if (manager == null)
            {
                bodyLabel.text = "Pandemic manager not available in this game session.";
                return;
            }

            var snapshot = manager.GetLiveSnapshot();
            if (snapshot == null)
            {
                bodyLabel.text = "Pandemic monitor unavailable.";
                return;
            }

            string status = snapshot.IsInitialized ? (snapshot.IsActive ? "Running" : "Finished") : "Initializing";
            string timeValue = snapshot.SimulationTime == default
                ? "-"
                : snapshot.SimulationTime.ToString("g", cultureInfo);

            bodyLabel.text =
                $"Status: {status}\n"
                + $"Simulation time: {timeValue}\n"
                + $"Population: H {snapshot.Healthy:N0} | S {snapshot.Sick:N0} | R {snapshot.Recovered:N0} | D {snapshot.Dead:N0}\n"
                + $"Trend (last step): dS {FormatSigned(snapshot.DeltaSick)} | dR {FormatSigned(snapshot.DeltaRecovered)} | dD {FormatSigned(snapshot.DeltaDead)}\n"
                + $"Quarantine now: {snapshot.QuarantineCitizens:N0}\n"
                + $"Tests: positive {snapshot.PositiveTests:N0} | tracked {snapshot.TestedCitizens:N0}\n"
                + $"Contacts: citizens {snapshot.ContactsTrackedCitizens:N0} | pairs {snapshot.ContactsTrackedPairs:N0} | total {snapshot.ContactsRecordedTotal:N0}\n"
                + $"Transmissions: total {snapshot.TransmissionsTotal:N0} | indoor {snapshot.TransmissionsIndoor:N0} | outdoor {snapshot.TransmissionsOutdoor:N0} | vehicle {snapshot.TransmissionsVehicle:N0}\n"
                + $"Trajectory points: {snapshot.ObservationCount:N0}";

            RefreshButtons();
        }

        private void RefreshButtons()
        {
            if (maskButton == null) return;
            var mgr = PandemicManager.Instance;
            bool masksOn = mgr != null && mgr.IsMasksEnabled();
            bool quarantineOn = mgr != null && mgr.IsQuarantineEnabled();
            bool lockdownOn = mgr != null && mgr.IsLockdownEnabled();

            maskButton.text = "Masks: " + (masksOn ? "ON" : "OFF");
            maskButton.color = masksOn ? new Color32(30, 160, 30, 255) : new Color32(160, 30, 30, 255);
            quarantineButton.text = "Quarant: " + (quarantineOn ? "ON" : "OFF");
            quarantineButton.color = quarantineOn ? new Color32(30, 160, 30, 255) : new Color32(160, 30, 30, 255);
            lockdownButton.text = "Lock: " + (lockdownOn ? "ON" : "OFF");
            lockdownButton.color = lockdownOn ? new Color32(30, 160, 30, 255) : new Color32(160, 30, 30, 255);
        }

        private static UIButton CreateToggleButton(UIPanel parent, string label, float x, float y, float width)
        {
            var btn = parent.AddUIComponent<UIButton>();
            btn.autoSize = false;
            btn.width = width;
            btn.height = 28f;
            btn.relativePosition = new Vector3(x, y);
            btn.text = label;
            btn.textScale = 0.75f;
            btn.textColor = new Color32(255, 255, 255, 255);
            btn.normalBgSprite = "ButtonMenu";
            btn.hoveredBgSprite = "ButtonMenuHovered";
            btn.pressedBgSprite = "ButtonMenuPressed";
            btn.textHorizontalAlignment = UIHorizontalAlignment.Center;
            return btn;
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
