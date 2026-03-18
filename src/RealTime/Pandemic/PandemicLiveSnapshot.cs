namespace RealTime.Pandemic
{
    using System.Collections.Generic;

    internal sealed class PandemicLiveSnapshot
    {
        public bool IsActive { get; set; }

        public bool IsInitialized { get; set; }

        public System.DateTime SimulationTime { get; set; }

        public int Healthy { get; set; }

        public int Sick { get; set; }

        public int Recovered { get; set; }

        public int Dead { get; set; }

        public int DeltaSick { get; set; }

        public int DeltaRecovered { get; set; }

        public int DeltaDead { get; set; }

        public int QuarantineCitizens { get; set; }

        public int PositiveTests { get; set; }

        public int TestedCitizens { get; set; }

        public int ContactsTrackedCitizens { get; set; }

        public int ContactsTrackedPairs { get; set; }

        public long ContactsRecordedTotal { get; set; }

        public int TransmissionsTotal { get; set; }

        public int TransmissionsIndoor { get; set; }

        public int TransmissionsOutdoor { get; set; }

        public int TransmissionsVehicle { get; set; }

        public int ObservationCount { get; set; }

        public IList<PandemicAgeGroupSnapshot> AgeGroups { get; } = new List<PandemicAgeGroupSnapshot>();

        public IList<PandemicDistrictSnapshot> Districts { get; } = new List<PandemicDistrictSnapshot>();
    }

    internal sealed class PandemicAgeGroupSnapshot
    {
        public string Label { get; set; }

        public int InfectedCount { get; set; }

        public float InfectedPercent { get; set; }
    }

    internal sealed class PandemicDistrictSnapshot
    {
        public int DistrictId { get; set; }

        public string DistrictName { get; set; }

        public int InfectedResidents { get; set; }

        public int ResidentCount { get; set; }

        public float InfectedPercent { get; set; }
    }
}
