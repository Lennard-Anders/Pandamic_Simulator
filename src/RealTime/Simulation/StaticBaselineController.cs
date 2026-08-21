// <copyright file="StaticBaselineController.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.Simulation
{
    using System;
    using System.Reflection;
    using RealTime.Config;
    using UnityEngine;

    /// <summary>
    /// Applies a configurable static city baseline for demand and optional growth controls.
    /// </summary>
    internal sealed class StaticBaselineController : MonoBehaviour
    {
        private const int StableCityBuildingsPerFrame = 256;
        private const ushort StableUtilitySupplyBuffer = 60000;
        private const ushort StableSewageBuffer = 0;
        private const byte StableBuildingHealth = 100;
        private const float StableCommercialGoodsFloorRatio = 0.75f;

        private static readonly Notification.Problem1 EconomyAndZoningProblems = Notification.Problem1.NoCustomers
            | Notification.Problem1.NoResources
            | Notification.Problem1.NoGoods
            | Notification.Problem1.NoPlaceforGoods
            | Notification.Problem1.NoWorkers
            | Notification.Problem1.NoEducatedWorkers
            | Notification.Problem1.TooFewServices
            | Notification.Problem1.LandValueLow
            | Notification.Problem1.TaxesTooHigh
            | Notification.Problem1.NoFood;

        private static readonly Notification.Problem1 ServiceAndLogisticsProblems = Notification.Problem1.Garbage
            | Notification.Problem1.Death
            | Notification.Problem1.LandfillFull
            | Notification.Problem1.NoInputProducts
            | Notification.Problem1.NoFishingGoods
            | Notification.Problem1.NoPlaceForFishingGoods
            | Notification.Problem1.WasteTransferFacilityFull;

        private static readonly Notification.Problem2 ServiceAndLogisticsProblems2 = Notification.Problem2.NoCargoServicePoint
            | Notification.Problem2.NoGarbageServicePoint;

        private static readonly Notification.Problem1 UtilitiesAndConnectivityProblems = Notification.Problem1.Electricity
            | Notification.Problem1.Sewage
            | Notification.Problem1.ElectricityNotConnected;

        private static readonly Notification.Problem2 UtilitiesAndConnectivityProblems2 = Notification.Problem2.None;

        private static readonly MethodInfo CommercialGetGoodsAmountMethod = typeof(CommercialBuildingAI).GetMethod(
            "GetGoodsAmount",
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            new[] { typeof(Building).MakeByRefType() },
            new ParameterModifier[0]);

        private static readonly MethodInfo CommercialGetGoodsCapacityMethod = typeof(CommercialBuildingAI).GetMethod(
            "GetGoodsCapacity",
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            new[] { typeof(ushort), typeof(Building).MakeByRefType() },
            new ParameterModifier[0]);

        private static readonly MethodInfo CommercialSetGoodsAmountMethod = typeof(CommercialBuildingAI).GetMethod(
            "SetGoodsAmount",
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            new[] { typeof(Building).MakeByRefType(), typeof(ushort) },
            new ParameterModifier[0]);

        private static readonly Notification.Problem1 SafetyAndEnvironmentProblems = Notification.Problem1.Fire
            | Notification.Problem1.Crime
            | Notification.Problem1.Pollution
            | Notification.Problem1.DirtyWater
            | Notification.Problem1.Flood
            | Notification.Problem1.Snow
            | Notification.Problem1.StructureDamaged
            | Notification.Problem1.StructureVisited
            | Notification.Problem1.StructureVisitedService;

        private static readonly Notification.Problem1 AreaAndDlcProblems = Notification.Problem1.NoPark
            | Notification.Problem1.NotInIndustryArea
            | Notification.Problem1.WrongAreaType
            | Notification.Problem1.ResourceNotSelected
            | Notification.Problem1.NoNaturalResources
            | Notification.Problem1.WrongCampusAreaType
            | Notification.Problem1.NotInCampusArea
            | Notification.Problem1.NotInAirportArea;

        private static readonly Notification.Problem2 AreaAndDlcProblems2 = Notification.Problem2.NotInPedestrianZone
            | Notification.Problem2.PedestrianZoneHighCargoTraffic
            | Notification.Problem2.PedestrianZoneHighGarbageTraffic;

        private RealTimeConfig config;
        private RuntimeState lastRuntimeState = RuntimeState.Off;
        private bool hasVanillaDemandSnapshot;
        private int lastVanillaResidentialDemand;
        private int lastVanillaCommercialDemand;
        private int lastVanillaWorkplaceDemand;
        private bool lastVanillaFullDemand;
        private ushort nextStableCityBuildingId = 1;

        private enum RuntimeState
        {
            Off,
            Growth,
            Stabilization,
        }

        [Flags]
        private enum StableCityProblemGroup
        {
            None = 0,
            EconomyAndZoning = 1 << 0,
            ServiceAndLogistics = 1 << 1,
            UtilitiesAndConnectivity = 1 << 2,
            SafetyAndEnvironment = 1 << 3,
            AreaAndDlcRestrictions = 1 << 4,
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
        /// Gets a value indicating whether the stable city sandbox is enabled.
        /// </summary>
        public bool IsStableCitySandboxEnabled => config?.StaticBaselineStableCityEnabled == true;

        /// <summary>Resets demand bookkeeping after experiment settings change in place.</summary>
        public void RefreshConfiguration()
        {
            ZoneManager zoneManager = ZoneManager.instance;
            if (zoneManager != null && lastRuntimeState != RuntimeState.Off)
            {
                RestoreVanillaDemand(zoneManager);
            }

            lastRuntimeState = RuntimeState.Off;
            hasVanillaDemandSnapshot = false;
            nextStableCityBuildingId = 1;
        }

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
            ApplyStableCitySandbox();
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

        private void ApplyStableCitySandbox()
        {
            StableCityProblemGroup groups = GetStableCityProblemGroups();
            if (groups == StableCityProblemGroup.None || BuildingManager.instance == null)
            {
                return;
            }

            Building[] buildings = BuildingManager.instance.m_buildings.m_buffer;
            if (buildings == null || buildings.Length <= 1)
            {
                return;
            }

            ushort maxBuildingId = (ushort)(buildings.Length - 1);
            ushort currentId = nextStableCityBuildingId;

            for (int i = 0; i < StableCityBuildingsPerFrame; i++)
            {
                if (currentId == 0 || currentId > maxBuildingId)
                {
                    currentId = 1;
                }

                ref Building building = ref buildings[currentId];
                if (ShouldSanitizeBuilding(ref building))
                {
                    SanitizeBuilding(currentId, ref building, groups);
                }

                currentId++;
            }

            nextStableCityBuildingId = currentId > maxBuildingId ? (ushort)1 : currentId;
        }

        private StableCityProblemGroup GetStableCityProblemGroups()
        {
            if (!IsStableCitySandboxEnabled)
            {
                return StableCityProblemGroup.None;
            }

            StableCityProblemGroup groups = StableCityProblemGroup.None;
            if (config.StaticBaselineNeutralizeEconomyAndZoningProblems)
            {
                groups |= StableCityProblemGroup.EconomyAndZoning;
            }

            if (config.StaticBaselineNeutralizeServiceAndLogisticsProblems)
            {
                groups |= StableCityProblemGroup.ServiceAndLogistics;
            }

            if (config.StaticBaselineNeutralizeUtilitiesAndConnectivityProblems)
            {
                groups |= StableCityProblemGroup.UtilitiesAndConnectivity;
            }

            if (config.StaticBaselineNeutralizeSafetyAndEnvironmentProblems)
            {
                groups |= StableCityProblemGroup.SafetyAndEnvironment;
            }

            if (config.StaticBaselineNeutralizeAreaAndDlcRestrictions)
            {
                groups |= StableCityProblemGroup.AreaAndDlcRestrictions;
            }

            return groups;
        }

        private static bool ShouldSanitizeBuilding(ref Building building)
        {
            if (building.Info == null)
            {
                return false;
            }

            Building.Flags flags = building.m_flags;
            return (flags & (Building.Flags.Created | Building.Flags.Completed)) == (Building.Flags.Created | Building.Flags.Completed)
                && (flags & Building.Flags.Deleted) == 0;
        }

        private static void SanitizeBuilding(ushort buildingId, ref Building building, StableCityProblemGroup groups)
        {
            Notification.Problem1 problem1Mask = Notification.Problem1.None;
            Notification.Problem2 problem2Mask = Notification.Problem2.None;

            if ((groups & StableCityProblemGroup.EconomyAndZoning) != 0)
            {
                SanitizeEconomyAndZoning(buildingId, ref building);
                problem1Mask |= EconomyAndZoningProblems;
            }

            if ((groups & StableCityProblemGroup.ServiceAndLogistics) != 0)
            {
                SanitizeServiceAndLogistics(ref building);
                problem1Mask |= ServiceAndLogisticsProblems;
                problem2Mask |= ServiceAndLogisticsProblems2;
            }

            if ((groups & StableCityProblemGroup.UtilitiesAndConnectivity) != 0)
            {
                SanitizeUtilitiesAndConnectivity(ref building);
                problem1Mask |= UtilitiesAndConnectivityProblems;
                problem2Mask |= UtilitiesAndConnectivityProblems2;
            }

            if ((groups & StableCityProblemGroup.SafetyAndEnvironment) != 0)
            {
                SanitizeSafetyAndEnvironment(ref building);
                problem1Mask |= SafetyAndEnvironmentProblems;
            }

            if ((groups & StableCityProblemGroup.AreaAndDlcRestrictions) != 0)
            {
                SanitizeAreaAndDlcRestrictions(ref building);
                problem1Mask |= AreaAndDlcProblems;
                problem2Mask |= AreaAndDlcProblems2;
            }

            if (problem1Mask != Notification.Problem1.None)
            {
                building.m_problems = building.m_problems & ~problem1Mask;
            }

            if (problem2Mask != Notification.Problem2.None)
            {
                building.m_problems = building.m_problems & ~problem2Mask;
            }

            building.m_problems = building.m_problems & ~Notification.Problem1.MajorProblem;
            building.m_problems = building.m_problems & ~Notification.Problem1.FatalProblem;
        }

        private static void SanitizeEconomyAndZoning(ushort buildingId, ref Building building)
        {
            building.m_incomingProblemTimer = 0;
            building.m_outgoingProblemTimer = 0;
            building.m_workerProblemTimer = 0;
            building.m_serviceProblemTimer = 0;
            building.m_taxProblemTimer = 0;
            building.m_majorProblemTimer = 0;
            StabilizeCommercialGoods(buildingId, ref building);
        }

        private static void SanitizeServiceAndLogistics(ref Building building)
        {
            building.m_garbageBuffer = 0;
            building.m_garbageTrafficRate = 0;
            building.m_mailBuffer = 0;
            building.m_deathProblemTimer = 0;
            building.m_healthProblemTimer = 0;
            building.m_serviceProblemTimer = 0;
            building.m_majorProblemTimer = 0;
        }

        private static void SanitizeUtilitiesAndConnectivity(ref Building building)
        {
            building.m_electricityBuffer = StableUtilitySupplyBuffer;
            building.m_sewageBuffer = StableSewageBuffer;
            building.m_electricityProblemTimer = 0;
        }

        private static void SanitizeSafetyAndEnvironment(ref Building building)
        {
            building.m_crimeBuffer = 0;
            building.m_fireHazard = 0;
            building.m_fireIntensity = 0;
            building.m_waterPollution = 0;
            building.m_health = StableBuildingHealth;
            building.m_serviceProblemTimer = 0;
            building.m_majorProblemTimer = 0;
        }

        private static void SanitizeAreaAndDlcRestrictions(ref Building building)
        {
            building.m_serviceProblemTimer = 0;
            building.m_majorProblemTimer = 0;
        }

        private static void StabilizeCommercialGoods(ushort buildingId, ref Building building)
        {
            if (buildingId == 0 || building.Info == null)
            {
                return;
            }

            CommercialBuildingAI commercialAI = building.Info.m_buildingAI as CommercialBuildingAI;
            if (commercialAI == null)
            {
                return;
            }

            if (CommercialGetGoodsAmountMethod == null || CommercialGetGoodsCapacityMethod == null || CommercialSetGoodsAmountMethod == null)
            {
                return;
            }

            int capacity = GetCommercialGoodsCapacity(commercialAI, buildingId, ref building);
            if (capacity <= 1)
            {
                return;
            }

            ushort currentAmount = GetCommercialGoodsAmount(commercialAI, ref building);
            ushort floorAmount = (ushort)Math.Min(capacity - 1, Math.Max(1, Mathf.CeilToInt(capacity * StableCommercialGoodsFloorRatio)));
            if (currentAmount >= floorAmount)
            {
                return;
            }

            SetCommercialGoodsAmount(commercialAI, ref building, floorAmount);
        }

        private static int GetCommercialGoodsCapacity(CommercialBuildingAI commercialAI, ushort buildingId, ref Building building)
        {
            object[] args = { buildingId, building };
            object result = CommercialGetGoodsCapacityMethod.Invoke(commercialAI, args);
            building = (Building)args[1];
            return result is int capacity ? capacity : 0;
        }

        private static ushort GetCommercialGoodsAmount(CommercialBuildingAI commercialAI, ref Building building)
        {
            object[] args = { building };
            object result = CommercialGetGoodsAmountMethod.Invoke(commercialAI, args);
            building = (Building)args[0];
            return result is ushort amount ? amount : (ushort)0;
        }

        private static void SetCommercialGoodsAmount(CommercialBuildingAI commercialAI, ref Building building, ushort amount)
        {
            object[] args = { building, amount };
            CommercialSetGoodsAmountMethod.Invoke(commercialAI, args);
            building = (Building)args[0];
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
