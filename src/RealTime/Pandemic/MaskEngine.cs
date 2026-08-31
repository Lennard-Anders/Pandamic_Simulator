// <copyright file="MaskEngine.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.Pandemic
{
    using System;
    using System.Collections.Generic;
    using RealTime.Config;
    using RealTime.Experiments;

    internal enum MaskProtectionType
    {
        None,
        SourceControl,
        PersonalProtection,
    }

    internal enum MaskTransmissionEnvironment
    {
        Outdoor,
        Indoor,
        Vehicle,
        ResidentialSharedArea,
    }

    internal sealed class MaskPolicy
    {
        public MaskBehavior Behavior { get; set; }
        public double TransmissionReductionFactor { get; set; }
        public double IgnorePercent { get; set; }
        public double SourceControlPercent { get; set; }
        public double PersonalProtectionPercent { get; set; }
        public double IndoorProbabilityPerHour { get; set; }
        public double OutdoorProbabilityPerHour { get; set; }
        public double ResidentialSharedAreaMultiplier { get; set; }

        public void Validate()
        {
            double sum = IgnorePercent + SourceControlPercent + PersonalProtectionPercent;
            if (!Enum.IsDefined(typeof(MaskBehavior), Behavior)
                || !FinitePositive(TransmissionReductionFactor)
                || !Percentage(IgnorePercent)
                || !Percentage(SourceControlPercent)
                || !Percentage(PersonalProtectionPercent)
                || Math.Abs(sum - 100d) > 0.0001d
                || !Probability(IndoorProbabilityPerHour)
                || !Probability(OutdoorProbabilityPerHour)
                || !FiniteNonnegative(ResidentialSharedAreaMultiplier))
            {
                throw new ArgumentOutOfRangeException(nameof(MaskPolicy));
            }
        }

        private static bool Percentage(double value) => FiniteNonnegative(value) && value <= 100d;
        private static bool Probability(double value) => FiniteNonnegative(value) && value <= 1d;
        private static bool FinitePositive(double value) => FiniteNonnegative(value) && value > 0d;
        private static bool FiniteNonnegative(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d;
    }

    /// <summary>Unity-independent stable mask assignment and transmission-hazard modifier.</summary>
    internal sealed class MaskEngine
    {
        private readonly Dictionary<uint, MaskProtectionType> overrides = new Dictionary<uint, MaskProtectionType>();
        private MaskPolicy policy;
        private int masterSeed;
        private double stepLengthHours;

        public void Reset(MaskPolicy newPolicy, int newMasterSeed)
        {
            if (newPolicy == null)
            {
                throw new ArgumentNullException(nameof(newPolicy));
            }

            if (newMasterSeed < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(newMasterSeed));
            }

            newPolicy.Validate();
            policy = newPolicy;
            masterSeed = newMasterSeed;
            stepLengthHours = 0d;
            overrides.Clear();
        }

        public void ResetStableTraits(int newMasterSeed)
        {
            if (newMasterSeed < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(newMasterSeed));
            }

            masterSeed = newMasterSeed;
            overrides.Clear();
        }

        public void SetStepLengthHours(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            stepLengthHours = value;
        }

        public MaskProtectionType GetAssignment(uint citizenId)
        {
            EnsureReady();
            if (overrides.TryGetValue(citizenId, out MaskProtectionType value))
            {
                return value;
            }

            double assignment = DeterministicCitizenTraitAssigner.GetUnitInterval(masterSeed, citizenId, "mask-assignment");
            double ignoreBoundary = policy.IgnorePercent / 100d;
            double sourceBoundary = ignoreBoundary + (policy.SourceControlPercent / 100d);
            if (assignment < ignoreBoundary)
            {
                return MaskProtectionType.None;
            }

            return assignment < sourceBoundary
                ? MaskProtectionType.SourceControl
                : MaskProtectionType.PersonalProtection;
        }

        public void SetOverride(uint citizenId, bool masked)
        {
            overrides[citizenId] = masked ? MaskProtectionType.SourceControl : MaskProtectionType.None;
        }

        public double GetTransmissionProbability(
            uint sourceCitizenId,
            uint targetCitizenId,
            MaskTransmissionEnvironment environment)
        {
            EnsureReady();
            double baseProbability = GetHourlyProbability(environment);
            int modifierCount = IsMaskEffective(environment)
                ? GetModifierCount(GetAssignment(sourceCitizenId), GetAssignment(targetCitizenId))
                : 0;
            double adjustedHourlyProbability = baseProbability
                / Math.Pow(policy.TransmissionReductionFactor, modifierCount);

            return ConvertHourlyProbabilityToStep(adjustedHourlyProbability);
        }

        /// <summary>
        /// Returns the configured probability for the current step without applying any
        /// citizen-specific mask assignment. This is the authoritative unprotected baseline.
        /// </summary>
        public double GetUnprotectedTransmissionProbability(MaskTransmissionEnvironment environment)
        {
            EnsureReady();
            return ConvertHourlyProbabilityToStep(GetHourlyProbability(environment));
        }

        private double GetHourlyProbability(MaskTransmissionEnvironment environment)
        {
            double value = environment == MaskTransmissionEnvironment.Outdoor
                ? policy.OutdoorProbabilityPerHour
                : policy.IndoorProbabilityPerHour;
            if (environment == MaskTransmissionEnvironment.ResidentialSharedArea)
            {
                value *= policy.ResidentialSharedAreaMultiplier;
            }

            return Math.Max(0d, Math.Min(1d, value));
        }

        private double ConvertHourlyProbabilityToStep(double hourlyProbability)
        {
            if (stepLengthHours <= 0d || hourlyProbability <= 0d)
            {
                return 0d;
            }

            if (hourlyProbability >= 1d)
            {
                return 1d;
            }

            return 1d - Math.Exp(Math.Log(1d - hourlyProbability) * stepLengthHours);
        }

        private static int GetModifierCount(MaskProtectionType source, MaskProtectionType target)
        {
            int count = source == MaskProtectionType.SourceControl ? 1 : 0;
            if (target == MaskProtectionType.PersonalProtection)
            {
                count++;
            }

            return count;
        }

        private bool IsMaskEffective(MaskTransmissionEnvironment environment)
        {
            return policy.Behavior == MaskBehavior.Full
                || (policy.Behavior == MaskBehavior.Vehicle && environment != MaskTransmissionEnvironment.Outdoor)
                || (policy.Behavior == MaskBehavior.Building
                    && (environment == MaskTransmissionEnvironment.Indoor
                        || environment == MaskTransmissionEnvironment.ResidentialSharedArea));
        }

        private void EnsureReady()
        {
            if (policy == null)
            {
                throw new InvalidOperationException("The mask engine has not been initialized.");
            }
        }
    }
}
