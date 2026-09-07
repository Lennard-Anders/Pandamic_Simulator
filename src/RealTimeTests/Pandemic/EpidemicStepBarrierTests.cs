namespace RealTimeTests.Pandemic
{
    using System;
    using NUnit.Framework;
    using RealTime.Pandemic;

    public sealed class EpidemicStepBarrierTests
    {
        [TestCase(1u)]
        [TestCase(5u)]
        [TestCase(60u)]
        public void SlowRendererCannotSkipStepsWithNativeMultiFrameTicks(uint stepMinutes)
        {
            var barrier = new EpidemicStepBarrier();
            var start = new DateTime(2030, 1, 1);
            DateTime game = start, last = start;
            bool paused = false;
            int steps = 0;
            barrier.Synchronize(true, last, stepMinutes, () => paused = false);
            // Native SimulationStep samples speed once, then completes all frames
            // before observing a pause on the next tick. Rendering time stays stale.
            DateTime staleRenderingTime = start;
            long longestFrame = EpidemicStepPacing.FrameTicks(6);
            for (int opportunity = 1; opportunity <= 500000 && last < start.AddDays(3); opportunity++)
            {
                if (!paused)
                {
                    int speed = EpidemicStepPacing.LimitFrames(9, stepMinutes, longestFrame);
                    long frameTicks = opportunity % 2 == 0 ? longestFrame : EpidemicStepPacing.FrameTicks(4);
                    for (int frame = 0; frame < speed; frame++) game = game.AddTicks(frameTicks);
                    barrier.CheckTick(game, () => paused = true);
                }
                if (opportunity % 100 != 0) continue;
                if (!barrier.TryAcquire(out DateTime observed)) continue;
                Assert.That(observed, Is.GreaterThan(staleRenderingTime));
                var decision = EpidemicStepScheduler.Evaluate(last, observed, stepMinutes, true);
                Assert.That(decision.Kind, Is.EqualTo(EpidemicStepDecisionKind.Ready));
                Assert.That(decision.StepTime - last, Is.EqualTo(TimeSpan.FromMinutes(stepMinutes)));
                last = decision.StepTime; steps++;
                barrier.Synchronize(true, last, stepMinutes, () => paused = false);
            }
            Assert.That(steps, Is.EqualTo(4320 / stepMinutes));
            Assert.That(last, Is.EqualTo(start.AddDays(3)));
            Assert.That(paused, Is.False);
        }

        [Test]
        public void PendingTickSurvivesUntilAcquiredAndCannotBeConsumedTwice()
        {
            var barrier = new EpidemicStepBarrier();
            var start = new DateTime(2030, 1, 1);
            barrier.Synchronize(true, start, 5, () => { });
            Assert.That(barrier.TryAcquire(out DateTime unused), Is.False);
            barrier.CheckTick(start.AddMinutes(6), () => { });
            Assert.That(barrier.TryAcquire(out DateTime observed), Is.True);
            Assert.That(observed, Is.EqualTo(start.AddMinutes(6)));
            Assert.That(barrier.TryAcquire(out unused), Is.False);
            barrier.Synchronize(true, start.AddMinutes(5), 5, () => { });
            Assert.That(barrier.TryAcquire(out unused), Is.False);
        }

        [Test]
        public void DisableReleasesOnlyOwnedPauseAndDoesNotPauseAnotherFrame()
        {
            var barrier = new EpidemicStepBarrier();
            var start = new DateTime(2030, 1, 1);
            int pauses = 0, releases = 0;
            barrier.Synchronize(true, start, 5, () => releases++);
            Assert.That(releases, Is.Zero);
            Assert.That(barrier.CheckTick(start.AddMinutes(5), () => pauses++), Is.True);
            Assert.That(barrier.CheckTick(start.AddMinutes(6), () => pauses++), Is.False);
            barrier.Synchronize(false, start, 5, () => releases++);
            Assert.That(barrier.CheckTick(start.AddMinutes(15), () => pauses++), Is.False);
            Assert.That(pauses, Is.EqualTo(1));
            Assert.That(releases, Is.EqualTo(1));
        }

        [Test]
        public void UnexpectedClockJumpStillPausesForIntegrityFailure()
        {
            var barrier = new EpidemicStepBarrier();
            var start = new DateTime(2030, 1, 1);
            barrier.Synchronize(true, start, 5, () => { });
            Assert.That(barrier.CheckTick(start.AddMinutes(20), () => { }), Is.True);
            Assert.That(EpidemicStepScheduler.Evaluate(start, start.AddMinutes(20), 5, true).Kind,
                Is.EqualTo(EpidemicStepDecisionKind.IntegrityViolation));
        }
    }
}
