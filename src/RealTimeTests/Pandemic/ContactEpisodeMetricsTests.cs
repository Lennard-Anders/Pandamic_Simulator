namespace RealTimeTests.Pandemic
{
    using System;
    using System.Linq;
    using NUnit.Framework;
    using RealTime.Pandemic;

    public sealed class ContactEpisodeMetricsTests
    {
        [Test]
        public void EpisodeParticipationMinutesAndRatesAreDistinctFromStepCounts()
        {
            var start = new DateTime(2030, 1, 1);
            var metrics = new ContactEpisodeMetrics();
            metrics.Record(new ContactEpisode { StartTime = start, EndTime = start.AddMinutes(20), Context = PhysicalContactContext.Workplace, EpidemiologicalSteps = 4 });
            metrics.Record(new ContactEpisode { StartTime = start, EndTime = start.AddMinutes(5), Context = PhysicalContactContext.Workplace, EpidemiologicalSteps = 1 });
            string row = metrics.SummaryCsv(2).Split('\n').Single(line => line.StartsWith("Workplace,"));
            Assert.That(row, Is.EqualTo("Workplace,2,25,2,2,25"));
            Assert.That(metrics.DurationCsv(), Is.EqualTo("episode_duration_minutes,contact_episodes\n5,1\n20,1\n"));
            Assert.That(metrics.SummaryCsv(0), Does.Contain("Workplace,2,25,0,,"));
        }

        [Test]
        public void RawMetricAliasesPreserveDataRows()
        {
            var metrics = new ContactNetworkMetrics();
            var start = new DateTime(2030, 1, 1);
            metrics.Begin(start);
            metrics.Complete(start.AddDays(1));
            string legacy = metrics.SummaryCsv;
            string explicitNames = ScientificRunExportService.BuildContactStepSummary(legacy);
            Assert.That(explicitNames, Does.Contain("raw_contact_steps"));
            Assert.That(explicitNames.Substring(explicitNames.IndexOf('\n')), Is.EqualTo(legacy.Substring(legacy.IndexOf('\n'))));
        }
    }
}
