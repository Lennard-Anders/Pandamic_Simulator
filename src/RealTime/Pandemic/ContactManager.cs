using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RealTime.Core;
using UnityEngine;

namespace RealTime.Pandemic
{
    class ContactManager
    {
        public static ContactManager Instance { get; } = new ContactManager();

        private System.Random random = new System.Random(1337);

        private Dictionary<uint, Dictionary<uint, DateTime>> contacts = new Dictionary<uint, Dictionary<uint, DateTime>>();

        private HashSet<uint> citizensUsingContactTracingBuilding = new HashSet<uint>();
        private HashSet<uint> citizensNotUsingContactTracingBuilding = new HashSet<uint>();

        private HashSet<uint> citizensUsingContactTracingApp = new HashSet<uint>();
        private HashSet<uint> citizensNotUsingContactTracingApp = new HashSet<uint>();

        private long totalRecordedContacts;
        private long totalRecordedBuildingContacts;
        private long totalRecordedNonBuildingContacts;
        private int totalDistinctContactPairs;

        private Config.RealTimeConfig config;

        public void Init(Config.RealTimeConfig config)
        {
            this.config = config;
            ResetRuntimeState();
        }

        /// <summary>Starts a new deterministic contact-tracing random stream.</summary>
        internal void ResetRandom(int seed)
        {
            random = new System.Random(seed);
        }

        /// <summary>Clears references and cached state retained by this process-wide singleton.</summary>
        internal void ResetForLevelUnload()
        {
            config = null;
            ResetRuntimeState();
        }

        private void ResetRuntimeState()
        {
            contacts.Clear();
            citizensUsingContactTracingBuilding.Clear();
            citizensNotUsingContactTracingBuilding.Clear();
            citizensUsingContactTracingApp.Clear();
            citizensNotUsingContactTracingApp.Clear();
            totalRecordedContacts = 0;
            totalRecordedBuildingContacts = 0;
            totalRecordedNonBuildingContacts = 0;
            totalDistinctContactPairs = 0;
        }

        private bool CitizenUsesContactTracing(uint citizenId, bool inBuilding)
        {
            if (!citizensUsingContactTracingApp.Contains(citizenId) && !citizensNotUsingContactTracingApp.Contains(citizenId))
            {
                if (random.NextDouble() < config.AppBasedContactTracingProbability / 100.0)
                {
                    citizensUsingContactTracingApp.Add(citizenId);
                }
                else
                {
                    citizensNotUsingContactTracingApp.Add(citizenId);
                }
            }

            if (inBuilding)
            {
                if (!citizensUsingContactTracingBuilding.Contains(citizenId) && !citizensNotUsingContactTracingBuilding.Contains(citizenId))
                {
                    if (random.NextDouble() < config.BuildingContactTracingProbability / 100.0)
                    {
                        citizensUsingContactTracingBuilding.Add(citizenId);
                    } else
                    {
                        citizensNotUsingContactTracingBuilding.Add(citizenId);
                    }
                }
            }
            return (citizensUsingContactTracingBuilding.Contains(citizenId) && inBuilding) || citizensUsingContactTracingApp.Contains(citizenId);
        }

        public void AddContact(uint citizenId, uint contactId, bool inBuilding, DateTime currentTime)
        {
            if (CitizenUsesContactTracing(citizenId, inBuilding) && CitizenUsesContactTracing(contactId, inBuilding))
            {
                if (!contacts.ContainsKey(citizenId))
                {
                    contacts.Add(citizenId, new Dictionary<uint, DateTime>());
                }

                if (!contacts[citizenId].ContainsKey(contactId))
                {
                    totalDistinctContactPairs++;
                }

                contacts[citizenId][contactId] = currentTime;
                totalRecordedContacts++;
                if (inBuilding)
                {
                    totalRecordedBuildingContacts++;
                }
                else
                {
                    totalRecordedNonBuildingContacts++;
                }
            }
        }

        public Dictionary<uint, DateTime> GetContactsForCitizen(uint citizenId)
        {
            if (contacts.ContainsKey(citizenId))
            {
                return contacts[citizenId];
            }
            return null;
        }

        public int GetTrackedCitizenCount() => contacts.Count;

        public int GetTrackedPairCount() => totalDistinctContactPairs;

        public long GetTotalRecordedContacts() => totalRecordedContacts;

        public long GetTotalRecordedBuildingContacts() => totalRecordedBuildingContacts;

        public long GetTotalRecordedNonBuildingContacts() => totalRecordedNonBuildingContacts;

        public void WriteToDisk()
        {
            string modRoot = ModPaths.GetModRoot();
            if (string.IsNullOrEmpty(modRoot))
            {
                return;
            }

            WriteToDisk(Path.Combine(modRoot, "contacts.csv"));
        }

        /// <summary>Writes the retained contact graph to an explicit run-owned destination.</summary>
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

        /// <summary>Builds the legacy contacts CSV without performing any file-system IO.</summary>
        internal string BuildCsv()
        {
            var csv = new StringBuilder();
            csv.AppendLine("citizen_id;contact_id;last_contact_time_ms");

            foreach (var citizen in contacts.OrderBy(c => c.Key))
            {
                foreach (var contact in citizen.Value.OrderBy(c => c.Key))
                {
                    csv.AppendLine($"{citizen.Key};{contact.Key};{contact.Value.Ticks / TimeSpan.TicksPerMillisecond}");
                }
            }

            csv.AppendLine();
            csv.AppendLine($"#tracked_citizens;{GetTrackedCitizenCount()}");
            csv.AppendLine($"#tracked_pairs;{GetTrackedPairCount()}");
            csv.AppendLine($"#total_recorded_contacts;{GetTotalRecordedContacts()}");
            csv.AppendLine($"#recorded_building_contacts;{GetTotalRecordedBuildingContacts()}");
            csv.AppendLine($"#recorded_non_building_contacts;{GetTotalRecordedNonBuildingContacts()}");

            return csv.ToString();
        }
    }
}
