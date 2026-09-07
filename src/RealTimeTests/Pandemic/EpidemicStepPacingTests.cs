namespace RealTimeTests.Pandemic
{
    using System;
    using NUnit.Framework;
    using RealTime.Pandemic;

    public sealed class EpidemicStepPacingTests
    {
        [Test]
        public void PendingStepHoldsTicksAndReleaseRestoresRequestedSpeedWithoutPauseState()
        {
            long frame = EpidemicStepPacing.FrameTicks(6);
            Assert.That(RealTime.GameConnection.Patches.SimulationPacingPatch.EffectiveFrames(9, 5, 6, frame, true), Is.Zero);
            Assert.That(RealTime.GameConnection.Patches.SimulationPacingPatch.EffectiveFrames(9, 5, 6, frame, false), Is.EqualTo(9));
            Assert.That(RealTime.GameConnection.Patches.SimulationPacingPatch.EffectiveFrames(0, 5, 6, frame, false), Is.Zero);
            Assert.That(RealTime.GameConnection.Patches.SimulationPacingPatch.EffectiveFrames(9, 0, 6, frame, true), Is.EqualTo(9));
        }
        [Test]
        public void EverySupportedStepAndDayNightSpeedFitsWholeNativeTickBeforeNextStep()
        {
            for (uint minutes = 1; minutes <= 60; minutes++)
                for (uint day = 1; day <= 6; day++)
                    for (uint night = 1; night <= 6; night++)
                        foreach (int requested in new[] { 1, 3, 4, 9 })
                        {
                            long longest = Math.Max(EpidemicStepPacing.FrameTicks(day), EpidemicStepPacing.FrameTicks(night));
                            int limited = EpidemicStepPacing.LimitFrames(requested, minutes, longest);
                            Assert.That(limited, Is.InRange(1, requested));
                            Assert.That(limited * longest, Is.LessThan(TimeSpan.FromMinutes(minutes).Ticks));
                            if (requested * longest < TimeSpan.FromMinutes(minutes).Ticks)
                                Assert.That(limited, Is.EqualTo(requested));
                        }
        }

        [Test]
        public void PausedAndInactiveSimulationSpeedIsUnchanged()
        {
            Assert.That(EpidemicStepPacing.LimitFrames(0, 1, 100), Is.Zero);
            Assert.That(EpidemicStepPacing.LimitFrames(9, 0, 100), Is.EqualTo(9));
            Assert.Throws<InvalidOperationException>(() => EpidemicStepPacing.LimitFrames(9, 1, TimeSpan.FromMinutes(2).Ticks));
        }
    }
}
