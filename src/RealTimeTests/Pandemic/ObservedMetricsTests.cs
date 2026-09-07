namespace RealTimeTests.Pandemic
{
    using System;
    using NUnit.Framework;
    using RealTime.Pandemic;

    public sealed class ObservedMetricsTests
    {
        [Test]
        public void PublishedFalsePositiveIsObservedButDoesNotBecomeTrueInfection()
        {
            var start = new DateTime(2030, 1, 1);
            var states = new DiseaseStateEngine();
            states.RegisterCitizen(1);
            var tests = new TestingEngine(new PandemicTestingPolicy
            {
                Population = 1, RelativeCapacityPercentPerSevenDays = 100,
                SpecificityPercent = 0, ResultDelayDays = 1, PositiveBlockingDays = 3,
                MaximumRequestToSampleDays = 10,
            }, new Random(1), start);
            tests.RequestTest(1, start, PandemicTestPriority.Routine, PandemicTestReason.Screening);
            tests.Advance(start, id => new PandemicTestSampleContext { DiseaseState = DiseaseState.Susceptible });
            var metrics = new MetricsEngine();
            Assert.That(metrics.Capture(states, start, tests).DetectedCasesTotal, Is.Zero);
            tests.Advance(start.AddDays(1), id => null);
            var observed = metrics.Capture(states, start.AddDays(1), tests);
            Assert.That(observed.DetectedActiveCases, Is.EqualTo(1));
            Assert.That(observed.DetectedCasesTotal, Is.EqualTo(1));
            Assert.That(observed.CumulativeInfections, Is.Zero);
            Assert.That(observed.UndetectedActiveInfections, Is.Zero);
            Assert.That(observed.CaseDetectionRatio, Is.Null);
            Assert.That(metrics.Capture(states, start.AddDays(4), tests).DetectedActiveCases, Is.Zero);
            Assert.That(tests.PositiveTestsTotal, Is.EqualTo(1));
        }
    }
}
