// <copyright file="InitialSeedSampler.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.Pandemic
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using RealTime.Config;

    internal sealed class InitialSeedCandidate
    {
        public uint CitizenId { get; set; }

        public bool IsAtResidence { get; set; }

        public int AgeStratum { get; set; }

        public int DistrictStratum { get; set; }
    }

    /// <summary>Order-independent seed selection with optional empirical stratification.</summary>
    internal sealed class InitialSeedSampler
    {
        public IList<uint> Select(
            IEnumerable<InitialSeedCandidate> candidates,
            int count,
            PandemicInitialSeedSamplingStrategy strategy,
            Random random)
        {
            if (candidates == null || random == null)
            {
                throw new ArgumentNullException(candidates == null ? nameof(candidates) : nameof(random));
            }

            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            if (!Enum.IsDefined(typeof(PandemicInitialSeedSamplingStrategy), strategy))
            {
                throw new ArgumentOutOfRangeException(nameof(strategy));
            }

            List<InitialSeedCandidate> eligible = candidates
                .Where(candidate => candidate != null && candidate.CitizenId != 0u)
                .GroupBy(candidate => candidate.CitizenId)
                .Select(group => group.First())
                .OrderBy(candidate => candidate.CitizenId)
                .ToList();
            if (strategy == PandemicInitialSeedSamplingStrategy.ResidentialOnly)
            {
                eligible = eligible.Where(candidate => candidate.IsAtResidence).ToList();
            }

            int selectedCount = Math.Min(count, eligible.Count);
            IList<uint> selected;
            switch (strategy)
            {
                case PandemicInitialSeedSamplingStrategy.AgeStratified:
                    selected = SelectStratified(eligible, selectedCount, candidate => candidate.AgeStratum, random);
                    break;
                case PandemicInitialSeedSamplingStrategy.DistrictStratified:
                    selected = SelectStratified(eligible, selectedCount, candidate => candidate.DistrictStratum, random);
                    break;
                default:
                    Shuffle(eligible, random);
                    selected = eligible.Take(selectedCount).Select(candidate => candidate.CitizenId).ToList();
                    break;
            }

            return selected.OrderBy(id => id).ToList();
        }

        private static IList<uint> SelectStratified(
            IList<InitialSeedCandidate> eligible,
            int count,
            Func<InitialSeedCandidate, int> stratum,
            Random random)
        {
            if (count == 0 || eligible.Count == 0)
            {
                return new List<uint>();
            }

            var groups = eligible
                .GroupBy(stratum)
                .OrderBy(group => group.Key)
                .Select(group => new Stratum
                {
                    Key = group.Key,
                    Candidates = group.OrderBy(candidate => candidate.CitizenId).ToList(),
                })
                .ToList();
            int assigned = 0;
            foreach (Stratum group in groups)
            {
                double exact = count * (group.Candidates.Count / (double)eligible.Count);
                group.Target = Math.Min(group.Candidates.Count, (int)Math.Floor(exact));
                group.Remainder = exact - group.Target;
                assigned += group.Target;
                Shuffle(group.Candidates, random);
            }

            foreach (Stratum group in groups
                .OrderByDescending(group => group.Remainder)
                .ThenBy(group => group.Key))
            {
                if (assigned >= count)
                {
                    break;
                }

                if (group.Target < group.Candidates.Count)
                {
                    group.Target++;
                    assigned++;
                }
            }

            // Capacity can remain only when multiple allocations hit small strata.
            while (assigned < count)
            {
                Stratum available = groups.FirstOrDefault(group => group.Target < group.Candidates.Count);
                if (available == null)
                {
                    break;
                }

                available.Target++;
                assigned++;
            }

            return groups
                .SelectMany(group => group.Candidates.Take(group.Target))
                .Select(candidate => candidate.CitizenId)
                .ToList();
        }

        private static void Shuffle<T>(IList<T> values, Random random)
        {
            for (int i = values.Count - 1; i > 0; --i)
            {
                int index = random.Next(i + 1);
                T value = values[index];
                values[index] = values[i];
                values[i] = value;
            }
        }

        private sealed class Stratum
        {
            public int Key;
            public List<InitialSeedCandidate> Candidates;
            public int Target;
            public double Remainder;
        }
    }
}
