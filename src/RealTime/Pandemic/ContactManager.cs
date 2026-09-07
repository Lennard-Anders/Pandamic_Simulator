using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RealTime.Core;

namespace RealTime.Pandemic
{
    /// <summary>
    /// Records every physical encounter first and derives traceability separately. The retained
    /// adjacency graph contains traceable contacts for the legacy quarantine workflow, while the
    /// complete physical event history is never discarded or overwritten.
    /// </summary>
    class ContactManager
    {
        public static ContactManager Instance { get; } = new ContactManager();

        private readonly ContactEngine contactEngine = new ContactEngine();
        private readonly Dictionary<uint, Dictionary<uint, DateTime>> contacts = new Dictionary<uint, Dictionary<uint, DateTime>>();
        private readonly HashSet<ulong> traceablePairs = new HashSet<ulong>();
        private ContactTracingEngine tracingEngine = new ContactTracingEngine(new ContactTracingPolicy(), 1337);
        private Config.RealTimeConfig config;
        private int masterSeed = 1337;
        private long totalPhysicalContacts;
        private long totalTraceableContacts;
        private long totalRecordedBuildingContacts;
        private long totalRecordedNonBuildingContacts;
        private DateTime nextPruneTime;
        private readonly List<KeyValuePair<uint, uint>> expiredDirections = new List<KeyValuePair<uint, uint>>();
        private readonly List<uint> emptyCitizens = new List<uint>();

        internal void PruneExpired(DateTime simulationTime, TimeSpan lookback)
        {
            if (lookback < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(lookback));
            if (simulationTime < nextPruneTime) return;
            nextPruneTime = simulationTime.AddDays(1);
            DateTime cutoff = simulationTime - lookback;
            expiredDirections.Clear(); emptyCitizens.Clear();
            foreach (var citizen in contacts)
                foreach (var contact in citizen.Value)
                    if (contact.Value <= cutoff) expiredDirections.Add(new KeyValuePair<uint, uint>(citizen.Key, contact.Key));
            foreach (var expired in expiredDirections)
            {
                contacts[expired.Key].Remove(expired.Value);
                traceablePairs.Remove(CreatePairKey(expired.Key, expired.Value));
            }
            foreach (var citizen in contacts) if (citizen.Value.Count == 0) emptyCitizens.Add(citizen.Key);
            foreach (uint citizen in emptyCitizens) contacts.Remove(citizen);
        }

        public void Init(Config.RealTimeConfig newConfig)
        {
            config = newConfig ?? throw new ArgumentNullException(nameof(newConfig));
            ResetRuntimeState();
            ResetTracingEngine();
        }

        internal void ResetStableTraits(int seed)
        {
            if (seed < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(seed));
            }

            masterSeed = seed;
            if (config != null)
            {
                ResetTracingEngine();
            }
        }

        internal void ResetForLevelUnload()
        {
            config = null;
            ResetRuntimeState();
            tracingEngine = new ContactTracingEngine(new ContactTracingPolicy(), masterSeed);
        }

        public PhysicalContactEvent AddPhysicalContact(PhysicalContactRequest request)
        {
            bool ignored;
            return AddPhysicalContact(request, out ignored);
        }

        internal PhysicalContactEvent AddPhysicalContact(PhysicalContactRequest request, out bool created)
        {
            PhysicalContactEvent contact = contactEngine.Record(request, out created);
            if (!created)
            {
                return contact;
            }

            ContactTraceability traceability = tracingEngine.Evaluate(contact);
            contact.TraceableByApp = traceability.TraceableByApp;
            contact.TraceableByManual = traceability.TraceableByManual;
            totalPhysicalContacts++;
            if (contact.BuildingId != 0)
            {
                totalRecordedBuildingContacts++;
            }
            else
            {
                totalRecordedNonBuildingContacts++;
            }

            if (traceability.IsTraceable)
            {
                AddTraceableDirection(contact.CitizenA, contact.CitizenB, contact.EndTime);
                AddTraceableDirection(contact.CitizenB, contact.CitizenA, contact.EndTime);
                traceablePairs.Add(CreatePairKey(contact.CitizenA, contact.CitizenB));
                totalTraceableContacts++;
            }

            return contact;
        }

        /// <summary>Compatibility adapter for callers that do not yet provide full context.</summary>
        public void AddContact(uint citizenId, uint contactId, bool inBuilding, DateTime currentTime)
        {
            AddPhysicalContact(new PhysicalContactRequest
            {
                CitizenA = citizenId,
                CitizenB = contactId,
                EndTime = currentTime,
                DurationMinutes = 1d,
                Context = PhysicalContactContext.Other,
                BuildingId = inBuilding ? (ushort)1 : (ushort)0,
            });
        }

        public Dictionary<uint, DateTime> GetContactsForCitizen(uint citizenId)
        {
            return contacts.TryGetValue(citizenId, out Dictionary<uint, DateTime> result) ? result : null;
        }

        internal IList<PhysicalContactEvent> GetPhysicalContacts() => contactEngine.GetEvents();

        internal void SetRetainPhysicalHistory(bool retain) => contactEngine.SetRetainHistory(retain);

        public int GetTrackedCitizenCount() => contacts.Count;

        public int GetTrackedPairCount() => traceablePairs.Count;

        public long GetTotalRecordedContacts() => totalPhysicalContacts;

        internal long GetTotalTraceableContacts() => totalTraceableContacts;

        public long GetTotalRecordedBuildingContacts() => totalRecordedBuildingContacts;

        public long GetTotalRecordedNonBuildingContacts() => totalRecordedNonBuildingContacts;

        internal bool CitizenUsesAppForTesting(uint citizenId) => tracingEngine.UsesApp(citizenId);

        internal bool CitizenUsesManualTracingForTesting(uint citizenId) => tracingEngine.IsManuallyTraceable(citizenId);

        public void WriteToDisk()
        {
            string modRoot = ModPaths.GetModRoot();
            if (string.IsNullOrEmpty(modRoot))
            {
                return;
            }

            WriteToDisk(Path.Combine(modRoot, "contacts.csv"));
        }

        internal void WriteToDisk(string outputFile)
        {
            if (string.IsNullOrEmpty(outputFile))
            {
                return;
            }

            string directory = Path.GetDirectoryName(outputFile);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(outputFile, BuildCsv());
        }

        internal string BuildCsv()
        {
            var csv = new StringBuilder();
            csv.AppendLine("citizen_id;contact_id;last_contact_time_ms");
            foreach (KeyValuePair<uint, Dictionary<uint, DateTime>> citizen in contacts.OrderBy(item => item.Key))
            {
                foreach (KeyValuePair<uint, DateTime> contact in citizen.Value.OrderBy(item => item.Key))
                {
                    csv.AppendLine($"{citizen.Key};{contact.Key};{contact.Value.Ticks / TimeSpan.TicksPerMillisecond}");
                }
            }

            csv.AppendLine();
            csv.AppendLine($"#tracked_citizens;{GetTrackedCitizenCount()}");
            csv.AppendLine($"#tracked_pairs;{GetTrackedPairCount()}");
            csv.AppendLine($"#total_recorded_contacts;{GetTotalRecordedContacts()}");
            csv.AppendLine($"#traceable_contacts;{GetTotalTraceableContacts()}");
            csv.AppendLine($"#recorded_building_contacts;{GetTotalRecordedBuildingContacts()}");
            csv.AppendLine($"#recorded_non_building_contacts;{GetTotalRecordedNonBuildingContacts()}");
            return csv.ToString();
        }

        private void ResetRuntimeState()
        {
            nextPruneTime = default(DateTime);
            expiredDirections.Clear(); emptyCitizens.Clear();
            contacts.Clear();
            traceablePairs.Clear();
            contactEngine.Reset();
            totalPhysicalContacts = 0L;
            totalTraceableContacts = 0L;
            totalRecordedBuildingContacts = 0L;
            totalRecordedNonBuildingContacts = 0L;
        }

        private void ResetTracingEngine()
        {
            tracingEngine.Reset(new ContactTracingPolicy
            {
                AppAdoptionPercent = config.AppBasedContactTracingProbability,
                ManualTraceabilityPercent = config.BuildingContactTracingProbability,
            }, masterSeed);
        }

        private void AddTraceableDirection(uint citizenId, uint contactId, DateTime currentTime)
        {
            if (!contacts.TryGetValue(citizenId, out Dictionary<uint, DateTime> citizenContacts))
            {
                citizenContacts = new Dictionary<uint, DateTime>();
                contacts.Add(citizenId, citizenContacts);
            }

            citizenContacts[contactId] = currentTime;
        }

        private static ulong CreatePairKey(uint citizenA, uint citizenB)
        {
            uint lower = Math.Min(citizenA, citizenB);
            uint upper = Math.Max(citizenA, citizenB);
            return ((ulong)lower << 32) | upper;
        }
    }
}
