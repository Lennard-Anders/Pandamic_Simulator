
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using RealTime.CustomAI;
using RealTime.GameConnection;
using SkyTools.Tools;
using UnityEngine;

namespace RealTime.Pandemic
{
    public class PandemicManager : MonoBehaviour
    {
        private bool QUICK_SIMULATION = true;
        private bool ACTIVE_ONLY = true;
        private bool SET_SICK_FLAG = false;

        private System.Random random = new System.Random(0);
        private bool active = false;
        private bool hasStartedAtLeastOnce;
        private bool worldOverlaysEnabled = true;
        private PandemicLifecycleState lifecycleState = PandemicLifecycleState.Dormant;
        private bool xRayEnabled;
        private PandemicXRayMetric xRayMetric = PandemicXRayMetric.Infected;
        private PandemicXRayLocationMode xRayLocationMode = PandemicXRayLocationMode.LivePositions;
        private float spreadDistance = 10;
        private DateTime lastDateTime;
        private DateTime lastDateTimeCitizensUpdate;
        private DateTime lastStoreTime;
        private DateTime currentDateTime;
        private DateTime pandemicRunStartedAt;
        private DateTime pandemicRunFinishedAt;
        private bool hadAnySickCitizens;

        private List<uint> initialPopulation = new List<uint>();
        private List<uint> initialPopulationHealthy = new List<uint>();
        private List<uint> initialPopulationRecovered = new List<uint>();
        private List<uint> initialPopulationDead = new List<uint>();
        private List<uint> initialPopulationSick = new List<uint>();

        private List<uint> removedCitizens = new List<uint>();
        private HashSet<uint> usedCitizens = new HashSet<uint>();
        private Dictionary<uint, uint> citizenMatching = new Dictionary<uint, uint>();

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

        // Quarantine fate: citizen → fate timestamp (ms), and whether they die
        private Dictionary<uint, long> quarantineFateTimestampMs = new Dictionary<uint, long>();
        private HashSet<uint> quarantineFatedToDie = new HashSet<uint>();
        private const int QuarantineFateDays = 14;
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
        private readonly Dictionary<PandemicLockdownFamily, PandemicFamilyExposure> lockdownFamilyExposure = new Dictionary<PandemicLockdownFamily, PandemicFamilyExposure>();
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
        private readonly Dictionary<int, List<PandemicCitizenTickState>> outdoorHealthyByCell = new Dictionary<int, List<PandemicCitizenTickState>>();
        private readonly Dictionary<int, List<PandemicCitizenTickState>> outdoorInfectiousByCell = new Dictionary<int, List<PandemicCitizenTickState>>();
        private readonly Dictionary<ushort, List<PandemicCitizenTickState>> buildingHealthyOccupants = new Dictionary<ushort, List<PandemicCitizenTickState>>();
        private readonly Dictionary<ushort, List<PandemicCitizenTickState>> buildingInfectiousOccupants = new Dictionary<ushort, List<PandemicCitizenTickState>>();
        private readonly Dictionary<ushort, List<PandemicCitizenTickState>> vehicleHealthyOccupants = new Dictionary<ushort, List<PandemicCitizenTickState>>();
        private readonly Dictionary<ushort, List<PandemicCitizenTickState>> vehicleInfectiousOccupants = new Dictionary<ushort, List<PandemicCitizenTickState>>();
        private readonly Dictionary<uint, ushort> lastObservedBuildingByCitizen = new Dictionary<uint, ushort>();
        private readonly Dictionary<uint, long> buildingEntryTimestampMsByCitizen = new Dictionary<uint, long>();
        private readonly Dictionary<uint, long> homeDepartureIntentTimestampMsByCitizen = new Dictionary<uint, long>();
        private readonly HashSet<uint> pendingTransmissionIds = new HashSet<uint>();
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
        private readonly long UPDATE_INTERVAL_MINUTES = 5;
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

        private sealed class PandemicFamilyExposure
        {
            public int TotalCitizens;
            public int InfectedCitizens;
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
            public bool ShouldQuarantine;
            public bool IsKnownSick;
            public bool AtHome;
            public ushort TargetBuilding;
            public ushort TargetNode;
            public bool IsInResidentialSharedAreaWindow;
            public int OutdoorCellX;
            public int OutdoorCellZ;
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
            EnsureRuntimeConfigDefaults();

            bool initialized = CitizenMgr != null && CitizenProxy != null && BuildingMgr != null;
            active = false;
            lifecycleState = PandemicLifecycleState.Dormant;
            pandemicRunStartedAt = default(DateTime);
            pandemicRunFinishedAt = default(DateTime);
            if (!initialized)
            {
                Log.Warning("The 'Real Time' pandemic manager could not be activated because one or more game connections are missing.");
                return;
            }

            ContactManager.Instance.Init(config);
        }

        public long GetInfectionDate(uint citizenID)
        {
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
            if (lifecycleState == PandemicLifecycleState.Running)
            {
                return true;
            }

            BootstrapSimulation(QuarantineManager.Instance.InLockDown);
            return lifecycleState == PandemicLifecycleState.Running;
        }

        public bool RestartSimulation()
        {
            bool lockdownEnabled = QuarantineManager.Instance.InLockDown;
            BootstrapSimulation(lockdownEnabled);
            return lifecycleState == PandemicLifecycleState.Running;
        }

        public void StopPandemic()
        {
            if (lifecycleState == PandemicLifecycleState.Dormant)
            {
                return;
            }

            try
            {
                RestorePublicTransportService();

                // Heal all currently sick citizens.
                Citizen[] citizens = CitizenMgr.GetCitizensArray();
                foreach (uint citizenId in initialPopulationSick)
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
                currentDateTime = simulation.m_currentGameTime;
                pandemicRunStartedAt = currentDateTime;
                pandemicRunFinishedAt = default(DateTime);
                SeedPolicyTimeline();

                Citizen[] citizens = CitizenMgr.GetCitizensArray();

                var infectionCandidates = new List<uint>();

                for (uint i = 0; i < citizens.Length; i++)
                {
                    if (CitizenProxy.IsEmpty(ref citizens[i]) || CitizenProxy.IsDead(ref citizens[i]) || CitizenProxy.GetHomeBuilding(ref citizens[i]) == 0)
                    {
                        continue;
                    }

                    initialPopulation.Add(i);
                    usedCitizens.Add(i);

                    if (CitizenProxy.IsSick(ref citizens[i]))
                    {
                        CitizenProxy.SetSick(ref citizens[i], false);
                    }

                    initialPopulationHealthy.Add(i);
                    if (CitizenProxy.GetLocation(ref citizens[i]) == Citizen.Location.Home)
                    {
                        infectionCandidates.Add(i);
                    }
                }

                float infectionRatio = Config.DiseaseStartInfectionRatio;
                uint intendedNumberOfSickCitizens = Math.Max((uint)(infectionRatio * (initialPopulationSick.Count + initialPopulationHealthy.Count) / 100f), 1);

                Debug.Log("Intended number of sick citizens: " + intendedNumberOfSickCitizens + ", actual number of sick citizens: " + initialPopulationSick.Count + ", number of healthy citizens: " + initialPopulationHealthy.Count + ", infection candidates: " + infectionCandidates);

                while (intendedNumberOfSickCitizens > initialPopulationSick.Count && initialPopulationHealthy.Count > 0)
                {
                    // Sickening citizens
                    uint citizenID;
                    if (infectionCandidates.Count > 0)
                    {
                        int index = random.Next(infectionCandidates.Count);
                        citizenID = infectionCandidates[index];
                        infectionCandidates.RemoveAt(index);
                        initialPopulationHealthy.Remove(citizenID);
                    }
                    else
                    {
                        int index = random.Next(initialPopulationHealthy.Count);
                        citizenID = initialPopulationHealthy[index];
                        initialPopulationHealthy.RemoveAt(index);
                    }

                    int infectionOffsetRange = (int)Math.Max(Config.StartInfection - 1, 0) * 24 * 3600 * 1000;
                    int offset = infectionOffsetRange > 0 ? random.Next(infectionOffsetRange) : 0;
                    InfectCitizen(citizenID, ref citizens[retrieveID(citizenID)], -offset);
                    PandemicInfectionOriginInfo seedOrigin = CreateSeedOrigin(ref citizens[retrieveID(citizenID)]);
                    RecordInfectionOrigin(citizenID, seedOrigin);
                    Observer.AddCitizenInfection(0, citizenID, currentDateTime.AddMilliseconds(-offset), seedOrigin);
                }

                if (initialPopulation.Count == 0)
                {
                    Log.Warning("The 'Real Time' pandemic manager found no eligible citizens yet; initialization will be retried.");
                    startCompleted = false;
                    return;
                }

                if (initialPopulationSick.Count == 0)
                {
                    Log.Warning("The 'Real Time' pandemic manager could not seed initial infections yet; initialization will be retried.");
                    startCompleted = false;
                    return;
                }

                Debug.Log("Intended number of sick citizens: " + intendedNumberOfSickCitizens + ", actual number of sick citizens: " + initialPopulationSick.Count + ", number of healthy citizens: " + initialPopulationHealthy.Count);

                lastDateTime = simulation.m_currentGameTime;
                lastDateTimeCitizensUpdate = simulation.m_currentGameTime;
                lastStoreTime = simulation.m_currentGameTime;
                hadAnySickCitizens = true;

                Observer.AddSickCitizens(currentDateTime, initialPopulationSick.Count);
                Observer.AddHealthyCitizens(currentDateTime, initialPopulationHealthy.Count);
                Observer.AddRecoveredCitizens(currentDateTime, initialPopulationRecovered.Count);
                Observer.AddDeadCitizens(currentDateTime, initialPopulationDead.Count);
                Observer.FinishObservations(currentDateTime);
                CaptureHealthcareUsageSample(currentDateTime, citizens);

                try
                {
                    Observer.WriteToDisc(true);
                    ContactManager.Instance.WriteToDisk();
                }
                catch (Exception ex)
                {
                    Log.Warning("The 'Real Time' pandemic manager failed to persist initial observer output: " + ex);
                }

                TestManager.Instance.Init(Config, initialPopulation.Count, currentDateTime);
                QuarantineManager.Instance.InLockDown = lockdownEnabled;
                active = true;
                startCompleted = true;
                hasStartedAtLeastOnce = true;
                lifecycleState = PandemicLifecycleState.Running;
                BuildPandemicBuildingSets();
                UpdatePublicTransportShutdownState(forceRefresh: true);
            }
            catch (Exception ex)
            {
                Log.Error("The 'Real Time' pandemic manager failed to start: " + ex);
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

            EnsureRuntimeConfigDefaults();
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
            if (publicTransportShutdownState != PandemicPublicTransportShutdownState.Draining
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
            if (publicTransportShutdownState == PandemicPublicTransportShutdownState.Open
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

        public void Update()
        {
            long tickStart = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                UpdatePublicTransportShutdownState();
            }
            catch (Exception ex)
            {
                Log.Warning("The 'Real Time' pandemic manager failed to update public transport shutdown state: " + ex);
            }

            if (!IsReadyForUpdate())
            {
                return;
            }

            bool forcedPauseApplied = false;
            try
            {
                DateTime tempDateTime = simulation.m_currentGameTime;
                if ((tempDateTime.Ticks - lastDateTime.Ticks) / TimeSpan.TicksPerMinute < UPDATE_INTERVAL_MINUTES)
                {
                    return;
                }

                currentDateTime = tempDateTime;
                simulation.ForcedSimulationPaused = true;
                forcedPauseApplied = true;

                long milliseconds = (currentDateTime.Ticks - lastDateTime.Ticks) / TimeSpan.TicksPerMillisecond;

                try
                {
                    Masks?.SetStepLengthInHours(milliseconds / (3600 * 1000.0));
                }
                catch (Exception ex)
                {
                    Log.Warning("The 'Real Time' pandemic manager could not update mask probabilities: " + ex);
                }

                double symptomPhaseDays = Math.Max(1.0, Config.DiseaseDuration - Config.StartSymptoms);
                double childDeathProbability = 1 - Math.Pow(1 - Config.DeathChild / 100.0, milliseconds / (symptomPhaseDays * 24 * 3600 * 1000.0));
                double teenDeathProbability = 1 - Math.Pow(1 - Config.DeathTeen / 100.0, milliseconds / (symptomPhaseDays * 24 * 3600 * 1000.0));
                double youngDeathProbability = 1 - Math.Pow(1 - Config.DeathYoung / 100.0, milliseconds / (symptomPhaseDays * 24 * 3600 * 1000.0));
                double adultDeathProbability = 1 - Math.Pow(1 - Config.DeathAdult / 100.0, milliseconds / (symptomPhaseDays * 24 * 3600 * 1000.0));
                double seniorDeathProbability = 1 - Math.Pow(1 - Config.DeathSenior / 100.0, milliseconds / (symptomPhaseDays * 24 * 3600 * 1000.0));

                try
                {
                    kill(childDeathProbability, teenDeathProbability, youngDeathProbability, adultDeathProbability, seniorDeathProbability);
                }
                catch (Exception ex)
                {
                    Log.Warning("The 'Real Time' pandemic manager failed during death simulation: " + ex);
                }

                Citizen[] citizens = CitizenMgr.GetCitizensArray();
                SyncSymptomStates(citizens);

                if (ACTIVE_ONLY)
                {
                    if ((tempDateTime.Ticks - lastDateTimeCitizensUpdate.Ticks) / TimeSpan.TicksPerMinute >= CITIZENS_UPDATE_INTERVAL_MINUTES)
                    {
                        lastDateTimeCitizensUpdate = tempDateTime;
                        citizens = CitizenMgr.GetCitizensArray();

                        for (int i = 0; i < initialPopulation.Count; i++)
                        {
                            if (CitizenProxy.IsEmpty(ref citizens[retrieveID(initialPopulation[i])]))
                            {
                                if (!initialPopulationDead.Contains(initialPopulation[i]) && !initialPopulationRecovered.Contains(initialPopulation[i]))
                                {
                                    if (!removedCitizens.Contains(initialPopulation[i]))
                                    {
                                        removedCitizens.Add(initialPopulation[i]);
                                    }
                                }

                                usedCitizens.Remove(retrieveID(initialPopulation[i]));
                                citizenMatching.Remove(initialPopulation[i]);
                                initialPopulation.RemoveAt(i);
                                i--;
                            }
                        }

                        for (uint i = 0; i < citizens.Length && removedCitizens.Count > 0; i++)
                        {
                            if (!CitizenProxy.IsEmpty(ref citizens[i]) && CitizenProxy.GetHomeBuilding(ref citizens[i]) != 0 && !usedCitizens.Contains(i))
                            {
                                uint removedCitizen = removedCitizens[0];
                                removedCitizens.RemoveAt(0);

                                usedCitizens.Add(i);
                                citizenMatching[removedCitizen] = i;
                                initialPopulation.Add(removedCitizen);

                            }
                        }
                    }
                }

                try
                {
                    ProcessQuarantineFates();
                }
                catch (Exception ex)
                {
                    Log.Warning("The 'Real Time' pandemic manager failed during quarantine fate processing: " + ex);
                }

                try
                {
                    spread();
                }
                catch (Exception ex)
                {
                    Log.Warning("The 'Real Time' pandemic manager failed during disease spread simulation: " + ex);
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
                    Log.Warning("The 'Real Time' pandemic manager failed to capture healthcare usage data: " + ex);
                }

                try
                {
                    Observer.AddSickCitizens(currentDateTime, initialPopulationSick.Count);
                    Observer.AddHealthyCitizens(currentDateTime, initialPopulationHealthy.Count);
                    Observer.AddRecoveredCitizens(currentDateTime, initialPopulationRecovered.Count);
                    Observer.AddDeadCitizens(currentDateTime, initialPopulationDead.Count);
                    Observer.FinishObservations(currentDateTime);

                }
                catch (Exception ex)
                {
                    Log.Warning("The 'Real Time' pandemic manager observer update failed: " + ex);
                }

                lastDateTime = currentDateTime;

                if ((tempDateTime.Ticks - lastStoreTime.Ticks) / TimeSpan.TicksPerMinute >= STORE_INTERVAL_MINUTES)
                {
                    try
                    {
                        Observer.WriteToDisc(true);
                        ContactManager.Instance.WriteToDisk();
                    }
                    catch (Exception ex)
                    {
                        Log.Warning("The 'Real Time' pandemic manager failed to persist periodic observer output: " + ex);
                    }

                    lastStoreTime = tempDateTime;
                }

                if (Observer.GetSickCitizens() == 0 && hadAnySickCitizens)
                {
                    try
                    {
                        Observer.WriteToDisc(false);
                        ContactManager.Instance.WriteToDisk();
                    }
                    catch (Exception ex)
                    {
                        Log.Warning("The 'Real Time' pandemic manager failed to persist final observer output: " + ex);
                    }

                    // No active infections left. Stop pandemic processing, but never pause the whole simulation.
                    active = false;
                    lifecycleState = PandemicLifecycleState.Finished;
                    pandemicRunFinishedAt = currentDateTime;
                }
            }
            catch (Exception ex)
            {
                Log.Error("The 'Real Time' pandemic manager update failed unexpectedly and was recovered: " + ex);
            }
            finally
            {
                RecordUpdateDuration(tickStart);
                if (forcedPauseApplied && simulation != null)
                {
                    simulation.ForcedSimulationPaused = false;
                }
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
            int sick = initialPopulationSick.Count;
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
            ClearTickStateBuckets(outdoorHealthyByCell);
            ClearTickStateBuckets(outdoorInfectiousByCell);
            ClearTickStateBuckets(buildingHealthyOccupants);
            ClearTickStateBuckets(buildingInfectiousOccupants);
            ClearTickStateBuckets(vehicleHealthyOccupants);
            ClearTickStateBuckets(vehicleInfectiousOccupants);
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

        private void ProcessCitizenTesting(Citizen[] citizens)
        {
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
        }

        private void ProcessRecoveries(Citizen[] citizens)
        {
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

                long infectedTime = (currentDateTime.Ticks / 10000) - activeInfections[citizenId];
                double infectedTimeInDays = infectedTime / 1000.0 / 3600.0 / 24.0;
                if (infectedTimeInDays > Config.DiseaseDuration)
                {
                    HealCitizen(citizenId, ref citizens[realId]);
                    i--;
                }
            }
        }

        private void ProcessContactTracingCandidates()
        {
            if (Config.QuarantineBehavior != RealTime.Config.QuarantineBehavior.Contacts
                && Config.QuarantineBehavior != RealTime.Config.QuarantineBehavior.Family)
            {
                return;
            }

            for (int i = 0; i < initialPopulationSick.Count; i++)
            {
                uint citizenId = initialPopulationSick[i];
                if (!activeInfections.ContainsKey(citizenId))
                {
                    continue;
                }

                if (ShouldBeInQuarantine(citizenId))
                {
                    CheckForContacts(citizenId, Config.DiseaseDuration, Config.QuarantineBehavior);
                }
            }
        }

        private void BuildCitizenTickStateIndex(Citizen[] citizens)
        {
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
                state.IsInfected = activeInfections.ContainsKey(citizenId);
                state.IsInfectious = state.IsInfected && IsInfectious(citizenId);
                state.IsKnownSick = state.IsInfected && IsKnownSick(citizenId);
                state.ShouldQuarantine = ShouldBeInQuarantine(citizenId);
                state.AtHome = state.CurrentBuilding != 0 && state.HomeBuilding != 0 && state.CurrentBuilding == state.HomeBuilding;
                state.TargetBuilding = state.InstanceId != 0 ? CitizenMgr.GetTargetBuilding(state.InstanceId) : (ushort)0;
                state.TargetNode = state.InstanceId != 0 ? CitizenMgr.GetTargetNode(state.InstanceId) : (ushort)0;
                state.IsInResidentialSharedAreaWindow = UpdateResidentialSharedAreaWindow(state, nowMs);
                state.Position = GetContactPosition(state, ref citizen);
                state.OutdoorCellX = GetOutdoorCellCoordinate(state.Position.x, outdoorCellSize);
                state.OutdoorCellZ = GetOutdoorCellCoordinate(state.Position.z, outdoorCellSize);
                citizenTickStates.Add(state);

                if (SET_SICK_FLAG && state.IsInfected && !CitizenProxy.IsSick(ref citizen))
                {
                    CitizenProxy.SetSick(ref citizen, true);
                }

                if (state.IsInfectious)
                {
                    Observer.AddInfectiousCitizen(citizenId);
                }

                if (state.CurrentBuilding != 0)
                {
                    ItemClass.Service currentService = BuildingMgr.GetBuildingService(state.CurrentBuilding);
                    ItemClass.SubService currentSubService = BuildingMgr.GetBuildingSubService(state.CurrentBuilding);
                    PandemicLockdownFamily family = PandemicTaxonomy.GetBuildingFamily(currentService, currentSubService);
                    PandemicFamilyExposure familyExposure = GetOrCreateFamilyExposure(family);
                    familyExposure.TotalCitizens++;
                    if (state.IsInfected)
                    {
                        familyExposure.InfectedCitizens++;
                    }
                }

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

                if (state.IsInfected && state.ShouldQuarantine
                    && (Config.QuarantineBehavior == RealTime.Config.QuarantineBehavior.Contacts
                        || Config.QuarantineBehavior == RealTime.Config.QuarantineBehavior.Family))
                {
                    continue;
                }

                if (state.ShouldQuarantine)
                {
                    continue;
                }

                if (state.VehicleId != 0)
                {
                    AddTickStateToBucket(state.IsInfected ? vehicleInfectiousOccupants : vehicleHealthyOccupants, state.VehicleId, state);
                    continue;
                }

                if (state.CurrentBuilding != 0)
                {
                    AddTickStateToBucket(state.IsInfected ? buildingInfectiousOccupants : buildingHealthyOccupants, state.CurrentBuilding, state);
                    continue;
                }

                if (state.InstanceId != 0 && state.Position != Vector3.zero)
                {
                    int cellKey = GetOutdoorCellKey(state.OutdoorCellX, state.OutdoorCellZ);
                    AddTickStateToBucket(state.IsInfected ? outdoorInfectiousByCell : outdoorHealthyByCell, cellKey, state);
                }
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
            infectedBuildingIds.Clear();
            quarantineBuildingIds.Clear();
            hotspotBuildingIds.Clear();
            hubBuildingIds.Clear();
            buildingInfectedCounts.Clear();
            buildingCurrentInfectedCounts.Clear();
            ResetLockdownFamilyExposure();

            if (!IsVisualizationDataAvailable())
            {
                return;
            }

            Citizen[] citizens = CitizenMgr.GetCitizensArray();
            if (citizens == null)
            {
                return;
            }

            BuildCitizenTickStateIndex(citizens);

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
                uint realId = retrieveID(citizenId);
                if (realId >= citizens.Length)
                {
                    continue;
                }

                ref Citizen c = ref citizens[realId];
                if (CitizenProxy.IsEmpty(ref c) || CitizenProxy.IsDead(ref c))
                {
                    continue;
                }

                ushort home = CitizenProxy.GetHomeBuilding(ref c);
                if (home != 0)
                {
                    quarantineBuildingIds.Add(home);
                }
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
            return Masks?.IsWearingMask(citizenId) ?? false;
        }

        public bool IsMasksEnabled() => Config != null && Config.MaskBehavior != RealTime.Config.MaskBehavior.None;

        public bool IsQuarantineEnabled() => Config != null && Config.QuarantineBehavior != RealTime.Config.QuarantineBehavior.None;

        internal DateTime GetPandemicRunStartedAt() => pandemicRunStartedAt;

        internal IList<PandemicObservation> GetAllObservations() =>
            Observer?.GetObservations() ?? new List<PandemicObservation>();

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

        /// <summary>Forces the mask state for a single citizen, overriding random assignment.</summary>
        public void ForceSetCitizenMask(uint citizenId, bool masked)
        {
            Masks?.SetMaskForCitizen(citizenId, masked);
        }

        /// <summary>Toggles quarantine for a single citizen. Adding quarantine also schedules a 14-day fate.</summary>
        public void ToggleCitizenQuarantine(uint citizenId)
        {
            if (simulation != null)
            {
                currentDateTime = simulation.m_currentGameTime;
            }

            DateTime now = currentDateTime;
            if (QuarantineManager.Instance.IsInQuarantine(citizenId, now))
            {
                QuarantineManager.Instance.RemoveCitizenInQuarantine(citizenId);
                quarantineFateTimestampMs.Remove(citizenId);
                quarantineFatedToDie.Remove(citizenId);
            }
            else
            {
                QuarantineManager.Instance.AddCitizenInQuarantine(citizenId, now);
                ScheduleQuarantineFate(citizenId);
            }
        }

        public bool ToggleMasks()
        {
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
            if (Config == null) return false;
            Config.QuarantineBehavior = Config.QuarantineBehavior == RealTime.Config.QuarantineBehavior.None
                ? RealTime.Config.QuarantineBehavior.Contacts
                : RealTime.Config.QuarantineBehavior.None;
            return IsQuarantineEnabled();
        }

        public bool ToggleLockdown()
        {
            QuarantineManager.Instance.InLockDown = !QuarantineManager.Instance.InLockDown;
            bool enabled = QuarantineManager.Instance.InLockDown;
            RecordPolicyTimelineEntry(PandemicPolicyMarkerType.Lockdown, enabled);
            UpdatePublicTransportShutdownState(forceRefresh: true);
            return enabled;
        }

        public bool ToggleWorldOverlays()
        {
            worldOverlaysEnabled = !worldOverlaysEnabled;
            return worldOverlaysEnabled;
        }

        internal bool ToggleXRayEnabled()
        {
            xRayEnabled = !xRayEnabled;
            return xRayEnabled;
        }

        internal PandemicXRayMetric CycleXRayMetric()
        {
            switch (xRayMetric)
            {
                case PandemicXRayMetric.Infected:
                    xRayMetric = PandemicXRayMetric.Recovered;
                    break;

                case PandemicXRayMetric.Recovered:
                    xRayMetric = PandemicXRayMetric.Dead;
                    break;

                default:
                    xRayMetric = PandemicXRayMetric.Infected;
                    break;
            }

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

            if (Config == null)
            {
                return;
            }

            RecordPolicyTimelineEntry(PandemicPolicyMarkerType.Masks, IsMasksEnabled(), force: true);
            RecordPolicyTimelineEntry(PandemicPolicyMarkerType.Lockdown, QuarantineManager.Instance.InLockDown, force: true);
        }

        private void RecordPolicyTimelineEntry(PandemicPolicyMarkerType type, bool enabled, bool force = false)
        {
            if (!force && lifecycleState != PandemicLifecycleState.Running)
            {
                return;
            }

            DateTime timestamp = simulation != null ? simulation.m_currentGameTime : currentDateTime;
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
            return infectionOrigins.TryGetValue(citizenId, out origin);
        }

        public void ManuallyInfectCitizen(uint citizenId)
        {
            if (activeInfections.ContainsKey(citizenId)) return;
            if (CitizenMgr == null || CitizenProxy == null) return;
            Citizen[] citizens = CitizenMgr.GetCitizensArray();
            uint realId = retrieveID(citizenId);
            if (realId >= citizens.Length) return;
            ref Citizen citizen = ref citizens[realId];
            if (CitizenProxy.IsEmpty(ref citizen) || CitizenProxy.IsDead(ref citizen)) return;
            InfectCitizen(citizenId, ref citizen);
            RecordInfectionOrigin(citizenId, CreateOutdoorOrigin(CitizenMgr.GetCitizenPosition(CitizenProxy.GetInstance(ref citizen))));
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
            snapshot.SimulationTime = simulation != null ? simulation.m_currentGameTime : currentDateTime;
            snapshot.PandemicDay = GetPandemicDayNumber();
            snapshot.Healthy = initialPopulationHealthy.Count;
            snapshot.Sick = initialPopulationSick.Count;
            snapshot.Recovered = initialPopulationRecovered.Count;
            snapshot.Dead = initialPopulationDead.Count;
            snapshot.DeltaSick = 0;
            snapshot.DeltaRecovered = 0;
            snapshot.DeltaDead = 0;
            snapshot.QuarantineCitizens = QuarantineManager.Instance.CitizensInQuarantine();
            snapshot.PositiveTests = TestManager.Instance.GetCurrentPositiveCount(snapshot.SimulationTime);
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
                    snapshot.Recovered = (int)latest.RecoveredCitizens;
                    snapshot.Dead = (int)latest.DeadCitizens;
                }

                if (Observer.TryGetLatestObservation(out latest) && Observer.TryGetPreviousObservation(out var previous))
                {
                    snapshot.DeltaSick = (int)latest.SickCitizens - (int)previous.SickCitizens;
                    snapshot.DeltaRecovered = (int)latest.RecoveredCitizens - (int)previous.RecoveredCitizens;
                    snapshot.DeltaDead = (int)latest.DeadCitizens - (int)previous.DeadCitizens;
                }
            }

            snapshot.TrackedPopulation = Math.Max(0, snapshot.Healthy + snapshot.Sick + snapshot.Recovered + snapshot.Dead);
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

            if (simulation != null)
            {
                return simulation.m_currentGameTime;
            }

            return currentDateTime;
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

            DateTime cutoff = simulationTime.AddDays(-7);
            healthcareUsageSamples.RemoveAll(entry => entry == null || entry.SimulationTime < cutoff);
        }

        private PandemicHealthcareUsageSample CalculateHealthcareUsageSample(DateTime simulationTime, Citizen[] citizens)
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
                || liveSnapshotCache.AgeGroups.Count == 0
                || liveSnapshotCache.Districts.Count == 0
                || now >= nextAnalyticsSnapshotRefreshTime
                || cachedAnalyticsInfectionCount != totalInfections;
        }

        private bool ShouldRefreshSuperspreaderSnapshot(float now, int totalInfections)
        {
            return liveSnapshotCache == null
                || liveSnapshotCache.TopSpreaders.Count == 0
                || now >= nextSuperspreaderSnapshotRefreshTime
                || cachedSuperspreaderInfectionCount != totalInfections;
        }

        private void RefreshChartSnapshot(PandemicLiveSnapshot snapshot)
        {
            if (snapshot == null || Observer == null)
            {
                return;
            }

            IList<PandemicObservation> observations = Observer.GetObservationView();
            if (observations == null)
            {
                return;
            }

            if (cachedChartObservationCount > observations.Count || snapshot.ChartPoints.Count > observations.Count)
            {
                snapshot.ChartPoints.Clear();
                cachedChartObservationCount = 0;
                chartVersion++;
            }

            while (snapshot.ChartPoints.Count < observations.Count)
            {
                PandemicObservation observation = observations[snapshot.ChartPoints.Count];
                snapshot.ChartPoints.Add(new PandemicChartPointSnapshot
                {
                    SimulationTime = observation.SimulationTime,
                    InfectedCount = (int)observation.SickCitizens,
                });
                chartVersion++;
            }

            if (observations.Count > 0 && snapshot.ChartPoints.Count == observations.Count)
            {
                PandemicObservation latestObservation = observations[observations.Count - 1];
                PandemicChartPointSnapshot latestPoint = snapshot.ChartPoints[snapshot.ChartPoints.Count - 1];
                if (latestPoint.SimulationTime != latestObservation.SimulationTime || latestPoint.InfectedCount != (int)latestObservation.SickCitizens)
                {
                    latestPoint.SimulationTime = latestObservation.SimulationTime;
                    latestPoint.InfectedCount = (int)latestObservation.SickCitizens;
                    chartVersion++;
                }
            }

            cachedChartObservationCount = observations.Count;
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
            int totalInfected = 0;

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
                        if (CitizenProxy.IsEmpty(ref citizen) || CitizenProxy.IsDead(ref citizen))
                        {
                            continue;
                        }

                        bool infected = activeInfections.ContainsKey(citizenId);
                        if (infected)
                        {
                            int ageGroupIndex = (int)CitizenProxy.GetAge(ref citizen);
                            if (ageGroupIndex >= 0 && ageGroupIndex < ageGroupCounts.Length)
                            {
                                ageGroupCounts[ageGroupIndex]++;
                            }

                            totalInfected++;
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
                    InfectedPercent = totalInfected > 0 ? (count * 100f) / totalInfected : 0f,
                });
            }

            foreach (PandemicDistrictSnapshot districtSnapshot in districtSnapshots.Values)
            {
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

        private void ResetLockdownFamilyExposure()
        {
            lockdownFamilyExposure.Clear();
            foreach (PandemicLockdownFamily family in LockdownFamilies)
            {
                lockdownFamilyExposure[family] = new PandemicFamilyExposure();
            }
        }

        private PandemicFamilyExposure GetOrCreateFamilyExposure(PandemicLockdownFamily family)
        {
            if (!lockdownFamilyExposure.TryGetValue(family, out PandemicFamilyExposure exposure))
            {
                exposure = new PandemicFamilyExposure();
                lockdownFamilyExposure[family] = exposure;
            }

            return exposure;
        }

        private float GetCurrentFamilyInfectedPercent(PandemicLockdownFamily family)
        {
            PandemicFamilyExposure exposure = GetOrCreateFamilyExposure(family);
            return exposure.TotalCitizens > 0
                ? (exposure.InfectedCitizens * 100f) / exposure.TotalCitizens
                : 0f;
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

            float autoThreshold = GetFamilyAutoThreshold(family);
            return autoThreshold <= 0f || GetCurrentFamilyInfectedPercent(family) < autoThreshold;
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

            const float mapHalfSize = 8640f;
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
            active = false;
            startCompleted = false;
            initialPopulation.Clear();
            initialPopulationHealthy.Clear();
            initialPopulationRecovered.Clear();
            initialPopulationDead.Clear();
            initialPopulationSick.Clear();
            removedCitizens.Clear();
            usedCitizens.Clear();
            citizenMatching.Clear();
            activeInfections.Clear();
            infectedCitizens.Clear();
            infectedCitizensWithSymptoms.Clear();
            symptomHospitalHandledCitizens.Clear();
            infectedBuildingIds.Clear();
            quarantineBuildingIds.Clear();
            hotspotBuildingIds.Clear();
            hubBuildingIds.Clear();
            buildingInfectedCounts.Clear();
            buildingCurrentInfectedCounts.Clear();
            quarantineFateTimestampMs.Clear();
            quarantineFatedToDie.Clear();
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
              ResetLockdownFamilyExposure();
              lastDateTime = default;
            lastDateTimeCitizensUpdate = default;
            lastStoreTime = default;
            currentDateTime = default;
            hadAnySickCitizens = false;
        }

        private void EnsureRuntimeConfigDefaults()
        {
            if (Config == null)
            {
                return;
            }

            bool changed = false;

            if (Config.DiseaseDuration == 0)
            {
                Config.DiseaseDuration = 14;
                changed = true;
            }

            if (Config.StartInfection == 0 && Config.EndInfection == 0)
            {
                Config.StartInfection = 1;
                Config.EndInfection = Math.Min(10u, Config.DiseaseDuration);
                changed = true;
            }

            if (Config.EndInfection == 0)
            {
                Config.EndInfection = Math.Min(10u, Config.DiseaseDuration);
                changed = true;
            }

            if (Config.EndInfection > Config.DiseaseDuration)
            {
                Config.EndInfection = Config.DiseaseDuration;
                changed = true;
            }

            if (Config.StartInfection > Config.EndInfection)
            {
                Config.StartInfection = Config.EndInfection;
                changed = true;
            }

            if (Config.StartSymptoms == 0 && Config.EndSymptoms == 0)
            {
                Config.StartSymptoms = Math.Min(3u, Config.DiseaseDuration);
                Config.EndSymptoms = Config.DiseaseDuration;
                changed = true;
            }

            if (Config.EndSymptoms == 0)
            {
                Config.EndSymptoms = Config.DiseaseDuration;
                changed = true;
            }

            if (Config.EndSymptoms > Config.DiseaseDuration)
            {
                Config.EndSymptoms = Config.DiseaseDuration;
                changed = true;
            }

            if (Config.StartSymptoms > Config.EndSymptoms)
            {
                Config.StartSymptoms = Config.EndSymptoms;
                changed = true;
            }

            if (Config.IndoorDiseaseTransmissionProbability <= 0f)
            {
                Config.IndoorDiseaseTransmissionProbability = 1.5f;
                changed = true;
            }

            if (Config.OutdoorDiseaseTransmissionProbability <= 0f)
            {
                Config.OutdoorDiseaseTransmissionProbability = 0.3f;
                changed = true;
            }

            if (Config.DiseaseTransmissionRange <= 0f)
            {
                Config.DiseaseTransmissionRange = 1.5f;
                changed = true;
            }

            if (Config.DiseaseStartInfectionRatio <= 0f)
            {
                Config.DiseaseStartInfectionRatio = 5f;
                changed = true;
            }

            if (Config.SymptomProbability <= 0f)
            {
                Config.SymptomProbability = 50f;
                changed = true;
            }

            if (Config.HubHighlightThreshold < 2)
            {
                Config.HubHighlightThreshold = 6;
                changed = true;
            }

            if (Config.SuperspreaderCitizenThreshold < 2)
            {
                Config.SuperspreaderCitizenThreshold = 5;
                changed = true;
            }

            if (Config.SuperspreaderLocationThreshold < 2)
            {
                Config.SuperspreaderLocationThreshold = 10;
                changed = true;
            }

            if (changed)
            {
                Log.Warning("The 'Real Time' pandemic configuration contained zero/invalid values and was adjusted to safe runtime defaults.");
            }
        }

        private bool IsInfectious(uint citizenID)
        {
            if (activeInfections.ContainsKey(citizenID))
            {
                long infectedTime = (currentDateTime.Ticks / 10000) - activeInfections[citizenID];
                double infectedTimeInDays = infectedTime / 1000.0 / 3600.0 / 24.0;
                if (Config.StartInfection <= infectedTimeInDays && infectedTimeInDays <= Config.EndInfection)
                {
                    return true;
                }
            }
            return false;
        }

        private void InfectCitizen(uint infectedCitizenID, ref Citizen infectedCitizen)
        {
            InfectCitizen(infectedCitizenID, ref infectedCitizen, 0);
        }

        private void InfectCitizen(uint infectedCitizenID, ref Citizen infectedCitizen, long offset)
        {
            if (infectedCitizens.Contains(infectedCitizenID))
            {
                return;
            }

            CitizenProxy.SetSick(ref infectedCitizen, false);
            infectedCitizens.Add(infectedCitizenID);

            if (random.NextDouble() < Config.SymptomProbability / 100.0)
            {
                infectedCitizensWithSymptoms.Add(infectedCitizenID);
            }

            long timeOfInfection = currentDateTime.Ticks / 10000;
            activeInfections.Add(infectedCitizenID, timeOfInfection + offset);
            hadAnySickCitizens = true;

            initialPopulationHealthy.Remove(infectedCitizenID);
            initialPopulationSick.Add(infectedCitizenID);
        }

        private void HealCitizen(uint infectedCitizenID, ref Citizen infectedCitizen)
        {
            if (!infectedCitizens.Contains(infectedCitizenID))
            {
                return;
            }

            CitizenProxy.SetSick(ref infectedCitizen, false);
            activeInfections.Remove(infectedCitizenID);
            infectionOrigins.Remove(infectedCitizenID);
            infectedCitizensWithSymptoms.Remove(infectedCitizenID);
            symptomHospitalHandledCitizens.Remove(infectedCitizenID);

            initialPopulationRecovered.Add(infectedCitizenID);
            initialPopulationSick.Remove(infectedCitizenID);
        }

        private void KillCitizen(uint infectedCitizenID, ref Citizen infectedCitizen)
        {
            if (!infectedCitizens.Contains(infectedCitizenID) || !activeInfections.ContainsKey(infectedCitizenID))
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
            return lifecycleState == PandemicLifecycleState.Running
                && activeInfections.ContainsKey(citizenId)
                && HasSymptoms(citizenId)
                && GetDaysSinceInfection(citizenId) >= Config.StartSymptoms
                && !symptomHospitalHandledCitizens.Contains(citizenId)
                && !IsInQuarantine(citizenId);
        }

        internal void OnCitizenVisitedHealthcare(uint citizenId)
        {
            if (!activeInfections.ContainsKey(citizenId))
            {
                return;
            }

            if (simulation != null)
            {
                currentDateTime = simulation.m_currentGameTime;
            }

            symptomHospitalHandledCitizens.Add(citizenId);
            QuarantineManager.Instance.AddCitizenInQuarantine(citizenId, currentDateTime);
            ScheduleQuarantineFate(citizenId);

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
            if (!activeInfections.ContainsKey(citizenId))
            {
                return;
            }

            if (simulation != null)
            {
                currentDateTime = simulation.m_currentGameTime;
            }

            symptomHospitalHandledCitizens.Add(citizenId);
            QuarantineManager.Instance.AddCitizenInQuarantine(citizenId, currentDateTime);
            ScheduleQuarantineFate(citizenId);
        }

        private int GetDaysSinceInfection(uint citizenId)
        {
            if (!activeInfections.ContainsKey(citizenId))
            {
                return -1;
            }

            long nowMs = (simulation != null ? simulation.m_currentGameTime : currentDateTime).Ticks / 10000;
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

                bool symptomatic = HasSymptoms(citizenId) && GetDaysSinceInfection(citizenId) >= Config.StartSymptoms && !IsInQuarantine(citizenId);
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
            return QuarantineManager.Instance.IsInQuarantine(citizenId, currentDateTime) || QuarantineManager.Instance.IsInProphylacticQuarantine(citizenId, currentDateTime);
        }

        public void Shuffle<T>(IList<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = random.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        public void spread()
        {
            Citizen[] citizens = CitizenMgr.GetCitizensArray();
            if (citizens == null)
            {
                return;
            }

            ProcessCitizenTesting(citizens);
            ProcessRecoveries(citizens);
            ProcessContactTracingCandidates();
            BuildCitizenTickStateIndex(citizens);

            pendingTransmissionIds.Clear();
            float rangeSq = Config.DiseaseTransmissionRange * Config.DiseaseTransmissionRange;
            SimulateOutdoorTransmissions(citizens, rangeSq);
            SimulateVehicleTransmissions(citizens);
            SimulateBuildingTransmissions(citizens);
        }

        private void SimulateOutdoorTransmissions(Citizen[] citizens, float rangeSq)
        {
            if (outdoorInfectiousByCell.Count == 0 || outdoorHealthyByCell.Count == 0)
            {
                return;
            }

            foreach (List<PandemicCitizenTickState> infectiousStates in outdoorInfectiousByCell.Values)
            {
                for (int i = 0; i < infectiousStates.Count; i++)
                {
                    PandemicCitizenTickState infectious = infectiousStates[i];
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int neighborKey = GetOutdoorCellKey(infectious.OutdoorCellX + dx, infectious.OutdoorCellZ + dz);
                            if (!outdoorHealthyByCell.TryGetValue(neighborKey, out List<PandemicCitizenTickState> healthyStates))
                            {
                                continue;
                            }

                            for (int j = 0; j < healthyStates.Count; j++)
                            {
                                PandemicCitizenTickState healthy = healthyStates[j];
                                if (pendingTransmissionIds.Contains(healthy.CitizenId) || activeInfections.ContainsKey(healthy.CitizenId))
                                {
                                    continue;
                                }

                                if ((healthy.Position - infectious.Position).sqrMagnitude > rangeSq)
                                {
                                    continue;
                                }

                                ContactManager.Instance.AddContact(infectious.CitizenId, healthy.CitizenId, false, currentDateTime);
                                if (Masks.GetOutdoorInfectionProbability(infectious.CitizenId, healthy.CitizenId) <= random.NextDouble())
                                {
                                    continue;
                                }

                                PandemicInfectionOriginInfo origin = CreateOutdoorOrigin(healthy.Position);
                                InfectTargetCitizen(infectious.CitizenId, healthy, citizens, origin);
                            }
                        }
                    }
                }
            }
        }

        private void SimulateVehicleTransmissions(Citizen[] citizens)
        {
            foreach (KeyValuePair<ushort, List<PandemicCitizenTickState>> bucket in vehicleInfectiousOccupants)
            {
                if (!vehicleHealthyOccupants.TryGetValue(bucket.Key, out List<PandemicCitizenTickState> healthyStates))
                {
                    continue;
                }

                for (int i = 0; i < bucket.Value.Count; i++)
                {
                    PandemicCitizenTickState infectious = bucket.Value[i];
                    for (int j = 0; j < healthyStates.Count; j++)
                    {
                        PandemicCitizenTickState healthy = healthyStates[j];
                        if (pendingTransmissionIds.Contains(healthy.CitizenId) || activeInfections.ContainsKey(healthy.CitizenId))
                        {
                            continue;
                        }

                        ContactManager.Instance.AddContact(infectious.CitizenId, healthy.CitizenId, false, currentDateTime);
                        if (Masks.GetVehicleInfectionProbability(infectious.CitizenId, healthy.CitizenId) <= random.NextDouble())
                        {
                            continue;
                        }

                        PandemicInfectionOriginInfo origin = CreateVehicleOrigin(bucket.Key, healthy.Position);
                        InfectTargetCitizen(infectious.CitizenId, healthy, citizens, origin);
                    }
                }
            }
        }

        private void SimulateBuildingTransmissions(Citizen[] citizens)
        {
            foreach (KeyValuePair<ushort, List<PandemicCitizenTickState>> bucket in buildingInfectiousOccupants)
            {
                if (!buildingHealthyOccupants.TryGetValue(bucket.Key, out List<PandemicCitizenTickState> healthyStates))
                {
                    continue;
                }

                Vector3 buildingPosition = BuildingMgr.GetBuildingPosition(bucket.Key);
                for (int i = 0; i < bucket.Value.Count; i++)
                {
                    PandemicCitizenTickState infectious = bucket.Value[i];
                    bool familyLookupAvailable = false;
                    if (infectious.AtHome)
                    {
                        Array.Clear(familyLookupBuffer, 0, familyLookupBuffer.Length);
                        familyLookupAvailable = CitizenMgr.TryGetFamily(infectious.CitizenId, familyLookupBuffer);
                    }

                    for (int j = 0; j < healthyStates.Count; j++)
                    {
                        PandemicCitizenTickState healthy = healthyStates[j];
                        if (pendingTransmissionIds.Contains(healthy.CitizenId) || activeInfections.ContainsKey(healthy.CitizenId))
                        {
                            continue;
                        }

                        double probability;
                        if (infectious.AtHome && healthy.AtHome)
                        {
                            bool sameFamily = familyLookupAvailable && Array.IndexOf(familyLookupBuffer, healthy.CitizenId) >= 0;
                            if (sameFamily)
                            {
                                ContactManager.Instance.AddContact(infectious.CitizenId, healthy.CitizenId, true, currentDateTime);
                                probability = Masks.GetHouseholdInfectionProbability(infectious.CitizenId, healthy.CitizenId);
                            }
                            else
                            {
                                if (!infectious.IsInResidentialSharedAreaWindow || !healthy.IsInResidentialSharedAreaWindow)
                                {
                                    continue;
                                }

                                ContactManager.Instance.AddContact(infectious.CitizenId, healthy.CitizenId, true, currentDateTime);
                                probability = Masks.GetIndoorInfectionProbabilityNoContact(infectious.CitizenId, healthy.CitizenId);
                            }
                        }
                        else
                        {
                            ContactManager.Instance.AddContact(infectious.CitizenId, healthy.CitizenId, true, currentDateTime);
                            probability = Masks.GetIndoorInfectionProbability(infectious.CitizenId, healthy.CitizenId);
                        }

                        if (probability <= random.NextDouble())
                        {
                            continue;
                        }

                        PandemicInfectionOriginInfo origin = CreateBuildingOrigin(bucket.Key, buildingPosition);
                        InfectTargetCitizen(infectious.CitizenId, healthy, citizens, origin);
                    }
                }
            }
        }

        private void InfectTargetCitizen(uint infectingCitizenId, PandemicCitizenTickState target, Citizen[] citizens, PandemicInfectionOriginInfo origin)
        {
            pendingTransmissionIds.Add(target.CitizenId);
            Observer.AddCitizenInfection(infectingCitizenId, target.CitizenId, currentDateTime, origin);
            InfectCitizen(target.CitizenId, ref citizens[target.RealCitizenId]);
            RecordInfectionOrigin(target.CitizenId, origin);
        }

        private uint retrieveID(uint citizenID)
        {
            if (citizenMatching.ContainsKey(citizenID))
            {
                return citizenMatching[citizenID];
            }
            return citizenID;
        }

        private void ScheduleQuarantineFate(uint citizenID)
        {
            if (quarantineFateTimestampMs.ContainsKey(citizenID))
            {
                return;
            }

            // Random resolution day between 1 and QuarantineFateDays (in ms)
            int fateDays = random.Next(1, QuarantineFateDays + 1);
            long fateTs = (currentDateTime.Ticks / 10000) + (long)fateDays * 24 * 3600 * 1000;
            quarantineFateTimestampMs[citizenID] = fateTs;

            double deathProb = GetDeathProbabilityForCitizen(citizenID);
            if (random.NextDouble() < deathProb)
            {
                quarantineFatedToDie.Add(citizenID);
            }
        }

        private double GetDeathProbabilityForCitizen(uint citizenID)
        {
            if (Config == null || CitizenMgr == null || CitizenProxy == null)
            {
                return 0;
            }

            Citizen[] citizens = CitizenMgr.GetCitizensArray();
            uint realId = retrieveID(citizenID);
            if (realId >= citizens.Length)
            {
                return 0;
            }

            Citizen.AgeGroup age = CitizenProxy.GetAge(ref citizens[realId]);
            switch (age)
            {
                case Citizen.AgeGroup.Child:  return Config.DeathChild  / 100.0;
                case Citizen.AgeGroup.Teen:   return Config.DeathTeen   / 100.0;
                case Citizen.AgeGroup.Young:  return Config.DeathYoung  / 100.0;
                case Citizen.AgeGroup.Adult:  return Config.DeathAdult  / 100.0;
                case Citizen.AgeGroup.Senior: return Config.DeathSenior / 100.0;
                default: return 0;
            }
        }

        private void ProcessQuarantineFates()
        {
            if (!IsVisualizationDataAvailable() || quarantineFateTimestampMs.Count == 0)
            {
                return;
            }

            long nowMs = currentDateTime.Ticks / 10000;
            Citizen[] citizens = CitizenMgr.GetCitizensArray();
            var toProcess = new List<uint>();

            foreach (var kvp in quarantineFateTimestampMs)
            {
                if (nowMs >= kvp.Value)
                {
                    toProcess.Add(kvp.Key);
                }
            }

            foreach (uint citizenID in toProcess)
            {
                quarantineFateTimestampMs.Remove(citizenID);

                uint realId = retrieveID(citizenID);
                if (realId >= citizens.Length)
                {
                    quarantineFatedToDie.Remove(citizenID);
                    continue;
                }

                if (!activeInfections.ContainsKey(citizenID))
                {
                    quarantineFatedToDie.Remove(citizenID);
                    continue;
                }

                if (quarantineFatedToDie.Contains(citizenID))
                {
                    quarantineFatedToDie.Remove(citizenID);
                    KillCitizen(citizenID, ref citizens[realId]);
                }
                else
                {
                    HealCitizen(citizenID, ref citizens[realId]);
                    QuarantineManager.Instance.RemoveCitizenInQuarantine(citizenID);
                }
            }
        }

        /// <summary>Returns the number of days the citizen has been infected, or -1 if not infected.</summary>
        public int GetDaysInfected(uint citizenId)
        {
            if (!activeInfections.ContainsKey(citizenId) || simulation == null)
            {
                return -1;
            }

            long nowMs = simulation.m_currentGameTime.Ticks / 10000;
            long infectionMs = activeInfections[citizenId];
            long deltaMs = nowMs - infectionMs;
            if (deltaMs < 0) deltaMs = 0;
            return (int)(deltaMs / (24L * 3600 * 1000));
        }

        /// <summary>Returns the age-based death probability as a percentage (0-100).</summary>
        public float GetCitizenDeathProbabilityPercent(uint citizenId)
        {
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
            if (simulation != null)
            {
                currentDateTime = simulation.m_currentGameTime;
            }

            if (IsInQuarantine(citizenID))
            {
                return true;
            }

            Citizen citizen = CitizenMgr.GetCitizensArray()[retrieveID(citizenID)];
            if (activeInfections.ContainsKey(citizenID) && Config.QuarantineBehavior != RealTime.Config.QuarantineBehavior.None)
            {
                long infectionInDays = ((simulation.m_currentGameTime.Ticks / 10000) - GetInfectionDate(citizenID)) / 1000 / 3600 / 24;
                bool knownSick = IsKnownSick(citizenID);
                if ((knownSick && !Config.OnlyTestedCitizensToQuarantine) || TestManager.Instance.IsBlocked(citizenID, currentDateTime) || infectionInDays >= 2)
                {
                    if (!IsInQuarantine(citizenID))
                    {
                        QuarantineManager.Instance.AddCitizenInQuarantine(citizenID, currentDateTime);
                        ScheduleQuarantineFate(citizenID);
                    }
                    return true;
                }
            }

            return false;
        }

        private bool IsKnownSick(uint citizenID)
        {
            Citizen citizen = CitizenMgr.GetCitizensArray()[retrieveID(citizenID)];
            if (activeInfections.ContainsKey(citizenID))
            {
                long infectionInDays = ((simulation.m_currentGameTime.Ticks / 10000) - GetInfectionDate(citizenID)) / 1000 / 3600 / 24;
                bool hasSymptoms = HasSymptoms(citizenID);
                return infectionInDays > Config.StartSymptoms && hasSymptoms;
            }
            return false;
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

                long leastConsideredTime = simulation.m_currentGameTime.Ticks - (TimeSpan.TicksPerDay * timeInDays);

                foreach (KeyValuePair<uint, DateTime> contact in contacts)
                {
                    if (contact.Value.Ticks > leastConsideredTime)
                    {
                        QuarantineManager.Instance.AddCitizenInProphylacticQuarantine(contact.Key, currentDateTime);
                    }
                }
            }

            if (quarantineBehavior == RealTime.Config.QuarantineBehavior.Family)// || quarantineBehavior == RealTime.Config.QuarantineBehavior.Contacts)
            {
                uint[] family = new uint[10];
                if (CitizenMgr.TryGetFamily(citizenID, family))
                {
                    foreach (uint otherCitizenID in family)
                    {
                        if (otherCitizenID > 0)
                        {
                            QuarantineManager.Instance.AddCitizenInProphylacticQuarantine(otherCitizenID, currentDateTime);
                        }
                    }
                }
            }
        }

        internal bool HasSymptoms(uint citizenID)
        {
            return infectedCitizensWithSymptoms.Contains(citizenID);
        }

        public void kill(double childDeathProbability, double teenDeathProbability, double youngDeathProbability, double adultDeathProbability, double seniorDeathProbability)
        {
            Citizen[] citizens = CitizenMgr.GetCitizensArray();
            
            for (int i = 0; i < initialPopulationSick.Count; i++)
            {
                uint citizenID = initialPopulationSick[i];
                if (!activeInfections.ContainsKey(citizenID))
                {
                    continue;
                }
                float infectedTime = ((currentDateTime.Ticks / 10000) - activeInfections[citizenID]) / (24 * 3600 * 1000f);
                
                if (infectedTime < Config.StartSymptoms)
                {
                    continue;
                }

                double deathProbability = 0;
                Citizen.AgeGroup ageGroup = CitizenProxy.GetAge(ref citizens[retrieveID(citizenID)]);
                switch (ageGroup)
                {
                    case Citizen.AgeGroup.Child:
                        deathProbability = childDeathProbability;
                        break;
                    case Citizen.AgeGroup.Teen:
                        deathProbability = teenDeathProbability;
                        break;
                    case Citizen.AgeGroup.Young:
                        deathProbability = youngDeathProbability;
                        break;
                    case Citizen.AgeGroup.Adult:
                        deathProbability = adultDeathProbability;
                        break;
                    case Citizen.AgeGroup.Senior:
                        deathProbability = seniorDeathProbability;
                        break;
                }

                if (random.NextDouble() < deathProbability)
                {
                    KillCitizen(citizenID, ref citizens[retrieveID(citizenID)]);
                    i--;
                }
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
        public static PandemicManager Instance { get; set; }
    }
}
