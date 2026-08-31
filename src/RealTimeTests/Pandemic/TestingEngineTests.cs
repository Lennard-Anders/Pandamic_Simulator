namespace RealTimeTests.Pandemic
{
    using System;
    using NUnit.Framework;
    using RealTime.Pandemic;

    [TestFixture]
    public sealed class TestingEngineTests
    {
        private static readonly DateTime Start = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        [Test]
        public void FuturePositiveResultCannotAffectBehavior()
        {
            TestingEngine engine = CreateEngine(quarantinePending: false, resultDelayDays: 1d);
            engine.RequestTest(1, Start, PandemicTestPriority.Symptomatic, PandemicTestReason.Symptoms);
            engine.Advance(Start, InfectedSince(Start.AddDays(-2)));

            PandemicTestRecord record = engine.GetLatestRecord(1);
            Assert.That(record.Result, Is.EqualTo(PandemicTestResult.Unknown));
            Assert.That(engine.IsPositiveResultAvailable(1, Start), Is.False);
            Assert.That(engine.ShouldBlockCitizen(1, Start), Is.False);

            engine.Advance(Start.AddDays(1), InfectedSince(Start.AddDays(-2)));
            Assert.That(engine.IsPositiveResultAvailable(1, Start.AddDays(1)), Is.True);
        }

        [Test]
        public void PendingQuarantineDoesNotDependOnFutureResult()
        {
            TestingEngine engine = CreateEngine(quarantinePending: true, resultDelayDays: 1d, specificity: 100d);
            engine.RequestTest(1, Start, PandemicTestPriority.Routine, PandemicTestReason.Screening);
            engine.Advance(Start, Susceptible);

            Assert.That(engine.GetLatestRecord(1).PendingResult, Is.EqualTo(PandemicTestResult.Negative));
            Assert.That(engine.ShouldBlockCitizen(1, Start), Is.True);
        }

        [Test]
        public void ResultAppearsExactlyAtAvailabilityTime()
        {
            TestingEngine engine = CreateEngine(quarantinePending: false, resultDelayDays: 1d);
            engine.RequestTest(1, Start, PandemicTestPriority.Symptomatic, PandemicTestReason.Symptoms);
            engine.Advance(Start, InfectedSince(Start.AddDays(-2)));

            engine.Advance(Start.AddDays(1).AddTicks(-1), InfectedSince(Start.AddDays(-2)));
            Assert.That(engine.GetLatestRecord(1).Result, Is.EqualTo(PandemicTestResult.Unknown));

            engine.Advance(Start.AddDays(1), InfectedSince(Start.AddDays(-2)));
            Assert.That(engine.GetLatestRecord(1).Result, Is.EqualTo(PandemicTestResult.Positive));
        }

        [Test]
        public void DetectionTimeIsRespected()
        {
            TestingEngine engine = CreateEngine(quarantinePending: false, resultDelayDays: 0d, detectionDays: 2d);
            engine.RequestTest(1, Start, PandemicTestPriority.Symptomatic, PandemicTestReason.Symptoms);
            engine.Advance(Start, InfectedSince(Start.AddDays(-1)));

            Assert.That(engine.GetLatestRecord(1).Result, Is.EqualTo(PandemicTestResult.Negative));
        }

        [Test]
        public void MaximumRequestDurationExpiresUnschedulableTest()
        {
            PandemicTestingPolicy policy = CreatePolicy();
            policy.RelativeCapacityPercentPerSevenDays = 0d;
            policy.MaximumRequestToSampleDays = 1d;
            var engine = new TestingEngine(policy, new Random(1), Start);
            engine.RequestTest(1, Start, PandemicTestPriority.Routine, PandemicTestReason.Screening);

            engine.Advance(Start.AddDays(1).AddTicks(1), Susceptible);

            Assert.That(engine.GetLatestRecord(1).State, Is.EqualTo(PandemicTestState.Expired));
        }

        [Test]
        public void RetestingWorksAfterConfiguredInterval()
        {
            TestingEngine engine = CreateEngine(quarantinePending: false, resultDelayDays: 0d);
            engine.RequestTest(1, Start, PandemicTestPriority.Routine, PandemicTestReason.Screening);
            engine.Advance(Start, Susceptible);

            Assert.That(engine.RequestTest(1, Start.AddDays(6), PandemicTestPriority.Routine, PandemicTestReason.Retest), Is.False);
            Assert.That(engine.RequestTest(1, Start.AddDays(7), PandemicTestPriority.Routine, PandemicTestReason.Retest), Is.True);
        }

        [Test]
        public void ExpiredPositiveDoesNotBlockForever()
        {
            TestingEngine engine = CreateEngine(quarantinePending: false, resultDelayDays: 0d, positiveBlockingDays: 3d);
            engine.RequestTest(1, Start, PandemicTestPriority.Symptomatic, PandemicTestReason.Symptoms);
            engine.Advance(Start, InfectedSince(Start.AddDays(-2)));

            Assert.That(engine.ShouldBlockCitizen(1, Start.AddDays(2)), Is.True);
            Assert.That(engine.ShouldBlockCitizen(1, Start.AddDays(3)), Is.False);
        }

        private static TestingEngine CreateEngine(
            bool quarantinePending,
            double resultDelayDays,
            double detectionDays = 0d,
            double specificity = 100d,
            double positiveBlockingDays = 14d)
        {
            PandemicTestingPolicy policy = CreatePolicy();
            policy.QuarantineWhileAwaitingResult = quarantinePending;
            policy.ResultDelayDays = resultDelayDays;
            policy.DetectionTimeDays = detectionDays;
            policy.SpecificityPercent = specificity;
            policy.PositiveBlockingDays = positiveBlockingDays;
            return new TestingEngine(policy, new Random(1), Start);
        }

        private static PandemicTestingPolicy CreatePolicy()
        {
            return new PandemicTestingPolicy
            {
                Population = 10,
                RelativeCapacityPercentPerSevenDays = 100d,
                ReservedForSymptomaticPercent = 50d,
                MaximumRequestToSampleDays = 7d,
                ResultDelayDays = 0d,
                DetectionTimeDays = 0d,
                SensitivityPercent = 100d,
                SpecificityPercent = 100d,
                RetestIntervalDays = 7d,
                PositiveBlockingDays = 14d,
                QuarantineWhileAwaitingResult = false,
            };
        }

        private static Func<uint, PandemicTestSampleContext> InfectedSince(DateTime exposureTime)
        {
            return id => new PandemicTestSampleContext
            {
                DiseaseState = DiseaseState.Infectious,
                ExposureTime = exposureTime,
            };
        }

        private static PandemicTestSampleContext Susceptible(uint citizenId)
        {
            return new PandemicTestSampleContext { DiseaseState = DiseaseState.Susceptible };
        }
    }
}
