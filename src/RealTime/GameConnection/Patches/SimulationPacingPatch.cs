namespace RealTime.GameConnection.Patches
{
    using System.Reflection;
    using RealTime.Pandemic;
    using SkyTools.Patching;

    internal static class SimulationPacingPatch
    {
        internal static volatile uint StepMinutes;
        internal static volatile uint MaximumTimeSpeed = 6;
        internal static volatile bool StepPending;
        internal static IPatch FinalSpeed { get; } = new FinalSimulationSpeedPatch();

        internal static int EffectiveFrames(int requestedFrames, uint stepMinutes, uint maximumTimeSpeed, long frameTicks, bool stepPending)
        {
            if (stepMinutes != 0 && stepPending) return 0;
            long longestFrame = EpidemicStepPacing.FrameTicks(maximumTimeSpeed);
            return EpidemicStepPacing.LimitFrames(requestedFrames, stepMinutes, System.Math.Max(longestFrame, frameTicks));
        }

        private sealed class FinalSimulationSpeedPatch : PatchBase
        {
            protected override MethodInfo GetMethod() => typeof(SimulationManager).GetProperty("FinalSimulationSpeed").GetGetMethod();

            private static void Postfix(SimulationManager __instance, ref int __result)
            {
                // Gate native ticks without changing the game's public pause flags.
                __result = EffectiveFrames(__result, StepMinutes, MaximumTimeSpeed, __instance.m_timePerFrame.Ticks, StepPending);
            }
        }
    }
}
