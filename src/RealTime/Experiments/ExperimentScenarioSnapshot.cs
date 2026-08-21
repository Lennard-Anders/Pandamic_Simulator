// <copyright file="ExperimentScenarioSnapshot.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.Experiments
{
    using System.Collections.Generic;
    using RealTime.Config;

    /// <summary>
    /// Versioned, presentation-free snapshot of settings that can affect an experiment.
    /// Deliberately excludes configuration persistence and notification preferences.
    /// </summary>
    public sealed class ExperimentScenarioSnapshot
    {
        public ExperimentScenarioSnapshot()
        {
            SchemaVersion = ExperimentSchema.CurrentVersion;
        }

        public int SchemaVersion { get; set; }

        public uint DayTimeSpeed { get; set; }

        public uint NightTimeSpeed { get; set; }

        public bool IsDynamicDayLengthEnabled { get; set; }

        public bool IsWeekendEnabled { get; set; }

        public VirtualCitizensLevel VirtualCitizens { get; set; }

        public bool UseSlowAging { get; set; }

        public bool StopConstructionAtNight { get; set; }

        public uint ConstructionSpeed { get; set; }

        public bool CanAbandonJourney { get; set; }

        public bool StaticBaselineEnabled { get; set; }

        public StaticBaselineMode StaticBaselineMode { get; set; }

        public int StaticBaselineResidentialDemand { get; set; }

        public int StaticBaselineCommercialDemand { get; set; }

        public int StaticBaselineIndustrialDemand { get; set; }

        public int StaticBaselineOfficeDemand { get; set; }

        public bool StaticBaselineFreezeConstructionAndUpgrades { get; set; }

        public bool StaticBaselineFreezeDemandProblemTimers { get; set; }

        public bool StaticBaselineDisableBirths { get; set; }

        public bool StaticBaselineDisableRealTimeEvents { get; set; }

        public bool StaticBaselineDisableTouristLeisure { get; set; }

        public bool StaticBaselineStableCityEnabled { get; set; }

        public bool StaticBaselineNeutralizeEconomyAndZoningProblems { get; set; }

        public bool StaticBaselineNeutralizeServiceAndLogisticsProblems { get; set; }

        public bool StaticBaselineNeutralizeUtilitiesAndConnectivityProblems { get; set; }

        public bool StaticBaselineNeutralizeSafetyAndEnvironmentProblems { get; set; }

        public bool StaticBaselineNeutralizeAreaAndDlcRestrictions { get; set; }

        public QuarantineBehavior QuarantineBehavior { get; set; }

        public bool OnlyTestedCitizensToQuarantine { get; set; }

        public LockdownBehavior LockdownBehavior { get; set; }

        public bool InitialLockdownEnabled { get; set; }

        public int TransmissionProbabilityReduction { get; set; }

        public int RatioIgnoreMasks { get; set; }

        public int RatioOtherProtectionMask { get; set; }

        public int RatioOwnProtectionMask { get; set; }

        public MaskBehavior MaskBehavior { get; set; }

        public float BuildingContactTracingProbability { get; set; }

        public float AppBasedContactTracingProbability { get; set; }

        public float RelativeTestCapacity { get; set; }

        public float PercentageOfTestsReservedForSickCitizens { get; set; }

        public uint MaximumTestDuration { get; set; }

        public uint MinimumTestDuration { get; set; }

        public uint DiseaseDuration { get; set; }

        public uint DetectionTime { get; set; }

        public uint StartSymptoms { get; set; }

        public uint EndSymptoms { get; set; }

        public uint StartInfection { get; set; }

        public uint EndInfection { get; set; }

        public float IndoorDiseaseTransmissionProbability { get; set; }

        public float OutdoorDiseaseTransmissionProbability { get; set; }

        public float DiseaseTransmissionRange { get; set; }

        public float DiseaseStartInfectionRatio { get; set; }

        public float DeathChild { get; set; }

        public float DeathTeen { get; set; }

        public float DeathYoung { get; set; }

        public float DeathAdult { get; set; }

        public float DeathSenior { get; set; }

        public float SymptomProbability { get; set; }

        public int HubHighlightThreshold { get; set; }

        public int SuperspreaderCitizenThreshold { get; set; }

        public int SuperspreaderLocationThreshold { get; set; }

        public bool CloseEducationDuringLockdown { get; set; }

        public float CloseEducationThresholdPercent { get; set; }

        public bool ClosePublicTransportDuringLockdown { get; set; }

        public float ClosePublicTransportThresholdPercent { get; set; }

        public bool CloseCommercialDuringLockdown { get; set; }

        public float CloseCommercialThresholdPercent { get; set; }

        public bool CloseLeisureTourismParksDuringLockdown { get; set; }

        public float CloseLeisureTourismParksThresholdPercent { get; set; }

        public bool CloseOfficeDuringLockdown { get; set; }

        public float CloseOfficeThresholdPercent { get; set; }

        public bool CloseIndustryDuringLockdown { get; set; }

        public float CloseIndustryThresholdPercent { get; set; }

        public bool CloseGovernmentOtherPublicDuringLockdown { get; set; }

        public float CloseGovernmentOtherPublicThresholdPercent { get; set; }

        public bool CloseEssentialServicesDuringLockdown { get; set; }

        public float CloseEssentialServicesThresholdPercent { get; set; }

        public uint SecondShiftQuota { get; set; }

        public uint NightShiftQuota { get; set; }

        public uint LunchQuota { get; set; }

        public uint LocalBuildingSearchQuota { get; set; }

        public uint ShoppingForFunQuota { get; set; }

        public uint OnTimeQuota { get; set; }

        public bool AreEventsEnabled { get; set; }

        public float EarliestHourEventStartWeekday { get; set; }

        public float LatestHourEventStartWeekday { get; set; }

        public float EarliestHourEventStartWeekend { get; set; }

        public float LatestHourEventStartWeekend { get; set; }

        public float WakeUpHour { get; set; }

        public float GoToSleepHour { get; set; }

        public float WorkBegin { get; set; }

        public float WorkEnd { get; set; }

        public bool IsLunchtimeEnabled { get; set; }

        public float LunchBegin { get; set; }

        public float LunchEnd { get; set; }

        public float MaxOvertime { get; set; }

        public float SchoolBegin { get; set; }

        public float SchoolEnd { get; set; }

        public uint MaxVacationLength { get; set; }

        /// <summary>Captures the scenario allowlist from a live configuration.</summary>
        public static ExperimentScenarioSnapshot Capture(RealTimeConfig configuration, bool initialLockdownEnabled)
        {
            return ExperimentConfigurationMapper.CaptureScenario(configuration, initialLockdownEnabled);
        }

        /// <summary>Creates a detached copy of this snapshot.</summary>
        public ExperimentScenarioSnapshot Clone()
        {
            return ExperimentConfigurationMapper.CloneScenario(this);
        }

        /// <summary>Returns a normalized detached copy using <see cref="RealTimeConfig.Validate"/>.</summary>
        public ExperimentScenarioSnapshot Normalize()
        {
            return ExperimentConfigurationMapper.NormalizeScenario(this);
        }

        /// <summary>Applies only the scenario allowlist to an existing configuration object.</summary>
        public void ApplyTo(RealTimeConfig configuration)
        {
            ExperimentConfigurationMapper.ApplyScenario(this, configuration);
        }

        /// <summary>Returns stable, property-level differences from another snapshot.</summary>
        public List<ExperimentSettingDifference> Diff(ExperimentScenarioSnapshot other)
        {
            return ExperimentConfigurationMapper.DiffScenario(this, other);
        }
    }
}
