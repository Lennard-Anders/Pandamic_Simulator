using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RealTime.Pandemic
{
    class QuarantineManager
    {
        public static QuarantineManager Instance { get; } = new QuarantineManager();

        private Dictionary<uint, DateTime> citizensInQuarantine = new Dictionary<uint, DateTime>();
        private HashSet<uint> citizensInQuarantineAlreadyChecked = new HashSet<uint>();
        private Dictionary<uint, DateTime> prophylacticQuarantine = new Dictionary<uint, DateTime>();

        private uint quarantineDuration = 10;

        public bool InLockDown { get; set; } = false;

        public void AddCitizenInQuarantine(uint citizenId, DateTime currentTime)
        {
            citizensInQuarantine[citizenId] = currentTime;
        }

        public void AddCheckedCitizen(uint citizenId)
        {
            citizensInQuarantineAlreadyChecked.Add(citizenId);
        }

        public void AddCitizenInProphylacticQuarantine(uint citizenId, DateTime currentTime)
        {
            prophylacticQuarantine[citizenId] = currentTime;
        }

        public void RemoveCitizenInQuarantine(uint citizenId)
        {
            citizensInQuarantine.Remove(citizenId);
            citizensInQuarantineAlreadyChecked.Remove(citizenId);
        }

        public bool IsInQuarantine(uint citizenId, DateTime currentTime)
        {
            if (citizensInQuarantine.ContainsKey(citizenId))
            {
                if ((currentTime.Ticks - citizensInQuarantine[citizenId].Ticks) / TimeSpan.TicksPerDay > quarantineDuration) {
                    citizensInQuarantine.Remove(citizenId);
                    citizensInQuarantineAlreadyChecked.Remove(citizenId);
                }
            }
            return citizensInQuarantine.ContainsKey(citizenId) && (currentTime.Ticks - citizensInQuarantine[citizenId].Ticks) / TimeSpan.TicksPerDay <= quarantineDuration;
        }

        public bool HasBeenChecked(uint citizenId)
        {
            return citizensInQuarantineAlreadyChecked.Contains(citizenId);
        }

        public bool IsInProphylacticQuarantine(uint citizenId, DateTime currentTime)
        {
            if (prophylacticQuarantine.ContainsKey(citizenId))
            {
                if ((currentTime.Ticks - prophylacticQuarantine[citizenId].Ticks) / TimeSpan.TicksPerDay > quarantineDuration)
                {
                    prophylacticQuarantine.Remove(citizenId);
                }
            }
            return prophylacticQuarantine.ContainsKey(citizenId) && (currentTime.Ticks - prophylacticQuarantine[citizenId].Ticks) / TimeSpan.TicksPerDay <= quarantineDuration;
        }

        internal int CitizensInQuarantine()
        {
            return prophylacticQuarantine.Count + citizensInQuarantine.Count;
        }
    }
}
