// <copyright file="EpidemicStepScheduler.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.Pandemic
{
    using System;

    internal enum EpidemicStepDecisionKind
    {
        NotDue,
        Ready,
        IntegrityViolation,
    }

    internal sealed class EpidemicStepDecision
    {
        public EpidemicStepDecisionKind Kind { get; set; }

        public DateTime StepTime { get; set; }

        public string ErrorCode { get; set; }
    }

    /// <summary>Schedules exact simulation-time epidemiological steps without replaying one contact snapshot.</summary>
    internal static class EpidemicStepScheduler
    {
        public static EpidemicStepDecision Evaluate(
            DateTime lastStepTime,
            DateTime observedTime,
            uint stepMinutes,
            bool strict)
        {
            if (stepMinutes == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(stepMinutes));
            }

            if (observedTime < lastStepTime)
            {
                return new EpidemicStepDecision
                {
                    Kind = EpidemicStepDecisionKind.IntegrityViolation,
                    ErrorCode = "SimulationTimeMovedBackwards",
                };
            }

            TimeSpan step = TimeSpan.FromMinutes(stepMinutes);
            TimeSpan elapsed = observedTime - lastStepTime;
            if (elapsed < step)
            {
                return new EpidemicStepDecision { Kind = EpidemicStepDecisionKind.NotDue };
            }

            if (elapsed >= TimeSpan.FromTicks(checked(step.Ticks * 2L)))
            {
                return new EpidemicStepDecision
                {
                    Kind = EpidemicStepDecisionKind.IntegrityViolation,
                    ErrorCode = strict
                        ? "SimulationStepIntegrityViolation"
                        : "InteractiveSimulationStepResynchronization",
                };
            }

            return new EpidemicStepDecision
            {
                Kind = EpidemicStepDecisionKind.Ready,
                StepTime = lastStepTime.Add(step),
            };
        }
    }
}
