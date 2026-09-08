namespace RealTimeTests.Pandemic
{
    using System;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Security.Cryptography;
    using NUnit.Framework;
    using RealTime.Config;
    using RealTime.Pandemic;

    public sealed class ScientificContactStorageTests
    {
        private static readonly DateTime Start = new DateTime(2030, 1, 1);
        private string directory;

        [SetUp]
        public void Setup() => directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "contact-storage-test-" + Guid.NewGuid().ToString("N"));

        [Test]
        public void RawRoundtripPartitionsHashesCountsAndTemporaryLifecycle()
        {
            using (var storage = new ScientificContactStorage(directory, Start, ScientificContactExportMode.FullRaw))
            {
                storage.Record(Contact(Start.AddMinutes(5)), 3);
                storage.Record(Contact(Start.AddDays(1).AddMinutes(5)), 3);
                Assert.That(Directory.GetFiles(directory, "*.gz", SearchOption.AllDirectories), Is.Empty);
                Assert.That(Directory.GetFiles(directory, "*.tmp", SearchOption.AllDirectories), Has.Length.EqualTo(2));
                storage.Complete();
                Assert.That(storage.Files.Count, Is.EqualTo(2));
                Assert.That(Directory.GetFiles(directory, "*.tmp", SearchOption.AllDirectories), Is.Empty);
                foreach (var file in storage.Files)
                {
                    string path = Path.Combine(directory, file.RelativePath);
                    Assert.That(file.RowCount, Is.EqualTo(1));
                    Assert.That(file.LengthBytes, Is.EqualTo(new FileInfo(path).Length));
                    using (var hash = SHA256.Create())
                        Assert.That(file.Sha256, Is.EqualTo(BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant()));
                    string[] lines = Read(path).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    Assert.That(lines.Length, Is.EqualTo(2));
                    Assert.That(lines[0], Is.EqualTo(ScientificContactStorage.PhysicalHeader));
                    Assert.That(lines[1].EndsWith(",1,0"), Is.True);
                    Assert.That(file.EndTime.Value - file.StartTime.Value, Is.EqualTo(TimeSpan.FromMinutes(5)));
                }
                storage.Complete();
            }
        }

        [Test]
        public void StandardStoresOneContinuousEpisodeAndSummaryStoresNoContacts()
        {
            using (var storage = new ScientificContactStorage(directory, Start, ScientificContactExportMode.Standard))
            {
                var tracker = new ContactEpisodeTracker(storage.Record);
                for (int step = 1; step <= 4; step++)
                {
                    var contact = Contact(Start.AddMinutes(step * 5));
                    tracker.BeginStep(contact.EndTime);
                    tracker.Observe(contact, 3);
                    tracker.EndStep();
                }
                tracker.Complete();
                storage.Complete();
                Assert.That(storage.Files.Single().RowCount, Is.EqualTo(1));
                Assert.That(Read(Path.Combine(directory, ScientificContactStorage.EpisodeFileName)), Does.Contain(",20,Workplace,7,0,3,1,0,4"));
            }
            using (var storage = new ScientificContactStorage(directory + "-summary", Start, ScientificContactExportMode.SummaryOnly))
            {
                storage.Record(Contact(Start.AddMinutes(5)), 3);
                storage.Complete();
                Assert.That(storage.Files, Is.Empty);
                Assert.That(Directory.GetFiles(directory + "-summary"), Is.Empty);
            }
        }

        [Test]
        public void InterruptedStreamNeverPublishes()
        {
            using (var storage = new ScientificContactStorage(directory, Start, ScientificContactExportMode.FullRaw))
                storage.Record(Contact(Start.AddMinutes(5)), 0);
            Assert.That(Directory.GetFiles(directory, "*.tmp", SearchOption.AllDirectories), Has.Length.EqualTo(1));
            Assert.That(Directory.GetFiles(directory, "*.gz", SearchOption.AllDirectories), Is.Empty);
        }

        [Test]
        public void SizeLimitFailureCannotBecomeComplete()
        {
            var storage = new ScientificContactStorage(directory, Start, ScientificContactExportMode.FullRaw, 0.000000001);
            storage.Record(Contact(Start.AddMinutes(5)), 0);
            Assert.That(() => storage.Complete(), Throws.TypeOf<IOException>());
            Assert.That(() => storage.Complete(), Throws.InvalidOperationException);
            storage.Dispose();
            Assert.That(Directory.GetFiles(directory, "*.gz", SearchOption.AllDirectories), Is.Empty);
        }

        [Test]
        public void ExistingOutputIsNeverOverwritten()
        {
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, ScientificContactStorage.EpisodeFileName);
            File.WriteAllText(path, "preserve");
            Assert.That(() => new ScientificContactStorage(directory, Start, ScientificContactExportMode.Standard), Throws.TypeOf<IOException>());
            Assert.That(File.ReadAllText(path), Is.EqualTo("preserve"));
        }

        [Test]
        public void InsufficientMeasuredDiskReserveFailsBeforeOpeningOutput()
        {
            Assert.That(() => new ScientificContactStorage(directory, Start, ScientificContactExportMode.FullRaw,
                availableSpace: () => ScientificContactStorage.DiskReserveBytes - 1), Throws.TypeOf<IOException>());
            Assert.That(Directory.GetFiles(directory, "*", SearchOption.AllDirectories), Is.Empty);
        }

        [Test]
        public void DiskReserveExhaustedDuringWriteCannotPublish()
        {
            int checks = 0;
            var storage = new ScientificContactStorage(directory, Start, ScientificContactExportMode.FullRaw,
                availableSpace: () => ++checks == 1 ? ScientificContactStorage.DiskReserveBytes : 0);
            storage.Record(Contact(Start.AddMinutes(5)), 0);
            Assert.That(() => storage.Complete(), Throws.TypeOf<IOException>());
            Assert.That(() => storage.Complete(), Throws.InvalidOperationException);
            storage.Dispose();
            Assert.That(Directory.GetFiles(directory, "*.gz", SearchOption.AllDirectories), Is.Empty);
        }

        private static PhysicalContactEvent Contact(DateTime end) => new PhysicalContactEvent
        {
            ContactId = 1, CitizenA = 1, CitizenB = 2, StartTime = end.AddMinutes(-5), EndTime = end,
            DurationMinutes = 5, Context = PhysicalContactContext.Workplace, BuildingId = 7, TraceableByApp = true,
        };

        [TestCase(false)] [TestCase(true)]
        public void UnderlyingWriteOrFlushFailureClosesHandleAndCannotPublish(bool failFlush)
        {
            FailingStream output = null;
            var storage = new ScientificContactStorage(directory, Start, ScientificContactExportMode.FullRaw,
                openOutput: path => output = new FailingStream(File.Create(path), failFlush));
            storage.Record(Contact(Start.AddMinutes(5)), 0);
            Assert.That(() => storage.Complete(), Throws.TypeOf<IOException>());
            Assert.That(output.Disposed, Is.True);
            Assert.That(storage.HasFailed, Is.True);
            Assert.That(() => storage.Complete(), Throws.InvalidOperationException);
            storage.Dispose();
            Assert.That(Directory.GetFiles(directory, "*.gz", SearchOption.AllDirectories), Is.Empty);
        }

        [Test]
        public void RepeatedGzipRunsHaveIdenticalStoredBytes()
        {
            foreach (string path in new[] { directory, directory + "-again" })
                using (var storage = new ScientificContactStorage(path, Start, ScientificContactExportMode.FullRaw))
                {
                    storage.Record(Contact(Start.AddMinutes(5)), 0);
                    storage.Complete();
                }
            CollectionAssert.AreEqual(File.ReadAllBytes(Path.Combine(directory, "physical_contacts/day_000.csv.gz")),
                File.ReadAllBytes(Path.Combine(directory + "-again", "physical_contacts/day_000.csv.gz")));
        }

        private sealed class FailingStream : Stream
        {
            private readonly Stream output;
            private readonly bool failFlush;
            internal bool Disposed;
            internal FailingStream(Stream output, bool failFlush) { this.output = output; this.failFlush = failFlush; }
            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => output.Length;
            public override long Position { get => output.Position; set => throw new NotSupportedException(); }
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count)
            {
                if (!failFlush) throw new IOException("Injected write failure");
                output.Write(buffer, offset, count);
            }
            public override void Flush() { if (failFlush) throw new IOException("Injected flush failure"); output.Flush(); }
            protected override void Dispose(bool disposing)
            {
                if (disposing && !Disposed)
                {
                    Disposed = true;
                    try { Flush(); }
                    finally { output.Dispose(); }
                }
                base.Dispose(disposing);
            }
        }

        private static string Read(string path)
        {
            using (var input = File.OpenRead(path))
            using (var gzip = new GZipStream(input, CompressionMode.Decompress))
            using (var text = new StreamReader(gzip)) return text.ReadToEnd();
        }
    }
}
