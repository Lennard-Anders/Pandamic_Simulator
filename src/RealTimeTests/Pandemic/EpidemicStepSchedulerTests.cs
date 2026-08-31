namespace RealTimeTests.Pandemic
{
    using System;
    using NUnit.Framework;
    using RealTime.Pandemic;

    [TestFixture]
    public sealed class EpidemicStepSchedulerTests
    {
        private static readonly DateTime Start = new DateTime(2030, 1, 1);

        [Test]
        public void StepIsDueAtExactConfiguredBoundary()
        {
            EpidemicStepDecision decision = EpidemicStepScheduler.Evaluate(Start, Start.AddMinutes(5), 5, true);

            Assert.That(decision.Kind, Is.EqualTo(EpidemicStepDecisionKind.Ready));
            Assert.That(decision.StepTime, Is.EqualTo(Start.AddMinutes(5)));
        }

        [Test]
        public void SmallOvershootStillProducesExactStepTime()
        {
            EpidemicStepDecision decision = EpidemicStepScheduler.Evaluate(Start, Start.AddMinutes(7), 5, true);

            Assert.That(decision.Kind, Is.EqualTo(EpidemicStepDecisionKind.Ready));
            Assert.That(decision.StepTime, Is.EqualTo(Start.AddMinutes(5)));
        }

        [Test]
        public void MissingAWholeStepFailsStrictRun()
        {
            EpidemicStepDecision decision = EpidemicStepScheduler.Evaluate(Start, Start.AddMinutes(10), 5, true);

            Assert.That(decision.Kind, Is.EqualTo(EpidemicStepDecisionKind.IntegrityViolation));
            Assert.That(decision.ErrorCode, Is.EqualTo("SimulationStepIntegrityViolation"));
        }
    }
}
