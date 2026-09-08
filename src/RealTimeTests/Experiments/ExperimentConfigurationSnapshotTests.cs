// <copyright file="ExperimentConfigurationSnapshotTests.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTimeTests.Experiments
{
    using System.Linq;
    using RealTime.Config;
    using RealTime.Experiments;
    using RealTime.Pandemic;
    using NUnit.Framework;

    public sealed class ExperimentConfigurationSnapshotTests
    {
        [Test]
        public void ScenarioApplyTouchesOnlyTheAllowlist()
        {
            RealTimeConfig configuration = new RealTimeConfig(true)
            {
                DayTimeSpeed = 2,
                SwitchOffLightsAtNight = false,
                UseEnglishUSFormats = true,
            };
            ExperimentScenarioSnapshot snapshot = ExperimentScenarioSnapshot.Capture(configuration, true);
            configuration.DayTimeSpeed = 6;
            configuration.SwitchOffLightsAtNight = true;
            configuration.UseEnglishUSFormats = false;

            snapshot.ApplyTo(configuration);

            Assert.That(configuration.DayTimeSpeed, Is.EqualTo(2u));
            Assert.That(configuration.SwitchOffLightsAtNight, Is.True);
            Assert.That(configuration.UseEnglishUSFormats, Is.False);
            Assert.That(snapshot.InitialLockdownEnabled, Is.True);
        }

        [Test]
        public void NormalizeReturnsACloneAndReportsStableDiffs()
        {
            ExperimentScenarioSnapshot source = ExperimentScenarioSnapshot.Capture(new RealTimeConfig(true), false);
            source.DayTimeSpeed = 99;

            ExperimentScenarioSnapshot normalized = source.Normalize();

            Assert.That(source.DayTimeSpeed, Is.EqualTo(99u));
            Assert.That(normalized.DayTimeSpeed, Is.EqualTo(6u));
            Assert.That(normalized.Diff(source).Exists(difference => difference.PropertyName == "DayTimeSpeed"), Is.True);
        }

        [Test]
        public void FullSnapshotRestoresExcludedAndPersistenceFields()
        {
            RealTimeConfig configuration = new RealTimeConfig(true)
            {
                SwitchOffLightsAtNight = false,
                StaticBaselineSaveAsDefault = true,
                UseEnglishUSFormats = true,
            };
            RealTimeConfigSnapshot snapshot = RealTimeConfigSnapshot.Capture(configuration);
            configuration.SwitchOffLightsAtNight = true;
            configuration.StaticBaselineSaveAsDefault = false;
            configuration.UseEnglishUSFormats = false;

            snapshot.ApplyTo(configuration);

            Assert.That(configuration.SwitchOffLightsAtNight, Is.False);
            Assert.That(configuration.StaticBaselineSaveAsDefault, Is.True);
            Assert.That(configuration.UseEnglishUSFormats, Is.True);
        }

        [Test]
        public void EveryConfigurationPropertyIsExplicitlyIncludedOrExcluded()
        {
            Assert.That(
                ExperimentConfigurationMapper.GetUncategorizedConfigurationProperties(),
                Is.Empty,
                "Every new RealTimeConfig property must be deliberately categorized for experiment capture.");
        }

        [Test]
        public void RuntimeNormalizationPreservesScientificallyValidZeros()
        {
            var configuration = new RealTimeConfig(true)
            {
                IndoorDiseaseTransmissionProbability = 0f,
                OutdoorDiseaseTransmissionProbability = 0f,
                DiseaseTransmissionRange = 0f,
                DiseaseStartInfectionRatio = 0f,
                SymptomProbability = 0f,
            };

            PandemicConfigurationNormalizer.Normalize(configuration);

            Assert.That(configuration.IndoorDiseaseTransmissionProbability, Is.Zero);
            Assert.That(configuration.OutdoorDiseaseTransmissionProbability, Is.Zero);
            Assert.That(configuration.DiseaseTransmissionRange, Is.Zero);
            Assert.That(configuration.DiseaseStartInfectionRatio, Is.Zero);
            Assert.That(configuration.SymptomProbability, Is.Zero);
        }

        [Test]
        public void LockdownPolicyValuesAreDeepCopiedAndAppliedInPlace()
        {
            var configuration = new RealTimeConfig(true)
            {
                CloseOfficeThresholdPercent = 30f,
                ReopenOfficeThresholdPercent = 15f,
                MinimumOfficeClosureDurationDays = 2.5f,
                OfficeLockdownCooldownDurationDays = 1.25f,
            };
            ExperimentScenarioSnapshot snapshot = ExperimentScenarioSnapshot.Capture(configuration, true);

            configuration.CloseOfficeThresholdPercent = 90f;
            configuration.ReopenOfficeThresholdPercent = 80f;
            configuration.MinimumOfficeClosureDurationDays = 0f;
            configuration.OfficeLockdownCooldownDurationDays = 0f;
            snapshot.ApplyTo(configuration);

            Assert.That(configuration.CloseOfficeThresholdPercent, Is.EqualTo(30f));
            Assert.That(configuration.ReopenOfficeThresholdPercent, Is.EqualTo(15f));
            Assert.That(configuration.MinimumOfficeClosureDurationDays, Is.EqualTo(2.5f));
            Assert.That(configuration.OfficeLockdownCooldownDurationDays, Is.EqualTo(1.25f));
        }

        [Test]
        public void LatestMigrationPreservesLegacyPoliciesAndCreatesDeterministicScientificModel()
        {
            var configuration = new RealTimeConfig
            {
                Version = 10,
                CloseOfficeThresholdPercent = 37f,
                ReopenOfficeThresholdPercent = 0f,
                MinimumOfficeClosureDurationDays = 99f,
                OfficeLockdownCooldownDurationDays = 99f,
            };

            configuration.MigrateWhenNecessary();

            Assert.That(configuration.Version, Is.EqualTo(14));
            Assert.That(configuration.ContactPersistenceModel, Is.EqualTo(ContactPersistenceModel.LegacyPerStep));
            Assert.That(configuration.StrictPopulationIntegrity, Is.False);
            Assert.That(configuration.ReopenOfficeThresholdPercent, Is.EqualTo(37f));
            Assert.That(configuration.MinimumOfficeClosureDurationDays, Is.Zero);
            Assert.That(configuration.OfficeLockdownCooldownDurationDays, Is.Zero);
            Assert.That(configuration.InfectiousStartDistributionType, Is.EqualTo(PandemicDistributionType.Deterministic));
            Assert.That(configuration.InfectiousStartFixedDays, Is.EqualTo(configuration.StartInfection));
            Assert.That(configuration.InfectiousEndFixedDays, Is.EqualTo(configuration.EndInfection));
            Assert.That(configuration.RecoveryFixedDays, Is.EqualTo(configuration.DiseaseDuration));
            Assert.That(configuration.InfectiousnessProfileType, Is.EqualTo(PandemicInfectiousnessProfileType.Flat));
            Assert.That(configuration.AsymptomaticMortalityMultiplier, Is.EqualTo(0.10f));
            Assert.That(configuration.RatioIgnoreMasks + configuration.RatioOtherProtectionMask + configuration.RatioOwnProtectionMask, Is.EqualTo(100));
        }

        [Test]
        public void LegacyMaskWeightsMigrateToNearestEffectivePercentages()
        {
            var configuration = new RealTimeConfig(true)
            {
                Version = 11,
                RatioIgnoreMasks = 30,
                RatioOtherProtectionMask = 50,
                RatioOwnProtectionMask = 50,
            };

            configuration.MigrateWhenNecessary();

            Assert.That(configuration.RatioIgnoreMasks, Is.EqualTo(23));
            Assert.That(configuration.RatioOtherProtectionMask, Is.EqualTo(39));
            Assert.That(configuration.RatioOwnProtectionMask, Is.EqualTo(38));
        }
    }
}
