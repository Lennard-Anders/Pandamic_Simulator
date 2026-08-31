namespace RealTimeTests.Pandemic
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;
    using RealTime.Config;
    using RealTime.Pandemic;

    [TestFixture]
    public sealed class InitialSeedSamplerTests
    {
        [Test]
        public void UniformSamplingUsesTheWholeEligiblePopulationAndIsOrderIndependent()
        {
            List<InitialSeedCandidate> candidates = Candidates();
            var sampler = new InitialSeedSampler();
            IList<uint> first = sampler.Select(candidates, candidates.Count, PandemicInitialSeedSamplingStrategy.UniformPopulation, new Random(9));
            IList<uint> second = sampler.Select(candidates.AsEnumerable().Reverse(), candidates.Count, PandemicInitialSeedSamplingStrategy.UniformPopulation, new Random(9));

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first, Does.Contain(2u), "Uniform sampling must not exclude citizens merely because they are away from home.");
        }

        [Test]
        public void ResidentialOnlyExplicitlyExcludesAwayCitizens()
        {
            IList<uint> selected = new InitialSeedSampler().Select(
                Candidates(),
                10,
                PandemicInitialSeedSamplingStrategy.ResidentialOnly,
                new Random(1));

            Assert.That(selected, Is.EquivalentTo(new[] { 1u, 3u, 5u }));
        }

        [Test]
        public void StratifiedSamplingPreservesEmpiricalStrataWithoutDuplicates()
        {
            IList<uint> selected = new InitialSeedSampler().Select(
                Candidates(),
                4,
                PandemicInitialSeedSamplingStrategy.AgeStratified,
                new Random(3));

            Assert.That(selected.Distinct().Count(), Is.EqualTo(4));
            Assert.That(selected.Count(id => id <= 2u), Is.EqualTo(1));
            Assert.That(selected.Count(id => id > 2u), Is.EqualTo(3));
        }

        private static List<InitialSeedCandidate> Candidates()
        {
            return new List<InitialSeedCandidate>
            {
                new InitialSeedCandidate { CitizenId = 1u, IsAtResidence = true, AgeStratum = 0, DistrictStratum = 1 },
                new InitialSeedCandidate { CitizenId = 2u, IsAtResidence = false, AgeStratum = 0, DistrictStratum = 1 },
                new InitialSeedCandidate { CitizenId = 3u, IsAtResidence = true, AgeStratum = 1, DistrictStratum = 2 },
                new InitialSeedCandidate { CitizenId = 4u, IsAtResidence = false, AgeStratum = 1, DistrictStratum = 2 },
                new InitialSeedCandidate { CitizenId = 5u, IsAtResidence = true, AgeStratum = 1, DistrictStratum = 2 },
                new InitialSeedCandidate { CitizenId = 6u, IsAtResidence = false, AgeStratum = 1, DistrictStratum = 2 },
            };
        }
    }
}
