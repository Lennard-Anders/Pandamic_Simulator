namespace RealTimeTests.Experiments
{
    using System.Linq;
    using NUnit.Framework;
    using RealTime.Config;
    using RealTime.Experiments;
    using RealTime.Pandemic;

    public sealed class ExperimentPresetTests
    {
        [TestCase(6)] [TestCase(8)]
        public void ClosurePresetsKeepEssentialServicesOpenBelowFullPrevalence(int index)
        {
            var baseline = ExperimentScenarioSnapshot.Capture(new RealTimeConfig(true), false).Normalize();
            baseline.CloseEssentialServicesThresholdPercent = 0;
            var settings = ExperimentPresetCatalog.Create(index, baseline).Settings;
            var policy = new PandemicLockdownPolicy(settings.CloseEssentialServicesThresholdPercent,
                settings.ReopenEssentialServicesThresholdPercent, System.TimeSpan.Zero, System.TimeSpan.Zero);
            var engine = new LockdownEngine();
            var start = new System.DateTime(2030, 1, 1);
            int step = 0;
            foreach (double prevalence in new[] { 0d, 3d, 5d, 99.9d })
                Assert.That(engine.Evaluate(PandemicLockdownFamily.EssentialServices, policy, prevalence, start.AddMinutes(step++)), Is.False);
            Assert.That(baseline.CloseEssentialServicesThresholdPercent, Is.Zero);
        }

        [TestCase(3)] [TestCase(7)] [TestCase(8)] [TestCase(9)]
        public void MaskPresetsUseExplicitDefaultWithoutRewritingBaseline(int index)
        {
            var baseline = ExperimentScenarioSnapshot.Capture(new RealTimeConfig(true), false).Normalize();
            baseline.TransmissionProbabilityReduction = 1;
            var scenario = ExperimentPresetCatalog.Create(index, baseline);
            var maskSettings = index == 9 ? scenario.InterventionSchedule.After : scenario.Settings;
            Assert.That(maskSettings.TransmissionProbabilityReduction, Is.EqualTo(2));
            Assert.That(baseline.TransmissionProbabilityReduction, Is.EqualTo(1));
            Assert.That(scenario.PresetVersion, Is.EqualTo(index == 8 ? 3 : 2));
            Assert.That(ExperimentPresetCatalog.Describe(index, baseline).Version, Is.EqualTo(scenario.PresetVersion));
            Assert.That(ExperimentPresetCatalog.Describe(index, baseline).LongDescription, Does.Contain("not a measured infection-risk reduction"));
            string hash = ExperimentConfigurationHasher.Compute(scenario);
            maskSettings.TransmissionProbabilityReduction = 1;
            Assert.That(ExperimentConfigurationHasher.Compute(scenario), Is.Not.EqualTo(hash));
        }

        [Test]
        public void SensitivityCopiesBaselineAndRecordsActualRoundedPerturbation()
        {
            var baseline = ExperimentPresetCatalog.Create(0, ExperimentScenarioSnapshot.Capture(new RealTimeConfig(true), false).Normalize());
            baseline.Settings.IndoorDiseaseTransmissionProbability = 4;
            var variants = ExperimentSensitivity.Create(baseline, "IndoorDiseaseTransmissionProbability", -0.2, 0.2);
            Assert.That(variants.Count, Is.EqualTo(3));
            Assert.That(variants[0].Settings.IndoorDiseaseTransmissionProbability, Is.EqualTo(3.2f));
            Assert.That(variants[1].Settings.IndoorDiseaseTransmissionProbability, Is.EqualTo(4f));
            Assert.That(variants[2].Settings.IndoorDiseaseTransmissionProbability, Is.EqualTo(4.8f));
            Assert.That(baseline.Settings.IndoorDiseaseTransmissionProbability, Is.EqualTo(4f));
            Assert.That(variants.Select(s => s.FirstSeed), Is.All.EqualTo(baseline.FirstSeed));
            Assert.That(variants.Select(s => s.RunCount), Is.All.EqualTo(baseline.RunCount));
            Assert.That(variants[0].Sensitivity.Parameter, Is.EqualTo("IndoorDiseaseTransmissionProbability"));
            Assert.That(variants[0].Sensitivity.AppliedValue, Is.EqualTo((double)variants[0].Settings.IndoorDiseaseTransmissionProbability));
            baseline.CustomizedAfterPreset = true;
            Assert.That(ExperimentSensitivity.Create(baseline, "IndoorDiseaseTransmissionProbability", -0.2, 0.2)[1].CustomizedAfterPreset, Is.True);
        }

        [Test]
        public void PresetScheduleAndSensitivityMetadataSurvivePersistence()
        {
            var scenario = ExperimentPresetCatalog.Create(9, ExperimentScenarioSnapshot.Capture(new RealTimeConfig(true), false).Normalize());
            var serializer = new ExperimentJsonSerializer();
            var restored = serializer.Deserialize<ExperimentScenario>(serializer.Serialize(scenario));
            Assert.That(restored.PresetId, Is.EqualTo(scenario.PresetId));
            Assert.That(restored.InterventionSchedule.ActivationDay, Is.EqualTo(7));
            Assert.That(ExperimentConfigurationHasher.Compute(restored), Is.EqualTo(ExperimentConfigurationHasher.Compute(scenario)));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        [TestCase(5)] [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)]
        public void PresetsAreValidDetachedAndDescribeChanges(int index)
        {
            var baseline = ExperimentScenarioSnapshot.Capture(new RealTimeConfig(true), false).Normalize();
            var original = baseline.Clone();
            var description = ExperimentPresetCatalog.Describe(index, baseline);
            var scenario = ExperimentPresetCatalog.Create(index, baseline);
            Assert.That(baseline.Diff(original), Is.Empty);
            Assert.That(ExperimentPlanValidator.ValidateSettings(scenario.Settings).Errors, Is.Empty);
            Assert.That(description.LongDescription, Does.Contain("not validated"));
            Assert.That(scenario.PresetId, Is.EqualTo(description.PresetId));
            Assert.That(scenario.Settings.DiseaseDuration, Is.EqualTo(baseline.DiseaseDuration));
            Assert.That(scenario.Settings.TestSensitivityPercent, Is.EqualTo(baseline.TestSensitivityPercent));
            scenario.Settings.SymptomProbability = 17;
            Assert.That(baseline.Diff(original), Is.Empty);
        }

        [Test]
        public void DelayedScheduleMatchesModerateAndIsHashed()
        {
            var baseline = ExperimentScenarioSnapshot.Capture(new RealTimeConfig(true), false).Normalize();
            var delayed = ExperimentPresetCatalog.Create(9, baseline);
            var moderate = ExperimentPresetCatalog.Create(7, baseline);
            Assert.That(delayed.Settings.Diff(delayed.InterventionSchedule.Before), Is.Empty);
            Assert.That(delayed.InterventionSchedule.After.Diff(moderate.Settings), Is.Empty);
            Assert.That(delayed.InterventionSchedule.ActivationDay, Is.EqualTo(7));
            string hash = ExperimentConfigurationHasher.Compute(delayed);
            delayed.InterventionSchedule.After.AppBasedContactTracingProbability = 61;
            Assert.That(ExperimentConfigurationHasher.Compute(delayed), Is.Not.EqualTo(hash));
        }

        [Test]
        public void ControlAndPerturbationsPreserveBiologyAndClampTransmission()
        {
            var baseline = ExperimentScenarioSnapshot.Capture(new RealTimeConfig(true), true).Normalize();
            baseline.IndoorDiseaseTransmissionProbability = 90;
            baseline.OutdoorDiseaseTransmissionProbability = 8;
            var high = ExperimentPresetCatalog.Create(2, baseline).Settings;
            Assert.That(high.IndoorDiseaseTransmissionProbability, Is.EqualTo(100));
            Assert.That(high.OutdoorDiseaseTransmissionProbability, Is.EqualTo(10));
            Assert.That(high.MaskBehavior, Is.EqualTo(MaskBehavior.None));
            Assert.That(high.RelativeTestCapacity, Is.Zero);
            Assert.That(high.InitialLockdownEnabled, Is.False);
        }
    }
}
