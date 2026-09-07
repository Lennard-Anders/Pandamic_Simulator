namespace RealTimeTests.Pandemic
{
    using System;
    using System.Collections.Generic;
    using NUnit.Framework;
    using RealTime.Pandemic;

    public sealed class ComplianceAndCalibrationTests
    {
        [Test]
        public void MaskPolicyToggleChangesHazardWithoutResettingTraitsOrStepLength()
        {
            var masks = new MaskEngine();
            masks.Reset(new MaskPolicy { Behavior = RealTime.Config.MaskBehavior.None, IgnorePercent = 0, SourceControlPercent = 100, PersonalProtectionPercent = 0, IndoorProbabilityPerHour = 0.4, TransmissionReductionFactor = 2 }, 42);
            masks.SetStepLengthHours(1);
            var assignment = masks.GetAssignment(1);
            Assert.That(masks.GetTransmissionProbability(1, 2, MaskTransmissionEnvironment.Indoor), Is.EqualTo(0.4).Within(1e-14));
            masks.SetBehavior(RealTime.Config.MaskBehavior.Full);
            Assert.That(masks.GetAssignment(1), Is.EqualTo(assignment));
            Assert.That(masks.GetTransmissionProbability(1, 2, MaskTransmissionEnvironment.Indoor), Is.EqualTo(0.2).Within(1e-14));
            masks.SetBehavior(RealTime.Config.MaskBehavior.None);
            Assert.That(masks.GetTransmissionProbability(1, 2, MaskTransmissionEnvironment.Indoor), Is.EqualTo(0.4).Within(1e-14));
        }
        [Test]
        public void CalibrationExportUsesRunObservationsAndCannotPassMissingRt()
        {
            var start = new DateTime(2030, 1, 1);
            var snapshot = new ExperimentRecorderSnapshot { RunStartTime = start, RunEndTime = start.AddDays(2) };
            snapshot.StateTimeSeries.Add(new PandemicStateTimePoint { SimulationTime = start.AddDays(1), TrackedPopulation = 100, Infectious = 10 });
            snapshot.StateTimeSeries.Add(new PandemicStateTimePoint { SimulationTime = start.AddDays(2), TrackedPopulation = 100, Infectious = 5 });
            var targets = new CalibrationTargetSet { Name = "Explicit external study", Source = "User supplied", RtDefinition = "Not estimated by this fixture", GenerationIntervalAssumptions = "Unspecified measurements" };
            targets.Targets.Add(new CalibrationTarget { Metric = CalibrationMetric.PeakPrevalence, TargetValue = 0.1, AbsoluteTolerance = 0.001, Weight = 1 });
            targets.Targets.Add(new CalibrationTarget { Metric = CalibrationMetric.TimeToPeakDays, TargetValue = 1, AbsoluteTolerance = 0.01, Weight = 1 });
            targets.Targets.Add(new CalibrationTarget { Metric = CalibrationMetric.Rt, TargetValue = 1, AbsoluteTolerance = 100, Weight = 1 });
            string csv = ScientificRunExportService.BuildCalibrationResultsCsv(snapshot, new ScientificRunSummary(), targets);
            string[] rows = csv.Trim().Split('\n');
            Assert.That(rows[1].Split(',')[8], Is.EqualTo("1"));
            Assert.That(rows[1].Split(',')[9], Is.EqualTo("1"));
            Assert.That(rows[2].Split(',')[6], Is.EqualTo("1"));
            Assert.That(rows[3].Split(',')[6], Is.Empty);
            Assert.That(rows[3].Split(',')[9], Is.EqualTo("0"));
            Assert.That(rows[3], Does.Contain("Unavailable"));
        }
        [Test]
        public void AssignedIsolationCanBeUnfollowedWithoutRemovingTheOrder()
        {
            var manager = new QuarantineManager();
            var time = new DateTime(2030, 1, 1);
            var events = new List<PandemicInterventionEvent>();
            manager.SetInterventionSink(events.Add);
            manager.ConfigureCompliance(42, 0, 100);
            manager.AddCitizenInIsolation(1, time);
            Assert.That(manager.IsInIsolation(1, time), Is.True);
            Assert.That(manager.IsRestricted(1, time), Is.False);
            Assert.That(events[0].ActuallyFollowed, Is.False);
            manager.AddContactQuarantine(1, time);
            Assert.That(manager.IsRestricted(1, time), Is.True);
            Assert.That(events[1].ActuallyFollowed, Is.True);
            manager.AddPendingTestQuarantine(1, time);
            Assert.That(manager.GetFollowingCount(time, true), Is.Zero);
            Assert.That(manager.GetFollowingCount(time, false), Is.EqualTo(1));
            Assert.That(manager.GetFollowingCount(time.AddDays(QuarantineManager.RestrictionDurationDays), false), Is.Zero);
        }

        [Test]
        public void ComplianceIsCitizenStableAcrossDiscoveryOrder()
        {
            var first = new QuarantineManager();
            var second = new QuarantineManager();
            first.ConfigureCompliance(42, 65, 43);
            second.ConfigureCompliance(42, 65, 43);
            var expected = new bool[100];
            for (uint i = 1; i < 100; i++) expected[i] = first.FollowsRestriction(i, true);
            for (uint i = 99; i > 0; i--) Assert.That(second.FollowsRestriction(i, true), Is.EqualTo(expected[i]));
        }

        [Test]
        public void CalibrationRejectsEmptyNegativeAndOutOfRangeRateTargets()
        {
            var targets = new CalibrationTargetSet();
            Assert.Throws<InvalidOperationException>(() => targets.Validate());
            targets.Targets.Add(new CalibrationTarget { Metric = CalibrationMetric.AttackRate, TargetValue = -0.1 });
            Assert.Throws<ArgumentOutOfRangeException>(() => targets.Validate());
            targets.Targets[0].TargetValue = 1.1;
            Assert.Throws<ArgumentOutOfRangeException>(() => targets.Validate());
            targets.Targets[0].TargetValue = 0.5;
            var evaluation = new CalibrationEngine().Evaluate(targets, new Dictionary<CalibrationMetric, double?>());
            Assert.That(evaluation.AllWithinTolerance, Is.False);
            Assert.That(evaluation.Results[0].ObservedValue, Is.Null);
        }

        [Test]
        public void ProfilerIsDisabledByDefaultAndBoundsItsWindow()
        {
            PandemicProfiler.Reset();
            PandemicProfiler.Enabled = false;
            using (PandemicProfiler.Measure("disabled")) { }
            Assert.That(PandemicProfiler.SnapshotCsv(), Does.Not.Contain("disabled"));
            try
            {
                PandemicProfiler.Enabled = true;
                for (int i = 0; i < 300; i++) using (PandemicProfiler.Measure("test")) { }
                Assert.That(PandemicProfiler.SnapshotCsv(), Does.Contain("test,300,256,"));
            }
            finally { PandemicProfiler.Enabled = false; PandemicProfiler.Reset(); }
        }
    }
}
