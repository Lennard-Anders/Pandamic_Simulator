// <copyright file="LockdownEngine.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.Pandemic
{
    using System;
    using System.Collections.Generic;

    /// <summary>Immutable policy values for one automatic lockdown family.</summary>
    internal sealed class PandemicLockdownPolicy
    {
        public PandemicLockdownPolicy(
            double closeThresholdPercent,
            double reopenThresholdPercent,
            TimeSpan minimumClosureDuration,
            TimeSpan cooldownDuration)
        {
            if (!IsFinitePercent(closeThresholdPercent))
            {
                throw new ArgumentOutOfRangeException(nameof(closeThresholdPercent));
            }

            if (!IsFinitePercent(reopenThresholdPercent) || reopenThresholdPercent > closeThresholdPercent)
            {
                throw new ArgumentOutOfRangeException(nameof(reopenThresholdPercent));
            }

            if (minimumClosureDuration < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumClosureDuration));
            }

            if (cooldownDuration < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(cooldownDuration));
            }

            CloseThresholdPercent = closeThresholdPercent;
            ReopenThresholdPercent = reopenThresholdPercent;
            MinimumClosureDuration = minimumClosureDuration;
            CooldownDuration = cooldownDuration;
        }

        public double CloseThresholdPercent { get; }

        public double ReopenThresholdPercent { get; }

        public TimeSpan MinimumClosureDuration { get; }

        public TimeSpan CooldownDuration { get; }

        private static bool IsFinitePercent(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d && value <= 100d;
        }
    }

    /// <summary>One auditable automatic lockdown state transition.</summary>
    internal sealed class PandemicLockdownTransition
    {
        public PandemicLockdownFamily Family { get; set; }

        public DateTime SimulationTime { get; set; }

        public bool IsClosed { get; set; }

        public double MetricPercent { get; set; }
    }

    /// <summary>Read-only state exposed by the lockdown policy engine.</summary>
    internal sealed class PandemicLockdownStateSnapshot
    {
        public bool IsClosed { get; set; }

        public bool HasBeenEvaluated { get; set; }

        public DateTime? LastTransitionTime { get; set; }

        public double LastMetricPercent { get; set; }
    }

    /// <summary>
    /// Unity-independent state machine for automatic lockdown policies. The caller owns the
    /// epidemiological metric; this engine only applies threshold, minimum-duration and cooldown rules.
    /// </summary>
    internal sealed class LockdownEngine
    {
        private readonly Dictionary<PandemicLockdownFamily, FamilyState> states =
            new Dictionary<PandemicLockdownFamily, FamilyState>();
        private readonly List<PandemicLockdownTransition> transitions = new List<PandemicLockdownTransition>();

        public bool Evaluate(
            PandemicLockdownFamily family,
            PandemicLockdownPolicy policy,
            double metricPercent,
            DateTime simulationTime)
        {
            if (policy == null)
            {
                throw new ArgumentNullException(nameof(policy));
            }

            if (double.IsNaN(metricPercent)
                || double.IsInfinity(metricPercent)
                || metricPercent < 0d
                || metricPercent > 100d)
            {
                throw new ArgumentOutOfRangeException(nameof(metricPercent));
            }

            FamilyState state = GetOrCreateState(family);
            if (state.HasBeenEvaluated && simulationTime < state.LastEvaluationTime)
            {
                throw new InvalidOperationException("Lockdown policy time must be monotonic.");
            }

            bool transitionAllowed = !state.LastTransitionTime.HasValue
                || simulationTime - state.LastTransitionTime.Value >= policy.CooldownDuration;

            bool shouldTransition = false;
            if (!state.IsClosed)
            {
                shouldTransition = transitionAllowed
                    && ShouldClose(policy, metricPercent);
            }
            else
            {
                bool minimumClosureElapsed = state.LastTransitionTime.HasValue
                    && simulationTime - state.LastTransitionTime.Value >= policy.MinimumClosureDuration;
                shouldTransition = transitionAllowed
                    && minimumClosureElapsed
                    && ShouldReopen(state, policy, metricPercent);
            }

            if (shouldTransition)
            {
                state.IsClosed = !state.IsClosed;
                state.LastTransitionTime = simulationTime;
                transitions.Add(new PandemicLockdownTransition
                {
                    Family = family,
                    SimulationTime = simulationTime,
                    IsClosed = state.IsClosed,
                    MetricPercent = metricPercent,
                });
            }

            state.HasBeenEvaluated = true;
            state.LastEvaluationTime = simulationTime;
            state.LastMetricPercent = metricPercent;
            return state.IsClosed;
        }

        public bool IsClosed(PandemicLockdownFamily family)
        {
            return states.TryGetValue(family, out FamilyState state) && state.IsClosed;
        }

        public PandemicLockdownStateSnapshot GetState(PandemicLockdownFamily family)
        {
            FamilyState state = GetOrCreateState(family);
            return new PandemicLockdownStateSnapshot
            {
                IsClosed = state.IsClosed,
                HasBeenEvaluated = state.HasBeenEvaluated,
                LastTransitionTime = state.LastTransitionTime,
                LastMetricPercent = state.LastMetricPercent,
            };
        }

        public IList<PandemicLockdownTransition> GetTransitions()
        {
            return transitions.AsReadOnly();
        }

        public void Deactivate(DateTime simulationTime, double metricPercent)
        {
            foreach (KeyValuePair<PandemicLockdownFamily, FamilyState> pair in states)
            {
                if (!pair.Value.IsClosed)
                {
                    continue;
                }

                transitions.Add(new PandemicLockdownTransition
                {
                    Family = pair.Key,
                    SimulationTime = simulationTime,
                    IsClosed = false,
                    MetricPercent = metricPercent,
                });
            }

            states.Clear();
        }

        public void Reset()
        {
            states.Clear();
            transitions.Clear();
        }

        private static bool ShouldClose(PandemicLockdownPolicy policy, double metricPercent)
        {
            return metricPercent >= policy.CloseThresholdPercent;
        }

        private static bool ShouldReopen(FamilyState state, PandemicLockdownPolicy policy, double metricPercent)
        {
            return policy.CloseThresholdPercent == policy.ReopenThresholdPercent
                ? metricPercent < policy.ReopenThresholdPercent
                : metricPercent <= policy.ReopenThresholdPercent;
        }

        private FamilyState GetOrCreateState(PandemicLockdownFamily family)
        {
            if (!states.TryGetValue(family, out FamilyState state))
            {
                state = new FamilyState();
                states.Add(family, state);
            }

            return state;
        }

        private sealed class FamilyState
        {
            public bool IsClosed;
            public bool HasBeenEvaluated;
            public DateTime LastEvaluationTime;
            public DateTime? LastTransitionTime;
            public double LastMetricPercent;
        }
    }
}
