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

        private Dictionary<uint, DateTime> citizensInQuarantine = new Dictionary<uint, DateTime>();
        private HashSet<uint> citizensInQuarantineAlreadyChecked = new HashSet<uint>();
        private Dictionary<uint, DateTime> prophylacticQuarantine = new Dictionary<uint, DateTime>();
        private Dictionary<uint, DateTime> pendingTestQuarantine = new Dictionary<uint, DateTime>();

        private uint quarantineDuration = RestrictionDurationDays;
        private Action<PandemicInterventionEvent> interventionSink;

        public bool InLockDown { get; set; } = false;

        internal void Reset()
        {
            citizensInQuarantine.Clear();
            citizensInQuarantineAlreadyChecked.Clear();
            prophylacticQuarantine.Clear();
            pendingTestQuarantine.Clear();
        }

        /// <summary>Clears all process-wide quarantine state when a level-owned manager is released.</summary>
        internal void ResetForLevelUnload()
        {
            Reset();
            InLockDown = false;
            interventionSink = null;
        }

        public void AddCitizenInQuarantine(uint citizenId, DateTime currentTime)
        {
            AddCitizenInIsolation(citizenId, currentTime);
        }

        public void AddCitizenInIsolation(uint citizenId, DateTime currentTime)
        {
            if (!citizensInQuarantine.ContainsKey(citizenId))
            {
                citizensInQuarantine[citizenId] = currentTime;
                RecordIntervention(citizenId, currentTime, PandemicInterventionType.Isolation, "Start", "CaseIsolation");
            }
        }

        public void AddCheckedCitizen(uint citizenId)
        {
            citizensInQuarantineAlreadyChecked.Add(citizenId);
        }

        public void AddCitizenInProphylacticQuarantine(uint citizenId, DateTime currentTime)
        {
            AddContactQuarantine(citizenId, currentTime);
        }

        public void AddContactQuarantine(uint citizenId, DateTime currentTime)
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

        public void AddPendingTestQuarantine(uint citizenId, DateTime currentTime)
        {
            if (!pendingTestQuarantine.ContainsKey(citizenId))
            {
                pendingTestQuarantine[citizenId] = currentTime;
                RecordIntervention(citizenId, currentTime, PandemicInterventionType.Quarantine, "Start", "PendingTestResult");
            }
        }

        public void RemovePendingTestQuarantine(uint citizenId, DateTime currentTime, string reason)
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

        public void RemoveCitizenInQuarantine(uint citizenId)
        {
            citizensInQuarantine.Remove(citizenId);
            citizensInQuarantineAlreadyChecked.Remove(citizenId);
        }

        public void RemoveAllRestrictions(uint citizenId)
        {
            RemoveAllRestrictions(citizenId, DateTime.Now, "ExplicitRemoval");
        }

        internal void RemoveAllRestrictions(uint citizenId, DateTime currentTime, string reason)
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

        public bool IsInQuarantine(uint citizenId, DateTime currentTime)
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

        public bool IsInIsolation(uint citizenId, DateTime currentTime)
        {
            return IsInQuarantine(citizenId, currentTime);
        }

        /// <summary>Queries terminal state without expiring or otherwise mutating retained data.</summary>
        internal bool IsInQuarantineReadOnly(uint citizenId, DateTime currentTime)
        {
            return citizensInQuarantine.ContainsKey(citizenId)
                && currentTime >= citizensInQuarantine[citizenId]
                && currentTime < citizensInQuarantine[citizenId].AddDays(quarantineDuration);
        }

        internal bool IsRestrictedReadOnly(uint citizenId, DateTime currentTime)
        {
            return IsInQuarantineReadOnly(citizenId, currentTime)
                || IsActive(prophylacticQuarantine, citizenId, currentTime)
                || IsActive(pendingTestQuarantine, citizenId, currentTime);
        }

        public bool HasBeenChecked(uint citizenId)
        {
            return citizensInQuarantineAlreadyChecked.Contains(citizenId);
        }

        public IEnumerable<uint> GetQuarantinedCitizens()
        {
            return new List<uint>(citizensInQuarantine.Keys);
        }

        public bool IsInProphylacticQuarantine(uint citizenId, DateTime currentTime)
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

        public bool IsContactQuarantined(uint citizenId, DateTime currentTime)
        {
            return IsInProphylacticQuarantine(citizenId, currentTime);
        }

        public bool IsPendingTestQuarantined(uint citizenId, DateTime currentTime)
        {
            if (pendingTestQuarantine.ContainsKey(citizenId)
                && currentTime >= pendingTestQuarantine[citizenId].AddDays(quarantineDuration))
            {
                pendingTestQuarantine.Remove(citizenId);
                RecordIntervention(citizenId, currentTime, PandemicInterventionType.Quarantine, "End", "DurationExpired");
            }

            return IsActive(pendingTestQuarantine, citizenId, currentTime);
        }

        public bool IsRestricted(uint citizenId, DateTime currentTime)
        {
            return IsInIsolation(citizenId, currentTime)
                || IsContactQuarantined(citizenId, currentTime)
                || IsPendingTestQuarantined(citizenId, currentTime);
        }

        internal IEnumerable<uint> GetIsolatedCitizens()
        {
            return new List<uint>(citizensInQuarantine.Keys);
        }

        internal IEnumerable<uint> GetContactQuarantinedCitizens()
        {
            return prophylacticQuarantine.Keys
                .Concat(pendingTestQuarantine.Keys)
                .Distinct()
                .ToList();
        }

        internal int CitizensInQuarantine()
        {
            return citizensInQuarantine.Keys
                .Concat(prophylacticQuarantine.Keys)
                .Concat(pendingTestQuarantine.Keys)
                .Distinct()
                .Count();
        }

        internal int GetIsolatedCount(DateTime currentTime)
        {
            ExpireRestrictions(currentTime);
            return citizensInQuarantine.Count;
        }

        internal int GetContactQuarantinedCount(DateTime currentTime)
        {
            ExpireRestrictions(currentTime);
            return prophylacticQuarantine.Keys
                .Concat(pendingTestQuarantine.Keys)
                .Distinct()
                .Count();
        }

        internal void SetInterventionSink(Action<PandemicInterventionEvent> sink)
        {
            interventionSink = sink;
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
            });
        }
    }
}
