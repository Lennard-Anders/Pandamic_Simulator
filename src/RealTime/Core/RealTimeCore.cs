// <copyright file="RealTimeCore.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.Core
{
    using UnityEngine;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using HarmonyAccessTools = Harmony.AccessTools;
    using RealTime.Config;
    using RealTime.CustomAI;
    using RealTime.Events;
    using RealTime.Events.Storage;
    using RealTime.Experiments;
    using RealTime.GameConnection;
    using RealTime.GameConnection.Patches;
    using RealTime.Pandemic;
    using RealTime.Simulation;
    using RealTime.UI;
    using SkyTools.Configuration;
    using SkyTools.GameTools;
    using SkyTools.Localization;
    using SkyTools.Patching;
    using SkyTools.Storage;
    using SkyTools.Tools;

    /// <summary>
    /// The core component of the Real Time mod. Activates and deactivates
    /// the different parts of the mod's logic.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1506:AvoidExcessiveClassCoupling", Justification = "This is the entry point and needs to instantiate all parts")]
    internal sealed class RealTimeCore
    {
        private const string HarmonyId = "com.cities_skylines.dymanoid.realtime";

        private readonly List<IStorageData> storageData = new List<IStorageData>();
        private readonly TimeAdjustment timeAdjustment;
        private readonly CustomTimeBar timeBar;
        private readonly PandemicLivePanel pandemicLivePanel;
        private readonly StaticBaselineController staticBaselineController;
        private readonly RealTimeEventManager eventManager;
        private readonly MethodPatcher patcher;
        private readonly VanillaEvents vanillaEvents;
        private readonly RealTimeConfig configuration;

        private GameObject pandemicManagerObject;
        private StorageBase levelStorage;
        private SpareTimeBehavior spareTimeBehavior;
        private WorkBehavior workBehavior;

        private bool isEnabled;

        private RealTimeCore(
            TimeAdjustment timeAdjustment,
            CustomTimeBar timeBar,
            PandemicLivePanel pandemicLivePanel,
            StaticBaselineController staticBaselineController,
            RealTimeEventManager eventManager,
            MethodPatcher patcher,
            VanillaEvents vanillaEvents,
            RealTimeConfig configuration)
        {
            this.timeAdjustment = timeAdjustment;
            this.timeBar = timeBar;
            this.pandemicLivePanel = pandemicLivePanel;
            this.staticBaselineController = staticBaselineController;
            this.eventManager = eventManager;
            this.patcher = patcher;
            this.vanillaEvents = vanillaEvents;
            this.configuration = configuration;
            isEnabled = true;
        }

        /// <summary>Gets a value indicating whether the mod is running in a restricted mode due to method patch failures.</summary>
        public bool IsRestrictedMode { get; private set; }

        /// <summary>Gets the mutable configuration instance retained by all level services.</summary>
        internal RealTimeConfig Configuration => configuration;

        /// <summary>Gets the Pandemic manager owned by this level, if it is still alive.</summary>
        internal PandemicManager PandemicManager => pandemicManagerObject == null
            ? null
            : pandemicManagerObject.GetComponent<PandemicManager>();

        /// <summary>Gets the live Pandemic panel so the experiment UI can be attached without replacing it.</summary>
        internal PandemicLivePanel PandemicLivePanel => pandemicLivePanel;

        /// <summary>Gets whether all level-owned services needed by an experiment are ready.</summary>
        internal bool IsExperimentReady => isEnabled
            && configuration != null
            && PandemicManager != null
            && SimulationManager.instance != null;

        /// <summary>
        /// Runs the mod by activating its parts.
        /// </summary>
        ///
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="configProvider"/> or <paramref name="localizationProvider"/>
        /// or <paramref name="compatibility"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="rootPath"/> is null or an empty string.</exception>
        ///
        /// <param name="configProvider">The configuration provider that provides the mod's configuration.</param>
        /// <param name="rootPath">The path to the mod's assembly. Additional files are stored here too.</param>
        /// <param name="localizationProvider">The <see cref="ILocalizationProvider"/> to use for text translation.</param>
        /// <param name="setDefaultTime"><c>true</c> to initialize the game time to a default value (real world date and city wake up hour);
        /// <c>false</c> to leave the game time unchanged.</param>
        /// <param name="compatibility">The compatibility checker object.</param>
        ///
        /// <returns>A <see cref="RealTimeCore"/> instance that can be used to stop the mod.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1506:AvoidExcessiveClassCoupling", Justification = "This is the entry point and needs to instantiate all parts")]
        public static RealTimeCore Run(
            ConfigurationProvider<RealTimeConfig> configProvider,
            string rootPath,
            ILocalizationProvider localizationProvider,
            bool setDefaultTime,
            Compatibility compatibility)
        {
            if (configProvider == null)
            {
                throw new ArgumentNullException(nameof(configProvider));
            }

            if (string.IsNullOrEmpty(rootPath))
            {
                throw new ArgumentException("The root path cannot be null or empty string", nameof(rootPath));
            }

            if (localizationProvider == null)
            {
                throw new ArgumentNullException(nameof(localizationProvider));
            }

            if (compatibility == null)
            {
                throw new ArgumentNullException(nameof(compatibility));
            }

            var patches = GetMethodPatches(compatibility);
            var patcher = new MethodPatcher(HarmonyId, patches);

            var appliedPatches = patcher.Apply();
            if (!CheckRequiredMethodPatches(appliedPatches, patches))
            {
                Log.Error("The 'Real Time' mod failed to perform method redirections for required methods");
                patcher.Revert();
                return null;
            }

            if (StorageBase.CurrentLevelStorage != null)
            {
                LoadStorageData(new[] { configProvider }, StorageBase.CurrentLevelStorage);
            }

            localizationProvider.SetEnglishUSFormatsState(configProvider.Configuration.UseEnglishUSFormats);

            var timeInfo = new TimeInfo(configProvider.Configuration);
            var buildingManager = new BuildingManagerConnection();
            var randomizer = new GameRandomizer();

            var weatherInfo = new WeatherInfo(new WeatherManagerConnection(), randomizer);

            var gameConnections = new GameConnections<Citizen>(
                timeInfo,
                new CitizenConnection(),
                new CitizenManagerConnection(),
                buildingManager,
                randomizer,
                new TransferManagerConnection(),
                weatherInfo);

            var eventManager = new RealTimeEventManager(
                configProvider.Configuration,
                CityEventsLoader.Instance,
                new EventManagerConnection(),
                buildingManager,
                randomizer,
                timeInfo,
                Constants.MaxTravelTime);

            if (!SetupCustomAI(
                timeInfo,
                configProvider.Configuration,
                gameConnections,
                eventManager,
                compatibility,
                out GameObject pandemicManagerObject,
                out SpareTimeBehavior spareTimeBehavior,
                out WorkBehavior workBehavior))
            {
                Log.Error("The 'Real Time' mod failed to setup the customized AI and will now be deactivated.");
                patcher.Revert();
                return null;
            }

            var timeAdjustment = new TimeAdjustment(configProvider.Configuration);
            var gameDate = timeAdjustment.Enable(setDefaultTime);
            SimulationHandler.CitizenProcessor.UpdateFrameDuration();

            CityEventsLoader.Instance.ReloadEvents(rootPath);

            var customTimeBar = new CustomTimeBar();
            customTimeBar.Enable(gameDate);
            customTimeBar.CityEventClick += CustomTimeBarCityEventClick;

            var pandemicLivePanel = new PandemicLivePanel();
            pandemicLivePanel.Enable();
            pandemicLivePanel.Translate(localizationProvider.CurrentCulture);

            if (GameObject.Find("StaticBaselineController") != null)
            {
                GameObject.Destroy(GameObject.Find("StaticBaselineController"));
            }

            var staticBaselineObject = new GameObject("StaticBaselineController");
            var staticBaselineController = staticBaselineObject.AddComponent<StaticBaselineController>();
            staticBaselineController.Init(configProvider.Configuration);

            var vanillaEvents = VanillaEvents.Customize();

            var result = new RealTimeCore(
                timeAdjustment,
                customTimeBar,
                pandemicLivePanel,
                staticBaselineController,
                eventManager,
                patcher,
                vanillaEvents,
                configProvider.Configuration);
            result.pandemicManagerObject = pandemicManagerObject;
            result.spareTimeBehavior = spareTimeBehavior;
            result.workBehavior = workBehavior;
            eventManager.EventsChanged += result.CityEventsChanged;

            var statistics = new Statistics(timeInfo, localizationProvider);
            if (statistics.Initialize())
            {
                statistics.RefreshUnits();
            }
            else
            {
                statistics = null;
            }

            SimulationHandler.NewDay += result.CityEventsChanged;

            SimulationHandler.TimeAdjustment = timeAdjustment;
            SimulationHandler.DayTimeSimulation = new DayTimeSimulation(configProvider.Configuration);
            SimulationHandler.EventManager = eventManager;
            SimulationHandler.WeatherInfo = weatherInfo;
            SimulationHandler.Buildings = BuildingAIPatch.RealTimeAI;
            SimulationHandler.Buildings.UpdateFrameDuration();

            if (appliedPatches.Contains(CitizenManagerPatch.CreateCitizenPatch1))
            {
                CitizenManagerPatch.NewCitizenBehavior = new NewCitizenBehavior(randomizer, configProvider.Configuration);
            }

            if (appliedPatches.Contains(BuildingAIPatch.GetColor))
            {
                SimulationHandler.Buildings.InitializeLightState();
            }

            SimulationHandler.Statistics = statistics;

            if (appliedPatches.Contains(WorldInfoPanelPatch.UpdateBindings))
            {
                WorldInfoPanelPatch.CitizenInfoPanel = CustomCitizenInfoPanel.Enable(ResidentAIPatch.RealTimeAI, localizationProvider);
                WorldInfoPanelPatch.VehicleInfoPanel = CustomVehicleInfoPanel.Enable(ResidentAIPatch.RealTimeAI, localizationProvider);
                WorldInfoPanelPatch.CampusWorldInfoPanel = CustomCampusWorldInfoPanel.Enable(localizationProvider);
                WorldInfoPanelPatch.BuildingInfoPanel = CustomBuildingInfoPanel.Enable();
            }

            AwakeSleepSimulation.Install(configProvider.Configuration);

            var schedulesStorage = ResidentAIPatch.RealTimeAI.GetStorageService(
                schedules => new CitizenScheduleStorage(schedules, gameConnections.CitizenManager.GetCitizensArray, timeInfo));

            result.storageData.Add(schedulesStorage);
            result.storageData.Add(eventManager);
            if (StorageBase.CurrentLevelStorage != null)
            {
                result.levelStorage = StorageBase.CurrentLevelStorage;
                result.levelStorage.GameSaving += result.GameSaving;
                LoadStorageData(result.storageData, result.levelStorage);
            }

            result.storageData.Add(configProvider);
            result.Translate(localizationProvider);
            result.IsRestrictedMode = appliedPatches.Count != patches.Count;

            return result;
        }

        /// <summary>Refreshes Real Time caches after an experiment scenario is copied into the live configuration.</summary>
        internal void RefreshExperimentConfiguration()
        {
            if (!isEnabled || configuration == null)
            {
                return;
            }

            configuration.Validate();
            timeAdjustment.Update(force: true);
            spareTimeBehavior?.RefreshConfiguration();
            workBehavior?.BeginNewDay();
            SimulationHandler.CitizenProcessor?.UpdateFrameDuration();
            SimulationHandler.Buildings?.UpdateFrameDuration();
            staticBaselineController?.RefreshConfiguration();
        }

        /// <summary>
        /// Stops the mod by deactivating all its parts.
        /// </summary>
        public void Stop()
        {
            if (!isEnabled)
            {
                return;
            }

            Log.Info("The 'Real Time' mod reverts method patches.");
            patcher.Revert();

            SimulationPacingPatch.StepMinutes = 0;
            SimulationPacingPatch.StepPending = false;

            ResidentAIPatch.RealTimeAI = null;
            TouristAIPatch.RealTimeAI = null;
            BuildingAIPatch.RealTimeAI = null;
            BuildingAIPatch.WeatherInfo = null;
            TransferManagerPatch.RealTimeAI = null;
            SimulationHandler.EventManager = null;
            SimulationHandler.DayTimeSimulation = null;
            SimulationHandler.TimeAdjustment = null;
            SimulationHandler.PandemicSimulationTick = null;
            PandemicManager?.StopStepSynchronization();
            SimulationHandler.WeatherInfo = null;
            SimulationHandler.Buildings = null;
            SimulationHandler.CitizenProcessor = null;
            SimulationHandler.Statistics?.Close();
            SimulationHandler.Statistics = null;
            ParkPatch.SpareTimeBehavior = null;
            OutsideConnectionAIPatch.SpareTimeBehavior = null;
            CitizenManagerPatch.NewCitizenBehavior = null;
            CitizenManagerPatch.CitizenReleased = null;

            vanillaEvents.Revert();

            timeAdjustment.Disable();
            timeBar.CityEventClick -= CustomTimeBarCityEventClick;
            timeBar.Disable();
            pandemicLivePanel?.Disable();
            if (pandemicManagerObject != null)
            {
                GameObject.Destroy(pandemicManagerObject);
                pandemicManagerObject = null;
            }

            if (staticBaselineController != null)
            {
                GameObject.Destroy(staticBaselineController.gameObject);
            }
            eventManager.EventsChanged -= CityEventsChanged;
            SimulationHandler.NewDay -= CityEventsChanged;

            CityEventsLoader.Instance.Clear();

            AwakeSleepSimulation.Uninstall();

            if (levelStorage != null)
            {
                levelStorage.GameSaving -= GameSaving;
                levelStorage = null;
            }

            WorldInfoPanelPatch.CitizenInfoPanel?.Disable();
            WorldInfoPanelPatch.CitizenInfoPanel = null;

            WorldInfoPanelPatch.VehicleInfoPanel?.Disable();
            WorldInfoPanelPatch.VehicleInfoPanel = null;

            WorldInfoPanelPatch.CampusWorldInfoPanel?.Disable();
            WorldInfoPanelPatch.CampusWorldInfoPanel = null;

            WorldInfoPanelPatch.BuildingInfoPanel?.Disable();
            WorldInfoPanelPatch.BuildingInfoPanel = null;

            isEnabled = false;
        }

        /// <summary>
        /// Translates all the mod's component to a different language obtained from
        /// the specified <paramref name="localizationProvider"/>.
        /// </summary>
        ///
        /// <exception cref="ArgumentNullException">Thrown when the argument is null.</exception>
        ///
        /// <param name="localizationProvider">An instance of the <see cref="ILocalizationProvider"/> to use for translation.</param>
        public void Translate(ILocalizationProvider localizationProvider)
        {
            if (localizationProvider == null)
            {
                throw new ArgumentNullException(nameof(localizationProvider));
            }

            timeBar.Translate(localizationProvider.CurrentCulture);
            UIGraphPatch.Translate(localizationProvider.CurrentCulture);
            pandemicLivePanel?.Translate(localizationProvider.CurrentCulture);
        }

        private static List<IPatch> GetMethodPatches(Compatibility compatibility)
        {
            var patches = new List<IPatch>();
            AddPatchIfAvailable(patches, BuildingAIPatch.GetConstructionTime, nameof(BuildingAIPatch.GetConstructionTime));
            AddPatchIfAvailable(patches, BuildingAIPatch.HandleWorkers, nameof(BuildingAIPatch.HandleWorkers));
            AddPatchIfAvailable(patches, BuildingAIPatch.CommercialSimulation, nameof(BuildingAIPatch.CommercialSimulation));
            AddPatchIfAvailable(patches, BuildingAIPatch.FishingMarketSimulation, nameof(BuildingAIPatch.FishingMarketSimulation));
            AddPatchIfAvailable(patches, BuildingAIPatch.GetColor, nameof(BuildingAIPatch.GetColor));
            AddPatchIfAvailable(patches, CitizenAIPatch.GetColor, nameof(CitizenAIPatch.GetColor));
            AddPatchIfAvailable(patches, BuildingAIPatch.CalculateUnspawnPosition, nameof(BuildingAIPatch.CalculateUnspawnPosition));
            AddPatchIfAvailable(patches, BuildingAIPatch.ProduceGoods, nameof(BuildingAIPatch.ProduceGoods));
            AddPatchIfAvailable(patches, BuildingAIPatch.TrySpawnBoot, nameof(BuildingAIPatch.TrySpawnBoot));
            AddPatchIfAvailable(patches, SimulationPacingPatch.FinalSpeed, nameof(SimulationPacingPatch.FinalSpeed));
            AddPatchIfAvailable(patches, ResidentAIPatch.Location, nameof(ResidentAIPatch.Location));
            AddPatchIfAvailable(patches, ResidentAIPatch.ArriveAtTarget, nameof(ResidentAIPatch.ArriveAtTarget));
            AddPatchIfAvailable(patches, ResidentAIPatch.StartMoving, nameof(ResidentAIPatch.StartMoving));
            AddPatchIfAvailable(patches, ResidentAIPatch.InstanceSimulationStep, nameof(ResidentAIPatch.InstanceSimulationStep));
            AddPatchIfAvailable(patches, TouristAIPatch.Location, nameof(TouristAIPatch.Location));
            AddPatchIfAvailable(patches, TransferManagerPatch.AddOutgoingOffer, nameof(TransferManagerPatch.AddOutgoingOffer));
            AddPatchIfAvailable(patches, WorldInfoPanelPatch.UpdateBindings, nameof(WorldInfoPanelPatch.UpdateBindings));
            AddPatchIfAvailable(patches, UIGraphPatch.MinDataPoints, nameof(UIGraphPatch.MinDataPoints));
            AddPatchIfAvailable(patches, UIGraphPatch.VisibleEndTime, nameof(UIGraphPatch.VisibleEndTime));
            AddPatchIfAvailable(patches, UIGraphPatch.BuildLabels, nameof(UIGraphPatch.BuildLabels));
            AddPatchIfAvailable(patches, WeatherManagerPatch.SimulationStepImpl, nameof(WeatherManagerPatch.SimulationStepImpl));
            AddPatchIfAvailable(patches, CitizenManagerPatch.ReleaseCitizenPatch, nameof(CitizenManagerPatch.ReleaseCitizenPatch));
            AddPatchIfAvailable(patches, ParkPatch.DistrictParkSimulation, nameof(ParkPatch.DistrictParkSimulation));
            AddPatchIfAvailable(patches, OutsideConnectionAIPatch.DummyTrafficProbability, nameof(OutsideConnectionAIPatch.DummyTrafficProbability));
            AddPatchIfAvailable(patches, PublicTransportAIPatch.DepotCreateVehicle, nameof(PublicTransportAIPatch.DepotCreateVehicle));
            AddPatchIfAvailable(patches, PublicTransportAIPatch.DepotStartTransfer, nameof(PublicTransportAIPatch.DepotStartTransfer));
            AddPatchIfAvailable(patches, PublicTransportAIPatch.BusArriveAtTarget, nameof(PublicTransportAIPatch.BusArriveAtTarget));
            AddPatchIfAvailable(patches, PublicTransportAIPatch.BusArriveAtSource, nameof(PublicTransportAIPatch.BusArriveAtSource));
            AddPatchIfAvailable(patches, PublicTransportAIPatch.TramArriveAtTarget, nameof(PublicTransportAIPatch.TramArriveAtTarget));
            AddPatchIfAvailable(patches, PublicTransportAIPatch.TramArriveAtSource, nameof(PublicTransportAIPatch.TramArriveAtSource));
            AddPatchIfAvailable(patches, PublicTransportAIPatch.TrolleybusArriveAtTarget, nameof(PublicTransportAIPatch.TrolleybusArriveAtTarget));
            AddPatchIfAvailable(patches, PublicTransportAIPatch.TrolleybusArriveAtSource, nameof(PublicTransportAIPatch.TrolleybusArriveAtSource));
            AddPatchIfAvailable(patches, PublicTransportAIPatch.PassengerTrainArriveAtTarget, nameof(PublicTransportAIPatch.PassengerTrainArriveAtTarget));
            AddPatchIfAvailable(patches, PublicTransportAIPatch.PassengerTrainArriveAtSource, nameof(PublicTransportAIPatch.PassengerTrainArriveAtSource));
            AddPatchIfAvailable(patches, PublicTransportAIPatch.PassengerShipArriveAtTarget, nameof(PublicTransportAIPatch.PassengerShipArriveAtTarget));
            AddPatchIfAvailable(patches, PublicTransportAIPatch.PassengerShipArriveAtSource, nameof(PublicTransportAIPatch.PassengerShipArriveAtSource));
            AddPatchIfAvailable(patches, PublicTransportAIPatch.PassengerPlaneArriveAtTarget, nameof(PublicTransportAIPatch.PassengerPlaneArriveAtTarget));
            AddPatchIfAvailable(patches, PublicTransportAIPatch.PassengerPlaneArriveAtSource, nameof(PublicTransportAIPatch.PassengerPlaneArriveAtSource));
            AddPatchIfAvailable(patches, PublicTransportAIPatch.PassengerHelicopterArriveAtTarget, nameof(PublicTransportAIPatch.PassengerHelicopterArriveAtTarget));
            AddPatchIfAvailable(patches, PublicTransportAIPatch.PassengerHelicopterArriveAtSource, nameof(PublicTransportAIPatch.PassengerHelicopterArriveAtSource));
            AddPatchIfAvailable(patches, PublicTransportAIPatch.CableCarArriveAtTarget, nameof(PublicTransportAIPatch.CableCarArriveAtTarget));
            AddPatchIfAvailable(patches, PublicTransportAIPatch.CableCarArriveAtSource, nameof(PublicTransportAIPatch.CableCarArriveAtSource));
            AddPatchIfAvailable(patches, PublicTransportAIPatch.TaxiArriveAtTarget, nameof(PublicTransportAIPatch.TaxiArriveAtTarget));
            AddPatchIfAvailable(patches, PublicTransportAIPatch.TaxiArriveAtSource, nameof(PublicTransportAIPatch.TaxiArriveAtSource));

            if (compatibility.IsAnyModActive(WorkshopMods.CitizenLifecycleRebalance, WorkshopMods.LifecycleRebalanceRevisited))
            {
                Log.Info("The 'Real Time' mod will not change the citizens aging because a 'Lifecycle Rebalance' mod is active.");
            }
            else
            {
                AddPatchIfAvailable(patches, ResidentAIPatch.UpdateAge, nameof(ResidentAIPatch.UpdateAge));
                AddPatchIfAvailable(patches, ResidentAIPatch.CanMakeBabies, nameof(ResidentAIPatch.CanMakeBabies));
                AddPatchIfAvailable(patches, CitizenManagerPatch.CreateCitizenPatch1, nameof(CitizenManagerPatch.CreateCitizenPatch1));
                AddPatchIfAvailable(patches, CitizenManagerPatch.CreateCitizenPatch2, nameof(CitizenManagerPatch.CreateCitizenPatch2));
            }

            if (compatibility.IsAnyModActive(
                WorkshopMods.BuildingThemes,
                WorkshopMods.ForceLevelUp,
                WorkshopMods.PloppableRico,
                WorkshopMods.PloppableRicoHighDensityFix,
                WorkshopMods.PloppableRicoRevisited,
                WorkshopMods.PlopTheGrowables))
            {
                Log.Info("The 'Real Time' mod will not change the building construction and upgrading behavior because some building mod is active.");
            }
            else
            {
                AddPatchIfAvailable(patches, BuildingAIPatch.GetUpgradeInfo, nameof(BuildingAIPatch.GetUpgradeInfo));
                AddPatchIfAvailable(patches, BuildingAIPatch.CreateBuilding, nameof(BuildingAIPatch.CreateBuilding));
            }

            foreach (var patch in TimeControlCompatibility.GetCompatibilityPatches())
            {
                AddPatchIfAvailable(patches, patch, patch.GetType().Name);
            }

            return patches;
        }

        private static bool CheckRequiredMethodPatches(HashSet<IPatch> appliedPatches, ICollection<IPatch> registeredPatches)
        {
            IPatch[] requiredPatches =
            {
                BuildingAIPatch.HandleWorkers,
                BuildingAIPatch.CommercialSimulation,
                BuildingAIPatch.FishingMarketSimulation,
                ResidentAIPatch.Location,
                ResidentAIPatch.ArriveAtTarget,
                TouristAIPatch.Location,
                TransferManagerPatch.AddOutgoingOffer,
            };

            return requiredPatches
                .Where(registeredPatches.Contains)
                .All(appliedPatches.Contains);
        }

        private static bool SetupCustomAI(
            TimeInfo timeInfo,
            RealTimeConfig config,
            GameConnections<Citizen> gameConnections,
            RealTimeEventManager eventManager,
            Compatibility compatibility,
            out GameObject pandemicManagerObject,
            out SpareTimeBehavior spareTimeBehavior,
            out WorkBehavior workBehavior)
        {
            pandemicManagerObject = null;
            spareTimeBehavior = null;
            workBehavior = null;
            var residentAIConnection = ResidentAIPatch.GetResidentAIConnection();
            if (residentAIConnection == null)
            {
                return false;
            }

            float travelDistancePerCycle = compatibility.IsAnyModActive(WorkshopMods.RealisticWalkingSpeed)
                ? Constants.AverageTravelDistancePerCycle * 0.583f
                : Constants.AverageTravelDistancePerCycle;

            spareTimeBehavior = new SpareTimeBehavior(config, timeInfo);
            var travelBehavior = new TravelBehavior(gameConnections.BuildingManager, travelDistancePerCycle);
            workBehavior = new WorkBehavior(config, gameConnections.Random, gameConnections.BuildingManager, timeInfo, travelBehavior);

            ParkPatch.SpareTimeBehavior = spareTimeBehavior;
            OutsideConnectionAIPatch.SpareTimeBehavior = spareTimeBehavior;

            var realTimePrivateBuildingAI = new RealTimeBuildingAI(
                config,
                timeInfo,
                gameConnections.BuildingManager,
                new ToolManagerConnection(),
                workBehavior,
                travelBehavior);

            BuildingAIPatch.RealTimeAI = realTimePrivateBuildingAI;
            BuildingAIPatch.WeatherInfo = gameConnections.WeatherInfo;
            TransferManagerPatch.RealTimeAI = realTimePrivateBuildingAI;

            var realTimeResidentAI = new RealTimeResidentAI<ResidentAI, Citizen>(
                config,
                gameConnections,
                residentAIConnection,
                eventManager,
                realTimePrivateBuildingAI,
                workBehavior,
                spareTimeBehavior,
                travelBehavior);

            ResidentAIPatch.RealTimeAI = realTimeResidentAI;
            SimulationHandler.CitizenProcessor = new CitizenProcessor<ResidentAI, Citizen>(
                realTimeResidentAI, timeInfo, spareTimeBehavior, travelBehavior);

            var touristAIConnection = TouristAIPatch.GetTouristAIConnection();
            if (touristAIConnection == null)
            {
                Log.Warning("The 'Real Time' mod could not initialize tourist AI delegates. Tourist AI customization will be skipped.");
            }
            else
            {
                var realTimeTouristAI = new RealTimeTouristAI<TouristAI, Citizen>(
                    config,
                    gameConnections,
                    touristAIConnection,
                    eventManager,
                    spareTimeBehavior);

                TouristAIPatch.RealTimeAI = realTimeTouristAI;
            }

            if (GameObject.Find("PandemicManager") != null)
            {
                GameObject.Destroy(GameObject.Find("PandemicManager"));
            }
            pandemicManagerObject = new GameObject("PandemicManager");
            var pandemicManager = pandemicManagerObject.AddComponent<PandemicManager>();
            pandemicManager.Init(config, gameConnections);
            SimulationHandler.PandemicSimulationTick = pandemicManager.OnSimulationTickCompleted;
            pandemicManagerObject.AddComponent<InfectedCitizenTrailBehavior>();
            pandemicManagerObject.AddComponent<InfectedCitizenIconBehavior>();
            pandemicManagerObject.AddComponent<QuarantineBuildingIconBehavior>();
            pandemicManagerObject.AddComponent<PandemicXRayOverlayBehavior>();

            return true;
        }

        private static void AddPatchIfAvailable(List<IPatch> patches, IPatch patch, string patchName)
        {
            if (patches == null || patch == null)
            {
                return;
            }

            var targetMethod = TryResolveTargetMethod(patch);
            if (targetMethod == null)
            {
                Log.Warning($"Skipping patch '{patchName}' because the target method could not be resolved.");
                return;
            }

            if (!IsMethodAvailable(targetMethod))
            {
                Log.Warning($"Skipping patch '{patchName}' because '{targetMethod.DeclaringType?.FullName}.{targetMethod.Name}' is missing in this game version.");
                return;
            }

            patches.Add(patch);
        }

        private static MethodInfo TryResolveTargetMethod(IPatch patch)
        {
            try
            {
                var getMethod = patch.GetType().GetMethod("GetMethod", BindingFlags.Instance | BindingFlags.NonPublic);
                if (getMethod == null)
                {
                    return null;
                }

                return getMethod.Invoke(patch, null) as MethodInfo;
            }
            catch (Exception ex)
            {
                Log.Warning($"The 'Real Time' mod failed to resolve a patch target method for '{patch.GetType().Name}', error message: {ex}");
                return null;
            }
        }

        private static bool IsMethodAvailable(MethodInfo method)
        {
            if (method == null || method.DeclaringType == null)
            {
                return false;
            }

            try
            {
                var parameterTypes = method.GetParameters().Select(p => p.ParameterType).ToArray();
                return HarmonyAccessTools.Method(method.DeclaringType, method.Name, parameterTypes) != null;
            }
            catch (Exception ex)
            {
                Log.Warning($"The 'Real Time' mod failed to validate patch method '{method.Name}', error message: {ex}");
                return false;
            }
        }

        private static void CustomTimeBarCityEventClick(object sender, CustomTimeBarClickEventArgs e)
            => CameraHelper.NavigateToBuilding(e.CityEventBuildingId, zoomIn: true);

        private static void LoadStorageData(IEnumerable<IStorageData> storageData, StorageBase storage)
        {
            foreach (var item in storageData)
            {
                storage.Deserialize(item);
                Log.Debug(LogCategory.Generic, "The 'Real Time' mod loaded its data from container " + item.StorageDataId);
            }
        }

        private void CityEventsChanged(object sender, EventArgs e) => timeBar.UpdateEventsDisplay(eventManager.AllEvents);

        private void GameSaving(object sender, EventArgs e)
        {
            var storage = (StorageBase)sender;
            foreach (var item in storageData)
            {
                if (ExperimentControlGate.IsControlLocked && item is ConfigurationProvider<RealTimeConfig>)
                {
                    Log.Info("[TENUS Batch] Skipping temporary experiment configuration during game saving.");
                    continue;
                }

                storage.Serialize(item);
                Log.Debug(LogCategory.Generic, "The 'Real Time' mod stored its data in the current game for container " + item.StorageDataId);
            }
        }
    }
}
