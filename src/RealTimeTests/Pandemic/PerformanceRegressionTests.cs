namespace RealTimeTests.Pandemic
{
    using System;
    using System.Diagnostics;
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;
    using RealTime.Pandemic;

    public sealed class PerformanceRegressionTests
    {
        private static readonly Func<long> AllocatedBytes = CreateAllocationCounter();

        [Test]
        public void BufferedContactCsvPreservesBothStreamsAndReducesAllocations()
        {
            var method = typeof(ExperimentRecorder).GetMethod("WriteCsvRowCore", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var legacy = (Action<System.IO.StreamWriter, object[]>)Delegate.CreateDelegate(typeof(Action<System.IO.StreamWriter, object[]>), method);
            var contacts = new List<PhysicalContactEvent>();
            var engine = new ContactEngine();
            var start = new DateTime(2030, 1, 1);
            var invariant = System.Globalization.CultureInfo.InvariantCulture;
            for (int i = 0; i < 10000; i++)
            {
                var contact = engine.Record(new PhysicalContactRequest { CitizenA = (uint)i + 1, CitizenB = (uint)i + 2,
                    EndTime = start.AddMinutes(5), DurationMinutes = 5, Context = (PhysicalContactContext)(i % 10),
                    BuildingId = (ushort)(i % 100), VehicleId = (ushort)(i % 10),
                    PositionX = i * 0.123f, PositionY = -i * 0.432f, PositionZ = -0.1f,
                    Distance = i % 2 == 0 ? (double?)null : 0.0123456789123 }, out bool created);
                contact.TraceableByApp = i % 2 == 0; contact.TraceableByManual = i % 3 == 0;
                contacts.Add(contact);
            }
            string from = start.ToString("o"), to = start.AddMinutes(5).ToString("o");
            Func<bool, byte[]> render = optimized =>
            {
                var current = new ContactCsvWriter();
                using (var stream = new System.IO.MemoryStream())
                using (var writer = new System.IO.StreamWriter(stream))
                {
                    foreach (var c in contacts)
                    {
                        if (optimized) current.Write(writer, writer, c, 4, from, to);
                        else
                        {
                            legacy(writer, new object[] { c.ContactId, from, to, c.DurationMinutes.ToString("R", invariant), c.CitizenA, c.CitizenB, c.Context,
                                c.BuildingId, c.VehicleId, 4, ((double)c.PositionX).ToString("R", invariant), ((double)c.PositionY).ToString("R", invariant),
                                ((double)c.PositionZ).ToString("R", invariant), c.Distance?.ToString("R", invariant) ?? "", c.TraceableByApp ? 1 : 0, c.TraceableByManual ? 1 : 0 });
                            if (c.TraceableByApp || c.TraceableByManual) legacy(writer, new object[] { c.ContactId, from, to, c.DurationMinutes.ToString("R", invariant),
                                c.CitizenA, c.CitizenB, c.Context, c.BuildingId, c.VehicleId, 4, c.TraceableByApp ? 1 : 0, c.TraceableByManual ? 1 : 0 });
                        }
                    }
                    writer.Flush(); return stream.ToArray();
                }
            };
            var originalCulture = System.Threading.Thread.CurrentThread.CurrentCulture;
            try
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
                CollectionAssert.AreEqual(render(false), render(true));
                long before = 0, after = 0;
                if (AllocatedBytes != null)
                {
                    long mark = AllocatedBytes(); render(false); before = AllocatedBytes() - mark;
                    mark = AllocatedBytes(); render(true); after = AllocatedBytes() - mark;
                    Assert.That(after, Is.LessThan(before));
                }
                double previousMs = Measure(() => render(false));
                double currentMs = Measure(() => render(true));
                TestContext.WriteLine("Buffered contact CSV 10000 contacts, identical bytes: {0:F3}->{1:F3}ms; allocations {2}->{3}", previousMs, currentMs, before, after);
            }
            finally { System.Threading.Thread.CurrentThread.CurrentCulture = originalCulture; }
        }

        [Test]
        public void CsvEscapeBufferReusePreservesBytesAndReducesAllocations()
        {
            var method = typeof(ExperimentRecorder).GetMethod("WriteCsvRowCore",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var current = (Action<System.IO.StreamWriter, object[]>)Delegate.CreateDelegate(
                typeof(Action<System.IO.StreamWriter, object[]>), method);
            Action<System.IO.StreamWriter, object[]> baseline = (writer, values) =>
            {
                for (int i = 0; i < values.Length; i++)
                {
                    if (i > 0) writer.Write(',');
                    string value = Convert.ToString(values[i], System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
                    if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0)
                    {
                        writer.Write('"'); writer.Write(value.Replace("\"", "\"\"")); writer.Write('"');
                    }
                    else writer.Write(value);
                }
                writer.WriteLine();
            };
            object[] row = { 1L, "2042-04-03T12:00:00.0000000Z", "2042-04-03T12:05:00.0000000Z", 5d,
                1u, 2u, "Workplace", 7, 0, 4, 1.25, -2.5, 0, "quoted,\"line\"\r\nnext", 1, 0 };
            Func<Action<System.IO.StreamWriter, object[]>, byte[]> render = write =>
            {
                using (var stream = new System.IO.MemoryStream())
                using (var writer = new System.IO.StreamWriter(stream))
                {
                    for (int i = 0; i < 10000; i++) write(writer, row);
                    writer.Flush();
                    return stream.ToArray();
                }
            };
            CollectionAssert.AreEqual(render(baseline), render(current));
            long beforeBytes = 0, afterBytes = 0;
            if (AllocatedBytes != null)
            {
                long start = AllocatedBytes(); render(baseline); beforeBytes = AllocatedBytes() - start;
                start = AllocatedBytes(); render(current); afterBytes = AllocatedBytes() - start;
                Assert.That(afterBytes, Is.LessThan(beforeBytes));
            }
            double before = Measure(() => render(baseline));
            double after = Measure(() => render(current));
            TestContext.WriteLine("CSV 10000 rows, identical bytes: baseline={0:F3}ms current={1:F3}ms allocations={2}->{3}",
                before, after, beforeBytes, afterBytes);
        }

        [TestCase(1000)] [TestCase(10000)]
        public void BenchmarkTransmissionPreservesAttribution(int targets)
        {
            var exposures = new List<TransmissionExposure<int>>();
            for (uint i = 1; i <= targets; i++)
                for (uint source = 1; source <= 4; source++)
                    exposures.Add(new TransmissionExposure<int> { SourceCitizenId = source, TargetCitizenId = i + 10, Probability = i % 13 == 0 ? 1d : 0.1 * source, StableContextKey = source, Context = (int)source });
            var original = new Reference.BaselineTransmissionEngine();
            var current = new TransmissionEngine();
            Func<ResolvedTransmission<int>, string> key = e => e.SourceCitizenId + ":" + e.TargetCitizenId + ":" + e.CombinedProbability.ToString("R") + ":" + e.Context;
            CollectionAssert.AreEqual(original.Resolve(exposures, new Random(42)).Select(key), current.Resolve(exposures, new Random(42)).Select(key));
            double before = Measure(() => { for (int i = 0; i < 10; i++) original.Resolve(exposures, new Random(i)); });
            double after = Measure(() => { for (int i = 0; i < 10; i++) current.Resolve(exposures, new Random(i)); });
            TestContext.WriteLine("Transmission targets={0} repetitions=10: baseline={1:F3}ms current={2:F3}ms", targets, before, after);
        }

        [TestCase(1000)] [TestCase(10000)]
        public void BenchmarkPhysicalContactRequestReusePreservesAllFields(int count)
        {
            var start = new DateTime(2030, 1, 1);
            Func<bool, List<PhysicalContactEvent>> record = reuse =>
            {
                var engine = new ContactEngine();
                engine.SetRetainHistory(false);
                var result = new List<PhysicalContactEvent>();
                var request = new PhysicalContactRequest();
                for (int i = 0; i < count; i++)
                {
                    if (!reuse) request = new PhysicalContactRequest();
                    request.CitizenA = (uint)(i + 1); request.CitizenB = (uint)(i + 2);
                    request.EndTime = start.AddMinutes(5 * (1 + i / 100)); request.DurationMinutes = 5;
                    request.Context = PhysicalContactContext.Outdoor; request.BuildingId = (ushort)(i % 100);
                    request.VehicleId = (ushort)(i % 10); request.PositionX = i; request.PositionY = 2 * i; request.PositionZ = -i;
                    request.Distance = i % 2 == 0 ? (double?)2 : null;
                    bool created;
                    result.Add(engine.Record(request, out created));
                }
                return result;
            };
            var serializer = new RealTime.Experiments.ExperimentJsonSerializer();
            CollectionAssert.AreEqual(record(false).Select(e => serializer.Serialize(e)), record(true).Select(e => serializer.Serialize(e)));
            double before = Measure(() => { for (int i = 0; i < 10; i++) record(false); });
            double after = Measure(() => { for (int i = 0; i < 10; i++) record(true); });
            TestContext.WriteLine("Physical contacts={0} repetitions=10: fresh request={1:F3}ms reused request={2:F3}ms", count, before, after);
        }

        [TestCase(1000)] [TestCase(10000)]
        public void BenchmarkSyntheticEpidemicStep(int population)
        {
            var start = new DateTime(2030, 1, 1);
            var contacts = new List<SyntheticContact>();
            for (uint i = 2; i <= population; i++) contacts.Add(new SyntheticContact { CitizenA = 1, CitizenB = i, TransmissionProbability = 0.1, Context = PhysicalContactContext.Workplace });
            Func<int> run = () =>
            {
                var core = new SyntheticEpidemicCore(DiseaseProgressionEngineTests.CreatePolicy(RealTime.Config.PandemicDistributionType.Deterministic), 42, 43);
                core.RegisterCourse(new DiseaseCourse(1, start, start.AddDays(1), start.AddDays(4), null, null, start.AddDays(7), false, DiseaseExposureKind.InitialSeed), start.AddDays(2));
                for (uint i = 2; i <= population; i++) core.RegisterSusceptible(i);
                int count = core.Step(start.AddDays(2), contacts);
                Assert.That(core.TransmissionEvents.Count, Is.EqualTo(count));
                return count;
            };
            int expected = run();
            double elapsed = Measure(() => Assert.That(run(), Is.EqualTo(expected)));
            TestContext.WriteLine("Synthetic population={0}: setup+step={1:F3}ms secondary={2}", population, elapsed, expected);
        }

        private static Func<long> CreateAllocationCounter()
        {
            var method = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", Type.EmptyTypes);
            return method == null ? null : (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), method);
        }
        [TestCase(20)]
        [TestCase(101)]
        [TestCase(1000)]
        public void SamplingPreservesBaselineEventOrder(int count)
        {
            var baseline = new Reference.ContactSamplingEngine();
            var current = new ContactSamplingEngine();
            var ids = Enumerable.Range(1, count).Select(i => (uint)i).Reverse().ToArray();
            for (int step = 0; step < 30; step++)
            {
                var expected = baseline.SampleBounded(ids, 10, 123, step, -7);
                var actual = current.SampleBounded(ids, 10, 123, step, -7);
                CollectionAssert.AreEqual(expected.Select(p => p.CitizenA + ":" + p.CitizenB), actual.Select(p => p.CitizenA + ":" + p.CitizenB));
            }
        }

        [TestCase(1000)]
        [TestCase(5000)]
        public void TestingPreservesBaselineResultsAndTimes(int count)
        {
            var start = new DateTime(2030, 1, 1);
            var policy = new PandemicTestingPolicy { Population = count, RelativeCapacityPercentPerSevenDays = 100, ReservedForSymptomaticPercent = 40, MaximumRequestToSampleDays = 10, ResultDelayDays = 0.5, SensitivityPercent = 73, SpecificityPercent = 94 };
            var baseline = new Reference.BaselineTestingEngine(policy, new Random(42), start);
            var current = new TestingEngine(policy, new Random(42), start);
            for (uint id = 1; id <= count; id++)
            {
                var priority = id % 3 == 0 ? PandemicTestPriority.Symptomatic : PandemicTestPriority.Routine;
                baseline.RequestTest(id, start, priority, PandemicTestReason.Screening);
                current.RequestTest(id, start, priority, PandemicTestReason.Screening);
            }
            Func<uint, PandemicTestSampleContext> sample = id => new PandemicTestSampleContext { DiseaseState = id % 2 == 0 ? DiseaseState.Infectious : DiseaseState.Susceptible, ExposureTime = start.AddDays(-3) };
            double before = Measure(() => { for (int day = 0; day < 12; day++) baseline.Advance(start.AddDays(day), sample); });
            double after = Measure(() => { for (int day = 0; day < 12; day++) current.Advance(start.AddDays(day), sample); });
            CollectionAssert.AreEqual(baseline.Records.Select(Key), current.Records.Select(Key));
            TestContext.WriteLine("Testing population={0}: baseline={1:F3}ms current={2:F3}ms", count, before, after);
        }

        [TestCase(20, 1000)]
        [TestCase(1000, 100)]
        [TestCase(10000, 20)]
        public void BenchmarkSampling(int count, int repetitions)
        {
            var ids = Enumerable.Range(1, count).Select(i => (uint)i).ToArray();
            var baseline = new Reference.ContactSamplingEngine();
            var current = new ContactSamplingEngine();
            baseline.SampleBounded(ids, 10, 1, 0, 1);
            current.SampleBounded(ids, 10, 1, 0, 1);
            double before = Measure(() => { for (int i = 0; i < repetitions; i++) baseline.SampleBounded(ids, 10, 1, i, 1); });
            double after = Measure(() => { for (int i = 0; i < repetitions; i++) current.SampleBounded(ids, 10, 1, i, 1); });
            TestContext.WriteLine("Sampling n={0} repetitions={1}: baseline={2:F3}ms current={3:F3}ms", count, repetitions, before, after);
        }

        private static string Key(PandemicTestRecord r) => string.Join("|", new object[] { r.TestId, r.CitizenId, r.State, r.Result, r.ScheduledAt, r.SampleTakenAt, r.ResultAvailableAt });

        private static double Measure(Action action)
        {
            GC.Collect();
            var timer = Stopwatch.StartNew();
            long bytes = AllocatedBytes == null ? 0 : AllocatedBytes();
            action();
            double elapsed = timer.Elapsed.TotalMilliseconds;
            TestContext.WriteLine("Measurement: {0:F3}ms; allocated bytes={1}", elapsed, AllocatedBytes == null ? "unavailable on this runtime" : (AllocatedBytes() - bytes).ToString());
            return elapsed;
        }
    }
}
