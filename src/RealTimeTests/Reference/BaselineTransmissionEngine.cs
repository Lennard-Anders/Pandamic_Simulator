namespace RealTimeTests.Reference
{
    using System;
    using System.Collections.Generic;
    using RealTime.Pandemic;
    /// <summary>Order-independent competing-hazard transmission resolution.</summary>
    internal sealed class BaselineTransmissionEngine
    {
        public IList<ResolvedTransmission<TContext>> Resolve<TContext>(
            IEnumerable<TransmissionExposure<TContext>> exposures,
            Random random)
        {
            if (exposures == null)
            {
                throw new ArgumentNullException(nameof(exposures));
            }

            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            var byTarget = new SortedDictionary<uint, List<TransmissionExposure<TContext>>>();
            foreach (TransmissionExposure<TContext> exposure in exposures)
            {
                ValidateExposure(exposure);
                if (exposure.Probability <= 0d)
                {
                    continue;
                }

                if (!byTarget.TryGetValue(exposure.TargetCitizenId, out List<TransmissionExposure<TContext>> targetExposures))
                {
                    targetExposures = new List<TransmissionExposure<TContext>>();
                    byTarget.Add(exposure.TargetCitizenId, targetExposures);
                }

                targetExposures.Add(exposure);
            }

            var resolved = new List<ResolvedTransmission<TContext>>();
            foreach (KeyValuePair<uint, List<TransmissionExposure<TContext>>> target in byTarget)
            {
                target.Value.Sort(CompareExposures);
                double survivalProbability = 1d;
                for (int i = 0; i < target.Value.Count; i++)
                {
                    survivalProbability *= 1d - target.Value[i].Probability;
                }

                double combinedProbability = 1d - survivalProbability;
                if (random.NextDouble() >= combinedProbability)
                {
                    continue;
                }

                TransmissionExposure<TContext> source = SelectSource(target.Value, random);
                resolved.Add(new ResolvedTransmission<TContext>
                {
                    SourceCitizenId = source.SourceCitizenId,
                    TargetCitizenId = source.TargetCitizenId,
                    CombinedProbability = combinedProbability,
                    SourceProbability = source.Probability,
                    Context = source.Context,
                });
            }

            return resolved;
        }

        private static TransmissionExposure<TContext> SelectSource<TContext>(
            IList<TransmissionExposure<TContext>> exposures,
            Random random)
        {
            var certain = new List<TransmissionExposure<TContext>>();
            double totalHazard = 0d;
            for (int i = 0; i < exposures.Count; i++)
            {
                if (exposures[i].Probability >= 1d)
                {
                    certain.Add(exposures[i]);
                }
                else
                {
                    totalHazard += -Math.Log(1d - exposures[i].Probability);
                }
            }

            if (certain.Count > 0)
            {
                return certain[(int)(random.NextDouble() * certain.Count) % certain.Count];
            }

            double threshold = random.NextDouble() * totalHazard;
            double cumulative = 0d;
            for (int i = 0; i < exposures.Count; i++)
            {
                cumulative += -Math.Log(1d - exposures[i].Probability);
                if (threshold < cumulative)
                {
                    return exposures[i];
                }
            }

            return exposures[exposures.Count - 1];
        }

        private static void ValidateExposure<TContext>(TransmissionExposure<TContext> exposure)
        {
            if (exposure == null)
            {
                throw new ArgumentException("Transmission exposures cannot contain null entries.", nameof(exposure));
            }

            if (exposure.SourceCitizenId == 0
                || exposure.TargetCitizenId == 0
                || exposure.SourceCitizenId == exposure.TargetCitizenId)
            {
                throw new ArgumentException("Transmission exposure citizen identifiers are invalid.", nameof(exposure));
            }

            if (double.IsNaN(exposure.Probability)
                || double.IsInfinity(exposure.Probability)
                || exposure.Probability < 0d
                || exposure.Probability > 1d)
            {
                throw new ArgumentOutOfRangeException(nameof(exposure), "Transmission probability must be finite and between zero and one.");
            }
        }

        private static int CompareExposures<TContext>(TransmissionExposure<TContext> left, TransmissionExposure<TContext> right)
        {
            int source = left.SourceCitizenId.CompareTo(right.SourceCitizenId);
            return source != 0 ? source : left.StableContextKey.CompareTo(right.StableContextKey);
        }
    }
}
