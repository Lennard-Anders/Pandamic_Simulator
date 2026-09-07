// <copyright file="ExperimentSeedProviderTests.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTimeTests.Experiments
{
    using System;
    using RealTime.Experiments;
    using NUnit.Framework;

    public sealed class ExperimentSeedProviderTests
    {
        [Test]
        public void DeriveSeedsMatchesAuditedVector()
        {
            ExperimentSeedSet seeds = new FnvExperimentSeedProvider().DeriveSeeds(42);

            Assert.That(seeds.Master, Is.EqualTo(42));
            Assert.That(seeds.InitialPopulation, Is.EqualTo(298721136));
            Assert.That(seeds.DiseaseProgression, Is.EqualTo(2085446224));
            Assert.That(seeds.Transmission, Is.EqualTo(797134664));
            Assert.That(seeds.Symptom, Is.EqualTo(1375536039));
            Assert.That(seeds.Mortality, Is.EqualTo(1859217673));
            Assert.That(seeds.Pandemic, Is.EqualTo(1915926915));
            Assert.That(seeds.Mask, Is.EqualTo(1429821284));
            Assert.That(seeds.Testing, Is.EqualTo(525376056));
            Assert.That(seeds.ContactTracing, Is.EqualTo(1553439791));
            Assert.That(seeds.Intervention, Is.EqualTo(1678075125));
            Assert.That(seeds.Contact, Is.EqualTo(687098664));
            Assert.That(seeds.Test, Is.EqualTo(1090736272));

            Assert.That(new[]
            {
                seeds.InitialPopulation,
                seeds.DiseaseProgression,
                seeds.Transmission,
                seeds.Symptom,
                seeds.Mortality,
                seeds.Mask,
                seeds.Testing,
                seeds.ContactTracing,
                seeds.Intervention,
            }, Is.Unique);
        }

        [Test]
        public void DeriveSeedsRejectsNegativeMasterSeed()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new FnvExperimentSeedProvider().DeriveSeeds(-1));
        }

        [Test]
        public void SequentialMasterSeedsAdvanceByRunIndex()
        {
            ExperimentScenario scenario = new ExperimentScenario { FirstSeed = 7, RunCount = 3, SeedStrategy = ExperimentSeedStrategy.Sequential };
            FnvExperimentSeedProvider provider = new FnvExperimentSeedProvider();

            Assert.That(provider.GetMasterSeed(scenario, 0), Is.EqualTo(7));
            Assert.That(provider.GetMasterSeed(scenario, 2), Is.EqualTo(9));
        }

        [Test]
        public void ScenarioDefaultsAreSafeAndReproducible()
        {
            ExperimentScenario scenario = new ExperimentScenario();

            Assert.That(scenario.DurationDays, Is.EqualTo(30d));
            Assert.That(scenario.EndMode, Is.EqualTo(ExperimentEndMode.FixedDuration));
            Assert.That(scenario.SeedStrategy, Is.EqualTo(ExperimentSeedStrategy.Fixed));
            Assert.That(scenario.RunCount, Is.EqualTo(2));
            var plan = new ExperimentBatchPlan { Scenarios = { scenario } };
            Assert.That(plan.SpeedMode, Is.EqualTo(ExperimentSpeedMode.Speed3));
            Assert.That(plan.PairedSeedMode, Is.False);
            var provider = new FnvExperimentSeedProvider();
            Assert.That(provider.GetMasterSeed(scenario, 0), Is.EqualTo(1));
            Assert.That(provider.GetMasterSeed(scenario, 1), Is.EqualTo(1));
            Assert.That(scenario.FirstSeed, Is.EqualTo(1));
        }
    }
}
