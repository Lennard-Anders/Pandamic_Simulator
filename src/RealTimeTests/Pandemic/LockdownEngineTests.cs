// <copyright file="LockdownEngineTests.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTimeTests.Pandemic
{
    using System;
    using RealTime.Pandemic;
    using NUnit.Framework;

    public sealed class LockdownEngineTests
    {
        private static readonly DateTime Start = new DateTime(2040, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        [Test]
        public void CloseThresholdClosesAndReopenThresholdControlsOpening()
        {
            var engine = new LockdownEngine();
            PandemicLockdownPolicy policy = Policy(20d, 10d, 0d, 0d);

            Assert.That(engine.Evaluate(PandemicLockdownFamily.Office, policy, 19.9d, Start), Is.False);
            Assert.That(engine.Evaluate(PandemicLockdownFamily.Office, policy, 20d, Start.AddMinutes(5)), Is.True);
            Assert.That(engine.Evaluate(PandemicLockdownFamily.Office, policy, 10.1d, Start.AddMinutes(10)), Is.True);
            Assert.That(engine.Evaluate(PandemicLockdownFamily.Office, policy, 10d, Start.AddMinutes(15)), Is.False);
        }

        [Test]
        public void MinimumClosureDurationPreventsEarlyReopening()
        {
            var engine = new LockdownEngine();
            PandemicLockdownPolicy policy = Policy(20d, 10d, 2d, 0d);

            Assert.That(engine.Evaluate(PandemicLockdownFamily.Commercial, policy, 25d, Start), Is.True);
            Assert.That(engine.Evaluate(PandemicLockdownFamily.Commercial, policy, 5d, Start.AddDays(1.99d)), Is.True);
            Assert.That(engine.Evaluate(PandemicLockdownFamily.Commercial, policy, 5d, Start.AddDays(2d)), Is.False);
        }

        [Test]
        public void CooldownAppliesAfterBothTransitionDirections()
        {
            var engine = new LockdownEngine();
            PandemicLockdownPolicy policy = Policy(20d, 10d, 0d, 1d);

            Assert.That(engine.Evaluate(PandemicLockdownFamily.Education, policy, 25d, Start), Is.True);
            Assert.That(engine.Evaluate(PandemicLockdownFamily.Education, policy, 5d, Start.AddHours(23)), Is.True);
            Assert.That(engine.Evaluate(PandemicLockdownFamily.Education, policy, 5d, Start.AddDays(1)), Is.False);
            Assert.That(engine.Evaluate(PandemicLockdownFamily.Education, policy, 25d, Start.AddDays(1.5d)), Is.False);
            Assert.That(engine.Evaluate(PandemicLockdownFamily.Education, policy, 25d, Start.AddDays(2d)), Is.True);
        }

        [Test]
        public void EqualThresholdDoesNotOscillateAtBoundary()
        {
            var engine = new LockdownEngine();
            PandemicLockdownPolicy policy = Policy(20d, 20d, 0d, 0d);

            Assert.That(engine.Evaluate(PandemicLockdownFamily.IndustryPlayerIndustry, policy, 19d, Start), Is.False);
            Assert.That(engine.Evaluate(PandemicLockdownFamily.IndustryPlayerIndustry, policy, 20d, Start.AddMinutes(5)), Is.True);
            Assert.That(engine.Evaluate(PandemicLockdownFamily.IndustryPlayerIndustry, policy, 20d, Start.AddMinutes(10)), Is.True);
            Assert.That(engine.Evaluate(PandemicLockdownFamily.IndustryPlayerIndustry, policy, 20d, Start.AddMinutes(15)), Is.True);
            Assert.That(engine.GetTransitions().Count, Is.EqualTo(1));
        }

        [Test]
        public void FamiliesKeepIndependentPolicyState()
        {
            var engine = new LockdownEngine();
            PandemicLockdownPolicy policy = Policy(20d, 10d, 0d, 0d);

            engine.Evaluate(PandemicLockdownFamily.Office, policy, 25d, Start);
            engine.Evaluate(PandemicLockdownFamily.Education, policy, 5d, Start);

            Assert.That(engine.IsClosed(PandemicLockdownFamily.Office), Is.True);
            Assert.That(engine.IsClosed(PandemicLockdownFamily.Education), Is.False);
        }

        [Test]
        public void DeactivationReopensWithoutLosingTransitionHistory()
        {
            var engine = new LockdownEngine();
            PandemicLockdownPolicy policy = Policy(20d, 10d, 0d, 0d);
            engine.Evaluate(PandemicLockdownFamily.Office, policy, 25d, Start);

            engine.Deactivate(Start.AddHours(1), 25d);

            Assert.That(engine.IsClosed(PandemicLockdownFamily.Office), Is.False);
            Assert.That(engine.GetTransitions().Count, Is.EqualTo(2));
            Assert.That(engine.GetTransitions()[1].IsClosed, Is.False);
        }

        [Test]
        public void InvalidPoliciesAndNonMonotonicTimeFailFast()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Policy(10d, 20d, 0d, 0d));
            Assert.Throws<ArgumentOutOfRangeException>(() => Policy(20d, 10d, -1d, 0d));

            var engine = new LockdownEngine();
            PandemicLockdownPolicy policy = Policy(20d, 10d, 0d, 0d);
            engine.Evaluate(PandemicLockdownFamily.Office, policy, 5d, Start);
            Assert.Throws<InvalidOperationException>(() =>
                engine.Evaluate(PandemicLockdownFamily.Office, policy, 5d, Start.AddTicks(-1)));
        }

        private static PandemicLockdownPolicy Policy(double close, double reopen, double minimumDays, double cooldownDays)
        {
            return new PandemicLockdownPolicy(
                close,
                reopen,
                TimeSpan.FromDays(minimumDays),
                TimeSpan.FromDays(cooldownDays));
        }
    }
}
