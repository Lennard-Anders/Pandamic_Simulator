namespace RealTimeTests.Pandemic
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using NUnit.Framework;
    using RealTime.Pandemic;

    public sealed class ExperimentRecorderTests
    {
        private static readonly DateTime Start = new DateTime(2042, 4, 3, 12, 0, 0, DateTimeKind.Utc);

        [Test]
        public void FreezePublishesCompleteStreamingPackageAndPreservesContactHistory()
        {
            string directory = CreateTemporaryDirectory();
            try
            {
                var recorder = new ExperimentRecorder();
                recorder.BeginRun(Start, directory);
                AssertTemporaryStreams(directory, true);

                var contacts = new ContactEngine();
                bool created;
                PhysicalContactEvent first = contacts.Record(Contact(9u, 2u, Start.AddMinutes(5d)), out created);
                first.TraceableByApp = true;
                recorder.RecordPhysicalContact(first, 4);
                PhysicalContactEvent second = contacts.Record(Contact(2u, 9u, Start.AddMinutes(10d)), out created);
                second.TraceableByManual = true;
                recorder.RecordPhysicalContact(second, 4);

                recorder.RecordTransmission(Transmission(Start.AddMinutes(10d)));
                recorder.RecordState(Start.AddMinutes(10d), Counts(), 1, 2, 1, 1, 1, 0);
                ExperimentRecorderSnapshot snapshot = recorder.Freeze(Start.AddMinutes(15d));

                AssertTemporaryStreams(directory, false);
                Assert.That(File.ReadAllLines(Path.Combine(directory, ExperimentRecorder.PhysicalContactsFileName)), Has.Length.EqualTo(3));
                Assert.That(File.ReadAllLines(Path.Combine(directory, ExperimentRecorder.TraceableContactsFileName)), Has.Length.EqualTo(3));
                Assert.That(File.ReadAllLines(Path.Combine(directory, ExperimentRecorder.TransmissionEventsFileName)), Has.Length.EqualTo(2));
                Assert.That(snapshot.PhysicalContactsTotal, Is.EqualTo(2));
                Assert.That(snapshot.TraceableContactsTotal, Is.EqualTo(2));
                Assert.That(snapshot.WorkContactsTotal, Is.EqualTo(2));
                Assert.That(snapshot.TransmissionEvents, Has.Count.EqualTo(1));
                Assert.That(snapshot.TransmissionEvents[0].IsInitialSeed, Is.False);
                Assert.That(snapshot.StateTimeSeries[0].InitialSeedCount, Is.EqualTo(2));
                Assert.That(snapshot.StateTimeSeries[0].SecondaryTransmissionsTotal, Is.EqualTo(1));
            }
            finally
            {
                DeleteTemporaryDirectory(directory);
            }
        }

        [Test]
        public void TransmissionRowsAcceptOnlySuccessfulNonSeedSusceptibleToExposedTransitions()
        {
            var recorder = new ExperimentRecorder();
            recorder.BeginRun(Start, null);
            PandemicTransmissionEvent invalidSeed = Transmission(Start);
            invalidSeed.IsInitialSeed = true;
            Assert.That(() => recorder.RecordTransmission(invalidSeed), Throws.InvalidOperationException);

            PandemicTransmissionEvent invalidTransition = Transmission(Start);
            invalidTransition.TargetPreviousState = DiseaseState.Recovered;
            Assert.That(() => recorder.RecordTransmission(invalidTransition), Throws.InvalidOperationException);

            PandemicTransmissionEvent invalidProbability = Transmission(Start);
            invalidProbability.TransmissionProbability = double.NaN;
            Assert.That(() => recorder.RecordTransmission(invalidProbability), Throws.InvalidOperationException);
        }

        [Test]
        public void StateSeriesMaintainsDisjointPopulationInvariantAndSecondaryDelta()
        {
            var recorder = new ExperimentRecorder();
            recorder.BeginRun(Start, null);
            recorder.RecordTransmission(Transmission(Start));
            recorder.RecordState(Start, Counts(), 1, 2, 1, 0, 0, 0);
            PandemicTransmissionEvent second = Transmission(Start.AddMinutes(5d));
            second.TargetCitizenId = 3u;
            recorder.RecordTransmission(second);
            PandemicTransmissionEvent third = Transmission(Start.AddMinutes(5d));
            third.TargetCitizenId = 4u;
            recorder.RecordTransmission(third);
            recorder.RecordState(Start.AddMinutes(5d), Counts(), 1, 2, 3, 0, 0, 0);

            ExperimentRecorderSnapshot snapshot = recorder.Freeze(Start.AddMinutes(5d));

            Assert.That(snapshot.StateTimeSeries[0].TrackedPopulation, Is.EqualTo(100));
            Assert.That(snapshot.StateTimeSeries[0].NewExposures, Is.EqualTo(1));
            Assert.That(snapshot.StateTimeSeries[1].NewExposures, Is.EqualTo(2));
            Assert.That(snapshot.StateTimeSeries.All(point =>
                point.Susceptible + point.Exposed + point.Infectious + point.PostInfectiousIll
                + point.Recovered + point.Dead == point.TrackedPopulation), Is.True);
        }

        [Test]
        public void SuccessfulFreezeRejectsMissingTransmissionRowsButInvalidFreezePreservesDiagnostics()
        {
            var recorder = new ExperimentRecorder();
            recorder.BeginRun(Start, null);
            recorder.RecordState(Start, Counts(), 1, 2, 1, 0, 0, 0);

            Assert.That(() => recorder.Freeze(Start), Throws.InvalidOperationException);
            ExperimentRecorderSnapshot diagnostic = recorder.Freeze(Start, false);
            Assert.That(diagnostic.StateTimeSeries[0].SecondaryTransmissionsTotal, Is.EqualTo(1));
            Assert.That(diagnostic.TransmissionEvents, Is.Empty);
        }

        [Test]
        public void ScientificSummarySeparatesSeedsAndComputesActualPersonDays()
        {
            var snapshot = new ExperimentRecorderSnapshot
            {
                RunStartTime = Start,
                RunEndTime = Start.AddDays(4d),
                PhysicalContactsTotal = 20,
                TraceableContactsTotal = 7,
            };
            snapshot.StateTimeSeries.Add(new PandemicStateTimePoint
            {
                SimulationTime = snapshot.RunEndTime,
                Susceptible = 80,
                Exposed = 2,
                Infectious = 3,
                PostInfectiousIll = 1,
                Recovered = 12,
                Dead = 2,
                TrackedPopulation = 100,
                InitialSeedCount = 4,
                SecondaryTransmissionsTotal = 16,
            });
            snapshot.TransmissionEvents.Add(Transmission(Start.AddDays(1d)));
            snapshot.InterventionEvents.Add(Intervention(1, PandemicInterventionType.Isolation, "Start", Start, 10u));
            snapshot.InterventionEvents.Add(Intervention(2, PandemicInterventionType.Isolation, "End", Start.AddDays(2d), 10u));
            snapshot.InterventionEvents.Add(Intervention(3, PandemicInterventionType.Quarantine, "Start", Start.AddDays(1d), 11u));
            snapshot.PopulationEvents.Add(new PandemicPopulationEvent { Action = "Added", Count = 2 });
            snapshot.PopulationEvents.Add(new PandemicPopulationEvent { Action = "Removed", Count = 1 });

            ScientificRunSummary summary = ScientificRunExportService.CalculateSummary(snapshot, new List<PandemicTestRecord>(), 35d);

            Assert.That(summary.InitialSeedCount, Is.EqualTo(4));
            Assert.That(summary.SecondaryTransmissionsTotal, Is.EqualTo(16));
            Assert.That(summary.CumulativeInfections, Is.EqualTo(20));
            Assert.That(summary.AttackRatePercent, Is.EqualTo(20d));
            Assert.That(summary.TotalIsolationPersonDays, Is.EqualTo(2d));
            Assert.That(summary.TotalQuarantinePersonDays, Is.EqualTo(3d));
            Assert.That(summary.ActualMaskUsagePercent, Is.EqualTo(35d));
            Assert.That(summary.AddedPopulation, Is.EqualTo(2));
            Assert.That(summary.RemovedPopulation, Is.EqualTo(1));
        }

        [Test]
        public void RichSettingsExportContainsEveryExtendedScientificModelFamily()
        {
            string csv = PandemicRunExportService.BuildSettingsSectionForTesting(new RealTime.Config.RealTimeConfig(true));

            Assert.That(csv, Does.Contain("ExposedDurationDistributionType,Deterministic"));
            Assert.That(csv, Does.Contain("InfectiousEndFixedDays,"));
            Assert.That(csv, Does.Contain("RecoveryDistributionType,Deterministic"));
            Assert.That(csv, Does.Contain("InfectiousnessProfileType,Flat"));
            Assert.That(csv, Does.Contain("InitialSeedSamplingStrategy,UniformPopulation"));
            Assert.That(csv, Does.Contain("InitialInfectionAgeMode,FixedInitialInfectionAge"));
            Assert.That(csv, Does.Contain("StrictPopulationIntegrity,0"));
            Assert.That(csv, Does.Contain("AsymptomaticMortalityMultiplier,"));
            Assert.That(csv, Does.Contain("HealthcareCriticalMortalityMultiplier,"));
        }

        [Test]
        public void HealthcareExportContainsTheEntireRunAndScientificFilesReplaceAtomically()
        {
            var points = Enumerable.Range(0, 31)
                .Select(day => new PandemicHealthcareTimePoint
                {
                    SimulationTime = Start.AddDays(day),
                    HospitalUsagePercent = day,
                    AmbulanceUsagePercent = day / 2f,
                })
                .ToList();
            string csv = ScientificRunExportService.BuildHealthcareTimeSeriesCsv(points, Start);
            Assert.That(csv.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries), Has.Length.EqualTo(32));

            string directory = CreateTemporaryDirectory();
            try
            {
                string path = Path.Combine(directory, "state_timeseries.csv");
                ScientificRunExportService.WriteAtomic(path, "first");
                ScientificRunExportService.WriteAtomic(path, "second");
                Assert.That(File.ReadAllText(path), Is.EqualTo("second"));
                Assert.That(Directory.GetFiles(directory, ".tmp-*"), Is.Empty);
            }
            finally
            {
                DeleteTemporaryDirectory(directory);
            }
        }

        private static DiseaseStateCounts Counts()
        {
            return new DiseaseStateCounts
            {
                Susceptible = 80,
                Exposed = 5,
                Infectious = 5,
                PostInfectiousIll = 2,
                Recovered = 7,
                Dead = 1,
            };
        }

        private static PandemicTransmissionEvent Transmission(DateTime time)
        {
            return new PandemicTransmissionEvent
            {
                SimulationTime = time,
                SourceCitizenId = 1u,
                TargetCitizenId = 2u,
                TargetPreviousState = DiseaseState.Susceptible,
                TargetNewState = DiseaseState.Exposed,
                Context = PhysicalContactContext.Household,
                TransmissionProbability = 0.5d,
                SourceProbability = 0.25d,
                InfectiousnessMultiplier = 1d,
                IsInitialSeed = false,
            };
        }

        private static PandemicInterventionEvent Intervention(
            long eventId,
            PandemicInterventionType type,
            string action,
            DateTime time,
            uint citizenId)
        {
            return new PandemicInterventionEvent
            {
                EventId = eventId,
                InterventionType = type,
                Action = action,
                SimulationTime = time,
                CitizenId = citizenId,
            };
        }

        private static PhysicalContactRequest Contact(uint citizenA, uint citizenB, DateTime end)
        {
            return new PhysicalContactRequest
            {
                CitizenA = citizenA,
                CitizenB = citizenB,
                EndTime = end,
                DurationMinutes = 5d,
                Context = PhysicalContactContext.Workplace,
                BuildingId = 7,
            };
        }

        private static void AssertTemporaryStreams(string directory, bool expected)
        {
            Assert.That(File.Exists(Path.Combine(directory, ExperimentRecorder.TransmissionEventsFileName + ".tmp")), Is.EqualTo(expected));
            Assert.That(File.Exists(Path.Combine(directory, ExperimentRecorder.PhysicalContactsFileName + ".tmp")), Is.EqualTo(expected));
            Assert.That(File.Exists(Path.Combine(directory, ExperimentRecorder.TraceableContactsFileName + ".tmp")), Is.EqualTo(expected));
        }

        private static string CreateTemporaryDirectory()
        {
            string path = Path.Combine(Path.GetTempPath(), "TENUS-recorder-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static void DeleteTemporaryDirectory(string path)
        {
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
    }
}
