
using System;
using System.Collections.Generic;
using System.Linq;
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
        private PandemicXRayMode xRayMode = PandemicXRayMode.Off;
        private float spreadDistance = 10;
        private DateTime lastDateTime;
        private DateTime lastDateTimeCitizensUpdate;
        private DateTime lastStoreTime;
        private DateTime currentDateTime;
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

        private readonly long CITIZENS_UPDATE_INTERVAL_MINUTES = 60 * 6;
        private readonly long UPDATE_INTERVAL_MINUTES = 5;
        private readonly long STORE_INTERVAL_MINUTES = 5;

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

        private void BootstrapSimulation(bool lockdownEnabled)
        {
            if (!TryPrepareSimulationForBootstrap())
            {
                return;
            }

            try
            {
                ResetRuntimeStateForBootstrap();
                ResetPandemicServices(lockdownEnabled);
                currentDateTime = simulation.m_currentGameTime;
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

            var simulationObject = GameObject.Find("SimulationManager");
            simulation = simulationObject?.GetComponent<SimulationManager>();
            if (simulation == null)
            {
                Log.Warning("The 'Real Time' pandemic manager could not find the simulation manager.");
                return false;
            }

            return true;
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

        public void Update()
        {
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
                        Debug.Log("Number of inactive citizens before: " + removedCitizens.Count);

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

                                Debug.Log("Replaced " + removedCitizen + " with citizen " + i);
                            }
                        }
                    }

                    Debug.Log("Number of inactive citizens: " + removedCitizens.Count);
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
                    Observer.AddSickCitizens(currentDateTime, initialPopulationSick.Count);
                    Observer.AddHealthyCitizens(currentDateTime, initialPopulationHealthy.Count);
                    Observer.AddRecoveredCitizens(currentDateTime, initialPopulationRecovered.Count);
                    Observer.AddDeadCitizens(currentDateTime, initialPopulationDead.Count);
                    Observer.FinishObservations(currentDateTime);

                    Debug.Log("Sick: " + Observer.GetSickCitizens() + ", Healty: " + Observer.GetHealthyCitizens() + ", Recovered: " + Observer.GetRecoveredCitizens() + ", Dead: " + Observer.GetDeadCitizens());
                    Debug.Log("Citizens in quarantine: " + QuarantineManager.Instance.CitizensInQuarantine());
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
                    Debug.Log("Writing to disc");

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
                }
            }
            catch (Exception ex)
            {
                Log.Error("The 'Real Time' pandemic manager update failed unexpectedly and was recovered: " + ex);
            }
            finally
            {
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

        private void BuildPandemicBuildingSets()
        {
            infectedBuildingIds.Clear();
            quarantineBuildingIds.Clear();
            hotspotBuildingIds.Clear();
            hubBuildingIds.Clear();
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

            var infectedCountPerBuilding = buildingInfectedCounts;
            infectedCountPerBuilding.Clear();
            foreach (uint citizenId in initialPopulation)
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

                ushort currentBuilding = CitizenProxy.GetCurrentBuilding(ref c);
                if (currentBuilding != 0)
                {
                    ItemClass.Service currentService = BuildingMgr.GetBuildingService(currentBuilding);
                    ItemClass.SubService currentSubService = BuildingMgr.GetBuildingSubService(currentBuilding);
                    PandemicLockdownFamily family = PandemicTaxonomy.GetBuildingFamily(currentService, currentSubService);
                    PandemicFamilyExposure familyExposure = GetOrCreateFamilyExposure(family);
                    familyExposure.TotalCitizens++;
                    if (activeInfections.ContainsKey(citizenId))
                    {
                        familyExposure.InfectedCitizens++;
                    }
                }

                if (!activeInfections.ContainsKey(citizenId))
                {
                    continue;
                }

                ushort home = CitizenProxy.GetHomeBuilding(ref c);
                if (home != 0)
                {
                    infectedBuildingIds.Add(home);
                    if (infectedCountPerBuilding.TryGetValue(home, out int cnt))
                    {
                        infectedCountPerBuilding[home] = cnt + 1;
                    }
                    else
                    {
                        infectedCountPerBuilding[home] = 1;
                    }
                }

                if (currentBuilding != 0)
                {
                    if (buildingCurrentInfectedCounts.TryGetValue(currentBuilding, out int currentCount))
                    {
                        buildingCurrentInfectedCounts[currentBuilding] = currentCount + 1;
                    }
                    else
                    {
                        buildingCurrentInfectedCounts[currentBuilding] = 1;
                    }
                }
            }

            foreach (var kvp in infectedCountPerBuilding)
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

            DateTime now = simulation?.m_currentGameTime ?? currentDateTime;
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
            return lifecycleState == PandemicLifecycleState.Running
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

        public bool IsLockdownEnabled() => QuarantineManager.Instance.InLockDown;

        public bool AreWorldOverlaysEnabled() => worldOverlaysEnabled;

        internal PandemicXRayMode GetXRayMode() => xRayMode;

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
            return enabled;
        }

        public bool ToggleWorldOverlays()
        {
            worldOverlaysEnabled = !worldOverlaysEnabled;
            return worldOverlaysEnabled;
        }

        internal PandemicXRayMode CycleXRayMode()
        {
            switch (xRayMode)
            {
                case PandemicXRayMode.Off:
                    xRayMode = PandemicXRayMode.LivePositions;
                    break;

                case PandemicXRayMode.LivePositions:
                    xRayMode = PandemicXRayMode.HomeLocations;
                    break;

                default:
                    xRayMode = PandemicXRayMode.Off;
                    break;
            }

            return xRayMode;
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
            var snapshot = new PandemicLiveSnapshot
            {
                IsActive = active,
                IsInitialized = startCompleted,
                LifecycleState = lifecycleState,
                HasStartedAtLeastOnce = hasStartedAtLeastOnce,
                CanStart = lifecycleState == PandemicLifecycleState.Dormant,
                CanRestart = hasStartedAtLeastOnce,
                WorldOverlaysEnabled = worldOverlaysEnabled,
                XRayMode = xRayMode,
                SimulationTime = simulation != null ? simulation.m_currentGameTime : currentDateTime,
                Healthy = initialPopulationHealthy.Count,
                Sick = initialPopulationSick.Count,
                Recovered = initialPopulationRecovered.Count,
                Dead = initialPopulationDead.Count,
                QuarantineCitizens = QuarantineManager.Instance.CitizensInQuarantine(),
                PositiveTests = TestManager.Instance.GetCurrentPositiveCount(simulation != null ? simulation.m_currentGameTime : currentDateTime),
                TestedCitizens = TestManager.Instance.GetTrackedTestsCount(),
                ContactsTrackedCitizens = ContactManager.Instance.GetTrackedCitizenCount(),
                ContactsTrackedPairs = ContactManager.Instance.GetTrackedPairCount(),
                ContactsRecordedTotal = ContactManager.Instance.GetTotalRecordedContacts(),
                ObservationCount = Observer?.GetObservationCount() ?? 0,
                TransmissionsTotal = Observer?.GetTotalInfections() ?? 0,
                TransmissionsIndoor = Observer?.GetIndoorInfections() ?? 0,
                TransmissionsOutdoor = Observer?.GetOutdoorInfections() ?? 0,
                TransmissionsVehicle = Observer?.GetVehicleInfections() ?? 0,
                HotspotBuildings = hotspotBuildingIds.Count,
                HubBuildings = hubBuildingIds.Count,
            };

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

            PopulateLiveSnapshotBreakdown(snapshot);
            PopulateOriginSnapshots(snapshot);
            PopulateSuperspreaders(snapshot);
            PopulateLockdownFamilies(snapshot);
            PopulateChart(snapshot);
            PopulatePolicyMarkers(snapshot);
            snapshot.HasChartData = snapshot.ChartPoints.Count > 0;
            return snapshot;
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
                bool canFocus = TryGetCitizenFocusPosition(kvp.Key, out focusPosition);
                snapshot.TopSpreaders.Add(new PandemicSuperspreaderCitizenSnapshot
                {
                    CitizenId = kvp.Key,
                    Label = "Citizen #" + kvp.Key,
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

        private bool TryGetCitizenFocusPosition(uint citizenId, out Vector3 position)
        {
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

            ushort instanceId = CitizenProxy.GetInstance(ref citizen);
            if (instanceId != 0)
            {
                position = CitizenMgr.GetCitizenPosition(instanceId);
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

        internal int PopulateHeatmapGrid(float[] target, PandemicXRayMode mode)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            Array.Clear(target, 0, target.Length);
            if (mode == PandemicXRayMode.Off || !IsVisualizationDataAvailable())
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

                Vector3 position = GetHeatmapPosition(ref citizen, mode);
                if (position == Vector3.zero)
                {
                    continue;
                }

                int x = Mathf.Clamp((int)(((position.x + mapHalfSize) / (2f * mapHalfSize)) * resolution), 0, resolution - 1);
                int z = Mathf.Clamp((int)(((position.z + mapHalfSize) / (2f * mapHalfSize)) * resolution), 0, resolution - 1);
                target[(z * resolution) + x] += 1f;
                written++;
            }

            return written;
        }

        private Vector3 GetHeatmapPosition(ref Citizen citizen, PandemicXRayMode mode)
        {
            if (mode == PandemicXRayMode.HomeLocations)
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

            CitizenProxy.SetDead(ref infectedCitizen, true);
            CitizenProxy.SetSick(ref infectedCitizen, false);
            activeInfections.Remove(infectedCitizenID);
            infectionOrigins.Remove(infectedCitizenID);
            infectedCitizensWithSymptoms.Remove(infectedCitizenID);
            symptomHospitalHandledCitizens.Remove(infectedCitizenID);

            initialPopulationDead.Add(infectedCitizenID);
            initialPopulationSick.Remove(infectedCitizenID);
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
            Debug.Log("Spreading disease");

            Citizen[] citizens = CitizenMgr.GetCitizensArray();

            HashSet<uint> newlyInfectedCitizenIDs = new HashSet<uint>();
            
            float range = Config.DiseaseTransmissionRange;

            uint numberOfSickCitizens = 0;
            uint numberOfInfectiousCitizens = 0;

            Shuffle(initialPopulation);
            for (int i = 0; i < initialPopulation.Count; i++)
            {
                uint citizenID = initialPopulation[i];

                if (!CitizenProxy.IsDead(ref citizens[retrieveID(citizenID)]))
                {
                    TestManager.Instance.TestCitizen(citizenID, activeInfections.ContainsKey(citizenID), IsKnownSick(citizenID), currentDateTime);
                }
            }

            uint tested = 0;
            for (int i = 0; i < initialPopulationSick.Count; i++)
            {
                uint citizenID = initialPopulationSick[i];

                if (newlyInfectedCitizenIDs.Contains(citizenID))
                {
                    continue;
                }

                ushort instanceID = CitizenProxy.GetInstance(ref citizens[retrieveID(citizenID)]);

                if (SET_SICK_FLAG && !CitizenProxy.IsSick(ref citizens[retrieveID(citizenID)]) && activeInfections.ContainsKey(citizenID))
                {
                    CitizenProxy.SetSick(ref citizens[retrieveID(citizenID)], true);
                }

                double infectedTimeInDays = 0;
                if (activeInfections.ContainsKey(citizenID))
                {
                    long infectedTime = (currentDateTime.Ticks / 10000) - activeInfections[citizenID];
                    infectedTimeInDays = infectedTime / 1000.0 / 3600.0 / 24.0;
                    if (infectedTimeInDays > Config.DiseaseDuration)
                    {
                        HealCitizen(citizenID, ref citizens[retrieveID(citizenID)]);
                        i--;
                        continue;
                    }
                }

                if (ShouldBeInQuarantine(citizenID) && (Config.QuarantineBehavior == RealTime.Config.QuarantineBehavior.Contacts || Config.QuarantineBehavior == RealTime.Config.QuarantineBehavior.Family))
                {
                    CheckForContacts(citizenID, Config.DiseaseDuration, Config.QuarantineBehavior);
                    continue;
                }

                if (activeInfections.ContainsKey(citizenID))
                {
                    numberOfInfectiousCitizens++;

                    if (!IsInfectious(citizenID) && QUICK_SIMULATION)
                    {
                        continue;
                    }

                    if (IsInfectious(citizenID))
                    {
                        Observer.AddInfectiousCitizen(citizenID);
                    }

                    if (CitizenProxy.GetCurrentBuilding(ref citizens[retrieveID(citizenID)]) == 0 && CitizenProxy.GetVehicle(ref citizens[retrieveID(citizenID)]) == 0 && instanceID > 0)
                    {
                        if (!ShouldBeInQuarantine(citizenID) || Config.QuarantineBehavior == RealTime.Config.QuarantineBehavior.None) {
                            Vector3 location = CitizenMgr.GetCitizenPosition(instanceID);

                            for (int j = 0; j < initialPopulationHealthy.Count; j++)
                            {
                                uint otherCitizenID = initialPopulationHealthy[j];
                                if (ShouldBeInQuarantine(otherCitizenID))
                                {
                                    continue;
                                }

                                ushort otherInstanceID = CitizenProxy.GetInstance(ref citizens[retrieveID(otherCitizenID)]);
                                if (CitizenProxy.GetCurrentBuilding(ref citizens[retrieveID(citizenID)]) == 0 && !activeInfections.ContainsKey(otherCitizenID) && otherInstanceID > 0)
                                {
                                    Vector3 otherLocation = CitizenMgr.GetCitizenPosition(otherInstanceID);

                                    if ((otherLocation - location).magnitude < range && Masks.GetOutdoorInfectionProbability(citizenID, otherCitizenID) > random.NextDouble())
                                    {
                                        ContactManager.Instance.AddContact(citizenID, otherCitizenID, false, currentDateTime);

                                        if (IsInfectious(citizenID))
                                        {
                                            Debug.Log("Citizen " + otherCitizenID + "(" + otherLocation + ")" + " is in transmission range of citizen " + citizenID + "(" + TestManager.Instance.GetPositiveDate(citizenID) + ") and got infected!");

                                            PandemicInfectionOriginInfo outdoorOrigin = CreateOutdoorOrigin(otherLocation);
                                            Observer.AddCitizenInfection(citizenID, otherCitizenID, currentDateTime, outdoorOrigin);

                                            newlyInfectedCitizenIDs.Add(otherCitizenID);
                                            InfectCitizen(otherCitizenID, ref citizens[retrieveID(otherCitizenID)]);
                                            RecordInfectionOrigin(otherCitizenID, outdoorOrigin);
                                            j--;
                                            continue;
                                        }
                                    }
                                }
                            }
                        }
                    } else if (CitizenProxy.GetVehicle(ref citizens[retrieveID(citizenID)]) > 0)
                    {
                        if (!ShouldBeInQuarantine(citizenID) || Config.QuarantineBehavior == RealTime.Config.QuarantineBehavior.None)
                        {
                            for (int j = 0; j < initialPopulationHealthy.Count; j++)
                            {
                                uint otherCitizenID = initialPopulationHealthy[j];
                                if (ShouldBeInQuarantine(otherCitizenID))
                                {
                                    continue;
                                }

                                if (CitizenProxy.GetVehicle(ref citizens[retrieveID(otherCitizenID)]) > 0 && !activeInfections.ContainsKey(otherCitizenID))
                                {
                                    if (CitizenProxy.GetVehicle(ref citizens[retrieveID(citizenID)]) == CitizenProxy.GetVehicle(ref citizens[retrieveID(otherCitizenID)]))
                                    {
                                        ContactManager.Instance.AddContact(citizenID, otherCitizenID, false, currentDateTime);

                                        if (IsInfectious(citizenID))
                                        {
                                            if (Masks.GetVehicleInfectionProbability(citizenID, otherCitizenID) > random.NextDouble())
                                            {
                                                Debug.Log("Citizen " + otherCitizenID + " is in the same vehicle as citizen " + citizenID + "(" + TestManager.Instance.GetPositiveDate(citizenID) + ")" + " and got infected!");

                                                ushort otherInstanceID = CitizenProxy.GetInstance(ref citizens[retrieveID(otherCitizenID)]);
                                                ushort vehicleId = CitizenProxy.GetVehicle(ref citizens[retrieveID(citizenID)]);
                                                Vector3 vehiclePosition = Vector3.zero;
                                                if (otherInstanceID > 0)
                                                {
                                                    vehiclePosition = CitizenMgr.GetCitizenPosition(otherInstanceID);
                                                }

                                                PandemicInfectionOriginInfo vehicleOrigin = CreateVehicleOrigin(vehicleId, vehiclePosition);
                                                Observer.AddCitizenInfection(citizenID, otherCitizenID, currentDateTime, vehicleOrigin);

                                                newlyInfectedCitizenIDs.Add(otherCitizenID);
                                                InfectCitizen(otherCitizenID, ref citizens[retrieveID(otherCitizenID)]);
                                                RecordInfectionOrigin(otherCitizenID, vehicleOrigin);
                                                j--;
                                                continue;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else if (CitizenProxy.GetCurrentBuilding(ref citizens[retrieveID(citizenID)]) > 0)
                    {
                        for (int j = 0; j < initialPopulationHealthy.Count; j++)
                        {
                            uint otherCitizenID = initialPopulationHealthy[j];
                            if (ShouldBeInQuarantine(otherCitizenID))
                            {
                                continue;
                            }

                            if (CitizenProxy.GetCurrentBuilding(ref citizens[retrieveID(otherCitizenID)]) > 0 && !activeInfections.ContainsKey(otherCitizenID))
                            {
                                if (CitizenProxy.GetCurrentBuilding(ref citizens[retrieveID(citizenID)]) == CitizenProxy.GetCurrentBuilding(ref citizens[retrieveID(otherCitizenID)]))
                                {
                                    ContactManager.Instance.AddContact(citizenID, otherCitizenID, true, currentDateTime);

                                    if (IsInfectious(citizenID))
                                    {
                                        double probability;
                                        bool infect = false;
                                        if (CitizenProxy.GetHomeBuilding(ref citizens[retrieveID(citizenID)]) == CitizenProxy.GetCurrentBuilding(ref citizens[retrieveID(citizenID)])
                                            && CitizenProxy.GetHomeBuilding(ref citizens[retrieveID(otherCitizenID)]) == CitizenProxy.GetCurrentBuilding(ref citizens[retrieveID(otherCitizenID)]))
                                        {
                                            uint[] family = new uint[10];
                                                if (CitizenMgr.TryGetFamily(citizenID, family))
                                            {
                                                if (Array.Exists(family, element => element == otherCitizenID))
                                                {
                                                    infect = !ShouldBeInQuarantine(citizenID);
                                                    probability = Masks.GetHouseholdInfectionProbability(citizenID, otherCitizenID);
                                                } else
                                                {
                                                    infect = !ShouldBeInQuarantine(citizenID);
                                                    probability = Masks.GetIndoorInfectionProbabilityNoContact(citizenID, otherCitizenID);
                                                }
                                            } else
                                            {
                                                infect = !ShouldBeInQuarantine(citizenID);
                                                probability = Masks.GetIndoorInfectionProbabilityNoContact(citizenID, otherCitizenID);
                                            }

                                        }
                                        else
                                        {
                                            infect = !ShouldBeInQuarantine(citizenID) || Config.QuarantineBehavior == RealTime.Config.QuarantineBehavior.None;
                                            probability = Masks.GetIndoorInfectionProbability(citizenID, otherCitizenID);
                                        }
                                        if (infect)
                                        {
                                            if (probability > random.NextDouble())
                                            {
                                                Debug.Log("Citizen " + otherCitizenID + " is in the same building as citizen " + citizenID + "(" + TestManager.Instance.GetPositiveDate(citizenID) + ")" + " and got infected!");

                                                ushort buildingID = CitizenProxy.GetCurrentBuilding(ref citizens[retrieveID(citizenID)]);
                                                Building building = BuildingManager.instance.m_buildings.m_buffer[buildingID];
                                                PandemicInfectionOriginInfo buildingOrigin = CreateBuildingOrigin(buildingID, building.m_position);

                                                Observer.AddCitizenInfection(citizenID, otherCitizenID, currentDateTime, buildingOrigin);

                                                newlyInfectedCitizenIDs.Add(otherCitizenID);
                                                InfectCitizen(otherCitizenID, ref citizens[retrieveID(otherCitizenID)]);
                                                RecordInfectionOrigin(otherCitizenID, buildingOrigin);
                                                j--;
                                                continue;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

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
                    Debug.Log("Killing citizen " + citizenID);
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
