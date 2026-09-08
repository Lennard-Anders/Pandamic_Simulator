namespace RealTimeTests.Experiments
{
    using NUnit.Framework;
    using RealTime.Config;
    using RealTime.Experiments;

    public sealed class ContactStorageEstimateTests
    {
        [Test]
        public void UnsupportedUnityDiskQueryIsUnknownInsteadOfFailingPreflightOrStorage()
        {
            Assert.That(ContactStorageEstimate.AvailableBytes("C:\\", path =>
                throw new System.NotImplementedException("The requested feature is not implemented.")), Is.Null);
            Assert.That(ContactStorageEstimate.AvailableBytes("C:\\", path =>
                throw new System.NotSupportedException()), Is.Null);
            Assert.That(ContactStorageEstimate.AvailableBytes("C:\\", path => 0), Is.EqualTo(0),
                "Known disk exhaustion must not be treated as an unsupported query");
            Assert.That(ContactStorageEstimate.AvailableBytes("C:\\", path => 123456789), Is.EqualTo(123456789));
        }

        [Test]
        public void EstimateScalesWithPopulationDurationRunsAndStepWithoutChangingSettings()
        {
            var config = new RealTimeConfig(true);
            var settings = ExperimentScenarioSnapshot.Capture(config, false);
            var baseline = ContactStorageEstimate.Calculate(1000, 30, 1, settings);
            Assert.That(ContactStorageEstimate.Calculate(2000, 30, 1, settings).HighGB, Is.EqualTo(2 * baseline.HighGB));
            Assert.That(ContactStorageEstimate.Calculate(1000, 60, 2, settings).HighGB, Is.EqualTo(4 * baseline.HighGB));
            Assert.That(settings.EpidemicStepMinutes, Is.EqualTo(5));
            settings.ScientificContactExportMode = ScientificContactExportMode.SummaryOnly;
            Assert.That(ContactStorageEstimate.Calculate(1000, 30, 1, settings).HighGB, Is.Zero);
            Assert.That(ContactStorageEstimate.Calculate(1000, 30, 1, settings).Category, Is.EqualTo("Low"));
        }

        [Test]
        public void LargeRawBatchIsFlaggedExtreme()
        {
            var settings = ExperimentScenarioSnapshot.Capture(new RealTimeConfig(true), false);
            settings.ScientificContactExportMode = ScientificContactExportMode.FullRaw;
            var estimate = ContactStorageEstimate.Calculate(100000, 30, 3, settings);
            Assert.That(estimate.Category, Is.EqualTo("Extreme"));
            Assert.That(estimate.Describe(), Does.Contain("Estimated contact output: Extreme"));
            Assert.That(estimate.LowGB, Is.LessThan(estimate.HighGB));
        }
    }
}
