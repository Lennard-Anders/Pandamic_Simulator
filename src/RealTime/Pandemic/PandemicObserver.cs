using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RealTime.Core;
using RealTime.GameConnection;
using UnityEngine;

namespace RealTime.Pandemic
{
    class PandemicObserver
    {
        public PandemicObservation GeneralObservation { get; } = new PandemicObservation(default);
        List<PandemicObservation> observations = new List<PandemicObservation>();
        Dictionary<PandemicInfectionOriginCategory, int> totalOriginCounts = new Dictionary<PandemicInfectionOriginCategory, int>();
        DateTime ignored;
        bool frozen;

        bool exported = false;
        int totalIndoorInfections;
        int totalOutdoorInfections;
        int totalVehicleInfections;

        internal void Reset()
        {
            observations.Clear();
            ignored = default;
            frozen = false;
            exported = false;
            totalIndoorInfections = 0;
            totalOutdoorInfections = 0;
            totalVehicleInfections = 0;
            totalOriginCounts.Clear();
            GeneralObservation.HealthyCitizens = 0;
            GeneralObservation.SickCitizens = 0;
            GeneralObservation.RecoveredCitizens = 0;
            GeneralObservation.DeadCitizens = 0;
            GeneralObservation.Infections.Clear();
        }

        public void WriteToDisc(bool force)
        {
            if (!force && exported)
            {
                return;
            }

            string modRoot = ModPaths.GetModRoot();
            if (string.IsNullOrEmpty(modRoot))
            {
                return;
            }

            WriteToDisc(force, Path.Combine(modRoot, "data.csv"));
        }

        /// <summary>Writes the retained raw observations to an explicit run-owned destination.</summary>
        internal void WriteToDisc(bool force, string outputFile)
        {
            if (!force && exported)
            {
                return;
            }

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

            if (!force)
            {
                exported = true;
            }
        }

        /// <summary>Builds the legacy observer CSV without performing any file-system IO.</summary>
        internal string BuildCsv()
        {
            var csv = new StringBuilder();
            csv.AppendLine("time_ms;healthy;sick;recovered;dead");

            foreach (PandemicObservation observation in observations)
            {
                var newLine = string.Format("{0};{1};{2};{3};{4}", observation.SimulationTime.Ticks / 10000, observation.HealthyCitizens, observation.SickCitizens, observation.RecoveredCitizens, observation.DeadCitizens);
                csv.AppendLine(newLine);
            }

            csv.AppendLine();
            csv.AppendLine();

            foreach (uint citizenID in GeneralObservation.Infections.Keys)
            {
                csv.AppendLine(string.Empty + citizenID);

                foreach (Infection infection in GeneralObservation.Infections[citizenID])
                {
                    csv.AppendLine(string.Format(
                        ";{0};{1};{2};{3};{4};{5};{6};{7}",
                        infection.infectedCitizenId,
                        infection.type,
                        infection.OriginCategory,
                        infection.time.Ticks / TimeSpan.TicksPerMillisecond,
                        infection.position,
                        infection.buildingType,
                        infection.BuildingId,
                        infection.VehicleId));
                }
            }

            return csv.ToString();
        }

        internal void FinishObservations(DateTime currentDateTime)
        {
            ignored = currentDateTime;
        }

        /// <summary>Prevents any mutation after a controller-owned terminal snapshot has been reached.</summary>
        internal void Freeze()
        {
            frozen = true;
        }

        public uint GetRecoveredCitizens()
        {
            if (observations.Count == 0)
            {
                return 0;
            }

            return observations[observations.Count - 1].RecoveredCitizens;
        }

        public uint GetSickCitizens()
        {
            if (observations.Count == 0)
            {
                return 0;
            }

            return observations[observations.Count - 1].SickCitizens;
        }

        public uint GetHealthyCitizens()
        {
            if (observations.Count == 0)
            {
                return 0;
            }

            return observations[observations.Count - 1].HealthyCitizens;
        }

        public uint GetDeadCitizens()
        {
            if (observations.Count == 0)
            {
                return 0;
            }

            return observations[observations.Count - 1].DeadCitizens;
        }

        public void AddSickCitizen(DateTime simulationTime)
        {
            if (ShouldIgnore(simulationTime))
            {
                return;
            }
            PandemicObservation observation = GetObservation(simulationTime);
            observation.SickCitizens++;
        }

        public void AddSickCitizens(DateTime simulationTime, int count)
        {
            if (ShouldIgnore(simulationTime))
            {
                return;
            }
            PandemicObservation observation = GetObservation(simulationTime);
            observation.SickCitizens = (uint)count;
        }

        public void AddExposedCitizens(DateTime simulationTime, int count)
        {
            if (ShouldIgnore(simulationTime))
            {
                return;
            }
            PandemicObservation observation = GetObservation(simulationTime);
            observation.ExposedCitizens = (uint)count;
        }

        public void AddLocationSnapshot(DateTime simulationTime, int atHome, int atWork, int visiting, int inTransit, int onFoot)
        {
            if (ShouldIgnore(simulationTime))
            {
                return;
            }
            PandemicObservation observation = GetObservation(simulationTime);
            observation.CitizensAtHome     = (uint)atHome;
            observation.CitizensAtWork     = (uint)atWork;
            observation.CitizensVisiting   = (uint)visiting;
            observation.CitizensInTransit  = (uint)inTransit;
            observation.CitizensOnFoot     = (uint)onFoot;
        }

        public void AddRecoveredCitizen(DateTime simulationTime)
        {

            if (ShouldIgnore(simulationTime))
            {
                return;
            }
            PandemicObservation observation = GetObservation(simulationTime);
            observation.RecoveredCitizens++;
        }

        public void AddRecoveredCitizens(DateTime simulationTime, int count)
        {

            if (ShouldIgnore(simulationTime))
            {
                return;
            }
            PandemicObservation observation = GetObservation(simulationTime);
            observation.RecoveredCitizens = (uint)count;
        }

        public void AddDeadCitizen(DateTime simulationTime)
        {

            if (ShouldIgnore(simulationTime))
            {
                return;
            }
            PandemicObservation observation = GetObservation(simulationTime);
            observation.DeadCitizens++;
        }

        public void AddDeadCitizens(DateTime simulationTime, int count)
        {
            if (ShouldIgnore(simulationTime))
            {
                return;
            }
            PandemicObservation observation = GetObservation(simulationTime);
            observation.DeadCitizens = (uint)count;
        }

        public void AddHealthyCitizen(DateTime simulationTime)
        {

            if (ShouldIgnore(simulationTime))
            {
                return;
            }
            PandemicObservation observation = GetObservation(simulationTime);
            observation.HealthyCitizens++;
        }

        public void AddHealthyCitizens(DateTime simulationTime, int count)
        {

            if (ShouldIgnore(simulationTime))
            {
                return;
            }
            PandemicObservation observation = GetObservation(simulationTime);
            observation.HealthyCitizens = (uint)count;
        }

        public void AddInfectiousCitizen(uint infectingCitizenId)
        {
            if (frozen)
            {
                return;
            }

            GeneralObservation.AddInfectiousCitizen(infectingCitizenId);
        }

        public void AddCitizenInfection(uint infectingCitizenId, uint infectedCitizenId, DateTime simulationTime, PandemicInfectionOriginInfo origin)
        {
            if (ShouldIgnore(simulationTime))
            {
                return;
            }

            if (origin == null)
            {
                origin = new PandemicInfectionOriginInfo
                {
                    Category = PandemicInfectionOriginCategory.OtherUnknown,
                    LegacyType = InfectionType.OUTDOOR,
                    Position = Vector3.zero,
                };
            }

            CountInfection(origin.LegacyType);
            CountOrigin(origin.Category);
            PandemicObservation observation = GetObservation(simulationTime);
            observation.AddInfection(infectingCitizenId, infectedCitizenId, simulationTime, origin);
            GeneralObservation.AddInfection(infectingCitizenId, infectedCitizenId, simulationTime, origin);
        }

        public int GetObservationCount()
        {
            return observations.Count;
        }

        public int GetTotalInfections()
        {
            return totalIndoorInfections + totalOutdoorInfections + totalVehicleInfections;
        }

        public int GetIndoorInfections()
        {
            return totalIndoorInfections;
        }

        public int GetOutdoorInfections()
        {
            return totalOutdoorInfections;
        }

        public int GetVehicleInfections()
        {
            return totalVehicleInfections;
        }

        public IDictionary<PandemicInfectionOriginCategory, int> GetOriginCounts()
        {
            return new Dictionary<PandemicInfectionOriginCategory, int>(totalOriginCounts);
        }

        public IList<PandemicObservation> GetObservations()
        {
            return new List<PandemicObservation>(observations);
        }

        internal IList<PandemicObservation> GetObservationView()
        {
            return observations;
        }

        public bool TryGetLatestObservation(out PandemicObservation observation)
        {
            if (observations.Count == 0)
            {
                observation = null;
                return false;
            }

            observation = observations[observations.Count - 1];
            return true;
        }

        public bool TryGetPreviousObservation(out PandemicObservation observation)
        {
            if (observations.Count < 2)
            {
                observation = null;
                return false;
            }

            observation = observations[observations.Count - 2];
            return true;
        }

        private PandemicObservation GetObservation(DateTime simulationTime)
        {
            if (observations.Count > 0)
            {
                PandemicObservation lastObservation = observations[observations.Count - 1];
                if (lastObservation.SimulationTime.Ticks == simulationTime.Ticks)
                {
                    return lastObservation;
                }
                PandemicObservation observation = new PandemicObservation(simulationTime);
                observation.DeadCitizens = lastObservation.DeadCitizens;
                observation.RecoveredCitizens = lastObservation.RecoveredCitizens;
                observations.Add(observation);
                return observation;
            } else
            {
                PandemicObservation observation = new PandemicObservation(simulationTime);
                observations.Add(observation);
                return observation;
            }
        }

        private bool ShouldIgnore(DateTime simulationTime)
        {
            return frozen || simulationTime.Ticks == ignored.Ticks;
        }

        private void CountInfection(InfectionType type)
        {
            switch (type)
            {
                case InfectionType.INDOOR:
                    totalIndoorInfections++;
                    break;
                case InfectionType.OUTDOOR:
                    totalOutdoorInfections++;
                    break;
                case InfectionType.VEHICLE:
                    totalVehicleInfections++;
                    break;
            }
        }

        private void CountOrigin(PandemicInfectionOriginCategory category)
        {
            if (totalOriginCounts.ContainsKey(category))
            {
                totalOriginCounts[category]++;
            }
            else
            {
                totalOriginCounts[category] = 1;
            }
        }
    }

    internal class PandemicObservation
    {
        public PandemicObservation(DateTime simulationTime)
        {
            SimulationTime = simulationTime;
        }

        public DateTime SimulationTime { get; }

        public uint HealthyCitizens { get; set; }
        public uint SickCitizens { get; set; }
        public uint ExposedCitizens { get; set; }
        public uint RecoveredCitizens { get; set; }
        public uint DeadCitizens { get; set; }

        public uint CitizensAtHome { get; set; }
        public uint CitizensAtWork { get; set; }
        public uint CitizensVisiting { get; set; }
        public uint CitizensInTransit { get; set; }
        public uint CitizensOnFoot { get; set; }

        public Dictionary<uint, List<Infection>> Infections { get; } = new Dictionary<uint, List<Infection>>();

        internal void AddInfection(uint infectingCitizenId, uint infectedCitizenId, DateTime currentTime, PandemicInfectionOriginInfo origin)
        {
            if (!Infections.ContainsKey(infectingCitizenId))
            {
                Infections.Add(infectingCitizenId, new List<Infection>());
            }
            Infections[infectingCitizenId].Add(new Infection(infectedCitizenId, currentTime, origin));
        }

        internal void AddInfectiousCitizen(uint infectingCitizenId)
        {
            if (!Infections.ContainsKey(infectingCitizenId))
            {
                Infections.Add(infectingCitizenId, new List<Infection>());
            }
        }
    }

    internal class Infection
    {
        public InfectionType type { get; }
        public DateTime time { get; }
        public uint infectedCitizenId { get; }
        public Vector3 position { get; }
        public ItemClass.Service buildingType { get; }
        public ItemClass.SubService buildingSubType { get; }
        public PandemicInfectionOriginCategory OriginCategory { get; }
        public ushort BuildingId { get; }
        public ushort VehicleId { get; }

        public Infection(uint infectedCitizenId, DateTime time, PandemicInfectionOriginInfo origin)
        {
            this.infectedCitizenId = infectedCitizenId;
            this.time = time;
            type = origin?.LegacyType ?? InfectionType.OUTDOOR;
            position = origin?.Position ?? Vector3.zero;
            buildingType = origin?.BuildingService ?? ItemClass.Service.None;
            buildingSubType = origin?.BuildingSubService ?? ItemClass.SubService.None;
            OriginCategory = origin?.Category ?? PandemicInfectionOriginCategory.OtherUnknown;
            BuildingId = origin?.BuildingId ?? 0;
            VehicleId = origin?.VehicleId ?? 0;
        }
    }

    public enum InfectionType
    {
        INDOOR, OUTDOOR, VEHICLE
    }
}
