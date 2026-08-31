namespace RealTimeTests.Pandemic
{
    using System;
    using System.Collections.Generic;
    using RealTime.Experiments;
    using RealTime.Pandemic;
    using NUnit.Framework;

    public sealed class DeterministicEventStreamTests
    {
        [Test]
        public void SameInputsConfigurationAndMasterSeedReplayIdenticalTransmissionStream()
        {
            ExperimentSeedSet seeds = new FnvExperimentSeedProvider().DeriveSeeds(7123);
            var exposures = new List<TransmissionExposure<string>>
            {
                Exposure(11u, 101u, 0.31d, "work"),
                Exposure(12u, 101u, 0.27d, "work"),
                Exposure(13u, 102u, 0.81d, "home"),
                Exposure(14u, 103u, 1d, "transit"),
            };
            var engine = new TransmissionEngine();

            IList<ResolvedTransmission<string>> first = engine.Resolve(exposures, new Random(seeds.Transmission));
            IList<ResolvedTransmission<string>> second = engine.Resolve(exposures, new Random(seeds.Transmission));

            Assert.That(second.Count, Is.EqualTo(first.Count));
            for (int i = 0; i < first.Count; ++i)
            {
                Assert.That(second[i].SourceCitizenId, Is.EqualTo(first[i].SourceCitizenId));
                Assert.That(second[i].TargetCitizenId, Is.EqualTo(first[i].TargetCitizenId));
                Assert.That(second[i].CombinedProbability, Is.EqualTo(first[i].CombinedProbability));
                Assert.That(second[i].Context, Is.EqualTo(first[i].Context));
            }
        }

        private static TransmissionExposure<string> Exposure(uint source, uint target, double probability, string context)
        {
            return new TransmissionExposure<string>
            {
                SourceCitizenId = source,
                TargetCitizenId = target,
                Probability = probability,
                StableContextKey = context.GetHashCode(),
                Context = context,
            };
        }
    }
}
