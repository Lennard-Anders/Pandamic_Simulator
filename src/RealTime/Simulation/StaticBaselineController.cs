// <copyright file="StaticBaselineController.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.Simulation
{
    using System;
    using RealTime.Config;
    using UnityEngine;

    /// <summary>
    /// Applies a configurable static city baseline for demand and optional growth controls.
    /// </summary>
    internal sealed class StaticBaselineController : MonoBehaviour
    {
        private RealTimeConfig config;
        private RuntimeState lastRuntimeState = RuntimeState.Off;
        private bool hasVanillaDemandSnapshot;
        private int lastVanillaResidentialDemand;
        private int lastVanillaCommercialDemand;
        private int lastVanillaWorkplaceDemand;
        private bool lastVanillaFullDemand;

        private enum RuntimeState
        {
            Off,
            Growth,
            Stabilization,
        }

        /// <summary>
        /// Gets the currently active baseline controller instance.
        /// </summary>
        public static StaticBaselineController Instance { get; private set; }

        /// <summary>
        /// Initializes this instance with the mod configuration.
        /// </summary>
        /// <param name="config">The configuration to use.</param>
        public void Init(RealTimeConfig config)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            Instance = this;
        }

        /// <summary>
        /// Gets a value indicating whether the static baseline demand override is currently enabled.
        /// </summary>
        public bool IsEnabled => config?.StaticBaselineEnabled == true;

        /// <summary>
        /// Gets a value indicating whether births are disabled by the optional baseline controls.
        /// </summary>
        public bool AreBirthsDisabled => config?.StaticBaselineDisableBirths == true;

        /// <summary>
        /// Gets a value indicating whether Real Time events are disabled by the optional baseline controls.
        /// </summary>
        public bool AreRealTimeEventsDisabled => config?.StaticBaselineDisableRealTimeEvents == true;

        /// <summary>
        /// Gets a value indicating whether tourist leisure is disabled by the optional baseline controls.
        /// </summary>
        public bool IsTouristLeisureDisabled => config?.StaticBaselineDisableTouristLeisure == true;

        /// <summary>
        /// Determines whether a building can currently be constructed or upgraded.
        /// </summary>
        /// <param name="service">The building service.</param>
        /// <returns><c>true</c> if construction or upgrading is allowed; otherwise, <c>false</c>.</returns>
        public bool CanBuildOrUpgrade(ItemClass.Service service)
            => config?.StaticBaselineFreezeConstructionAndUpgrades != true || !IsManagedDemandService(service);

        /// <summary>
        /// Determines whether building problem timers should be frozen.
        /// </summary>
        /// <param name="buildingId">The building to check.</param>
        /// <returns><c>true</c> if problem timers should stay unchanged; otherwise, <c>false</c>.</returns>
        public bool ShouldFreezeBuildingProblemTimers(ushort buildingId)
        {
            if (config?.StaticBaselineFreezeDemandProblemTimers != true || buildingId == 0)
            {
                return false;
            }

            ref Building building = ref BuildingManager.instance.m_buildings.m_buffer[buildingId];
            return building.Info != null && IsManagedDemandService(building.Info.GetService());
        }

        private void LateUpdate()
        {
            if (config == null)
            {
                return;
            }

            ApplyDemandState();
        }

        private void OnDestroy()
        {
            if (ZoneManager.instance != null && lastRuntimeState != RuntimeState.Off)
            {
                RestoreVanillaDemand(ZoneManager.instance);
            }

            if (ReferenceEquals(Instance, this))
            {
                Instance = null;
            }
        }

        private static bool IsManagedDemandService(ItemClass.Service service)
        {
            switch (service)
            {
                case ItemClass.Service.Residential:
                case ItemClass.Service.Commercial:
                case ItemClass.Service.Industrial:
                case ItemClass.Service.Office:
                    return true;

                default:
                    return false;
            }
        }

        private void ApplyDemandState()
        {
            ZoneManager zoneManager = ZoneManager.instance;
            if (zoneManager == null)
            {
                return;
            }

            RuntimeState currentState = GetCurrentRuntimeState();
            if (!hasVanillaDemandSnapshot)
            {
                CaptureVanillaDemand(zoneManager);
            }

            if (currentState == RuntimeState.Off)
            {
                if (lastRuntimeState != RuntimeState.Off)
                {
                    RestoreVanillaDemand(zoneManager);
                }

                CaptureVanillaDemand(zoneManager);
            }
            else
            {
                ApplyConfiguredDemand(zoneManager);
            }

            lastRuntimeState = currentState;
        }

        private RuntimeState GetCurrentRuntimeState()
        {
            if (!IsEnabled)
            {
                return RuntimeState.Off;
            }

            return config.StaticBaselineMode == StaticBaselineMode.Growth
                ? RuntimeState.Growth
                : RuntimeState.Stabilization;
        }

        private void CaptureVanillaDemand(ZoneManager zoneManager)
        {
            lastVanillaResidentialDemand = zoneManager.m_residentialDemand;
            lastVanillaCommercialDemand = zoneManager.m_commercialDemand;
            lastVanillaWorkplaceDemand = zoneManager.m_workplaceDemand;
            lastVanillaFullDemand = zoneManager.m_fullDemand;
            hasVanillaDemandSnapshot = true;
        }

        private void RestoreVanillaDemand(ZoneManager zoneManager)
        {
            if (!hasVanillaDemandSnapshot)
            {
                return;
            }

            zoneManager.m_residentialDemand = lastVanillaResidentialDemand;
            zoneManager.m_actualResidentialDemand = lastVanillaResidentialDemand;
            zoneManager.m_commercialDemand = lastVanillaCommercialDemand;
            zoneManager.m_actualCommercialDemand = lastVanillaCommercialDemand;
            zoneManager.m_workplaceDemand = lastVanillaWorkplaceDemand;
            zoneManager.m_actualWorkplaceDemand = lastVanillaWorkplaceDemand;
            zoneManager.m_fullDemand = lastVanillaFullDemand;
        }

        private void ApplyConfiguredDemand(ZoneManager zoneManager)
        {
            int residentialDemand = ClampDemand(config.StaticBaselineResidentialDemand);
            int commercialDemand = ClampDemand(config.StaticBaselineCommercialDemand);
            int workplaceDemand = ClampDemand((int)Math.Round((config.StaticBaselineIndustrialDemand + config.StaticBaselineOfficeDemand) / 2f));

            zoneManager.m_residentialDemand = residentialDemand;
            zoneManager.m_actualResidentialDemand = residentialDemand;
            zoneManager.m_commercialDemand = commercialDemand;
            zoneManager.m_actualCommercialDemand = commercialDemand;
            zoneManager.m_workplaceDemand = workplaceDemand;
            zoneManager.m_actualWorkplaceDemand = workplaceDemand;
            zoneManager.m_fullDemand = residentialDemand >= 100 && commercialDemand >= 100 && workplaceDemand >= 100;
        }

        private static int ClampDemand(int value)
        {
            if (value < 0)
            {
                return 0;
            }

            return value > 100 ? 100 : value;
        }
    }
}
