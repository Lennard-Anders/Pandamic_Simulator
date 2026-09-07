namespace RealTime.Pandemic
{
    using System;

    internal static class EpidemicStepPacing
    {
        internal static long FrameTicks(uint timeSpeed)
        {
            if (timeSpeed < 1 || timeSpeed > 6) throw new ArgumentOutOfRangeException(nameof(timeSpeed));
            return 24L * 3600L * 400_000_000L / (1u << (int)(23 - timeSpeed));
        }
        // Cities samples its final speed once per tick and may execute nine frames
        // before observing a new pause. Keep that whole tick shorter than one step.
        internal static int LimitFrames(int requestedFrames, uint stepMinutes, long frameTicks)
        {
            if (requestedFrames <= 0 || stepMinutes == 0) return requestedFrames;
            if (frameTicks <= 0) throw new ArgumentOutOfRangeException(nameof(frameTicks));
            long maximum = (TimeSpan.FromMinutes(stepMinutes).Ticks - 1) / frameTicks;
            if (maximum < 1) throw new InvalidOperationException("One game frame is too long for the configured epidemic step.");
            return (int)Math.Min(requestedFrames, maximum);
        }
    }
}
