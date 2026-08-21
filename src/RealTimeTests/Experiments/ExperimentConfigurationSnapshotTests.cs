// <copyright file="ExperimentConfigurationSnapshotTests.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTimeTests.Experiments
{
    using System.Linq;
    using RealTime.Config;
    using RealTime.Experiments;
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
    }
}
