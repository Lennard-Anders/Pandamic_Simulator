namespace RealTimeTests.Experiments
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using NUnit.Framework;
    using RealTime.Experiments;
    using RealTime.Config;

    public sealed class ExperimentResultsAnalysisTests
    {
        [Test]
        public void StatisticsShowSampleUncertaintyAndLeaveSingleRunIntervalUnavailable()
        {
            var s = ExperimentMetricStatistics.Describe(new[] { 1d, 2, 3, 4 });
            Assert.That(s.Mean, Is.EqualTo(2.5)); Assert.That(s.Median, Is.EqualTo(2.5));
            Assert.That(s.StandardDeviation, Is.EqualTo(Math.Sqrt(5d / 3)).Within(1e-12));
            Assert.That(s.Lower95, Is.EqualTo(0.4457).Within(0.001));
            Assert.That(ExperimentMetricStatistics.Describe(new[] { 5d }).Lower95, Is.Null);
            Assert.That(ExperimentMetricStatistics.Describe(new[] { double.NaN }).Count, Is.Zero);
        }

        [Test]
        public void PairedComparisonRejectsDifferentSeedsBaselinesAndDuplicatePairs()
        {
            var a = Run(1, 10, 10); var b = Run(1, 10, 7);
            Assert.That(ExperimentResultsAnalysis.Paired(new[] { a }, new[] { b }, "deaths_total").Mean, Is.EqualTo(-3));
            b.Manifest.MasterSeed = 11;
            Assert.That(ExperimentResultsAnalysis.Paired(new[] { a }, new[] { b }, "deaths_total").Count, Is.Zero);
            b.Manifest.MasterSeed = 10; b.Manifest.BaselineAssetFullName = "Different city";
            Assert.That(ExperimentResultsAnalysis.Paired(new[] { a }, new[] { b }, "deaths_total").Count, Is.Zero);
            b.Manifest.BaselineAssetFullName = "City";
            Assert.That(ExperimentResultsAnalysis.Paired(new[] { a, a }, new[] { b }, "deaths_total").Count, Is.Zero);
        }

        [Test]
        public void MultiplePhasesValidatePersistAndChangeConfigurationHash()
        {
            var scenario = ExperimentPresetCatalog.Create(9, ExperimentScenarioSnapshot.Capture(new RealTimeConfig(true), false).Normalize());
            var schedule = scenario.InterventionSchedule;
            schedule.AdditionalPhases.Add(new ExperimentInterventionPhase { ActivationDay = 14, Settings = scenario.Settings.Clone() });
            schedule.Validate(scenario.Settings);
            Assert.That(schedule.SettingsAfter(0), Is.SameAs(schedule.Before));
            Assert.That(schedule.SettingsAfter(2).MaskBehavior, Is.EqualTo(MaskBehavior.None));
            string hash = ExperimentConfigurationHasher.Compute(scenario);
            schedule.AdditionalPhases[0].ActivationDay = 15;
            Assert.That(ExperimentConfigurationHasher.Compute(scenario), Is.Not.EqualTo(hash));
            var serializer = new ExperimentJsonSerializer();
            var restored = serializer.Deserialize<ExperimentScenario>(serializer.Serialize(scenario));
            Assert.That(restored.InterventionSchedule.DayAt(1), Is.EqualTo(15));
            Assert.That(ExperimentConfigurationHasher.Compute(restored), Is.EqualTo(ExperimentConfigurationHasher.Compute(scenario)));
            schedule.AdditionalPhases[0].ActivationDay = 7;
            Assert.Throws<ArgumentException>(() => schedule.Validate(scenario.Settings));
            schedule.AdditionalPhases[0].ActivationDay = 15;
            schedule.AdditionalPhases[0].Settings.DiseaseDuration++;
            Assert.Throws<ArgumentException>(() => schedule.Validate(scenario.Settings));
        }

        [Test]
        public void QualityWarnsWhenScheduledInterventionCannotBeObserved()
        {
            var scenario = ExperimentPresetCatalog.Create(9, ExperimentScenarioSnapshot.Capture(new RealTimeConfig(true), false).Normalize());
            scenario.DurationDays = 3;
            Assert.That(ExperimentResultsAnalysis.ScheduleHorizonWarnings(scenario), Does.Contain("day 7"));
            scenario.DurationDays = 7;
            Assert.That(ExperimentResultsAnalysis.ScheduleHorizonWarnings(scenario), Is.Not.Empty);
            scenario.DurationDays = 8;
            Assert.That(ExperimentResultsAnalysis.ScheduleHorizonWarnings(scenario), Is.Empty);
        }

        [Test]
        public void ClosureBurdenCountsSectorsSeparatelyAndOldFilesRemainUnavailable()
        {
            DateTime start = new DateTime(2030, 1, 1);
            Func<string, string, int, Dictionary<string, string>> entry = (family, action, day) => new Dictionary<string, string> { { "reason", "EffectiveFamilyState" }, { "context", family }, { "action", action }, { "simulation_time", start.AddDays(day).ToString("o") } };
            var events = new[] { entry("Schools", "Close", 1), entry("Offices", "Close", 2), entry("Schools", "Reopen", 3) };
            Assert.That(ExperimentResultsAnalysis.ClosureDays(events, start.AddDays(4)), Is.EqualTo(4));
            Assert.That(ExperimentResultsAnalysis.ClosureDays(new Dictionary<string, string>[0], start), Is.Null);
        }

        [Test]
        public void CsvReaderPreservesQuotedCommasQuotesAndEmbeddedNewlines()
        {
            string path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, "a,b\r\n\"x,y\",\"line1\nline2 \"\"quote\"\"\"\r\n");
                var rows = ExperimentResultsAnalysis.ReadCsv(path).ToArray();
                Assert.That(rows.Length, Is.EqualTo(1)); Assert.That(rows[0]["a"], Is.EqualTo("x,y"));
                Assert.That(rows[0]["b"], Is.EqualTo("line1\nline2 \"quote\""));
            }
            finally { File.Delete(path); }
        }

        [Test]
        public void ReplayBoundsDisplayMemoryPreservesFinalFrameAndIsDeterministic()
        {
            string root = Path.Combine(Path.GetTempPath(), "tenus-replay-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var start = new DateTime(2030, 1, 1);
                using (var writer = new StreamWriter(Path.Combine(root, "state_timeseries.csv")))
                {
                    writer.WriteLine("simulation_time,infectious");
                    for (int i = 0; i < 10020; i++) writer.WriteLine(start.AddMinutes(i).ToString("o") + "," + i);
                }
                using (var writer = new StreamWriter(Path.Combine(root, "transmission_events.csv")))
                {
                    writer.WriteLine("simulation_time,source_citizen_id,target_citizen_id,position_x,position_z,has_position");
                    for (int i = 0; i < 2000; i++) writer.WriteLine(start.AddMinutes(i).ToString("o") + ",1," + (i + 2) + ",10,20,1");
                }
                File.WriteAllText(Path.Combine(root, "intervention_events.csv"), "simulation_time,intervention_type,action,citizen_id\n");
                var data = ExperimentReplayData.Load(root); var again = ExperimentReplayData.Load(root);
                Assert.That(data.TotalFrames, Is.EqualTo(10020)); Assert.That(data.Frames.Count, Is.LessThanOrEqualTo(10001));
                Assert.That(data.Frames[data.Frames.Count - 1]["infectious"], Is.EqualTo("10019"));
                Assert.That(data.TotalLinks, Is.EqualTo(2000)); Assert.That(data.Links.Count, Is.LessThanOrEqualTo(256));
                CollectionAssert.AreEqual(data.Links.Select(link => link.Target), again.Links.Select(link => link.Target));
                Assert.That(File.ReadAllLines(Path.Combine(root, "transmission_events.csv")).Length, Is.EqualTo(2001));
            }
            finally { Directory.Delete(root, true); }
        }

        private static ExperimentResultRun Run(int pair, int seed, double deaths)
        {
            var run = new ExperimentResultRun { Manifest = new ExperimentRunManifest { BatchId = "Batch", PairId = pair, MasterSeed = seed, PairedSeedMode = true, BaselineAssetFullName = "City", GitCommitSha = "build", ConfiguredDurationDays = 30, EndMode = "Fixed" } };
            run.Metrics["deaths_total"] = deaths; return run;
        }
    }
}
