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
            Assert.That(mapped.PandemicManagerSeed, Is.EqualTo(1915926915));
            Assert.That(mapped.MaskManagerSeed, Is.EqualTo(1429821284));
            Assert.That(mapped.ContactManagerSeed, Is.EqualTo(687098664));
            Assert.That(mapped.TestManagerSeed, Is.EqualTo(1090736272));
        }

        [Test]
        public void ComponentSeedAdapterPreservesExplicitFieldMapping()
        {
            var source = new ExperimentSeedSet
            {
                Master = 1,
                Pandemic = 2,
                Mask = 3,
                Test = 4,
                Contact = 5,
            };

            PandemicComponentSeeds mapped = PandemicComponentSeeds.FromExperimentSeedSet(source);

            Assert.That(mapped.MasterSeed, Is.EqualTo(1));
            Assert.That(mapped.PandemicManagerSeed, Is.EqualTo(2));
            Assert.That(mapped.MaskManagerSeed, Is.EqualTo(3));
            Assert.That(mapped.TestManagerSeed, Is.EqualTo(4));
            Assert.That(mapped.ContactManagerSeed, Is.EqualTo(5));
        }
    }
}
