using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RealTime.Pandemic
{
    class QuarantineManager
    {
        internal const uint RestrictionDurationDays = 10;

        public static QuarantineManager Instance { get; } = new QuarantineManager();

        // Simulation callbacks and the live UI share these collections. All public
        // and internal operations hold this reentrant gate, including expiry reads.
        // Enumerable APIs return materialized snapshots before releasing it.
        private readonly object gate = new object();

        private Dictionary<uint, DateTime> citizensInQuarantine = new Dictionary<uint, DateTime>();
        private HashSet<uint> citizensInQuarantineAlreadyChecked = new HashSet<uint>();
        private Dictionary<uint, DateTime> prophylacticQuarantine = new Dictionary<uint, DateTime>();
        private Dictionary<uint, DateTime> pendingTestQuarantine = new Dictionary<uint, DateTime>();

        private uint quarantineDuration = RestrictionDurationDays;
        private Action<PandemicInterventionEvent> interventionSink;
        private int complianceSeed = 1337;
        private double isolationCompliance = 100d;
        private double quarantineCompliance = 100d;
        private readonly Dictionary<uint, bool> isolationFollowers = new Dictionary<uint, bool>();
        private readonly Dictionary<uint, bool> quarantineFollowers = new Dictionary<uint, bool>();

        internal void ConfigureCompliance(int seed, double isolationPercent, double quarantinePercent)
        {
            lock (gate)
            {
                // Validate both values without consuming any random stream.
                RealTime.Experiments.DeterministicCitizenTraitAssigner.IsAssigned(seed, 1, "isolation-compliance", isolationPercent);
                RealTime.Experiments.DeterministicCitizenTraitAssigner.IsAssigned(seed, 1, "quarantine-compliance", quarantinePercent);
                complianceSeed = seed;
                isolationCompliance = isolationPercent;
                quarantineCompliance = quarantinePercent;
                isolationFollowers.Clear(); quarantineFollowers.Clear();
            }
        }

        internal bool FollowsRestriction(uint citizenId, bool isolation)
        {
            lock (gate)
            {
                var cache = isolation ? isolationFollowers : quarantineFollowers;
                if (!cache.TryGetValue(citizenId, out bool follows))
                {
                    follows = RealTime.Experiments.DeterministicCitizenTraitAssigner.IsAssigned(complianceSeed, citizenId,
                        isolation ? "isolation-compliance" : "quarantine-compliance", isolation ? isolationCompliance : quarantineCompliance);
                    cache[citizenId] = follows;
                }
                return follows;
            }
        }

        public bool InLockDown { get; set; } = false;

        internal void Reset()
        {
            lock (gate)
            {
                isolationFollowers.Clear(); quarantineFollowers.Clear();
                citizensInQuarantine.Clear();
                citizensInQuarantineAlreadyChecked.Clear();
                prophylacticQuarantine.Clear();
                pendingTestQuarantine.Clear();
            }
        }

        /// <summary>Clears all process-wide quarantine state when a level-owned manager is released.</summary>
        internal void ResetForLevelUnload()
        {
            lock (gate)
            {
                Reset();
                InLockDown = false;
                interventionSink = null;
            }
        }

        public void AddCitizenInQuarantine(uint citizenId, DateTime currentTime)
        {
            lock (gate)
            {
                AddCitizenInIsolation(citizenId, currentTime);
            }
        }

        public void AddCitizenInIsolation(uint citizenId, DateTime currentTime)
        {
            lock (gate)
            {
                if (!citizensInQuarantine.ContainsKey(citizenId))
                {
                    citizensInQuarantine[citizenId] = currentTime;
                    RecordIntervention(citizenId, currentTime, PandemicInterventionType.Isolation, "Start", "CaseIsolation");
                }
            }
        }

        public void AddCheckedCitizen(uint citizenId)
        {
            lock (gate)
            {
                citizensInQuarantineAlreadyChecked.Add(citizenId);
            }
        }

        public void AddCitizenInProphylacticQuarantine(uint citizenId, DateTime currentTime)
        {
            lock (gate)
            {
                AddContactQuarantine(citizenId, currentTime);
            }
        }

        public void AddContactQuarantine(uint citizenId, DateTime currentTime)
        {
            lock (gate)
            {
                if (!prophylacticQuarantine.TryGetValue(citizenId, out DateTime existing))
                {
                    prophylacticQuarantine[citizenId] = currentTime;
                    RecordIntervention(citizenId, currentTime, PandemicInterventionType.Quarantine, "Start", "ContactOrProphylactic");
                }
                else if (currentTime > existing)
                {
                    prophylacticQuarantine[citizenId] = currentTime;
                    RecordIntervention(citizenId, currentTime, PandemicInterventionType.Quarantine, "Extend", "NewContactExposure");
                }
            }
        }

        public void AddPendingTestQuarantine(uint citizenId, DateTime currentTime)
        {
            lock (gate)
            {
                if (!pendingTestQuarantine.ContainsKey(citizenId))
                {
                    pendingTestQuarantine[citizenId] = currentTime;
                    RecordIntervention(citizenId, currentTime, PandemicInterventionType.Quarantine, "Start", "PendingTestResult");
                }
            }
        }

        public void RemovePendingTestQuarantine(uint citizenId, DateTime currentTime, string reason)
        {
            lock (gate)
            {
                if (pendingTestQuarantine.Remove(citizenId))
                {
                    RecordIntervention(
                        citizenId,
                        currentTime,
                        PandemicInterventionType.Quarantine,
                        "End",
                        string.IsNullOrEmpty(reason) ? "PendingTestEnded" : reason);
                }
            }
        }

        public void RemoveCitizenInQuarantine(uint citizenId)
        {
            lock (gate)
            {
                citizensInQuarantine.Remove(citizenId);
                citizensInQuarantineAlreadyChecked.Remove(citizenId);
            }
        }

        public void RemoveAllRestrictions(uint citizenId)
        {
            lock (gate)
            {
                RemoveAllRestrictions(citizenId, DateTime.Now, "ExplicitRemoval");
            }
        }

        internal void RemoveAllRestrictions(uint citizenId, DateTime currentTime, string reason)
        {
            lock (gate)
            {
                bool wasIsolated = citizensInQuarantine.ContainsKey(citizenId);
                bool wasQuarantined = prophylacticQuarantine.ContainsKey(citizenId);
                bool wasPendingTestQuarantined = pendingTestQuarantine.ContainsKey(citizenId);
                citizensInQuarantine.Remove(citizenId);
                citizensInQuarantineAlreadyChecked.Remove(citizenId);
                prophylacticQuarantine.Remove(citizenId);
                pendingTestQuarantine.Remove(citizenId);
                if (wasIsolated)
                {
                    RecordIntervention(citizenId, currentTime, PandemicInterventionType.Isolation, "End", reason);
                }

                if (wasQuarantined)
                {
                    RecordIntervention(citizenId, currentTime, PandemicInterventionType.Quarantine, "End", reason);
                }

                if (wasPendingTestQuarantined)
                {
                    RecordIntervention(citizenId, currentTime, PandemicInterventionType.Quarantine, "End", reason);
                }
            }
        }

        public bool IsInQuarantine(uint citizenId, DateTime currentTime)
        {
            lock (gate)
            {
                if (citizensInQuarantine.ContainsKey(citizenId))
                {
                    if (currentTime >= citizensInQuarantine[citizenId].AddDays(quarantineDuration)) {
                        citizensInQuarantine.Remove(citizenId);
                        citizensInQuarantineAlreadyChecked.Remove(citizenId);
                        RecordIntervention(citizenId, currentTime, PandemicInterventionType.Isolation, "End", "DurationExpired");
                    }
                }
                return citizensInQuarantine.ContainsKey(citizenId)
                    && currentTime >= citizensInQuarantine[citizenId]
                    && currentTime < citizensInQuarantine[citizenId].AddDays(quarantineDuration);
            }
        }

        public bool IsInIsolation(uint citizenId, DateTime currentTime)
        {
            lock (gate)
            {
                return IsInQuarantine(citizenId, currentTime);
            }
        }

        /// <summary>Queries terminal state without expiring or otherwise mutating retained data.</summary>
        internal bool IsInQuarantineReadOnly(uint citizenId, DateTime currentTime)
        {
            lock (gate)
            {
                return citizensInQuarantine.ContainsKey(citizenId)
                    && currentTime >= citizensInQuarantine[citizenId]
                    && currentTime < citizensInQuarantine[citizenId].AddDays(quarantineDuration);
            }
        }

        internal bool IsRestrictedReadOnly(uint citizenId, DateTime currentTime)
        {
            lock (gate)
            {
                return (FollowsRestriction(citizenId, true) && IsInQuarantineReadOnly(citizenId, currentTime))
                    || (FollowsRestriction(citizenId, false) && (IsActive(prophylacticQuarantine, citizenId, currentTime)
                    || IsActive(pendingTestQuarantine, citizenId, currentTime)));
            }
        }

        public bool HasBeenChecked(uint citizenId)
        {
            lock (gate)
            {
                return citizensInQuarantineAlreadyChecked.Contains(citizenId);
            }
        }

        public IEnumerable<uint> GetQuarantinedCitizens()
        {
            lock (gate)
            {
                return new List<uint>(citizensInQuarantine.Keys);
            }
        }

        public bool IsInProphylacticQuarantine(uint citizenId, DateTime currentTime)
        {
            lock (gate)
            {
                if (prophylacticQuarantine.ContainsKey(citizenId))
                {
                    if (currentTime >= prophylacticQuarantine[citizenId].AddDays(quarantineDuration))
                    {
                        prophylacticQuarantine.Remove(citizenId);
                        RecordIntervention(citizenId, currentTime, PandemicInterventionType.Quarantine, "End", "DurationExpired");
                    }
                }
                return prophylacticQuarantine.ContainsKey(citizenId)
                    && currentTime >= prophylacticQuarantine[citizenId]
                    && currentTime < prophylacticQuarantine[citizenId].AddDays(quarantineDuration);
            }
        }

        public bool IsContactQuarantined(uint citizenId, DateTime currentTime)
        {
            lock (gate)
            {
                return IsInProphylacticQuarantine(citizenId, currentTime);
            }
        }

        public bool IsPendingTestQuarantined(uint citizenId, DateTime currentTime)
        {
            lock (gate)
            {
                if (pendingTestQuarantine.ContainsKey(citizenId)
                    && currentTime >= pendingTestQuarantine[citizenId].AddDays(quarantineDuration))
                {
                    pendingTestQuarantine.Remove(citizenId);
                    RecordIntervention(citizenId, currentTime, PandemicInterventionType.Quarantine, "End", "DurationExpired");
                }

                return IsActive(pendingTestQuarantine, citizenId, currentTime);
            }
        }

        public bool IsRestricted(uint citizenId, DateTime currentTime)
        {
            lock (gate)
            {
                bool isolated = IsInIsolation(citizenId, currentTime);
                bool quarantined = IsContactQuarantined(citizenId, currentTime) || IsPendingTestQuarantined(citizenId, currentTime);
                return (isolated && FollowsRestriction(citizenId, true)) || (quarantined && FollowsRestriction(citizenId, false));
            }
        }

        internal IEnumerable<uint> GetIsolatedCitizens()
        {
            lock (gate)
            {
                return new List<uint>(citizensInQuarantine.Keys);
            }
        }

        internal IEnumerable<uint> GetContactQuarantinedCitizens()
        {
            lock (gate)
            {
                return prophylacticQuarantine.Keys
                    .Concat(pendingTestQuarantine.Keys)
                    .Distinct()
                    .ToList();
            }
        }

        internal int CitizensInQuarantine()
        {
            lock (gate)
            {
                return CountRestrictions(true);
            }
        }

        internal int GetIsolatedCount(DateTime currentTime)
        {
            lock (gate)
            {
                ExpireRestrictions(currentTime);
                return citizensInQuarantine.Count;
            }
        }

        internal int GetFollowingCount(DateTime currentTime, bool isolation)
        {
            lock (gate)
            {
                int count = 0;
                var restrictions = isolation ? citizensInQuarantine : prophylacticQuarantine;
                foreach (uint citizen in restrictions.Keys)
                    if (IsActive(restrictions, citizen, currentTime) && FollowsRestriction(citizen, isolation)) count++;
                if (!isolation)
                    foreach (uint citizen in pendingTestQuarantine.Keys)
                        if (!IsActive(prophylacticQuarantine, citizen, currentTime)
                            && IsActive(pendingTestQuarantine, citizen, currentTime) && FollowsRestriction(citizen, false)) count++;
                return count;
            }
        }

        internal int GetContactQuarantinedCount(DateTime currentTime)
        {
            lock (gate)
            {
                ExpireRestrictions(currentTime);
                return CountRestrictions(false);
            }
        }

        internal void SetInterventionSink(Action<PandemicInterventionEvent> sink)
        {
            lock (gate)
            {
                interventionSink = sink;
            }
        }

        // Caller holds gate. Count overlapping restrictions without a temporary set.
        private int CountRestrictions(bool includeIsolation)
        {
            int count = includeIsolation ? citizensInQuarantine.Count : 0;
            foreach (uint citizen in prophylacticQuarantine.Keys)
                if (!includeIsolation || !citizensInQuarantine.ContainsKey(citizen)) count++;
            foreach (uint citizen in pendingTestQuarantine.Keys)
                if (!prophylacticQuarantine.ContainsKey(citizen)
                    && (!includeIsolation || !citizensInQuarantine.ContainsKey(citizen))) count++;
            return count;
        }

        private void ExpireRestrictions(DateTime currentTime)
        {
            foreach (uint citizenId in new List<uint>(citizensInQuarantine.Keys))
            {
                IsInIsolation(citizenId, currentTime);
            }

            foreach (uint citizenId in new List<uint>(prophylacticQuarantine.Keys))
            {
                IsContactQuarantined(citizenId, currentTime);
            }

            foreach (uint citizenId in new List<uint>(pendingTestQuarantine.Keys))
            {
                IsPendingTestQuarantined(citizenId, currentTime);
            }
        }

        private bool IsActive(Dictionary<uint, DateTime> restrictions, uint citizenId, DateTime currentTime)
        {
            return restrictions.ContainsKey(citizenId)
                && currentTime >= restrictions[citizenId]
                && currentTime < restrictions[citizenId].AddDays(quarantineDuration);
        }

        private void RecordIntervention(
            uint citizenId,
            DateTime currentTime,
            PandemicInterventionType type,
            string action,
            string reason)
        {
            interventionSink?.Invoke(new PandemicInterventionEvent
            {
                SimulationTime = currentTime,
                CitizenId = citizenId,
                InterventionType = type,
                Action = action,
                Reason = reason,
                Context = "CitizenRestriction",
                ActuallyFollowed = FollowsRestriction(citizenId, type == PandemicInterventionType.Isolation),
            });
        }
    }
}
