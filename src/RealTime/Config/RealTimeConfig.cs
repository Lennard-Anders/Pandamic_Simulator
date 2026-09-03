// <copyright file="RealTimeConfig.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.Config
{
    using SkyTools.Configuration;
    using SkyTools.Tools;
    using SkyTools.UI;

    /// <summary>
    /// The mod's configuration.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1708:IdentifiersShouldDifferByMoreThanCase", Justification = "Property will be removed later")]
    public sealed class RealTimeConfig : IConfiguration
    {
        /// <summary>The storage ID for the configuration objects.</summary>
        public const string StorageId = "PandemicConfiguration";

        private const int LatestVersion = 13;

        /// <summary>Initializes a new instance of the <see cref="RealTimeConfig"/> class.</summary>
        public RealTimeConfig()
        {
            ResetToDefaults();
        }

        /// <summary>Initializes a new instance of the <see cref="RealTimeConfig"/> class.</summary>
        /// <param name="latestVersion">if set to <c>true</c>, the latest version of the configuration will be created.</param>
        public RealTimeConfig(bool latestVersion)
            : this()
        {
            if (latestVersion)
            {
                Version = LatestVersion;
            }
        }

        /// <summary>Gets or sets the version number of this configuration.</summary>
        public int Version { get; set; }

        /// <summary>
        /// Gets or sets the speed of the time flow on daytime. Valid values are 1..7.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1702:CompoundWordsShouldBeCasedCorrectly", MessageId = "DayTime", Justification = "Reviewed")]
        [ConfigItem("1General", "0Time", 2)]
        [ConfigItemSlider(1, 6, ValueType = SliderValueType.Default)]
        public uint DayTimeSpeed { get; set; }

        /// <summary>
        /// Gets or sets the speed of the time flow on night time. Valid values are 1..7.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1702:CompoundWordsShouldBeCasedCorrectly", MessageId = "NightTime", Justification = "Reviewed")]
        [ConfigItem("1General", "0Time", 3)]
        [ConfigItemSlider(1, 6, ValueType = SliderValueType.Default)]
        public uint NightTimeSpeed { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the dynamic day length is enabled.
        /// The dynamic day length depends on map's location and day of the year.
        /// </summary>
        [ConfigItem("1General", "0Time", 4)]
        [ConfigItemCheckBox]
        public bool IsDynamicDayLengthEnabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the weekends are enabled. Cims don't go to work on weekends.
        /// </summary>
        [ConfigItem("1General", "0Time", 5)]
        [ConfigItemCheckBox]
        public bool IsWeekendEnabled { get; set; }

        /// <summary>
        /// Gets or sets the virtual citizens mode.
        /// </summary>
        [ConfigItem("1General", "1Other", 0)]
        [ConfigItemComboBox]
        public VirtualCitizensLevel VirtualCitizens { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the citizens aging and birth rates must be slowed down.
        /// </summary>
        [ConfigItem("1General", "1Other", 1)]
        [ConfigItemCheckBox]
        public bool UseSlowAging { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the construction sites should pause at night time.
        /// </summary>
        [ConfigItem("1General", "1Other", 2)]
        [ConfigItemCheckBox]
        public bool StopConstructionAtNight { get; set; }

        /// <summary>
        /// Gets or sets the percentage value of the building construction speed. Valid values are 1..100.
        /// </summary>
        [ConfigItem("1General", "1Other", 3)]
        [ConfigItemSlider(1, 100)]
        public uint ConstructionSpeed { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the inactive buildings should switch off the lights at night time.
        /// </summary>
        [ConfigItem("1General", "1Other", 4)]
        [ConfigItemCheckBox]
        public bool SwitchOffLightsAtNight { get; set; }

        /// <summary>
        /// Gets or sets the maximum height of a residential, commercial, or office building that will switch the lights off
        /// at night. All buildings higher than this value will not switch the lights off.
        /// </summary>
        [ConfigItem("1General", "1Other", 4)]
        [ConfigItemSlider(0, 100, 5, ValueType = SliderValueType.Default)]
        public float SwitchOffLightsMaxHeight { get; set; }

        /// <summary>Gets or sets a value indicating whether a citizen can abandon a journey when being too long in
        /// a traffic congestion or waiting too long for public transport.</summary>
        [ConfigItem("1General", "1Other", 5)]
        [ConfigItemCheckBox]
        public bool CanAbandonJourney { get; set; }

        /// <summary>Gets or sets a value indicating whether the static baseline is enabled.</summary>
        [ConfigItem("1StaticBaseline", "0Mode", 0)]
        [ConfigItemCheckBox]
        public bool StaticBaselineEnabled { get; set; }

        /// <summary>Gets or sets the static baseline operating mode.</summary>
        [ConfigItem("1StaticBaseline", "0Mode", 1)]
        [ConfigItemComboBox]
        public StaticBaselineMode StaticBaselineMode { get; set; }

        /// <summary>Gets or sets the residential demand target for the static baseline.</summary>
        [ConfigItem("1StaticBaseline", "1DemandTargets", 0)]
        [ConfigItemSlider(0, 100, 1, ValueType = SliderValueType.Percentage)]
        public int StaticBaselineResidentialDemand { get; set; }

        /// <summary>Gets or sets the commercial demand target for the static baseline.</summary>
        [ConfigItem("1StaticBaseline", "1DemandTargets", 1)]
        [ConfigItemSlider(0, 100, 1, ValueType = SliderValueType.Percentage)]
        public int StaticBaselineCommercialDemand { get; set; }

        /// <summary>Gets or sets the industrial demand target for the static baseline.</summary>
        [ConfigItem("1StaticBaseline", "1DemandTargets", 2)]
        [ConfigItemSlider(0, 100, 1, ValueType = SliderValueType.Percentage)]
        public int StaticBaselineIndustrialDemand { get; set; }

        /// <summary>Gets or sets the office demand target for the static baseline.</summary>
        [ConfigItem("1StaticBaseline", "1DemandTargets", 3)]
        [ConfigItemSlider(0, 100, 1, ValueType = SliderValueType.Percentage)]
        public int StaticBaselineOfficeDemand { get; set; }

        /// <summary>Gets or sets a value indicating whether demand-zone construction and upgrades are frozen.</summary>
        [ConfigItem("2StableCityOptions", "2OptionalControls", 0)]
        [ConfigItemCheckBox]
        public bool StaticBaselineFreezeConstructionAndUpgrades { get; set; }

        /// <summary>Gets or sets a value indicating whether demand-related building problem timers are frozen.</summary>
        [ConfigItem("2StableCityOptions", "2OptionalControls", 1)]
        [ConfigItemCheckBox]
        public bool StaticBaselineFreezeDemandProblemTimers { get; set; }

        /// <summary>Gets or sets a value indicating whether births should be disabled.</summary>
        [ConfigItem("2StableCityOptions", "2OptionalControls", 2)]
        [ConfigItemCheckBox]
        public bool StaticBaselineDisableBirths { get; set; }

        /// <summary>Gets or sets a value indicating whether Real Time events should be disabled.</summary>
        [ConfigItem("2StableCityOptions", "2OptionalControls", 3)]
        [ConfigItemCheckBox]
        public bool StaticBaselineDisableRealTimeEvents { get; set; }

        /// <summary>Gets or sets a value indicating whether tourist leisure traffic should be disabled.</summary>
        [ConfigItem("2StableCityOptions", "2OptionalControls", 4)]
        [ConfigItemCheckBox]
        public bool StaticBaselineDisableTouristLeisure { get; set; }

        /// <summary>Gets or sets a value indicating whether the stable city sandbox is enabled.</summary>
        [ConfigItem("2StableCityOptions", "3StableCitySandbox", 0)]
        [ConfigItemCheckBox]
        public bool StaticBaselineStableCityEnabled { get; set; }

        /// <summary>Gets or sets a value indicating whether economy and zoning problems should be neutralized.</summary>
        [ConfigItem("2StableCityOptions", "3StableCitySandbox", 1)]
        [ConfigItemCheckBox]
        public bool StaticBaselineNeutralizeEconomyAndZoningProblems { get; set; }

        /// <summary>Gets or sets a value indicating whether service and logistics problems should be neutralized.</summary>
        [ConfigItem("2StableCityOptions", "3StableCitySandbox", 2)]
        [ConfigItemCheckBox]
        public bool StaticBaselineNeutralizeServiceAndLogisticsProblems { get; set; }

        /// <summary>Gets or sets a value indicating whether utility and connectivity problems should be neutralized.</summary>
        [ConfigItem("2StableCityOptions", "3StableCitySandbox", 3)]
        [ConfigItemCheckBox]
        public bool StaticBaselineNeutralizeUtilitiesAndConnectivityProblems { get; set; }

        /// <summary>Gets or sets a value indicating whether safety and environment problems should be neutralized.</summary>
        [ConfigItem("2StableCityOptions", "3StableCitySandbox", 4)]
        [ConfigItemCheckBox]
        public bool StaticBaselineNeutralizeSafetyAndEnvironmentProblems { get; set; }

        /// <summary>Gets or sets a value indicating whether area and DLC restrictions should be neutralized.</summary>
        [ConfigItem("2StableCityOptions", "3StableCitySandbox", 5)]
        [ConfigItemCheckBox]
        public bool StaticBaselineNeutralizeAreaAndDlcRestrictions { get; set; }

        /// <summary>Gets or sets a value indicating whether the current baseline should be stored as the default for new games.</summary>
        public bool StaticBaselineSaveAsDefault { get; set; }

        /// <summary>Gets or sets the quarantine behavior of citizens.</summary>
        [ConfigItem("Quarantine", "CitizenBehavior", 0)]
        [ConfigItemComboBox]
        public QuarantineBehavior QuarantineBehavior { get; set; }

        /// <summary>Gets or sets the quarantine behavior of citizens.</summary>
        [ConfigItem("Quarantine", "CitizenBehavior", 1)]
        [ConfigItemCheckBox]
        public bool OnlyTestedCitizensToQuarantine { get; set; }

        /// <summary>Gets or sets the lockdown behavior.</summary>
        [ConfigItem("Quarantine", "Lockdown", 0)]
        [ConfigItemComboBox]
        public LockdownBehavior LockdownBehavior { get; set; }

        /// <summary>Gets or sets the lockdown behavior.</summary>
        [ConfigItem("Pandemic", "Masks", 0)]
        [ConfigItemSlider(1, 20, 1, ValueType = SliderValueType.Default)]
        public int TransmissionProbabilityReduction { get; set; }

        /// <summary>Gets or sets the lockdown behavior.</summary>
        [ConfigItem("Pandemic", "Masks", 1)]
        [ConfigItemSlider(0, 100, 1, ValueType = SliderValueType.Percentage)]
        public int RatioIgnoreMasks { get; set; }

        /// <summary>Gets or sets the lockdown behavior.</summary>
        [ConfigItem("Pandemic", "Masks", 2)]
        [ConfigItemSlider(0, 100, 1, ValueType = SliderValueType.Percentage)]
        public int RatioOtherProtectionMask { get; set; }

        /// <summary>Gets or sets the lockdown behavior.</summary>
        [ConfigItem("Pandemic", "Masks", 3)]
        [ConfigItemSlider(0, 100, 1, ValueType = SliderValueType.Percentage)]
        public int RatioOwnProtectionMask { get; set; }

        /// <summary>Gets or sets the lockdown behavior.</summary>
        [ConfigItem("Pandemic", "Masks", 4)]
        [ConfigItemComboBox]
        public MaskBehavior MaskBehavior { get; set; }

        /*
        /// <summary>Gets or sets the lockdown behavior.</summary>
        [ConfigItem("Pandemic", "ContactTracing", 0)]
        [ConfigItemComboBox]
        public ContactTracingBehavior ContactTracingBehavior { get; set; }
        */

        /// <summary>Gets or sets the lockdown behavior.</summary>
        [ConfigItem("Pandemic", "ContactTracing", 0)]
        [ConfigItemSlider(0, 100, .1f, ValueType = SliderValueType.Default)]
        public float BuildingContactTracingProbability { get; set; }

        /// <summary>Gets or sets the lockdown behavior.</summary>
        [ConfigItem("Pandemic", "ContactTracing", 1)]
        [ConfigItemSlider(0, 100, .1f, ValueType = SliderValueType.Default)]
        public float AppBasedContactTracingProbability { get; set; }

        /// <summary>Gets or sets the lockdown behavior.</summary>
        [ConfigItem("Pandemic", "Testing", 0)]
        [ConfigItemSlider(0, 100, .1f, ValueType = SliderValueType.Default)]
        public float RelativeTestCapacity { get; set; }

        /// <summary>Gets or sets the lockdown behavior.</summary>
        [ConfigItem("Pandemic", "Testing", 1)]
        [ConfigItemSlider(0, 100, .1f, ValueType = SliderValueType.Default)]
        public float PercentageOfTestsReservedForSickCitizens { get; set; }

        /// <summary>Gets or sets the lockdown behavior.</summary>
        [ConfigItem("Pandemic", "Testing", 2)]
        [ConfigItemSlider(0, 28, 1, ValueType = SliderValueType.Default)]
        public uint MaximumTestDuration { get; set; }

        /// <summary>Gets or sets the lockdown behavior.</summary>
        [ConfigItem("Pandemic", "Testing", 3)]
        [ConfigItemSlider(0, 28, 1, ValueType = SliderValueType.Default)]
        public uint MinimumTestDuration { get; set; }

        /// <summary>Gets or sets the probability that a detectable infection produces a positive result.</summary>
        [ConfigItem("Pandemic", "Testing", 4)]
        [ConfigItemSlider(0, 100, .1f, ValueType = SliderValueType.Percentage)]
        public float TestSensitivityPercent { get; set; }

        /// <summary>Gets or sets the probability that an uninfected sample produces a negative result.</summary>
        [ConfigItem("Pandemic", "Testing", 5)]
        [ConfigItemSlider(0, 100, .1f, ValueType = SliderValueType.Percentage)]
        public float TestSpecificityPercent { get; set; }

        /// <summary>Gets or sets a value indicating whether every pending test triggers precautionary quarantine.</summary>
        [ConfigItem("Pandemic", "Testing", 6)]
        [ConfigItemCheckBox]
        public bool QuarantineWhileAwaitingTestResult { get; set; }

        /// <summary>Gets or sets the minimum interval before a citizen can be tested again.</summary>
        [ConfigItem("Pandemic", "Testing", 7)]
        [ConfigItemSlider(0, 28, 1, ValueType = SliderValueType.Default)]
        public uint RetestIntervalDays { get; set; }

        /// <summary>Gets or sets the fixed epidemiological simulation step in simulation minutes.</summary>
        [ConfigItem("Pandemic", "Testing", 8)]
        [ConfigItemSlider(1, 60, 1, ValueType = SliderValueType.Default)]
        public uint EpidemicStepMinutes { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether an unexpected resident or immigrant removal
        /// invalidates a scientific batch run. Expected tourist and commuter departures are never fatal.
        /// </summary>
        [ConfigItem("ScientificModel", "PopulationLifecycle", 0)]
        [ConfigItemCheckBox]
        public bool StrictPopulationIntegrity { get; set; }

        /// <summary>Gets or sets the per-step school contact cap used for deterministic sampling.</summary>
        [ConfigItem("Pandemic", "ContactModel", 0)]
        [ConfigItemSlider(1, 100, 1, ValueType = SliderValueType.Default)]
        public uint MaxContactsPerPersonPerStepSchool { get; set; }

        /// <summary>Gets or sets the per-step workplace contact cap used for deterministic sampling.</summary>
        [ConfigItem("Pandemic", "ContactModel", 1)]
        [ConfigItemSlider(1, 100, 1, ValueType = SliderValueType.Default)]
        public uint MaxContactsPerPersonPerStepWorkplace { get; set; }

        /// <summary>Gets or sets the per-step commercial contact cap used for deterministic sampling.</summary>
        [ConfigItem("Pandemic", "ContactModel", 2)]
        [ConfigItemSlider(1, 100, 1, ValueType = SliderValueType.Default)]
        public uint MaxContactsPerPersonPerStepCommercial { get; set; }

        /// <summary>Gets or sets the per-step healthcare contact cap used for deterministic sampling.</summary>
        [ConfigItem("Pandemic", "ContactModel", 3)]
        [ConfigItemSlider(1, 100, 1, ValueType = SliderValueType.Default)]
        public uint MaxContactsPerPersonPerStepHealthcare { get; set; }

        /// <summary>Gets or sets the per-step public-transport contact cap used for deterministic sampling.</summary>
        [ConfigItem("Pandemic", "ContactModel", 4)]
        [ConfigItemSlider(1, 100, 1, ValueType = SliderValueType.Default)]
        public uint MaxContactsPerPersonPerStepTransit { get; set; }

        /// <summary>Gets or sets the per-step residential shared-area contact cap.</summary>
        [ConfigItem("Pandemic", "ContactModel", 5)]
        [ConfigItemSlider(1, 100, 1, ValueType = SliderValueType.Default)]
        public uint MaxContactsPerPersonPerStepResidentialSharedArea { get; set; }

        /// <summary>Gets or sets the named legacy-calibration multiplier for residential shared areas.</summary>
        [ConfigItem("Pandemic", "ContactModel", 6)]
        [ConfigItemSlider(0f, 10f, 0.001f, ValueType = SliderValueType.Default)]
        public float ResidentialSharedAreaTransmissionMultiplier { get; set; }

        /// <summary>Gets or sets the disease duration.</summary>
        [ConfigItem("DiseaseProperties", 0)]
        [ConfigItemSlider(0, 28, 1, ValueType = SliderValueType.Default)]
        public uint DiseaseDuration { get; set; }

        /// <summary>Gets or sets the disease duration.</summary>
        [ConfigItem("DiseaseProperties", 0)]
        [ConfigItemSlider(0, 28, 1, ValueType = SliderValueType.Default)]
        public uint DetectionTime { get; set; }

        /// <summary>Gets or sets the disease duration.</summary>
        [ConfigItem("DiseaseProperties", 0)]
        [ConfigItemSlider(0, 28, 1, ValueType = SliderValueType.Default)]
        public uint StartSymptoms { get; set; }

        /// <summary>Gets or sets the disease duration.</summary>
        [ConfigItem("DiseaseProperties", 0)]
        [ConfigItemSlider(0, 28, 1, ValueType = SliderValueType.Default)]
        public uint EndSymptoms { get; set; }

        /// <summary>Gets or sets the disease duration.</summary>
        [ConfigItem("DiseaseProperties", 0)]
        [ConfigItemSlider(0, 28, 1, ValueType = SliderValueType.Default)]
        public uint StartInfection { get; set; }

        /// <summary>Gets or sets the disease duration.</summary>
        [ConfigItem("DiseaseProperties", 0)]
        [ConfigItemSlider(0, 28, 1, ValueType = SliderValueType.Default)]
        public uint EndInfection { get; set; }

        /// <summary>Gets or sets the indoor transmission probability.</summary>
        [ConfigItem("DiseaseProperties", 1)]
        [ConfigItemSlider(0f, 10f, 0.1f, ValueType = SliderValueType.Default)]
        public float IndoorDiseaseTransmissionProbability { get; set; }

        /// <summary>Gets or sets the outdoor transmission probability.</summary>
        [ConfigItem("DiseaseProperties", 2)]
        [ConfigItemSlider(0f, 10f, 0.1f, ValueType = SliderValueType.Default)]
        public float OutdoorDiseaseTransmissionProbability { get; set; }

        /// <summary>Gets or sets the transmission range.</summary>
        [ConfigItem("DiseaseProperties", 3)]
        [ConfigItemSlider(0, 5, 0.5f, ValueType = SliderValueType.Default)]
        public float DiseaseTransmissionRange { get; set; }

        /// <summary>Gets or sets the ratio of initial infections.</summary>
        [ConfigItem("DiseaseProperties", 4)]
        [ConfigItemSlider(0, 100, 0.1f, ValueType = SliderValueType.Default)]
        public float DiseaseStartInfectionRatio { get; set; }

        /// <summary>Gets or sets the death probability.</summary>
        [ConfigItem("Symptoms", "Death", 0)]
        [ConfigItemSlider(0, 100, 1, ValueType = SliderValueType.Percentage)]
        public float DeathChild { get; set; }

        /// <summary>Gets or sets the death probability.</summary>
        [ConfigItem("Symptoms", "Death", 1)]
        [ConfigItemSlider(0, 100, 1, ValueType = SliderValueType.Percentage)]
        public float DeathTeen { get; set; }

        /// <summary>Gets or sets the death probability.</summary>
        [ConfigItem("Symptoms", "Death", 2)]
        [ConfigItemSlider(0, 100, 1, ValueType = SliderValueType.Percentage)]
        public float DeathYoung { get; set; }

        /// <summary>Gets or sets the death probability.</summary>
        [ConfigItem("Symptoms", "Death", 3)]
        [ConfigItemSlider(0, 100, 1, ValueType = SliderValueType.Percentage)]
        public float DeathAdult { get; set; }

        /// <summary>Gets or sets the death probability.</summary>
        [ConfigItem("Symptoms", "Death", 4)]
        [ConfigItemSlider(0, 100, 1, ValueType = SliderValueType.Percentage)]
        public float DeathSenior { get; set; }

        /// <summary>Gets or sets the death probability.</summary>
        [ConfigItem("Symptoms", "OtherSymptoms", 4)]
        [ConfigItemSlider(0, 100, 1, ValueType = SliderValueType.Percentage)]
        public float SymptomProbability { get; set; }

        /// <summary>Gets or sets the distribution used for the minimum exposed duration.</summary>
        [ConfigItem("ScientificModel", "DiseaseTimeline", 0)]
        [ConfigItemComboBox]
        public PandemicDistributionType ExposedDurationDistributionType { get; set; }

        /// <summary>Gets or sets the arithmetic mean exposed duration in days.</summary>
        [ConfigItem("ScientificModel", "DiseaseTimeline", 1)]
        [ConfigItemSlider(0, 3650, 0.01f, ValueType = SliderValueType.Default)]
        public float ExposedDurationMeanDays { get; set; }

        /// <summary>Gets or sets the exposed-duration standard deviation in days.</summary>
        [ConfigItem("ScientificModel", "DiseaseTimeline", 2)]
        [ConfigItemSlider(0, 3650, 0.01f, ValueType = SliderValueType.Default)]
        public float ExposedDurationStandardDeviationDays { get; set; }

        /// <summary>Gets or sets the minimum exposed duration in days.</summary>
        [ConfigItem("ScientificModel", "DiseaseTimeline", 3)]
        [ConfigItemSlider(0, 3650, 0.01f, ValueType = SliderValueType.Default)]
        public float ExposedDurationMinimumDays { get; set; }

        /// <summary>Gets or sets the maximum exposed duration in days.</summary>
        [ConfigItem("ScientificModel", "DiseaseTimeline", 4)]
        [ConfigItemSlider(0, 3650, 0.01f, ValueType = SliderValueType.Default)]
        public float ExposedDurationMaximumDays { get; set; }

        /// <summary>Gets or sets the deterministic exposed duration in days.</summary>
        [ConfigItem("ScientificModel", "DiseaseTimeline", 5)]
        [ConfigItemSlider(0, 3650, 0.01f, ValueType = SliderValueType.Default)]
        public float ExposedDurationFixedDays { get; set; }

        /// <summary>Gets or sets the distribution for infectious-start offset.</summary>
        [ConfigItem("ScientificModel", "DiseaseTimeline", 6)]
        [ConfigItemComboBox]
        public PandemicDistributionType InfectiousStartDistributionType { get; set; }

        /// <summary>Gets or sets infectious-start arithmetic mean days.</summary>
        [ConfigItem("ScientificModel", "DiseaseTimeline", 7)]
        [ConfigItemSlider(0, 3650, 0.01f, ValueType = SliderValueType.Default)]
        public float InfectiousStartMeanDays { get; set; }

        /// <summary>Gets or sets infectious-start standard deviation days.</summary>
        [ConfigItem("ScientificModel", "DiseaseTimeline", 8)]
        [ConfigItemSlider(0, 3650, 0.01f, ValueType = SliderValueType.Default)]
        public float InfectiousStartStandardDeviationDays { get; set; }

        /// <summary>Gets or sets minimum infectious-start offset days.</summary>
        [ConfigItem("ScientificModel", "DiseaseTimeline", 9)]
        [ConfigItemSlider(0, 3650, 0.01f, ValueType = SliderValueType.Default)]
        public float InfectiousStartMinimumDays { get; set; }

        /// <summary>Gets or sets maximum infectious-start offset days.</summary>
        [ConfigItem("ScientificModel", "DiseaseTimeline", 10)]
        [ConfigItemSlider(0, 3650, 0.01f, ValueType = SliderValueType.Default)]
        public float InfectiousStartMaximumDays { get; set; }

        /// <summary>Gets or sets deterministic infectious-start offset days.</summary>
        [ConfigItem("ScientificModel", "DiseaseTimeline", 11)]
        [ConfigItemSlider(0, 3650, 0.01f, ValueType = SliderValueType.Default)]
        public float InfectiousStartFixedDays { get; set; }

        /// <summary>Gets or sets the distribution for infectious-end offset.</summary>
        [ConfigItem("ScientificModel", "DiseaseTimeline", 12)]
        [ConfigItemComboBox]
        public PandemicDistributionType InfectiousEndDistributionType { get; set; }

        /// <summary>Gets or sets infectious-end arithmetic mean days.</summary>
        [ConfigItem("ScientificModel", "DiseaseTimeline", 13)]
        [ConfigItemSlider(0, 3650, 0.01f, ValueType = SliderValueType.Default)]
        public float InfectiousEndMeanDays { get; set; }

        /// <summary>Gets or sets infectious-end standard deviation days.</summary>
        [ConfigItem("ScientificModel", "DiseaseTimeline", 14)]
        [ConfigItemSlider(0, 3650, 0.01f, ValueType = SliderValueType.Default)]
        public float InfectiousEndStandardDeviationDays { get; set; }

        /// <summary>Gets or sets minimum infectious-end offset days.</summary>
        [ConfigItem("ScientificModel", "DiseaseTimeline", 15)]
        [ConfigItemSlider(0, 3650, 0.01f, ValueType = SliderValueType.Default)]
        public float InfectiousEndMinimumDays { get; set; }

        /// <summary>Gets or sets maximum infectious-end offset days.</summary>
        [ConfigItem("ScientificModel", "DiseaseTimeline", 16)]
        [ConfigItemSlider(0, 3650, 0.01f, ValueType = SliderValueType.Default)]
        public float InfectiousEndMaximumDays { get; set; }

        /// <summary>Gets or sets deterministic infectious-end offset days.</summary>
        [ConfigItem("ScientificModel", "DiseaseTimeline", 17)]
        [ConfigItemSlider(0, 3650, 0.01f, ValueType = SliderValueType.Default)]
        public float InfectiousEndFixedDays { get; set; }

        /// <summary>Gets or sets the distribution for symptom-start offset.</summary>
        [ConfigItem("ScientificModel", "DiseaseTimeline", 18)]
        [ConfigItemComboBox]
        public PandemicDistributionType SymptomStartDistributionType { get; set; }

        /// <summary>Gets or sets symptom-start arithmetic mean days.</summary>
        [ConfigItem("ScientificModel", "DiseaseTimeline", 19)]
        [ConfigItemSlider(0, 3650, 0.01f, ValueType = SliderValueType.Default)]
        public float SymptomStartMeanDays { get; set; }

        /// <summary>Gets or sets symptom-start standard deviation days.</summary>
        [ConfigItem("ScientificModel", "DiseaseTimeline", 20)]
        [ConfigItemSlider(0, 3650, 0.01f, ValueType = SliderValueType.Default)]
        public float SymptomStartStandardDeviationDays { get; set; }

        /// <summary>Gets or sets minimum symptom-start offset days.</summary>
        [ConfigItem("ScientificModel", "DiseaseTimeline", 21)]
        [ConfigItemSlider(0, 3650, 0.01f, ValueType = SliderValueType.Default)]
        public float SymptomStartMinimumDays { get; set; }

        /// <summary>Gets or sets maximum symptom-start offset days.</summary>
        [ConfigItem("ScientificModel", "DiseaseTimeline", 22)]
        [ConfigItemSlider(0, 3650, 0.01f, ValueType = SliderValueType.Default)]
        public float SymptomStartMaximumDays { get; set; }

        /// <summary>Gets or sets deterministic symptom-start offset days.</summary>
        [ConfigItem("ScientificModel", "DiseaseTimeline", 23)]
        [ConfigItemSlider(0, 3650, 0.01f, ValueType = SliderValueType.Default)]
        public float SymptomStartFixedDays { get; set; }

        /// <summary>Gets or sets the distribution for symptom-end offset.</summary>
        [ConfigItem("ScientificModel", "DiseaseTimeline", 24)]
        [ConfigItemComboBox]
        public PandemicDistributionType SymptomEndDistributionType { get; set; }

        /// <summary>Gets or sets symptom-end arithmetic mean days.</summary>
        [ConfigItem("ScientificModel", "DiseaseTimeline", 25)]
        [ConfigItemSlider(0, 3650, 0.01f, ValueType = SliderValueType.Default)]
        public float SymptomEndMeanDays { get; set; }

        /// <summary>Gets or sets symptom-end standard deviation days.</summary>
        [ConfigItem("ScientificModel", "DiseaseTimeline", 26)]
        [ConfigItemSlider(0, 3650, 0.01f, ValueType = SliderValueType.Default)]
        public float SymptomEndStandardDeviationDays { get; set; }

        /// <summary>Gets or sets minimum symptom-end offset days.</summary>
        [ConfigItem("ScientificModel", "DiseaseTimeline", 27)]
        [ConfigItemSlider(0, 3650, 0.01f, ValueType = SliderValueType.Default)]
        public float SymptomEndMinimumDays { get; set; }

        /// <summary>Gets or sets maximum symptom-end offset days.</summary>
        [ConfigItem("ScientificModel", "DiseaseTimeline", 28)]
        [ConfigItemSlider(0, 3650, 0.01f, ValueType = SliderValueType.Default)]
        public float SymptomEndMaximumDays { get; set; }

        /// <summary>Gets or sets deterministic symptom-end offset days.</summary>
        [ConfigItem("ScientificModel", "DiseaseTimeline", 29)]
        [ConfigItemSlider(0, 3650, 0.01f, ValueType = SliderValueType.Default)]
        public float SymptomEndFixedDays { get; set; }

        /// <summary>Gets or sets the distribution for recovery offset.</summary>
        [ConfigItem("ScientificModel", "DiseaseTimeline", 30)]
        [ConfigItemComboBox]
        public PandemicDistributionType RecoveryDistributionType { get; set; }

        /// <summary>Gets or sets recovery arithmetic mean days.</summary>
        [ConfigItem("ScientificModel", "DiseaseTimeline", 31)]
        [ConfigItemSlider(0, 3650, 0.01f, ValueType = SliderValueType.Default)]
        public float RecoveryMeanDays { get; set; }

        /// <summary>Gets or sets recovery standard deviation days.</summary>
        [ConfigItem("ScientificModel", "DiseaseTimeline", 32)]
        [ConfigItemSlider(0, 3650, 0.01f, ValueType = SliderValueType.Default)]
        public float RecoveryStandardDeviationDays { get; set; }

        /// <summary>Gets or sets minimum recovery offset days.</summary>
        [ConfigItem("ScientificModel", "DiseaseTimeline", 33)]
        [ConfigItemSlider(0, 3650, 0.01f, ValueType = SliderValueType.Default)]
        public float RecoveryMinimumDays { get; set; }

        /// <summary>Gets or sets maximum recovery offset days.</summary>
        [ConfigItem("ScientificModel", "DiseaseTimeline", 34)]
        [ConfigItemSlider(0, 3650, 0.01f, ValueType = SliderValueType.Default)]
        public float RecoveryMaximumDays { get; set; }

        /// <summary>Gets or sets deterministic recovery offset days.</summary>
        [ConfigItem("ScientificModel", "DiseaseTimeline", 35)]
        [ConfigItemSlider(0, 3650, 0.01f, ValueType = SliderValueType.Default)]
        public float RecoveryFixedDays { get; set; }

        /// <summary>Gets or sets the infectiousness profile type.</summary>
        [ConfigItem("ScientificModel", "Infectiousness", 0)]
        [ConfigItemComboBox]
        public PandemicInfectiousnessProfileType InfectiousnessProfileType { get; set; }

        /// <summary>Gets or sets the multiplier at infectious-window start.</summary>
        [ConfigItem("ScientificModel", "Infectiousness", 1)]
        [ConfigItemSlider(0, 100, 0.01f, ValueType = SliderValueType.Default)]
        public float InfectiousnessProfileStartMultiplier { get; set; }

        /// <summary>Gets or sets the relative peak position in the infectious interval.</summary>
        [ConfigItem("ScientificModel", "Infectiousness", 2)]
        [ConfigItemSlider(0, 1, 0.01f, ValueType = SliderValueType.Default)]
        public float InfectiousnessProfilePeakTimeFraction { get; set; }

        /// <summary>Gets or sets the multiplier at the profile peak.</summary>
        [ConfigItem("ScientificModel", "Infectiousness", 3)]
        [ConfigItemSlider(0, 100, 0.01f, ValueType = SliderValueType.Default)]
        public float InfectiousnessProfilePeakMultiplier { get; set; }

        /// <summary>Gets or sets the multiplier at infectious-window end.</summary>
        [ConfigItem("ScientificModel", "Infectiousness", 4)]
        [ConfigItemSlider(0, 100, 0.01f, ValueType = SliderValueType.Default)]
        public float InfectiousnessProfileEndMultiplier { get; set; }

        /// <summary>Gets or sets the initial-case population sampling strategy.</summary>
        [ConfigItem("ScientificModel", "InitialCases", 0)]
        [ConfigItemComboBox]
        public PandemicInitialSeedSamplingStrategy InitialSeedSamplingStrategy { get; set; }

        /// <summary>Gets or sets whether initial infection age is fixed or distributed.</summary>
        [ConfigItem("ScientificModel", "InitialCases", 1)]
        [ConfigItemComboBox]
        public PandemicInitialInfectionAgeMode InitialInfectionAgeMode { get; set; }

        /// <summary>Gets or sets the initial-infection-age distribution type.</summary>
        [ConfigItem("ScientificModel", "InitialCases", 2)]
        [ConfigItemComboBox]
        public PandemicDistributionType InitialInfectionAgeDistributionType { get; set; }

        /// <summary>Gets or sets arithmetic mean initial infection age in days.</summary>
        [ConfigItem("ScientificModel", "InitialCases", 3)]
        [ConfigItemSlider(0, 3650, 0.01f, ValueType = SliderValueType.Default)]
        public float InitialInfectionAgeMeanDays { get; set; }

        /// <summary>Gets or sets initial infection age standard deviation in days.</summary>
        [ConfigItem("ScientificModel", "InitialCases", 4)]
        [ConfigItemSlider(0, 3650, 0.01f, ValueType = SliderValueType.Default)]
        public float InitialInfectionAgeStandardDeviationDays { get; set; }

        /// <summary>Gets or sets minimum initial infection age in days.</summary>
        [ConfigItem("ScientificModel", "InitialCases", 5)]
        [ConfigItemSlider(0, 3650, 0.01f, ValueType = SliderValueType.Default)]
        public float InitialInfectionAgeMinimumDays { get; set; }

        /// <summary>Gets or sets maximum initial infection age in days.</summary>
        [ConfigItem("ScientificModel", "InitialCases", 6)]
        [ConfigItemSlider(0, 3650, 0.01f, ValueType = SliderValueType.Default)]
        public float InitialInfectionAgeMaximumDays { get; set; }

        /// <summary>Gets or sets deterministic initial infection age in days.</summary>
        [ConfigItem("ScientificModel", "InitialCases", 7)]
        [ConfigItemSlider(0, 3650, 0.01f, ValueType = SliderValueType.Default)]
        public float InitialInfectionAgeFixedDays { get; set; }

        /// <summary>Gets or sets the relative mortality multiplier for asymptomatic courses.</summary>
        [ConfigItem("ScientificModel", "Mortality", 0)]
        [ConfigItemSlider(0, 100, 0.01f, ValueType = SliderValueType.Default)]
        public float AsymptomaticMortalityMultiplier { get; set; }

        /// <summary>Gets or sets the hospital-usage warning threshold.</summary>
        [ConfigItem("ScientificModel", "Mortality", 1)]
        [ConfigItemSlider(0, 100, 0.1f, ValueType = SliderValueType.Percentage)]
        public float HealthcareWarningThresholdPercent { get; set; }

        /// <summary>Gets or sets the hospital-usage critical threshold.</summary>
        [ConfigItem("ScientificModel", "Mortality", 2)]
        [ConfigItemSlider(0, 100, 0.1f, ValueType = SliderValueType.Percentage)]
        public float HealthcareCriticalThresholdPercent { get; set; }

        /// <summary>Gets or sets the mortality-hazard multiplier at warning saturation.</summary>
        [ConfigItem("ScientificModel", "Mortality", 3)]
        [ConfigItemSlider(0, 100, 0.01f, ValueType = SliderValueType.Default)]
        public float HealthcareWarningMortalityMultiplier { get; set; }

        /// <summary>Gets or sets the mortality-hazard multiplier at critical saturation.</summary>
        [ConfigItem("ScientificModel", "Mortality", 4)]
        [ConfigItemSlider(0, 100, 0.01f, ValueType = SliderValueType.Default)]
        public float HealthcareCriticalMortalityMultiplier { get; set; }

        /// <summary>Gets or sets the infected count threshold for hub highlighting.</summary>
        [ConfigItem("PandemicMonitor", "Overlays", 0)]
        [ConfigItemSlider(2, 30, 1, ValueType = SliderValueType.Default)]
        public int HubHighlightThreshold { get; set; }

        /// <summary>Gets or sets the minimum infection count for a citizen superspreader label.</summary>
        [ConfigItem("PandemicMonitor", "Superspreaders", 0)]
        [ConfigItemSlider(2, 20, 1, ValueType = SliderValueType.Default)]
        public int SuperspreaderCitizenThreshold { get; set; }

        /// <summary>Gets or sets the minimum infection count for a location superspreader label.</summary>
        [ConfigItem("PandemicMonitor", "Superspreaders", 1)]
        [ConfigItemSlider(2, 50, 1, ValueType = SliderValueType.Default)]
        public int SuperspreaderLocationThreshold { get; set; }

        /// <summary>Gets or sets a value indicating whether education buildings close during lockdown.</summary>
        [ConfigItem("PandemicLockdown", "Families", 0)]
        [ConfigItemCheckBox]
        public bool CloseEducationDuringLockdown { get; set; }

        /// <summary>Gets or sets the education auto-close threshold in percent.</summary>
        [ConfigItem("PandemicLockdown", "Families", 1)]
        [ConfigItemSlider(0, 100, 1, ValueType = SliderValueType.Percentage)]
        public float CloseEducationThresholdPercent { get; set; }

        /// <summary>Gets or sets a value indicating whether public transport buildings close during lockdown.</summary>
        [ConfigItem("PandemicLockdown", "Families", 2)]
        [ConfigItemCheckBox]
        public bool ClosePublicTransportDuringLockdown { get; set; }

        /// <summary>Gets or sets the public transport auto-close threshold in percent.</summary>
        [ConfigItem("PandemicLockdown", "Families", 3)]
        [ConfigItemSlider(0, 100, 1, ValueType = SliderValueType.Percentage)]
        public float ClosePublicTransportThresholdPercent { get; set; }

        /// <summary>Gets or sets a value indicating whether commercial buildings close during lockdown.</summary>
        [ConfigItem("PandemicLockdown", "Families", 4)]
        [ConfigItemCheckBox]
        public bool CloseCommercialDuringLockdown { get; set; }

        /// <summary>Gets or sets the commercial auto-close threshold in percent.</summary>
        [ConfigItem("PandemicLockdown", "Families", 5)]
        [ConfigItemSlider(0, 100, 1, ValueType = SliderValueType.Percentage)]
        public float CloseCommercialThresholdPercent { get; set; }

        /// <summary>Gets or sets a value indicating whether leisure, tourism and park buildings close during lockdown.</summary>
        [ConfigItem("PandemicLockdown", "Families", 6)]
        [ConfigItemCheckBox]
        public bool CloseLeisureTourismParksDuringLockdown { get; set; }

        /// <summary>Gets or sets the leisure, tourism and park auto-close threshold in percent.</summary>
        [ConfigItem("PandemicLockdown", "Families", 7)]
        [ConfigItemSlider(0, 100, 1, ValueType = SliderValueType.Percentage)]
        public float CloseLeisureTourismParksThresholdPercent { get; set; }

        /// <summary>Gets or sets a value indicating whether office buildings close during lockdown.</summary>
        [ConfigItem("PandemicLockdown", "Families", 8)]
        [ConfigItemCheckBox]
        public bool CloseOfficeDuringLockdown { get; set; }

        /// <summary>Gets or sets the office auto-close threshold in percent.</summary>
        [ConfigItem("PandemicLockdown", "Families", 9)]
        [ConfigItemSlider(0, 100, 1, ValueType = SliderValueType.Percentage)]
        public float CloseOfficeThresholdPercent { get; set; }

        /// <summary>Gets or sets a value indicating whether industry buildings close during lockdown.</summary>
        [ConfigItem("PandemicLockdown", "Families", 10)]
        [ConfigItemCheckBox]
        public bool CloseIndustryDuringLockdown { get; set; }

        /// <summary>Gets or sets the industry auto-close threshold in percent.</summary>
        [ConfigItem("PandemicLockdown", "Families", 11)]
        [ConfigItemSlider(0, 100, 1, ValueType = SliderValueType.Percentage)]
        public float CloseIndustryThresholdPercent { get; set; }

        /// <summary>Gets or sets a value indicating whether government and other public buildings close during lockdown.</summary>
        [ConfigItem("PandemicLockdown", "Families", 12)]
        [ConfigItemCheckBox]
        public bool CloseGovernmentOtherPublicDuringLockdown { get; set; }

        /// <summary>Gets or sets the government and other public auto-close threshold in percent.</summary>
        [ConfigItem("PandemicLockdown", "Families", 13)]
        [ConfigItemSlider(0, 100, 1, ValueType = SliderValueType.Percentage)]
        public float CloseGovernmentOtherPublicThresholdPercent { get; set; }

        /// <summary>Gets or sets a value indicating whether essential-service buildings close during lockdown.</summary>
        [ConfigItem("PandemicLockdown", "Families", 14)]
        [ConfigItemCheckBox]
        public bool CloseEssentialServicesDuringLockdown { get; set; }

        /// <summary>Gets or sets the essential-services auto-close threshold in percent.</summary>
        [ConfigItem("PandemicLockdown", "Families", 15)]
        [ConfigItemSlider(0, 100, 1, ValueType = SliderValueType.Percentage)]
        public float CloseEssentialServicesThresholdPercent { get; set; }

        /// <summary>Gets or sets the education auto-reopen threshold in percent.</summary>
        [ConfigItem("PandemicLockdown", "Families", 16)]
        [ConfigItemSlider(0, 100, 1, ValueType = SliderValueType.Percentage)]
        public float ReopenEducationThresholdPercent { get; set; }

        /// <summary>Gets or sets the minimum education closure duration in simulation days.</summary>
        [ConfigItem("PandemicLockdown", "Families", 17)]
        [ConfigItemSlider(0, 3650, 0.25f, ValueType = SliderValueType.Default)]
        public float MinimumEducationClosureDurationDays { get; set; }

        /// <summary>Gets or sets the education lockdown transition cooldown in simulation days.</summary>
        [ConfigItem("PandemicLockdown", "Families", 18)]
        [ConfigItemSlider(0, 3650, 0.25f, ValueType = SliderValueType.Default)]
        public float EducationLockdownCooldownDurationDays { get; set; }

        /// <summary>Gets or sets the public transport auto-reopen threshold in percent.</summary>
        [ConfigItem("PandemicLockdown", "Families", 19)]
        [ConfigItemSlider(0, 100, 1, ValueType = SliderValueType.Percentage)]
        public float ReopenPublicTransportThresholdPercent { get; set; }

        /// <summary>Gets or sets the minimum public transport closure duration in simulation days.</summary>
        [ConfigItem("PandemicLockdown", "Families", 20)]
        [ConfigItemSlider(0, 3650, 0.25f, ValueType = SliderValueType.Default)]
        public float MinimumPublicTransportClosureDurationDays { get; set; }

        /// <summary>Gets or sets the public transport lockdown transition cooldown in simulation days.</summary>
        [ConfigItem("PandemicLockdown", "Families", 21)]
        [ConfigItemSlider(0, 3650, 0.25f, ValueType = SliderValueType.Default)]
        public float PublicTransportLockdownCooldownDurationDays { get; set; }

        /// <summary>Gets or sets the commercial auto-reopen threshold in percent.</summary>
        [ConfigItem("PandemicLockdown", "Families", 22)]
        [ConfigItemSlider(0, 100, 1, ValueType = SliderValueType.Percentage)]
        public float ReopenCommercialThresholdPercent { get; set; }

        /// <summary>Gets or sets the minimum commercial closure duration in simulation days.</summary>
        [ConfigItem("PandemicLockdown", "Families", 23)]
        [ConfigItemSlider(0, 3650, 0.25f, ValueType = SliderValueType.Default)]
        public float MinimumCommercialClosureDurationDays { get; set; }

        /// <summary>Gets or sets the commercial lockdown transition cooldown in simulation days.</summary>
        [ConfigItem("PandemicLockdown", "Families", 24)]
        [ConfigItemSlider(0, 3650, 0.25f, ValueType = SliderValueType.Default)]
        public float CommercialLockdownCooldownDurationDays { get; set; }

        /// <summary>Gets or sets the leisure/tourism/parks auto-reopen threshold in percent.</summary>
        [ConfigItem("PandemicLockdown", "Families", 25)]
        [ConfigItemSlider(0, 100, 1, ValueType = SliderValueType.Percentage)]
        public float ReopenLeisureTourismParksThresholdPercent { get; set; }

        /// <summary>Gets or sets the minimum leisure/tourism/parks closure duration in simulation days.</summary>
        [ConfigItem("PandemicLockdown", "Families", 26)]
        [ConfigItemSlider(0, 3650, 0.25f, ValueType = SliderValueType.Default)]
        public float MinimumLeisureTourismParksClosureDurationDays { get; set; }

        /// <summary>Gets or sets the leisure/tourism/parks lockdown transition cooldown in simulation days.</summary>
        [ConfigItem("PandemicLockdown", "Families", 27)]
        [ConfigItemSlider(0, 3650, 0.25f, ValueType = SliderValueType.Default)]
        public float LeisureTourismParksLockdownCooldownDurationDays { get; set; }

        /// <summary>Gets or sets the office auto-reopen threshold in percent.</summary>
        [ConfigItem("PandemicLockdown", "Families", 28)]
        [ConfigItemSlider(0, 100, 1, ValueType = SliderValueType.Percentage)]
        public float ReopenOfficeThresholdPercent { get; set; }

        /// <summary>Gets or sets the minimum office closure duration in simulation days.</summary>
        [ConfigItem("PandemicLockdown", "Families", 29)]
        [ConfigItemSlider(0, 3650, 0.25f, ValueType = SliderValueType.Default)]
        public float MinimumOfficeClosureDurationDays { get; set; }

        /// <summary>Gets or sets the office lockdown transition cooldown in simulation days.</summary>
        [ConfigItem("PandemicLockdown", "Families", 30)]
        [ConfigItemSlider(0, 3650, 0.25f, ValueType = SliderValueType.Default)]
        public float OfficeLockdownCooldownDurationDays { get; set; }

        /// <summary>Gets or sets the industry auto-reopen threshold in percent.</summary>
        [ConfigItem("PandemicLockdown", "Families", 31)]
        [ConfigItemSlider(0, 100, 1, ValueType = SliderValueType.Percentage)]
        public float ReopenIndustryThresholdPercent { get; set; }

        /// <summary>Gets or sets the minimum industry closure duration in simulation days.</summary>
        [ConfigItem("PandemicLockdown", "Families", 32)]
        [ConfigItemSlider(0, 3650, 0.25f, ValueType = SliderValueType.Default)]
        public float MinimumIndustryClosureDurationDays { get; set; }

        /// <summary>Gets or sets the industry lockdown transition cooldown in simulation days.</summary>
        [ConfigItem("PandemicLockdown", "Families", 33)]
        [ConfigItemSlider(0, 3650, 0.25f, ValueType = SliderValueType.Default)]
        public float IndustryLockdownCooldownDurationDays { get; set; }

        /// <summary>Gets or sets the government/other-public auto-reopen threshold in percent.</summary>
        [ConfigItem("PandemicLockdown", "Families", 34)]
        [ConfigItemSlider(0, 100, 1, ValueType = SliderValueType.Percentage)]
        public float ReopenGovernmentOtherPublicThresholdPercent { get; set; }

        /// <summary>Gets or sets the minimum government/other-public closure duration in simulation days.</summary>
        [ConfigItem("PandemicLockdown", "Families", 35)]
        [ConfigItemSlider(0, 3650, 0.25f, ValueType = SliderValueType.Default)]
        public float MinimumGovernmentOtherPublicClosureDurationDays { get; set; }

        /// <summary>Gets or sets the government/other-public lockdown transition cooldown in simulation days.</summary>
        [ConfigItem("PandemicLockdown", "Families", 36)]
        [ConfigItemSlider(0, 3650, 0.25f, ValueType = SliderValueType.Default)]
        public float GovernmentOtherPublicLockdownCooldownDurationDays { get; set; }

        /// <summary>Gets or sets the essential-services auto-reopen threshold in percent.</summary>
        [ConfigItem("PandemicLockdown", "Families", 37)]
        [ConfigItemSlider(0, 100, 1, ValueType = SliderValueType.Percentage)]
        public float ReopenEssentialServicesThresholdPercent { get; set; }

        /// <summary>Gets or sets the minimum essential-services closure duration in simulation days.</summary>
        [ConfigItem("PandemicLockdown", "Families", 38)]
        [ConfigItemSlider(0, 3650, 0.25f, ValueType = SliderValueType.Default)]
        public float MinimumEssentialServicesClosureDurationDays { get; set; }

        /// <summary>Gets or sets the essential-services lockdown transition cooldown in simulation days.</summary>
        [ConfigItem("PandemicLockdown", "Families", 39)]
        [ConfigItemSlider(0, 3650, 0.25f, ValueType = SliderValueType.Default)]
        public float EssentialServicesLockdownCooldownDurationDays { get; set; }

        /// <summary>
        /// Gets or sets a value that determines the percentage of the Cims that will work second shift.
        /// Valid values are 1..8.
        /// </summary>
        [ConfigItem("2Quotas", 0)]
        [ConfigItemSlider(1, 25)]
        public uint SecondShiftQuota { get; set; }

        /// <summary>
        /// Gets or sets a value that determines the percentage of the Cims that will work night shift.
        /// Valid values are 1..8.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1702:CompoundWordsShouldBeCasedCorrectly", MessageId = "NightShift", Justification = "Reviewed")]
        [ConfigItem("2Quotas", 1)]
        [ConfigItemSlider(1, 25)]
        public uint NightShiftQuota { get; set; }

        /// <summary>
        /// Gets or sets the percentage of the Cims that will go out for lunch.
        /// Valid values are 0..100.
        /// </summary>
        [ConfigItem("2Quotas", 2)]
        [ConfigItemSlider(0, 100)]
        public uint LunchQuota { get; set; }

        /// <summary>
        /// Gets or sets the percentage of the population that will search locally for buildings.
        /// Valid values are 0..100.
        /// </summary>
        [ConfigItem("2Quotas", 3)]
        [ConfigItemSlider(0, 100)]
        public uint LocalBuildingSearchQuota { get; set; }

        /// <summary>
        /// Gets or sets the percentage of the Cims that will go shopping just for fun without needing to buy something.
        /// Valid values are 0..100.
        /// </summary>
        [ConfigItem("2Quotas", 4)]
        [ConfigItemSlider(0, 50)]
        public uint ShoppingForFunQuota { get; set; }

        /// <summary>
        /// Gets or sets the percentage of the Cims that will go to and leave their work or school
        /// on time (no overtime!).
        /// Valid values are 0..100.
        /// </summary>
        [ConfigItem("2Quotas", 5)]
        [ConfigItemSlider(0, 100)]
        public uint OnTimeQuota { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the custom events are enabled.
        /// </summary>
        [ConfigItem("3Events", 0)]
        [ConfigItemCheckBox]
        public bool AreEventsEnabled { get; set; }

        /// <summary>
        /// Gets or sets the daytime hour when the earliest event on a week day can start.
        /// </summary>
        [ConfigItem("3Events", 1)]
        [ConfigItemSlider(0, 23.5f, 0.5f, ValueType = SliderValueType.Time)]
        public float EarliestHourEventStartWeekday { get; set; }

        /// <summary>
        /// Gets or sets the daytime hour when the latest event on a week day can start.
        /// </summary>
        [ConfigItem("3Events", 2)]
        [ConfigItemSlider(0, 23.5f, 0.5f, ValueType = SliderValueType.Time)]
        public float LatestHourEventStartWeekday { get; set; }

        /// <summary>
        /// Gets or sets the daytime hour when the earliest event on a Weekend day can start.
        /// </summary>
        [ConfigItem("3Events", 3)]
        [ConfigItemSlider(0, 23.5f, 0.5f, ValueType = SliderValueType.Time)]
        public float EarliestHourEventStartWeekend { get; set; }

        /// <summary>
        /// Gets or sets the daytime hour when the latest event on a Weekend day can start.
        /// </summary>
        [ConfigItem("3Events", 4)]
        [ConfigItemSlider(0, 23.5f, 0.5f, ValueType = SliderValueType.Time)]
        public float LatestHourEventStartWeekend { get; set; }

        /// <summary>
        /// Gets or sets the daytime hour when the city wakes up.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1702:CompoundWordsShouldBeCasedCorrectly", MessageId = "WakeUp", Justification = "Reviewed")]
        [ConfigItem("4Time", 0)]
        [ConfigItemSlider(4f, 8f, 0.25f, ValueType = SliderValueType.Time)]
        public float WakeUpHour { get; set; }

        /// <summary>
        /// Gets or sets the daytime hour when the city goes to sleep.
        /// </summary>
        [ConfigItem("4Time", 1)]
        [ConfigItemSlider(20f, 23.75f, 0.25f, ValueType = SliderValueType.Time)]
        public float GoToSleepHour { get; set; }

        /// <summary>
        /// Gets or sets the work start daytime hour. The adult Cims must be at work.
        /// </summary>
        [ConfigItem("4Time", 2)]
        [ConfigItemSlider(4, 11, 0.25f, ValueType = SliderValueType.Time)]
        public float WorkBegin { get; set; }

        /// <summary>
        /// Gets or sets the daytime hour when the adult Cims return from work.
        /// </summary>
        [ConfigItem("4Time", 3)]
        [ConfigItemSlider(12, 20, 0.25f, ValueType = SliderValueType.Time)]
        public float WorkEnd { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether Cims should go out at lunch for food.
        /// </summary>
        [ConfigItem("4Time", 4)]
        [ConfigItemCheckBox]
        public bool IsLunchtimeEnabled { get; set; }

        /// <summary>
        /// Gets or sets the daytime hour when the Cims go out for lunch.
        /// </summary>
        [ConfigItem("4Time", 5)]
        [ConfigItemSlider(11, 13, 0.25f, ValueType = SliderValueType.Time)]
        public float LunchBegin { get; set; }

        /// <summary>
        /// Gets or sets the daytime hour when the Cims return from lunch back to work.
        /// </summary>
        [ConfigItem("4Time", 6)]
        [ConfigItemSlider(13, 15, 0.25f, ValueType = SliderValueType.Time)]
        public float LunchEnd { get; set; }

        /// <summary>
        /// Gets or sets the maximum overtime for the Cims. They come to work earlier or stay at work longer for at most this
        /// amount of hours. This applies only for those Cims that are not on time, see <see cref="OnTimeQuota"/>.
        /// The young Cims (school and university) don't do overtime.
        /// </summary>
        [ConfigItem("4Time", 7)]
        [ConfigItemSlider(0, 4, 0.25f, ValueType = SliderValueType.Duration)]
        public float MaxOvertime { get; set; }

        /// <summary>
        /// Gets or sets the school start daytime hour. The young Cims must be at school or university.
        /// </summary>
        [ConfigItem("4Time", 8)]
        [ConfigItemSlider(4, 10, 0.25f, ValueType = SliderValueType.Time)]
        public float SchoolBegin { get; set; }

        /// <summary>
        /// Gets or sets the daytime hour when the young Cims return from school or university.
        /// </summary>
        [ConfigItem("4Time", 9)]
        [ConfigItemSlider(11, 16, 0.25f, ValueType = SliderValueType.Time)]
        public float SchoolEnd { get; set; }

        /// <summary>
        /// Gets or sets the maximum vacation length in days.
        /// </summary>
        [ConfigItem("4Time", 10)]
        [ConfigItemSlider(0, 7, ValueType = SliderValueType.Default)]
        public uint MaxVacationLength { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the mod should show the incompatibility notifications.
        /// </summary>
        public bool ShowIncompatibilityNotifications { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the mod should use the English-US time and date formats, if the English language is selected.
        /// </summary>
        public bool UseEnglishUSFormats { get; set; }

        /// <summary>Checks the version of the deserialized object and migrates it to the latest version when necessary.</summary>
        public void MigrateWhenNecessary()
        {
            if (Version == 0)
            {
                SecondShiftQuota = (uint)(SecondShiftQuota * 3.125f);
                NightShiftQuota = (uint)(NightShiftQuota * 3.125f);
            }

            if (Version < 5)
            {
                StaticBaselineEnabled = false;
                StaticBaselineMode = StaticBaselineMode.Stabilization;
                StaticBaselineResidentialDemand = 70;
                StaticBaselineCommercialDemand = 70;
                StaticBaselineIndustrialDemand = 70;
                StaticBaselineOfficeDemand = 70;
                StaticBaselineSaveAsDefault = false;
            }

            if (Version < 6)
            {
                bool preserveLegacyStabilization = StaticBaselineEnabled && StaticBaselineMode == StaticBaselineMode.Stabilization;
                StaticBaselineFreezeConstructionAndUpgrades = preserveLegacyStabilization;
                StaticBaselineFreezeDemandProblemTimers = preserveLegacyStabilization;
                StaticBaselineDisableBirths = preserveLegacyStabilization;
                StaticBaselineDisableRealTimeEvents = false;
                StaticBaselineDisableTouristLeisure = false;
            }

            if (Version < 7)
            {
                StaticBaselineStableCityEnabled = false;
                StaticBaselineNeutralizeEconomyAndZoningProblems = true;
                StaticBaselineNeutralizeServiceAndLogisticsProblems = true;
                StaticBaselineNeutralizeUtilitiesAndConnectivityProblems = true;
                StaticBaselineNeutralizeSafetyAndEnvironmentProblems = true;
                StaticBaselineNeutralizeAreaAndDlcRestrictions = true;
            }

            if (Version < 8)
            {
                // Version 8 makes the previously hidden perfect-test assumptions explicit and reproducible.
                TestSensitivityPercent = 100f;
                TestSpecificityPercent = 100f;
                QuarantineWhileAwaitingTestResult = true;
                RetestIntervalDays = 7;
            }

            if (Version < 9)
            {
                EpidemicStepMinutes = 5;
            }

            if (Version < 10)
            {
                // Explicit, uncalibrated operational caps replace large-building all-to-all mixing.
                MaxContactsPerPersonPerStepSchool = 10;
                MaxContactsPerPersonPerStepWorkplace = 10;
                MaxContactsPerPersonPerStepCommercial = 10;
                MaxContactsPerPersonPerStepHealthcare = 10;
                MaxContactsPerPersonPerStepTransit = 10;
                MaxContactsPerPersonPerStepResidentialSharedArea = 10;
                ResidentialSharedAreaTransmissionMultiplier = 1f / 96f;
            }

            if (Version < 11)
            {
                // Preserve the legacy threshold response while making every family policy explicit.
                // Zero duration/cooldown values are deliberate compatibility defaults, not missing values.
                ReopenEducationThresholdPercent = CloseEducationThresholdPercent;
                ReopenPublicTransportThresholdPercent = ClosePublicTransportThresholdPercent;
                ReopenCommercialThresholdPercent = CloseCommercialThresholdPercent;
                ReopenLeisureTourismParksThresholdPercent = CloseLeisureTourismParksThresholdPercent;
                ReopenOfficeThresholdPercent = CloseOfficeThresholdPercent;
                ReopenIndustryThresholdPercent = CloseIndustryThresholdPercent;
                ReopenGovernmentOtherPublicThresholdPercent = CloseGovernmentOtherPublicThresholdPercent;
                ReopenEssentialServicesThresholdPercent = CloseEssentialServicesThresholdPercent;
                MinimumEducationClosureDurationDays = 0f;
                MinimumPublicTransportClosureDurationDays = 0f;
                MinimumCommercialClosureDurationDays = 0f;
                MinimumLeisureTourismParksClosureDurationDays = 0f;
                MinimumOfficeClosureDurationDays = 0f;
                MinimumIndustryClosureDurationDays = 0f;
                MinimumGovernmentOtherPublicClosureDurationDays = 0f;
                MinimumEssentialServicesClosureDurationDays = 0f;
                EducationLockdownCooldownDurationDays = 0f;
                PublicTransportLockdownCooldownDurationDays = 0f;
                CommercialLockdownCooldownDurationDays = 0f;
                LeisureTourismParksLockdownCooldownDurationDays = 0f;
                OfficeLockdownCooldownDurationDays = 0f;
                IndustryLockdownCooldownDurationDays = 0f;
                GovernmentOtherPublicLockdownCooldownDurationDays = 0f;
                EssentialServicesLockdownCooldownDurationDays = 0f;
            }

            if (Version < 12)
            {
                // Version 12 makes every previously fixed disease timeline, seed age, and
                // mortality-saturation constant explicit while preserving legacy behavior.
                SetScientificDefaultsFromLegacy();
            }

            if (Version < 13)
            {
                // Population turnover was previously treated as strict in every scientific batch.
                // Keep normal city turnover usable unless strict invalidation is explicitly selected.
                StrictPopulationIntegrity = false;
            }

            Version = LatestVersion;
        }

        /// <summary>Validates this instance and corrects possible invalid property values.</summary>
        public void Validate()
        {
            // Repair core Real Time realism features that should be active
            // These are the fundamental features that provide Real Time functionality
            IsDynamicDayLengthEnabled = true;
            UseSlowAging = true;
            IsWeekendEnabled = true;
            IsLunchtimeEnabled = true;
            StopConstructionAtNight = true;
            SwitchOffLightsAtNight = true;
            CanAbandonJourney = true;
            AreEventsEnabled = true;

            // Ensure Static Baseline features don't disable Real Time functionality
            StaticBaselineDisableRealTimeEvents = false;
            StaticBaselineDisableTouristLeisure = false;

            // Validate numeric ranges
            WakeUpHour = FastMath.Clamp(WakeUpHour, 4f, 8f);
            GoToSleepHour = FastMath.Clamp(GoToSleepHour, 20f, 23.75f);

            DayTimeSpeed = FastMath.Clamp(DayTimeSpeed, 1u, 6u);
            NightTimeSpeed = FastMath.Clamp(NightTimeSpeed, 1u, 6u);

            // Don't force VirtualCitizens to None to avoid performance issues with large cities
            // The Real Time AI patches remain active regardless
            VirtualCitizens = (VirtualCitizensLevel)FastMath.Clamp((int)VirtualCitizens, (int)VirtualCitizensLevel.None, (int)VirtualCitizensLevel.Vanilla);
            ConstructionSpeed = FastMath.Clamp(ConstructionSpeed, 1u, 100u);

            SwitchOffLightsMaxHeight = FastMath.Clamp(SwitchOffLightsMaxHeight, 0f, 100f);
            StaticBaselineResidentialDemand = FastMath.Clamp(StaticBaselineResidentialDemand, 0, 100);
            StaticBaselineCommercialDemand = FastMath.Clamp(StaticBaselineCommercialDemand, 0, 100);
            StaticBaselineIndustrialDemand = FastMath.Clamp(StaticBaselineIndustrialDemand, 0, 100);
            StaticBaselineOfficeDemand = FastMath.Clamp(StaticBaselineOfficeDemand, 0, 100);
            StaticBaselineMode = (Config.StaticBaselineMode)FastMath.Clamp((int)StaticBaselineMode, (int)Config.StaticBaselineMode.Growth, (int)Config.StaticBaselineMode.Stabilization);

            SecondShiftQuota = FastMath.Clamp(SecondShiftQuota, 1u, 25u);
            NightShiftQuota = FastMath.Clamp(NightShiftQuota, 1u, 25u);
            LunchQuota = FastMath.Clamp(LunchQuota, 0u, 100u);
            LocalBuildingSearchQuota = FastMath.Clamp(LocalBuildingSearchQuota, 0u, 100u);
            ShoppingForFunQuota = FastMath.Clamp(ShoppingForFunQuota, 0u, 50u);
            OnTimeQuota = FastMath.Clamp(OnTimeQuota, 0u, 100u);

            EarliestHourEventStartWeekday = FastMath.Clamp(EarliestHourEventStartWeekday, 0f, 23.5f);
            LatestHourEventStartWeekday = FastMath.Clamp(LatestHourEventStartWeekday, 0f, 23.5f);
            if (LatestHourEventStartWeekday < EarliestHourEventStartWeekday)
            {
                LatestHourEventStartWeekday = EarliestHourEventStartWeekday;
            }

            EarliestHourEventStartWeekend = FastMath.Clamp(EarliestHourEventStartWeekend, 0f, 23.5f);
            LatestHourEventStartWeekend = FastMath.Clamp(LatestHourEventStartWeekend, 0f, 23.5f);
            if (LatestHourEventStartWeekend < EarliestHourEventStartWeekend)
            {
                LatestHourEventStartWeekend = EarliestHourEventStartWeekend;
            }

            WorkBegin = FastMath.Clamp(WorkBegin, 4f, 11f);
            WorkEnd = FastMath.Clamp(WorkEnd, 12f, 20f);
            LunchBegin = FastMath.Clamp(LunchBegin, 11f, 13f);
            LunchEnd = FastMath.Clamp(LunchEnd, 13f, 15f);
            SchoolBegin = FastMath.Clamp(SchoolBegin, 4f, 10f);
            SchoolEnd = FastMath.Clamp(SchoolEnd, 11f, 16f);
            MaxOvertime = FastMath.Clamp(MaxOvertime, 0f, 4f);
            MaxVacationLength = FastMath.Clamp(MaxVacationLength, 0u, 7u);

            if (StartInfection > DiseaseDuration)
            {
                StartInfection = DiseaseDuration;
            }
            if (EndInfection > DiseaseDuration)
            {
                EndInfection = DiseaseDuration;
            }
            if (StartInfection > EndInfection)
            {
                StartInfection = EndInfection;
            }
            if (StartSymptoms > DiseaseDuration)
            {
                StartSymptoms = DiseaseDuration;
            }
            if (EndSymptoms > DiseaseDuration)
            {
                EndSymptoms = DiseaseDuration;
            }
            if (StartSymptoms > EndSymptoms)
            {
                StartSymptoms = EndSymptoms;
            }

            DiseaseTransmissionRange = FastMath.Clamp(DiseaseTransmissionRange, 0f, 5f);
            DiseaseStartInfectionRatio = FastMath.Clamp(DiseaseStartInfectionRatio, 0f, 100f);
            IndoorDiseaseTransmissionProbability = FastMath.Clamp(IndoorDiseaseTransmissionProbability, 0f, 10f);
            OutdoorDiseaseTransmissionProbability = FastMath.Clamp(OutdoorDiseaseTransmissionProbability, 0f, 10f);
            SymptomProbability = FastMath.Clamp(SymptomProbability, 0f, 100f);
            TestSensitivityPercent = FastMath.Clamp(TestSensitivityPercent, 0f, 100f);
            TestSpecificityPercent = FastMath.Clamp(TestSpecificityPercent, 0f, 100f);
            RetestIntervalDays = FastMath.Clamp(RetestIntervalDays, 0u, 28u);
            EpidemicStepMinutes = FastMath.Clamp(EpidemicStepMinutes, 1u, 60u);
            MaxContactsPerPersonPerStepSchool = FastMath.Clamp(MaxContactsPerPersonPerStepSchool, 1u, 100u);
            MaxContactsPerPersonPerStepWorkplace = FastMath.Clamp(MaxContactsPerPersonPerStepWorkplace, 1u, 100u);
            MaxContactsPerPersonPerStepCommercial = FastMath.Clamp(MaxContactsPerPersonPerStepCommercial, 1u, 100u);
            MaxContactsPerPersonPerStepHealthcare = FastMath.Clamp(MaxContactsPerPersonPerStepHealthcare, 1u, 100u);
            MaxContactsPerPersonPerStepTransit = FastMath.Clamp(MaxContactsPerPersonPerStepTransit, 1u, 100u);
            MaxContactsPerPersonPerStepResidentialSharedArea = FastMath.Clamp(MaxContactsPerPersonPerStepResidentialSharedArea, 1u, 100u);
            if (float.IsNaN(ResidentialSharedAreaTransmissionMultiplier)
                || float.IsInfinity(ResidentialSharedAreaTransmissionMultiplier))
            {
                ResidentialSharedAreaTransmissionMultiplier = 1f / 96f;
            }

            ResidentialSharedAreaTransmissionMultiplier = FastMath.Clamp(ResidentialSharedAreaTransmissionMultiplier, 0f, 10f);
            HubHighlightThreshold = FastMath.Clamp(HubHighlightThreshold, 2, 30);
            SuperspreaderCitizenThreshold = FastMath.Clamp(SuperspreaderCitizenThreshold, 2, 20);
            SuperspreaderLocationThreshold = FastMath.Clamp(SuperspreaderLocationThreshold, 2, 50);
            CloseEducationThresholdPercent = FastMath.Clamp(CloseEducationThresholdPercent, 0f, 100f);
            ClosePublicTransportThresholdPercent = FastMath.Clamp(ClosePublicTransportThresholdPercent, 0f, 100f);
            CloseCommercialThresholdPercent = FastMath.Clamp(CloseCommercialThresholdPercent, 0f, 100f);
            CloseLeisureTourismParksThresholdPercent = FastMath.Clamp(CloseLeisureTourismParksThresholdPercent, 0f, 100f);
            CloseOfficeThresholdPercent = FastMath.Clamp(CloseOfficeThresholdPercent, 0f, 100f);
            CloseIndustryThresholdPercent = FastMath.Clamp(CloseIndustryThresholdPercent, 0f, 100f);
            CloseGovernmentOtherPublicThresholdPercent = FastMath.Clamp(CloseGovernmentOtherPublicThresholdPercent, 0f, 100f);
            CloseEssentialServicesThresholdPercent = FastMath.Clamp(CloseEssentialServicesThresholdPercent, 0f, 100f);
            ReopenEducationThresholdPercent = ClampLockdownReopenThreshold(ReopenEducationThresholdPercent, CloseEducationThresholdPercent);
            ReopenPublicTransportThresholdPercent = ClampLockdownReopenThreshold(ReopenPublicTransportThresholdPercent, ClosePublicTransportThresholdPercent);
            ReopenCommercialThresholdPercent = ClampLockdownReopenThreshold(ReopenCommercialThresholdPercent, CloseCommercialThresholdPercent);
            ReopenLeisureTourismParksThresholdPercent = ClampLockdownReopenThreshold(ReopenLeisureTourismParksThresholdPercent, CloseLeisureTourismParksThresholdPercent);
            ReopenOfficeThresholdPercent = ClampLockdownReopenThreshold(ReopenOfficeThresholdPercent, CloseOfficeThresholdPercent);
            ReopenIndustryThresholdPercent = ClampLockdownReopenThreshold(ReopenIndustryThresholdPercent, CloseIndustryThresholdPercent);
            ReopenGovernmentOtherPublicThresholdPercent = ClampLockdownReopenThreshold(ReopenGovernmentOtherPublicThresholdPercent, CloseGovernmentOtherPublicThresholdPercent);
            ReopenEssentialServicesThresholdPercent = ClampLockdownReopenThreshold(ReopenEssentialServicesThresholdPercent, CloseEssentialServicesThresholdPercent);
            MinimumEducationClosureDurationDays = ClampLockdownDuration(MinimumEducationClosureDurationDays);
            MinimumPublicTransportClosureDurationDays = ClampLockdownDuration(MinimumPublicTransportClosureDurationDays);
            MinimumCommercialClosureDurationDays = ClampLockdownDuration(MinimumCommercialClosureDurationDays);
            MinimumLeisureTourismParksClosureDurationDays = ClampLockdownDuration(MinimumLeisureTourismParksClosureDurationDays);
            MinimumOfficeClosureDurationDays = ClampLockdownDuration(MinimumOfficeClosureDurationDays);
            MinimumIndustryClosureDurationDays = ClampLockdownDuration(MinimumIndustryClosureDurationDays);
            MinimumGovernmentOtherPublicClosureDurationDays = ClampLockdownDuration(MinimumGovernmentOtherPublicClosureDurationDays);
            MinimumEssentialServicesClosureDurationDays = ClampLockdownDuration(MinimumEssentialServicesClosureDurationDays);
            EducationLockdownCooldownDurationDays = ClampLockdownDuration(EducationLockdownCooldownDurationDays);
            PublicTransportLockdownCooldownDurationDays = ClampLockdownDuration(PublicTransportLockdownCooldownDurationDays);
            CommercialLockdownCooldownDurationDays = ClampLockdownDuration(CommercialLockdownCooldownDurationDays);
            LeisureTourismParksLockdownCooldownDurationDays = ClampLockdownDuration(LeisureTourismParksLockdownCooldownDurationDays);
            OfficeLockdownCooldownDurationDays = ClampLockdownDuration(OfficeLockdownCooldownDurationDays);
            IndustryLockdownCooldownDurationDays = ClampLockdownDuration(IndustryLockdownCooldownDurationDays);
            GovernmentOtherPublicLockdownCooldownDurationDays = ClampLockdownDuration(GovernmentOtherPublicLockdownCooldownDurationDays);
            EssentialServicesLockdownCooldownDurationDays = ClampLockdownDuration(EssentialServicesLockdownCooldownDurationDays);
            ExposedDurationDistributionType = ClampDistributionType(ExposedDurationDistributionType);
            InfectiousStartDistributionType = ClampDistributionType(InfectiousStartDistributionType);
            InfectiousEndDistributionType = ClampDistributionType(InfectiousEndDistributionType);
            SymptomStartDistributionType = ClampDistributionType(SymptomStartDistributionType);
            SymptomEndDistributionType = ClampDistributionType(SymptomEndDistributionType);
            RecoveryDistributionType = ClampDistributionType(RecoveryDistributionType);
            InitialInfectionAgeDistributionType = ClampDistributionType(InitialInfectionAgeDistributionType);
            InitialSeedSamplingStrategy = (PandemicInitialSeedSamplingStrategy)FastMath.Clamp(
                (int)InitialSeedSamplingStrategy,
                (int)PandemicInitialSeedSamplingStrategy.UniformPopulation,
                (int)PandemicInitialSeedSamplingStrategy.DistrictStratified);
            InitialInfectionAgeMode = (PandemicInitialInfectionAgeMode)FastMath.Clamp(
                (int)InitialInfectionAgeMode,
                (int)PandemicInitialInfectionAgeMode.FixedInitialInfectionAge,
                (int)PandemicInitialInfectionAgeMode.DistributedInitialInfectionAge);
            InfectiousnessProfileType = (PandemicInfectiousnessProfileType)FastMath.Clamp(
                (int)InfectiousnessProfileType,
                (int)PandemicInfectiousnessProfileType.Flat,
                (int)PandemicInfectiousnessProfileType.PiecewiseLinear);
            ClampScientificNumbers();
        }

        /// <summary>Resets all values to their defaults.</summary>
        public void ResetToDefaults()
        {
            WakeUpHour = 6f;
            GoToSleepHour = 22f;

            IsDynamicDayLengthEnabled = true;
            DayTimeSpeed = 4;
            NightTimeSpeed = 5;

            VirtualCitizens = VirtualCitizensLevel.Vanilla;
            UseSlowAging = true;
            IsWeekendEnabled = true;
            IsLunchtimeEnabled = true;

            StopConstructionAtNight = true;
            ConstructionSpeed = 50;
            SwitchOffLightsAtNight = true;
            SwitchOffLightsMaxHeight = 40f;
            CanAbandonJourney = true;
            StaticBaselineEnabled = false;
            StaticBaselineMode = StaticBaselineMode.Stabilization;
            StaticBaselineResidentialDemand = 70;
            StaticBaselineCommercialDemand = 70;
            StaticBaselineIndustrialDemand = 70;
            StaticBaselineOfficeDemand = 70;
            StaticBaselineFreezeConstructionAndUpgrades = false;
            StaticBaselineFreezeDemandProblemTimers = false;
            StaticBaselineDisableBirths = false;
            StaticBaselineDisableRealTimeEvents = false;
            StaticBaselineDisableTouristLeisure = false;
            StaticBaselineStableCityEnabled = false;
            StaticBaselineNeutralizeEconomyAndZoningProblems = true;
            StaticBaselineNeutralizeServiceAndLogisticsProblems = true;
            StaticBaselineNeutralizeUtilitiesAndConnectivityProblems = true;
            StaticBaselineNeutralizeSafetyAndEnvironmentProblems = true;
            StaticBaselineNeutralizeAreaAndDlcRestrictions = true;
            StaticBaselineSaveAsDefault = false;

            SecondShiftQuota = 13;
            NightShiftQuota = 6;

            LunchQuota = 80;
            LocalBuildingSearchQuota = 60;
            ShoppingForFunQuota = 30;
            OnTimeQuota = 80;

            AreEventsEnabled = true;
            EarliestHourEventStartWeekday = 16f;
            LatestHourEventStartWeekday = 20f;
            EarliestHourEventStartWeekend = 8f;
            LatestHourEventStartWeekend = 22f;

            WorkBegin = 9f;
            WorkEnd = 18f;
            LunchBegin = 12f;
            LunchEnd = 13f;
            MaxOvertime = 2f;
            SchoolBegin = 8f;
            SchoolEnd = 14f;
            MaxVacationLength = 3u;

            QuarantineBehavior = QuarantineBehavior.None;
            OnlyTestedCitizensToQuarantine = false;
            LockdownBehavior = LockdownBehavior.None;

            TransmissionProbabilityReduction = 2;
            // Integer percentages nearest to the legacy 30:50:50 weighted assignment
            // (23.08%, 38.46%, 38.46%), using a stable largest-remainder tie break.
            RatioIgnoreMasks = 23;
            RatioOtherProtectionMask = 39;
            RatioOwnProtectionMask = 38;
            MaskBehavior = MaskBehavior.None;
            BuildingContactTracingProbability = 30f;
            AppBasedContactTracingProbability = 20f;
            RelativeTestCapacity = 40f;
            PercentageOfTestsReservedForSickCitizens = 50f;
            MinimumTestDuration = 1;
            MaximumTestDuration = 3;
            TestSensitivityPercent = 100f;
            TestSpecificityPercent = 100f;
            QuarantineWhileAwaitingTestResult = true;
            RetestIntervalDays = 7;
            EpidemicStepMinutes = 5;
            StrictPopulationIntegrity = false;
            MaxContactsPerPersonPerStepSchool = 10;
            MaxContactsPerPersonPerStepWorkplace = 10;
            MaxContactsPerPersonPerStepCommercial = 10;
            MaxContactsPerPersonPerStepHealthcare = 10;
            MaxContactsPerPersonPerStepTransit = 10;
            MaxContactsPerPersonPerStepResidentialSharedArea = 10;
            ResidentialSharedAreaTransmissionMultiplier = 1f / 96f;

            DiseaseDuration = 14;
            DetectionTime = 2;
            StartSymptoms = 3;
            EndSymptoms = 14;
            StartInfection = 1;
            EndInfection = 10;
            IndoorDiseaseTransmissionProbability = 1.5f;
            OutdoorDiseaseTransmissionProbability = 0.3f;
            DiseaseTransmissionRange = 1.5f;
            DiseaseStartInfectionRatio = 33;

            DeathChild = 0.1f;
            DeathTeen = 0.1f;
            DeathYoung = 0.2f;
            DeathAdult = 0.5f;
            DeathSenior = 2.0f;
            SymptomProbability = 60f;
            HubHighlightThreshold = 6;
            SuperspreaderCitizenThreshold = 5;
            SuperspreaderLocationThreshold = 10;
            CloseEducationDuringLockdown = true;
            CloseEducationThresholdPercent = 0f;
            ClosePublicTransportDuringLockdown = false;
            ClosePublicTransportThresholdPercent = 35f;
            CloseCommercialDuringLockdown = true;
            CloseCommercialThresholdPercent = 0f;
            CloseLeisureTourismParksDuringLockdown = true;
            CloseLeisureTourismParksThresholdPercent = 0f;
            CloseOfficeDuringLockdown = false;
            CloseOfficeThresholdPercent = 25f;
            CloseIndustryDuringLockdown = false;
            CloseIndustryThresholdPercent = 30f;
            CloseGovernmentOtherPublicDuringLockdown = false;
            CloseGovernmentOtherPublicThresholdPercent = 40f;
            CloseEssentialServicesDuringLockdown = false;
            CloseEssentialServicesThresholdPercent = 100f;
            ReopenEducationThresholdPercent = CloseEducationThresholdPercent;
            ReopenPublicTransportThresholdPercent = ClosePublicTransportThresholdPercent;
            ReopenCommercialThresholdPercent = CloseCommercialThresholdPercent;
            ReopenLeisureTourismParksThresholdPercent = CloseLeisureTourismParksThresholdPercent;
            ReopenOfficeThresholdPercent = CloseOfficeThresholdPercent;
            ReopenIndustryThresholdPercent = CloseIndustryThresholdPercent;
            ReopenGovernmentOtherPublicThresholdPercent = CloseGovernmentOtherPublicThresholdPercent;
            ReopenEssentialServicesThresholdPercent = CloseEssentialServicesThresholdPercent;
            MinimumEducationClosureDurationDays = 0f;
            MinimumPublicTransportClosureDurationDays = 0f;
            MinimumCommercialClosureDurationDays = 0f;
            MinimumLeisureTourismParksClosureDurationDays = 0f;
            MinimumOfficeClosureDurationDays = 0f;
            MinimumIndustryClosureDurationDays = 0f;
            MinimumGovernmentOtherPublicClosureDurationDays = 0f;
            MinimumEssentialServicesClosureDurationDays = 0f;
            EducationLockdownCooldownDurationDays = 0f;
            PublicTransportLockdownCooldownDurationDays = 0f;
            CommercialLockdownCooldownDurationDays = 0f;
            LeisureTourismParksLockdownCooldownDurationDays = 0f;
            OfficeLockdownCooldownDurationDays = 0f;
            IndustryLockdownCooldownDurationDays = 0f;
            GovernmentOtherPublicLockdownCooldownDurationDays = 0f;
            EssentialServicesLockdownCooldownDurationDays = 0f;

            SetScientificDefaultsFromLegacy();

            ShowIncompatibilityNotifications = true;
        }

        private void SetScientificDefaultsFromLegacy()
        {
            PreserveLegacyEffectiveMaskDistribution();
            ExposedDurationDistributionType = PandemicDistributionType.Deterministic;
            ExposedDurationMeanDays = StartInfection;
            ExposedDurationStandardDeviationDays = 0f;
            ExposedDurationMinimumDays = 0f;
            ExposedDurationMaximumDays = 3650f;
            ExposedDurationFixedDays = StartInfection;
            InfectiousStartDistributionType = PandemicDistributionType.Deterministic;
            InfectiousStartMeanDays = StartInfection;
            InfectiousStartStandardDeviationDays = 0f;
            InfectiousStartMinimumDays = 0f;
            InfectiousStartMaximumDays = 3650f;
            InfectiousStartFixedDays = StartInfection;
            InfectiousEndDistributionType = PandemicDistributionType.Deterministic;
            InfectiousEndMeanDays = EndInfection;
            InfectiousEndStandardDeviationDays = 0f;
            InfectiousEndMinimumDays = 0f;
            InfectiousEndMaximumDays = 3650f;
            InfectiousEndFixedDays = EndInfection;
            SymptomStartDistributionType = PandemicDistributionType.Deterministic;
            SymptomStartMeanDays = StartSymptoms;
            SymptomStartStandardDeviationDays = 0f;
            SymptomStartMinimumDays = 0f;
            SymptomStartMaximumDays = 3650f;
            SymptomStartFixedDays = StartSymptoms;
            SymptomEndDistributionType = PandemicDistributionType.Deterministic;
            SymptomEndMeanDays = EndSymptoms;
            SymptomEndStandardDeviationDays = 0f;
            SymptomEndMinimumDays = 0f;
            SymptomEndMaximumDays = 3650f;
            SymptomEndFixedDays = EndSymptoms;
            RecoveryDistributionType = PandemicDistributionType.Deterministic;
            RecoveryMeanDays = DiseaseDuration;
            RecoveryStandardDeviationDays = 0f;
            RecoveryMinimumDays = 0f;
            RecoveryMaximumDays = 3650f;
            RecoveryFixedDays = DiseaseDuration;

            InfectiousnessProfileType = PandemicInfectiousnessProfileType.Flat;
            InfectiousnessProfileStartMultiplier = 1f;
            InfectiousnessProfilePeakTimeFraction = 0.5f;
            InfectiousnessProfilePeakMultiplier = 1f;
            InfectiousnessProfileEndMultiplier = 1f;
            InitialSeedSamplingStrategy = PandemicInitialSeedSamplingStrategy.UniformPopulation;
            InitialInfectionAgeMode = PandemicInitialInfectionAgeMode.FixedInitialInfectionAge;
            InitialInfectionAgeDistributionType = PandemicDistributionType.Deterministic;
            InitialInfectionAgeMeanDays = StartInfection;
            InitialInfectionAgeStandardDeviationDays = 0f;
            InitialInfectionAgeMinimumDays = 0f;
            InitialInfectionAgeMaximumDays = 3650f;
            InitialInfectionAgeFixedDays = StartInfection;
            AsymptomaticMortalityMultiplier = 0.10f;
            HealthcareWarningThresholdPercent = 75f;
            HealthcareCriticalThresholdPercent = 90f;
            HealthcareWarningMortalityMultiplier = 1.35f;
            HealthcareCriticalMortalityMultiplier = 2f;
        }

        private void PreserveLegacyEffectiveMaskDistribution()
        {
            int[] weights =
            {
                ClampPercentage(RatioIgnoreMasks),
                ClampPercentage(RatioOtherProtectionMask),
                ClampPercentage(RatioOwnProtectionMask),
            };
            int total = weights[0] + weights[1] + weights[2];
            if (total == 0)
            {
                // The legacy division-by-zero comparisons assigned no mask to anyone.
                RatioIgnoreMasks = 100;
                RatioOtherProtectionMask = 0;
                RatioOwnProtectionMask = 0;
                return;
            }

            int[] normalized = new int[3];
            double[] remainders = new double[3];
            int assigned = 0;
            for (int i = 0; i < weights.Length; ++i)
            {
                double exact = weights[i] * 100d / total;
                normalized[i] = (int)System.Math.Floor(exact);
                remainders[i] = exact - normalized[i];
                assigned += normalized[i];
            }

            while (assigned < 100)
            {
                int largest = 0;
                for (int i = 1; i < remainders.Length; ++i)
                {
                    if (remainders[i] > remainders[largest])
                    {
                        largest = i;
                    }
                }

                normalized[largest]++;
                remainders[largest] = -1d;
                assigned++;
            }

            RatioIgnoreMasks = normalized[0];
            RatioOtherProtectionMask = normalized[1];
            RatioOwnProtectionMask = normalized[2];
        }

        private static int ClampPercentage(int value)
        {
            return value < 0 ? 0 : value > 100 ? 100 : value;
        }

        private void ClampScientificNumbers()
        {
            ExposedDurationMeanDays = ClampScientificDays(ExposedDurationMeanDays);
            ExposedDurationStandardDeviationDays = ClampScientificDays(ExposedDurationStandardDeviationDays);
            ExposedDurationMinimumDays = ClampScientificDays(ExposedDurationMinimumDays);
            ExposedDurationMaximumDays = ClampScientificDays(ExposedDurationMaximumDays);
            ExposedDurationFixedDays = ClampScientificDays(ExposedDurationFixedDays);
            InfectiousStartMeanDays = ClampScientificDays(InfectiousStartMeanDays);
            InfectiousStartStandardDeviationDays = ClampScientificDays(InfectiousStartStandardDeviationDays);
            InfectiousStartMinimumDays = ClampScientificDays(InfectiousStartMinimumDays);
            InfectiousStartMaximumDays = ClampScientificDays(InfectiousStartMaximumDays);
            InfectiousStartFixedDays = ClampScientificDays(InfectiousStartFixedDays);
            InfectiousEndMeanDays = ClampScientificDays(InfectiousEndMeanDays);
            InfectiousEndStandardDeviationDays = ClampScientificDays(InfectiousEndStandardDeviationDays);
            InfectiousEndMinimumDays = ClampScientificDays(InfectiousEndMinimumDays);
            InfectiousEndMaximumDays = ClampScientificDays(InfectiousEndMaximumDays);
            InfectiousEndFixedDays = ClampScientificDays(InfectiousEndFixedDays);
            SymptomStartMeanDays = ClampScientificDays(SymptomStartMeanDays);
            SymptomStartStandardDeviationDays = ClampScientificDays(SymptomStartStandardDeviationDays);
            SymptomStartMinimumDays = ClampScientificDays(SymptomStartMinimumDays);
            SymptomStartMaximumDays = ClampScientificDays(SymptomStartMaximumDays);
            SymptomStartFixedDays = ClampScientificDays(SymptomStartFixedDays);
            SymptomEndMeanDays = ClampScientificDays(SymptomEndMeanDays);
            SymptomEndStandardDeviationDays = ClampScientificDays(SymptomEndStandardDeviationDays);
            SymptomEndMinimumDays = ClampScientificDays(SymptomEndMinimumDays);
            SymptomEndMaximumDays = ClampScientificDays(SymptomEndMaximumDays);
            SymptomEndFixedDays = ClampScientificDays(SymptomEndFixedDays);
            RecoveryMeanDays = ClampScientificDays(RecoveryMeanDays);
            RecoveryStandardDeviationDays = ClampScientificDays(RecoveryStandardDeviationDays);
            RecoveryMinimumDays = ClampScientificDays(RecoveryMinimumDays);
            RecoveryMaximumDays = ClampScientificDays(RecoveryMaximumDays);
            RecoveryFixedDays = ClampScientificDays(RecoveryFixedDays);
            InitialInfectionAgeMeanDays = ClampScientificDays(InitialInfectionAgeMeanDays);
            InitialInfectionAgeStandardDeviationDays = ClampScientificDays(InitialInfectionAgeStandardDeviationDays);
            InitialInfectionAgeMinimumDays = ClampScientificDays(InitialInfectionAgeMinimumDays);
            InitialInfectionAgeMaximumDays = ClampScientificDays(InitialInfectionAgeMaximumDays);
            InitialInfectionAgeFixedDays = ClampScientificDays(InitialInfectionAgeFixedDays);
            InfectiousnessProfileStartMultiplier = ClampScientificMultiplier(InfectiousnessProfileStartMultiplier);
            InfectiousnessProfilePeakTimeFraction = ClampFinite(InfectiousnessProfilePeakTimeFraction, 0f, 1f, 0.5f);
            InfectiousnessProfilePeakMultiplier = ClampScientificMultiplier(InfectiousnessProfilePeakMultiplier);
            InfectiousnessProfileEndMultiplier = ClampScientificMultiplier(InfectiousnessProfileEndMultiplier);
            AsymptomaticMortalityMultiplier = ClampScientificMultiplier(AsymptomaticMortalityMultiplier);
            HealthcareWarningThresholdPercent = ClampFinite(HealthcareWarningThresholdPercent, 0f, 100f, 75f);
            HealthcareCriticalThresholdPercent = ClampFinite(HealthcareCriticalThresholdPercent, 0f, 100f, 90f);
            HealthcareWarningMortalityMultiplier = ClampScientificMultiplier(HealthcareWarningMortalityMultiplier);
            HealthcareCriticalMortalityMultiplier = ClampScientificMultiplier(HealthcareCriticalMortalityMultiplier);
        }

        private static PandemicDistributionType ClampDistributionType(PandemicDistributionType value)
        {
            return (PandemicDistributionType)FastMath.Clamp(
                (int)value,
                (int)PandemicDistributionType.Deterministic,
                (int)PandemicDistributionType.Gamma);
        }

        private static float ClampScientificDays(float value)
        {
            return ClampFinite(value, 0f, 3650f, 0f);
        }

        private static float ClampScientificMultiplier(float value)
        {
            return ClampFinite(value, 0f, 100f, 0f);
        }

        private static float ClampFinite(float value, float minimum, float maximum, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? fallback
                : FastMath.Clamp(value, minimum, maximum);
        }

        private static float ClampLockdownReopenThreshold(float value, float closeThreshold)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return closeThreshold;
            }

            return FastMath.Clamp(value, 0f, closeThreshold);
        }

        private static float ClampLockdownDuration(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return 0f;
            }

            return FastMath.Clamp(value, 0f, 3650f);
        }
    }
}
