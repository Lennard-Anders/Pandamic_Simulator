// <copyright file="ExperimentPlanValidator.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.Experiments
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using RealTime.Config;

    /// <summary>Performs side-effect-free validation of batch plans and durable state.</summary>
    public sealed class ExperimentPlanValidator
    {
        public static ExperimentValidationResult ValidateSettings(ExperimentScenarioSnapshot settings)
        {
            var result = new ExperimentValidationResult();
            if (settings == null) result.Errors.Add("Settings are required.");
            else ValidateEpidemiology(settings, "Scenario", result);
            return result;
        }
        public ExperimentValidationResult Validate(ExperimentBatchPlan plan)
        {
            ExperimentValidationResult result = new ExperimentValidationResult();
            if (plan == null)
            {
                result.Errors.Add("The batch plan is missing.");
                return result;
            }

            ValidateSchema(plan.SchemaVersion, "batch plan", result);
            Require(plan.BatchId, "BatchId", result);
            Require(plan.BatchName, "BatchName", result);
            Require(plan.CreatedUtc, "CreatedUtc", result);
            Require(plan.ModVersion, "ModVersion", result);
            Require(plan.GameVersion, "GameVersion", result);
            Require(plan.GitCommitSha, "GitCommitSha", result);
            Require(plan.GitBranchOrTag, "GitBranchOrTag", result);
            Require(plan.OutputRoot, "OutputRoot", result);
            if (plan.OutputLocation == null)
            {
                result.Errors.Add("An output-root selection is required.");
            }
            else if (plan.OutputLocation.Kind != ExperimentOutputRootKind.ModPandemicData
                && plan.OutputLocation.Kind != ExperimentOutputRootKind.UserPandemicData)
            {
                result.Errors.Add("The output-root selection is unsupported.");
            }

            if (plan.ModelProvenance == null)
            {
                result.Errors.Add("Model provenance is required.");
            }
            else
            {
                ValidateSchema(plan.ModelProvenance.SchemaVersion, "model provenance", result);
                Require(plan.ModelProvenance.RandomAlgorithm, "ModelProvenance.RandomAlgorithm", result);
                Require(plan.ModelProvenance.TraitAssignmentAlgorithm, "ModelProvenance.TraitAssignmentAlgorithm", result);
                Require(plan.ModelProvenance.ConfigurationHashAlgorithm, "ModelProvenance.ConfigurationHashAlgorithm", result);
                Require(plan.ModelProvenance.DiseaseProgressionModel, "ModelProvenance.DiseaseProgressionModel", result);
                Require(plan.ModelProvenance.TransmissionModel, "ModelProvenance.TransmissionModel", result);
                Require(plan.ModelProvenance.EpidemicStepPolicy, "ModelProvenance.EpidemicStepPolicy", result);
                if (plan.ModelProvenance.RandomStreamCount != 9)
                {
                    result.Errors.Add("ModelProvenance.RandomStreamCount must identify all nine independent TENUS streams.");
                }
            }

            if (plan.SpeedMode == ExperimentSpeedMode.Unspecified)
            {
                result.Errors.Add("An experiment execution speed must be selected explicitly.");
            }
            ValidateBaseline(plan.Baseline, result);

            if (plan.Scenarios == null || plan.Scenarios.Count == 0)
            {
                result.Errors.Add("At least one scenario is required.");
                return result;
            }

            Dictionary<string, bool> scenarioIds = new Dictionary<string, bool>(StringComparer.Ordinal);
            for (int i = 0; i < plan.Scenarios.Count; ++i)
            {
                ValidateScenario(plan.Scenarios[i], i, scenarioIds, result, !plan.PairedSeedMode);
            }

            if (plan.PairedSeedMode && plan.Scenarios[0] != null)
            {
                int maximumRunCount = 0;
                for (int i = 0; i < plan.Scenarios.Count; ++i)
                {
                    ExperimentScenario scenario = plan.Scenarios[i];
                    if (scenario != null)
                    {
                        maximumRunCount = Math.Max(maximumRunCount, scenario.RunCount);
                    }
                }

                if (maximumRunCount > 0
                    && (long)plan.Scenarios[0].FirstSeed + maximumRunCount - 1L > int.MaxValue)
                {
                    result.Errors.Add("The paired master-seed range exceeds Int32.MaxValue.");
                }
            }

            return result;
        }

        public ExperimentValidationResult ValidateState(ExperimentBatchPlan plan, ExperimentBatchState state)
        {
            ExperimentValidationResult result = new ExperimentValidationResult();
            if (state == null)
            {
                result.Errors.Add("The durable batch state is missing.");
                return result;
            }

            ValidateSchema(state.SchemaVersion, "batch state", result);
            if (plan == null || !string.Equals(plan.BatchId, state.BatchId, StringComparison.Ordinal))
            {
                result.Errors.Add("The durable batch state does not match the batch plan.");
                return result;
            }

            if (state.ScenarioIndex < 0 || state.RunIndex < 0)
            {
                result.Errors.Add("The durable run position cannot be negative.");
            }

            bool afterLastRun = plan.Scenarios != null && state.ScenarioIndex == plan.Scenarios.Count;
            if (!afterLastRun
                && (plan.Scenarios == null
                    || state.ScenarioIndex >= plan.Scenarios.Count
                    || plan.Scenarios[state.ScenarioIndex] == null
                    || state.RunIndex >= plan.Scenarios[state.ScenarioIndex].RunCount))
            {
                result.Errors.Add("The durable run position is outside the batch plan.");
            }

            if (afterLastRun && state.RunIndex != 0)
            {
                result.Errors.Add("A completed batch must have a zero run index after its final scenario.");
            }

            return result;
        }

        private static void ValidateScenario(
            ExperimentScenario scenario,
            int index,
            IDictionary<string, bool> scenarioIds,
            ExperimentValidationResult result,
            bool validateScenarioSeedRange)
        {
            string prefix = string.Format(CultureInfo.InvariantCulture, "Scenario {0}", index + 1);
            if (scenario == null)
            {
                result.Errors.Add(prefix + " is missing.");
                return;
            }

            ValidateSchema(scenario.SchemaVersion, prefix, result);
            if (string.IsNullOrEmpty(scenario.ScenarioId))
            {
                result.Errors.Add(prefix + " has no ScenarioId.");
            }
            else if (scenarioIds.ContainsKey(scenario.ScenarioId))
            {
                result.Errors.Add(prefix + " has a duplicate ScenarioId.");
            }
            else
            {
                scenarioIds.Add(scenario.ScenarioId, true);
            }

            if (string.IsNullOrEmpty(scenario.Name))
            {
                result.Errors.Add(prefix + " has no name.");
            }

            if (scenario.RunCount <= 0)
            {
                result.Errors.Add(prefix + " must have at least one run.");
            }

            if (scenario.FirstSeed < 0)
            {
                result.Errors.Add(prefix + " master seed cannot be negative.");
            }

            if (scenario.DurationDays <= 0d
                || scenario.DurationDays > 3650d
                || double.IsNaN(scenario.DurationDays)
                || double.IsInfinity(scenario.DurationDays))
            {
                result.Errors.Add(prefix + " must have a finite duration greater than zero and no more than 3650 days.");
            }

            if (validateScenarioSeedRange
                && scenario.SeedStrategy == ExperimentSeedStrategy.Sequential
                && scenario.RunCount > 0
                && (long)scenario.FirstSeed + scenario.RunCount - 1L > int.MaxValue)
            {
                result.Errors.Add(prefix + " sequential seed range exceeds Int32.MaxValue.");
            }

            if (scenario.Settings == null)
            {
                result.Errors.Add(prefix + " has no settings snapshot.");
            }
            else
            {
                ValidateSchema(scenario.Settings.SchemaVersion, prefix + " settings", result);
                ValidateEpidemiology(scenario.Settings, prefix, result);
                var schedule = scenario.InterventionSchedule;
                if (schedule != null)
                {
                    try { schedule.Validate(scenario.Settings); }
                    catch (Exception ex) { result.Errors.Add(prefix + " schedule: " + ex.Message); }
                }
            }
            if (scenario.CalibrationTargets != null)
            {
                try { scenario.CalibrationTargets.Validate(); }
                catch (Exception ex) { result.Errors.Add(prefix + " calibration targets: " + ex.Message); }
            }
        }

        private static void ValidateEpidemiology(
            ExperimentScenarioSnapshot settings,
            string prefix,
            ExperimentValidationResult result)
        {
            if (settings.DiseaseDuration == 0)
            {
                result.Errors.Add(prefix + " DiseaseDuration must be greater than zero.");
            }

            if (settings.StartInfection >= settings.EndInfection)
            {
                result.Errors.Add(prefix + " requires StartInfection < EndInfection.");
            }

            if (settings.EndInfection > settings.DiseaseDuration)
            {
                result.Errors.Add(prefix + " requires EndInfection <= DiseaseDuration.");
            }

            if (settings.StartSymptoms >= settings.EndSymptoms)
            {
                result.Errors.Add(prefix + " requires StartSymptoms < EndSymptoms.");
            }

            if (settings.EndSymptoms > settings.DiseaseDuration)
            {
                result.Errors.Add(prefix + " requires EndSymptoms <= DiseaseDuration.");
            }

            ValidatePercent(settings.DiseaseStartInfectionRatio, prefix + " DiseaseStartInfectionRatio", result);
            ValidatePercent(settings.SymptomProbability, prefix + " SymptomProbability", result);
            ValidatePercent(settings.MaskCompliancePercent, prefix + " MaskCompliancePercent", result);
            ValidatePercent(settings.IsolationCompliancePercent, prefix + " IsolationCompliancePercent", result);
            ValidatePercent(settings.QuarantineCompliancePercent, prefix + " QuarantineCompliancePercent", result);
            ValidatePercent(settings.DeathChild, prefix + " DeathChild", result);
            ValidatePercent(settings.DeathTeen, prefix + " DeathTeen", result);
            ValidatePercent(settings.DeathYoung, prefix + " DeathYoung", result);
            ValidatePercent(settings.DeathAdult, prefix + " DeathAdult", result);
            ValidatePercent(settings.DeathSenior, prefix + " DeathSenior", result);
            ValidatePercent(settings.RelativeTestCapacity, prefix + " RelativeTestCapacity", result);
            ValidatePercent(settings.PercentageOfTestsReservedForSickCitizens, prefix + " PercentageOfTestsReservedForSickCitizens", result);
            ValidatePercent(settings.TestSensitivityPercent, prefix + " TestSensitivityPercent", result);
            ValidatePercent(settings.TestSpecificityPercent, prefix + " TestSpecificityPercent", result);
            ValidatePercent(settings.BuildingContactTracingProbability, prefix + " BuildingContactTracingProbability", result);
            ValidatePercent(settings.AppBasedContactTracingProbability, prefix + " AppBasedContactTracingProbability", result);
            if (settings.TransmissionProbabilityReduction < 1
                || settings.TransmissionProbabilityReduction > 20)
            {
                result.Errors.Add(prefix + " TransmissionProbabilityReduction must be between 1 and 20.");
            }
            ValidateDistribution(
                settings.ExposedDurationDistributionType,
                settings.ExposedDurationMeanDays,
                settings.ExposedDurationStandardDeviationDays,
                settings.ExposedDurationMinimumDays,
                settings.ExposedDurationMaximumDays,
                settings.ExposedDurationFixedDays,
                prefix + " ExposedDuration",
                result);
            ValidateDistribution(
                settings.InfectiousStartDistributionType,
                settings.InfectiousStartMeanDays,
                settings.InfectiousStartStandardDeviationDays,
                settings.InfectiousStartMinimumDays,
                settings.InfectiousStartMaximumDays,
                settings.InfectiousStartFixedDays,
                prefix + " InfectiousStart",
                result);
            ValidateDistribution(
                settings.InfectiousEndDistributionType,
                settings.InfectiousEndMeanDays,
                settings.InfectiousEndStandardDeviationDays,
                settings.InfectiousEndMinimumDays,
                settings.InfectiousEndMaximumDays,
                settings.InfectiousEndFixedDays,
                prefix + " InfectiousEnd",
                result);
            ValidateDistribution(
                settings.SymptomStartDistributionType,
                settings.SymptomStartMeanDays,
                settings.SymptomStartStandardDeviationDays,
                settings.SymptomStartMinimumDays,
                settings.SymptomStartMaximumDays,
                settings.SymptomStartFixedDays,
                prefix + " SymptomStart",
                result);
            ValidateDistribution(
                settings.SymptomEndDistributionType,
                settings.SymptomEndMeanDays,
                settings.SymptomEndStandardDeviationDays,
                settings.SymptomEndMinimumDays,
                settings.SymptomEndMaximumDays,
                settings.SymptomEndFixedDays,
                prefix + " SymptomEnd",
                result);
            ValidateDistribution(
                settings.RecoveryDistributionType,
                settings.RecoveryMeanDays,
                settings.RecoveryStandardDeviationDays,
                settings.RecoveryMinimumDays,
                settings.RecoveryMaximumDays,
                settings.RecoveryFixedDays,
                prefix + " Recovery",
                result);
            ValidateDistribution(
                settings.InitialInfectionAgeDistributionType,
                settings.InitialInfectionAgeMeanDays,
                settings.InitialInfectionAgeStandardDeviationDays,
                settings.InitialInfectionAgeMinimumDays,
                settings.InitialInfectionAgeMaximumDays,
                settings.InitialInfectionAgeFixedDays,
                prefix + " InitialInfectionAge",
                result);
            if (!Enum.IsDefined(typeof(RealTime.Pandemic.PolicyTriggerMetric), settings.AutomaticPolicyTriggerMetric))
            {
                result.Errors.Add(prefix + " AutomaticPolicyTriggerMetric is unsupported.");
            }

            if (!Enum.IsDefined(typeof(PandemicInfectiousnessProfileType), settings.InfectiousnessProfileType))
            {
                result.Errors.Add(prefix + " InfectiousnessProfileType is unsupported.");
            }

            if (!Enum.IsDefined(typeof(PandemicInitialSeedSamplingStrategy), settings.InitialSeedSamplingStrategy))
            {
                result.Errors.Add(prefix + " InitialSeedSamplingStrategy is unsupported.");
            }

            if (!Enum.IsDefined(typeof(PandemicInitialInfectionAgeMode), settings.InitialInfectionAgeMode))
            {
                result.Errors.Add(prefix + " InitialInfectionAgeMode is unsupported.");
            }

            ValidateFiniteNonnegative(settings.InfectiousnessProfileStartMultiplier, prefix + " InfectiousnessProfileStartMultiplier", result);
            ValidateFiniteNonnegative(settings.InfectiousnessProfilePeakMultiplier, prefix + " InfectiousnessProfilePeakMultiplier", result);
            ValidateFiniteNonnegative(settings.InfectiousnessProfileEndMultiplier, prefix + " InfectiousnessProfileEndMultiplier", result);
            if (!IsFiniteInRange(settings.InfectiousnessProfilePeakTimeFraction, 0d, 1d))
            {
                result.Errors.Add(prefix + " InfectiousnessProfilePeakTimeFraction must be finite and between 0 and 1.");
            }

            ValidateFiniteNonnegative(settings.AsymptomaticMortalityMultiplier, prefix + " AsymptomaticMortalityMultiplier", result);
            ValidatePercent(settings.HealthcareWarningThresholdPercent, prefix + " HealthcareWarningThresholdPercent", result);
            ValidatePercent(settings.HealthcareCriticalThresholdPercent, prefix + " HealthcareCriticalThresholdPercent", result);
            ValidateFiniteNonnegative(settings.HealthcareWarningMortalityMultiplier, prefix + " HealthcareWarningMortalityMultiplier", result);
            ValidateFiniteNonnegative(settings.HealthcareCriticalMortalityMultiplier, prefix + " HealthcareCriticalMortalityMultiplier", result);
            if (settings.HealthcareWarningThresholdPercent > settings.HealthcareCriticalThresholdPercent)
            {
                result.Errors.Add(prefix + " requires HealthcareWarningThresholdPercent <= HealthcareCriticalThresholdPercent.");
            }

            double earliestInfectiousStart = Math.Max(
                settings.ExposedDurationMinimumDays,
                settings.InfectiousStartMinimumDays);
            if (settings.InfectiousEndMaximumDays <= earliestInfectiousStart)
            {
                result.Errors.Add(prefix + " disease distributions cannot produce InfectiousStart < InfectiousEnd.");
            }

            if (settings.RecoveryMaximumDays <= earliestInfectiousStart)
            {
                result.Errors.Add(prefix + " disease distributions cannot produce InfectiousStart < Recovery.");
            }

            if (settings.SymptomEndMaximumDays <= settings.SymptomStartMinimumDays)
            {
                result.Errors.Add(prefix + " disease distributions cannot produce SymptomStart < SymptomEnd.");
            }
            ValidateLockdownPolicy(
                settings.CloseEducationThresholdPercent,
                settings.ReopenEducationThresholdPercent,
                settings.MinimumEducationClosureDurationDays,
                settings.EducationLockdownCooldownDurationDays,
                prefix + " Education lockdown",
                result);
            ValidateLockdownPolicy(
                settings.ClosePublicTransportThresholdPercent,
                settings.ReopenPublicTransportThresholdPercent,
                settings.MinimumPublicTransportClosureDurationDays,
                settings.PublicTransportLockdownCooldownDurationDays,
                prefix + " PublicTransport lockdown",
                result);
            ValidateLockdownPolicy(
                settings.CloseCommercialThresholdPercent,
                settings.ReopenCommercialThresholdPercent,
                settings.MinimumCommercialClosureDurationDays,
                settings.CommercialLockdownCooldownDurationDays,
                prefix + " Commercial lockdown",
                result);
            ValidateLockdownPolicy(
                settings.CloseLeisureTourismParksThresholdPercent,
                settings.ReopenLeisureTourismParksThresholdPercent,
                settings.MinimumLeisureTourismParksClosureDurationDays,
                settings.LeisureTourismParksLockdownCooldownDurationDays,
                prefix + " LeisureTourismParks lockdown",
                result);
            ValidateLockdownPolicy(
                settings.CloseOfficeThresholdPercent,
                settings.ReopenOfficeThresholdPercent,
                settings.MinimumOfficeClosureDurationDays,
                settings.OfficeLockdownCooldownDurationDays,
                prefix + " Office lockdown",
                result);
            ValidateLockdownPolicy(
                settings.CloseIndustryThresholdPercent,
                settings.ReopenIndustryThresholdPercent,
                settings.MinimumIndustryClosureDurationDays,
                settings.IndustryLockdownCooldownDurationDays,
                prefix + " Industry lockdown",
                result);
            ValidateLockdownPolicy(
                settings.CloseGovernmentOtherPublicThresholdPercent,
                settings.ReopenGovernmentOtherPublicThresholdPercent,
                settings.MinimumGovernmentOtherPublicClosureDurationDays,
                settings.GovernmentOtherPublicLockdownCooldownDurationDays,
                prefix + " GovernmentOtherPublic lockdown",
                result);
            ValidateLockdownPolicy(
                settings.CloseEssentialServicesThresholdPercent,
                settings.ReopenEssentialServicesThresholdPercent,
                settings.MinimumEssentialServicesClosureDurationDays,
                settings.EssentialServicesLockdownCooldownDurationDays,
                prefix + " EssentialServices lockdown",
                result);

            if (!IsFiniteInRange(settings.IndoorDiseaseTransmissionProbability, 0d, 100d))
            {
                result.Errors.Add(prefix + " IndoorDiseaseTransmissionProbability must be finite and between 0 and 100.");
            }

            if (!IsFiniteInRange(settings.OutdoorDiseaseTransmissionProbability, 0d, 100d))
            {
                result.Errors.Add(prefix + " OutdoorDiseaseTransmissionProbability must be finite and between 0 and 100.");
            }

            if (settings.DiseaseTransmissionRange < 0f
                || float.IsNaN(settings.DiseaseTransmissionRange)
                || float.IsInfinity(settings.DiseaseTransmissionRange))
            {
                result.Errors.Add(prefix + " DiseaseTransmissionRange must be finite and nonnegative.");
            }

            if (settings.MinimumTestDuration > 28 || settings.MaximumTestDuration > 28)
            {
                result.Errors.Add(prefix + " test durations must be no more than 28 days.");
            }

            if (settings.RetestIntervalDays > 28)
            {
                result.Errors.Add(prefix + " RetestIntervalDays must be no more than 28 days.");
            }

            if (settings.EpidemicStepMinutes == 0 || settings.EpidemicStepMinutes > 60)
            {
                result.Errors.Add(prefix + " EpidemicStepMinutes must be between 1 and 60.");
            }

            if (!Enum.IsDefined(typeof(ScientificContactExportMode), settings.ScientificContactExportMode)
                || !IsFiniteInRange(settings.MaximumRawContactExportGB, 0, 1000))
                result.Errors.Add(prefix + " contact export mode must be recognized and maximum output must be 0–1000 GB (0 means unlimited).");

            if (!Enum.IsDefined(typeof(ContactPersistenceModel), settings.ContactPersistenceModel)
                || settings.ContactPersistenceMinutesSchool > 1440
                || settings.ContactPersistenceMinutesUniversity > 1440
                || settings.ContactPersistenceMinutesWorkplace > 1440
                || settings.ContactPersistenceMinutesHealthcare > 1440
                || settings.ContactPersistenceMinutesCommercial > 1440
                || settings.ContactPersistenceMinutesLeisure > 1440
                || settings.ContactPersistenceMinutesTransit > 1440
                || settings.ContactPersistenceMinutesResidentialSharedArea > 1440)
            {
                result.Errors.Add(prefix + " contact persistence model must be recognized and windows must be 0–1440 minutes (0 retains per-step sampling).");
            }

            if (settings.MaxContactsPerPersonPerStepSchool == 0
                || settings.MaxContactsPerPersonPerStepWorkplace == 0
                || settings.MaxContactsPerPersonPerStepCommercial == 0
                || settings.MaxContactsPerPersonPerStepHealthcare == 0
                || settings.MaxContactsPerPersonPerStepTransit == 0
                || settings.MaxContactsPerPersonPerStepResidentialSharedArea == 0)
            {
                result.Errors.Add(prefix + " contact sampling caps must all be greater than zero.");
            }

            if (!IsFiniteInRange(settings.ResidentialSharedAreaTransmissionMultiplier, 0d, 10d))
            {
                result.Errors.Add(prefix + " ResidentialSharedAreaTransmissionMultiplier must be finite and between 0 and 10.");
            }

            int maskTotal = settings.RatioIgnoreMasks
                + settings.RatioOtherProtectionMask
                + settings.RatioOwnProtectionMask;
            if (settings.RatioIgnoreMasks < 0
                || settings.RatioOtherProtectionMask < 0
                || settings.RatioOwnProtectionMask < 0
                || maskTotal != 100)
            {
                result.Errors.Add(prefix + " mask percentages must be nonnegative and sum to exactly 100.");
            }
        }

        private static void ValidatePercent(double value, string name, ExperimentValidationResult result)
        {
            if (!IsFiniteInRange(value, 0d, 100d))
            {
                result.Errors.Add(name + " must be finite and between 0 and 100.");
            }
        }

        private static void ValidateDistribution(
            PandemicDistributionType type,
            double mean,
            double standardDeviation,
            double minimum,
            double maximum,
            double fixedValue,
            string name,
            ExperimentValidationResult result)
        {
            if (!Enum.IsDefined(typeof(PandemicDistributionType), type))
            {
                result.Errors.Add(name + " distribution type is unsupported.");
                return;
            }

            if (!IsFiniteInRange(minimum, 0d, 3650d)
                || !IsFiniteInRange(maximum, 0d, 3650d)
                || maximum < minimum)
            {
                result.Errors.Add(name + " bounds must be finite, between 0 and 3650, and ordered.");
            }

            if (!IsFiniteInRange(standardDeviation, 0d, 3650d))
            {
                result.Errors.Add(name + " standard deviation must be finite and between 0 and 3650.");
            }

            if (!IsFiniteInRange(mean, minimum, maximum))
            {
                result.Errors.Add(name + " arithmetic mean must lie within its configured bounds.");
            }

            if (type == PandemicDistributionType.Deterministic
                && !IsFiniteInRange(fixedValue, minimum, maximum))
            {
                result.Errors.Add(name + " fixed value must lie within its configured bounds.");
            }

            if ((type == PandemicDistributionType.LogNormal || type == PandemicDistributionType.Gamma)
                && mean <= 0d)
            {
                result.Errors.Add(name + " arithmetic mean must be positive for log-normal or gamma sampling.");
            }
        }

        private static void ValidateFiniteNonnegative(
            double value,
            string name,
            ExperimentValidationResult result)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
            {
                result.Errors.Add(name + " must be finite and nonnegative.");
            }
        }

        private static void ValidateLockdownPolicy(
            double closeThreshold,
            double reopenThreshold,
            double minimumClosureDurationDays,
            double cooldownDurationDays,
            string name,
            ExperimentValidationResult result)
        {
            ValidatePercent(closeThreshold, name + " CloseThresholdPercent", result);
            ValidatePercent(reopenThreshold, name + " ReopenThresholdPercent", result);
            if (IsFiniteInRange(closeThreshold, 0d, 100d)
                && IsFiniteInRange(reopenThreshold, 0d, 100d)
                && reopenThreshold > closeThreshold)
            {
                result.Errors.Add(name + " requires ReopenThresholdPercent <= CloseThresholdPercent.");
            }

            if (!IsFiniteInRange(minimumClosureDurationDays, 0d, 3650d))
            {
                result.Errors.Add(name + " MinimumClosureDurationDays must be finite and between 0 and 3650.");
            }

            if (!IsFiniteInRange(cooldownDurationDays, 0d, 3650d))
            {
                result.Errors.Add(name + " CooldownDurationDays must be finite and between 0 and 3650.");
            }
        }

        private static bool IsFiniteInRange(double value, double minimum, double maximum)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value >= minimum && value <= maximum;
        }

        private static void ValidateBaseline(BaselineSaveIdentity baseline, ExperimentValidationResult result)
        {
            if (baseline == null)
            {
                result.Errors.Add("A baseline save is required.");
                return;
            }

            ValidateSchema(baseline.SchemaVersion, "baseline save identity", result);
            Require(baseline.AssetFullName, "Baseline.AssetFullName", result);
            Require(baseline.AssetChecksum, "Baseline.AssetChecksum", result);
            Require(baseline.PackageName, "Baseline.PackageName", result);
            Require(baseline.DataAssetChecksum, "Baseline.DataAssetChecksum", result);
        }

        private static void ValidateSchema(int version, string artifact, ExperimentValidationResult result)
        {
            if (version != ExperimentSchema.CurrentVersion)
            {
                result.Errors.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "The {0} schema version {1} is unsupported; expected {2}.",
                    artifact,
                    version,
                    ExperimentSchema.CurrentVersion));
            }
        }

        private static void Require(string value, string name, ExperimentValidationResult result)
        {
            if (string.IsNullOrEmpty(value) || value.Trim().Length == 0)
            {
                result.Errors.Add(name + " is required.");
            }
        }
    }
}
