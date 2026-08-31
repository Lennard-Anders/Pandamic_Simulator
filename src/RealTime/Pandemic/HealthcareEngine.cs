// <copyright file="HealthcareEngine.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.Pandemic
{
    using System;

    internal sealed class HealthcarePolicy
    {
        public double WarningThresholdPercent { get; set; }

        public double CriticalThresholdPercent { get; set; }

        public double WarningMortalityMultiplier { get; set; }

        public double CriticalMortalityMultiplier { get; set; }

        public void Validate()
        {
            if (!Percent(WarningThresholdPercent)
                || !Percent(CriticalThresholdPercent)
                || WarningThresholdPercent > CriticalThresholdPercent
                || !Nonnegative(WarningMortalityMultiplier)
                || !Nonnegative(CriticalMortalityMultiplier))
            {
                throw new ArgumentOutOfRangeException(nameof(HealthcarePolicy));
            }
        }

        private static bool Percent(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d && value <= 100d;
        }

        private static bool Nonnegative(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d;
        }
    }

    /// <summary>Pure healthcare-saturation effect on mortality hazard.</summary>
    internal sealed class HealthcareEngine
    {
        private HealthcarePolicy policy;

        public HealthcareEngine(HealthcarePolicy policy)
        {
            Reset(policy);
        }

        public void Reset(HealthcarePolicy newPolicy)
        {
            if (newPolicy == null)
            {
                throw new ArgumentNullException(nameof(newPolicy));
            }

            newPolicy.Validate();
            policy = newPolicy;
        }

        public double GetMortalityHazardMultiplier(double hospitalUsagePercent)
        {
            if (double.IsNaN(hospitalUsagePercent) || double.IsInfinity(hospitalUsagePercent))
            {
                throw new ArgumentOutOfRangeException(nameof(hospitalUsagePercent));
            }

            if (hospitalUsagePercent >= policy.CriticalThresholdPercent)
            {
                return policy.CriticalMortalityMultiplier;
            }

            if (hospitalUsagePercent >= policy.WarningThresholdPercent)
            {
                return policy.WarningMortalityMultiplier;
            }

            return 1d;
        }
    }
}
