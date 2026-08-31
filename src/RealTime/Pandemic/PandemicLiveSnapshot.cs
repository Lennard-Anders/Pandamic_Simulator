namespace RealTime.Pandemic
{
    using System.Collections.Generic;
    using UnityEngine;

    internal sealed class PandemicLiveSnapshot
    {
        public bool IsActive { get; set; }

        public bool IsInitialized { get; set; }

        public PandemicLifecycleState LifecycleState { get; set; }

        public bool HasStartedAtLeastOnce { get; set; }

        public bool CanStart { get; set; }

        public bool CanRestart { get; set; }

        public bool CanStop { get; set; }

        public bool WorldOverlaysEnabled { get; set; }

        public bool XRayEnabled { get; set; }

        public PandemicXRayMetric XRayMetric { get; set; }

        public PandemicXRayLocationMode XRayLocationMode { get; set; }

        public PandemicPublicTransportShutdownState PublicTransportState { get; set; }

        public int PublicTransportTrackedLines { get; set; }

        public int PublicTransportReturningVehicles { get; set; }

        public int PublicTransportClosedDepots { get; set; }

        public System.DateTime SimulationTime { get; set; }

        public int PandemicDay { get; set; }

        public int TrackedPopulation { get; set; }

        public int Healthy { get; set; }

        public int Exposed { get; set; }

        public int Sick { get; set; }

        public int LocationHome { get; set; }

        public int LocationWork { get; set; }

        public int LocationVisit { get; set; }

        public int LocationTransit { get; set; }

        public int LocationMoving { get; set; }

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

        public int HotspotBuildings { get; set; }

        public int HubBuildings { get; set; }

        public float HospitalUsagePercent { get; set; }

        public float HospitalUsageDeltaPercent { get; set; }

        public float AmbulanceUsagePercent { get; set; }

        public float AmbulanceUsageDeltaPercent { get; set; }

        public bool HasChartData { get; set; }

        public int SnapshotVersion { get; set; }

        public int ChartVersion { get; set; }

        public IList<PandemicAgeGroupSnapshot> AgeGroups { get; } = new List<PandemicAgeGroupSnapshot>();

        public IList<PandemicDistrictSnapshot> Districts { get; } = new List<PandemicDistrictSnapshot>();

        public IList<PandemicOriginSnapshot> Origins { get; } = new List<PandemicOriginSnapshot>();

        public IList<PandemicSuperspreaderCitizenSnapshot> TopSpreaders { get; } = new List<PandemicSuperspreaderCitizenSnapshot>();

        public IList<PandemicSuperspreaderLocationSnapshot> TopOriginLocations { get; } = new List<PandemicSuperspreaderLocationSnapshot>();

        public IList<PandemicLockdownFamilySnapshot> LockdownFamilies { get; } = new List<PandemicLockdownFamilySnapshot>();

        public IList<PandemicChartPointSnapshot> ChartPoints { get; } = new List<PandemicChartPointSnapshot>();

        public IList<PandemicPolicyMarkerSnapshot> PolicyMarkers { get; } = new List<PandemicPolicyMarkerSnapshot>();
    }

    internal sealed class PandemicAgeGroupSnapshot
    {
        public string Label { get; set; }

        public int InfectedCount { get; set; }

        public int PopulationCount { get; set; }

        public float ShareOfInfectionsPercent { get; set; }

        public float InfectionPrevalenceWithinAgeGroupPercent { get; set; }

        /// <summary>Legacy alias for <see cref="ShareOfInfectionsPercent"/>.</summary>
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

    internal sealed class PandemicOriginSnapshot
    {
        public string Label { get; set; }

        public int Count { get; set; }

        public float Percent { get; set; }
    }

    internal sealed class PandemicSuperspreaderCitizenSnapshot
    {
        public uint CitizenId { get; set; }

        public ushort CitizenInstanceId { get; set; }

        public string Label { get; set; }

        public int InfectionCount { get; set; }

        public bool IsSuperspreader { get; set; }

        public bool CanFocus { get; set; }

        public Vector3 FocusPosition { get; set; }
    }

    internal sealed class PandemicSuperspreaderLocationSnapshot
    {
        public string Label { get; set; }

        public int InfectionCount { get; set; }

        public bool IsSuperspreader { get; set; }

        public ushort BuildingId { get; set; }

        public bool CanFocus { get; set; }

        public Vector3 FocusPosition { get; set; }
    }

    internal sealed class PandemicLockdownFamilySnapshot
    {
        public PandemicLockdownFamily Family { get; set; }

        public string Label { get; set; }

        public bool IsClosed { get; set; }

        public bool ManualClosed { get; set; }

        public float AutoCloseThresholdPercent { get; set; }

        public float AutoReopenThresholdPercent { get; set; }

        public float MinimumClosureDurationDays { get; set; }

        public float CooldownDurationDays { get; set; }

        public string ThresholdMetric { get; set; }

        public float CurrentInfectedPercent { get; set; }
    }

    internal sealed class PandemicChartPointSnapshot
    {
        public System.DateTime SimulationTime { get; set; }

        public int InfectedCount { get; set; }
    }

    internal sealed class PandemicPolicyMarkerSnapshot
    {
        public System.DateTime SimulationTime { get; set; }

        public PandemicPolicyMarkerType Type { get; set; }

        public bool Enabled { get; set; }

        public string ShortLabel { get; set; }
    }
}
