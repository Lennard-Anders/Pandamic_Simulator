// <copyright file="ExperimentValidationAndControlTests.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTimeTests.Experiments
{
    using System;
    using System.Collections.Generic;
    using RealTime.Config;
    using RealTime.Experiments;
    using NUnit.Framework;

    public sealed class ExperimentValidationAndControlTests
    {
        [Test]
        public void ValidatorRejectsNegativeAndOverflowingSeeds()
        {
            ExperimentBatchPlan negative = CreateValidPlan();
            negative.Scenarios[0].FirstSeed = -1;
            Assert.That(new ExperimentPlanValidator().Validate(negative).IsValid, Is.False);

            ExperimentBatchPlan overflow = CreateValidPlan();
            overflow.Scenarios[0].SeedStrategy = ExperimentSeedStrategy.Sequential;
            overflow.Scenarios[0].FirstSeed = int.MaxValue;
            overflow.Scenarios[0].RunCount = 2;
            Assert.That(new ExperimentPlanValidator().Validate(overflow).IsValid, Is.False);

            ExperimentBatchPlan pairedOverflow = CreateValidPlan();
            pairedOverflow.PairedSeedMode = true;
            pairedOverflow.Scenarios[0].FirstSeed = int.MaxValue;
            pairedOverflow.Scenarios[0].RunCount = 2;
            Assert.That(new ExperimentPlanValidator().Validate(pairedOverflow).Errors, Has.Some.Contains("paired master-seed"));
        }

        [Test]
        public void ValidatorRequiresExplicitSpeedAndBoundedDuration()
        {
            ExperimentBatchPlan plan = CreateValidPlan();
            Assert.That(new ExperimentPlanValidator().Validate(plan).IsValid, Is.True);

            plan.SpeedMode = ExperimentSpeedMode.Unspecified;
            Assert.That(new ExperimentPlanValidator().Validate(plan).IsValid, Is.False);

            plan.SpeedMode = ExperimentSpeedMode.Speed3;
            plan.Scenarios[0].DurationDays = 3650.0001d;
            Assert.That(new ExperimentPlanValidator().Validate(plan).IsValid, Is.False);
        }

        [Test]
        public void ControlGateSupportsNestedOwnerLeasesAndRejectsCompetitors()
        {
            Assert.That(ExperimentControlGate.IsControlLocked, Is.False);
            using (IDisposable first = ExperimentControlGate.Acquire("batch-a"))
            using (IDisposable second = ExperimentControlGate.Acquire("batch-a"))
            {
                Assert.That(ExperimentControlGate.IsControlLocked, Is.True);
                Assert.That(ExperimentControlGate.CurrentOwner, Is.EqualTo("batch-a"));
                Assert.Throws<InvalidOperationException>(() => ExperimentControlGate.Acquire("batch-b"));
            }

            Assert.That(ExperimentControlGate.IsControlLocked, Is.False);
        }

        [Test]
        public void ValidatorPreservesValidZeroTransmissionProbabilities()
        {
            ExperimentBatchPlan plan = CreateValidPlan();
            plan.Scenarios[0].Settings.IndoorDiseaseTransmissionProbability = 0f;
            plan.Scenarios[0].Settings.OutdoorDiseaseTransmissionProbability = 0f;

            Assert.That(new ExperimentPlanValidator().Validate(plan).IsValid, Is.True);
        }

        [Test]
        public void ValidatorRejectsInvalidTimelineAndMaskPercentages()
        {
            ExperimentBatchPlan plan = CreateValidPlan();
            plan.Scenarios[0].Settings.StartInfection = plan.Scenarios[0].Settings.EndInfection;
            plan.Scenarios[0].Settings.RatioIgnoreMasks = 30;
            plan.Scenarios[0].Settings.RatioOtherProtectionMask = 50;
            plan.Scenarios[0].Settings.RatioOwnProtectionMask = 50;

            ExperimentValidationResult result = new ExperimentPlanValidator().Validate(plan);
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("StartInfection"));
            Assert.That(result.Errors, Has.Some.Contains("mask percentages"));
        }

        [Test]
        public void ValidatorRejectsInvalidMaskReductionFactor()
        {
            ExperimentBatchPlan plan = CreateValidPlan();
            plan.Scenarios[0].Settings.TransmissionProbabilityReduction = 0;

            ExperimentValidationResult result = new ExperimentPlanValidator().Validate(plan);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("TransmissionProbabilityReduction"));
        }

        [Test]
        public void ValidatorRejectsInvalidLockdownHysteresisAndDurations()
        {
            ExperimentBatchPlan plan = CreateValidPlan();
            plan.Scenarios[0].Settings.CloseOfficeThresholdPercent = 20f;
            plan.Scenarios[0].Settings.ReopenOfficeThresholdPercent = 21f;
            plan.Scenarios[0].Settings.MinimumOfficeClosureDurationDays = float.NaN;
            plan.Scenarios[0].Settings.OfficeLockdownCooldownDurationDays = -1f;

            ExperimentValidationResult result = new ExperimentPlanValidator().Validate(plan);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("ReopenThresholdPercent <= CloseThresholdPercent"));
            Assert.That(result.Errors, Has.Some.Contains("MinimumClosureDurationDays"));
            Assert.That(result.Errors, Has.Some.Contains("CooldownDurationDays"));
        }

        private static ExperimentBatchPlan CreateValidPlan()
        {
            var configuration = new RealTimeConfig(true)
            {
                RatioIgnoreMasks = 30,
                RatioOtherProtectionMask = 35,
                RatioOwnProtectionMask = 35,
            };
            return new ExperimentBatchPlan
            {
                BatchId = "batch",
                BatchName = "Batch",
                CreatedUtc = "2026-01-01T00:00:00Z",
                ModVersion = "test-mod",
                GameVersion = "test-game",
                GitCommitSha = "0123456789012345678901234567890123456789",
                GitBranchOrTag = "Beta",
                OutputRoot = "output",
                SpeedMode = ExperimentSpeedMode.PreserveStartingSpeed,
                Baseline = new BaselineSaveIdentity
                {
                    AssetFullName = "package.metadata",
                    AssetChecksum = "metadata-checksum",
                    PackageName = "package",
                    DataAssetChecksum = "data-checksum",
                },
                Scenarios = new List<ExperimentScenario>
                {
                    new ExperimentScenario
                    {
                        ScenarioId = "scenario",
                        Name = "Scenario",
                        Settings = ExperimentScenarioSnapshot.Capture(configuration, false),
                    },
                },
            };
        }
    }
}
