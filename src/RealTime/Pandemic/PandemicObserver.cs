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
        DateTime ignored;

        bool exported = false;
        int totalIndoorInfections;
        int totalOutdoorInfections;
        int totalVehicleInfections;

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
                    csv.AppendLine(string.Format(";{0};{1};{2};{3};{4}", infection.infectedCitizenId, infection.type, infection.time.Ticks / TimeSpan.TicksPerMillisecond, infection.position, infection.buildingType));
                }
            }

            string outputFile = Path.Combine(modRoot, "data.csv");
            File.WriteAllText(outputFile, csv.ToString());

            if (!force)
            {
                exported = true;
            }
        }

        internal void FinishObservations(DateTime currentDateTime)
        {
            ignored = currentDateTime;
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
            if (simulationTime.Ticks == ignored.Ticks)
            {
                return;
            }
            PandemicObservation observation = GetObservation(simulationTime);
            observation.SickCitizens++;
        }

        public void AddSickCitizens(DateTime simulationTime, int count)
        {
            if (simulationTime.Ticks == ignored.Ticks)
            {
                return;
            }
            PandemicObservation observation = GetObservation(simulationTime);
            observation.SickCitizens = (uint)count;
        }

        public void AddRecoveredCitizen(DateTime simulationTime)
        {

            if (simulationTime.Ticks == ignored.Ticks)
            {
                return;
            }
            PandemicObservation observation = GetObservation(simulationTime);
            observation.RecoveredCitizens++;
        }

        public void AddRecoveredCitizens(DateTime simulationTime, int count)
        {

            if (simulationTime.Ticks == ignored.Ticks)
            {
                return;
            }
            PandemicObservation observation = GetObservation(simulationTime);
            observation.RecoveredCitizens = (uint)count;
        }

        public void AddDeadCitizen(DateTime simulationTime)
        {

            if (simulationTime.Ticks == ignored.Ticks)
            {
                return;
            }
            PandemicObservation observation = GetObservation(simulationTime);
            observation.DeadCitizens++;
        }

        public void AddDeadCitizens(DateTime simulationTime, int count)
        {
            if (simulationTime.Ticks == ignored.Ticks)
            {
                return;
            }
            PandemicObservation observation = GetObservation(simulationTime);
            observation.DeadCitizens = (uint)count;
        }

        public void AddHealthyCitizen(DateTime simulationTime)
        {

            if (simulationTime.Ticks == ignored.Ticks)
            {
                return;
            }
            PandemicObservation observation = GetObservation(simulationTime);
            observation.HealthyCitizens++;
        }

        public void AddHealthyCitizens(DateTime simulationTime, int count)
        {

            if (simulationTime.Ticks == ignored.Ticks)
            {
                return;
            }
            PandemicObservation observation = GetObservation(simulationTime);
            observation.HealthyCitizens = (uint)count;
        }

        public void AddInfectiousCitizen(uint infectingCitizenId)
        {
            GeneralObservation.AddInfectiousCitizen(infectingCitizenId);
        }

        public void AddCitizenInfection(uint infectingCitizenId, uint infectedCitizenId, InfectionType type, DateTime simulationTime, Vector3 position, ItemClass.Service buildingType)
        {
            if (simulationTime.Ticks == ignored.Ticks)
            {
                return;
            }
            CountInfection(type);
            PandemicObservation observation = GetObservation(simulationTime);
            observation.AddInfection(infectingCitizenId, infectedCitizenId, simulationTime, type, position, buildingType);
            GeneralObservation.AddInfection(infectingCitizenId, infectedCitizenId, simulationTime, type, position, buildingType);
        }

        public void AddCitizenInfection(uint infectingCitizenId, uint infectedCitizenId, InfectionType type, DateTime simulationTime, Vector3 position)
        {
            if (simulationTime.Ticks == ignored.Ticks)
            {
                return;
            }
            CountInfection(type);
            PandemicObservation observation = GetObservation(simulationTime);
            observation.AddInfection(infectingCitizenId, infectedCitizenId, simulationTime, type, position);
            GeneralObservation.AddInfection(infectingCitizenId, infectedCitizenId, simulationTime, type, position);
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
    }

    public class PandemicObservation
    {
        public PandemicObservation(DateTime simulationTime)
        {
            SimulationTime = simulationTime;
        }

        public DateTime SimulationTime { get; }

        public uint HealthyCitizens { get; set; }
        public uint SickCitizens { get; set; }
        public uint RecoveredCitizens { get; set; }
        public uint DeadCitizens { get; set; }

        public Dictionary<uint, List<Infection>> Infections { get; } = new Dictionary<uint, List<Infection>>();

        internal void AddInfection(uint infectingCitizenId, uint infectedCitizenId, DateTime currentTime, InfectionType type, Vector3 position, ItemClass.Service buildingType)
        {
            if (!Infections.ContainsKey(infectingCitizenId))
            {
                Infections.Add(infectingCitizenId, new List<Infection>());
            }
            Infections[infectingCitizenId].Add(new Infection(infectedCitizenId, currentTime, type, position, buildingType));
        }

        internal void AddInfection(uint infectingCitizenId, uint infectedCitizenId, DateTime currentTime, InfectionType type, Vector3 position)
        {
            if (!Infections.ContainsKey(infectingCitizenId))
            {
                Infections.Add(infectingCitizenId, new List<Infection>());
            }
            Infections[infectingCitizenId].Add(new Infection(infectedCitizenId, currentTime, type, position));
        }

        internal void AddInfectiousCitizen(uint infectingCitizenId)
        {
            if (!Infections.ContainsKey(infectingCitizenId))
            {
                Infections.Add(infectingCitizenId, new List<Infection>());
            }
        }
    }

    public class Infection
    {
        public InfectionType type { get; }
        public DateTime time { get; }
        public uint infectedCitizenId { get; }
        public Vector3 position { get; }
        public ItemClass.Service buildingType { get; }

        public Infection(uint infectedCitizenId, DateTime time, InfectionType type, Vector3 position, ItemClass.Service buildingType) : this(infectedCitizenId, time, type, position)
        {
            this.buildingType = buildingType;

        }

        public Infection(uint infectedCitizenId, DateTime time, InfectionType type, Vector3 position)
        {
            this.infectedCitizenId = infectedCitizenId;
            this.time = time;
            this.type = type;
            this.position = position;
        }
    }

    public enum InfectionType
    {
        INDOOR, OUTDOOR, VEHICLE
    }
}
