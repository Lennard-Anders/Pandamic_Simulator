namespace RealTimeTests.Pandemic
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using NUnit.Framework;
    using RealTime.Config;
    using RealTime.Pandemic;

    internal sealed class ContactThirtyDayExportTests
    {
        [TestCase(ScientificContactExportMode.Standard)]
        [TestCase(ScientificContactExportMode.FullRaw)]
        [TestCase(ScientificContactExportMode.SummaryOnly)]
        [Explicit("Thirty-day synthetic export and partition validation")]
        public void ThirtyDaysPreserveEveryObservedStep(ScientificContactExportMode mode)
        {
            var start = new DateTime(2030, 1, 1);
            string directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "thirty-day-" + mode + "-" + Guid.NewGuid().ToString("N"));
            var config = new RealTimeConfig(true);
            var sampler = new ContactSamplingEngine();
            var ids = Enumerable.Range(1, 100).Select(i => (uint)i).ToArray();
            var timer = Stopwatch.StartNew();
            using (var recorder = new ExperimentRecorder())
            {
                recorder.BeginRun(start, directory, mode);
                long contactId = 0;
                for (int step = 1; step <= 30 * 288; step++)
                {
                    DateTime end = start.AddMinutes(step * 5);
                    DateTime begin = end.AddMinutes(-5);
                    recorder.BeginContactStep(end);
                    if (begin.Hour >= 8 && begin.Hour < 16)
                        foreach (var pair in sampler.SampleBounded(ids, 10, 42, ContactPersistencePolicy.SamplingKey(config, PhysicalContactContext.Workplace, end), 7))
                            recorder.RecordPhysicalContact(new PhysicalContactEvent { ContactId = ++contactId,
                                CitizenA = pair.CitizenA, CitizenB = pair.CitizenB, StartTime = begin, EndTime = end,
                                DurationMinutes = 5, Context = PhysicalContactContext.Workplace, BuildingId = 7 }, 0);
                    recorder.EndContactStep();
                }
                var snapshot = recorder.Freeze(start.AddDays(30));
                timer.Stop();
                Assert.That(snapshot.PhysicalContactsTotal, Is.EqualTo(1440000));
                Assert.That(snapshot.ContactEpisodesTotal, Is.LessThan(120000));
                Assert.That(snapshot.ContactFiles.Count, Is.EqualTo(mode == ScientificContactExportMode.FullRaw ? 30 : mode == ScientificContactExportMode.Standard ? 1 : 0));
                long rows = 0, representedSteps = 0;
                foreach (var file in snapshot.ContactFiles)
                {
                    long fileRows = 0;
                    using (var input = File.OpenRead(Path.Combine(directory, file.RelativePath)))
                    using (var gzip = new GZipStream(input, CompressionMode.Decompress))
                    using (var reader = new StreamReader(gzip))
                    {
                        reader.ReadLine();
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            fileRows++;
                            representedSteps += mode == ScientificContactExportMode.Standard ? long.Parse(line.Split(',')[12], CultureInfo.InvariantCulture) : 1;
                        }
                    }
                    Assert.That(fileRows, Is.EqualTo(file.RowCount));
                    rows += fileRows;
                }
                Assert.That(representedSteps, Is.EqualTo(mode == ScientificContactExportMode.SummaryOnly ? 0 : 1440000));
                Assert.That(Directory.GetFiles(directory, "*.tmp", SearchOption.AllDirectories), Is.Empty);
                TestContext.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "mode={0},days=30,population=100,occupied_hours_day=8,raw_steps={1},episodes={2},stored_rows={3},compressed_bytes={4},write_finalize_ms={5:F3}",
                    mode, snapshot.PhysicalContactsTotal, snapshot.ContactEpisodesTotal, rows,
                    snapshot.ContactFiles.Sum(file => file.LengthBytes), timer.Elapsed.TotalMilliseconds));
            }
        }
    }
}
