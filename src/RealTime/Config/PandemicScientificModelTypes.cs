// <copyright file="PandemicScientificModelTypes.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.Config
{
    /// <summary>Supported distributions for individual disease-timeline values.</summary>
    public enum PandemicDistributionType
    {
        Deterministic,
        Normal,
        LogNormal,
        Gamma,
    }

    /// <summary>Shape applied to transmission hazard across a citizen's infectious interval.</summary>
    public enum PandemicInfectiousnessProfileType
    {
        Flat,
        PiecewiseLinear,
    }

    /// <summary>Explicit population pool/stratification used to choose initial cases.</summary>
    public enum PandemicInitialSeedSamplingStrategy
    {
        UniformPopulation,
        ResidentialOnly,
        AgeStratified,
        DistrictStratified,
    }

    /// <summary>Whether every seed has the legacy fixed infection age or a configured draw.</summary>
    public enum PandemicInitialInfectionAgeMode
    {
        FixedInitialInfectionAge,
        DistributedInitialInfectionAge,
    }
}
