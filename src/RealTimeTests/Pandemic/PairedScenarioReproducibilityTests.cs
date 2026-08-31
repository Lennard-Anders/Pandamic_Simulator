namespace RealTimeTests.Pandemic
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;
    using RealTime.Config;
    using RealTime.Experiments;
    using RealTime.Pandemic;

    [TestFixture]
    public sealed class PairedScenarioReproducibilityTests
    {
        [Test]
        public void ScenarioIPairedSeedsAlignPopulationTraitsAndDiseaseCourseDraws()
        {
            ExperimentSeedSet seeds = new FnvExperimentSeedProvider().DeriveSeeds(8128);
            List<InitialSeedCandidate> candidates = Enumerable.Range(1, 20)
                .Select(value => new InitialSeedCandidate
                {
                    CitizenId = (uint)value,
                    IsAtResidence = (value & 1) == 0,
                    AgeStratum = value % 5,
                    DistrictStratum = value % 3,
                })
                .ToList();
            var sampler = new InitialSeedSampler();
            IList<uint> firstPopulation = sampler.Select(
                candidates,
                5,
                PandemicInitialSeedSamplingStrategy.UniformPopulation,
                new Random(seeds.InitialPopulation));
            IList<uint> secondPopulation = sampler.Select(
                candidates.AsEnumerable().Reverse(),
                5,
                PandemicInitialSeedSamplingStrategy.UniformPopulation,
                new Random(seeds.InitialPopulation));
            Assert.That(secondPopulation, Is.EqualTo(firstPopulation));

            MaskPolicy firstMaskPolicy = MaskPolicy(MaskBehavior.Full);
            MaskPolicy secondMaskPolicy = MaskPolicy(MaskBehavior.None);
            var firstMasks = new MaskEngine();
            var secondMasks = new MaskEngine();
            firstMasks.Reset(firstMaskPolicy, seeds.Master);
            secondMasks.Reset(secondMaskPolicy, seeds.Master);
            for (uint citizenId = 1; citizenId <= 20; citizenId++)
            {
                Assert.That(secondMasks.GetAssignment(citizenId), Is.EqualTo(firstMasks.GetAssignment(citizenId)));
            }

            var tracingPolicy = new ContactTracingPolicy
            {
                AppAdoptionPercent = 43d,
                ManualTraceabilityPercent = 61d,
            };
            var firstTracing = new ContactTracingEngine(tracingPolicy, seeds.Master);
            var secondTracing = new ContactTracingEngine(tracingPolicy, seeds.Master);
            for (uint citizenId = 1; citizenId <= 20; citizenId++)
            {
                Assert.That(secondTracing.UsesApp(citizenId), Is.EqualTo(firstTracing.UsesApp(citizenId)));
                Assert.That(secondTracing.IsManuallyTraceable(citizenId), Is.EqualTo(firstTracing.IsManuallyTraceable(citizenId)));
            }

            var firstProgression = new DiseaseProgressionEngine(
                DiseaseProgressionEngineTests.CreatePolicy(PandemicDistributionType.Gamma),
                new Random(seeds.DiseaseProgression));
            var secondProgression = new DiseaseProgressionEngine(
                DiseaseProgressionEngineTests.CreatePolicy(PandemicDistributionType.Gamma),
                new Random(seeds.DiseaseProgression));
            DateTime exposure = new DateTime(2040, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            foreach (uint citizenId in firstPopulation)
            {
                DiseaseCourse first = firstProgression.CreateCourse(
                    citizenId,
                    exposure,
                    true,
                    DiseaseExposureKind.InitialSeed);
                DiseaseCourse second = secondProgression.CreateCourse(
                    citizenId,
                    exposure,
                    true,
                    DiseaseExposureKind.InitialSeed);
                Assert.That(second.InfectiousStartTime, Is.EqualTo(first.InfectiousStartTime));
                Assert.That(second.InfectiousEndTime, Is.EqualTo(first.InfectiousEndTime));
                Assert.That(second.SymptomStartTime, Is.EqualTo(first.SymptomStartTime));
                Assert.That(second.SymptomEndTime, Is.EqualTo(first.SymptomEndTime));
                Assert.That(second.RecoveryTime, Is.EqualTo(first.RecoveryTime));
            }
        }

        private static MaskPolicy MaskPolicy(MaskBehavior behavior)
        {
            return new MaskPolicy
            {
                Behavior = behavior,
                TransmissionReductionFactor = 2d,
                IgnorePercent = 23d,
                SourceControlPercent = 39d,
                PersonalProtectionPercent = 38d,
                IndoorProbabilityPerHour = 0.1d,
                OutdoorProbabilityPerHour = 0.01d,
                ResidentialSharedAreaMultiplier = 1d / 96d,
            };
        }
    }
}
