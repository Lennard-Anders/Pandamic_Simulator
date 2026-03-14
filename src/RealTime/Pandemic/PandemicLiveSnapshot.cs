namespace RealTime.Pandemic
{
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
    }
}
