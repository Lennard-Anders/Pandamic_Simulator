namespace RealTimeTests.Pandemic
{
    using System;
    using NUnit.Framework;
    using RealTime.Config;
    using RealTime.Pandemic;

    [TestFixture]
    public sealed class DiseaseProgressionEngineTests
    {
        private static readonly DateTime Start = new DateTime(2035, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        [Test]
        public void DeterministicPolicyPreservesConfiguredLegacyTimeline()
        {
            var engine = new DiseaseProgressionEngine(CreatePolicy(PandemicDistributionType.Deterministic), new Random(1));
            DiseaseCourse course = engine.CreateCourse(1u, Start, true, DiseaseExposureKind.SecondaryTransmission);

            Assert.That(course.InfectiousStartTime, Is.EqualTo(Start.AddDays(1)));
            Assert.That(course.InfectiousEndTime, Is.EqualTo(Start.AddDays(4)));
            Assert.That(course.SymptomStartTime, Is.EqualTo(Start.AddDays(2)));
            Assert.That(course.SymptomEndTime, Is.EqualTo(Start.AddDays(5)));
            Assert.That(course.RecoveryTime, Is.EqualTo(Start.AddDays(7)));
        }

        [TestCase(PandemicDistributionType.Normal)]
        [TestCase(PandemicDistributionType.LogNormal)]
        [TestCase(PandemicDistributionType.Gamma)]
        public void StochasticTimelinesAreBoundedLogicalAndReproducible(PandemicDistributionType type)
        {
            DiseaseTimelinePolicy policy = CreatePolicy(type);
            var first = new DiseaseProgressionEngine(policy, new Random(123));
            var second = new DiseaseProgressionEngine(policy, new Random(123));

            for (uint citizenId = 1; citizenId <= 25; citizenId++)
            {
                DiseaseCourse a = first.CreateCourse(citizenId, Start, true, DiseaseExposureKind.SecondaryTransmission);
                DiseaseCourse b = second.CreateCourse(citizenId, Start, true, DiseaseExposureKind.SecondaryTransmission);
                Assert.That(a.InfectiousStartTime, Is.EqualTo(b.InfectiousStartTime));
                Assert.That(a.RecoveryTime, Is.EqualTo(b.RecoveryTime));
                Assert.That(a.InfectiousStartTime, Is.GreaterThanOrEqualTo(a.ExposureTime));
                Assert.That(a.InfectiousEndTime, Is.GreaterThan(a.InfectiousStartTime));
                Assert.That(a.RecoveryTime, Is.GreaterThanOrEqualTo(a.InfectiousEndTime));
                Assert.That(a.SymptomEndTime.Value, Is.GreaterThan(a.SymptomStartTime.Value));
                Assert.That(a.SymptomEndTime.Value, Is.LessThanOrEqualTo(a.RecoveryTime));
            }
        }

        [Test]
        public void InitialInfectionAgeIsClampedBeforeRecovery()
        {
            var engine = new DiseaseProgressionEngine(CreatePolicy(PandemicDistributionType.Deterministic), new Random(2));
            DiseaseCourse course = engine.CreateInitialCourse(
                1u,
                Start,
                100d,
                false,
                DiseaseExposureKind.InitialSeed,
                out double actualAge);

            Assert.That(actualAge, Is.LessThan(7d));
            Assert.That(course.RecoveryTime, Is.GreaterThan(Start));
        }

        [Test]
        public void PiecewiseProfileAndHazardScalingRespectBoundariesAndZero()
        {
            DiseaseCourse course = new DiseaseProgressionEngine(
                CreatePolicy(PandemicDistributionType.Deterministic),
                new Random(1)).CreateCourse(1u, Start, false, DiseaseExposureKind.InitialSeed);
            var policy = new InfectiousnessProfilePolicy
            {
                Type = PandemicInfectiousnessProfileType.PiecewiseLinear,
                StartMultiplier = 0d,
                PeakTimeFraction = 0.5d,
                PeakMultiplier = 2d,
                EndMultiplier = 0d,
            };

            Assert.That(InfectiousnessProfileEngine.GetMultiplier(course, Start.AddDays(1), policy), Is.Zero);
            Assert.That(InfectiousnessProfileEngine.GetMultiplier(course, Start.AddDays(2.5), policy), Is.EqualTo(2d).Within(1e-12));
            Assert.That(InfectiousnessProfileEngine.GetMultiplier(course, Start.AddDays(4), policy), Is.Zero);
            Assert.That(InfectiousnessProfileEngine.ApplyToProbability(0d, 100d), Is.Zero);
            Assert.That(InfectiousnessProfileEngine.ApplyToProbability(0.25d, 2d), Is.EqualTo(0.4375d).Within(1e-12));
        }

        [Test]
        public void MortalityProbabilityUsesHazardNotProbabilityMultiplication()
        {
            double probability = DiseaseProgressionEngine.CalculateStepMortalityProbability(
                0.2d,
                TimeSpan.FromDays(10),
                TimeSpan.FromDays(10),
                2d);

            Assert.That(probability, Is.EqualTo(0.36d).Within(1e-12));
            Assert.That(DiseaseProgressionEngine.CalculateStepMortalityProbability(0d, TimeSpan.FromDays(1), TimeSpan.FromHours(1), 2d), Is.Zero);
        }

        [Test]
        public void PathologicallySmallGammaSpreadFallsBackToFiniteMean()
        {
            var spec = new DistributionSpec
            {
                Type = PandemicDistributionType.Gamma,
                Mean = 5d,
                StandardDeviation = double.Epsilon,
                Minimum = 0d,
                Maximum = 10d,
                FixedValue = 5d,
            };

            Assert.That(DistributionSampler.Sample(spec, new Random(1)), Is.EqualTo(5d));
        }

        [Test]
        public void DiseaseTimelineRejectsNegativeBoundsAndMeanOutsideBounds()
        {
            DiseaseTimelinePolicy negative = CreatePolicy(PandemicDistributionType.Deterministic);
            negative.ExposedDurationDays.Minimum = -1d;
            Assert.Throws<ArgumentOutOfRangeException>(() => negative.Validate());

            DiseaseTimelinePolicy outside = CreatePolicy(PandemicDistributionType.Normal);
            outside.RecoveryDays.Mean = outside.RecoveryDays.Maximum + 1d;
            Assert.Throws<ArgumentOutOfRangeException>(() => outside.Validate());
        }

        internal static DiseaseTimelinePolicy CreatePolicy(PandemicDistributionType type)
        {
            return new DiseaseTimelinePolicy
            {
                ExposedDurationDays = Spec(type, 1d),
                InfectiousStartDays = Spec(type, 1d),
                InfectiousEndDays = Spec(type, 4d),
                SymptomStartDays = Spec(type, 2d),
                SymptomEndDays = Spec(type, 5d),
                RecoveryDays = Spec(type, 7d),
            };
        }

        private static DistributionSpec Spec(PandemicDistributionType type, double value)
        {
            return new DistributionSpec
            {
                Type = type,
                Mean = value,
                StandardDeviation = type == PandemicDistributionType.Deterministic ? 0d : value * 0.15d,
                Minimum = Math.Max(0d, value * 0.25d),
                Maximum = value * 2d,
                FixedValue = value,
            };
        }
    }
}
