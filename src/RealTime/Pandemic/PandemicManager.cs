
using System;
using System.Collections.Generic;
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

        private PandemicObserver Observer;

        private Dictionary<uint, long> activeInfections = new Dictionary<uint, long>();
        private HashSet<uint> infectedCitizens = new HashSet<uint>();
        private HashSet<uint> infectedCitizensWithSymptoms = new HashSet<uint>();

        private SimulationManager simulation;
        private bool startCompleted;

        private readonly long CITIZENS_UPDATE_INTERVAL_MINUTES = 60 * 6;
        private readonly long UPDATE_INTERVAL_MINUTES = 5;
        private readonly long STORE_INTERVAL_MINUTES = 5;

        private MaskManager Masks = new MaskManager();

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

            active = CitizenMgr != null && CitizenProxy != null && BuildingMgr != null;
            if (!active)
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
            if (startCompleted)
            {
                return;
            }

            if (!active || Config == null || CitizenMgr == null || CitizenProxy == null || BuildingMgr == null)
            {
                Log.Warning("The 'Real Time' pandemic manager cannot start because it was not initialized.");
                return;
            }

            var simulationObject = GameObject.Find("SimulationManager");
            simulation = simulationObject?.GetComponent<SimulationManager>();
            if (simulation == null)
            {
                Log.Warning("The 'Real Time' pandemic manager could not find the simulation manager.");
                return;
            }

            try
            {
                ResetRuntimeStateForBootstrap();
                currentDateTime = simulation.m_currentGameTime;

                Citizen[] citizens = CitizenMgr.GetCitizensArray();

                var infectionCandidates = new List<uint>();

                for (uint i = 0; i < citizens.Length; i++)
                {
                    if (CitizenProxy.IsEmpty(ref citizens[retrieveID(i)]) || CitizenProxy.GetHomeBuilding(ref citizens[retrieveID(i)]) == 0)
                    {
                        continue;
                    }

                    initialPopulation.Add(i);
                    usedCitizens.Add(i);

                    if (CitizenProxy.IsSick(ref citizens[retrieveID(i)]))
                    {
                        CitizenProxy.SetSick(ref citizens[retrieveID(i)], false);
                    }

                    initialPopulationHealthy.Add(i);
                    if (CitizenProxy.GetLocation(ref citizens[retrieveID(i)]) == Citizen.Location.Home)
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

                QuarantineManager.Instance.InLockDown = true;

                TestManager.Instance.Init(Config, initialPopulation.Count, currentDateTime);
                Masks?.Init(Config);
                startCompleted = true;
            }
            catch (Exception ex)
            {
                Log.Error("The 'Real Time' pandemic manager failed to start: " + ex);
                active = false;
                startCompleted = false;
            }
        }

        public void Update()
        {
            if (active && !startCompleted)
            {
                Start();
                if (!startCompleted)
                {
                    return;
                }
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

                if (ACTIVE_ONLY)
                {
                    if ((tempDateTime.Ticks - lastDateTimeCitizensUpdate.Ticks) / TimeSpan.TicksPerMinute >= CITIZENS_UPDATE_INTERVAL_MINUTES)
                    {
                        Debug.Log("Number of inactive citizens before: " + removedCitizens.Count);

                        lastDateTimeCitizensUpdate = tempDateTime;
                        Citizen[] citizens = CitizenMgr.GetCitizensArray();

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
                    startCompleted = false;
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

            if (!IsVisualizationDataAvailable())
            {
                return;
            }

            Citizen[] citizens = CitizenMgr.GetCitizensArray();
            if (citizens == null)
            {
                return;
            }

            foreach (uint citizenId in activeInfections.Keys)
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
                    infectedBuildingIds.Add(home);
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
            return active
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

        public bool IsActive => active && startCompleted;

        public HashSet<ushort> InfectedBuildingIds => infectedBuildingIds;

        public HashSet<ushort> QuarantineBuildingIds => quarantineBuildingIds;

        public bool IsCitizenWearingMask(uint citizenId)
        {
            return Masks?.IsWearingMask(citizenId) ?? false;
        }

        public bool IsMasksEnabled() => Config != null && Config.MaskBehavior != RealTime.Config.MaskBehavior.None;

        public bool IsQuarantineEnabled() => Config != null && Config.QuarantineBehavior != RealTime.Config.QuarantineBehavior.None;

        public bool IsLockdownEnabled() => QuarantineManager.Instance.InLockDown;

        public bool ToggleMasks()
        {
            if (Config == null) return false;
            Config.MaskBehavior = Config.MaskBehavior == RealTime.Config.MaskBehavior.None
                ? RealTime.Config.MaskBehavior.Full
                : RealTime.Config.MaskBehavior.None;
            return IsMasksEnabled();
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
            return QuarantineManager.Instance.InLockDown;
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

            return snapshot;
        }

        private void ResetRuntimeStateForBootstrap()
        {
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
            infectedBuildingIds.Clear();
            quarantineBuildingIds.Clear();
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

            if (SET_SICK_FLAG)
            {
                CitizenProxy.SetSick(ref infectedCitizen, true);
            }
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

            if (SET_SICK_FLAG)
            {
                CitizenProxy.SetSick(ref infectedCitizen, false);
            }
            activeInfections.Remove(infectedCitizenID);

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
            activeInfections.Remove(infectedCitizenID);

            initialPopulationDead.Add(infectedCitizenID);
            initialPopulationSick.Remove(infectedCitizenID);
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

                                            Observer.AddCitizenInfection(citizenID, otherCitizenID, InfectionType.OUTDOOR, currentDateTime, otherLocation);

                                            newlyInfectedCitizenIDs.Add(otherCitizenID);
                                            InfectCitizen(otherCitizenID, ref citizens[retrieveID(otherCitizenID)]);
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
                                                if (otherInstanceID > 0)
                                                {
                                                    Observer.AddCitizenInfection(citizenID, otherCitizenID, InfectionType.VEHICLE, currentDateTime, CitizenMgr.GetCitizenPosition(otherInstanceID));
                                                } else
                                                {
                                                    Observer.AddCitizenInfection(citizenID, otherCitizenID, InfectionType.VEHICLE, currentDateTime, Vector3.zero);
                                                }

                                                newlyInfectedCitizenIDs.Add(otherCitizenID);
                                                InfectCitizen(otherCitizenID, ref citizens[retrieveID(otherCitizenID)]);
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
                                                    probability = Masks.GetDefaultIndoorInfectionProbability();
                                                } else
                                                {
                                                    infect = !ShouldBeInQuarantine(citizenID);
                                                    probability = Masks.GetIndoorInfectionProbabilityNoContact();
                                                }
                                            } else
                                            {
                                                infect = !ShouldBeInQuarantine(citizenID);
                                                probability = Masks.GetIndoorInfectionProbabilityNoContact();
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

                                                Observer.AddCitizenInfection(citizenID, otherCitizenID, InfectionType.INDOOR, currentDateTime, building.m_position, BuildingMgr.GetBuildingService(buildingID));

                                                newlyInfectedCitizenIDs.Add(otherCitizenID);
                                                InfectCitizen(otherCitizenID, ref citizens[retrieveID(otherCitizenID)]);
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

        public bool ShouldBeInQuarantine(uint citizenID)
        {
            if (IsInQuarantine(citizenID))
            {
                return true;
            }

            Citizen citizen = CitizenMgr.GetCitizensArray()[retrieveID(citizenID)];
            if (activeInfections.ContainsKey(citizenID) && Config.QuarantineBehavior != RealTime.Config.QuarantineBehavior.None)
            {
                long infectionInDays = ((simulation.m_currentGameTime.Ticks / 10000) - GetInfectionDate(citizenID)) / 1000 / 3600 / 24;
                bool knownSick = IsKnownSick(citizenID);
                if ((knownSick && !Config.OnlyTestedCitizensToQuarantine) || TestManager.Instance.IsBlocked(citizenID, currentDateTime) || infectionInDays > Config.DetectionTime)
                {
                    if (!IsInQuarantine(citizenID))
                    {
                        QuarantineManager.Instance.AddCitizenInQuarantine(citizenID, currentDateTime);
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
