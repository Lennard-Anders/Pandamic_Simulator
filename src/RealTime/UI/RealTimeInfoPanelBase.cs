// <copyright file="RealTimeInfoPanelBase.cs" company="dymanoid">Copyright (c) dymanoid. All rights reserved.</copyright>

namespace RealTime.UI
{
    using System;
    using System.Text;
    using ColossalFramework.UI;
    using RealTime.CustomAI;
    using RealTime.Pandemic;
    using SkyTools.Localization;
    using SkyTools.UI;
    using UnityEngine;
    using static Localization.TranslationKeys;

    /// <summary>A base class for the customized world info panels.</summary>
    /// <typeparam name="T">The type of the game world info panel to customize.</typeparam>
    internal abstract class RealTimeInfoPanelBase<T> : CustomInfoPanelBase<T>
        where T : WorldInfoPanel
    {
        private const string ComponentId = "RealTimeInfoSchedule";
        private const string PandemicComponentId = "RealTimePandemicStatus";
        private const string AgeEducationLabelName = "AgeEducation";
        private const float LineHeight = 14f;

        private readonly RealTimeResidentAI<ResidentAI, Citizen> residentAI;
        private readonly ILocalizationProvider localizationProvider;
        private UILabel scheduleLabel;
        private UILabel pandemicLabel;
        private UIPanel pandemicButtonPanel;
        private UIButton maskToggleButton;
        private UIButton quarantineToggleButton;
        private CitizenSchedule scheduleCopy;
        private uint currentViewedCitizenId;
        private UIButton infectButton;
        private UIButton infectionSourceButton;
        private ushort infectionSourceBuildingId;

        /// <summary>Initializes a new instance of the <see cref="RealTimeInfoPanelBase{T}"/> class.</summary>
        /// <param name="panelName">Name of the game's panel object.</param>
        /// <param name="residentAI">The custom resident AI.</param>
        /// <param name="localizationProvider">The localization provider to use for text translation.</param>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when <paramref name="residentAI"/> or <paramref name="localizationProvider"/> is null.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Thrown when <paramref name="panelName"/> is null or an empty string.
        /// </exception>
        protected RealTimeInfoPanelBase(string panelName, RealTimeResidentAI<ResidentAI, Citizen> residentAI, ILocalizationProvider localizationProvider)
            : base(panelName)
        {
            this.residentAI = residentAI ?? throw new System.ArgumentNullException(nameof(residentAI));
            this.localizationProvider = localizationProvider ?? throw new System.ArgumentNullException(nameof(localizationProvider));
        }

        /// <summary>Disables the custom citizen info panel, if it is enabled.</summary>
        protected sealed override void DisableCore()
        {
            if (scheduleLabel != null)
            {
                ItemsPanel.RemoveUIComponent(scheduleLabel);
                UnityEngine.Object.Destroy(scheduleLabel.gameObject);
                scheduleLabel = null;
            }

            if (pandemicLabel != null)
            {
                ItemsPanel.RemoveUIComponent(pandemicLabel);
                UnityEngine.Object.Destroy(pandemicLabel.gameObject);
                pandemicLabel = null;
            }

            if (pandemicButtonPanel != null)
            {
                ItemsPanel.RemoveUIComponent(pandemicButtonPanel);
                UnityEngine.Object.Destroy(pandemicButtonPanel.gameObject);
                pandemicButtonPanel = null;
                maskToggleButton = null;
                quarantineToggleButton = null;
                infectButton = null;
                infectionSourceButton = null;
            }
        }

        /// <summary>Updates the citizen information for the citizen with specified ID.</summary>
        /// <param name="citizenId">The citizen ID.</param>
        protected void UpdateCitizenInfo(uint citizenId)
        {
            if (citizenId == 0)
            {
                SetCustomPanelVisibility(scheduleLabel, visible: false);
                SetCustomPanelVisibility(pandemicLabel, visible: false);
                if (pandemicButtonPanel != null) pandemicButtonPanel.isVisible = false;
                return;
            }

            UpdatePandemicInfo(citizenId);

            ref CitizenSchedule schedule = ref residentAI.GetCitizenSchedule(citizenId);

            if (schedule.LastScheduledState == scheduleCopy.LastScheduledState
                && schedule.ScheduledStateTime == scheduleCopy.ScheduledStateTime
                && schedule.WorkStatus == scheduleCopy.WorkStatus
                && schedule.VacationDaysLeft == scheduleCopy.VacationDaysLeft
                && schedule.WorkShift == scheduleCopy.WorkShift)
            {
                return;
            }

            if (schedule.LastScheduledState == ResidentState.Ignored)
            {
                return;
            }

            SetCustomPanelVisibility(scheduleLabel, false);
            scheduleCopy = schedule;
            BuildTextInfo(ref schedule);
        }

        /// <summary>Builds up the custom UI objects for the info panel.</summary>
        /// <returns><c>true</c> on success; otherwise, <c>false</c>.</returns>
        protected sealed override bool InitializeCore()
        {
            var statusLabel = ItemsPanel.Find<UILabel>(AgeEducationLabelName);
            if (statusLabel == null)
            {
                return false;
            }

            scheduleLabel = UIComponentTools.CreateCopy(statusLabel, ItemsPanel, ComponentId);
            scheduleLabel.width = 270;
            scheduleLabel.zOrder = statusLabel.zOrder + 1;
            scheduleLabel.isVisible = false;

            pandemicLabel = UIComponentTools.CreateCopy(statusLabel, ItemsPanel, PandemicComponentId);
            pandemicLabel.width = 270;
            pandemicLabel.zOrder = statusLabel.zOrder + 2;
            pandemicLabel.isVisible = false;

            var itemsAsPanel = ItemsPanel as UIPanel;
            if (itemsAsPanel != null)
            {
                pandemicButtonPanel = itemsAsPanel.AddUIComponent<UIPanel>();
                pandemicButtonPanel.name = "RealTimePandemicActionButtons";
                pandemicButtonPanel.autoSize = false;
                pandemicButtonPanel.width = 270f;
                pandemicButtonPanel.height = 32f;
                pandemicButtonPanel.isVisible = false;
                pandemicButtonPanel.zOrder = statusLabel.zOrder + 3;

                maskToggleButton = CreateCitizenToggleButton(pandemicButtonPanel, "Mask: OFF", 0f, 84f);
                maskToggleButton.eventClicked += (c, e) => OnMaskToggleClicked();

                quarantineToggleButton = CreateCitizenToggleButton(pandemicButtonPanel, "Quarantine", 88f, 84f);
                quarantineToggleButton.eventClicked += (c, e) => OnQuarantineToggleClicked();

                infectButton = CreateCitizenToggleButton(pandemicButtonPanel, "Infect", 176f, 84f);
                infectButton.color = new Color32(200, 40, 40, 255);
                infectButton.eventClicked += (c, e) => OnInfectClicked();

                infectionSourceButton = pandemicButtonPanel.AddUIComponent<UIButton>();
                infectionSourceButton.autoSize = false;
                infectionSourceButton.width = 270f;
                infectionSourceButton.height = 20f;
                infectionSourceButton.relativePosition = new Vector3(0f, 34f);
                infectionSourceButton.textScale = 0.72f;
                infectionSourceButton.textColor = new Color32(80, 200, 255, 255);
                infectionSourceButton.hoveredTextColor = new Color32(150, 230, 255, 255);
                infectionSourceButton.normalBgSprite = string.Empty;
                infectionSourceButton.isVisible = false;
                infectionSourceButton.textHorizontalAlignment = UIHorizontalAlignment.Left;
                infectionSourceButton.eventClicked += (c, e) => OnInfectionSourceClicked();
            }

            return true;
        }

        private void BuildTextInfo(ref CitizenSchedule schedule)
        {
            var info = new StringBuilder(100);
            float labelHeight = 0;
            if (schedule.LastScheduledState != ResidentState.Unknown)
            {
                string action = localizationProvider.Translate(ScheduledAction + "." + schedule.LastScheduledState.ToString());
                if (!string.IsNullOrEmpty(action))
                {
                    info.Append(localizationProvider.Translate(ScheduledAction)).Append(": ").Append(action);
                    labelHeight += LineHeight;
                }
            }

            if (schedule.ScheduledStateTime != default)
            {
                string action = localizationProvider.Translate(NextScheduledAction);
                if (!string.IsNullOrEmpty(action))
                {
                    if (info.Length > 0)
                    {
                        info.AppendLine();
                    }

                    info.Append(action).Append(": ").Append(schedule.ScheduledStateTime.ToString(localizationProvider.CurrentCulture));
                    labelHeight += LineHeight;
                }
            }

            if (schedule.WorkShift != WorkShift.Unemployed)
            {
                string workShift = localizationProvider.Translate(WorkShiftKey + "." + schedule.WorkShift.ToString());
                if (!string.IsNullOrEmpty(workShift))
                {
                    if (info.Length > 0)
                    {
                        info.AppendLine();
                    }

                    info.Append(workShift);
                    labelHeight += LineHeight;

                    if (schedule.WorkStatus == WorkStatus.OnVacation)
                    {
                        string vacation = localizationProvider.Translate(WorkStatusOnVacation);
                        if (!string.IsNullOrEmpty(vacation))
                        {
                            info.Append(' ');
                            info.AppendFormat(vacation, schedule.VacationDaysLeft);
                        }
                    }
                }
            }

            scheduleLabel.height = labelHeight;
            scheduleLabel.text = info.ToString();
            SetCustomPanelVisibility(scheduleLabel, info.Length > 0);
        }

        private void UpdatePandemicInfo(uint citizenId)
        {
            if (pandemicLabel == null)
            {
                return;
            }

            try
            {
                currentViewedCitizenId = citizenId;

                var manager = PandemicManager.Instance;
                if (manager == null)
                {
                    SetCustomPanelVisibility(pandemicLabel, false);
                    if (pandemicButtonPanel != null) pandemicButtonPanel.isVisible = false;
                    return;
                }

                bool infected = manager.IsCitizenInfected(citizenId);
                bool inQuarantine = QuarantineManager.Instance.IsInQuarantine(
                    citizenId,
                    ColossalFramework.Singleton<SimulationManager>.instance.m_currentGameTime);
                bool wearsMask = manager.IsCitizenWearingMask(citizenId);
                bool socialDistance = QuarantineManager.Instance.InLockDown;

                var sb = new StringBuilder(180);
                sb.AppendLine("--- Pandemic Status ---");
                sb.AppendLine("Infected:     " + (infected ? "YES" : "No"));
                sb.AppendLine("Quarantine:   " + (inQuarantine ? "YES" : "No"));
                sb.AppendLine("Mask:         " + (wearsMask ? "YES" : "No"));
                sb.AppendLine("Social Dist.: " + (socialDistance ? "YES" : "No"));

                if (infected)
                {
                    int days = manager.GetDaysInfected(citizenId);
                    float deathPct = manager.GetCitizenDeathProbabilityPercent(citizenId);
                    if (days >= 0)
                    {
                        sb.AppendLine("Days Infected: " + days);
                    }

                    sb.AppendLine(string.Format("Death Risk:   {0:F1}%", deathPct));
                }
                else
                {
                    sb.Append(string.Empty);
                    infectionSourceBuildingId = 0;
                }

                pandemicLabel.text = sb.ToString();
                pandemicLabel.height = (infected ? 7 : 5) * LineHeight + 14f;
                SetCustomPanelVisibility(pandemicLabel, true);

                // Update per-citizen action buttons
                if (pandemicButtonPanel != null)
                {
                    pandemicButtonPanel.isVisible = true;
                    RefreshCitizenPandemicButtons(citizenId, inQuarantine, wearsMask, infected);
                    RefreshInfectionSource(citizenId, infected);
                }
            }
            catch (Exception)
            {
                SetCustomPanelVisibility(pandemicLabel, false);
                if (pandemicButtonPanel != null) pandemicButtonPanel.isVisible = false;
            }
        }

        private void OnMaskToggleClicked()
        {
            if (currentViewedCitizenId == 0) return;
            var mgr = PandemicManager.Instance;
            if (mgr == null) return;
            bool isMasked = mgr.IsCitizenWearingMask(currentViewedCitizenId);
            mgr.ForceSetCitizenMask(currentViewedCitizenId, !isMasked);
            bool inQ = QuarantineManager.Instance.IsInQuarantine(
                currentViewedCitizenId,
                ColossalFramework.Singleton<SimulationManager>.instance.m_currentGameTime);
            RefreshCitizenPandemicButtons(currentViewedCitizenId, inQ, !isMasked);
        }

        private void OnQuarantineToggleClicked()
        {
            if (currentViewedCitizenId == 0) return;
            var mgr = PandemicManager.Instance;
            if (mgr == null) return;
            mgr.ToggleCitizenQuarantine(currentViewedCitizenId);
            bool inQ = QuarantineManager.Instance.IsInQuarantine(
                currentViewedCitizenId,
                ColossalFramework.Singleton<SimulationManager>.instance.m_currentGameTime);
            bool masked = mgr.IsCitizenWearingMask(currentViewedCitizenId);
            RefreshCitizenPandemicButtons(currentViewedCitizenId, inQ, masked);
        }

        private void RefreshCitizenPandemicButtons(uint citizenId, bool inQuarantine, bool masked, bool infected = false)
        {
            if (maskToggleButton == null || quarantineToggleButton == null) return;
            if (infectButton != null) infectButton.isVisible = !infected;
            maskToggleButton.text = "Mask: " + (masked ? "ON" : "OFF");
            maskToggleButton.color = masked
                ? new Color32(30, 160, 30, 255)
                : new Color32(160, 30, 30, 255);
            quarantineToggleButton.text = inQuarantine ? "Unquarantine" : "Quarantine";
            quarantineToggleButton.color = inQuarantine
                ? new Color32(30, 160, 30, 255)
                : new Color32(100, 100, 100, 255);
        }

        private void RefreshInfectionSource(uint citizenId, bool infected)
        {
            if (infectionSourceButton == null) return;
            infectionSourceBuildingId = 0;
            if (!infected)
            {
                infectionSourceButton.isVisible = false;
                pandemicButtonPanel.height = 32f;
                return;
            }

            var manager = PandemicManager.Instance;
            ushort bldId;
            InfectionType infType;
            if (manager == null || !manager.TryGetInfectionSource(citizenId, out bldId, out infType))
            {
                infectionSourceButton.isVisible = false;
                pandemicButtonPanel.height = 32f;
                return;
            }

            string sourceText;
            if (infType == InfectionType.INDOOR && bldId != 0)
            {
                string bldName = BuildingManager.instance.GetBuildingName(bldId, new InstanceID { Building = bldId });
                sourceText = "\u21b3 Infected at: " + (string.IsNullOrEmpty(bldName) ? "a building" : bldName);
                infectionSourceBuildingId = bldId;
            }
            else if (infType == InfectionType.VEHICLE)
            {
                sourceText = "\u21b3 Infected: In a vehicle";
            }
            else
            {
                sourceText = "\u21b3 Infected: Outdoors";
            }

            infectionSourceButton.text = sourceText;
            infectionSourceButton.isVisible = true;
            pandemicButtonPanel.height = 56f;
        }

        private void OnInfectionSourceClicked()
        {
            if (infectionSourceBuildingId == 0) return;
            try
            {
                var pos = BuildingManager.instance.m_buildings.m_buffer[infectionSourceBuildingId].m_position;
                var cc = UnityEngine.Object.FindObjectOfType<CameraController>();
                if (cc != null)
                {
                    InstanceID iid = new InstanceID();
                    iid.Building = infectionSourceBuildingId;
                    cc.SetTarget(iid, pos, true);
                }
            }
            catch { }
        }

        private void OnInfectClicked()
        {
            if (currentViewedCitizenId == 0) return;
            var mgr = PandemicManager.Instance;
            if (mgr == null) return;
            mgr.ManuallyInfectCitizen(currentViewedCitizenId);
            UpdatePandemicInfo(currentViewedCitizenId);
        }

        private static UIButton CreateCitizenToggleButton(UIPanel parent, string label, float x, float width)
        {
            var btn = parent.AddUIComponent<UIButton>();
            btn.autoSize = false;
            btn.width = width;
            btn.height = 26f;
            btn.relativePosition = new UnityEngine.Vector3(x, 2f);
            btn.text = label;
            btn.textScale = 0.72f;
            btn.textColor = new Color32(255, 255, 255, 255);
            btn.normalBgSprite = "ButtonMenu";
            btn.hoveredBgSprite = "ButtonMenuHovered";
            btn.pressedBgSprite = "ButtonMenuPressed";
            btn.textHorizontalAlignment = UIHorizontalAlignment.Center;
            return btn;
        }
    }
}