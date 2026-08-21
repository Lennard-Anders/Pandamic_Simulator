// <copyright file="ExperimentSeedProvider.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.Experiments
{
    using System;
    using System.Text;

    /// <summary>Provides deterministic master and subsystem seeds.</summary>
    public interface IExperimentSeedProvider
    {
        int GetMasterSeed(ExperimentScenario scenario, int runIndex);

        ExperimentSeedSet DeriveSeeds(int masterSeed);
    }

    /// <summary>All deterministic seeds assigned to one experiment run.</summary>
    public sealed class ExperimentSeedSet
    {
        public int Master { get; set; }

        public int Pandemic { get; set; }

        public int Mask { get; set; }

        public int Test { get; set; }

        public int Contact { get; set; }
    }

    /// <summary>FNV-1a based deterministic seed derivation.</summary>
    public sealed class FnvExperimentSeedProvider : IExperimentSeedProvider
    {
        private const uint FnvOffsetBasis = 2166136261u;
        private const uint FnvPrime = 16777619u;
        private static readonly byte[] SeedNamespace = Encoding.ASCII.GetBytes("TENUS-RNG-v1\0");

        public int GetMasterSeed(ExperimentScenario scenario, int runIndex)
        {
            if (scenario == null)
            {
                throw new ArgumentNullException("scenario");
            }

            if (runIndex < 0 || runIndex >= scenario.RunCount)
            {
                throw new ArgumentOutOfRangeException("runIndex");
            }

            if (scenario.FirstSeed < 0)
            {
                throw new ArgumentOutOfRangeException("scenario", "Experiment master seeds cannot be negative.");
            }

            if (scenario.SeedStrategy == ExperimentSeedStrategy.Fixed)
            {
                return scenario.FirstSeed;
            }

            return checked(scenario.FirstSeed + runIndex);
        }

        public ExperimentSeedSet DeriveSeeds(int masterSeed)
        {
            if (masterSeed < 0)
            {
                throw new ArgumentOutOfRangeException("masterSeed", "Experiment master seeds cannot be negative.");
            }

            return new ExperimentSeedSet
            {
                Master = masterSeed,
                Pandemic = Derive(masterSeed, "pandemic"),
                Mask = Derive(masterSeed, "mask"),
                Contact = Derive(masterSeed, "contact"),
                Test = Derive(masterSeed, "test"),
            };
        }

        private static int Derive(int masterSeed, string component)
        {
            uint hash = FnvOffsetBasis;
            unchecked
            {
                for (int i = 0; i < SeedNamespace.Length; ++i)
                {
                    hash = Add(hash, SeedNamespace[i]);
                }

                hash = Add(hash, (byte)masterSeed);
                hash = Add(hash, (byte)(masterSeed >> 8));
                hash = Add(hash, (byte)(masterSeed >> 16));
                hash = Add(hash, (byte)(masterSeed >> 24));

                byte[] label = Encoding.ASCII.GetBytes(component);
                for (int i = 0; i < label.Length; ++i)
                {
                    hash = Add(hash, label[i]);
                }
            }

            return unchecked((int)(hash & 0x7fffffffu));
        }

        private static uint Add(uint hash, byte value)
        {
            return unchecked((hash ^ value) * FnvPrime);
        }
    }
}
