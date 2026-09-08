
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using RealTime.CustomAI;
using RealTime.Experiments;
using RealTime.GameConnection;
using RealTime.GameConnection.Patches;
using SkyTools.Tools;
using UnityEngine;

namespace RealTime.Pandemic
{
    public class PandemicManager : MonoBehaviour
    {
        private bool ACTIVE_ONLY = true;
        private bool SET_SICK_FLAG = false;

        private System.Random initialPopulationRandom = new System.Random(0);
        private System.Random diseaseProgressionRandom = new System.Random(1);
        private System.Random transmissionRandom = new System.Random(2);
        private System.Random symptomRandom = new System.Random(3);
        private System.Random mortalityRandom = new System.Random(4);
        private System.Random interventionRandom = new System.Random(5);
        private bool active = false;
        private readonly EpidemicStepBarrier stepBarrier = new EpidemicStepBarrier();
        private Action pauseStepClock, releaseStepClock;
        private bool hasStartedAtLeastOnce;
        private bool worldOverlaysEnabled = true;
        private PandemicLifecycleState lifecycleState = PandemicLifecycleState.Dormant;
        private bool xRayEnabled;
        private PandemicXRayMetric xRayMetric = PandemicXRayMetric.Infected;
        private float[] transmissionHotspotGrid;
        private int transmissionHotspotCount;
        private PandemicXRayLocationMode xRayLocationMode = PandemicXRayLocationMode.LivePositions;
        private DateTime lastDateTime;
        private DateTime lastDateTimeCitizensUpdate;
        private DateTime lastStoreTime;
        private DateTime currentDateTime;
        private DateTime pandemicRunStartedAt;
        private DateTime pandemicRunFinishedAt;
        private PandemicRunContext runContext;
        private PandemicCompletionReason completionReason;
        private bool frozenForFinalization;
        private bool hadAnySickCitizens;

        private List<uint> initialPopulation = new List<uint>();
        private List<uint> initialPopulationHealthy = new List<uint>();
        private List<uint> initialPopulationRecovered = new List<uint>();
        private List<uint> initialPopulationDead = new List<uint>();
        private readonly Dictionary<uint, Citizen.AgeGroup> trackedAgeGroupByCitizen = new Dictionary<uint, Citizen.AgeGroup>();
        private readonly Dictionary<uint, PandemicPopulationCategory> trackedPopulationCategoryByCitizen = new Dictionary<uint, PandemicPopulationCategory>();
        private List<uint> initialPopulationSick = new List<uint>();
        // SEIR: citizens infected but not yet infectious (latent/exposed phase)
        private List<uint> initialPopulationExposed = new List<uint>();

        private HashSet<uint> usedCitizens = new HashSet<uint>();
        private Dictionary<uint, uint> citizenMatching = new Dictionary<uint, uint>();
        private readonly Dictionary<uint, uint> logicalCitizenByRealCitizen = new Dictionary<uint, uint>();
        private readonly object releasedCitizenIdsLock = new object();
        private readonly HashSet<uint> releasedRealCitizenIds = new HashSet<uint>();
        private uint nextLogicalCitizenId;

        private HashSet<ushort> infectedBuildingIds = new HashSet<ushort>();
        private HashSet<ushort> quarantineBuildingIds = new HashSet<ushort>();
        private HashSet<ushort> hotspotBuildingIds = new HashSet<ushort>();
        private Dictionary<ushort, int> buildingInfectedCounts = new Dictionary<ushort, int>();
        private HashSet<ushort> hubBuildingIds = new HashSet<ushort>();
        private Dictionary<ushort, int> buildingCurrentInfectedCounts = new Dictionary<ushort, int>();

        private PandemicObserver Observer;

        private Dictionary<uint, long> activeInfections = new Dictionary<uint, long>();
        private HashSet<uint> infectedCitizens = new HashSet<uint>();
        private HashSet<uint> infectedCitizensWithSymptoms = new HashSet<uint>();
        private HashSet<uint> symptomHospitalHandledCitizens = new HashSet<uint>();
        private readonly DiseaseStateEngine diseaseStateEngine = new DiseaseStateEngine();
        private readonly InitialSeedSampler initialSeedSampler = new InitialSeedSampler();
        private readonly MetricsEngine metricsEngine = new MetricsEngine();
        // Single simulation-thread scratch input; ContactEngine copies every field into its event.
        private readonly PhysicalContactRequest contactRequest = new PhysicalContactRequest();
        private readonly PopulationLifecycleEngine populationLifecycleEngine = new PopulationLifecycleEngine();
        private readonly ExperimentRunIntegrityMonitor runIntegrityMonitor = new ExperimentRunIntegrityMonitor();
        private readonly IsolationQuarantineEngine isolationQuarantineEngine = new IsolationQuarantineEngine();
        private DiseaseProgressionEngine diseaseProgressionEngine;
        private InfectiousnessProfilePolicy infectiousnessProfilePolicy;
        private DistributionSpec initialInfectionAgeDistribution;
        private HealthcareEngine healthcareEngine;

        private Dictionary<uint, PandemicInfectionOriginInfo> infectionOrigins = new Dictionary<uint, PandemicInfectionOriginInfo>();
        private readonly List<PandemicDeathRecord> deathRecords = new List<PandemicDeathRecord>();
        private readonly List<PandemicHealthcareUsageSample> healthcareUsageSamples = new List<PandemicHealthcareUsageSample>();
        private static readonly Citizen.AgeGroup[] LiveSnapshotAgeGroups =
        {
            Citizen.AgeGroup.Child,
            Citizen.AgeGroup.Teen,
            Citizen.AgeGroup.Young,
            Citizen.AgeGroup.Adult,
            Citizen.AgeGroup.Senior,
        };
        private static readonly PandemicLockdownFamily[] LockdownFamilies =
        {
            PandemicLockdownFamily.Education,
            PandemicLockdownFamily.PublicTransport,
            PandemicLockdownFamily.Commercial,
            PandemicLockdownFamily.LeisureTourismParks,
            PandemicLockdownFamily.Office,
            PandemicLockdownFamily.IndustryPlayerIndustry,
            PandemicLockdownFamily.GovernmentOtherPublic,
            PandemicLockdownFamily.EssentialServices,
            PandemicLockdownFamily.Healthcare,
        };

        private SimulationManager simulation;
        private bool startCompleted;
        private readonly LockdownEngine lockdownEngine = new LockdownEngine();
        private double currentLockdownMetricPercent;
        private readonly List<PandemicPolicyTimelineEntry> policyTimeline = new List<PandemicPolicyTimelineEntry>();
        private readonly Dictionary<ushort, PandemicPublicTransportLineState> publicTransportLineStates = new Dictionary<ushort, PandemicPublicTransportLineState>();
        private readonly Dictionary<ushort, PandemicPublicTransportDepotState> publicTransportDepotStates = new Dictionary<ushort, PandemicPublicTransportDepotState>();
        private readonly Dictionary<ushort, PandemicPublicTransportVehicleState> publicTransportVehicles = new Dictionary<ushort, PandemicPublicTransportVehicleState>();
        private PandemicPublicTransportShutdownState publicTransportShutdownState = PandemicPublicTransportShutdownState.Open;
        private bool publicTransportClosedLastTick;
        private PandemicPerformanceTier performanceTier = PandemicPerformanceTier.Light;
        private readonly Queue<double> recentUpdateDurationsMs = new Queue<double>();
        private double averageUpdateDurationMs;
        private readonly List<PandemicCitizenTickState> citizenTickStates = new List<PandemicCitizenTickState>();
        private readonly Stack<PandemicCitizenTickState> citizenTickStatePool = new Stack<PandemicCitizenTickState>();
        private readonly Stack<List<PandemicCitizenTickState>> citizenTickStateListPool = new Stack<List<PandemicCitizenTickState>>();
        private readonly Dictionary<int, List<PandemicCitizenTickState>> outdoorOccupantsByCell = new Dictionary<int, List<PandemicCitizenTickState>>();
        private readonly Dictionary<ushort, List<PandemicCitizenTickState>> buildingOccupants = new Dictionary<ushort, List<PandemicCitizenTickState>>();
        private readonly Dictionary<ushort, List<PandemicCitizenTickState>> vehicleOccupants = new Dictionary<ushort, List<PandemicCitizenTickState>>();
        private readonly Dictionary<uint, PandemicCitizenTickState> citizenTickStateById = new Dictionary<uint, PandemicCitizenTickState>();
        private readonly Dictionary<uint, ushort> lastObservedBuildingByCitizen = new Dictionary<uint, ushort>();
        private readonly Dictionary<uint, long> buildingEntryTimestampMsByCitizen = new Dictionary<uint, long>();
        private readonly Dictionary<uint, long> homeDepartureIntentTimestampMsByCitizen = new Dictionary<uint, long>();
        private readonly HashSet<uint> pendingTransmissionIds = new HashSet<uint>();
        private readonly TransmissionEngine transmissionEngine = new TransmissionEngine();
        private readonly ContactSamplingEngine contactSamplingEngine = new ContactSamplingEngine();
        private readonly ExperimentRecorder experimentRecorder = new ExperimentRecorder();
        private readonly List<TransmissionExposure<PandemicPendingTransmissionContext>> pendingTransmissionExposures = new List<TransmissionExposure<PandemicPendingTransmissionContext>>();
        private ExperimentRecorderSnapshot frozenScientificSnapshot;
        private int contactSamplingSeed;
        private readonly uint[] familyLookupBuffer = new uint[10];
        private PandemicLiveSnapshot liveSnapshotCache;
        private float nextAnalyticsSnapshotRefreshTime;
        private float nextSuperspreaderSnapshotRefreshTime;
        private int cachedChartObservationCount = -1;
        private int cachedPolicyMarkerCount = -1;
        private int cachedAnalyticsInfectionCount = -1;
        private int cachedSuperspreaderInfectionCount = -1;
        private int snapshotVersion;
        private int chartVersion;

        private readonly long CITIZENS_UPDATE_INTERVAL_MINUTES = 60 * 6;
        private readonly long STORE_INTERVAL_MINUTES = 5;
        private const float OutdoorMapHalfSize = 8640f;
        private const int OutdoorCellStride = 32768;
        private const int PerformanceWindowSize = 10;
        private const long ResidentialSharedAreaExposureWindowMs = 15 * 60 * 1000;
        private static readonly MethodInfo VehicleStartPathFindMethod = typeof(VehicleAI).GetMethod(
            "StartPathFind",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            new[] { typeof(ushort), typeof(Vehicle).MakeByRefType() },
            new ParameterModifier[0]);
        private static readonly MethodInfo DepotManualActivationMethod = typeof(DepotAI).GetMethod(
            "ManualActivation",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            new[] { typeof(ushort), typeof(Building).MakeByRefType() },
            new ParameterModifier[0]);
        private static readonly MethodInfo DepotManualDeactivationMethod = typeof(DepotAI).GetMethod(
            "ManualDeactivation",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            new[] { typeof(ushort), typeof(Building).MakeByRefType() },
            new ParameterModifier[0]);
        private static readonly Dictionary<Type, MethodInfo> VehicleRemoveLineMethods = new Dictionary<Type, MethodInfo>
        {
            { typeof(BusAI), GetVehicleRefMethod(typeof(BusAI), "RemoveLine") },
            { typeof(TramAI), GetVehicleRefMethod(typeof(TramAI), "RemoveLine") },
            { typeof(TrolleybusAI), GetVehicleRefMethod(typeof(TrolleybusAI), "RemoveLine") },
            { typeof(PassengerTrainAI), GetVehicleRefMethod(typeof(PassengerTrainAI), "RemoveLine") },
            { typeof(PassengerShipAI), GetVehicleRefMethod(typeof(PassengerShipAI), "RemoveLine") },
            { typeof(PassengerPlaneAI), GetVehicleRefMethod(typeof(PassengerPlaneAI), "RemoveLine") },
            { typeof(PassengerHelicopterAI), GetVehicleRefMethod(typeof(PassengerHelicopterAI), "RemoveLine") },
            { typeof(CableCarAI), GetVehicleRefMethod(typeof(CableCarAI), "RemoveLine") },
        };

        private MaskManager Masks = new MaskManager();
        internal bool ScheduledInterventionApplied { get; private set; }
        internal int AppliedInterventionCount { get; private set; }

        private void ApplyScheduledIntervention()
        {
            var schedule = runContext?.InterventionSchedule;
            if (schedule == null || AppliedInterventionCount >= schedule.PhaseCount) return;
            double activationDay = schedule.DayAt(AppliedInterventionCount);
            var before = schedule.SettingsAfter(AppliedInterventionCount);
            var after = schedule.SettingsAfter(AppliedInterventionCount + 1);
            DateTime activation = pandemicRunStartedAt.AddDays(activationDay);
            if (currentDateTime < activation) return;
            if (currentDateTime != activation) throw new InvalidOperationException("Scheduled intervention missed its exact epidemiological step.");
            if (before.Diff(RealTime.Experiments.ExperimentScenarioSnapshot.Capture(Config, QuarantineManager.Instance.InLockDown)).Count != 0)
                throw new InvalidOperationException("Configuration drift before scheduled intervention.");
            after.ApplyTo(Config);
            Masks.Init(Config);
            TestManager.Instance.UpdatePolicy(Config, diseaseStateEngine.TrackedPopulationCount, currentDateTime);
            ContactManager.Instance.ResetStableTraits(runContext.ComponentSeeds.MasterSeed);
            QuarantineManager.Instance.ConfigureCompliance(runContext.ComponentSeeds.MasterSeed, Config.IsolationCompliancePercent, Config.QuarantineCompliancePercent);
            QuarantineManager.Instance.InLockDown = after.InitialLockdownEnabled;
            ScheduledInterventionApplied = true;
            AppliedInterventionCount++;
            CapturePolicyTimelineState(force: true);
            experimentRecorder.RecordIntervention(new PandemicInterventionEvent
            {
                SimulationTime = currentDateTime, InterventionType = PandemicInterventionType.ScheduledPolicy,
                Action = "Activate", Reason = "ConfiguredSimulationDay", TriggerMetric = "SimulationDay",
                TriggerValue = activationDay, TriggerThreshold = activationDay,
                Context = "Exact before/after configurations are stored in the scenario manifest",
            });
        }

        private sealed class PandemicLocationAggregate
        {
            public string Label;
            public ushort BuildingId;
            public Vector3 Position;
            public int Count;
        }

        private sealed class PandemicPolicyTimelineEntry
        {
            public DateTime SimulationTime;
            public PandemicPolicyMarkerType Type;
            public bool Enabled;
        }

        private sealed class PandemicHealthcareUsageSample
        {
            public DateTime SimulationTime;
            public float HospitalUsagePercent;
            public float AmbulanceUsagePercent;
        }

        private sealed class PandemicPublicTransportLineState
        {
            public bool DayActive;
            public bool NightActive;
        }

        private sealed class PandemicPublicTransportDepotState
        {
            public bool WasActive;
            public bool IsClosed;
        }

        private sealed class PandemicPublicTransportVehicleState
        {
            public ushort SourceBuilding;
            public ushort OriginalLine;
            public bool RedirectPending;
            public bool ReturningToSource;
        }

        private sealed class PandemicCitizenTickState
        {
            public uint CitizenId;
            public uint RealCitizenId;
            public ushort HomeBuilding;
            public ushort CurrentBuilding;
            public ushort VehicleId;
            public ushort InstanceId;
            public Vector3 Position;
            public Citizen.Location Location;
            public Citizen.AgeGroup AgeGroup;
            public bool IsInfected;
            public bool IsInfectious;
            public bool IsSusceptible;
            public bool ShouldQuarantine;
            public bool IsKnownSick;
            public bool AtHome;
            public ushort TargetBuilding;
            public ushort TargetNode;
            public bool IsInResidentialSharedAreaWindow;
            public int OutdoorCellX;
            public int OutdoorCellZ;
        }

        private sealed class PandemicPendingTransmissionContext
        {
            public PandemicCitizenTickState Target;
            public PandemicInfectionOriginInfo Origin;
            public double InfectiousnessMultiplier;
        }

        public void Awake()
        {
            Instance = this;
        }

        internal void Init(Config.RealTimeConfig config, GameConnections<Citizen> connections)
        {
            Instance = this;

            if (config == null || connections == null)
            {
                Log.Warning("The 'Real Time' pandemic manager initialization was skipped because configuration or game connections are missing.");
                active = false;
                return;
            }

            Observer = new PandemicObserver();

            CitizenMgr = connections.CitizenManager;
            CitizenProxy = connections.CitizenConnection;
            BuildingMgr = connections.BuildingManager;

            Config = config;
            NormalizeRuntimeConfiguration(Config);
            CitizenManagerPatch.CitizenReleased = NotifyCitizenReleased;

            bool initialized = CitizenMgr != null && CitizenProxy != null && BuildingMgr != null;
            active = false;
            lifecycleState = PandemicLifecycleState.Dormant;
            pandemicRunStartedAt = default(DateTime);
            pandemicRunFinishedAt = default(DateTime);
            runContext = null;
            completionReason = PandemicCompletionReason.None;
            frozenForFinalization = false;
            if (!initialized)
            {
                Log.Warning("The 'Real Time' pandemic manager could not be activated because one or more game connections are missing.");
                return;
            }

            ContactManager.Instance.Init(config);
        }

        public long GetInfectionDate(uint citizenID)
        {
            citizenID = ResolveLogicalCitizenIdFromGame(citizenID);
            if (!activeInfections.ContainsKey(citizenID))
            {
                return -1;
            }
            return activeInfections[citizenID];
        }

        private void CloseSchools()
        {
            for (ushort i = 0; i < BuildingMgr.GetMaxBuildingsCount(); i++)
            {
                if (BuildingMgr.GetBuildingService(i) == ItemClass.Service.Education)
                {
                    BuildingMgr.ReleaseBuilding(i);
                }
            }
        }

        public void Start()
        {
            Instance = this;
        }

        public bool StartPandemic()
        {
            if (ExperimentControlGate.IsControlLocked)
            {
                return false;
            }

            if (lifecycleState == PandemicLifecycleState.Running)
            {
                return true;
            }

            runContext = PandemicRunContext.CreateManual(DateTime.Now);
            BootstrapSimulation(QuarantineManager.Instance.InLockDown);
            return lifecycleState == PandemicLifecycleState.Running;
        }

        /// <summary>Starts a controller-owned run with explicit policy, outputs, and random streams.</summary>
        internal bool StartPandemic(PandemicRunContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (!context.IsBatch)
            {
                throw new ArgumentException("The controller-owned start entry point requires a batch run context.", nameof(context));
            }

            if (lifecycleState == PandemicLifecycleState.Running)
            {
                return ReferenceEquals(runContext, context);
            }

            runContext = context;
            if (context.ComponentSeeds != null)
            {
                ResetRandomState(context.ComponentSeeds);
            }

            BootstrapSimulation(QuarantineManager.Instance.InLockDown);
            return lifecycleState == PandemicLifecycleState.Running;
        }

        public bool RestartSimulation()
        {
            if (ExperimentControlGate.IsControlLocked)
            {
                return false;
            }

            bool lockdownEnabled = QuarantineManager.Instance.InLockDown;
            runContext = PandemicRunContext.CreateManual(DateTime.Now);
            BootstrapSimulation(lockdownEnabled);
            return lifecycleState == PandemicLifecycleState.Running;
        }

        /// <summary>Restarts from the current city state under a controller-owned context.</summary>
        internal bool RestartSimulation(PandemicRunContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (!context.IsBatch)
            {
                throw new ArgumentException("The controller-owned restart entry point requires a batch run context.", nameof(context));
            }

            bool lockdownEnabled = QuarantineManager.Instance.InLockDown;
            runContext = context;
            if (context.ComponentSeeds != null)
            {
                ResetRandomState(context.ComponentSeeds);
            }

            BootstrapSimulation(lockdownEnabled);
            return lifecycleState == PandemicLifecycleState.Running;
        }

        public void StopPandemic()
        {
            if (ExperimentControlGate.IsControlLocked)
            {
                return;
            }

            StopPandemicCore();
        }

        private void StopPandemicCore()
        {
            if (lifecycleState == PandemicLifecycleState.Dormant)
            {
                // A bootstrap attempt can fail before the lifecycle leaves Dormant. Release its
                // controller-owned context as well so an aborted batch cannot remain latched.
                active = false;
                startCompleted = false;
                frozenForFinalization = false;
                runContext = null;
                return;
            }

            if (completionReason == PandemicCompletionReason.None)
            {
                completionReason = PandemicCompletionReason.ManualStop;
            }

            try
            {
                RestorePublicTransportService();

                // Heal all currently sick citizens (both infectious and latent/exposed).
                Citizen[] citizens = CitizenMgr.GetCitizensArray();
                foreach (uint citizenId in initialPopulationSick)
                {
                    uint realId = retrieveID(citizenId);
                    if (realId < citizens.Length && !CitizenProxy.IsEmpty(ref citizens[realId]))
                    {
                        CitizenProxy.SetSick(ref citizens[realId], false);
                    }
                }
                foreach (uint citizenId in initialPopulationExposed)
                {
                    uint realId = retrieveID(citizenId);
                    if (realId < citizens.Length && !CitizenProxy.IsEmpty(ref citizens[realId]))
                    {
                        CitizenProxy.SetSick(ref citizens[realId], false);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Log.Warning("The 'Real Time' pandemic manager encountered an error while stopping: " + ex);
            }

            active = false;
            startCompleted = false;
            lifecycleState = PandemicLifecycleState.Dormant;
            frozenForFinalization = false;
            runContext = null;
        }

        /// <summary>Freezes a batch run at its terminal snapshot without healing citizens or clearing metrics.</summary>
        internal bool FreezeForBatchFinalization(PandemicCompletionReason reason)
        {
            if (runContext == null || !runContext.IsBatch)
            {
                return false;
            }

            if (IsAwaitingBatchFinalization)
            {
                return true;
            }

            if (lifecycleState != PandemicLifecycleState.Running)
            {
                throw new InvalidOperationException("Only a running batch can be frozen for finalization.");
            }

            if (reason == PandemicCompletionReason.None || reason == PandemicCompletionReason.ManualStop)
            {
                throw new ArgumentOutOfRangeException(nameof(reason));
            }

            FreezeRun(reason);
            return true;
        }

        /// <summary>Freezes a failed batch without healing or converting it into a successful completion.</summary>
        internal bool FreezeInvalidBatchRun()
        {
            if (runContext == null || !runContext.IsBatch || !runIntegrityMonitor.HasFailed)
            {
                return false;
            }

            if (!frozenForFinalization)
            {
                FreezeRun(PandemicCompletionReason.Aborted);
            }

            return frozenForFinalization;
        }

        /// <summary>Performs destructive cleanup only after the batch controller has durably finalized exports.</summary>
        internal void CompleteBatchFinalization()
        {
            if (runContext != null && runContext.IsBatch)
            {
                if (!IsAwaitingBatchFinalization)
                {
                    throw new InvalidOperationException("The batch must be frozen and exported before final cleanup.");
                }

                StopPandemicCore();
            }
        }

        /// <summary>Stops and cleans up a controller-owned run that cannot be finalized normally.</summary>
        internal void AbortBatchRun()
        {
            if (runContext != null && runContext.IsBatch)
            {
                if (completionReason == PandemicCompletionReason.None)
                {
                    completionReason = PandemicCompletionReason.Aborted;
                }

                StopPandemicCore();
            }
        }

        private void BootstrapSimulation(bool lockdownEnabled)
        {
            if (!TryPrepareSimulationForBootstrap())
            {
                return;
            }

            try
            {
                RestorePublicTransportService();
                ResetRuntimeStateForBootstrap();
                ResetPandemicServices(lockdownEnabled);
                currentDateTime = simulation.m_ThreadingWrapper.simulationTime;
                pauseStepClock = () => SimulationPacingPatch.StepPending = true;
                releaseStepClock = () => SimulationPacingPatch.StepPending = false;
                pandemicRunStartedAt = currentDateTime;
                pandemicRunFinishedAt = default(DateTime);
                completionReason = PandemicCompletionReason.None;
                frozenForFinalization = false;
                string scientificOutputDirectory = runContext?.IsBatch == true && runContext.Output != null
                    ? Path.GetDirectoryName(runContext.Output.RichCsvPath)
                    : null;
                experimentRecorder.BeginRun(currentDateTime, scientificOutputDirectory, Config.ScientificContactExportMode, Config.MaximumRawContactExportGB);
                QuarantineManager.Instance.SetInterventionSink(experimentRecorder.RecordIntervention);
                ContactManager.Instance.SetRetainPhysicalHistory(runContext?.IsBatch != true);
                SeedPolicyTimeline();

                Citizen[] citizens = CitizenMgr.GetCitizensArray();
                nextLogicalCitizenId = (uint)citizens.Length;

                var infectionCandidates = new List<InitialSeedCandidate>();

                for (uint i = 0; i < citizens.Length; i++)
                {
                    if (i == 0u
                        || CitizenProxy.IsEmpty(ref citizens[i])
                        || CitizenProxy.IsDead(ref citizens[i]))
                    {
                        continue;
                    }

                    initialPopulation.Add(i);
                    usedCitizens.Add(i);
                    logicalCitizenByRealCitizen[i] = i;
                    diseaseStateEngine.RegisterCitizen(i);
                    trackedAgeGroupByCitizen[i] = CitizenProxy.GetAge(ref citizens[i]);
                    trackedPopulationCategoryByCitizen[i] = ClassifyPopulation(ref citizens[i]);

                    if (CitizenProxy.IsSick(ref citizens[i]))
                    {
                        CitizenProxy.SetSick(ref citizens[i], false);
                    }

                    initialPopulationHealthy.Add(i);
                    ushort homeBuilding = CitizenProxy.GetHomeBuilding(ref citizens[i]);
                    infectionCandidates.Add(new InitialSeedCandidate
                    {
                        CitizenId = i,
                        IsAtResidence = CitizenProxy.GetLocation(ref citizens[i]) == Citizen.Location.Home,
                        AgeStratum = (int)CitizenProxy.GetAge(ref citizens[i]),
                        DistrictStratum = GetDistrictId(homeBuilding),
                    });
                }

                float infectionRatio = Config.DiseaseStartInfectionRatio;
                uint intendedNumberOfSickCitizens = (uint)(infectionRatio * (initialPopulationSick.Count + initialPopulationExposed.Count + initialPopulationHealthy.Count) / 100f);

                IList<uint> selectedSeeds = initialSeedSampler.Select(
                    infectionCandidates,
                    checked((int)intendedNumberOfSickCitizens),
                    Config.InitialSeedSamplingStrategy,
                    initialPopulationRandom);
                if (selectedSeeds.Count != intendedNumberOfSickCitizens && runContext?.IsBatch == true)
                {
                    throw new InvalidOperationException(
                        "InitialSeedSamplingInsufficientPopulation: the selected strategy provided "
                        + selectedSeeds.Count.ToString(CultureInfo.InvariantCulture)
                        + " eligible citizens for "
                        + intendedNumberOfSickCitizens.ToString(CultureInfo.InvariantCulture)
                        + " requested initial seeds.");
                }

                foreach (uint citizenID in selectedSeeds)
                {
                    double requestedInfectionAgeDays = Config.InitialInfectionAgeMode
                        == RealTime.Config.PandemicInitialInfectionAgeMode.DistributedInitialInfectionAge
                        ? DistributionSampler.Sample(initialInfectionAgeDistribution, diseaseProgressionRandom)
                        : Config.InitialInfectionAgeFixedDays;
                    if (TryInfectCitizen(
                        citizenID,
                        ref citizens[retrieveID(citizenID)],
                        requestedInfectionAgeDays,
                        DiseaseExposureKind.InitialSeed))
                    {
                        PandemicInfectionOriginInfo seedOrigin = CreateSeedOrigin(ref citizens[retrieveID(citizenID)]);
                        RecordInfectionOrigin(citizenID, seedOrigin);
                    }
                }

                if (initialPopulation.Count == 0)
                {
                    Log.Warning("The 'Real Time' pandemic manager found no eligible citizens yet; initialization will be retried.");
                    startCompleted = false;
                    return;
                }

                if (intendedNumberOfSickCitizens > 0 && initialPopulationSick.Count == 0 && initialPopulationExposed.Count == 0)
                {
                    Log.Warning("The 'Real Time' pandemic manager could not seed initial infections yet; initialization will be retried.");
                    startCompleted = false;
                    return;
                }

                Debug.Log("Intended number of sick citizens: " + intendedNumberOfSickCitizens + ", actual number of sick citizens: " + (initialPopulationSick.Count + initialPopulationExposed.Count) + ", number of healthy citizens: " + initialPopulationHealthy.Count);

                foreach (IGrouping<PandemicPopulationCategory, KeyValuePair<uint, PandemicPopulationCategory>> group
                    in trackedPopulationCategoryByCitizen.GroupBy(item => item.Value).OrderBy(item => item.Key))
                {
                    experimentRecorder.RecordPopulation(new PandemicPopulationEvent
                    {
                        SimulationTime = currentDateTime,
                        Action = "InitialPopulation",
                        PopulationCategory = group.Key.ToString(),
                        Count = group.Count(),
                        Reason = "EligibleBaselinePopulation",
                    });
                }

                lastDateTime = currentDateTime;
                lastDateTimeCitizensUpdate = currentDateTime;
                lastStoreTime = currentDateTime;
                hadAnySickCitizens = initialPopulationSick.Count > 0 || initialPopulationExposed.Count > 0;

                Observer.AddSickCitizens(currentDateTime, initialPopulationSick.Count);
                Observer.AddExposedCitizens(currentDateTime, initialPopulationExposed.Count);
                Observer.AddHealthyCitizens(currentDateTime, initialPopulationHealthy.Count);
                Observer.AddRecoveredCitizens(currentDateTime, initialPopulationRecovered.Count);
                Observer.AddDeadCitizens(currentDateTime, initialPopulationDead.Count);
                Observer.FinishObservations(currentDateTime);
                CaptureHealthcareUsageSample(currentDateTime, citizens);
                RecordScientificState(currentDateTime);

                PersistRunSnapshot(force: true, "initial");

                TestManager.Instance.Init(Config, initialPopulation.Count, currentDateTime);
                QuarantineManager.Instance.InLockDown = lockdownEnabled;
                active = true;
                startCompleted = true;
                hasStartedAtLeastOnce = true;
                lifecycleState = PandemicLifecycleState.Running;
                BuildCitizenTickStateIndex(citizens);
                BuildPandemicBuildingSets();
                UpdatePublicTransportShutdownState(forceRefresh: true);
                stepBarrier.Synchronize(true, lastDateTime, Config.EpidemicStepMinutes, releaseStepClock);
                SimulationPacingPatch.StepMinutes = Config.EpidemicStepMinutes;
                SimulationPacingPatch.MaximumTimeSpeed = Math.Max(Config.DayTimeSpeed, Config.NightTimeSpeed);
            }
            catch (Exception ex)
            {
                Log.Error("The 'Real Time' pandemic manager failed to start: " + ex);
                if (runContext?.IsBatch == true)
                {
                    runIntegrityMonitor.Fail("SimulationBootstrapFailure", "The pandemic simulation failed during bootstrap.", currentDateTime, ex);
                }

                active = false;
                startCompleted = false;
                lifecycleState = PandemicLifecycleState.Dormant;
            }
        }

        private bool TryPrepareSimulationForBootstrap()
        {
            if (Config == null || CitizenMgr == null || CitizenProxy == null || BuildingMgr == null)
            {
                Log.Warning("The 'Real Time' pandemic manager cannot start because it was not initialized.");
                active = false;
                return false;
            }

            NormalizeRuntimeConfiguration(Config);
            active = false;
            if (Observer == null)
            {
                Observer = new PandemicObserver();
            }

            if (!EnsureSimulationReference())
            {
                Log.Warning("The 'Real Time' pandemic manager could not find the simulation manager.");
                return false;
            }

            return true;
        }

        private bool EnsureSimulationReference()
        {
            if (simulation != null)
            {
                return true;
            }

            GameObject simulationObject = GameObject.Find("SimulationManager");
            simulation = simulationObject?.GetComponent<SimulationManager>();
            return simulation != null;
        }

        private void ResetPandemicServices(bool lockdownEnabled)
        {
            Observer?.Reset();
            ContactManager.Instance.Init(Config);
            TestManager.Instance.Reset();
            Masks?.Init(Config);
            QuarantineManager.Instance.Reset();
            QuarantineManager.Instance.InLockDown = lockdownEnabled;
            diseaseProgressionEngine = new DiseaseProgressionEngine(
                PandemicScientificModelFactory.CreateDiseaseTimelinePolicy(Config),
                diseaseProgressionRandom);
            infectiousnessProfilePolicy = PandemicScientificModelFactory.CreateInfectiousnessPolicy(Config);
            infectiousnessProfilePolicy.Validate();
            initialInfectionAgeDistribution = PandemicScientificModelFactory.CreateInitialInfectionAgeDistribution(Config);
            initialInfectionAgeDistribution.Validate(nameof(initialInfectionAgeDistribution));
            healthcareEngine = new HealthcareEngine(new HealthcarePolicy
            {
                WarningThresholdPercent = Config.HealthcareWarningThresholdPercent,
                CriticalThresholdPercent = Config.HealthcareCriticalThresholdPercent,
                WarningMortalityMultiplier = Config.HealthcareWarningMortalityMultiplier,
                CriticalMortalityMultiplier = Config.HealthcareCriticalMortalityMultiplier,
            });
            runIntegrityMonitor.Reset();
        }

        /// <summary>Reseeds every <see cref="System.Random"/> stream owned by the TENUS pandemic.</summary>
        internal void ResetRandomState(PandemicComponentSeeds seeds)
        {
            if (seeds == null)
            {
                throw new ArgumentNullException(nameof(seeds));
            }

            initialPopulationRandom = new System.Random(seeds.InitialPopulationSeed);
            diseaseProgressionRandom = new System.Random(seeds.DiseaseProgressionSeed);
            transmissionRandom = new System.Random(seeds.TransmissionSeed);
            contactSamplingSeed = seeds.TransmissionSeed;
            symptomRandom = new System.Random(seeds.SymptomSeed);
            mortalityRandom = new System.Random(seeds.MortalitySeed);
            interventionRandom = new System.Random(seeds.InterventionSeed);
            Masks.ResetStableTraits(seeds.MasterSeed);
            TestManager.Instance.ResetRandom(seeds.TestingSeed);
            ContactManager.Instance.ResetStableTraits(seeds.MasterSeed);
            QuarantineManager.Instance.ConfigureCompliance(seeds.MasterSeed, Config?.IsolationCompliancePercent ?? 100f, Config?.QuarantineCompliancePercent ?? 100f);
        }

        private void PersistRunSnapshot(bool force, string phase)
        {
            PandemicOutputContext output = runContext?.Output;
            if (runContext?.IsBatch == true || output == null || !output.WriteManagerSnapshots)
            {
                return;
            }

            try
            {
                Observer?.WriteToDisc(force, output.ObserverCsvPath);
                ContactManager.Instance.WriteToDisk(output.ContactsCsvPath);
            }
            catch (Exception ex)
            {
                Log.Warning("The 'Real Time' pandemic manager failed to persist " + phase + " observer output: " + ex);
            }
        }

        private void FreezeRun(PandemicCompletionReason reason)
        {
            if (frozenForFinalization)
            {
                return;
            }

            active = false;
            lifecycleState = PandemicLifecycleState.Finished;
            pandemicRunFinishedAt = currentDateTime != default(DateTime)
                ? currentDateTime
                : simulation != null ? simulation.m_ThreadingWrapper.simulationTime : DateTime.Now;
            completionReason = reason;
            Observer?.Freeze();
            frozenScientificSnapshot = experimentRecorder.Freeze(
                pandemicRunFinishedAt,
                !runIntegrityMonitor.HasFailed);
            pandemicRunFinishedAt = frozenScientificSnapshot.RunEndTime;

            // Force one final coherent analytics snapshot while Update still owns the simulation pause.
            cachedAnalyticsInfectionCount = -1;
            cachedSuperspreaderInfectionCount = -1;
            nextAnalyticsSnapshotRefreshTime = 0f;
            nextSuperspreaderSnapshotRefreshTime = 0f;
            try
            {
                GetLiveSnapshot();
            }
            catch (Exception ex)
            {
                Log.Warning("The 'Real Time' pandemic manager could not refresh every terminal analytics view: " + ex);
            }
            finally
            {
                frozenForFinalization = true;
            }
        }

        internal PandemicPublicTransportShutdownState GetPublicTransportShutdownState() => publicTransportShutdownState;

        internal bool ShouldBlockPublicTransportDepotSpawn(ushort buildingId, VehicleInfo vehicleInfo)
        {
            if (publicTransportShutdownState == PandemicPublicTransportShutdownState.Open || vehicleInfo == null)
            {
                return false;
            }

            return IsPublicTransportDepotBuilding(buildingId)
                && vehicleInfo.m_class != null
                && vehicleInfo.m_class.m_service == ItemClass.Service.PublicTransport;
        }

        internal bool ShouldBlockPublicTransportDepotTransfer(ushort buildingId)
        {
            return publicTransportShutdownState != PandemicPublicTransportShutdownState.Open
                && IsPublicTransportDepotBuilding(buildingId);
        }

        internal void NotifyPublicTransportVehicleArrivedAtTarget(ushort vehicleId, ref Vehicle vehicle)
        {
            if (IsAwaitingBatchFinalization
                || publicTransportShutdownState != PandemicPublicTransportShutdownState.Draining
                || !IsPublicTransportVehicle(vehicleId, ref vehicle))
            {
                return;
            }

            TrackPublicTransportVehicle(vehicleId, ref vehicle);
            if (!publicTransportVehicles.TryGetValue(vehicleId, out PandemicPublicTransportVehicleState trackedVehicle)
                || trackedVehicle.ReturningToSource
                || !trackedVehicle.RedirectPending)
            {
                return;
            }

            if (trackedVehicle.SourceBuilding == 0)
            {
                publicTransportVehicles.Remove(vehicleId);
                VehicleManager.instance?.ReleaseVehicle(vehicleId);
                return;
            }

            if (!TrySendVehicleBackToSource(vehicleId, ref vehicle, trackedVehicle))
            {
                publicTransportVehicles.Remove(vehicleId);
                VehicleManager.instance?.ReleaseVehicle(vehicleId);
            }
        }

        internal void NotifyPublicTransportVehicleArrivedAtSource(ushort vehicleId, ref Vehicle vehicle)
        {
            if (IsAwaitingBatchFinalization
                || publicTransportShutdownState == PandemicPublicTransportShutdownState.Open
                || !IsPublicTransportVehicle(vehicleId, ref vehicle))
            {
                return;
            }

            if (publicTransportVehicles.TryGetValue(vehicleId, out PandemicPublicTransportVehicleState trackedVehicle)
                && trackedVehicle.ReturningToSource)
            {
                MarkPublicTransportVehicleAtSource(vehicleId);
            }
        }

        private bool CanUpdatePublicTransportShutdown()
        {
            return Config != null
                && BuildingMgr != null
                && EnsureSimulationReference();
        }

        private bool IsPublicTransportClosedEffective() => !IsLockdownFamilyOpen(PandemicLockdownFamily.PublicTransport);

        private void UpdatePublicTransportShutdownState(bool forceRefresh = false)
        {
            if (!CanUpdatePublicTransportShutdown())
            {
                return;
            }

            bool shouldClose = IsPublicTransportClosedEffective();
            if (forceRefresh || shouldClose != publicTransportClosedLastTick)
            {
                if (shouldClose)
                {
                    BeginPublicTransportShutdown();
                }
                else
                {
                    RestorePublicTransportService();
                }

                publicTransportClosedLastTick = shouldClose;
            }

            if (!shouldClose)
            {
                return;
            }

            CaptureTrackedPublicTransportVehicles();
            RefreshTrackedPublicTransportVehicles();
        }

        private void BeginPublicTransportShutdown()
        {
            if (publicTransportShutdownState != PandemicPublicTransportShutdownState.Open)
            {
                return;
            }

            CapturePublicTransportLines();
            CapturePublicTransportDepots();
            CaptureTrackedPublicTransportVehicles();

            publicTransportShutdownState = publicTransportVehicles.Count > 0
                ? PandemicPublicTransportShutdownState.Draining
                : PandemicPublicTransportShutdownState.Closed;
            RecordRuntimeIntervention(
                PandemicInterventionType.PublicTransportShutdown,
                "Start",
                "LockdownFamilyClosed",
                publicTransportShutdownState.ToString());

            if (publicTransportShutdownState == PandemicPublicTransportShutdownState.Closed)
            {
                DeactivateTrackedPublicTransportDepots();
            }
        }

        private void RestorePublicTransportService()
        {
            if (publicTransportShutdownState == PandemicPublicTransportShutdownState.Open
                && publicTransportLineStates.Count == 0
                && publicTransportDepotStates.Count == 0
                && publicTransportVehicles.Count == 0)
            {
                return;
            }

            ActivateTrackedPublicTransportDepots();

            TransportManager transportManager = TransportManager.instance;
            TransportLine[] lines = transportManager?.m_lines.m_buffer;
            if (lines != null)
            {
                foreach (KeyValuePair<ushort, PandemicPublicTransportLineState> entry in publicTransportLineStates)
                {
                    ushort lineId = entry.Key;
                    if (lineId >= lines.Length)
                    {
                        continue;
                    }

                    ref TransportLine line = ref lines[lineId];
                    if (!IsCreatedTransportLine(ref line))
                    {
                        continue;
                    }

                    TransportInfo lineInfo = line.Info;
                    if (lineInfo?.m_class == null || lineInfo.m_class.m_service != ItemClass.Service.PublicTransport)
                    {
                        continue;
                    }

                    line.SetActive(entry.Value.DayActive, entry.Value.NightActive);
                }
            }

            publicTransportVehicles.Clear();
            publicTransportLineStates.Clear();
            publicTransportDepotStates.Clear();
            publicTransportShutdownState = PandemicPublicTransportShutdownState.Open;
            RecordRuntimeIntervention(
                PandemicInterventionType.PublicTransportShutdown,
                "End",
                "LockdownFamilyOpened",
                PandemicPublicTransportShutdownState.Open.ToString());
        }

        private void CapturePublicTransportLines()
        {
            publicTransportLineStates.Clear();

            TransportManager transportManager = TransportManager.instance;
            TransportLine[] lines = transportManager?.m_lines.m_buffer;
            if (lines == null)
            {
                return;
            }

            for (ushort lineId = 0; lineId < lines.Length; lineId++)
            {
                ref TransportLine line = ref lines[lineId];
                if (!IsCreatedTransportLine(ref line))
                {
                    continue;
                }

                TransportInfo lineInfo = line.Info;
                if (lineInfo?.m_class == null || lineInfo.m_class.m_service != ItemClass.Service.PublicTransport)
                {
                    continue;
                }

                bool dayActive;
                bool nightActive;
                line.GetActive(out dayActive, out nightActive);
                publicTransportLineStates[lineId] = new PandemicPublicTransportLineState
                {
                    DayActive = dayActive,
                    NightActive = nightActive,
                };

                line.SetActive(false, false);
            }
        }

        private void CapturePublicTransportDepots()
        {
            publicTransportDepotStates.Clear();

            BuildingManager buildingManager = BuildingManager.instance;
            if (buildingManager == null)
            {
                return;
            }

            Building[] buildings = buildingManager.m_buildings.m_buffer;
            if (buildings == null)
            {
                return;
            }

            for (ushort buildingId = 0; buildingId < buildings.Length; buildingId++)
            {
                ref Building building = ref buildings[buildingId];
                if (!IsPublicTransportDepotBuilding(buildingId))
                {
                    continue;
                }

                publicTransportDepotStates[buildingId] = new PandemicPublicTransportDepotState
                {
                    WasActive = (building.m_flags & Building.Flags.Active) != 0,
                    IsClosed = false,
                };
            }
        }

        private void CaptureTrackedPublicTransportVehicles()
        {
            VehicleManager vehicleManager = VehicleManager.instance;
            Vehicle[] vehicles = vehicleManager?.m_vehicles.m_buffer;
            if (vehicles == null)
            {
                return;
            }

            for (ushort vehicleId = 1; vehicleId < vehicles.Length; vehicleId++)
            {
                ref Vehicle vehicle = ref vehicles[vehicleId];
                TrackPublicTransportVehicle(vehicleId, ref vehicle);
            }
        }

        private void TrackPublicTransportVehicle(ushort vehicleId, ref Vehicle vehicle)
        {
            if (vehicleId == 0
                || publicTransportVehicles.ContainsKey(vehicleId)
                || !IsVehicleCreated(ref vehicle)
                || (vehicle.m_flags & Vehicle.Flags.Spawned) == 0
                || !IsPublicTransportVehicle(vehicleId, ref vehicle))
            {
                return;
            }

            ushort sourceBuilding = vehicle.m_sourceBuilding;
            publicTransportVehicles[vehicleId] = new PandemicPublicTransportVehicleState
            {
                SourceBuilding = sourceBuilding,
                OriginalLine = vehicle.m_transportLine,
                RedirectPending = sourceBuilding != 0 || vehicle.m_transportLine != 0,
                ReturningToSource = IsVehicleReturningToSource(ref vehicle, sourceBuilding),
            };
        }

        private void RefreshTrackedPublicTransportVehicles()
        {
            if (publicTransportVehicles.Count == 0)
            {
                if (publicTransportShutdownState == PandemicPublicTransportShutdownState.Draining)
                {
                    publicTransportShutdownState = PandemicPublicTransportShutdownState.Closed;
                    DeactivateTrackedPublicTransportDepots();
                }

                return;
            }

            VehicleManager vehicleManager = VehicleManager.instance;
            Vehicle[] vehicles = vehicleManager?.m_vehicles.m_buffer;
            if (vehicles == null)
            {
                return;
            }

            foreach (ushort vehicleId in publicTransportVehicles.Keys.ToList())
            {
                if (vehicleId >= vehicles.Length)
                {
                    publicTransportVehicles.Remove(vehicleId);
                    continue;
                }

                ref Vehicle vehicle = ref vehicles[vehicleId];
                if (!IsVehicleCreated(ref vehicle)
                    || (vehicle.m_flags & Vehicle.Flags.Spawned) == 0
                    || !IsPublicTransportVehicle(vehicleId, ref vehicle))
                {
                    publicTransportVehicles.Remove(vehicleId);
                    continue;
                }

                PandemicPublicTransportVehicleState trackedVehicle = publicTransportVehicles[vehicleId];
                trackedVehicle.ReturningToSource = IsVehicleReturningToSource(ref vehicle, trackedVehicle.SourceBuilding);
                publicTransportVehicles[vehicleId] = trackedVehicle;
            }

            if (publicTransportVehicles.Count == 0 && publicTransportShutdownState == PandemicPublicTransportShutdownState.Draining)
            {
                publicTransportShutdownState = PandemicPublicTransportShutdownState.Closed;
                DeactivateTrackedPublicTransportDepots();
            }
        }

        private bool TrySendVehicleBackToSource(ushort vehicleId, ref Vehicle vehicle, PandemicPublicTransportVehicleState trackedVehicle)
        {
            VehicleAI vehicleAI = vehicle.Info?.m_vehicleAI;
            if (vehicleAI == null || trackedVehicle.SourceBuilding == 0)
            {
                return false;
            }

            TryDetachVehicleFromTransportLine(vehicleId, ref vehicle, vehicleAI);

            vehicleAI.SetTransportLine(vehicleId, ref vehicle, 0);
            vehicleAI.SetSource(vehicleId, ref vehicle, trackedVehicle.SourceBuilding);
            vehicleAI.SetTarget(vehicleId, ref vehicle, trackedVehicle.SourceBuilding);
            vehicle.m_transportLine = 0;
            vehicle.m_flags |= Vehicle.Flags.GoingBack | Vehicle.Flags.TransferToSource;
            vehicle.m_flags &= ~Vehicle.Flags.TransferToTarget;

            bool startedPath = InvokeStartPathFind(vehicleAI, vehicleId, ref vehicle);
            if (startedPath)
            {
                trackedVehicle.RedirectPending = false;
                trackedVehicle.ReturningToSource = true;
                publicTransportVehicles[vehicleId] = trackedVehicle;
            }

            return startedPath;
        }

        private static void TryDetachVehicleFromTransportLine(ushort vehicleId, ref Vehicle vehicle, VehicleAI vehicleAI)
        {
            if (vehicle.m_transportLine == 0)
            {
                return;
            }

            foreach (KeyValuePair<Type, MethodInfo> entry in VehicleRemoveLineMethods)
            {
                if (entry.Key.IsInstanceOfType(vehicleAI))
                {
                    InvokeVehicleRefMethod(entry.Value, vehicleAI, vehicleId, ref vehicle);
                    break;
                }
            }
        }

        private void MarkPublicTransportVehicleAtSource(ushort vehicleId)
        {
            publicTransportVehicles.Remove(vehicleId);
            if (publicTransportVehicles.Count == 0 && publicTransportShutdownState == PandemicPublicTransportShutdownState.Draining)
            {
                publicTransportShutdownState = PandemicPublicTransportShutdownState.Closed;
                DeactivateTrackedPublicTransportDepots();
            }
        }

        private void DeactivateTrackedPublicTransportDepots()
        {
            foreach (KeyValuePair<ushort, PandemicPublicTransportDepotState> entry in publicTransportDepotStates.ToList())
            {
                PandemicPublicTransportDepotState depotState = entry.Value;
                if (depotState.IsClosed || !depotState.WasActive)
                {
                    continue;
                }

                if (TrySetDepotManualState(entry.Key, false))
                {
                    depotState.IsClosed = true;
                    publicTransportDepotStates[entry.Key] = depotState;
                }
            }
        }

        private void ActivateTrackedPublicTransportDepots()
        {
            foreach (KeyValuePair<ushort, PandemicPublicTransportDepotState> entry in publicTransportDepotStates)
            {
                if (!entry.Value.WasActive)
                {
                    continue;
                }

                TrySetDepotManualState(entry.Key, true);
            }
        }

        private bool TrySetDepotManualState(ushort buildingId, bool activeState)
        {
            BuildingManager buildingManager = BuildingManager.instance;
            if (buildingId == 0 || buildingManager == null || buildingId >= buildingManager.m_buildings.m_buffer.Length)
            {
                return false;
            }

            ref Building building = ref buildingManager.m_buildings.m_buffer[buildingId];
            DepotAI depotAI = building.Info?.m_buildingAI as DepotAI;
            if (depotAI == null)
            {
                return false;
            }

            if (activeState)
            {
                InvokeBuildingRefMethod(DepotManualActivationMethod, depotAI, buildingId, ref building);
            }
            else
            {
                InvokeBuildingRefMethod(DepotManualDeactivationMethod, depotAI, buildingId, ref building);
                BuildingMgr?.DeactivateVisually(buildingId);
            }

            BuildingMgr?.UpdateBuildingColors(buildingId);
            return true;
        }

        private bool IsPublicTransportDepotBuilding(ushort buildingId)
        {
            BuildingManager buildingManager = BuildingManager.instance;
            if (buildingId == 0 || buildingManager == null || buildingId >= buildingManager.m_buildings.m_buffer.Length)
            {
                return false;
            }

            ref Building building = ref buildingManager.m_buildings.m_buffer[buildingId];
            return (building.m_flags & Building.Flags.Created) != 0
                && (building.m_flags & Building.Flags.Deleted) == 0
                && building.Info?.m_class != null
                && building.Info.m_class.m_service == ItemClass.Service.PublicTransport
                && building.Info.m_buildingAI is DepotAI;
        }

        private static bool IsCreatedTransportLine(ref TransportLine line)
        {
            return (line.m_flags & TransportLine.Flags.Created) != 0
                && (line.m_flags & TransportLine.Flags.Deleted) == 0;
        }

        private static bool IsVehicleCreated(ref Vehicle vehicle)
        {
            return (vehicle.m_flags & Vehicle.Flags.Created) != 0
                && (vehicle.m_flags & Vehicle.Flags.Deleted) == 0;
        }

        private bool IsPublicTransportVehicle(ushort vehicleId, ref Vehicle vehicle)
        {
            return vehicleId != 0
                && vehicle.Info?.m_class != null
                && vehicle.Info.m_class.m_service == ItemClass.Service.PublicTransport;
        }

        private static bool IsVehicleReturningToSource(ref Vehicle vehicle, ushort sourceBuilding)
        {
            return sourceBuilding != 0
                && ((vehicle.m_flags & Vehicle.Flags.GoingBack) != 0
                    || (vehicle.m_flags & Vehicle.Flags.TransferToSource) != 0
                    || vehicle.m_targetBuilding == sourceBuilding);
        }

        private static MethodInfo GetVehicleRefMethod(Type type, string name)
        {
            return type.GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(ushort), typeof(Vehicle).MakeByRefType() },
                new ParameterModifier[0]);
        }

        private static bool InvokeStartPathFind(VehicleAI vehicleAI, ushort vehicleId, ref Vehicle vehicle)
        {
            if (vehicleAI == null || VehicleStartPathFindMethod == null)
            {
                return false;
            }

            object[] args = { vehicleId, vehicle };
            object result = VehicleStartPathFindMethod.Invoke(vehicleAI, args);
            vehicle = (Vehicle)args[1];
            return result is bool started && started;
        }

        private static void InvokeVehicleRefMethod(MethodInfo method, VehicleAI vehicleAI, ushort vehicleId, ref Vehicle vehicle)
        {
            if (method == null || vehicleAI == null)
            {
                return;
            }

            object[] args = { vehicleId, vehicle };
            method.Invoke(vehicleAI, args);
            vehicle = (Vehicle)args[1];
        }

        private static void InvokeBuildingRefMethod(MethodInfo method, DepotAI depotAI, ushort buildingId, ref Building building)
        {
            if (method == null || depotAI == null)
            {
                return;
            }

            object[] args = { buildingId, building };
            method.Invoke(depotAI, args);
            building = (Building)args[1];
        }

        internal void OnSimulationTickCompleted()
        {
            if (ReferenceEquals(simulation, null) || pauseStepClock == null) return;
            stepBarrier.CheckTick(simulation.m_ThreadingWrapper.simulationTime, pauseStepClock);
        }

        internal void StopStepSynchronization()
        {
            SimulationPacingPatch.StepMinutes = 0;
            stepBarrier.Synchronize(false, default(DateTime), 5, releaseStepClock);
            SimulationPacingPatch.StepPending = false;
        }

        public void Update()
        {
            using (PandemicProfiler.Measure("Update"))
            {
                if (!IsReadyForUpdate() || frozenForFinalization)
                {
                    StopStepSynchronization();
                    return;
                }
                // Never consult the interpolated rendering clock. Only process a
                // completed simulation tick that has requested and owns the pause.
                if (!stepBarrier.TryAcquire(out DateTime observedTime)) return;
                try { UpdateProfiled(observedTime); }
                finally
                {
                    bool running = IsReadyForUpdate() && !frozenForFinalization;
                    SimulationPacingPatch.StepMinutes = running ? Config.EpidemicStepMinutes : 0;
                    SimulationPacingPatch.MaximumTimeSpeed = Math.Max(Config.DayTimeSpeed, Config.NightTimeSpeed);
                    stepBarrier.Synchronize(running, lastDateTime, Config.EpidemicStepMinutes, releaseStepClock);
                }
            }
        }
        private void UpdateProfiled(DateTime observedTime) {
            if (frozenForFinalization)
            {
                return;
            }

            long tickStart = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                UpdatePublicTransportShutdownState();
            }
            catch (Exception ex)
            {
                if (HandleSimulationFailure("InterventionEngineException", "Public transport intervention update failed.", ex))
                {
                    return;
                }
            }

            if (!IsReadyForUpdate())
            {
                return;
            }

            try
            {
                DateTime tempDateTime = observedTime;
                EpidemicStepDecision stepDecision = EpidemicStepScheduler.Evaluate(
                    lastDateTime,
                    tempDateTime,
                    Config.EpidemicStepMinutes,
                    runContext?.IsBatch == true);
                if (stepDecision.Kind == EpidemicStepDecisionKind.NotDue)
                {
                    return;
                }

                if (stepDecision.Kind == EpidemicStepDecisionKind.IntegrityViolation)
                {
                    if (runContext?.IsBatch == true)
                    {
                        runIntegrityMonitor.Fail(
                            stepDecision.ErrorCode,
                            "A required epidemiological simulation step could not be reconstructed safely.",
                            tempDateTime,
                            null);
                        runIntegrityMonitor.Failure.Detail = string.Format(CultureInfo.InvariantCulture,
                            "LastProcessed={0:o}; Observed={1:o}; ElapsedSeconds={2:R}; StepMinutes={3}",
                            lastDateTime, tempDateTime, (tempDateTime - lastDateTime).TotalSeconds, Config.EpidemicStepMinutes);
                        active = false;
                    }
                    else
                    {
                        Log.Warning("The epidemiological clock lost monotonic step integrity; interactive simulation timing was resynchronized.");
                        lastDateTime = tempDateTime;
                    }

                    return;
                }

                currentDateTime = stepDecision.StepTime;
                ApplyScheduledIntervention();

                long milliseconds = checked((long)Config.EpidemicStepMinutes * 60L * 1000L);

                try
                {
                    Masks?.SetStepLengthInHours(milliseconds / (3600 * 1000.0));
                    Masks?.SetBehavior(Config.MaskBehavior);
                }
                catch (Exception ex)
                {
                    if (HandleSimulationFailure("TransmissionEngineException", "Transmission probability update failed.", ex))
                    {
                        return;
                    }
                }

                try
                {
                    PromoteExposedToInfectious();
                    ProcessMortality();
                }
                catch (Exception ex)
                {
                    if (HandleSimulationFailure("DiseaseProgressionException", "Disease progression failed.", ex))
                    {
                        return;
                    }
                }

                Citizen[] citizens = CitizenMgr.GetCitizensArray();
                if (!ProcessReleasedCitizens())
                {
                    return;
                }

                SyncSymptomStates(citizens);

                using (PandemicProfiler.Measure("PopulationReconciliation"))
                {
                if (ACTIVE_ONLY)
                {
                    if ((currentDateTime.Ticks - lastDateTimeCitizensUpdate.Ticks) / TimeSpan.TicksPerMinute >= CITIZENS_UPDATE_INTERVAL_MINUTES)
                    {
                        lastDateTimeCitizensUpdate = currentDateTime;
                        citizens = CitizenMgr.GetCitizensArray();

                        for (int i = 0; i < initialPopulation.Count; i++)
                        {
                            uint logicalCitizenId = initialPopulation[i];
                            uint realCitizenId = retrieveID(logicalCitizenId);
                            bool realCitizenMissing = realCitizenId >= citizens.Length
                                || CitizenProxy.IsEmpty(ref citizens[realCitizenId]);
                            bool deceasedSlotWasReused = !realCitizenMissing
                                && diseaseStateEngine.GetState(logicalCitizenId, currentDateTime) == DiseaseState.Dead
                                && !CitizenProxy.IsDead(ref citizens[realCitizenId]);
                            if (realCitizenMissing || deceasedSlotWasReused)
                            {
                                if (!HandleTrackedCitizenDeparture(
                                    logicalCitizenId,
                                    realCitizenId,
                                    deceasedSlotWasReused ? "CitizenSlotReused" : "CitizenNoLongerExists"))
                                {
                                    return;
                                }

                                i--;
                            }
                        }

                        // Track every newly eligible resident as a new logical person. Population
                        // growth must not depend on a preceding removal, and a reused game-buffer
                        // slot must never inherit the old logical citizen's disease course.
                        for (uint i = 0; i < citizens.Length; i++)
                        {
                            if (i != 0u
                                && !CitizenProxy.IsEmpty(ref citizens[i])
                                && !CitizenProxy.IsDead(ref citizens[i])
                                && !usedCitizens.Contains(i))
                            {
                                uint newLogicalCitizen = AllocateLogicalCitizenId();
                                usedCitizens.Add(i);
                                citizenMatching[newLogicalCitizen] = i;
                                logicalCitizenByRealCitizen[i] = newLogicalCitizen;
                                initialPopulation.Add(newLogicalCitizen);
                                initialPopulationHealthy.Add(newLogicalCitizen);
                                diseaseStateEngine.RegisterCitizen(newLogicalCitizen);
                                trackedAgeGroupByCitizen[newLogicalCitizen] = CitizenProxy.GetAge(ref citizens[i]);
                                PandemicPopulationCategory populationCategory = ClassifyPopulation(ref citizens[i]);
                                trackedPopulationCategoryByCitizen[newLogicalCitizen] = populationCategory;
                                experimentRecorder.RecordPopulation(new PandemicPopulationEvent
                                {
                                    SimulationTime = currentDateTime,
                                    CitizenId = newLogicalCitizen,
                                    Action = "Added",
                                    PopulationCategory = populationCategory.ToString(),
                                    Count = 1,
                                    Reason = "NewEligibleCitizen",
                                });
                            }
                        }
                    }
                }

                }

                try
                {
                    ProcessCitizenTesting(citizens);
                }
                catch (Exception ex)
                {
                    if (HandleSimulationFailure("TestingEngineException", "Testing state-machine processing failed.", ex))
                    {
                        return;
                    }
                }

                try
                {
                    ProcessRecoveries(citizens);
                }
                catch (Exception ex)
                {
                    if (HandleSimulationFailure("DiseaseProgressionException", "Disease recovery processing failed.", ex))
                    {
                        return;
                    }
                }

                try
                {
                    UpdateLockdownPolicyStates(currentDateTime);
                    UpdatePublicTransportShutdownState();
                    CapturePolicyTimelineState();
                }
                catch (Exception ex)
                {
                    if (HandleSimulationFailure("InterventionEngineException", "Lockdown policy update failed.", ex))
                    {
                        return;
                    }
                }

                try
                {
                    ProcessContactTracingCandidates();
                }
                catch (Exception ex)
                {
                    if (HandleSimulationFailure("ContactTracingEngineException", "Contact tracing processing failed.", ex))
                    {
                        return;
                    }
                }

                try
                {
                    spread();
                    RefreshPostTransmissionTickStates(citizens);
                }
                catch (Exception ex)
                {
                    if (HandleSimulationFailure("TransmissionEngineException", "Testing or transmission simulation failed.", ex))
                    {
                        return;
                    }
                }

                try
                {
                    BuildPandemicBuildingSets();
                }
                catch (Exception ex)
                {
                    Log.Warning("The 'Real Time' pandemic manager failed to build visualization data: " + ex);
                }

                try
                {
                    CaptureHealthcareUsageSample(currentDateTime, citizens);
                }
                catch (Exception ex)
                {
                    if (HandleSimulationFailure("HealthcareEngineException", "Healthcare time-series capture failed.", ex))
                    {
                        return;
                    }
                }

                try
                {
                    int locHome = 0, locWork = 0, locVisit = 0, locTransit = 0, locOnFoot = 0;
                    foreach (var state in citizenTickStates)
                    {
                        switch (state.Location)
                        {
                            case Citizen.Location.Home:    locHome++;    break;
                            case Citizen.Location.Work:    locWork++;    break;
                            case Citizen.Location.Visit:   locVisit++;   break;
                            case Citizen.Location.Moving:
                                if (state.VehicleId > 0) locTransit++;
                                else                     locOnFoot++;
                                break;
                        }
                    }
                    Observer.AddLocationSnapshot(currentDateTime, locHome, locWork, locVisit, locTransit, locOnFoot);
                    Observer.AddSickCitizens(currentDateTime, initialPopulationSick.Count);
                    Observer.AddExposedCitizens(currentDateTime, initialPopulationExposed.Count);
                    Observer.AddHealthyCitizens(currentDateTime, initialPopulationHealthy.Count);
                    Observer.AddRecoveredCitizens(currentDateTime, initialPopulationRecovered.Count);
                    Observer.AddDeadCitizens(currentDateTime, initialPopulationDead.Count);
                    Observer.FinishObservations(currentDateTime);

                }
                catch (Exception ex)
                {
                    if (HandleSimulationFailure("MetricsEngineException", "Epidemiological metrics update failed.", ex))
                    {
                        return;
                    }
                }

                runIntegrityMonitor.ValidateDiseaseState(diseaseStateEngine, currentDateTime);
                if (runIntegrityMonitor.HasFailed)
                {
                    active = false;
                    return;
                }

                RecordScientificState(currentDateTime);

                lastDateTime = currentDateTime;

                if ((currentDateTime.Ticks - lastStoreTime.Ticks) / TimeSpan.TicksPerMinute >= STORE_INTERVAL_MINUTES)
                {
                    PersistRunSnapshot(force: true, "periodic");
                    lastStoreTime = currentDateTime;
                }

                PandemicRunPolicy policy = runContext?.Policy ?? PandemicRunPolicy.CreateLegacyManual();
                PandemicCompletionReason terminalReason = policy.Evaluate(
                    pandemicRunStartedAt,
                    currentDateTime,
                    initialPopulationSick.Count,
                    initialPopulationExposed.Count,
                    hadAnySickCitizens);
                if (terminalReason != PandemicCompletionReason.None)
                {
                    PersistRunSnapshot(terminalReason == PandemicCompletionReason.DurationReached, "final");

                    FreezeRun(terminalReason);
                    if (terminalReason == PandemicCompletionReason.DurationReached)
                    {
                        Log.Info("The 'Real Time' pandemic manager reached its configured simulation duration.");
                    }
                }
            }
            catch (Exception ex)
            {
                HandleSimulationFailure("SimulationCoreException", "The epidemiological simulation failed unexpectedly.", ex);
            }
            finally
            {
                RecordUpdateDuration(tickStart);
            }
        }

        private bool IsReadyForUpdate()
        {
            return active
                && lifecycleState == PandemicLifecycleState.Running
                && startCompleted
                && simulation != null
                && Config != null
                && CitizenMgr != null
                && CitizenProxy != null
                && BuildingMgr != null
                && Observer != null;
        }

        internal PandemicPerformanceTier GetPerformanceTier() => performanceTier;

        internal float GetAnalyticsRefreshIntervalSeconds()
        {
            switch (performanceTier)
            {
                case PandemicPerformanceTier.Heavy:
                    return 2f;
                case PandemicPerformanceTier.Extreme:
                    return 4f;
                default:
                    return 1f;
            }
        }

        internal float GetOverlayRefreshIntervalSeconds()
        {
            switch (performanceTier)
            {
                case PandemicPerformanceTier.Heavy:
                    return 0.5f;
                case PandemicPerformanceTier.Extreme:
                    return 1f;
                default:
                    return 0.25f;
            }
        }

        internal float GetTrailRefreshIntervalSeconds()
        {
            switch (performanceTier)
            {
                case PandemicPerformanceTier.Heavy:
                    return 0.5f;
                case PandemicPerformanceTier.Extreme:
                    return 1f;
                default:
                    return 0.25f;
            }
        }

        internal float GetXRayRefreshIntervalSeconds()
        {
            switch (performanceTier)
            {
                case PandemicPerformanceTier.Heavy:
                    return 2f;
                case PandemicPerformanceTier.Extreme:
                    return 3f;
                default:
                    return 1f;
            }
        }

        internal float GetPanelRefreshIntervalSeconds() => GetAnalyticsRefreshIntervalSeconds();

        private void RecordUpdateDuration(long tickStartTimestamp)
        {
            if (tickStartTimestamp <= 0)
            {
                return;
            }

            double elapsedMs = (System.Diagnostics.Stopwatch.GetTimestamp() - tickStartTimestamp) * 1000d / System.Diagnostics.Stopwatch.Frequency;
            recentUpdateDurationsMs.Enqueue(elapsedMs);
            while (recentUpdateDurationsMs.Count > PerformanceWindowSize)
            {
                recentUpdateDurationsMs.Dequeue();
            }

            averageUpdateDurationMs = recentUpdateDurationsMs.Count > 0 ? recentUpdateDurationsMs.Average() : 0d;
            UpdatePerformanceTier();
        }

        private void UpdatePerformanceTier()
        {
            int population = initialPopulation.Count;
            int sick = initialPopulationSick.Count + initialPopulationExposed.Count;
            if (population > 30000 || sick > 2000 || averageUpdateDurationMs > 25d)
            {
                performanceTier = PandemicPerformanceTier.Extreme;
                return;
            }

            if (population >= 10000 || sick >= 500)
            {
                performanceTier = PandemicPerformanceTier.Heavy;
                return;
            }

            performanceTier = PandemicPerformanceTier.Light;
        }

        private void ClearTickStateIndex()
        {
            for (int i = 0; i < citizenTickStates.Count; i++)
            {
                citizenTickStatePool.Push(citizenTickStates[i]);
            }

            citizenTickStates.Clear();
            citizenTickStateById.Clear();
            ClearTickStateBuckets(outdoorOccupantsByCell);
            ClearTickStateBuckets(buildingOccupants);
            ClearTickStateBuckets(vehicleOccupants);
        }

        private void ClearTickStateBuckets<TKey>(Dictionary<TKey, List<PandemicCitizenTickState>> buckets)
        {
            foreach (List<PandemicCitizenTickState> states in buckets.Values)
            {
                states.Clear();
                citizenTickStateListPool.Push(states);
            }

            buckets.Clear();
        }

        private PandemicCitizenTickState GetTickState()
        {
            return citizenTickStatePool.Count > 0
                ? citizenTickStatePool.Pop()
                : new PandemicCitizenTickState();
        }

        private List<PandemicCitizenTickState> GetTickStateList()
        {
            return citizenTickStateListPool.Count > 0
                ? citizenTickStateListPool.Pop()
                : new List<PandemicCitizenTickState>(8);
        }

        private void ClearResidentialSharedAreaState(uint citizenId)
        {
            lastObservedBuildingByCitizen.Remove(citizenId);
            buildingEntryTimestampMsByCitizen.Remove(citizenId);
            homeDepartureIntentTimestampMsByCitizen.Remove(citizenId);
        }

        private bool UpdateResidentialSharedAreaWindow(PandemicCitizenTickState state, long nowMs)
        {
            ushort previousBuilding = 0;
            if (!lastObservedBuildingByCitizen.TryGetValue(state.CitizenId, out previousBuilding))
            {
                lastObservedBuildingByCitizen[state.CitizenId] = state.CurrentBuilding;
                buildingEntryTimestampMsByCitizen.Remove(state.CitizenId);
                homeDepartureIntentTimestampMsByCitizen.Remove(state.CitizenId);
            }
            else if (previousBuilding != state.CurrentBuilding)
            {
                lastObservedBuildingByCitizen[state.CitizenId] = state.CurrentBuilding;
                if (state.CurrentBuilding != 0)
                {
                    buildingEntryTimestampMsByCitizen[state.CitizenId] = nowMs;
                }
                else
                {
                    buildingEntryTimestampMsByCitizen.Remove(state.CitizenId);
                }

                homeDepartureIntentTimestampMsByCitizen.Remove(state.CitizenId);
            }

            if (!state.AtHome)
            {
                homeDepartureIntentTimestampMsByCitizen.Remove(state.CitizenId);
                return false;
            }

            bool inEntryWindow = buildingEntryTimestampMsByCitizen.TryGetValue(state.CitizenId, out long buildingEnteredAtMs)
                && nowMs - buildingEnteredAtMs <= ResidentialSharedAreaExposureWindowMs;
            bool hasDepartureIntent = HasHomeDepartureIntent(state);
            if (!hasDepartureIntent)
            {
                homeDepartureIntentTimestampMsByCitizen.Remove(state.CitizenId);
                return inEntryWindow;
            }

            if (!homeDepartureIntentTimestampMsByCitizen.TryGetValue(state.CitizenId, out long departureIntentStartedAtMs))
            {
                departureIntentStartedAtMs = nowMs;
                homeDepartureIntentTimestampMsByCitizen[state.CitizenId] = departureIntentStartedAtMs;
            }

            return inEntryWindow || (nowMs - departureIntentStartedAtMs <= ResidentialSharedAreaExposureWindowMs);
        }

        private static bool HasHomeDepartureIntent(PandemicCitizenTickState state)
        {
            return state.AtHome
                && (state.Location != Citizen.Location.Home
                    || (state.TargetBuilding != 0 && state.TargetBuilding != state.HomeBuilding)
                    || state.TargetNode != 0);
        }

        private void AddTickStateToBucket<TKey>(Dictionary<TKey, List<PandemicCitizenTickState>> buckets, TKey key, PandemicCitizenTickState state)
        {
            if (!buckets.TryGetValue(key, out List<PandemicCitizenTickState> states))
            {
                states = GetTickStateList();
                buckets[key] = states;
            }

            states.Add(state);
        }

        private void ProcessCitizenTesting(Citizen[] citizens) { using (PandemicProfiler.Measure("ProcessCitizenTesting")) { ProcessCitizenTestingProfiled(citizens); } }
        private void ProcessCitizenTestingProfiled(Citizen[] citizens) {
            for (int i = 0; i < initialPopulation.Count; i++)
            {
                uint citizenId = initialPopulation[i];
                uint realId = retrieveID(citizenId);
                if (realId >= citizens.Length)
                {
                    continue;
                }

                if (!CitizenProxy.IsDead(ref citizens[realId]))
                {
                    TestManager.Instance.TestCitizen(citizenId, activeInfections.ContainsKey(citizenId), IsKnownSick(citizenId), currentDateTime);
                }
            }

            TestManager.Instance.ProcessPendingTests(currentDateTime, CreateTestSampleContext);
        }

        private PandemicTestSampleContext CreateTestSampleContext(uint citizenId)
        {
            DiseaseState state = diseaseStateEngine.GetState(citizenId, currentDateTime);
            diseaseStateEngine.TryGetCourse(citizenId, out DiseaseCourse course);
            return new PandemicTestSampleContext
            {
                DiseaseState = state,
                ExposureTime = course?.ExposureTime,
            };
        }

        private void ProcessRecoveries(Citizen[] citizens) { using (PandemicProfiler.Measure("ProcessRecoveries")) { ProcessRecoveriesProfiled(citizens); } }
        private void ProcessRecoveriesProfiled(Citizen[] citizens) {
            for (int i = 0; i < initialPopulationSick.Count; i++)
            {
                uint citizenId = initialPopulationSick[i];
                if (!activeInfections.ContainsKey(citizenId))
                {
                    continue;
                }

                uint realId = retrieveID(citizenId);
                if (realId >= citizens.Length)
                {
                    continue;
                }

                if (diseaseStateEngine.GetState(citizenId, currentDateTime) == DiseaseState.Recovered)
                {
                    HealCitizen(citizenId, ref citizens[realId]);
                    i--;
                }
            }
        }

        private void ProcessContactTracingCandidates() { using (PandemicProfiler.Measure("ProcessContactTracingCandidates")) { ProcessContactTracingCandidatesProfiled(); } }
        private void ProcessContactTracingCandidatesProfiled() {
            // Batch inputs cannot increase the biological lookback mid-run. Manual runs retain
            // history because the user may still increase their tracing window interactively.
            if (runContext?.IsBatch == true) ContactManager.Instance.PruneExpired(currentDateTime, TimeSpan.FromDays(Config.DiseaseDuration));
            if (Config.QuarantineBehavior != RealTime.Config.QuarantineBehavior.Contacts
                && Config.QuarantineBehavior != RealTime.Config.QuarantineBehavior.Family)
            {
                return;
            }

            for (int i = 0; i < initialPopulation.Count; i++)
            {
                uint citizenId = initialPopulation[i];
                DiseaseState state = diseaseStateEngine.GetState(citizenId, currentDateTime);
                if (state == DiseaseState.Dead || state == DiseaseState.Removed)
                {
                    continue;
                }

                if (TestManager.Instance.IsTestedPositive(citizenId, currentDateTime)
                    || (!Config.OnlyTestedCitizensToQuarantine && IsKnownSick(citizenId)))
                {
                    CheckForContacts(citizenId, Config.DiseaseDuration, Config.QuarantineBehavior);
                }
            }
        }

        private void BuildCitizenTickStateIndex(Citizen[] citizens) { using (PandemicProfiler.Measure("BuildCitizenTickStateIndex")) { BuildCitizenTickStateIndexProfiled(citizens); } }
        private void BuildCitizenTickStateIndexProfiled(Citizen[] citizens) {
            ClearTickStateIndex();

            float outdoorCellSize = Mathf.Max(1f, Config.DiseaseTransmissionRange);
            long nowMs = currentDateTime.Ticks / 10000;
            for (int i = 0; i < initialPopulation.Count; i++)
            {
                uint citizenId = initialPopulation[i];
                uint realId = retrieveID(citizenId);
                if (realId >= citizens.Length)
                {
                    ClearResidentialSharedAreaState(citizenId);
                    continue;
                }

                ref Citizen citizen = ref citizens[realId];
                if (CitizenProxy.IsEmpty(ref citizen) || CitizenProxy.IsDead(ref citizen))
                {
                    ClearResidentialSharedAreaState(citizenId);
                    continue;
                }

                PandemicCitizenTickState state = GetTickState();
                state.CitizenId = citizenId;
                state.RealCitizenId = realId;
                state.HomeBuilding = CitizenProxy.GetHomeBuilding(ref citizen);
                state.CurrentBuilding = CitizenProxy.GetCurrentBuilding(ref citizen);
                state.VehicleId = CitizenProxy.GetVehicle(ref citizen);
                state.InstanceId = CitizenProxy.GetInstance(ref citizen);
                state.Location = CitizenProxy.GetLocation(ref citizen);
                state.AgeGroup = CitizenProxy.GetAge(ref citizen);
                DiseaseState diseaseState = diseaseStateEngine.GetState(citizenId, currentDateTime);
                state.IsInfected = diseaseState == DiseaseState.Exposed
                    || diseaseState == DiseaseState.Infectious
                    || diseaseState == DiseaseState.PostInfectiousIll;
                state.IsInfectious = diseaseState == DiseaseState.Infectious && IsInfectious(citizenId);
                state.IsSusceptible = diseaseState == DiseaseState.Susceptible;
                state.IsKnownSick = state.IsInfected && IsKnownSick(citizenId);
                state.ShouldQuarantine = ShouldLogicalCitizenBeInQuarantine(citizenId);
                state.AtHome = state.CurrentBuilding != 0 && state.HomeBuilding != 0 && state.CurrentBuilding == state.HomeBuilding;
                state.TargetBuilding = state.InstanceId != 0 ? CitizenMgr.GetTargetBuilding(state.InstanceId) : (ushort)0;
                state.TargetNode = state.InstanceId != 0 ? CitizenMgr.GetTargetNode(state.InstanceId) : (ushort)0;
                state.IsInResidentialSharedAreaWindow = UpdateResidentialSharedAreaWindow(state, nowMs);
                state.Position = GetContactPosition(state, ref citizen);
                state.OutdoorCellX = GetOutdoorCellCoordinate(state.Position.x, outdoorCellSize);
                state.OutdoorCellZ = GetOutdoorCellCoordinate(state.Position.z, outdoorCellSize);
                citizenTickStates.Add(state);
                citizenTickStateById[state.CitizenId] = state;

                if (state.VehicleId != 0)
                {
                    AddTickStateToBucket(vehicleOccupants, state.VehicleId, state);
                }
                else if (state.CurrentBuilding != 0)
                {
                    AddTickStateToBucket(buildingOccupants, state.CurrentBuilding, state);
                }
                else if (state.InstanceId != 0 && state.Position != Vector3.zero)
                {
                    AddTickStateToBucket(
                        outdoorOccupantsByCell,
                        GetOutdoorCellKey(state.OutdoorCellX, state.OutdoorCellZ),
                        state);
                }

                if (SET_SICK_FLAG && state.IsInfected && !CitizenProxy.IsSick(ref citizen))
                {
                    CitizenProxy.SetSick(ref citizen, true);
                }

                if (state.IsInfectious)
                {
                    Observer.AddInfectiousCitizen(citizenId);
                }


            }
        }

        private void RefreshPostTransmissionTickStates(Citizen[] citizens)
        {
            using (PandemicProfiler.Measure("PostTransmissionState"))
            foreach (PandemicCitizenTickState state in citizenTickStates)
            {
                DiseaseState disease = diseaseStateEngine.GetState(state.CitizenId, currentDateTime);
                state.IsInfected = disease == DiseaseState.Exposed || disease == DiseaseState.Infectious || disease == DiseaseState.PostInfectiousIll;
                state.IsInfectious = disease == DiseaseState.Infectious && IsInfectious(state.CitizenId);
                state.IsSusceptible = disease == DiseaseState.Susceptible;
                state.IsKnownSick = state.IsInfected && IsKnownSick(state.CitizenId);
                state.ShouldQuarantine = ShouldLogicalCitizenBeInQuarantine(state.CitizenId);
                if (SET_SICK_FLAG && state.IsInfected && !CitizenProxy.IsSick(ref citizens[state.RealCitizenId]))
                    CitizenProxy.SetSick(ref citizens[state.RealCitizenId], true);
                if (state.IsInfectious) Observer.AddInfectiousCitizen(state.CitizenId);
            }
        }

        private static int GetOutdoorCellCoordinate(float coordinate, float cellSize)
        {
            return Mathf.FloorToInt((coordinate + OutdoorMapHalfSize) / cellSize);
        }

        private static int GetOutdoorCellKey(int cellX, int cellZ)
        {
            return (cellX * OutdoorCellStride) + cellZ;
        }

        private Vector3 GetContactPosition(PandemicCitizenTickState state, ref Citizen citizen)
        {
            if (state.InstanceId != 0)
            {
                return CitizenMgr.GetCitizenPosition(state.InstanceId);
            }

            if (state.CurrentBuilding != 0)
            {
                return BuildingMgr.GetBuildingPosition(state.CurrentBuilding);
            }

            if (state.VehicleId != 0 && VehicleManager.instance != null)
            {
                return VehicleManager.instance.m_vehicles.m_buffer[state.VehicleId].m_frame0.m_position;
            }

            return state.HomeBuilding != 0 ? BuildingMgr.GetBuildingPosition(state.HomeBuilding) : Vector3.zero;
        }

        private void BuildPandemicBuildingSets()
        {
            using (PandemicProfiler.Measure("BuildPandemicBuildingSets")) BuildPandemicBuildingSetsProfiled();
        }

        private void BuildPandemicBuildingSetsProfiled()
        {
            infectedBuildingIds.Clear();
            quarantineBuildingIds.Clear();
            hotspotBuildingIds.Clear();
            hubBuildingIds.Clear();
            buildingInfectedCounts.Clear();
            buildingCurrentInfectedCounts.Clear();

            if (!worldOverlaysEnabled || !IsVisualizationDataAvailable()) return;

            foreach (PandemicCitizenTickState state in citizenTickStates)
            {
                if (state.IsInfected && state.HomeBuilding != 0)
                {
                    infectedBuildingIds.Add(state.HomeBuilding);
                    if (buildingInfectedCounts.TryGetValue(state.HomeBuilding, out int infectedAtHome))
                    {
                        buildingInfectedCounts[state.HomeBuilding] = infectedAtHome + 1;
                    }
                    else
                    {
                        buildingInfectedCounts[state.HomeBuilding] = 1;
                    }
                }

                if (state.IsInfected && state.CurrentBuilding != 0)
                {
                    if (buildingCurrentInfectedCounts.TryGetValue(state.CurrentBuilding, out int infectedInCurrent))
                    {
                        buildingCurrentInfectedCounts[state.CurrentBuilding] = infectedInCurrent + 1;
                    }
                    else
                    {
                        buildingCurrentInfectedCounts[state.CurrentBuilding] = 1;
                    }
                }
            }

            foreach (var kvp in buildingInfectedCounts)
            {
                if (kvp.Value >= Config.HubHighlightThreshold)
                {
                    hotspotBuildingIds.Add(kvp.Key);
                    quarantineBuildingIds.Add(kvp.Key);
                }
            }

            foreach (var kvp in buildingCurrentInfectedCounts)
            {
                if (kvp.Value < Config.HubHighlightThreshold)
                {
                    continue;
                }

                if (BuildingMgr.GetBuildingService(kvp.Key) != ItemClass.Service.Residential)
                {
                    hubBuildingIds.Add(kvp.Key);
                }
            }

            foreach (uint citizenId in QuarantineManager.Instance.GetQuarantinedCitizens())
            {
                if (citizenTickStateById.TryGetValue(citizenId, out PandemicCitizenTickState state) && state.HomeBuilding != 0)
                    quarantineBuildingIds.Add(state.HomeBuilding);
            }
        }

        internal bool IsVisualizationDataAvailable()
        {
            return lifecycleState != PandemicLifecycleState.Dormant
                && startCompleted
                && CitizenMgr != null
                && CitizenProxy != null
                && BuildingMgr != null;
        }

        internal int PopulateInfectedResidentsByBuilding(Dictionary<ushort, int> target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            target.Clear();
            if (!IsVisualizationDataAvailable())
            {
                return 0;
            }

            Citizen[] citizens = CitizenMgr.GetCitizensArray();
            if (citizens == null || citizens.Length == 0)
            {
                return 0;
            }

            int infectedResidents = 0;
            foreach (uint citizenId in activeInfections.Keys)
            {
                uint resolvedCitizenId = retrieveID(citizenId);
                if (resolvedCitizenId >= citizens.Length)
                {
                    continue;
                }

                ref Citizen citizen = ref citizens[resolvedCitizenId];
                if (CitizenProxy.IsEmpty(ref citizen) || CitizenProxy.IsDead(ref citizen))
                {
                    continue;
                }

                ushort homeBuilding = CitizenProxy.GetHomeBuilding(ref citizen);
                if (homeBuilding == 0 || BuildingMgr.GetBuildingService(homeBuilding) != ItemClass.Service.Residential)
                {
                    continue;
                }

                if (target.TryGetValue(homeBuilding, out int currentCount))
                {
                    target[homeBuilding] = currentCount + 1;
                }
                else
                {
                    target.Add(homeBuilding, 1);
                }

                infectedResidents++;
            }

            return infectedResidents;
        }

        public bool IsCitizenInfected(uint citizenId)
        {
            citizenId = ResolveLogicalCitizenIdFromGame(citizenId);
            return citizenId != 0 && activeInfections.ContainsKey(citizenId);
        }

        public bool IsActive => lifecycleState == PandemicLifecycleState.Running;

        public HashSet<ushort> InfectedBuildingIds => infectedBuildingIds;

        public HashSet<ushort> QuarantineBuildingIds => quarantineBuildingIds;

        public HashSet<ushort> HotspotBuildingIds => hotspotBuildingIds;

        public HashSet<ushort> HubBuildingIds => hubBuildingIds;

        public int GetInfectedCountInBuilding(ushort buildingId)
        {
            if (buildingId == 0) return 0;
            return buildingInfectedCounts.TryGetValue(buildingId, out int count) ? count : 0;
        }

        public int GetCurrentInfectedCountAtBuilding(ushort buildingId)
        {
            if (buildingId == 0)
            {
                return 0;
            }

            return buildingCurrentInfectedCounts.TryGetValue(buildingId, out int count) ? count : 0;
        }

        public bool IsCitizenWearingMask(uint citizenId)
        {
            citizenId = ResolveLogicalCitizenIdFromGame(citizenId);
            return Masks?.IsWearingMask(citizenId) ?? false;
        }

        public bool IsMasksEnabled() => Config != null && Config.MaskBehavior != RealTime.Config.MaskBehavior.None;

        public bool IsQuarantineEnabled() => Config != null && Config.QuarantineBehavior != RealTime.Config.QuarantineBehavior.None;

        internal DateTime GetPandemicRunStartedAt() => pandemicRunStartedAt;

        internal IList<PandemicObservation> GetAllObservations() =>
            Observer?.GetObservations() ?? new List<PandemicObservation>();

        internal ExperimentRecorderSnapshot GetScientificRunSnapshot()
        {
            if (!frozenForFinalization || frozenScientificSnapshot == null)
            {
                throw new InvalidOperationException("Scientific output can only be captured after the run is frozen.");
            }

            return frozenScientificSnapshot;
        }

        internal IList<PandemicTestRecord> GetTestRecords()
        {
            return TestManager.Instance.Engine == null
                ? new List<PandemicTestRecord>()
                : TestManager.Instance.Engine.Records.OrderBy(record => record.TestId).ToList();
        }

        internal ExperimentRunIntegrityFailure GetRunIntegrityFailure() => runIntegrityMonitor.Failure;

        internal string BuildTransmissionDrilldown(uint citizenId, double lookbackDays, int limit)
        {
            var text = new System.Text.StringBuilder();
            text.AppendLine("Citizen " + citizenId.ToString(CultureInfo.InvariantCulture));
            if (diseaseStateEngine.TryGetCourse(citizenId, out DiseaseCourse course))
            {
                text.AppendLine("Infection: " + course.ExposureTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
                text.AppendLine("Infectious interval: " + course.InfectiousStartTime.ToString("g", CultureInfo.InvariantCulture)
                    + " to " + course.InfectiousEndTime.ToString("g", CultureInfo.InvariantCulture) + " (end exclusive)");
            }
            var events = experimentRecorder.TransmissionEvents;
            var contexts = new SortedDictionary<string, int>(StringComparer.Ordinal);
            int total = 0;
            int inWindow = 0;
            DateTime since = currentDateTime.AddDays(-Math.Min(lookbackDays, 3650d));
            for (int i = 0; i < events.Count; i++)
            {
                var item = events[i];
                if (item.SourceCitizenId != citizenId) continue;
                total++;
                string context = item.Context.ToString();
                contexts[context] = contexts.TryGetValue(context, out int count) ? count + 1 : 1;
                if (item.SimulationTime >= since) inWindow++;
            }
            text.AppendLine("Secondary transmissions: " + total + " | In selected window: " + inWindow);
            foreach (var context in contexts) text.Append(context.Key).Append(": ").Append(context.Value).Append("   ");
            text.AppendLine();
            text.AppendLine("Recent transmission links (latest " + limit + "; display limit only):");
            int shown = 0;
            for (int i = events.Count - 1; i >= 0 && shown < limit; i--)
            {
                var item = events[i];
                if (item.SourceCitizenId != citizenId || item.SimulationTime < since) continue;
                text.Append(citizenId).Append(" → ").Append(item.TargetCitizenId).Append(" | ")
                    .Append(item.SimulationTime.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture)).Append(" | ")
                    .Append(item.Context).Append(" | B ").Append(item.BuildingId).Append(" / V ").Append(item.VehicleId).AppendLine();
                shown++;
            }
            return text.ToString();
        }

        internal double GetActualMaskUsagePercent()
        {
            if (!IsMasksEnabled() || initialPopulation.Count == 0)
            {
                return 0d;
            }

            int wearing = 0;
            for (int i = 0; i < initialPopulation.Count; ++i)
            {
                if (Masks.IsWearingMask(initialPopulation[i]))
                {
                    wearing++;
                }
            }

            return wearing * 100d / initialPopulation.Count;
        }

        internal IList<PandemicHealthcareTimePoint> GetHealthcareTimeSeries()
        {
            var result = new List<PandemicHealthcareTimePoint>(healthcareUsageSamples.Count);
            foreach (PandemicHealthcareUsageSample sample in healthcareUsageSamples)
            {
                if (sample != null)
                {
                    result.Add(new PandemicHealthcareTimePoint
                    {
                        SimulationTime = sample.SimulationTime,
                        HospitalUsagePercent = sample.HospitalUsagePercent,
                        AmbulanceUsagePercent = sample.AmbulanceUsagePercent,
                    });
                }
            }

            return result;
        }

        public bool IsLockdownEnabled() => QuarantineManager.Instance.InLockDown;

        public bool AreWorldOverlaysEnabled() => worldOverlaysEnabled;

        internal bool IsXRayEnabled() => xRayEnabled;

        internal PandemicXRayMetric GetXRayMetric() => xRayMetric;

        internal PandemicXRayLocationMode GetXRayLocationMode() => xRayLocationMode;

        internal Config.RealTimeConfig RuntimeConfig => Config;

        internal PandemicRunContext CurrentRunContext => runContext;

        internal PandemicCompletionReason CompletionReason => completionReason;

        internal bool IsBatchRunOwned => runContext != null && runContext.IsBatch;

        internal bool TryGetRunIntegrityFailure(out ExperimentRunIntegrityFailure failure)
        {
            failure = runIntegrityMonitor.Failure;
            return failure != null;
        }

        internal bool IsAwaitingBatchFinalization => IsBatchRunOwned
            && lifecycleState == PandemicLifecycleState.Finished
            && frozenForFinalization;

        internal DateTime GetRunTargetSimulationTime()
        {
            return runContext != null && pandemicRunStartedAt != default(DateTime)
                ? runContext.Policy.CalculateTarget(pandemicRunStartedAt)
                : default(DateTime);
        }

        internal string BuildObserverCsv() => Observer?.BuildCsv() ?? string.Empty;

        internal string BuildContactsCsv() => ContactManager.Instance.BuildCsv();

        /// <summary>Forces the mask state for a single citizen, overriding random assignment.</summary>
        public void ForceSetCitizenMask(uint citizenId, bool masked)
        {
            if (ExperimentControlGate.IsControlLocked)
            {
                return;
            }

            Masks?.SetMaskForCitizen(ResolveLogicalCitizenIdFromGame(citizenId), masked);
        }

        /// <summary>Toggles quarantine for a single citizen without altering the disease course.</summary>
        public void ToggleCitizenQuarantine(uint citizenId)
        {
            if (ExperimentControlGate.IsControlLocked)
            {
                return;
            }

            // Policy callbacks use the last epidemiological step. Advancing this shared
            // clock to a frame time can put population events ahead of the next exact step.

            citizenId = ResolveLogicalCitizenIdFromGame(citizenId);
            DateTime now = currentDateTime;
            if (QuarantineManager.Instance.IsRestricted(citizenId, now))
            {
                QuarantineManager.Instance.RemoveAllRestrictions(citizenId, now, "ManualToggle");
            }
            else
            {
                QuarantineManager.Instance.AddCitizenInIsolation(citizenId, now);
            }
        }

        public bool ToggleMasks()
        {
            if (ExperimentControlGate.IsControlLocked) return IsMasksEnabled();
            if (Config == null) return false;
            Config.MaskBehavior = Config.MaskBehavior == RealTime.Config.MaskBehavior.None
                ? RealTime.Config.MaskBehavior.Full
                : RealTime.Config.MaskBehavior.None;
            bool enabled = IsMasksEnabled();
            RecordPolicyTimelineEntry(PandemicPolicyMarkerType.Masks, enabled);
            return enabled;
        }

        public bool ToggleQuarantine()
        {
            if (ExperimentControlGate.IsControlLocked) return IsQuarantineEnabled();
            if (Config == null) return false;
            Config.QuarantineBehavior = Config.QuarantineBehavior == RealTime.Config.QuarantineBehavior.None
                ? RealTime.Config.QuarantineBehavior.Contacts
                : RealTime.Config.QuarantineBehavior.None;
            return IsQuarantineEnabled();
        }

        public bool ToggleLockdown()
        {
            if (ExperimentControlGate.IsControlLocked) return IsLockdownEnabled();
            QuarantineManager.Instance.InLockDown = !QuarantineManager.Instance.InLockDown;
            bool enabled = QuarantineManager.Instance.InLockDown;
            RecordPolicyTimelineEntry(PandemicPolicyMarkerType.Lockdown, enabled);
            UpdatePublicTransportShutdownState(forceRefresh: true);
            return enabled;
        }

        public bool ToggleWorldOverlays()
        {
            worldOverlaysEnabled = !worldOverlaysEnabled;
            BuildPandemicBuildingSets();
            return worldOverlaysEnabled;
        }

        internal bool ToggleXRayEnabled()
        {
            xRayEnabled = !xRayEnabled;
            return xRayEnabled;
        }

        internal PandemicXRayMetric CycleXRayMetric()
        {
            xRayMetric = (PandemicXRayMetric)(((int)xRayMetric + 1) % Enum.GetValues(typeof(PandemicXRayMetric)).Length);
            return xRayMetric;
        }

        internal PandemicXRayLocationMode CycleXRayLocationMode()
        {
            xRayLocationMode = xRayLocationMode == PandemicXRayLocationMode.LivePositions
                ? PandemicXRayLocationMode.HomeLocations
                : PandemicXRayLocationMode.LivePositions;

            return xRayLocationMode;
        }

        private void SeedPolicyTimeline()
        {
            policyTimeline.Clear();
            districtHeatmapIds = null;
            previousRecordedContactTotal = lastStepContactEvents = 0;

            if (Config == null)
            {
                return;
            }

            CapturePolicyTimelineState(force: true);
        }

        private void CapturePolicyTimelineState(bool force = false)
        {
            RecordPolicyTimelineEntry(PandemicPolicyMarkerType.Masks, IsMasksEnabled(), force);
            RecordPolicyTimelineEntry(PandemicPolicyMarkerType.Lockdown, QuarantineManager.Instance.InLockDown, force);
            RecordPolicyTimelineEntry(PandemicPolicyMarkerType.Testing, Config.RelativeTestCapacity > 0, force);
            bool isolation = Config.QuarantineBehavior != RealTime.Config.QuarantineBehavior.None;
            RecordPolicyTimelineEntry(PandemicPolicyMarkerType.Isolation, isolation, force);
            RecordPolicyTimelineEntry(PandemicPolicyMarkerType.Quarantine, isolation && (Config.QuarantineWhileAwaitingTestResult || Config.QuarantineBehavior == RealTime.Config.QuarantineBehavior.Contacts || Config.QuarantineBehavior == RealTime.Config.QuarantineBehavior.Family), force);
            RecordPolicyTimelineEntry(PandemicPolicyMarkerType.Tracing, Config.QuarantineBehavior == RealTime.Config.QuarantineBehavior.Contacts && (Config.AppBasedContactTracingProbability > 0 || Config.BuildingContactTracingProbability > 0), force);
            RecordPolicyTimelineEntry(PandemicPolicyMarkerType.PublicTransport, IsPublicTransportClosedEffective(), force);
        }

        private void RecordPolicyTimelineEntry(PandemicPolicyMarkerType type, bool enabled, bool force = false)
        {
            if (!force && lifecycleState != PandemicLifecycleState.Running)
            {
                return;
            }

            if (!force)
                for (int i = policyTimeline.Count - 1; i >= 0; i--)
                    if (policyTimeline[i].Type == type)
                    {
                        if (policyTimeline[i].Enabled == enabled) return;
                        break;
                    }

            DateTime timestamp = currentDateTime;
            if (timestamp == default(DateTime))
            {
                timestamp = DateTime.Now;
            }

            policyTimeline.Add(new PandemicPolicyTimelineEntry
            {
                SimulationTime = timestamp,
                Type = type,
                Enabled = enabled,
            });
            PandemicInterventionType intervention;
            switch (type)
            {
                case PandemicPolicyMarkerType.Masks: intervention = PandemicInterventionType.Mask; break;
                case PandemicPolicyMarkerType.Testing: intervention = PandemicInterventionType.Testing; break;
                case PandemicPolicyMarkerType.Isolation: intervention = PandemicInterventionType.Isolation; break;
                case PandemicPolicyMarkerType.Quarantine: intervention = PandemicInterventionType.Quarantine; break;
                case PandemicPolicyMarkerType.Tracing: intervention = PandemicInterventionType.Tracing; break;
                case PandemicPolicyMarkerType.PublicTransport: intervention = PandemicInterventionType.PublicTransportShutdown; break;
                default: intervention = PandemicInterventionType.Lockdown; break;
            }
            RecordRuntimeIntervention(intervention,
                enabled ? "Enable" : "Disable",
                force && lifecycleState != PandemicLifecycleState.Running ? "RunInitialPolicy" : "PolicyChanged",
                "GlobalPolicy");
        }

        private void RecordRuntimeIntervention(
            PandemicInterventionType type,
            string action,
            string reason,
            string context)
        {
            if ((lifecycleState != PandemicLifecycleState.Running && reason != "RunInitialPolicy")
                || frozenForFinalization
                || experimentRecorder.RunStartTime == default(DateTime))
            {
                return;
            }

            DateTime timestamp = currentDateTime;
            if (timestamp == default(DateTime))
            {
                return;
            }

            experimentRecorder.RecordIntervention(new PandemicInterventionEvent
            {
                SimulationTime = timestamp,
                InterventionType = type,
                Action = action,
                Reason = reason,
                Context = context,
            });
        }

        private void RecordInfectionOrigin(uint citizenId, PandemicInfectionOriginInfo origin)
        {
            if (origin == null)
            {
                return;
            }

            infectionOrigins[citizenId] = origin;
        }

        public bool TryGetInfectionSource(uint citizenId, out ushort buildingId, out InfectionType infType)
        {
            citizenId = ResolveLogicalCitizenIdFromGame(citizenId);
            infType = InfectionType.OUTDOOR;
            buildingId = 0;

            if (!infectionOrigins.TryGetValue(citizenId, out PandemicInfectionOriginInfo origin) || origin == null)
            {
                return false;
            }

            buildingId = origin.BuildingId;
            infType = origin.LegacyType;
            return buildingId != 0;
        }

        internal bool TryGetInfectionOrigin(uint citizenId, out PandemicInfectionOriginInfo origin)
        {
            citizenId = ResolveLogicalCitizenIdFromGame(citizenId);
            return infectionOrigins.TryGetValue(citizenId, out origin);
        }

        public void ManuallyInfectCitizen(uint citizenId)
        {
            if (ExperimentControlGate.IsControlLocked) return;
            citizenId = ResolveLogicalCitizenIdFromGame(citizenId);
            if (activeInfections.ContainsKey(citizenId)) return;
            if (CitizenMgr == null || CitizenProxy == null) return;
            Citizen[] citizens = CitizenMgr.GetCitizensArray();
            uint realId = retrieveID(citizenId);
            if (realId >= citizens.Length) return;
            ref Citizen citizen = ref citizens[realId];
            if (CitizenProxy.IsEmpty(ref citizen) || CitizenProxy.IsDead(ref citizen)) return;
            if (TryInfectCitizen(citizenId, ref citizen, 0, DiseaseExposureKind.InitialSeed))
            {
                RecordInfectionOrigin(citizenId, CreateOutdoorOrigin(CitizenMgr.GetCitizenPosition(CitizenProxy.GetInstance(ref citizen))));
            }
        }

        public IEnumerable<ushort> GetInfectedCitizenInstanceIds()
        {
            if (!IsVisualizationDataAvailable())
            {
                yield break;
            }

            Citizen[] citizens = CitizenMgr.GetCitizensArray();
            if (citizens == null)
            {
                yield break;
            }

            foreach (uint citizenId in activeInfections.Keys)
            {
                uint realId = retrieveID(citizenId);
                if (realId < citizens.Length)
                {
                    ushort instanceId = CitizenProxy.GetInstance(ref citizens[realId]);
                    if (instanceId != 0)
                    {
                        yield return instanceId;
                    }
                }
            }
        }

        internal PandemicLiveSnapshot GetLiveSnapshot()
        {
            using (PandemicProfiler.Measure("AnalyticsSnapshot")) return GetLiveSnapshotCore();
        }

        private PandemicLiveSnapshot GetLiveSnapshotCore()
        {
            if (frozenForFinalization && liveSnapshotCache != null)
            {
                return liveSnapshotCache;
            }

            if (liveSnapshotCache == null)
            {
                liveSnapshotCache = new PandemicLiveSnapshot();
            }

            PandemicLiveSnapshot snapshot = liveSnapshotCache;
            RefreshCoreSnapshot(snapshot);

            float now = Time.unscaledTime;
            if (ShouldRefreshAnalyticsSnapshot(now, snapshot.TransmissionsTotal))
            {
                snapshot.AgeGroups.Clear();
                snapshot.Districts.Clear();
                snapshot.Origins.Clear();
                snapshot.LockdownFamilies.Clear();
                PopulateLiveSnapshotBreakdown(snapshot);
                PopulateOriginSnapshots(snapshot);
                PopulateLockdownFamilies(snapshot);
                cachedAnalyticsInfectionCount = snapshot.TransmissionsTotal;
                nextAnalyticsSnapshotRefreshTime = now + GetAnalyticsRefreshIntervalSeconds();
                snapshotVersion++;
            }

            if (ShouldRefreshSuperspreaderSnapshot(now, snapshot.TransmissionsTotal))
            {
                snapshot.TopSpreaders.Clear();
                snapshot.TopOriginLocations.Clear();
                PopulateSuperspreaders(snapshot);
                cachedSuperspreaderInfectionCount = snapshot.TransmissionsTotal;
                nextSuperspreaderSnapshotRefreshTime = now + (GetAnalyticsRefreshIntervalSeconds() * 2f);
                snapshotVersion++;
            }

            RefreshChartSnapshot(snapshot);
            RefreshPolicyMarkerSnapshot(snapshot);
            snapshot.HasChartData = snapshot.ChartPoints.Count > 0;
            snapshot.SnapshotVersion = snapshotVersion;
            snapshot.ChartVersion = chartVersion;
            return snapshot;
        }

        private void RefreshCoreSnapshot(PandemicLiveSnapshot snapshot)
        {
            snapshot.IsActive = active;
            snapshot.IsInitialized = startCompleted;
            snapshot.LifecycleState = lifecycleState;
            snapshot.HasStartedAtLeastOnce = hasStartedAtLeastOnce;
            snapshot.CanStart = lifecycleState == PandemicLifecycleState.Dormant;
            snapshot.CanRestart = hasStartedAtLeastOnce;
            snapshot.CanStop = lifecycleState != PandemicLifecycleState.Dormant;
            snapshot.WorldOverlaysEnabled = worldOverlaysEnabled;
            snapshot.XRayEnabled = xRayEnabled;
            snapshot.XRayMetric = xRayMetric;
            snapshot.XRayLocationMode = xRayLocationMode;
            snapshot.PublicTransportState = publicTransportShutdownState;
            snapshot.PublicTransportTrackedLines = publicTransportLineStates.Count;
            snapshot.PublicTransportReturningVehicles = publicTransportVehicles.Count;
            snapshot.PublicTransportClosedDepots = publicTransportDepotStates.Count(depot => depot.Value.IsClosed);
            snapshot.SimulationTime = currentDateTime;
            snapshot.PandemicDay = GetPandemicDayNumber();
            snapshot.Healthy = initialPopulationHealthy.Count;
            snapshot.Sick = initialPopulationSick.Count;
            snapshot.Exposed = initialPopulationExposed.Count;
            snapshot.Recovered = initialPopulationRecovered.Count;
            snapshot.Dead = initialPopulationDead.Count;
            snapshot.DeltaSick = 0;
            snapshot.DeltaRecovered = 0;
            snapshot.DeltaDead = 0;
            snapshot.QuarantineCitizens = QuarantineManager.Instance.CitizensInQuarantine();
            snapshot.PositiveTests = TestManager.Instance.GetCurrentPositiveCount(snapshot.SimulationTime);
            var latestScientific = experimentRecorder.LatestState;
            snapshot.ActiveInfectious = latestScientific?.Infectious ?? 0;
            snapshot.PostInfectiousIll = latestScientific?.PostInfectiousIll ?? 0;
            snapshot.NewInfections = latestScientific?.NewExposures ?? 0;
            snapshot.DetectedNewCases = latestScientific?.DetectedNewCases ?? 0;
            snapshot.ObservedIncidence = latestScientific?.TrackedPopulation > 0 ? latestScientific.DetectedNewCases * 100000d / latestScientific.TrackedPopulation : 0d;
            snapshot.PolicyTriggerLabel = Config.AutomaticPolicyTriggerMetric.ToString();
            snapshot.TestedCitizens = TestManager.Instance.GetTrackedTestsCount();
            snapshot.ContactsTrackedCitizens = ContactManager.Instance.GetTrackedCitizenCount();
            snapshot.ContactsTrackedPairs = ContactManager.Instance.GetTrackedPairCount();
            snapshot.ContactsRecordedTotal = ContactManager.Instance.GetTotalRecordedContacts();
            snapshot.ObservationCount = Observer?.GetObservationCount() ?? 0;
            snapshot.TransmissionsTotal = Observer?.GetTotalInfections() ?? 0;
            snapshot.TransmissionsIndoor = Observer?.GetIndoorInfections() ?? 0;
            snapshot.TransmissionsOutdoor = Observer?.GetOutdoorInfections() ?? 0;
            snapshot.TransmissionsVehicle = Observer?.GetVehicleInfections() ?? 0;
            snapshot.HotspotBuildings = hotspotBuildingIds.Count;
            snapshot.HubBuildings = hubBuildingIds.Count;
            PopulateHealthcareUsageSnapshot(snapshot);

            if (Observer != null)
            {
                if (Observer.TryGetLatestObservation(out var latest))
                {
                    snapshot.Healthy = (int)latest.HealthyCitizens;
                    snapshot.Sick = (int)latest.SickCitizens;
                    snapshot.Exposed = (int)latest.ExposedCitizens;
                    snapshot.Recovered = (int)latest.RecoveredCitizens;
                    snapshot.Dead = (int)latest.DeadCitizens;
                    snapshot.LocationHome    = (int)latest.CitizensAtHome;
                    snapshot.LocationWork    = (int)latest.CitizensAtWork;
                    snapshot.LocationVisit   = (int)latest.CitizensVisiting;
                    snapshot.LocationTransit = (int)latest.CitizensInTransit;
                    snapshot.LocationMoving  = (int)latest.CitizensOnFoot;
                }

                if (Observer.TryGetLatestObservation(out latest) && Observer.TryGetPreviousObservation(out var previous))
                {
                    snapshot.DeltaSick = (int)latest.SickCitizens - (int)previous.SickCitizens;
                    snapshot.DeltaRecovered = (int)latest.RecoveredCitizens - (int)previous.RecoveredCitizens;
                    snapshot.DeltaDead = (int)latest.DeadCitizens - (int)previous.DeadCitizens;
                }
            }

            snapshot.TrackedPopulation = Math.Max(0, snapshot.Healthy + snapshot.Exposed + snapshot.Sick + snapshot.Recovered + snapshot.Dead);
        }

        internal int GetPandemicDayNumber()
        {
            if (!hasStartedAtLeastOnce || lifecycleState == PandemicLifecycleState.Dormant || pandemicRunStartedAt == default(DateTime))
            {
                return 0;
            }

            DateTime referenceTime = GetPandemicReferenceTime();
            if (referenceTime <= pandemicRunStartedAt)
            {
                return 1;
            }

            return Math.Max(1, (int)Math.Floor((referenceTime - pandemicRunStartedAt).TotalDays) + 1);
        }

        internal string GetTimeBarPandemicStatusText()
        {
            if (!hasStartedAtLeastOnce || lifecycleState == PandemicLifecycleState.Dormant)
            {
                return string.Empty;
            }

            int pandemicDay = GetPandemicDayNumber();
            if (lifecycleState == PandemicLifecycleState.Finished)
            {
                return "Finished \u00b7 " + pandemicDay.ToString(CultureInfo.InvariantCulture) + "d";
            }

            return "Pandemic day " + pandemicDay.ToString(CultureInfo.InvariantCulture);
        }

        private DateTime GetPandemicReferenceTime()
        {
            if (lifecycleState == PandemicLifecycleState.Finished && pandemicRunFinishedAt != default(DateTime))
            {
                return pandemicRunFinishedAt;
            }

            return currentDateTime != default(DateTime) ? currentDateTime
                : simulation != null ? simulation.m_ThreadingWrapper.simulationTime : default(DateTime);
        }

        private void PopulateHealthcareUsageSnapshot(PandemicLiveSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            DateTime referenceTime = lifecycleState == PandemicLifecycleState.Finished
                ? GetPandemicReferenceTime()
                : snapshot.SimulationTime != default(DateTime)
                    ? snapshot.SimulationTime
                    : GetPandemicReferenceTime();

            PandemicHealthcareUsageSample currentSample = GetCurrentHealthcareUsageSample(referenceTime);
            snapshot.HospitalUsagePercent = currentSample.HospitalUsagePercent;
            snapshot.AmbulanceUsagePercent = currentSample.AmbulanceUsagePercent;

            PandemicHealthcareUsageSample previousSample = GetHealthcareUsageSampleForDelta(referenceTime);
            snapshot.HospitalUsageDeltaPercent = currentSample.HospitalUsagePercent - previousSample.HospitalUsagePercent;
            snapshot.AmbulanceUsageDeltaPercent = currentSample.AmbulanceUsagePercent - previousSample.AmbulanceUsagePercent;
        }

        private PandemicHealthcareUsageSample GetCurrentHealthcareUsageSample(DateTime referenceTime)
        {
            if (healthcareUsageSamples.Count > 0)
            {
                PandemicHealthcareUsageSample latestSample = healthcareUsageSamples[healthcareUsageSamples.Count - 1];
                if (latestSample != null)
                {
                    return latestSample;
                }
            }

            return CalculateHealthcareUsageSample(referenceTime, CitizenMgr?.GetCitizensArray());
        }

        private PandemicHealthcareUsageSample GetHealthcareUsageSampleForDelta(DateTime referenceTime)
        {
            if (healthcareUsageSamples.Count == 0)
            {
                return GetCurrentHealthcareUsageSample(referenceTime);
            }

            DateTime targetTime = referenceTime.AddDays(-1);
            PandemicHealthcareUsageSample bestSample = healthcareUsageSamples[0];
            for (int i = healthcareUsageSamples.Count - 1; i >= 0; i--)
            {
                PandemicHealthcareUsageSample sample = healthcareUsageSamples[i];
                if (sample.SimulationTime <= targetTime)
                {
                    return sample;
                }

                bestSample = sample;
            }

            return bestSample ?? GetCurrentHealthcareUsageSample(referenceTime);
        }

        private void CaptureHealthcareUsageSample(DateTime simulationTime, Citizen[] citizens)
        {
            PandemicHealthcareUsageSample sample = CalculateHealthcareUsageSample(simulationTime, citizens);
            healthcareUsageSamples.Add(sample);
        }

        private long previousRecordedContactTotal;
        internal int GetPopulationForStorageEstimate()
        {
            Citizen[] citizens = CitizenMgr?.GetCitizensArray();
            if (citizens == null || CitizenProxy == null) return 0;
            int count = 0;
            for (int i = 1; i < citizens.Length; i++) if (!CitizenProxy.IsEmpty(ref citizens[i])) count++;
            return count;
        }
        private long lastStepContactEvents;
        internal string GetDiagnosticsSummary() => "Tier: " + performanceTier + "; tracked population: " + diseaseStateEngine.TrackedPopulationCount + "; physical contact events/last recorded step: " + lastStepContactEvents;

        private void RecordScientificState(DateTime simulationTime)
        {
            long currentContactTotal = ContactManager.Instance.GetTotalRecordedContacts();
            lastStepContactEvents = Math.Max(0L, currentContactTotal - previousRecordedContactTotal);
            previousRecordedContactTotal = currentContactTotal;
            using (PandemicProfiler.Measure("ScientificStateMetrics")) RecordScientificStateCore(simulationTime);
        }

        private readonly Dictionary<PandemicLockdownFamily, bool> recordedFamilyClosures = new Dictionary<PandemicLockdownFamily, bool>();

        private void RecordScientificStateCore(DateTime simulationTime)
        {
            foreach (var family in LockdownFamilies)
            {
                bool closed = !IsLockdownFamilyOpen(family);
                if (!recordedFamilyClosures.TryGetValue(family, out bool previous) || previous != closed)
                {
                    experimentRecorder.RecordIntervention(new PandemicInterventionEvent { SimulationTime = simulationTime, InterventionType = PandemicInterventionType.Lockdown, Action = closed ? "Close" : "Reopen", Reason = "EffectiveFamilyState", Context = family.ToString() });
                    recordedFamilyClosures[family] = closed;
                }
            }
            EpidemicMetricsSnapshot metrics = metricsEngine.Capture(diseaseStateEngine, simulationTime, TestManager.Instance.Engine);
            experimentRecorder.RecordState(
                simulationTime,
                metrics.Counts,
                metrics.Symptomatic,
                metrics.InitialSeedCount,
                metrics.SecondaryTransmissionCount,
                metrics.HospitalizationsTotal,
                QuarantineManager.Instance.GetIsolatedCount(simulationTime),
                QuarantineManager.Instance.GetContactQuarantinedCount(simulationTime),
                metrics,
                QuarantineManager.Instance.GetFollowingCount(simulationTime, true),
                QuarantineManager.Instance.GetFollowingCount(simulationTime, false));
        }

        private PandemicHealthcareUsageSample CalculateHealthcareUsageSample(DateTime simulationTime, Citizen[] citizens)
        {
            using (PandemicProfiler.Measure("HealthcareSampling")) return CalculateHealthcareUsageSampleCore(simulationTime, citizens);
        }

        private PandemicHealthcareUsageSample CalculateHealthcareUsageSampleCore(DateTime simulationTime, Citizen[] citizens)
        {
            int hospitalCapacity = 0;
            int ambulanceCapacity = 0;
            int currentHospitalUsage = 0;
            int currentAmbulanceUsage = 0;

            BuildingManager buildingManager = BuildingManager.instance;
            if (buildingManager != null)
            {
                Building[] buildings = buildingManager.m_buildings.m_buffer;
                for (ushort buildingId = 1; buildingId < buildings.Length; buildingId++)
                {
                    ref Building building = ref buildings[buildingId];
                    if ((building.m_flags & (Building.Flags.Created | Building.Flags.Deleted)) != Building.Flags.Created || building.Info == null)
                    {
                        continue;
                    }

                    if (building.Info.GetService() != ItemClass.Service.HealthCare)
                    {
                        continue;
                    }

                    if (building.Info.m_buildingAI is HospitalAI hospitalAI)
                    {
                        hospitalCapacity += Math.Max(0, hospitalAI.PatientCapacity);
                        ambulanceCapacity += Math.Max(0, hospitalAI.AmbulanceCount);
                    }
                }
            }

            if (citizens != null && initialPopulation.Count > 0)
            {
                for (int i = 0; i < initialPopulation.Count; i++)
                {
                    uint citizenId = initialPopulation[i];
                    uint realId = retrieveID(citizenId);
                    if (realId >= citizens.Length)
                    {
                        continue;
                    }

                    ref Citizen citizen = ref citizens[realId];
                    if (CitizenProxy.IsEmpty(ref citizen) || CitizenProxy.IsDead(ref citizen))
                    {
                        continue;
                    }

                    ushort currentBuilding = CitizenProxy.GetCurrentBuilding(ref citizen);
                    ushort visitBuilding = CitizenProxy.GetVisitBuilding(ref citizen);
                    ushort workBuilding = CitizenProxy.GetWorkBuilding(ref citizen);
                    bool visitingHealthcare = visitBuilding != 0 && BuildingMgr.GetBuildingService(visitBuilding) == ItemClass.Service.HealthCare;
                    bool insideHealthcareAsPatient = currentBuilding != 0
                        && BuildingMgr.GetBuildingService(currentBuilding) == ItemClass.Service.HealthCare
                        && currentBuilding != workBuilding;

                    if (visitingHealthcare || insideHealthcareAsPatient)
                    {
                        currentHospitalUsage++;
                    }
                }
            }

            VehicleManager vehicleManager = VehicleManager.instance;
            if (vehicleManager != null)
            {
                Vehicle[] vehicles = vehicleManager.m_vehicles.m_buffer;
                for (ushort vehicleId = 1; vehicleId < vehicles.Length; vehicleId++)
                {
                    ref Vehicle vehicle = ref vehicles[vehicleId];
                    if ((vehicle.m_flags & (Vehicle.Flags.Created | Vehicle.Flags.Deleted)) != Vehicle.Flags.Created || vehicle.Info == null)
                    {
                        continue;
                    }

                    VehicleAI vehicleAI = vehicle.Info.m_vehicleAI;
                    if (vehicleAI is AmbulanceAI || vehicleAI is AmbulanceCopterAI)
                    {
                        currentAmbulanceUsage++;
                    }
                }
            }

            return new PandemicHealthcareUsageSample
            {
                SimulationTime = simulationTime,
                HospitalUsagePercent = hospitalCapacity > 0 ? Mathf.Clamp((currentHospitalUsage * 100f) / hospitalCapacity, 0f, 100f) : 0f,
                AmbulanceUsagePercent = ambulanceCapacity > 0 ? Mathf.Clamp((currentAmbulanceUsage * 100f) / ambulanceCapacity, 0f, 100f) : 0f,
            };
        }

        private bool ShouldRefreshAnalyticsSnapshot(float now, int totalInfections)
        {
            return liveSnapshotCache == null
                || now >= nextAnalyticsSnapshotRefreshTime
                || cachedAnalyticsInfectionCount != totalInfections;
        }

        private bool ShouldRefreshSuperspreaderSnapshot(float now, int totalInfections)
        {
            return liveSnapshotCache == null
                || now >= nextSuperspreaderSnapshotRefreshTime
                || cachedSuperspreaderInfectionCount != totalInfections;
        }

        private void RefreshChartSnapshot(PandemicLiveSnapshot snapshot)
        {
            if (snapshot == null) return;
            var points = experimentRecorder.StateTimeSeries;
            if (snapshot.ChartPoints.Count > points.Count)
            {
                snapshot.ChartPoints.Clear();
                chartVersion++;
            }
            while (snapshot.ChartPoints.Count < points.Count)
            {
                var point = points[snapshot.ChartPoints.Count];
                snapshot.ChartPoints.Add(new PandemicChartPointSnapshot
                {
                    SimulationTime = point.SimulationTime, InfectedCount = point.Infectious,
                    Exposed = point.Exposed, DetectedCases = point.DetectedActiveCases,
                    Recovered = point.Recovered, Deaths = point.Dead, NewInfections = point.NewExposures,
                });
                chartVersion++;
            }
            cachedChartObservationCount = points.Count;
        }

        private void RefreshPolicyMarkerSnapshot(PandemicLiveSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            if (cachedPolicyMarkerCount > policyTimeline.Count || snapshot.PolicyMarkers.Count > policyTimeline.Count)
            {
                snapshot.PolicyMarkers.Clear();
                cachedPolicyMarkerCount = 0;
                chartVersion++;
            }

            while (snapshot.PolicyMarkers.Count < policyTimeline.Count)
            {
                PandemicPolicyTimelineEntry marker = policyTimeline[snapshot.PolicyMarkers.Count];
                snapshot.PolicyMarkers.Add(new PandemicPolicyMarkerSnapshot
                {
                    SimulationTime = marker.SimulationTime,
                    Type = marker.Type,
                    Enabled = marker.Enabled,
                    ShortLabel = GetPolicyMarkerShortLabel(marker.Type, marker.Enabled),
                });
                chartVersion++;
            }

            cachedPolicyMarkerCount = policyTimeline.Count;
        }

        private void PopulateLiveSnapshotBreakdown(PandemicLiveSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var districtSnapshots = CreateDistrictSnapshots();
            int[] ageGroupCounts = new int[Enum.GetValues(typeof(Citizen.AgeGroup)).Length];
            int[] ageGroupPopulationCounts = new int[Enum.GetValues(typeof(Citizen.AgeGroup)).Length];
            int totalInfected = 0;

            foreach (KeyValuePair<uint, Citizen.AgeGroup> trackedCitizen in trackedAgeGroupByCitizen)
            {
                DiseaseState state = diseaseStateEngine.GetState(trackedCitizen.Key, currentDateTime);
                if (state == DiseaseState.Removed)
                {
                    continue;
                }

                int ageGroupIndex = (int)trackedCitizen.Value;
                if (ageGroupIndex >= 0 && ageGroupIndex < ageGroupPopulationCounts.Length)
                {
                    ageGroupPopulationCounts[ageGroupIndex]++;
                }

                if (diseaseStateEngine.TryGetCourse(trackedCitizen.Key, out DiseaseCourse ignoredCourse))
                {
                    if (ageGroupIndex >= 0 && ageGroupIndex < ageGroupCounts.Length)
                    {
                        ageGroupCounts[ageGroupIndex]++;
                    }

                    totalInfected++;
                }
            }

            if (CitizenMgr != null && CitizenProxy != null && BuildingMgr != null)
            {
                Citizen[] citizens = CitizenMgr.GetCitizensArray();
                if (citizens != null && citizens.Length > 0)
                {
                    foreach (uint citizenId in initialPopulation)
                    {
                        uint resolvedCitizenId = retrieveID(citizenId);
                        if (resolvedCitizenId >= citizens.Length)
                        {
                            continue;
                        }

                        ref Citizen citizen = ref citizens[resolvedCitizenId];
                        DiseaseState citizenState = diseaseStateEngine.GetState(citizenId, currentDateTime);
                        bool infected = citizenState == DiseaseState.Exposed || citizenState == DiseaseState.Infectious || citizenState == DiseaseState.PostInfectiousIll;
                        if (CitizenProxy.IsEmpty(ref citizen) || CitizenProxy.IsDead(ref citizen))
                        {
                            continue;
                        }

                        ushort homeBuilding = CitizenProxy.GetHomeBuilding(ref citizen);
                        if (homeBuilding == 0 || BuildingMgr.GetBuildingService(homeBuilding) != ItemClass.Service.Residential)
                        {
                            continue;
                        }

                        byte districtId = GetDistrictId(homeBuilding);
                        if (districtId == 0 || !districtSnapshots.TryGetValue(districtId, out PandemicDistrictSnapshot districtSnapshot))
                        {
                            continue;
                        }

                        districtSnapshot.ResidentCount++;
                        if (TestManager.Instance.IsTestedPositive(citizenId, currentDateTime)) districtSnapshot.DetectedResidents++;
                        DateTime intervalEnd = experimentRecorder.LatestState?.SimulationTime ?? currentDateTime;
                        if (diseaseStateEngine.TryGetCourse(citizenId, out DiseaseCourse course)
                            && course.ExposureKind == DiseaseExposureKind.SecondaryTransmission
                            && course.ExposureTime > intervalEnd.AddMinutes(-Config.EpidemicStepMinutes) && course.ExposureTime <= intervalEnd)
                            districtSnapshot.NewInfections++;
                        if (infected)
                        {
                            districtSnapshot.InfectedResidents++;
                        }
                    }
                }
            }

            foreach (Citizen.AgeGroup ageGroup in LiveSnapshotAgeGroups)
            {
                int count = ageGroupCounts[(int)ageGroup];
                snapshot.AgeGroups.Add(new PandemicAgeGroupSnapshot
                {
                    Label = GetAgeGroupLabel(ageGroup),
                    InfectedCount = count,
                    PopulationCount = ageGroupPopulationCounts[(int)ageGroup],
                    ShareOfInfectionsPercent = totalInfected > 0 ? (count * 100f) / totalInfected : 0f,
                    InfectionPrevalenceWithinAgeGroupPercent = ageGroupPopulationCounts[(int)ageGroup] > 0
                        ? count * 100f / ageGroupPopulationCounts[(int)ageGroup]
                        : 0f,
                    InfectedPercent = totalInfected > 0 ? (count * 100f) / totalInfected : 0f,
                });
            }

            foreach (PandemicDistrictSnapshot districtSnapshot in districtSnapshots.Values)
            {
                districtSnapshot.DetectedPrevalencePercent = districtSnapshot.ResidentCount > 0 ? districtSnapshot.DetectedResidents * 100f / districtSnapshot.ResidentCount : 0f;
                districtSnapshot.IncidencePer100000 = districtSnapshot.ResidentCount > 0 ? districtSnapshot.NewInfections * 100000f / districtSnapshot.ResidentCount : 0f;
                districtSnapshot.SecondaryTransmissions = experimentRecorder.GetDistrictTransmissions((byte)districtSnapshot.DistrictId);
                districtSnapshot.InfectedPercent = districtSnapshot.ResidentCount > 0
                    ? (districtSnapshot.InfectedResidents * 100f) / districtSnapshot.ResidentCount
                    : 0f;
            }

            foreach (PandemicDistrictSnapshot districtSnapshot in districtSnapshots.Values
                .OrderByDescending(d => d.InfectedPercent)
                .ThenByDescending(d => d.InfectedResidents)
                .ThenBy(d => d.DistrictName, StringComparer.OrdinalIgnoreCase))
            {
                snapshot.Districts.Add(districtSnapshot);
            }
        }

        private void PopulateOriginSnapshots(PandemicLiveSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            IDictionary<PandemicInfectionOriginCategory, int> originCounts = Observer?.GetOriginCounts()
                ?? new Dictionary<PandemicInfectionOriginCategory, int>();
            int total = originCounts.Values.Sum();

            foreach (PandemicInfectionOriginCategory category in Enum.GetValues(typeof(PandemicInfectionOriginCategory)))
            {
                int count = originCounts.ContainsKey(category) ? originCounts[category] : 0;
                snapshot.Origins.Add(new PandemicOriginSnapshot
                {
                    Label = PandemicTaxonomy.GetOriginCategoryLabel(category),
                    Count = count,
                    Percent = total > 0 ? (count * 100f) / total : 0f,
                });
            }
        }

        private void PopulateSuperspreaders(PandemicLiveSnapshot snapshot)
        {
            if (snapshot == null || Observer == null)
            {
                return;
            }

            Dictionary<uint, List<Infection>> infections = Observer.GeneralObservation.Infections;
            if (infections == null)
            {
                return;
            }

            foreach (KeyValuePair<uint, List<Infection>> kvp in infections
                .Where(p => p.Key != 0 && p.Value != null && p.Value.Count > 0)
                .OrderByDescending(p => p.Value.Count)
                .ThenBy(p => p.Key)
                .Take(5))
            {
                Vector3 focusPosition;
                ushort citizenInstanceId;
                bool canFocus = TryGetCitizenFocusTarget(kvp.Key, out citizenInstanceId, out focusPosition);
                string citizenLabel = CitizenManager.instance != null ? CitizenManager.instance.GetCitizenName(kvp.Key) : null;
                if (string.IsNullOrEmpty(citizenLabel))
                {
                    citizenLabel = "Citizen #" + kvp.Key;
                }

                snapshot.TopSpreaders.Add(new PandemicSuperspreaderCitizenSnapshot
                {
                    CitizenId = kvp.Key,
                    CitizenInstanceId = citizenInstanceId,
                    Label = citizenLabel,
                    InfectionCount = kvp.Value.Count,
                    IsSuperspreader = kvp.Value.Count >= Config.SuperspreaderCitizenThreshold,
                    CanFocus = canFocus,
                    FocusPosition = focusPosition,
                });
            }

            var locations = new Dictionary<string, PandemicLocationAggregate>();
            foreach (List<Infection> infectionList in infections.Values)
            {
                if (infectionList == null)
                {
                    continue;
                }

                foreach (Infection infection in infectionList)
                {
                    string key = GetLocationAggregationKey(infection);
                    if (!locations.TryGetValue(key, out PandemicLocationAggregate aggregate))
                    {
                        aggregate = CreateLocationAggregate(infection);
                        locations.Add(key, aggregate);
                    }

                    aggregate.Count++;
                }
            }

            foreach (PandemicLocationAggregate location in locations.Values
                .OrderByDescending(l => l.Count)
                .ThenBy(l => l.Label, StringComparer.OrdinalIgnoreCase)
                .Take(5))
            {
                snapshot.TopOriginLocations.Add(new PandemicSuperspreaderLocationSnapshot
                {
                    Label = location.Label,
                    InfectionCount = location.Count,
                    IsSuperspreader = location.Count >= Config.SuperspreaderLocationThreshold,
                    BuildingId = location.BuildingId,
                    CanFocus = location.BuildingId != 0 || location.Position != Vector3.zero,
                    FocusPosition = location.Position,
                });
            }
        }

        private void PopulateLockdownFamilies(PandemicLiveSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            foreach (PandemicLockdownFamily family in LockdownFamilies)
            {
                snapshot.LockdownFamilies.Add(new PandemicLockdownFamilySnapshot
                {
                    Family = family,
                    Label = PandemicTaxonomy.GetLockdownFamilyLabel(family),
                    ManualClosed = IsFamilyClosedManually(family),
                    AutoCloseThresholdPercent = GetFamilyAutoThreshold(family),
                    AutoReopenThresholdPercent = GetFamilyReopenThreshold(family),
                    MinimumClosureDurationDays = GetFamilyMinimumClosureDurationDays(family),
                    CooldownDurationDays = GetFamilyCooldownDurationDays(family),
                    ThresholdMetric = Config.AutomaticPolicyTriggerMetric.ToString(),
                    CurrentInfectedPercent = GetCurrentFamilyInfectedPercent(family),
                    IsClosed = !IsLockdownFamilyOpen(family),
                });
            }
        }

        private void PopulateChart(PandemicLiveSnapshot snapshot)
        {
            if (snapshot == null || Observer == null)
            {
                return;
            }

            IList<PandemicObservation> observations = Observer.GetObservations();
            if (observations == null)
            {
                return;
            }

            foreach (PandemicObservation observation in observations.OrderBy(o => o.SimulationTime))
            {
                snapshot.ChartPoints.Add(new PandemicChartPointSnapshot
                {
                    SimulationTime = observation.SimulationTime,
                    InfectedCount = (int)observation.SickCitizens,
                });
            }
        }

        private void PopulatePolicyMarkers(PandemicLiveSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            foreach (PandemicPolicyTimelineEntry marker in policyTimeline.OrderBy(m => m.SimulationTime))
            {
                snapshot.PolicyMarkers.Add(new PandemicPolicyMarkerSnapshot
                {
                    SimulationTime = marker.SimulationTime,
                    Type = marker.Type,
                    Enabled = marker.Enabled,
                    ShortLabel = GetPolicyMarkerShortLabel(marker.Type, marker.Enabled),
                });
            }
        }

        private Dictionary<byte, PandemicDistrictSnapshot> CreateDistrictSnapshots()
        {
            var result = new Dictionary<byte, PandemicDistrictSnapshot>();
            DistrictManager districtManager = DistrictManager.instance;
            if (districtManager == null)
            {
                return result;
            }

            District[] districts = districtManager.m_districts.m_buffer;
            if (districts == null || districts.Length == 0)
            {
                return result;
            }

            for (int districtId = 1; districtId < districts.Length; districtId++)
            {
                if ((districts[districtId].m_flags & District.Flags.Created) == 0)
                {
                    continue;
                }

                result[(byte)districtId] = new PandemicDistrictSnapshot
                {
                    DistrictId = districtId,
                    DistrictName = districtManager.GetDistrictName(districtId),
                };
            }

            return result;
        }

        private byte GetDistrictId(ushort homeBuilding)
        {
            if (homeBuilding == 0 || DistrictManager.instance == null)
            {
                return 0;
            }

            return DistrictManager.instance.GetDistrict(BuildingMgr.GetBuildingPosition(homeBuilding));
        }

        private PandemicPopulationCategory ClassifyPopulation(ref Citizen citizen)
        {
            return populationLifecycleEngine.ClassifyCurrent(
                CitizenProxy.HasFlags(ref citizen, Citizen.Flags.Tourist),
                CitizenProxy.HasFlags(ref citizen, Citizen.Flags.MovingIn),
                CitizenProxy.HasFlags(ref citizen, Citizen.Flags.DummyTraffic),
                CitizenProxy.GetHomeBuilding(ref citizen) != 0);
        }

        private static string GetAgeGroupLabel(Citizen.AgeGroup ageGroup)
        {
            switch (ageGroup)
            {
                case Citizen.AgeGroup.Child:
                    return "Child";
                case Citizen.AgeGroup.Teen:
                    return "Teen";
                case Citizen.AgeGroup.Young:
                    return "Young";
                case Citizen.AgeGroup.Adult:
                    return "Adult";
                case Citizen.AgeGroup.Senior:
                    return "Senior";
                default:
                    return ageGroup.ToString();
            }
        }

        private string GetLocationAggregationKey(Infection infection)
        {
            if (infection == null)
            {
                return "unknown";
            }

            if (infection.BuildingId != 0)
            {
                return "b:" + infection.BuildingId;
            }

            int x = Mathf.RoundToInt(infection.position.x / 32f);
            int z = Mathf.RoundToInt(infection.position.z / 32f);
            return infection.OriginCategory + ":" + x + ":" + z;
        }

        private PandemicLocationAggregate CreateLocationAggregate(Infection infection)
        {
            var aggregate = new PandemicLocationAggregate();
            if (infection == null)
            {
                aggregate.Label = "Unknown";
                return aggregate;
            }

            aggregate.BuildingId = infection.BuildingId;
            aggregate.Position = infection.position;
            if (infection.BuildingId != 0)
            {
                string buildingName = BuildingMgr?.GetBuildingName(infection.BuildingId);
                if (string.IsNullOrEmpty(buildingName))
                {
                    buildingName = PandemicTaxonomy.GetOriginCategoryLabel(infection.OriginCategory) + " #" + infection.BuildingId;
                }

                aggregate.Label = buildingName;
                if (aggregate.Position == Vector3.zero && BuildingMgr != null)
                {
                    aggregate.Position = BuildingMgr.GetBuildingPosition(infection.BuildingId);
                }

                return aggregate;
            }

            aggregate.Label = PandemicTaxonomy.GetOriginCategoryLabel(infection.OriginCategory)
                + " @ "
                + infection.position.x.ToString("0")
                + ", "
                + infection.position.z.ToString("0");
            return aggregate;
        }

        private static string GetPolicyMarkerShortLabel(PandemicPolicyMarkerType type, bool enabled)
        {
            switch (type)
            {
                case PandemicPolicyMarkerType.Masks:
                    return enabled ? "M+" : "M-";
                case PandemicPolicyMarkerType.Lockdown:
                    return enabled ? "L+" : "L-";
                case PandemicPolicyMarkerType.Testing: return enabled ? "T+" : "T-";
                case PandemicPolicyMarkerType.Isolation: return enabled ? "I+" : "I-";
                case PandemicPolicyMarkerType.Quarantine: return enabled ? "Q+" : "Q-";
                case PandemicPolicyMarkerType.Tracing: return enabled ? "C+" : "C-";
                case PandemicPolicyMarkerType.PublicTransport: return enabled ? "PT-" : "PT+";
                default:
                    return enabled ? "+" : "-";
            }
        }

        private bool TryGetCitizenFocusTarget(uint citizenId, out ushort citizenInstanceId, out Vector3 position)
        {
            citizenInstanceId = 0;
            position = Vector3.zero;
            if (CitizenMgr == null || CitizenProxy == null)
            {
                return false;
            }

            Citizen[] citizens = CitizenMgr.GetCitizensArray();
            if (citizens == null)
            {
                return false;
            }

            uint realId = retrieveID(citizenId);
            if (realId >= citizens.Length)
            {
                return false;
            }

            ref Citizen citizen = ref citizens[realId];
            if (CitizenProxy.IsEmpty(ref citizen) || CitizenProxy.IsDead(ref citizen))
            {
                return false;
            }

            citizenInstanceId = CitizenProxy.GetInstance(ref citizen);
            if (citizenInstanceId != 0)
            {
                position = CitizenMgr.GetCitizenPosition(citizenInstanceId);
                return position != Vector3.zero;
            }

            ushort currentBuilding = CitizenProxy.GetCurrentBuilding(ref citizen);
            if (currentBuilding != 0)
            {
                position = BuildingMgr.GetBuildingPosition(currentBuilding);
                return position != Vector3.zero;
            }

            ushort homeBuilding = CitizenProxy.GetHomeBuilding(ref citizen);
            if (homeBuilding != 0)
            {
                position = BuildingMgr.GetBuildingPosition(homeBuilding);
                return position != Vector3.zero;
            }

            return false;
        }

        private float GetCurrentFamilyInfectedPercent(PandemicLockdownFamily family)
        {
            return (float)currentLockdownMetricPercent;
        }

        private void UpdateLockdownPolicyStates(DateTime simulationTime)
        {
            DiseaseStateCounts counts = diseaseStateEngine.GetCounts(simulationTime);
            int activeCases = counts.Exposed + counts.Infectious + counts.PostInfectiousIll;
            int trackedPopulation = diseaseStateEngine.TrackedPopulationCount;
            currentLockdownMetricPercent = trackedPopulation > 0
                ? (activeCases * 100d) / trackedPopulation
                : 0d;

            TestingEngine tests = TestManager.Instance.Engine;
            switch (Config.AutomaticPolicyTriggerMetric)
            {
                case PolicyTriggerMetric.IdealizedTruePrevalence:
                    break;
                case PolicyTriggerMetric.DetectedPrevalence:
                    currentLockdownMetricPercent = trackedPopulation > 0 ? (tests?.GetCurrentPositiveCount(simulationTime) ?? 0) * 100d / trackedPopulation : 0d;
                    break;
                case PolicyTriggerMetric.TestPositivityRate:
                    // No available tests means there is no signal to act on.
                    if (tests == null || tests.AvailableTestsTotal == 0) return;
                    currentLockdownMetricPercent = tests.PositiveTestsTotal * 100d / tests.AvailableTestsTotal;
                    break;
                case PolicyTriggerMetric.HospitalOccupancy:
                    if (healthcareUsageSamples.Count == 0) return;
                    currentLockdownMetricPercent = healthcareUsageSamples[healthcareUsageSamples.Count - 1].HospitalUsagePercent;
                    break;
                default:
                    throw new InvalidOperationException("Unsupported automatic policy trigger metric.");
            }

            if (!QuarantineManager.Instance.InLockDown)
            {
                lockdownEngine.Deactivate(simulationTime, currentLockdownMetricPercent);
                return;
            }

            foreach (PandemicLockdownFamily family in LockdownFamilies)
            {
                if (PandemicTaxonomy.IsProtectedFamily(family))
                {
                    continue;
                }

                bool wasClosed = lockdownEngine.IsClosed(family);
                bool isClosed = lockdownEngine.Evaluate(
                    family,
                    GetFamilyLockdownPolicy(family),
                    currentLockdownMetricPercent,
                    simulationTime);
                if (isClosed != wasClosed)
                {
                    experimentRecorder.RecordIntervention(new PandemicInterventionEvent
                    {
                        SimulationTime = simulationTime,
                        InterventionType = PandemicInterventionType.Lockdown,
                        Action = isClosed ? "Close" : "Reopen",
                        Reason = "AutomaticThreshold",
                        Context = PandemicTaxonomy.GetLockdownFamilyLabel(family),
                        TriggerMetric = Config.AutomaticPolicyTriggerMetric.ToString(),
                        TriggerValue = currentLockdownMetricPercent,
                        TriggerThreshold = isClosed ? GetFamilyAutoThreshold(family) : GetFamilyReopenThreshold(family),
                    });
                }
            }
        }

        private PandemicLockdownPolicy GetFamilyLockdownPolicy(PandemicLockdownFamily family)
        {
            return new PandemicLockdownPolicy(
                GetFamilyAutoThreshold(family),
                GetFamilyReopenThreshold(family),
                TimeSpan.FromDays(GetFamilyMinimumClosureDurationDays(family)),
                TimeSpan.FromDays(GetFamilyCooldownDurationDays(family)));
        }

        private bool IsLockdownFamilyOpen(PandemicLockdownFamily family)
        {
            if (PandemicTaxonomy.IsProtectedFamily(family))
            {
                return true;
            }

            if (!QuarantineManager.Instance.InLockDown)
            {
                return true;
            }

            if (IsFamilyClosedManually(family))
            {
                return false;
            }

            return !lockdownEngine.IsClosed(family);
        }

        public bool IsBuildingOpenForPandemic(ushort buildingId)
        {
            if (buildingId == 0 || BuildingMgr == null)
            {
                return true;
            }

            ItemClass.Service service = BuildingMgr.GetBuildingService(buildingId);
            ItemClass.SubService subService = BuildingMgr.GetBuildingSubService(buildingId);
            PandemicLockdownFamily family = PandemicTaxonomy.GetBuildingFamily(service, subService);
            return IsLockdownFamilyOpen(family);
        }

        private byte[] districtHeatmapIds;
        private float nextDistrictGeometryRefresh;
        private readonly float[] districtHeatmapValues = new float[128];

        private int PopulateDistrictHeatmap(float[] target, int resolution, PandemicXRayMetric metric)
        {
            if (DistrictManager.instance == null) return 0;
            if (districtHeatmapIds == null || districtHeatmapIds.Length != target.Length || Time.unscaledTime >= nextDistrictGeometryRefresh)
            {
                districtHeatmapIds = new byte[target.Length];
                for (int z = 0; z < resolution; z++)
                    for (int x = 0; x < resolution; x++)
                        districtHeatmapIds[z * resolution + x] = DistrictManager.instance.GetDistrict(new Vector3(((x + 0.5f) / resolution - 0.5f) * 17280f, 0, ((z + 0.5f) / resolution - 0.5f) * 17280f));
                nextDistrictGeometryRefresh = Time.unscaledTime + 30f;
            }
            Array.Clear(districtHeatmapValues, 0, districtHeatmapValues.Length);
            foreach (PandemicDistrictSnapshot district in GetLiveSnapshot().Districts)
                if (district.DistrictId > 0 && district.DistrictId < districtHeatmapValues.Length)
                    districtHeatmapValues[district.DistrictId] = PandemicDistrictMetricProjection.Value(district, metric);
            int nonzero = 0;
            for (int i = 0; i < target.Length; i++)
            {
                target[i] = districtHeatmapValues[districtHeatmapIds[i]];
                if (target[i] > 0) nonzero++;
            }
            return nonzero;
        }

        internal int PopulateHeatmapGrid(float[] target, PandemicXRayMetric metric, PandemicXRayLocationMode locationMode)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            Array.Clear(target, 0, target.Length);
            if (!xRayEnabled || !IsVisualizationDataAvailable())
            {
                return 0;
            }

            int resolution = (int)Math.Sqrt(target.Length);
            if (resolution * resolution != target.Length)
            {
                return 0;
            }

            if (metric >= PandemicXRayMetric.DistrictActiveInfections) return PopulateDistrictHeatmap(target, resolution, metric);
            const float mapHalfSize = 8640f;
            if (metric == PandemicXRayMetric.TransmissionHotspots)
            {
                var events = experimentRecorder.TransmissionEvents;
                if (transmissionHotspotGrid == null || transmissionHotspotGrid.Length != target.Length || transmissionHotspotCount > events.Count)
                {
                    transmissionHotspotGrid = new float[target.Length];
                    transmissionHotspotCount = 0;
                }
                while (transmissionHotspotCount < events.Count)
                {
                    var item = events[transmissionHotspotCount++];
                    if (!item.HasPosition) continue;
                    int x = Mathf.Clamp((int)(((item.PositionX + mapHalfSize) / (2f * mapHalfSize)) * resolution), 0, resolution - 1);
                    int z = Mathf.Clamp((int)(((item.PositionZ + mapHalfSize) / (2f * mapHalfSize)) * resolution), 0, resolution - 1);
                    transmissionHotspotGrid[z * resolution + x]++;
                }
                Array.Copy(transmissionHotspotGrid, target, target.Length);
                return events.Count;
            }
            Citizen[] citizens = CitizenMgr.GetCitizensArray();
            if (citizens == null)
            {
                return 0;
            }

            int written = 0;
            switch (metric)
            {
                case PandemicXRayMetric.Recovered:
                    foreach (uint citizenId in initialPopulationRecovered)
                    {
                        uint realId = retrieveID(citizenId);
                        if (realId >= citizens.Length)
                        {
                            continue;
                        }

                        ref Citizen citizen = ref citizens[realId];
                        if (CitizenProxy.IsEmpty(ref citizen) || CitizenProxy.IsDead(ref citizen))
                        {
                            continue;
                        }

                        written += AddHeatmapPosition(target, resolution, mapHalfSize, GetHeatmapPosition(ref citizen, locationMode));
                    }

                    break;

                case PandemicXRayMetric.Dead:
                    foreach (PandemicDeathRecord deathRecord in deathRecords)
                    {
                        if (deathRecord == null)
                        {
                            continue;
                        }

                        written += AddHeatmapPosition(target, resolution, mapHalfSize, GetDeathHeatmapPosition(deathRecord, locationMode));
                    }

                    break;

                default:
                    foreach (uint citizenId in activeInfections.Keys)
                    {
                        if (metric == PandemicXRayMetric.Infected && diseaseStateEngine.GetState(citizenId, currentDateTime) != DiseaseState.Infectious) continue;
                        uint realId = retrieveID(citizenId);
                        if (realId >= citizens.Length)
                        {
                            continue;
                        }

                        ref Citizen citizen = ref citizens[realId];
                        if (CitizenProxy.IsEmpty(ref citizen) || CitizenProxy.IsDead(ref citizen))
                        {
                            continue;
                        }

                        written += AddHeatmapPosition(target, resolution, mapHalfSize, GetHeatmapPosition(ref citizen,
                            metric == PandemicXRayMetric.ResidentialInfectionClusters ? PandemicXRayLocationMode.HomeLocations : locationMode));
                    }

                    break;
            }

            return written;
        }

        private int AddHeatmapPosition(float[] target, int resolution, float mapHalfSize, Vector3 position)
        {
            if (position == Vector3.zero)
            {
                return 0;
            }

            int x = Mathf.Clamp((int)(((position.x + mapHalfSize) / (2f * mapHalfSize)) * resolution), 0, resolution - 1);
            int z = Mathf.Clamp((int)(((position.z + mapHalfSize) / (2f * mapHalfSize)) * resolution), 0, resolution - 1);
            target[(z * resolution) + x] += 1f;
            return 1;
        }

        private Vector3 GetDeathHeatmapPosition(PandemicDeathRecord deathRecord, PandemicXRayLocationMode locationMode)
        {
            if (deathRecord == null)
            {
                return Vector3.zero;
            }

            if (locationMode == PandemicXRayLocationMode.HomeLocations)
            {
                return deathRecord.HomeBuildingId != 0 ? BuildingMgr.GetBuildingPosition(deathRecord.HomeBuildingId) : Vector3.zero;
            }

            return deathRecord.DeathPosition;
        }

        private Vector3 GetHeatmapPosition(ref Citizen citizen, PandemicXRayLocationMode locationMode)
        {
            if (locationMode == PandemicXRayLocationMode.HomeLocations)
            {
                ushort homeBuilding = CitizenProxy.GetHomeBuilding(ref citizen);
                return homeBuilding != 0 ? BuildingMgr.GetBuildingPosition(homeBuilding) : Vector3.zero;
            }

            ushort instanceId = CitizenProxy.GetInstance(ref citizen);
            if (instanceId != 0)
            {
                return CitizenMgr.GetCitizenPosition(instanceId);
            }

            ushort currentBuilding = CitizenProxy.GetCurrentBuilding(ref citizen);
            if (currentBuilding != 0)
            {
                return BuildingMgr.GetBuildingPosition(currentBuilding);
            }

            ushort vehicleId = CitizenProxy.GetVehicle(ref citizen);
            if (vehicleId != 0 && VehicleManager.instance != null)
            {
                return VehicleManager.instance.m_vehicles.m_buffer[vehicleId].m_frame0.m_position;
            }

            ushort home = CitizenProxy.GetHomeBuilding(ref citizen);
            return home != 0 ? BuildingMgr.GetBuildingPosition(home) : Vector3.zero;
        }

        private bool IsFamilyClosedManually(PandemicLockdownFamily family)
        {
            if (Config == null)
            {
                return false;
            }

            switch (family)
            {
                case PandemicLockdownFamily.Education:
                    return Config.CloseEducationDuringLockdown;
                case PandemicLockdownFamily.PublicTransport:
                    return Config.ClosePublicTransportDuringLockdown;
                case PandemicLockdownFamily.Commercial:
                    return Config.CloseCommercialDuringLockdown;
                case PandemicLockdownFamily.LeisureTourismParks:
                    return Config.CloseLeisureTourismParksDuringLockdown;
                case PandemicLockdownFamily.Office:
                    return Config.CloseOfficeDuringLockdown;
                case PandemicLockdownFamily.IndustryPlayerIndustry:
                    return Config.CloseIndustryDuringLockdown;
                case PandemicLockdownFamily.GovernmentOtherPublic:
                    return Config.CloseGovernmentOtherPublicDuringLockdown;
                case PandemicLockdownFamily.EssentialServices:
                    return Config.CloseEssentialServicesDuringLockdown;
                default:
                    return false;
            }
        }

        private float GetFamilyAutoThreshold(PandemicLockdownFamily family)
        {
            if (Config == null)
            {
                return 0f;
            }

            switch (family)
            {
                case PandemicLockdownFamily.Education:
                    return Config.CloseEducationThresholdPercent;
                case PandemicLockdownFamily.PublicTransport:
                    return Config.ClosePublicTransportThresholdPercent;
                case PandemicLockdownFamily.Commercial:
                    return Config.CloseCommercialThresholdPercent;
                case PandemicLockdownFamily.LeisureTourismParks:
                    return Config.CloseLeisureTourismParksThresholdPercent;
                case PandemicLockdownFamily.Office:
                    return Config.CloseOfficeThresholdPercent;
                case PandemicLockdownFamily.IndustryPlayerIndustry:
                    return Config.CloseIndustryThresholdPercent;
                case PandemicLockdownFamily.GovernmentOtherPublic:
                    return Config.CloseGovernmentOtherPublicThresholdPercent;
                case PandemicLockdownFamily.EssentialServices:
                    return Config.CloseEssentialServicesThresholdPercent;
                default:
                    return 0f;
            }
        }

        private void ResetRuntimeStateForBootstrap()
        {
            StopStepSynchronization();
            transmissionHotspotGrid = null;
            transmissionHotspotCount = 0;
            PandemicProfiler.Reset();
            ScheduledInterventionApplied = false;
            AppliedInterventionCount = 0;
            recordedFamilyClosures.Clear();
            active = false;
            startCompleted = false;
            initialPopulation.Clear();
            initialPopulationHealthy.Clear();
            initialPopulationRecovered.Clear();
            initialPopulationDead.Clear();
            trackedAgeGroupByCitizen.Clear();
            trackedPopulationCategoryByCitizen.Clear();
            initialPopulationSick.Clear();
            initialPopulationExposed.Clear();
            usedCitizens.Clear();
            citizenMatching.Clear();
            logicalCitizenByRealCitizen.Clear();
            lock (releasedCitizenIdsLock)
            {
                releasedRealCitizenIds.Clear();
            }

            nextLogicalCitizenId = 0;
            activeInfections.Clear();
            infectedCitizens.Clear();
            infectedCitizensWithSymptoms.Clear();
            symptomHospitalHandledCitizens.Clear();
            diseaseStateEngine.Reset();
            infectedBuildingIds.Clear();
            quarantineBuildingIds.Clear();
            hotspotBuildingIds.Clear();
            hubBuildingIds.Clear();
            buildingInfectedCounts.Clear();
            buildingCurrentInfectedCounts.Clear();
            infectionOrigins.Clear();
            policyTimeline.Clear();
            publicTransportLineStates.Clear();
            publicTransportDepotStates.Clear();
              publicTransportVehicles.Clear();
              publicTransportShutdownState = PandemicPublicTransportShutdownState.Open;
              publicTransportClosedLastTick = false;
              ClearTickStateIndex();
              lastObservedBuildingByCitizen.Clear();
              buildingEntryTimestampMsByCitizen.Clear();
              homeDepartureIntentTimestampMsByCitizen.Clear();
              deathRecords.Clear();
              healthcareUsageSamples.Clear();
              pendingTransmissionIds.Clear();
              pendingTransmissionExposures.Clear();
              recentUpdateDurationsMs.Clear();
              averageUpdateDurationMs = 0d;
              performanceTier = PandemicPerformanceTier.Light;
              liveSnapshotCache = null;
              nextAnalyticsSnapshotRefreshTime = 0f;
              nextSuperspreaderSnapshotRefreshTime = 0f;
              cachedChartObservationCount = -1;
              cachedPolicyMarkerCount = -1;
              cachedAnalyticsInfectionCount = -1;
              cachedSuperspreaderInfectionCount = -1;
              snapshotVersion = 0;
              chartVersion = 0;
              lockdownEngine.Reset();
              currentLockdownMetricPercent = 0d;
              experimentRecorder.Reset();
              frozenScientificSnapshot = null;
              lastDateTime = default;
            lastDateTimeCitizensUpdate = default;
            lastStoreTime = default;
            currentDateTime = default;
            hadAnySickCitizens = false;
        }

        private bool HandleSimulationFailure(string code, string message, Exception exception)
        {
            runIntegrityMonitor.Fail(code, message, currentDateTime, exception);
            active = false;

            if (runContext?.IsBatch == true)
            {
                Log.Error("[TENUS Batch] " + message + " " + exception);
                return true;
            }

            Log.Error("The 'Real Time' pandemic simulation was stopped after a fatal scientific-core error: " + message + " " + exception);
            try
            {
                FreezeRun(PandemicCompletionReason.Aborted);
            }
            catch (Exception freezeException)
            {
                lifecycleState = PandemicLifecycleState.Finished;
                frozenForFinalization = false;
                Log.Error("The 'Real Time' pandemic simulation could not freeze its failed manual run: " + freezeException);
            }

            return true;
        }

        /// <summary>Normalizes the pandemic values that managers cache or assume are valid at runtime.</summary>
        internal static void NormalizeRuntimeConfiguration(Config.RealTimeConfig config)
        {
            PandemicConfigurationNormalizer.Normalize(config);
        }

        private bool IsInfectious(uint citizenID)
        {
            return diseaseStateEngine.IsInfectious(citizenID, currentDateTime);
        }

        // SEIR: promote citizens from latent/exposed phase into the infectious phase once
        // StartInfection days have elapsed since their infection timestamp.
        private void PromoteExposedToInfectious()
        {
            for (int i = 0; i < initialPopulationExposed.Count; i++)
            {
                uint citizenId = initialPopulationExposed[i];
                if (!activeInfections.ContainsKey(citizenId))
                {
                    initialPopulationExposed.RemoveAt(i--);
                    continue;
                }

                DiseaseState state = diseaseStateEngine.GetState(citizenId, currentDateTime);
                if (state == DiseaseState.Infectious
                    || state == DiseaseState.PostInfectiousIll
                    || state == DiseaseState.Recovered)
                {
                    initialPopulationExposed.RemoveAt(i--);
                    initialPopulationSick.Add(citizenId);
                }
            }
        }

        private float GetFamilyReopenThreshold(PandemicLockdownFamily family)
        {
            if (Config == null)
            {
                return 0f;
            }

            switch (family)
            {
                case PandemicLockdownFamily.Education:
                    return Config.ReopenEducationThresholdPercent;
                case PandemicLockdownFamily.PublicTransport:
                    return Config.ReopenPublicTransportThresholdPercent;
                case PandemicLockdownFamily.Commercial:
                    return Config.ReopenCommercialThresholdPercent;
                case PandemicLockdownFamily.LeisureTourismParks:
                    return Config.ReopenLeisureTourismParksThresholdPercent;
                case PandemicLockdownFamily.Office:
                    return Config.ReopenOfficeThresholdPercent;
                case PandemicLockdownFamily.IndustryPlayerIndustry:
                    return Config.ReopenIndustryThresholdPercent;
                case PandemicLockdownFamily.GovernmentOtherPublic:
                    return Config.ReopenGovernmentOtherPublicThresholdPercent;
                case PandemicLockdownFamily.EssentialServices:
                    return Config.ReopenEssentialServicesThresholdPercent;
                default:
                    return 0f;
            }
        }

        private float GetFamilyMinimumClosureDurationDays(PandemicLockdownFamily family)
        {
            if (Config == null)
            {
                return 0f;
            }

            switch (family)
            {
                case PandemicLockdownFamily.Education:
                    return Config.MinimumEducationClosureDurationDays;
                case PandemicLockdownFamily.PublicTransport:
                    return Config.MinimumPublicTransportClosureDurationDays;
                case PandemicLockdownFamily.Commercial:
                    return Config.MinimumCommercialClosureDurationDays;
                case PandemicLockdownFamily.LeisureTourismParks:
                    return Config.MinimumLeisureTourismParksClosureDurationDays;
                case PandemicLockdownFamily.Office:
                    return Config.MinimumOfficeClosureDurationDays;
                case PandemicLockdownFamily.IndustryPlayerIndustry:
                    return Config.MinimumIndustryClosureDurationDays;
                case PandemicLockdownFamily.GovernmentOtherPublic:
                    return Config.MinimumGovernmentOtherPublicClosureDurationDays;
                case PandemicLockdownFamily.EssentialServices:
                    return Config.MinimumEssentialServicesClosureDurationDays;
                default:
                    return 0f;
            }
        }

        private float GetFamilyCooldownDurationDays(PandemicLockdownFamily family)
        {
            if (Config == null)
            {
                return 0f;
            }

            switch (family)
            {
                case PandemicLockdownFamily.Education:
                    return Config.EducationLockdownCooldownDurationDays;
                case PandemicLockdownFamily.PublicTransport:
                    return Config.PublicTransportLockdownCooldownDurationDays;
                case PandemicLockdownFamily.Commercial:
                    return Config.CommercialLockdownCooldownDurationDays;
                case PandemicLockdownFamily.LeisureTourismParks:
                    return Config.LeisureTourismParksLockdownCooldownDurationDays;
                case PandemicLockdownFamily.Office:
                    return Config.OfficeLockdownCooldownDurationDays;
                case PandemicLockdownFamily.IndustryPlayerIndustry:
                    return Config.IndustryLockdownCooldownDurationDays;
                case PandemicLockdownFamily.GovernmentOtherPublic:
                    return Config.GovernmentOtherPublicLockdownCooldownDurationDays;
                case PandemicLockdownFamily.EssentialServices:
                    return Config.EssentialServicesLockdownCooldownDurationDays;
                default:
                    return 0f;
            }
        }

        private bool TryInfectCitizen(uint infectedCitizenID, ref Citizen infectedCitizen)
        {
            return TryInfectCitizen(infectedCitizenID, ref infectedCitizen, 0d, DiseaseExposureKind.SecondaryTransmission);
        }

        private bool TryInfectCitizen(
            uint infectedCitizenID,
            ref Citizen infectedCitizen,
            double infectionAgeDays,
            DiseaseExposureKind exposureKind)
        {
            if (!diseaseStateEngine.IsSusceptible(infectedCitizenID, currentDateTime))
            {
                return false;
            }

            bool isSymptomatic = symptomRandom.NextDouble() < Config.SymptomProbability / 100.0;
            DiseaseCourse course = exposureKind == DiseaseExposureKind.InitialSeed
                ? diseaseProgressionEngine.CreateInitialCourse(
                    infectedCitizenID,
                    currentDateTime,
                    infectionAgeDays,
                    isSymptomatic,
                    exposureKind,
                    out _)
                : diseaseProgressionEngine.CreateCourse(
                    infectedCitizenID,
                    currentDateTime,
                    isSymptomatic,
                    exposureKind);
            if (!diseaseStateEngine.TryExpose(course, currentDateTime))
            {
                return false;
            }

            CitizenProxy.SetSick(ref infectedCitizen, false);
            infectedCitizens.Add(infectedCitizenID);
            if (isSymptomatic)
            {
                infectedCitizensWithSymptoms.Add(infectedCitizenID);
            }

            activeInfections.Add(infectedCitizenID, course.ExposureTime.Ticks / 10000);
            hadAnySickCitizens = true;

            initialPopulationHealthy.Remove(infectedCitizenID);

            DiseaseState state = diseaseStateEngine.GetState(infectedCitizenID, currentDateTime);
            if (state == DiseaseState.Infectious || state == DiseaseState.PostInfectiousIll)
            {
                initialPopulationSick.Add(infectedCitizenID);
            }
            else
            {
                initialPopulationExposed.Add(infectedCitizenID);
            }

            return true;
        }

        private void HealCitizen(uint infectedCitizenID, ref Citizen infectedCitizen)
        {
            if (!infectedCitizens.Contains(infectedCitizenID))
            {
                return;
            }

            if (diseaseStateEngine.TryGetCourse(infectedCitizenID, out DiseaseCourse recoveryCourse)
                && recoveryCourse.HospitalizationState == HospitalizationState.Hospitalized)
            {
                recoveryCourse.HospitalizationState = HospitalizationState.Discharged;
            }

            CitizenProxy.SetSick(ref infectedCitizen, false);
            activeInfections.Remove(infectedCitizenID);
            infectionOrigins.Remove(infectedCitizenID);
            infectedCitizensWithSymptoms.Remove(infectedCitizenID);
            symptomHospitalHandledCitizens.Remove(infectedCitizenID);

            initialPopulationRecovered.Add(infectedCitizenID);
            initialPopulationSick.Remove(infectedCitizenID);
            initialPopulationExposed.Remove(infectedCitizenID); // defensive: in case of edge-case recovery before promotion
        }

        private void KillCitizen(uint infectedCitizenID, ref Citizen infectedCitizen, DateTime diseaseDeathTime)
        {
            if (!infectedCitizens.Contains(infectedCitizenID) || !activeInfections.ContainsKey(infectedCitizenID))
            {
                return;
            }

            if (!diseaseStateEngine.TryMarkDead(infectedCitizenID, diseaseDeathTime))
            {
                return;
            }

            RecordDeath(infectedCitizenID, ref infectedCitizen);
            CitizenProxy.SetDead(ref infectedCitizen, true);
            CitizenProxy.SetSick(ref infectedCitizen, false);
            activeInfections.Remove(infectedCitizenID);
            infectionOrigins.Remove(infectedCitizenID);
            infectedCitizensWithSymptoms.Remove(infectedCitizenID);
            symptomHospitalHandledCitizens.Remove(infectedCitizenID);

            initialPopulationDead.Add(infectedCitizenID);
            initialPopulationSick.Remove(infectedCitizenID);
            initialPopulationExposed.Remove(infectedCitizenID); // defensive
        }

        private void RecordDeath(uint citizenId, ref Citizen citizen)
        {
            deathRecords.Add(new PandemicDeathRecord
            {
                CitizenId = citizenId,
                SimulationTime = currentDateTime,
                DeathPosition = GetHeatmapPosition(ref citizen, PandemicXRayLocationMode.LivePositions),
                HomeBuildingId = CitizenProxy.GetHomeBuilding(ref citizen),
            });
        }

        internal bool ShouldSeekHospital(uint citizenId)
        {
            citizenId = ResolveLogicalCitizenIdFromGame(citizenId);
            bool shouldSeek = lifecycleState == PandemicLifecycleState.Running
                && activeInfections.ContainsKey(citizenId)
                && diseaseStateEngine.TryGetCourse(citizenId, out DiseaseCourse symptomCourse)
                && symptomCourse.GetSymptomState(currentDateTime) == SymptomState.Symptomatic
                && !symptomHospitalHandledCitizens.Contains(citizenId);
            if (shouldSeek
                && diseaseStateEngine.TryGetCourse(citizenId, out DiseaseCourse course)
                && course.HospitalizationState == HospitalizationState.None)
            {
                course.HospitalizationState = HospitalizationState.SeekingCare;
            }

            return shouldSeek;
        }

        internal void OnCitizenVisitedHealthcare(uint citizenId)
        {
            citizenId = ResolveLogicalCitizenIdFromGame(citizenId);
            if (lifecycleState != PandemicLifecycleState.Running || !activeInfections.ContainsKey(citizenId))
            {
                return;
            }

            // Policy callbacks use the last epidemiological step. Advancing this shared
            // clock to a frame time can put population events ahead of the next exact step.

            symptomHospitalHandledCitizens.Add(citizenId);
            if (diseaseStateEngine.TryGetCourse(citizenId, out DiseaseCourse course))
            {
                course.HospitalizationState = HospitalizationState.Hospitalized;
            }
            QuarantineManager.Instance.AddCitizenInIsolation(citizenId, currentDateTime);

            Citizen[] citizens = CitizenMgr?.GetCitizensArray();
            if (citizens == null)
            {
                return;
            }

            uint realId = retrieveID(citizenId);
            if (realId < citizens.Length)
            {
                CitizenProxy.SetSick(ref citizens[realId], false);
            }
        }

        internal void OnHospitalUnavailable(uint citizenId)
        {
            citizenId = ResolveLogicalCitizenIdFromGame(citizenId);
            if (lifecycleState != PandemicLifecycleState.Running || !activeInfections.ContainsKey(citizenId))
            {
                return;
            }

            // Policy callbacks use the last epidemiological step. Advancing this shared
            // clock to a frame time can put population events ahead of the next exact step.

            symptomHospitalHandledCitizens.Add(citizenId);
            if (diseaseStateEngine.TryGetCourse(citizenId, out DiseaseCourse course))
            {
                course.HospitalizationState = HospitalizationState.CareUnavailable;
            }
            QuarantineManager.Instance.AddCitizenInIsolation(citizenId, currentDateTime);
        }

        private int GetDaysSinceInfection(uint citizenId)
        {
            if (!activeInfections.ContainsKey(citizenId))
            {
                return -1;
            }

            long nowMs = (currentDateTime).Ticks / 10000;
            long infectionMs = activeInfections[citizenId];
            return (int)Math.Max(0L, (nowMs - infectionMs) / (24L * 3600L * 1000L));
        }

        private void SyncSymptomStates(Citizen[] citizens)
        {
            if (citizens == null)
            {
                return;
            }

            for (int i = 0; i < initialPopulationSick.Count; i++)
            {
                uint citizenId = initialPopulationSick[i];
                uint realId = retrieveID(citizenId);
                if (realId >= citizens.Length)
                {
                    continue;
                }

                if (!activeInfections.ContainsKey(citizenId))
                {
                    continue;
                }

                bool symptomatic = IsKnownSick(citizenId);
                CitizenProxy.SetSick(ref citizens[realId], symptomatic);
            }
        }

        private PandemicInfectionOriginInfo CreateSeedOrigin(ref Citizen citizen)
        {
            return new PandemicInfectionOriginInfo
            {
                Category = PandemicInfectionOriginCategory.InitialSeed,
                LegacyType = InfectionType.OUTDOOR,
                BuildingId = CitizenProxy.GetHomeBuilding(ref citizen),
                BuildingService = ItemClass.Service.Residential,
                Position = CitizenProxy.GetHomeBuilding(ref citizen) != 0
                    ? BuildingMgr.GetBuildingPosition(CitizenProxy.GetHomeBuilding(ref citizen))
                    : Vector3.zero,
            };
        }

        private PandemicInfectionOriginInfo CreateOutdoorOrigin(Vector3 position)
        {
            return new PandemicInfectionOriginInfo
            {
                Category = PandemicInfectionOriginCategory.OutdoorStreet,
                LegacyType = InfectionType.OUTDOOR,
                Position = position,
            };
        }

        private PandemicInfectionOriginInfo CreateVehicleOrigin(ushort vehicleId, Vector3 position)
        {
            return new PandemicInfectionOriginInfo
            {
                Category = PandemicTaxonomy.GetVehicleOriginCategory(vehicleId),
                LegacyType = InfectionType.VEHICLE,
                VehicleId = vehicleId,
                Position = position,
            };
        }

        private PandemicInfectionOriginInfo CreateBuildingOrigin(ushort buildingId, Vector3 position)
        {
            ItemClass.Service service = BuildingMgr.GetBuildingService(buildingId);
            ItemClass.SubService subService = BuildingMgr.GetBuildingSubService(buildingId);
            return new PandemicInfectionOriginInfo
            {
                Category = PandemicTaxonomy.GetBuildingOriginCategory(service, subService),
                LegacyType = InfectionType.INDOOR,
                BuildingId = buildingId,
                BuildingService = service,
                BuildingSubService = subService,
                Position = position,
            };
        }

        private bool IsInQuarantine(uint citizenId)
        {
            return QuarantineManager.Instance.IsRestricted(citizenId, currentDateTime);
        }

        public void Shuffle<T>(IList<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = interventionRandom.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        public void spread()
        {
            experimentRecorder.BeginContactStep(currentDateTime);
            Citizen[] citizens = CitizenMgr.GetCitizensArray();
            if (citizens == null)
            {
                experimentRecorder.EndContactStep();
                return;
            }

            BuildCitizenTickStateIndex(citizens);

            pendingTransmissionIds.Clear();
            pendingTransmissionExposures.Clear();
            float rangeSq = Config.DiseaseTransmissionRange * Config.DiseaseTransmissionRange;
            SimulateOutdoorTransmissions(rangeSq);
            SimulateVehicleTransmissions();
            SimulateBuildingTransmissions();
            experimentRecorder.EndContactStep();
            ResolvePendingTransmissions(citizens);
        }

        private void SimulateOutdoorTransmissions(float rangeSq)
        {
            using (PandemicProfiler.Measure("SimulateOutdoorTransmissions")) SimulateOutdoorTransmissionsProfiled(rangeSq);
        }

        private void SimulateOutdoorTransmissionsProfiled(float rangeSq)
        {
            if (outdoorOccupantsByCell.Count == 0)
            {
                return;
            }

            foreach (int cellKey in outdoorOccupantsByCell.Keys.OrderBy(key => key))
            {
                List<PandemicCitizenTickState> occupants = outdoorOccupantsByCell[cellKey];
                for (int i = 0; i < occupants.Count; i++)
                {
                    PandemicCitizenTickState first = occupants[i];
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int neighborKey = GetOutdoorCellKey(first.OutdoorCellX + dx, first.OutdoorCellZ + dz);
                            if (!outdoorOccupantsByCell.TryGetValue(neighborKey, out List<PandemicCitizenTickState> neighborStates))
                            {
                                continue;
                            }

                            for (int j = 0; j < neighborStates.Count; j++)
                            {
                                PandemicCitizenTickState second = neighborStates[j];
                                if (second.CitizenId <= first.CitizenId)
                                {
                                    continue;
                                }

                                if (!IsWithinOutdoorContactRange(first.Position, second.Position, rangeSq, out float squaredDistance))
                                {
                                    continue;
                                }

                                PandemicInfectionOriginInfo origin = CreateOutdoorOrigin(Midpoint(first.Position, second.Position));
                                ProcessPhysicalContactPair(
                                    first,
                                    second,
                                    PhysicalContactContext.Outdoor,
                                    origin,
                                    Math.Sqrt(squaredDistance),
                                    false);
                            }
                        }
                    }
                }
            }
        }

        private void SimulateVehicleTransmissions()
        {
            using (PandemicProfiler.Measure("SimulateVehicleTransmissions")) SimulateVehicleTransmissionsProfiled();
        }

        private void SimulateVehicleTransmissionsProfiled()
        {
            foreach (ushort vehicleId in vehicleOccupants.Keys.OrderBy(id => id))
            {
                List<PandemicCitizenTickState> occupants = vehicleOccupants[vehicleId];
                IList<ContactPair> pairs = contactSamplingEngine.SampleBounded(
                    occupants.Select(state => state.CitizenId),
                    (int)Config.MaxContactsPerPersonPerStepTransit,
                    contactSamplingSeed,
                    ContactPersistencePolicy.SamplingKey(Config, PhysicalContactContext.PublicTransport, currentDateTime),
                    100000 + vehicleId);
                for (int i = 0; i < pairs.Count; ++i)
                {
                    ContactPair pair = pairs[i];
                    if (citizenTickStateById.TryGetValue(pair.CitizenA, out PandemicCitizenTickState first)
                        && citizenTickStateById.TryGetValue(pair.CitizenB, out PandemicCitizenTickState second))
                    {
                        PandemicInfectionOriginInfo origin = CreateVehicleOrigin(vehicleId, Midpoint(first.Position, second.Position));
                        ProcessPhysicalContactPair(
                            first,
                            second,
                            GetContactContext(origin),
                            origin,
                            null,
                            false);
                    }
                }
            }
        }

        private void SimulateBuildingTransmissions()
        {
            using (PandemicProfiler.Measure("SimulateBuildingTransmissions")) SimulateBuildingTransmissionsProfiled();
        }

        private void SimulateBuildingTransmissionsProfiled()
        {
            foreach (ushort buildingId in buildingOccupants.Keys.OrderBy(id => id))
            {
                List<PandemicCitizenTickState> occupants = buildingOccupants[buildingId];
                if (occupants.Count < 2)
                {
                    continue;
                }

                Vector3 buildingPosition = BuildingMgr.GetBuildingPosition(buildingId);
                ItemClass.Service service = BuildingMgr.GetBuildingService(buildingId);
                ItemClass.SubService subService = BuildingMgr.GetBuildingSubService(buildingId);
                PandemicInfectionOriginInfo origin = CreateBuildingOrigin(buildingId, buildingPosition);
                if (service == ItemClass.Service.Residential)
                {
                    HashSet<ulong> householdPairs = RecordHouseholdContacts(occupants, origin);
                    IList<ContactPair> sharedPairs = contactSamplingEngine.SampleBounded(
                        occupants.Select(state => state.CitizenId),
                        (int)Config.MaxContactsPerPersonPerStepResidentialSharedArea,
                        contactSamplingSeed,
                        ContactPersistencePolicy.SamplingKey(Config, PhysicalContactContext.Other, currentDateTime, true),
                        200000 + buildingId);
                    for (int i = 0; i < sharedPairs.Count; ++i)
                    {
                        ContactPair pair = sharedPairs[i];
                        if (householdPairs.Contains(CreateCitizenPairKey(pair.CitizenA, pair.CitizenB))
                            || !citizenTickStateById.TryGetValue(pair.CitizenA, out PandemicCitizenTickState first)
                            || !citizenTickStateById.TryGetValue(pair.CitizenB, out PandemicCitizenTickState second)
                            || !first.IsInResidentialSharedAreaWindow
                            || !second.IsInResidentialSharedAreaWindow)
                        {
                            continue;
                        }

                        ProcessPhysicalContactPair(first, second, PhysicalContactContext.Other, origin, null, true);
                    }

                    continue;
                }

                PhysicalContactContext context = GetBuildingContactContext(service, subService);
                int maximumContacts = GetMaximumContactsPerPerson(context);
                IList<ContactPair> sampledPairs = contactSamplingEngine.SampleBounded(
                    occupants.Select(state => state.CitizenId),
                    maximumContacts,
                    contactSamplingSeed,
                    ContactPersistencePolicy.SamplingKey(Config, context, currentDateTime),
                    300000 + ((int)context * 65536) + buildingId);
                for (int i = 0; i < sampledPairs.Count; ++i)
                {
                    ContactPair pair = sampledPairs[i];
                    if (citizenTickStateById.TryGetValue(pair.CitizenA, out PandemicCitizenTickState first)
                        && citizenTickStateById.TryGetValue(pair.CitizenB, out PandemicCitizenTickState second))
                    {
                        ProcessPhysicalContactPair(first, second, context, origin, null, false);
                    }
                }
            }
        }

        private void QueueTransmissionExposure(
            uint sourceCitizenId,
            PandemicCitizenTickState target,
            double probability,
            PandemicInfectionOriginInfo origin,
            double infectiousnessMultiplier)
        {
            long contextKey = origin == null
                ? 0L
                : ((long)(int)origin.Category << 32)
                    | ((long)origin.BuildingId << 16)
                    | origin.VehicleId;
            pendingTransmissionExposures.Add(new TransmissionExposure<PandemicPendingTransmissionContext>
            {
                SourceCitizenId = sourceCitizenId,
                TargetCitizenId = target.CitizenId,
                Probability = probability,
                StableContextKey = contextKey,
                Context = new PandemicPendingTransmissionContext
                {
                    Target = target,
                    Origin = origin,
                    InfectiousnessMultiplier = infectiousnessMultiplier,
                },
            });
        }

        private HashSet<ulong> RecordHouseholdContacts(
            IList<PandemicCitizenTickState> occupants,
            PandemicInfectionOriginInfo origin)
        {
            using (PandemicProfiler.Measure("HouseholdContacts")) return RecordHouseholdContactsCore(occupants, origin);
        }

        private HashSet<ulong> RecordHouseholdContactsCore(IList<PandemicCitizenTickState> occupants, PandemicInfectionOriginInfo origin)
        {
            var occupantIds = new HashSet<uint>(occupants.Select(state => state.CitizenId));
            var pairs = new HashSet<ulong>();
            foreach (PandemicCitizenTickState citizen in occupants.OrderBy(state => state.CitizenId))
            {
                if (!citizen.AtHome)
                {
                    continue;
                }

                Array.Clear(familyLookupBuffer, 0, familyLookupBuffer.Length);
                if (!CitizenMgr.TryGetFamily(citizen.RealCitizenId, familyLookupBuffer))
                {
                    continue;
                }

                for (int i = 0; i < familyLookupBuffer.Length; ++i)
                {
                    uint realFamilyMemberId = familyLookupBuffer[i];
                    if (!TryGetLogicalCitizenId(realFamilyMemberId, out uint familyMemberId)
                        || familyMemberId <= citizen.CitizenId
                        || !occupantIds.Contains(familyMemberId)
                        || !citizenTickStateById.TryGetValue(familyMemberId, out PandemicCitizenTickState familyMember)
                        || !familyMember.AtHome)
                    {
                        continue;
                    }

                    ulong pairKey = CreateCitizenPairKey(citizen.CitizenId, familyMemberId);
                    if (pairs.Add(pairKey))
                    {
                        ProcessPhysicalContactPair(
                            citizen,
                            familyMember,
                            PhysicalContactContext.Household,
                            origin,
                            null,
                            false);
                    }
                }
            }

            return pairs;
        }

        private void ProcessPhysicalContactPair(
            PandemicCitizenTickState first,
            PandemicCitizenTickState second,
            PhysicalContactContext context,
            PandemicInfectionOriginInfo origin,
            double? distance,
            bool residentialSharedArea)
        {
            if (first == null || second == null || first.CitizenId == second.CitizenId)
            {
                return;
            }

            if (!isolationQuarantineEngine.IsPhysicalContactAllowed(
                first.ShouldQuarantine,
                first.AtHome,
                second.ShouldQuarantine,
                second.AtHome,
                context))
            {
                experimentRecorder.RecordPreventedContact();
                return;
            }

            if (!RecordPhysicalContact(first.CitizenId, second.CitizenId, context, origin, distance))
            {
                return;
            }

            QueueDirectionalTransmission(first, second, context, origin, residentialSharedArea);
            QueueDirectionalTransmission(second, first, context, origin, residentialSharedArea);
        }

        private void QueueDirectionalTransmission(
            PandemicCitizenTickState source,
            PandemicCitizenTickState target,
            PhysicalContactContext context,
            PandemicInfectionOriginInfo origin,
            bool residentialSharedArea)
        {
            using (PandemicProfiler.Measure("TransmissionHazard")) QueueDirectionalTransmissionCore(source, target, context, origin, residentialSharedArea);
        }

        private void QueueDirectionalTransmissionCore(PandemicCitizenTickState source, PandemicCitizenTickState target, PhysicalContactContext context, PandemicInfectionOriginInfo origin, bool residentialSharedArea)
        {
            if (!source.IsInfectious
                || !target.IsSusceptible
                || !diseaseStateEngine.IsSusceptible(target.CitizenId, currentDateTime))
            {
                return;
            }

            double probability;
            if (context == PhysicalContactContext.Household)
            {
                probability = Masks.GetHouseholdInfectionProbability(source.CitizenId, target.CitizenId);
            }
            else if (residentialSharedArea)
            {
                probability = Masks.GetIndoorInfectionProbabilityNoContact(source.CitizenId, target.CitizenId);
            }
            else if (origin != null && origin.VehicleId != 0)
            {
                probability = Masks.GetVehicleInfectionProbability(source.CitizenId, target.CitizenId);
            }
            else if (context == PhysicalContactContext.Outdoor)
            {
                probability = Masks.GetOutdoorInfectionProbability(source.CitizenId, target.CitizenId);
            }
            else
            {
                probability = Masks.GetIndoorInfectionProbability(source.CitizenId, target.CitizenId);
            }

            if (!diseaseStateEngine.TryGetCourse(source.CitizenId, out DiseaseCourse sourceCourse))
            {
                return;
            }

            double infectiousnessMultiplier = InfectiousnessProfileEngine.GetMultiplier(
                sourceCourse,
                currentDateTime,
                infectiousnessProfilePolicy);
            probability = InfectiousnessProfileEngine.ApplyToProbability(probability, infectiousnessMultiplier);
            QueueTransmissionExposure(source.CitizenId, target, probability, origin, infectiousnessMultiplier);
        }

        private int GetMaximumContactsPerPerson(PhysicalContactContext context)
        {
            switch (context)
            {
                case PhysicalContactContext.School:
                case PhysicalContactContext.University:
                    return (int)Config.MaxContactsPerPersonPerStepSchool;
                case PhysicalContactContext.Healthcare:
                    return (int)Config.MaxContactsPerPersonPerStepHealthcare;
                case PhysicalContactContext.Commercial:
                case PhysicalContactContext.Leisure:
                    return (int)Config.MaxContactsPerPersonPerStepCommercial;
                case PhysicalContactContext.PublicTransport:
                    return (int)Config.MaxContactsPerPersonPerStepTransit;
                default:
                    return (int)Config.MaxContactsPerPersonPerStepWorkplace;
            }
        }

        private static PhysicalContactContext GetBuildingContactContext(
            ItemClass.Service service,
            ItemClass.SubService subService)
        {
            switch (service)
            {
                case ItemClass.Service.Education:
                    return PhysicalContactContext.School;
                case ItemClass.Service.PlayerEducation:
                    return subService == ItemClass.SubService.PlayerEducationUniversity
                        || subService == ItemClass.SubService.PlayerEducationLiberalArts
                        || subService == ItemClass.SubService.PlayerEducationTradeSchool
                        ? PhysicalContactContext.University
                        : PhysicalContactContext.School;
                case ItemClass.Service.Office:
                case ItemClass.Service.Industrial:
                case ItemClass.Service.PlayerIndustry:
                    return PhysicalContactContext.Workplace;
                case ItemClass.Service.HealthCare:
                    return PhysicalContactContext.Healthcare;
                case ItemClass.Service.Commercial:
                    return PhysicalContactContext.Commercial;
                case ItemClass.Service.Tourism:
                case ItemClass.Service.Monument:
                case ItemClass.Service.Museums:
                case ItemClass.Service.VarsitySports:
                case ItemClass.Service.Beautification:
                    return PhysicalContactContext.Leisure;
                case ItemClass.Service.PublicTransport:
                    return PhysicalContactContext.PublicTransport;
                default:
                    return PhysicalContactContext.Other;
            }
        }

        internal static bool IsWithinOutdoorContactRange(Vector3 first, Vector3 second, float rangeSquared, out float squaredDistance)
        {
            squaredDistance = (second - first).sqrMagnitude;
            return !(squaredDistance > rangeSquared);
        }

        private static Vector3 Midpoint(Vector3 first, Vector3 second)
        {
            return (first + second) * 0.5f;
        }

        private static ulong CreateCitizenPairKey(uint citizenA, uint citizenB)
        {
            uint lower = Math.Min(citizenA, citizenB);
            uint upper = Math.Max(citizenA, citizenB);
            return ((ulong)lower << 32) | upper;
        }

        private bool RecordPhysicalContact(
            uint citizenA,
            uint citizenB,
            PhysicalContactContext context,
            PandemicInfectionOriginInfo origin,
            double? distance)
        {
            bool contactCreated;
            contactRequest.CitizenA = citizenA;
            contactRequest.CitizenB = citizenB;
            contactRequest.EndTime = currentDateTime;
            contactRequest.DurationMinutes = Config.EpidemicStepMinutes;
            contactRequest.Context = context;
            contactRequest.BuildingId = origin?.BuildingId ?? 0;
            contactRequest.VehicleId = origin?.VehicleId ?? 0;
            contactRequest.PositionX = origin?.Position.x ?? 0f;
            contactRequest.PositionY = origin?.Position.y ?? 0f;
            contactRequest.PositionZ = origin?.Position.z ?? 0f;
            contactRequest.Distance = distance;
            PhysicalContactEvent contact = ContactManager.Instance.AddPhysicalContact(contactRequest, out contactCreated);
            if (!contactCreated)
            {
                return false;
            }

            experimentRecorder.RecordPhysicalContact(contact, GetOriginDistrictId(origin),
                trackedAgeGroupByCitizen.TryGetValue(citizenA, out Citizen.AgeGroup ageA) ? (int)ageA : -1,
                trackedAgeGroupByCitizen.TryGetValue(citizenB, out Citizen.AgeGroup ageB) ? (int)ageB : -1);
            return true;
        }

        private byte GetOriginDistrictId(PandemicInfectionOriginInfo origin)
        {
            if (origin == null || DistrictManager.instance == null)
            {
                return 0;
            }

            if (origin.BuildingId != 0)
            {
                return GetDistrictId(origin.BuildingId);
            }

            return origin.Position == Vector3.zero
                ? (byte)0
                : DistrictManager.instance.GetDistrict(origin.Position);
        }

        private static PhysicalContactContext GetContactContext(PandemicInfectionOriginInfo origin)
        {
            if (origin == null)
            {
                return PhysicalContactContext.Other;
            }

            switch (origin.Category)
            {
                case PandemicInfectionOriginCategory.ResidentialHome:
                    return PhysicalContactContext.Household;
                case PandemicInfectionOriginCategory.WorkplaceOfficeIndustry:
                    return PhysicalContactContext.Workplace;
                case PandemicInfectionOriginCategory.SchoolUniversity:
                    return PhysicalContactContext.School;
                case PandemicInfectionOriginCategory.Healthcare:
                    return PhysicalContactContext.Healthcare;
                case PandemicInfectionOriginCategory.CommercialLeisureTourism:
                    return PhysicalContactContext.Commercial;
                case PandemicInfectionOriginCategory.Bus:
                case PandemicInfectionOriginCategory.Tram:
                case PandemicInfectionOriginCategory.Metro:
                case PandemicInfectionOriginCategory.Train:
                case PandemicInfectionOriginCategory.ShipFerry:
                case PandemicInfectionOriginCategory.Plane:
                case PandemicInfectionOriginCategory.StopPlatform:
                    return PhysicalContactContext.PublicTransport;
                case PandemicInfectionOriginCategory.OutdoorStreet:
                    return PhysicalContactContext.Outdoor;
                default:
                    return PhysicalContactContext.Other;
            }
        }

        private void ResolvePendingTransmissions(Citizen[] citizens) { using (PandemicProfiler.Measure("ResolvePendingTransmissions")) { ResolvePendingTransmissionsProfiled(citizens); } }
        private void ResolvePendingTransmissionsProfiled(Citizen[] citizens) {
            IList<ResolvedTransmission<PandemicPendingTransmissionContext>> resolved = transmissionEngine.Resolve(
                pendingTransmissionExposures,
                transmissionRandom);
            for (int i = 0; i < resolved.Count; i++)
            {
                ResolvedTransmission<PandemicPendingTransmissionContext> transmission = resolved[i];
                if (!diseaseStateEngine.IsSusceptible(transmission.TargetCitizenId, currentDateTime))
                {
                    continue;
                }

                InfectTargetCitizen(
                    transmission.SourceCitizenId,
                    transmission.Context.Target,
                    citizens,
                    transmission.Context.Origin,
                    transmission.CombinedProbability,
                    transmission.SourceProbability,
                    transmission.Context.InfectiousnessMultiplier);
            }
        }

        private void InfectTargetCitizen(
            uint infectingCitizenId,
            PandemicCitizenTickState target,
            Citizen[] citizens,
            PandemicInfectionOriginInfo origin,
            double combinedProbability,
            double sourceProbability,
            double infectiousnessMultiplier)
        {
            if (TryInfectCitizen(target.CitizenId, ref citizens[target.RealCitizenId]))
            {
                pendingTransmissionIds.Add(target.CitizenId);
                Observer.AddCitizenInfection(infectingCitizenId, target.CitizenId, currentDateTime, origin);
                RecordInfectionOrigin(target.CitizenId, origin);
                double sourceInfectionAge = 0d;
                if (diseaseStateEngine.TryGetCourse(infectingCitizenId, out DiseaseCourse sourceCourse))
                {
                    sourceInfectionAge = Math.Max(0d, (currentDateTime - sourceCourse.ExposureTime).TotalDays);
                }

                experimentRecorder.RecordTransmission(new PandemicTransmissionEvent
                {
                    SimulationTime = currentDateTime,
                    SourceCitizenId = infectingCitizenId,
                    TargetCitizenId = target.CitizenId,
                    SourceInfectionAgeDays = sourceInfectionAge,
                    TargetPreviousState = DiseaseState.Susceptible,
                    TargetNewState = DiseaseState.Exposed,
                    Context = GetContactContext(origin),
                    OriginCategory = origin?.Category ?? PandemicInfectionOriginCategory.OtherUnknown,
                    BuildingId = origin?.BuildingId ?? 0,
                    VehicleId = origin?.VehicleId ?? 0,
                    DistrictId = GetOriginDistrictId(origin),
                    SourceMaskType = Masks.GetMaskTypeName(infectingCitizenId),
                    TargetMaskType = Masks.GetMaskTypeName(target.CitizenId),
                    TransmissionProbability = combinedProbability,
                    SourceProbability = sourceProbability,
                    InfectiousnessMultiplier = infectiousnessMultiplier,
                    IsInitialSeed = false,
                    PositionX = origin?.Position.x ?? 0f,
                    PositionY = origin?.Position.y ?? 0f,
                    PositionZ = origin?.Position.z ?? 0f,
                    HasPosition = origin != null,
                });
            }
        }

        private uint retrieveID(uint citizenID)
        {
            if (citizenMatching.ContainsKey(citizenID))
            {
                return citizenMatching[citizenID];
            }
            return citizenID;
        }

        private uint ResolveLogicalCitizenIdFromGame(uint realCitizenId)
        {
            return logicalCitizenByRealCitizen.TryGetValue(realCitizenId, out uint logicalCitizenId)
                ? logicalCitizenId
                : realCitizenId;
        }

        private bool TryGetLogicalCitizenId(uint realCitizenId, out uint logicalCitizenId)
        {
            return logicalCitizenByRealCitizen.TryGetValue(realCitizenId, out logicalCitizenId);
        }

        private void NotifyCitizenReleased(uint realCitizenId)
        {
            if (realCitizenId == 0u)
            {
                return;
            }

            lock (releasedCitizenIdsLock)
            {
                releasedRealCitizenIds.Add(realCitizenId);
            }
        }

        private bool ProcessReleasedCitizens()
        {
            List<uint> released;
            lock (releasedCitizenIdsLock)
            {
                if (releasedRealCitizenIds.Count == 0)
                {
                    return true;
                }

                released = releasedRealCitizenIds.OrderBy(id => id).ToList();
                releasedRealCitizenIds.Clear();
            }

            for (int i = 0; i < released.Count; ++i)
            {
                uint realCitizenId = released[i];
                if (TryGetLogicalCitizenId(realCitizenId, out uint logicalCitizenId)
                    && !HandleTrackedCitizenDeparture(logicalCitizenId, realCitizenId, "CitizenReleased"))
                {
                    return false;
                }
            }

            return true;
        }

        private bool HandleTrackedCitizenDeparture(uint logicalCitizenId, uint realCitizenId, string reason)
        {
            PandemicPopulationCategory previousCategory = trackedPopulationCategoryByCitizen.TryGetValue(
                logicalCitizenId,
                out PandemicPopulationCategory storedCategory)
                ? storedCategory
                : PandemicPopulationCategory.Resident;
            bool expectedDeadRelease = diseaseStateEngine.GetState(logicalCitizenId, currentDateTime)
                == DiseaseState.Dead;
            PandemicPopulationDepartureKind departureKind = populationLifecycleEngine.ClassifyDeparture(
                previousCategory,
                string.Equals(reason, "CitizenReleased", StringComparison.Ordinal));
            experimentRecorder.RecordPopulation(new PandemicPopulationEvent
            {
                SimulationTime = currentDateTime,
                CitizenId = logicalCitizenId,
                Action = expectedDeadRelease ? "Detached" : "Removed",
                PopulationCategory = expectedDeadRelease
                    ? previousCategory.ToString()
                    : populationLifecycleEngine.ClassifyRemoval(previousCategory).ToString(),
                Count = 1,
                Reason = expectedDeadRelease
                    ? "DeceasedCitizenReleased"
                    : GetPopulationDepartureReason(previousCategory, departureKind, reason),
            });

            usedCitizens.Remove(realCitizenId);
            logicalCitizenByRealCitizen.Remove(realCitizenId);
            citizenMatching.Remove(logicalCitizenId);
            initialPopulation.Remove(logicalCitizenId);
            if (expectedDeadRelease)
            {
                DetachTerminalCitizenFromGame(logicalCitizenId);
                return true;
            }

            RemoveTrackedCitizen(logicalCitizenId);
            if (runContext?.IsBatch != true
                || !Config.StrictPopulationIntegrity
                || departureKind != PandemicPopulationDepartureKind.UnexpectedDrift)
            {
                return true;
            }

            runIntegrityMonitor.Fail(
                "PopulationIntegrityViolation",
                "An unexpected " + previousCategory + " citizen departure occurred during strict scientific batch execution.",
                currentDateTime,
                null);
            active = false;
            return false;
        }

        private static string GetPopulationDepartureReason(
            PandemicPopulationCategory previousCategory,
            PandemicPopulationDepartureKind departureKind,
            string fallbackReason)
        {
            switch (departureKind)
            {
                case PandemicPopulationDepartureKind.ExpectedTransient:
                    return previousCategory + "Departed";
                case PandemicPopulationDepartureKind.ExpectedEmigration:
                    return "EmigrantDeparted";
                default:
                    return fallbackReason;
            }
        }

        private uint AllocateLogicalCitizenId()
        {
            if (nextLogicalCitizenId == uint.MaxValue)
            {
                throw new InvalidOperationException("The logical citizen identifier space is exhausted.");
            }

            return nextLogicalCitizenId++;
        }

        private void RemoveTrackedCitizen(uint citizenId)
        {
            diseaseStateEngine.RemoveCitizen(citizenId);
            activeInfections.Remove(citizenId);
            infectedCitizensWithSymptoms.Remove(citizenId);
            symptomHospitalHandledCitizens.Remove(citizenId);
            infectionOrigins.Remove(citizenId);
            trackedAgeGroupByCitizen.Remove(citizenId);
            trackedPopulationCategoryByCitizen.Remove(citizenId);
            initialPopulationHealthy.Remove(citizenId);
            initialPopulationExposed.Remove(citizenId);
            initialPopulationSick.Remove(citizenId);
            initialPopulationRecovered.Remove(citizenId);
            initialPopulationDead.Remove(citizenId);
            QuarantineManager.Instance.RemoveAllRestrictions(citizenId, currentDateTime, "PopulationRemoved");
            TestManager.Instance.CancelCitizen(citizenId);
        }

        private void DetachTerminalCitizenFromGame(uint citizenId)
        {
            activeInfections.Remove(citizenId);
            infectedCitizensWithSymptoms.Remove(citizenId);
            symptomHospitalHandledCitizens.Remove(citizenId);
            infectionOrigins.Remove(citizenId);
            QuarantineManager.Instance.RemoveAllRestrictions(citizenId, currentDateTime, "DeceasedCitizenReleased");
            TestManager.Instance.CancelCitizen(citizenId);
        }

        /// <summary>Returns the number of days the citizen has been infected, or -1 if not infected.</summary>
        public int GetDaysInfected(uint citizenId)
        {
            citizenId = ResolveLogicalCitizenIdFromGame(citizenId);
            if (!activeInfections.ContainsKey(citizenId) || simulation == null)
            {
                return -1;
            }

            long nowMs = currentDateTime.Ticks / 10000;
            long infectionMs = activeInfections[citizenId];
            long deltaMs = nowMs - infectionMs;
            if (deltaMs < 0) deltaMs = 0;
            return (int)(deltaMs / (24L * 3600 * 1000));
        }

        /// <summary>Returns the age-based death probability as a percentage (0-100).</summary>
        public float GetCitizenDeathProbabilityPercent(uint citizenId)
        {
            citizenId = ResolveLogicalCitizenIdFromGame(citizenId);
            if (Config == null || CitizenMgr == null || CitizenProxy == null)
            {
                return 0f;
            }

            Citizen[] citizens = CitizenMgr.GetCitizensArray();
            uint realId = retrieveID(citizenId);
            if (realId >= citizens.Length)
            {
                return 0f;
            }

            Citizen.AgeGroup age = CitizenProxy.GetAge(ref citizens[realId]);
            switch (age)
            {
                case Citizen.AgeGroup.Child:  return Config.DeathChild;
                case Citizen.AgeGroup.Teen:   return Config.DeathTeen;
                case Citizen.AgeGroup.Young:  return Config.DeathYoung;
                case Citizen.AgeGroup.Adult:  return Config.DeathAdult;
                case Citizen.AgeGroup.Senior: return Config.DeathSenior;
                default: return 0f;
            }
        }

        public bool ShouldBeInQuarantine(uint citizenID)
        {
            return ShouldLogicalCitizenBeInQuarantine(ResolveLogicalCitizenIdFromGame(citizenID));
        }

        private bool ShouldLogicalCitizenBeInQuarantine(uint citizenID)
        {
            if (lifecycleState != PandemicLifecycleState.Running)
            {
                return lifecycleState == PandemicLifecycleState.Finished
                    && QuarantineManager.Instance.IsRestrictedReadOnly(citizenID, currentDateTime);
            }

            // Policy callbacks use the last epidemiological step. Advancing this shared
            // clock to a frame time can put population events ahead of the next exact step.

            bool quarantineEnabled = Config.QuarantineBehavior != RealTime.Config.QuarantineBehavior.None;
            bool positiveAvailable = TestManager.Instance.IsTestedPositive(citizenID, currentDateTime);
            bool pendingBlock = quarantineEnabled
                && Config.QuarantineWhileAwaitingTestResult
                && TestManager.Instance.IsAwaitingResult(citizenID)
                && TestManager.Instance.IsBlocked(citizenID, currentDateTime);
            if (isolationQuarantineEngine.ShouldQuarantineForPendingTest(pendingBlock)
                && !positiveAvailable)
            {
                QuarantineManager.Instance.AddPendingTestQuarantine(citizenID, currentDateTime);
            }
            else
            {
                QuarantineManager.Instance.RemovePendingTestQuarantine(
                    citizenID,
                    currentDateTime,
                    positiveAvailable ? "PositiveResultAvailable" : "TestResultAvailableOrTestEnded");
            }

            bool knownSick = activeInfections.ContainsKey(citizenID) && IsKnownSick(citizenID);
            if (quarantineEnabled
                && isolationQuarantineEngine.ShouldIsolateCase(
                    Config.OnlyTestedCitizensToQuarantine,
                    knownSick,
                    positiveAvailable,
                    false))
            {
                if (!QuarantineManager.Instance.IsInIsolation(citizenID, currentDateTime))
                {
                    QuarantineManager.Instance.AddCitizenInIsolation(citizenID, currentDateTime);
                }
            }

            return QuarantineManager.Instance.IsRestricted(citizenID, currentDateTime);
        }

        private bool IsKnownSick(uint citizenID)
        {
            return diseaseStateEngine.TryGetCourse(citizenID, out DiseaseCourse course)
                && course.GetSymptomState(currentDateTime) == SymptomState.Symptomatic;
        }

        private void CheckForContacts(uint citizenID, uint timeInDays, Config.QuarantineBehavior quarantineBehavior)
        {
            if (QuarantineManager.Instance.HasBeenChecked(citizenID))
            {
                return;
            }

            if (!(TestManager.Instance.IsTestedPositive(citizenID, currentDateTime) || (!Config.OnlyTestedCitizensToQuarantine && IsKnownSick(citizenID))))
            {
                return;
            }

            QuarantineManager.Instance.AddCheckedCitizen(citizenID);

            if (quarantineBehavior == RealTime.Config.QuarantineBehavior.Contacts)
            {
                Dictionary<uint, DateTime> contacts = ContactManager.Instance.GetContactsForCitizen(citizenID);

                if (contacts == null)
                {
                    return;
                }

                long leastConsideredTime = currentDateTime.Ticks - (TimeSpan.TicksPerDay * timeInDays);

                var contactIds = new List<uint>(contacts.Keys);
                contactIds.Sort();
                foreach (uint contactId in contactIds)
                {
                    if (contacts[contactId].Ticks > leastConsideredTime)
                    {
                        QuarantineManager.Instance.AddContactQuarantine(contactId, currentDateTime);
                    }
                }
            }

            if (quarantineBehavior == RealTime.Config.QuarantineBehavior.Family)// || quarantineBehavior == RealTime.Config.QuarantineBehavior.Contacts)
            {
                uint[] family = new uint[10];
                uint realCitizenId = retrieveID(citizenID);
                if (CitizenMgr.TryGetFamily(realCitizenId, family))
                {
                    foreach (uint realFamilyMemberId in family)
                    {
                        if (TryGetLogicalCitizenId(realFamilyMemberId, out uint otherCitizenID)
                            && otherCitizenID != citizenID)
                        {
                            QuarantineManager.Instance.AddContactQuarantine(otherCitizenID, currentDateTime);
                        }
                    }
                }
            }
        }

        internal bool HasSymptoms(uint citizenID)
        {
            return diseaseStateEngine.TryGetCourse(citizenID, out DiseaseCourse course) && course.IsSymptomatic;
        }

        private void ProcessMortality() { using (PandemicProfiler.Measure("ProcessMortality")) { ProcessMortalityProfiled(); } }
        private void ProcessMortalityProfiled() {
            Citizen[] citizens = CitizenMgr.GetCitizensArray();
            double hospitalUsage = healthcareUsageSamples.Count == 0
                ? 0d
                : healthcareUsageSamples[healthcareUsageSamples.Count - 1].HospitalUsagePercent;
            double healthcareMultiplier = healthcareEngine.GetMortalityHazardMultiplier(hospitalUsage);
            TimeSpan configuredStepDuration = TimeSpan.FromMinutes(Config.EpidemicStepMinutes);
            DateTime stepStart = currentDateTime.Subtract(configuredStepDuration);

            for (int i = 0; i < initialPopulationSick.Count; i++)
            {
                uint citizenID = initialPopulationSick[i];
                if (!diseaseStateEngine.TryGetCourse(citizenID, out DiseaseCourse course))
                {
                    continue;
                }

                DateTime effectiveMortalityStart = course.MortalityStartTime > course.InfectiousStartTime
                    ? course.MortalityStartTime
                    : course.InfectiousStartTime;
                if (!activeInfections.ContainsKey(citizenID)
                    || currentDateTime <= effectiveMortalityStart
                    || stepStart >= course.RecoveryTime)
                {
                    continue;
                }

                DateTime hazardEnd = currentDateTime < course.RecoveryTime
                    ? currentDateTime
                    : course.RecoveryTime.AddTicks(-1L);
                DateTime hazardStart = stepStart > effectiveMortalityStart
                    ? stepStart
                    : effectiveMortalityStart;
                if (hazardEnd <= hazardStart)
                {
                    continue;
                }

                DiseaseState state = diseaseStateEngine.GetState(citizenID, hazardEnd);
                if (state != DiseaseState.Infectious && state != DiseaseState.PostInfectiousIll)
                {
                    continue;
                }

                uint realCitizenId = retrieveID(citizenID);
                if (realCitizenId >= citizens.Length)
                {
                    continue;
                }

                double baseProbability = GetConfiguredMortalityProbability(
                    CitizenProxy.GetAge(ref citizens[realCitizenId]));
                double hazardMultiplier = healthcareMultiplier
                    * (course.IsSymptomatic ? 1d : Config.AsymptomaticMortalityMultiplier);
                double stepProbability = DiseaseProgressionEngine.CalculateStepMortalityProbability(
                    baseProbability,
                    course.RecoveryTime - effectiveMortalityStart,
                    hazardEnd - hazardStart,
                    hazardMultiplier);
                if (mortalityRandom.NextDouble() < stepProbability)
                {
                    KillCitizen(citizenID, ref citizens[realCitizenId], hazardEnd);
                    i--;
                }
            }
        }

        private double GetConfiguredMortalityProbability(Citizen.AgeGroup ageGroup)
        {
            switch (ageGroup)
            {
                case Citizen.AgeGroup.Child:
                    return Config.DeathChild / 100d;
                case Citizen.AgeGroup.Teen:
                    return Config.DeathTeen / 100d;
                case Citizen.AgeGroup.Young:
                    return Config.DeathYoung / 100d;
                case Citizen.AgeGroup.Adult:
                    return Config.DeathAdult / 100d;
                case Citizen.AgeGroup.Senior:
                    return Config.DeathSenior / 100d;
                default:
                    return 0d;
            }
        }

        /// <summary>
        /// Gets a reference to the citizen manager proxy object.
        /// </summary>
        private ICitizenManagerConnection<Citizen> CitizenMgr { get; set; }

        /// <summary>
        /// Gets a reference to the proxy class that provides access to citizen's methods and fields.
        /// </summary>
        private ICitizenConnection<Citizen> CitizenProxy { get; set; }

        /// <summary>
        /// Gets a reference to the proxy class that provides access to buildings methods and fields.
        /// </summary>
        private IBuildingManagerConnection BuildingMgr { get; set; }
        
        /// <summary>
        /// Gets or sets the configuration.
        /// </summary>
        private Config.RealTimeConfig Config { get; set; }

        private void OnDestroy()
        {
            // Unity destroys the previous component asynchronously during level reload. Never let that
            // stale callback clear the freshly-created manager or its process-wide service state.
            if (!ReferenceEquals(Instance, this))
            {
                return;
            }

            CitizenManagerPatch.CitizenReleased = null;

            StopStepSynchronization();

            try
            {
                RestorePublicTransportService();
            }
            catch (Exception ex)
            {
                Log.Warning("The 'Real Time' pandemic manager could not restore public transport while unloading: " + ex);
            }

            try
            {
                experimentRecorder.Dispose();
            }
            catch (Exception ex)
            {
                // An interrupted writer must not prevent releasing level-owned singleton state.
                // Its temporary files remain incomplete and are never published as a successful run.
                Log.Warning("The 'Real Time' pandemic manager could not close scientific contact data while unloading: " + ex);
            }
            ContactManager.Instance.ResetForLevelUnload();
            TestManager.Instance.ResetForLevelUnload();
            QuarantineManager.Instance.ResetForLevelUnload();
            runContext = null;
            Config = null;
            CitizenMgr = null;
            CitizenProxy = null;
            BuildingMgr = null;
            simulation = null;
            Observer = null;
            Instance = null;
        }

        public static PandemicManager Instance { get; set; }
    }
}
