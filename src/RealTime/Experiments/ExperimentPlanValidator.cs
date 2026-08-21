// <copyright file="ExperimentPlanValidator.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.Experiments
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    /// <summary>Performs side-effect-free validation of batch plans and durable state.</summary>
    public sealed class ExperimentPlanValidator
    {
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
                ValidateScenario(plan.Scenarios[i], i, scenarioIds, result);
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
            ExperimentValidationResult result)
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

            if (scenario.SeedStrategy == ExperimentSeedStrategy.Sequential
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
            }
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
