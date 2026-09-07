// <copyright file="ContactSamplingEngine.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.Pandemic
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;
    using RealTime.Experiments;

    internal struct ContactPair
    {
        public uint CitizenA { get; set; }

        public uint CitizenB { get; set; }
    }

    /// <summary>
    /// Deterministic round-robin sampler. It bounds each participant to at most one contact per
    /// round and at most the configured number of rounds, avoiding an all-pairs materialization.
    /// </summary>
    internal sealed class ContactSamplingEngine
    {
        private readonly HashSet<uint> unique = new HashSet<uint>();
        private readonly List<ParticipantOrder> order = new List<ParticipantOrder>();
        private readonly List<uint> rotation = new List<uint>();

        public IList<ContactPair> SampleBounded(IEnumerable<uint> citizenIds, int maximumContactsPerPerson, int samplingSeed, long simulationStepKey, int contextKey)
        {
            using (PandemicProfiler.Measure("ContactSamplingEngine")) return SampleBoundedCore(citizenIds, maximumContactsPerPerson, samplingSeed, simulationStepKey, contextKey);
        }

        private IList<ContactPair> SampleBoundedCore(
            IEnumerable<uint> citizenIds,
            int maximumContactsPerPerson,
            int samplingSeed,
            long simulationStepKey,
            int contextKey)
        {
            if (citizenIds == null)
            {
                throw new ArgumentNullException(nameof(citizenIds));
            }

            if (maximumContactsPerPerson <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumContactsPerPerson));
            }

            if (samplingSeed < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(samplingSeed));
            }

            unique.Clear();
            foreach (uint citizenId in citizenIds)
            {
                if (citizenId == 0u)
                {
                    throw new ArgumentException("Contact participants must have nonzero IDs.", nameof(citizenIds));
                }

                unique.Add(citizenId);
            }

            if (unique.Count < 2)
            {
                return new List<ContactPair>();
            }

            string featureNamespace = string.Format(
                CultureInfo.InvariantCulture,
                "contact-sampling:{0}:{1}",
                simulationStepKey,
                contextKey);
            byte[] feature = Encoding.ASCII.GetBytes(featureNamespace);
            order.Clear();
            foreach (uint citizenId in unique)
            {
                order.Add(new ParticipantOrder
                {
                    CitizenId = citizenId,
                    Score = DeterministicCitizenTraitAssigner.GetUnitInterval(
                        samplingSeed,
                        citizenId,
                        feature),
                });
            }

            order.Sort(CompareParticipants);
            rotation.Clear();
            for (int i = 0; i < order.Count; ++i)
            {
                rotation.Add(order[i].CitizenId);
            }

            if ((rotation.Count & 1) != 0)
            {
                rotation.Add(0u);
            }

            int rounds = Math.Min(maximumContactsPerPerson, rotation.Count - 1);
            var pairs = new List<ContactPair>((rotation.Count / 2) * rounds);
            for (int round = 0; round < rounds; ++round)
            {
                int half = rotation.Count / 2;
                for (int i = 0; i < half; ++i)
                {
                    uint left = rotation[i];
                    uint right = rotation[rotation.Count - 1 - i];
                    if (left == 0u || right == 0u)
                    {
                        continue;
                    }

                    pairs.Add(new ContactPair
                    {
                        CitizenA = Math.Min(left, right),
                        CitizenB = Math.Max(left, right),
                    });
                }

                RotateKeepingFirst(rotation);
            }

            return pairs;
        }

        public IList<ContactPair> AllPairs(IEnumerable<uint> citizenIds)
        {
            if (citizenIds == null)
            {
                throw new ArgumentNullException(nameof(citizenIds));
            }

            var unique = new Dictionary<uint, bool>();
            foreach (uint citizenId in citizenIds)
            {
                unique[citizenId] = true;
            }

            var ordered = new List<uint>(unique.Keys);
            ordered.Sort();
            var pairs = new List<ContactPair>();
            for (int i = 0; i < ordered.Count; ++i)
            {
                if (ordered[i] == 0u)
                {
                    throw new ArgumentException("Contact participants must have nonzero IDs.", nameof(citizenIds));
                }

                for (int j = i + 1; j < ordered.Count; ++j)
                {
                    pairs.Add(new ContactPair { CitizenA = ordered[i], CitizenB = ordered[j] });
                }
            }

            return pairs;
        }

        private static int CompareParticipants(ParticipantOrder left, ParticipantOrder right)
        {
            int score = left.Score.CompareTo(right.Score);
            return score != 0 ? score : left.CitizenId.CompareTo(right.CitizenId);
        }

        private static void RotateKeepingFirst(IList<uint> values)
        {
            uint last = values[values.Count - 1];
            for (int i = values.Count - 1; i > 1; --i)
            {
                values[i] = values[i - 1];
            }

            values[1] = last;
        }

        private struct ParticipantOrder
        {
            public uint CitizenId { get; set; }

            public double Score { get; set; }
        }
    }
}
