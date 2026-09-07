// <copyright file="DeterministicCitizenTraitAssigner.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.Experiments
{
    using System;
    using System.Text;

    /// <summary>
    /// Assigns persistent citizen traits without consuming a stateful random stream. A trait is a
    /// pure function of master seed, citizen ID, and feature namespace, so discovery order and
    /// unrelated scenario behavior cannot change the assignment.
    /// </summary>
    internal static class DeterministicCitizenTraitAssigner
    {
        internal const string AlgorithmName = "TENUS-TRAIT-v1/FNV-1a-32";

        private const uint FnvOffsetBasis = 2166136261u;
        private const uint FnvPrime = 16777619u;
        private static readonly byte[] TraitNamespace = Encoding.ASCII.GetBytes("TENUS-TRAIT-v1\0");

        internal static double GetUnitInterval(int masterSeed, uint citizenId, string featureNamespace)
        {
            if (masterSeed < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(masterSeed));
            }

            if (string.IsNullOrEmpty(featureNamespace))
            {
                throw new ArgumentException("A stable feature namespace is required.", nameof(featureNamespace));
            }

            return GetUnitInterval(masterSeed, citizenId, Encoding.ASCII.GetBytes(featureNamespace));
        }

        // Reuse the exact v1 namespace bytes in repeated sampling; no hash or RNG change.
        internal static double GetUnitInterval(int masterSeed, uint citizenId, byte[] feature)
        {
            if (masterSeed < 0) throw new ArgumentOutOfRangeException(nameof(masterSeed));
            if (feature == null || feature.Length == 0) throw new ArgumentException("A stable feature namespace is required.", nameof(feature));
            uint hash = FnvOffsetBasis;
            unchecked
            {
                for (int i = 0; i < TraitNamespace.Length; ++i)
                {
                    hash = Add(hash, TraitNamespace[i]);
                }

                hash = AddInt32(hash, masterSeed);
                hash = AddUInt32(hash, citizenId);
                for (int i = 0; i < feature.Length; ++i)
                {
                    hash = Add(hash, feature[i]);
                }
            }

            return hash / 4294967296d;
        }

        internal static bool IsAssigned(int masterSeed, uint citizenId, string featureNamespace, double percent)
        {
            if (double.IsNaN(percent) || double.IsInfinity(percent) || percent < 0d || percent > 100d)
            {
                throw new ArgumentOutOfRangeException(nameof(percent));
            }

            return GetUnitInterval(masterSeed, citizenId, featureNamespace) < percent / 100d;
        }

        private static uint AddInt32(uint hash, int value)
        {
            return AddUInt32(hash, unchecked((uint)value));
        }

        private static uint AddUInt32(uint hash, uint value)
        {
            hash = Add(hash, (byte)value);
            hash = Add(hash, (byte)(value >> 8));
            hash = Add(hash, (byte)(value >> 16));
            return Add(hash, (byte)(value >> 24));
        }

        private static uint Add(uint hash, byte value)
        {
            return unchecked((hash ^ value) * FnvPrime);
        }
    }
}
