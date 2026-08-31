// <copyright file="MetricsEngine.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.Pandemic
{
    using System;

    /// <summary>An immutable, internally consistent view of the epidemiological state.</summary>
    internal sealed class EpidemicMetricsSnapshot
    {
        public DiseaseStateCounts Counts { get; set; }

        public int Symptomatic { get; set; }

        public int InitialSeedCount { get; set; }

        public int SecondaryTransmissionCount { get; set; }

        public int HospitalizationsTotal { get; set; }

        public int TrackedPopulation { get; set; }

        public int CumulativeInfections { get; set; }

        public double AttackRate { get; set; }

        public double InfectionPrevalence { get; set; }

        public double? ResolvedCaseFatalityRatio { get; set; }
    }

    /// <summary>Computes epidemiological metrics without Unity or Cities: Skylines dependencies.</summary>
    internal sealed class MetricsEngine
    {
        public EpidemicMetricsSnapshot Capture(DiseaseStateEngine stateEngine, DateTime simulationTime)
        {
            if (stateEngine == null)
            {
                throw new ArgumentNullException(nameof(stateEngine));
            }

            DiseaseStateCounts counts = stateEngine.GetCounts(simulationTime);
            int tracked = counts.Susceptible + counts.Exposed + counts.Infectious
                + counts.PostInfectiousIll + counts.Recovered + counts.Dead;
            int cumulative = stateEngine.InitialSeedCount + stateEngine.SecondaryTransmissionCount;
            int unresolved = counts.Exposed + counts.Infectious + counts.PostInfectiousIll;
            int resolved = counts.Recovered + counts.Dead;
            return new EpidemicMetricsSnapshot
            {
                Counts = counts,
                Symptomatic = stateEngine.GetSymptomaticCount(simulationTime),
                InitialSeedCount = stateEngine.InitialSeedCount,
                SecondaryTransmissionCount = stateEngine.SecondaryTransmissionCount,
                HospitalizationsTotal = stateEngine.GetCumulativeHospitalizationCount(),
                TrackedPopulation = tracked,
                CumulativeInfections = cumulative,
                AttackRate = tracked == 0 ? 0d : cumulative / (double)tracked,
                InfectionPrevalence = tracked == 0 ? 0d : unresolved / (double)tracked,
                ResolvedCaseFatalityRatio = resolved == 0 ? (double?)null : counts.Dead / (double)resolved,
            };
        }
    }
}
