namespace RealTimeTests.Experiments
{
    using RealTime.Config;
    using RealTime.Experiments;
    using NUnit.Framework;

    public sealed class ExperimentConfigurationHasherTests
    {
        [Test]
        public void LegacyIdentityOmitsNewContactFieldsButV2IncludesThem()
        {
            var scenario = CreateScenario();
            string legacy = ExperimentConfigurationHasher.ComputeLegacy(scenario);
            string current = ExperimentConfigurationHasher.Compute(scenario);
            scenario.Settings.ContactPersistenceMinutesWorkplace += 15;
            scenario.Settings.ScientificContactExportMode = ScientificContactExportMode.FullRaw;
            Assert.That(ExperimentConfigurationHasher.ComputeLegacy(scenario), Is.EqualTo(legacy));
            Assert.That(ExperimentConfigurationHasher.Compute(scenario), Is.Not.EqualTo(current));
            scenario.Settings.IndoorDiseaseTransmissionProbability += 1;
            Assert.That(ExperimentConfigurationHasher.ComputeLegacy(scenario), Is.Not.EqualTo(legacy));
        }
        [Test]
        public void HashIsStableAcrossDetachedCopiesAndPresentationChanges()
        {
            ExperimentScenario first = CreateScenario();
            ExperimentScenario second = new ExperimentScenario
            {
                ScenarioId = "different-id",
                Name = "Different display name",
                RunCount = 99,
                FirstSeed = 98765,
                DurationDays = first.DurationDays,
                EndMode = first.EndMode,
                Settings = first.Settings.Clone(),
            };

            Assert.That(ExperimentConfigurationHasher.Compute(second), Is.EqualTo(ExperimentConfigurationHasher.Compute(first)));
            Assert.That(ExperimentConfigurationHasher.BuildCanonicalRepresentation(second), Is.EqualTo(ExperimentConfigurationHasher.BuildCanonicalRepresentation(first)));
        }

        [Test]
        public void HashChangesForScientificSettingDurationOrEndPolicy()
        {
            ExperimentScenario baseline = CreateScenario();
            string expected = ExperimentConfigurationHasher.Compute(baseline);

            ExperimentScenario changedSetting = CreateScenario();
            changedSetting.Settings.IndoorDiseaseTransmissionProbability += 1f;
            Assert.That(ExperimentConfigurationHasher.Compute(changedSetting), Is.Not.EqualTo(expected));

            ExperimentScenario changedDuration = CreateScenario();
            changedDuration.DurationDays += 1d;
            Assert.That(ExperimentConfigurationHasher.Compute(changedDuration), Is.Not.EqualTo(expected));

            ExperimentScenario changedPolicy = CreateScenario();
            changedPolicy.EndMode = ExperimentEndMode.DurationOrExtinction;
            Assert.That(ExperimentConfigurationHasher.Compute(changedPolicy), Is.Not.EqualTo(expected));
        }

        private static ExperimentScenario CreateScenario()
        {
            var configuration = new RealTimeConfig(true)
            {
                RatioIgnoreMasks = 30,
                RatioOtherProtectionMask = 35,
                RatioOwnProtectionMask = 35,
            };
            return new ExperimentScenario
            {
                ScenarioId = "scenario",
                Name = "Scenario",
                DurationDays = 30d,
                EndMode = ExperimentEndMode.FixedDuration,
                Settings = ExperimentScenarioSnapshot.Capture(configuration, false),
            };
        }
    }
}
