// <copyright file="PandemicScientificModelFactory.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.Pandemic
{
    using System;
    using RealTime.Config;

    /// <summary>Maps the primitive persisted configuration into unit-bearing core policies.</summary>
    internal static class PandemicScientificModelFactory
    {
        public static DiseaseTimelinePolicy CreateDiseaseTimelinePolicy(RealTimeConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            return new DiseaseTimelinePolicy
            {
                ExposedDurationDays = Distribution(
                    config.ExposedDurationDistributionType,
                    config.ExposedDurationMeanDays,
                    config.ExposedDurationStandardDeviationDays,
                    config.ExposedDurationMinimumDays,
                    config.ExposedDurationMaximumDays,
                    config.ExposedDurationFixedDays),
                InfectiousStartDays = Distribution(
                    config.InfectiousStartDistributionType,
                    config.InfectiousStartMeanDays,
                    config.InfectiousStartStandardDeviationDays,
                    config.InfectiousStartMinimumDays,
                    config.InfectiousStartMaximumDays,
                    config.InfectiousStartFixedDays),
                InfectiousEndDays = Distribution(
                    config.InfectiousEndDistributionType,
                    config.InfectiousEndMeanDays,
                    config.InfectiousEndStandardDeviationDays,
                    config.InfectiousEndMinimumDays,
                    config.InfectiousEndMaximumDays,
                    config.InfectiousEndFixedDays),
                SymptomStartDays = Distribution(
                    config.SymptomStartDistributionType,
                    config.SymptomStartMeanDays,
                    config.SymptomStartStandardDeviationDays,
                    config.SymptomStartMinimumDays,
                    config.SymptomStartMaximumDays,
                    config.SymptomStartFixedDays),
                SymptomEndDays = Distribution(
                    config.SymptomEndDistributionType,
                    config.SymptomEndMeanDays,
                    config.SymptomEndStandardDeviationDays,
                    config.SymptomEndMinimumDays,
                    config.SymptomEndMaximumDays,
                    config.SymptomEndFixedDays),
                RecoveryDays = Distribution(
                    config.RecoveryDistributionType,
                    config.RecoveryMeanDays,
                    config.RecoveryStandardDeviationDays,
                    config.RecoveryMinimumDays,
                    config.RecoveryMaximumDays,
                    config.RecoveryFixedDays),
            };
        }

        public static InfectiousnessProfilePolicy CreateInfectiousnessPolicy(RealTimeConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            return new InfectiousnessProfilePolicy
            {
                Type = config.InfectiousnessProfileType,
                StartMultiplier = config.InfectiousnessProfileStartMultiplier,
                PeakTimeFraction = config.InfectiousnessProfilePeakTimeFraction,
                PeakMultiplier = config.InfectiousnessProfilePeakMultiplier,
                EndMultiplier = config.InfectiousnessProfileEndMultiplier,
            };
        }

        public static DistributionSpec CreateInitialInfectionAgeDistribution(RealTimeConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            return Distribution(
                config.InitialInfectionAgeDistributionType,
                config.InitialInfectionAgeMeanDays,
                config.InitialInfectionAgeStandardDeviationDays,
                config.InitialInfectionAgeMinimumDays,
                config.InitialInfectionAgeMaximumDays,
                config.InitialInfectionAgeFixedDays);
        }

        private static DistributionSpec Distribution(
            PandemicDistributionType type,
            double mean,
            double standardDeviation,
            double minimum,
            double maximum,
            double fixedValue)
        {
            return new DistributionSpec
            {
                Type = type,
                Mean = mean,
                StandardDeviation = standardDeviation,
                Minimum = minimum,
                Maximum = maximum,
                FixedValue = fixedValue,
            };
        }
    }
}
