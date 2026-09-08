namespace RealTimeTests.Pandemic
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using NUnit.Framework;
    using RealTime.Pandemic;

    // Explicit because this writes a full synthetic day. Run before changing the recorder.
    [TestFixture]
    public sealed class ContactExportBaselineTests
    {
        [TestCase(20)]
        [TestCase(100)]
        [TestCase(1000)]
        [Explicit("Synthetic full-day disk benchmark")]
        public void MeasureLegacyDay(int population)
        {
            string directory = Path.Combine(TestContext.CurrentContext.WorkDirectory,
                "contact-baseline-" + population + "-" + Guid.NewGuid().ToString("N"));
            var start = new DateTime(2030, 1, 1);
            var sampler = new ContactSamplingEngine();
            var engine = new ContactEngine();
            engine.SetRetainHistory(false);
            var tracing = new ContactTracingEngine(new ContactTracingPolicy
                { AppAdoptionPercent = 60, ManualTraceabilityPercent = 80 }, 42);
            var metrics = new ContactNetworkMetrics();
            metrics.Begin(start);
            var ids = Enumerable.Range(1, population).Select(i => (uint)i).ToArray();
            var pairs = new HashSet<ulong>();
            long rows = 0, traceable = 0, generationTicks = 0, engineTicks = 0, tracingTicks = 0, metricsTicks = 0, recorderTicks = 0;
            var total = Stopwatch.StartNew();
            using (var recorder = new Reference.LegacyExperimentRecorder())
            {
                recorder.BeginRun(start, directory);
                for (int step = 1; step <= 288; step++)
                {
                    long before = Stopwatch.GetTimestamp();
                    var sampled = sampler.SampleBounded(ids, 10, 42, start.AddMinutes(step * 5).Ticks, 7);
                    generationTicks += Stopwatch.GetTimestamp() - before;
                    foreach (var pair in sampled)
                    {
                        before = Stopwatch.GetTimestamp();
                        bool created;
                        var contact = engine.Record(new PhysicalContactRequest { CitizenA = pair.CitizenA,
                            CitizenB = pair.CitizenB, EndTime = start.AddMinutes(step * 5), DurationMinutes = 5,
                            Context = PhysicalContactContext.Workplace, BuildingId = 7 }, out created);
                        engineTicks += Stopwatch.GetTimestamp() - before;
                        Assert.That(created, Is.True);
                        before = Stopwatch.GetTimestamp();
                        var flags = tracing.Evaluate(contact);
                        contact.TraceableByApp = flags.TraceableByApp;
                        contact.TraceableByManual = flags.TraceableByManual;
                        tracingTicks += Stopwatch.GetTimestamp() - before;
                        before = Stopwatch.GetTimestamp();
                        metrics.Record(contact, 3, 3);
                        metricsTicks += Stopwatch.GetTimestamp() - before;
                        before = Stopwatch.GetTimestamp();
                        recorder.RecordPhysicalContact(contact, 1, 3, 3);
                        recorderTicks += Stopwatch.GetTimestamp() - before;
                        rows++;
                        if (flags.IsTraceable) traceable++;
                        pairs.Add(((ulong)pair.CitizenA << 32) | pair.CitizenB);
                    }
                }
                long closeStart = Stopwatch.GetTimestamp();
                recorder.Freeze(start.AddDays(1));
                TestContext.WriteLine("finalize_ms=" + Milliseconds(Stopwatch.GetTimestamp() - closeStart));
            }
            total.Stop();
            long physicalBytes = new FileInfo(Path.Combine(directory, ExperimentRecorder.PhysicalContactsFileName)).Length;
            long traceableBytes = new FileInfo(Path.Combine(directory, ExperimentRecorder.TraceableContactsFileName)).Length;
            TestContext.WriteLine("population={0} rows_step={1} rows_day={2} participations_person_day={3} unique_person_day={4} repeated_ratio={5:R} traceable_percent={6:R} physical_bytes={7} traceable_bytes={8} bytes_per_physical_row={9:R}",
                population, rows / 288, rows, 2d * rows / population, 2d * pairs.Count / population,
                1d - pairs.Count / (double)rows, 100d * traceable / rows, physicalBytes, traceableBytes, physicalBytes / (double)rows);
            TestContext.WriteLine("generation_ms={0} engine_ms={1} tracing_ms={2} metrics_ms={3} recorder_including_metrics_ms={4} total_ms={5} output={6}",
                Milliseconds(generationTicks), Milliseconds(engineTicks), Milliseconds(tracingTicks), Milliseconds(metricsTicks), Milliseconds(recorderTicks), total.Elapsed.TotalMilliseconds, directory);
        }

        private static double Milliseconds(long ticks) => ticks * 1000d / Stopwatch.Frequency;
    }
}
