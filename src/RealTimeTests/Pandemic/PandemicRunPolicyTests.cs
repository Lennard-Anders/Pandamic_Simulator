// <copyright file="PandemicRunPolicyTests.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTimeTests.Pandemic
{
    using System;
    using NUnit.Framework;
    using RealTime.Experiments;
    using RealTime.Pandemic;

    public sealed class PandemicRunPolicyTests
    {
        private static readonly DateTime RunStart = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

        [Test]
        public void LegacyManualPolicyRetainsThirtyDayDurationAndExtinctionBehavior()
        {
            PandemicRunPolicy policy = PandemicRunPolicy.CreateLegacyManual();

            Assert.That(policy.Duration, Is.EqualTo(TimeSpan.FromDays(30d)));
            Assert.That(policy.StopOnExtinction, Is.True);
            Assert.That(policy.CalculateTarget(RunStart), Is.EqualTo(RunStart.AddDays(30d)));
            Assert.That(
                policy.Evaluate(RunStart, RunStart.AddDays(1d), 0, 0, true),
                Is.EqualTo(PandemicCompletionReason.EpidemicExtinct));
        }

        [Test]
        public void DurationTakesPrecedenceWhenExtinctionAndTargetCoincide()
        {
            PandemicRunPolicy policy = PandemicRunPolicy.CreateLegacyManual();

            Assert.That(
                policy.Evaluate(RunStart, RunStart.AddDays(30d), 0, 0, true),
                Is.EqualTo(PandemicCompletionReason.DurationReached));
        }

        [Test]
        public void ExactAndOvershotTargetsFinishButEarlierTimeDoesNot()
        {
            PandemicRunPolicy policy = PandemicRunPolicy.CreateBatch(2.5d, false);
            DateTime target = RunStart.AddDays(2.5d);

            Assert.That(policy.CalculateTarget(RunStart), Is.EqualTo(target));
            Assert.That(
                policy.Evaluate(RunStart, target.AddTicks(-1L), 0, 0, true),
                Is.EqualTo(PandemicCompletionReason.None));
            Assert.That(
                policy.Evaluate(RunStart, target, 0, 0, true),
                Is.EqualTo(PandemicCompletionReason.DurationReached));
            Assert.That(
                policy.Evaluate(RunStart, target.AddTicks(1L), 0, 0, true),
                Is.EqualTo(PandemicCompletionReason.DurationReached));
        }

        [Test]
        public void FractionalDurationTicksRoundAwayFromZero()
        {
            double durationDays = 10.5d / TimeSpan.TicksPerDay;

            PandemicRunPolicy policy = PandemicRunPolicy.CreateBatch(durationDays, false);

            Assert.That(policy.Duration.Ticks, Is.EqualTo(11L));
        }

        [Test]
        public void FixedDurationSuppressesEarlyExtinction()
        {
            PandemicRunPolicy policy = PandemicRunPolicy.CreateBatch(1d, false);

            Assert.That(
                policy.Evaluate(RunStart, RunStart.AddHours(12d), 0, 0, true),
                Is.EqualTo(PandemicCompletionReason.None));
        }

        [Test]
        public void OptionalExtinctionRequiresPriorInfectionsAndNoActiveCases()
        {
            PandemicRunPolicy policy = PandemicRunPolicy.CreateBatch(1d, true);
            DateTime current = RunStart.AddHours(12d);

            Assert.That(
                policy.Evaluate(RunStart, current, 0, 0, false),
                Is.EqualTo(PandemicCompletionReason.None));
            Assert.That(
                policy.Evaluate(RunStart, current, 1, 0, true),
                Is.EqualTo(PandemicCompletionReason.None));
            Assert.That(
                policy.Evaluate(RunStart, current, 0, 1, true),
                Is.EqualTo(PandemicCompletionReason.None));
            Assert.That(
                policy.Evaluate(RunStart, current, 0, 0, true),
                Is.EqualTo(PandemicCompletionReason.EpidemicExtinct));
        }

        [Test]
        public void BatchDurationAllowsMaximumAndRejectsInvalidValues()
        {
            Assert.That(PandemicRunPolicy.CreateBatch(3650d, false).Duration, Is.EqualTo(TimeSpan.FromDays(3650d)));
            Assert.Throws<ArgumentOutOfRangeException>(() => PandemicRunPolicy.CreateBatch(3650.000001d, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => PandemicRunPolicy.CreateBatch(0d, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => PandemicRunPolicy.CreateBatch(-1d, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => PandemicRunPolicy.CreateBatch(double.NaN, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => PandemicRunPolicy.CreateBatch(double.PositiveInfinity, false));
        }

        [Test]
        public void ComponentSeedsMapTheAuditedProviderStreamsWithoutReDerivation()
        {
            ExperimentSeedSet derived = new FnvExperimentSeedProvider().DeriveSeeds(42);

            PandemicComponentSeeds mapped = PandemicComponentSeeds.FromExperimentSeedSet(derived);

            Assert.That(mapped.MasterSeed, Is.EqualTo(42));
            Assert.That(mapped.InitialPopulationSeed, Is.EqualTo(298721136));
            Assert.That(mapped.DiseaseProgressionSeed, Is.EqualTo(2085446224));
            Assert.That(mapped.TransmissionSeed, Is.EqualTo(797134664));
            Assert.That(mapped.SymptomSeed, Is.EqualTo(1375536039));
            Assert.That(mapped.MortalitySeed, Is.EqualTo(1859217673));
            Assert.That(mapped.MaskSeed, Is.EqualTo(1429821284));
            Assert.That(mapped.TestingSeed, Is.EqualTo(525376056));
            Assert.That(mapped.ContactTracingSeed, Is.EqualTo(1553439791));
            Assert.That(mapped.InterventionSeed, Is.EqualTo(1678075125));
        }

        [Test]
        public void ComponentSeedAdapterPreservesExplicitFieldMapping()
        {
            var source = new ExperimentSeedSet
            {
                Master = 1,
                InitialPopulation = 2,
                DiseaseProgression = 3,
                Transmission = 4,
                Symptom = 5,
                Mortality = 6,
                Mask = 7,
                Testing = 8,
                ContactTracing = 9,
                Intervention = 10,
            };

            PandemicComponentSeeds mapped = PandemicComponentSeeds.FromExperimentSeedSet(source);

            Assert.That(mapped.MasterSeed, Is.EqualTo(1));
            Assert.That(mapped.InitialPopulationSeed, Is.EqualTo(2));
            Assert.That(mapped.DiseaseProgressionSeed, Is.EqualTo(3));
            Assert.That(mapped.TransmissionSeed, Is.EqualTo(4));
            Assert.That(mapped.SymptomSeed, Is.EqualTo(5));
            Assert.That(mapped.MortalitySeed, Is.EqualTo(6));
            Assert.That(mapped.MaskSeed, Is.EqualTo(7));
            Assert.That(mapped.TestingSeed, Is.EqualTo(8));
            Assert.That(mapped.ContactTracingSeed, Is.EqualTo(9));
            Assert.That(mapped.InterventionSeed, Is.EqualTo(10));
        }
    }
}
