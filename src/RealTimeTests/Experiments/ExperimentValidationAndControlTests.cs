// <copyright file="ExperimentValidationAndControlTests.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTimeTests.Experiments
{
    using System;
    using System.Collections.Generic;
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
            overflow.Scenarios[0].FirstSeed = int.MaxValue;
            overflow.Scenarios[0].RunCount = 2;
            Assert.That(new ExperimentPlanValidator().Validate(overflow).IsValid, Is.False);
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

        private static ExperimentBatchPlan CreateValidPlan()
        {
            return new ExperimentBatchPlan
            {
                BatchId = "batch",
                BatchName = "Batch",
                CreatedUtc = "2026-01-01T00:00:00Z",
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
                    new ExperimentScenario { ScenarioId = "scenario", Name = "Scenario" },
                },
            };
        }
    }
}
