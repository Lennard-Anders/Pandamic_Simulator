namespace RealTimeTests.Pandemic
{
    using System;
    using System.Collections.Generic;
    using NUnit.Framework;
    using RealTime.Config;
    using RealTime.Pandemic;

    [TestFixture]
    public sealed class ScientificSupportEngineTests
    {
        [Test]
        public void HealthcareThresholdsSelectConfiguredHazardMultipliers()
        {
            var engine = new HealthcareEngine(new HealthcarePolicy
            {
                WarningThresholdPercent = 70d,
                CriticalThresholdPercent = 90d,
                WarningMortalityMultiplier = 1.25d,
                CriticalMortalityMultiplier = 2.5d,
            });

            Assert.That(engine.GetMortalityHazardMultiplier(69.9d), Is.EqualTo(1d));
            Assert.That(engine.GetMortalityHazardMultiplier(70d), Is.EqualTo(1.25d));
            Assert.That(engine.GetMortalityHazardMultiplier(90d), Is.EqualTo(2.5d));
        }

        [Test]
        public void MetricsUseDisjointCompartmentsAndResolvedCfrDefinition()
        {
            DateTime start = new DateTime(2030, 1, 1);
            var state = new DiseaseStateEngine();
            state.RegisterCitizen(1u);
            state.RegisterCitizen(2u);
            state.TryExpose(new DiseaseCourse(1u, start, start.AddDays(1), start.AddDays(2), null, null, start.AddDays(3), false, DiseaseExposureKind.InitialSeed));

            EpidemicMetricsSnapshot metrics = new MetricsEngine().Capture(state, start.AddDays(1));
            Assert.That(metrics.TrackedPopulation, Is.EqualTo(2));
            Assert.That(metrics.CumulativeInfections, Is.EqualTo(1));
            Assert.That(metrics.AttackRate, Is.EqualTo(0.5d));
            Assert.That(metrics.InfectionPrevalence, Is.EqualTo(0.5d));
            Assert.That(metrics.ResolvedCaseFatalityRatio, Is.Null);
        }

        [Test]
        public void CalibrationLeavesUnavailableRtUnavailable()
        {
            var targets = new CalibrationTargetSet { Name = "Synthetic", Source = "Unit test" };
            targets.Targets.Add(new CalibrationTarget { Metric = CalibrationMetric.AttackRate, TargetValue = 0.5d, AbsoluteTolerance = 0.1d });
            targets.Targets.Add(new CalibrationTarget { Metric = CalibrationMetric.Rt, TargetValue = 1.2d, AbsoluteTolerance = 0.2d });
            CalibrationEvaluation result = new CalibrationEngine().Evaluate(
                targets,
                new Dictionary<CalibrationMetric, double?>
                {
                    { CalibrationMetric.AttackRate, 0.55d },
                    { CalibrationMetric.Rt, null },
                });

            Assert.That(result.Results[0].IsWithinTolerance, Is.True);
            Assert.That(result.Results[1].IsAvailable, Is.False);
            Assert.That(result.AllWithinTolerance, Is.False);
            Assert.That(result.WeightedMeanAbsoluteError, Is.EqualTo(0.05d).Within(1e-12));
        }

        [Test]
        public void MaskEngineSeparatesSourceAndWearerProtectionAndPreservesZero()
        {
            var engine = new MaskEngine();
            engine.Reset(new MaskPolicy
            {
                Behavior = MaskBehavior.Full,
                TransmissionReductionFactor = 2d,
                IgnorePercent = 0d,
                SourceControlPercent = 100d,
                PersonalProtectionPercent = 0d,
                IndoorProbabilityPerHour = 0.2d,
                OutdoorProbabilityPerHour = 0d,
                ResidentialSharedAreaMultiplier = 1d,
            }, 10);
            engine.SetStepLengthHours(1d);

            Assert.That(engine.GetTransmissionProbability(1u, 2u, MaskTransmissionEnvironment.Indoor), Is.EqualTo(0.1d).Within(1e-12));
            Assert.That(engine.GetUnprotectedTransmissionProbability(MaskTransmissionEnvironment.Indoor), Is.EqualTo(0.2d).Within(1e-12));
            Assert.That(engine.GetTransmissionProbability(1u, 2u, MaskTransmissionEnvironment.Outdoor), Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => new MaskEngine().Reset(new MaskPolicy
            {
                Behavior = MaskBehavior.Full,
                TransmissionReductionFactor = 2d,
                IgnorePercent = 30d,
                SourceControlPercent = 50d,
                PersonalProtectionPercent = 50d,
                IndoorProbabilityPerHour = 0.2d,
                OutdoorProbabilityPerHour = 0.1d,
                ResidentialSharedAreaMultiplier = 1d,
            }, 1));
        }

        [Test]
        public void HospitalizationTotalCountsAdmissionsButNotSeekingOrUnavailableCare()
        {
            DateTime start = new DateTime(2030, 1, 1);
            var state = new DiseaseStateEngine();
            for (uint citizenId = 1; citizenId <= 4; citizenId++)
            {
                state.RegisterCitizen(citizenId);
                state.TryExpose(new DiseaseCourse(
                    citizenId,
                    start,
                    start.AddDays(1),
                    start.AddDays(2),
                    null,
                    null,
                    start.AddDays(3),
                    false,
                    DiseaseExposureKind.InitialSeed));
            }

            Assert.That(state.TryGetCourse(1u, out DiseaseCourse seeking), Is.True);
            Assert.That(state.TryGetCourse(2u, out DiseaseCourse hospitalized), Is.True);
            Assert.That(state.TryGetCourse(3u, out DiseaseCourse unavailable), Is.True);
            Assert.That(state.TryGetCourse(4u, out DiseaseCourse discharged), Is.True);
            seeking.HospitalizationState = HospitalizationState.SeekingCare;
            hospitalized.HospitalizationState = HospitalizationState.Hospitalized;
            unavailable.HospitalizationState = HospitalizationState.CareUnavailable;
            discharged.HospitalizationState = HospitalizationState.Discharged;

            Assert.That(state.GetCumulativeHospitalizationCount(), Is.EqualTo(2));
        }

        [Test]
        public void PopulationLifecycleCategoriesAreExplicitAndRemovalDoesNotTransferState()
        {
            var engine = new PopulationLifecycleEngine();
            Assert.That(engine.ClassifyCurrent(true, false, false, false), Is.EqualTo(PandemicPopulationCategory.Tourist));
            Assert.That(engine.ClassifyCurrent(false, true, false, true), Is.EqualTo(PandemicPopulationCategory.Immigrant));
            Assert.That(engine.ClassifyCurrent(false, false, true, false), Is.EqualTo(PandemicPopulationCategory.Commuter));
            Assert.That(engine.ClassifyCurrent(false, false, false, true), Is.EqualTo(PandemicPopulationCategory.Resident));
            Assert.That(engine.ClassifyRemoval(PandemicPopulationCategory.Resident), Is.EqualTo(PandemicPopulationCategory.Emigrant));
            Assert.That(engine.ClassifyRemoval(PandemicPopulationCategory.Tourist), Is.EqualTo(PandemicPopulationCategory.Tourist));
        }
    }
}
