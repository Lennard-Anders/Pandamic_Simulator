namespace RealTimeTests.Pandemic
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using NUnit.Framework;
    using RealTime.Config;
    using RealTime.Pandemic;

    [TestFixture]
    public sealed class ContactExportBenchmarkTests
    {
        [Test]
        public void PairHashOptimizationPreservesEveryNetworkAggregate()
        {
            var start = new DateTime(2030, 1, 1);
            var previous = new Reference.LegacyContactNetworkMetrics();
            var current = new ContactNetworkMetrics();
            previous.Begin(start); current.Begin(start);
            var population = new PandemicPopulationEvent { SimulationTime = start, Action = "InitialPopulation", Count = 100 };
            previous.Population(population); current.Population(population);
            var sampler = new ContactSamplingEngine();
            var ids = Enumerable.Range(1, 100).Select(i => (uint)i).ToArray();
            for (int step = 1; step <= 580; step++)
                foreach (var pair in sampler.SampleBounded(ids, 10, 42, step, 7))
                {
                    var contact = new PhysicalContactEvent { CitizenA = pair.CitizenA, CitizenB = pair.CitizenB,
                        StartTime = start.AddMinutes(step * 5 - 5), EndTime = start.AddMinutes(step * 5), DurationMinutes = 5,
                        Context = PhysicalContactContext.Workplace };
                    previous.Record(contact, 2, 3); current.Record(contact, 2, 3);
                }
            previous.Complete(start.AddMinutes(2900)); current.Complete(start.AddMinutes(2900));
            Assert.That(current.SummaryCsv, Is.EqualTo(previous.SummaryCsv));
            Assert.That(current.DegreeCsv, Is.EqualTo(previous.DegreeCsv));
            Assert.That(current.DurationCsv(), Is.EqualTo(previous.DurationCsv()));
            Assert.That(current.HourCsv(), Is.EqualTo(previous.HourCsv()));
            Assert.That(current.AgeMixingCsv(), Is.EqualTo(previous.AgeMixingCsv()));
        }

        [TestCase(20)] [TestCase(100)] [TestCase(1000)]
        [Explicit("Full-day measured disk workload")]
        public void CompareLegacyAndContactModes(int population)
        {
            TestContext.WriteLine("population,variant,raw_steps,episodes,unique_pairs,participations_person_day,unique_person_day,repeated_ratio,stored_contact_bytes,uncompressed_contact_bytes,sampling_ms,engine_ms,tracing_ms,recorder_ms,finalize_ms,total_ms,allocated_bytes");
            Run(population, "Legacy", ScientificContactExportMode.FullRaw, false, true);
            Run(population, "FullRawLegacyMixing", ScientificContactExportMode.FullRaw, false, false);
            Run(population, "StandardLegacyMixing", ScientificContactExportMode.Standard, false, false);
            Run(population, "FullRawPersistent", ScientificContactExportMode.FullRaw, true, false);
            Run(population, "StandardPersistent", ScientificContactExportMode.Standard, true, false);
            Run(population, "SummaryPersistent", ScientificContactExportMode.SummaryOnly, true, false);
        }

        private static void Run(int population, string variant, ScientificContactExportMode mode, bool persistent, bool legacy)
        {
            string directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "benchmark-" + population + "-" + variant + "-" + Guid.NewGuid().ToString("N"));
            var start = new DateTime(2030, 1, 1);
            var config = new RealTimeConfig(true) { ContactPersistenceModel = persistent ? ContactPersistenceModel.ContextWindows : ContactPersistenceModel.LegacyPerStep };
            var sampler = new ContactSamplingEngine();
            var engine = new ContactEngine();
            engine.SetRetainHistory(false);
            var tracing = new ContactTracingEngine(new ContactTracingPolicy { AppAdoptionPercent = 60, ManualTraceabilityPercent = 80 }, 42);
            var ids = Enumerable.Range(1, population).Select(i => (uint)i).ToArray();
            var unique = new HashSet<ulong>();
            var oldRecorder = legacy ? new Reference.LegacyExperimentRecorder() : null;
            var recorder = legacy ? null : new ExperimentRecorder();
            long samplingTicks = 0, engineTicks = 0, tracingTicks = 0, recordingTicks = 0, steps = 0;
            var allocationMethod = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", Type.EmptyTypes);
            var allocations = allocationMethod == null ? null : (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), allocationMethod);
            long allocatedStart = allocations == null ? 0 : allocations();
            var total = Stopwatch.StartNew();
            if (legacy) oldRecorder.BeginRun(start, directory); else recorder.BeginRun(start, directory, mode);
            var populationEvent = new PandemicPopulationEvent { SimulationTime = start, Action = "InitialPopulation", Count = population };
            if (legacy) oldRecorder.RecordPopulation(populationEvent); else recorder.RecordPopulation(populationEvent);
            for (int step = 1; step <= 288; step++)
            {
                DateTime end = start.AddMinutes(step * 5);
                long tick = Stopwatch.GetTimestamp();
                recorder?.BeginContactStep(end);
                recordingTicks += Stopwatch.GetTimestamp() - tick;
                tick = Stopwatch.GetTimestamp();
                var pairs = sampler.SampleBounded(ids, 10, 42, ContactPersistencePolicy.SamplingKey(config, PhysicalContactContext.Workplace, end), 7);
                samplingTicks += Stopwatch.GetTimestamp() - tick;
                foreach (var pair in pairs)
                {
                    tick = Stopwatch.GetTimestamp();
                    var contact = engine.Record(new PhysicalContactRequest { CitizenA = pair.CitizenA, CitizenB = pair.CitizenB,
                        EndTime = end, DurationMinutes = 5, Context = PhysicalContactContext.Workplace, BuildingId = 7 }, out bool created);
                    engineTicks += Stopwatch.GetTimestamp() - tick;
                    tick = Stopwatch.GetTimestamp();
                    var flags = tracing.Evaluate(contact);
                    contact.TraceableByApp = flags.TraceableByApp; contact.TraceableByManual = flags.TraceableByManual;
                    tracingTicks += Stopwatch.GetTimestamp() - tick;
                    tick = Stopwatch.GetTimestamp();
                    if (legacy) oldRecorder.RecordPhysicalContact(contact, 1, 3, 3); else recorder.RecordPhysicalContact(contact, 1, 3, 3);
                    recordingTicks += Stopwatch.GetTimestamp() - tick;
                    steps++;
                    unique.Add(((ulong)pair.CitizenA << 32) | pair.CitizenB);
                }
                tick = Stopwatch.GetTimestamp();
                recorder?.EndContactStep();
                recordingTicks += Stopwatch.GetTimestamp() - tick;
            }
            long finalStart = Stopwatch.GetTimestamp();
            var snapshot = legacy ? oldRecorder.Freeze(start.AddDays(1)) : recorder.Freeze(start.AddDays(1));
            recorder?.Dispose(); oldRecorder?.Dispose();
            double finalizeMs = Ms(Stopwatch.GetTimestamp() - finalStart);
            total.Stop();
            long allocatedBytes = allocations == null ? -1 : allocations() - allocatedStart;
            long storedBytes = 0, uncompressedBytes = 0;
            foreach (string file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(file) == "transmission_events.csv") continue;
                storedBytes += new FileInfo(file).Length;
                if (file.EndsWith(".gz", StringComparison.Ordinal))
                {
                    using (var input = File.OpenRead(file))
                    using (var gzip = new GZipStream(input, CompressionMode.Decompress))
                    {
                        var buffer = new byte[65536];
                        int length;
                        while ((length = gzip.Read(buffer, 0, buffer.Length)) > 0) uncompressedBytes += length;
                    }
                }
                else uncompressedBytes += new FileInfo(file).Length;
            }
            Assert.That(steps, Is.EqualTo(population * 5L * 288));
            TestContext.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "{0},{1},{2},{3},{4},{5:R},{6:R},{7:R},{8},{9},{10:F3},{11:F3},{12:F3},{13:F3},{14:F3},{15:F3},{16}",
                population, variant, steps, legacy ? -1 : snapshot.ContactEpisodesTotal, unique.Count, 2d * steps / population,
                2d * unique.Count / population, 1d - unique.Count / (double)steps, storedBytes, uncompressedBytes,
                Ms(samplingTicks), Ms(engineTicks), Ms(tracingTicks), Ms(recordingTicks), finalizeMs, total.Elapsed.TotalMilliseconds, allocatedBytes));
            File.WriteAllText(Path.Combine(directory, "episode_summary.csv"), snapshot.ContactEpisodeSummaryCsv ?? "legacy\n");
        }

        private static double Ms(long ticks) => ticks * 1000d / Stopwatch.Frequency;

        [Test, Explicit("Separates CSV formatting and filesystem API timing")]
        public void MeasureSerializationAndFilesystemCalls()
        {
            const int count = 100000;
            var start = new DateTime(2030, 1, 1);
            var contact = new PhysicalContactEvent { CitizenA = 1, CitizenB = 2, StartTime = start,
                EndTime = start.AddMinutes(5), DurationMinutes = 5, Context = PhysicalContactContext.Workplace,
                BuildingId = 7, TraceableByApp = true };
            var formatter = new ContactCsvWriter();
            var timer = Stopwatch.StartNew();
            using (var sink = new StreamWriter(Stream.Null, new System.Text.UTF8Encoding(false), 65536))
                for (int row = 1; row <= count; row++)
                {
                    contact.ContactId = row;
                    formatter.Write(sink, null, contact, 0, "2030-01-01T00:00:00.0000000", "2030-01-01T00:05:00.0000000");
                }
            timer.Stop();
            TestContext.WriteLine("csv_format_encode_null_sink_ms=" + timer.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture));
            foreach (bool gzip in new[] { false, true })
            {
                string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "io-breakdown-" + Guid.NewGuid().ToString("N") + (gzip ? ".csv.gz" : ".csv"));
                var measured = new TimedFileStream(path);
                timer.Restart();
                using (var writer = new StreamWriter(gzip ? (Stream)new GZipStream(measured, CompressionMode.Compress) : measured,
                    new System.Text.UTF8Encoding(false), 65536))
                    for (int row = 1; row <= count; row++)
                    {
                        contact.ContactId = row;
                        formatter.Write(writer, null, contact, 0, "2030-01-01T00:00:00.0000000", "2030-01-01T00:05:00.0000000");
                        if (row % (gzip ? 8192 : 256) == 0) writer.Flush();
                    }
                timer.Stop();
                TestContext.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "gzip={0},rows={1},bytes={2},total_ms={3:F3},filesystem_write_ms={4:F3},filesystem_flush_close_ms={5:F3}",
                    gzip, count, new FileInfo(path).Length, timer.Elapsed.TotalMilliseconds, Ms(measured.WriteTicks), Ms(measured.FlushTicks)));
            }
        }

        private sealed class TimedFileStream : Stream
        {
            private readonly FileStream file;
            internal long WriteTicks;
            internal long FlushTicks;
            internal TimedFileStream(string path) { file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536); }
            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => file.Length;
            public override long Position { get => file.Position; set => throw new NotSupportedException(); }
            public override void Write(byte[] buffer, int offset, int count)
            {
                long tick = Stopwatch.GetTimestamp();
                try { file.Write(buffer, offset, count); } finally { WriteTicks += Stopwatch.GetTimestamp() - tick; }
            }
            public override void Flush()
            {
                long tick = Stopwatch.GetTimestamp();
                try { file.Flush(); } finally { FlushTicks += Stopwatch.GetTimestamp() - tick; }
            }
            protected override void Dispose(bool disposing)
            {
                long tick = Stopwatch.GetTimestamp();
                try { if (disposing) file.Dispose(); } finally { FlushTicks += Stopwatch.GetTimestamp() - tick; base.Dispose(disposing); }
            }
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
        }

        [Test]
        [Explicit("Buffered gzip throughput comparison")]
        public void CompareSafeFlushThresholds()
        {
            var start = new DateTime(2030, 1, 1);
            TestContext.WriteLine("flush_rows,raw_rows,compressed_bytes,write_and_close_ms");
            foreach (int threshold in new[] { 256, 8192, 65536 })
            {
                string directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "flush-benchmark-" + threshold + "-" + Guid.NewGuid().ToString("N"));
                var timer = Stopwatch.StartNew();
                long bytes;
                using (var storage = new ScientificContactStorage(directory, start, ScientificContactExportMode.FullRaw, flushRows: threshold))
                {
                    for (int row = 0; row < 100000; row++)
                        storage.Record(new PhysicalContactEvent { ContactId = row + 1, CitizenA = (uint)(row % 1000 + 1), CitizenB = (uint)(row % 1000 + 1001),
                            StartTime = start, EndTime = start.AddMinutes(5), DurationMinutes = 5, Context = PhysicalContactContext.Workplace, BuildingId = 7,
                            TraceableByApp = row % 2 == 0 }, 1);
                    storage.Complete();
                    bytes = storage.StoredBytes;
                }
                timer.Stop();
                TestContext.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0},100000,{1},{2:F3}", threshold, bytes, timer.Elapsed.TotalMilliseconds));
            }
        }
    }
}
