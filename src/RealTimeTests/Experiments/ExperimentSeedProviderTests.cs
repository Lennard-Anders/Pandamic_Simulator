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
            Assert.That(seeds.Pandemic, Is.EqualTo(1915926915));
            Assert.That(seeds.Mask, Is.EqualTo(1429821284));
            Assert.That(seeds.Contact, Is.EqualTo(687098664));
            Assert.That(seeds.Test, Is.EqualTo(1090736272));
        }

        [Test]
        public void DeriveSeedsRejectsNegativeMasterSeed()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new FnvExperimentSeedProvider().DeriveSeeds(-1));
        }

        [Test]
        public void SequentialMasterSeedsAdvanceByRunIndex()
        {
            ExperimentScenario scenario = new ExperimentScenario { FirstSeed = 7, RunCount = 3 };
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
            Assert.That(scenario.SeedStrategy, Is.EqualTo(ExperimentSeedStrategy.Sequential));
            Assert.That(scenario.FirstSeed, Is.EqualTo(1));
        }
    }
}
