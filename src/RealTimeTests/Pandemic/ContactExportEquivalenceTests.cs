namespace RealTimeTests.Pandemic
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using NUnit.Framework;
    using RealTime.Config;
    using RealTime.Experiments;
    using RealTime.Pandemic;

    public sealed class ContactExportEquivalenceTests
    {
        [TestCase(42)] [TestCase(73)]
        public void StorageModesPreserveStepwiseDiseaseTransmissionTestingAndInterventions(int seed)
        {
            string expected = Run(ScientificContactExportMode.FullRaw, seed);
            Assert.That(Run(ScientificContactExportMode.Standard, seed), Is.EqualTo(expected));
            Assert.That(Run(ScientificContactExportMode.SummaryOnly, seed), Is.EqualTo(expected));
            TestContext.WriteLine("seed={0} all three modes scientific ledger SHA256={1}", seed, expected);
        }

        private static string Run(ScientificContactExportMode mode, int seed)
        {
            const int population = 20;
            var start = new DateTime(2030, 1, 1);
            var config = new RealTimeConfig(true);
            var core = new SyntheticEpidemicCore(DiseaseProgressionEngineTests.CreatePolicy(PandemicDistributionType.Deterministic), seed, seed + 1);
            for (uint id = 1; id <= 2; id++)
            {
                var course = new DiseaseCourse(id, start.AddDays(-2), start.AddDays(-1), start.AddDays(2), null, null,
                    start.AddDays(5), false, DiseaseExposureKind.InitialSeed);
                if (id == 2) course.ScheduleDeath(start.AddDays(1));
                core.RegisterCourse(course, start);
            }
            for (uint id = 3; id <= population; id++) core.RegisterSusceptible(id);
            var testing = new TestingEngine(new PandemicTestingPolicy { Population = population,
                RelativeCapacityPercentPerSevenDays = 100, ReservedForSymptomaticPercent = 0,
                MaximumRequestToSampleDays = 7, ResultDelayDays = 0.25, SensitivityPercent = 80,
                SpecificityPercent = 95, RetestIntervalDays = 1 }, new Random(seed + 2), start);
            for (uint id = 1; id <= population; id++) testing.RequestTest(id, start, PandemicTestPriority.Routine, PandemicTestReason.Screening);
            var tracing = new ContactTracingEngine(new ContactTracingPolicy { AppAdoptionPercent = 60, ManualTraceabilityPercent = 80 }, seed);
            var sampler = new ContactSamplingEngine();
            var engine = new ContactEngine();
            engine.SetRetainHistory(false);
            var ids = Enumerable.Range(1, population).Select(i => (uint)i).ToArray();
            var isolated = new HashSet<uint>();
            var ledger = new StringBuilder();
            string directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "equivalence-" + mode + "-" + Guid.NewGuid().ToString("N"));
            using (var recorder = new ExperimentRecorder())
            {
                recorder.BeginRun(start, directory, mode);
                recorder.RecordPopulation(new PandemicPopulationEvent { SimulationTime = start, Action = "InitialPopulation", Count = population });
                for (int step = 1; step <= 2880; step++)
                {
                    DateTime time = start.AddMinutes(step * 5);
                    testing.Advance(time, id => new PandemicTestSampleContext { DiseaseState = core.GetState(id, time), ExposureTime = start });
                    foreach (var result in testing.Records)
                        if (result.Result == PandemicTestResult.Positive && isolated.Add(result.CitizenId))
                            recorder.RecordIntervention(new PandemicInterventionEvent { SimulationTime = time, CitizenId = result.CitizenId,
                                InterventionType = PandemicInterventionType.Isolation, Action = "Start", Reason = "Synthetic positive-test policy" });
                    var contacts = new List<SyntheticContact>();
                    recorder.BeginContactStep(time);
                    foreach (var pair in sampler.SampleBounded(ids, 4, seed, ContactPersistencePolicy.SamplingKey(config, PhysicalContactContext.Workplace, time), 7))
                    {
                        if (isolated.Contains(pair.CitizenA) || isolated.Contains(pair.CitizenB)
                            || core.GetState(pair.CitizenA, time) == DiseaseState.Dead || core.GetState(pair.CitizenB, time) == DiseaseState.Dead)
                        { recorder.RecordPreventedContact(); continue; }
                        var physical = engine.Record(new PhysicalContactRequest { CitizenA = pair.CitizenA, CitizenB = pair.CitizenB,
                            EndTime = time, DurationMinutes = 5, Context = PhysicalContactContext.Workplace, BuildingId = 7 }, out bool created);
                        var flags = tracing.Evaluate(physical);
                        physical.TraceableByApp = flags.TraceableByApp; physical.TraceableByManual = flags.TraceableByManual;
                        recorder.RecordPhysicalContact(physical, 0, 3, 3);
                        ledger.Append(physical.ContactId).Append(':').Append(flags.IsTraceable).Append(';');
                        contacts.Add(new SyntheticContact { CitizenA = pair.CitizenA, CitizenB = pair.CitizenB,
                            Context = PhysicalContactContext.Workplace, TransmissionProbability = 0.01 });
                    }
                    recorder.EndContactStep();
                    int before = core.TransmissionEvents.Count;
                    core.Step(time, contacts); // Always five-minute resolution, never delayed to episode closure.
                    foreach (var transmission in core.TransmissionEvents.Skip(before))
                        recorder.RecordTransmission(new PandemicTransmissionEvent { SimulationTime = time,
                            SourceCitizenId = transmission.SourceCitizenId, TargetCitizenId = transmission.TargetCitizenId,
                            TargetPreviousState = DiseaseState.Susceptible, TargetNewState = DiseaseState.Exposed,
                            Context = PhysicalContactContext.Workplace, TransmissionProbability = 0.01, SourceProbability = 0.01,
                            InfectiousnessMultiplier = 1 });
                    var metrics = core.CaptureMetrics(time);
                    recorder.RecordState(time, metrics.Counts, metrics.Symptomatic, metrics.InitialSeedCount,
                        metrics.SecondaryTransmissionCount, metrics.HospitalizationsTotal, isolated.Count, 0);
                    foreach (uint id in ids) ledger.Append((int)core.GetState(id, time));
                }
                var snapshot = recorder.Freeze(start.AddDays(10));
                Assert.That(snapshot.TransmissionEvents.Count, Is.GreaterThan(0));
                Assert.That(snapshot.StateTimeSeries.Last().Dead, Is.EqualTo(1));
                Assert.That(snapshot.StateTimeSeries.Last().Recovered, Is.GreaterThan(0));
                Assert.That(snapshot.InterventionEvents.Count, Is.GreaterThan(0));
                Assert.That(testing.Records.Any(r => r.Result == PandemicTestResult.Negative), Is.True);
                var serializer = new ExperimentJsonSerializer { MaxJsonLength = 16000000 };
                ledger.Append(serializer.Serialize(snapshot.StateTimeSeries));
                ledger.Append(serializer.Serialize(snapshot.TransmissionEvents));
                ledger.Append(serializer.Serialize(snapshot.InterventionEvents));
                ledger.Append(serializer.Serialize(testing.Records.ToList()));
                ledger.Append(snapshot.ContactNetworkSummaryCsv).Append(snapshot.ContactEpisodeSummaryCsv)
                    .Append(snapshot.AgeMixingCsv).Append(snapshot.ContactEpisodeDurationCsv);
                using (var hash = SHA256.Create())
                    return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(ledger.ToString()))).Replace("-", "");
            }
        }
    }
}
