namespace RealTimeTests.Pandemic
{
    using System;
    using NUnit.Framework;
    using RealTime.Simulation;

    public sealed class TimeAdjustmentTests
    {
        [Test]
        public void EveryBaselineReloadRestoresSaveTimeDespitePreviousRunClock()
        {
            var baseline = new DateTime(2021, 10, 23, 20, 58, 21);
            var clock = new DateTime(2026, 9, 6);
            for (int run = 0; run < 4; run++)
            {
                clock = TimeAdjustment.RebaseTime(clock, baseline);
                Assert.That(clock, Is.EqualTo(baseline));
                clock = clock.AddDays(3);
            }
        }

        [Test]
        public void DayNightAndPauseRebasesPreserveLiveTimeWithoutLoadedSnapshot()
        {
            var clock = new DateTime(2021, 10, 24, 6, 3, 21);
            for (int transition = 0; transition < 20; transition++)
            {
                Assert.That(TimeAdjustment.RebaseTime(clock, null), Is.EqualTo(clock));
                clock = clock.AddHours(12);
            }
        }
    }
}
