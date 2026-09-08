namespace RealTime.Pandemic
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.IO.Compression;
    using System.Security.Cryptography;
    using System.Text;
    using RealTime.Config;

    internal sealed class ContactStorageFile
    {
        public string RelativePath { get; set; }
        public long LengthBytes { get; set; }
        public string Sha256 { get; set; }
        public long RowCount { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
    }

    /// <summary>Bounded, synchronous gzip storage. All files remain temporary until successful closure.</summary>
    internal sealed class ScientificContactStorage : IDisposable
    {
        internal const string EpisodeFileName = "contact_episodes.csv.gz";
        internal const string PhysicalHeader = "contact_id,start_time,end_time,duration_minutes,citizen_a,citizen_b,context,building_id,vehicle_id,district_id,position_x,position_y,position_z,distance,traceable_by_app,traceable_by_manual";
        internal const string EpisodeHeader = "episode_id,citizen_a,citizen_b,start_time,end_time,duration_minutes,context,building_id,vehicle_id,district_id,traceable_by_app,traceable_by_manual,number_of_epidemiological_steps";
        private readonly string directory;
        private readonly DateTime start;
        private readonly ScientificContactExportMode mode;
        private readonly long maximumBytes;
        private readonly int flushRows;
        private readonly List<ContactStorageFile> files = new List<ContactStorageFile>();
        private readonly ContactCsvWriter csv = new ContactCsvWriter();
        private StreamWriter writer;
        private Stream ownedOutput;
        private readonly Func<string, Stream> openOutput;
        private ContactStorageFile current;
        private long storedBytes;
        private long day = -1;
        private int pending;
        private DateTime lastFlush = DateTime.UtcNow;
        private bool complete;
        private bool failed;
        internal const long DiskReserveBytes = 100000000;
        private readonly Func<long?> availableSpace;
        private long bytesSinceDiskCheck;
        private long? lastAvailableBytes;
        private DateTime cachedStart;
        private DateTime cachedEnd;
        private string cachedStartIso;
        private string cachedEndIso;

        internal ScientificContactStorage(string directory, DateTime start, ScientificContactExportMode mode,
            double maximumGB = 0, int flushRows = 8192, Func<long?> availableSpace = null, Func<string, Stream> openOutput = null)
        {
            if (!Enum.IsDefined(typeof(ScientificContactExportMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            if (double.IsNaN(maximumGB) || double.IsInfinity(maximumGB) || maximumGB < 0 || maximumGB > long.MaxValue / 1e9)
                throw new ArgumentOutOfRangeException(nameof(maximumGB));
            if (flushRows < 1) throw new ArgumentOutOfRangeException(nameof(flushRows));
            this.directory = Path.GetFullPath(directory);
            this.start = start;
            this.mode = mode;
            this.flushRows = flushRows;
            this.openOutput = openOutput ?? (path => new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 65536));
            this.availableSpace = availableSpace ?? (() => RealTime.Experiments.ContactStorageEstimate.AvailableBytes(this.directory));
            maximumBytes = maximumGB == 0 ? 0 : Math.Max(1, (long)(maximumGB * 1e9));
            Directory.CreateDirectory(this.directory);
            CheckDiskSpace(0, true);
            if (mode == ScientificContactExportMode.Standard) Open(EpisodeFileName, EpisodeHeader);
            if (mode == ScientificContactExportMode.FullRaw) Rotate(0);
        }

        internal IList<ContactStorageFile> Files => files.AsReadOnly();
        internal long StoredBytes => storedBytes;
        internal bool HasFailed => failed;

        internal void Record(PhysicalContactEvent contact, byte district)
        {
            CheckWritable();
            if (mode != ScientificContactExportMode.FullRaw) return;
            if (contact.StartTime < start) throw new InvalidOperationException("Contact precedes run.");
            long nextDay = (contact.StartTime.Ticks - start.Ticks) / TimeSpan.TicksPerDay;
            if (nextDay < day) throw new InvalidOperationException("Raw partitions require monotonic contact time.");
            try
            {
                if (nextDay != day) Rotate(nextDay);
                if (cachedStartIso == null || cachedStart.ToBinary() != contact.StartTime.ToBinary())
                { cachedStart = contact.StartTime; cachedStartIso = contact.StartTime.ToString("o", CultureInfo.InvariantCulture); }
                if (cachedEndIso == null || cachedEnd.ToBinary() != contact.EndTime.ToBinary())
                { cachedEnd = contact.EndTime; cachedEndIso = contact.EndTime.ToString("o", CultureInfo.InvariantCulture); }
                csv.Write(writer, null, contact, district, cachedStartIso, cachedEndIso);
                Recorded(contact.StartTime, contact.EndTime);
            }
            catch { failed = true; throw; }
        }

        internal void Record(ContactEpisode episode)
        {
            CheckWritable();
            if (mode != ScientificContactExportMode.Standard) return;
            try
            {
                writer.WriteLine(string.Join(",", new[] { episode.EpisodeId.ToString(CultureInfo.InvariantCulture),
                    episode.CitizenA.ToString(CultureInfo.InvariantCulture), episode.CitizenB.ToString(CultureInfo.InvariantCulture),
                    episode.StartTime.ToString("o", CultureInfo.InvariantCulture), episode.EndTime.ToString("o", CultureInfo.InvariantCulture),
                    episode.DurationMinutes.ToString("R", CultureInfo.InvariantCulture), episode.Context.ToString(),
                    episode.BuildingId.ToString(CultureInfo.InvariantCulture), episode.VehicleId.ToString(CultureInfo.InvariantCulture),
                    episode.DistrictId.ToString(CultureInfo.InvariantCulture), episode.TraceableByApp ? "1" : "0",
                    episode.TraceableByManual ? "1" : "0", episode.EpidemiologicalSteps.ToString(CultureInfo.InvariantCulture) }));
                Recorded(episode.StartTime, episode.EndTime);
            }
            catch { failed = true; throw; }
        }

        internal void Complete()
        {
            if (complete) return;
            CheckWritable();
            try
            {
                Close(); // Disposes gzip and writes its footer before hashing or publishing.
                foreach (var file in files)
                {
                    string path = Path.Combine(directory, file.RelativePath);
                    if (File.Exists(path)) throw new IOException("Scientific contact output already exists: " + path);
                    string temporary = path + ".tmp";
                    file.LengthBytes = new FileInfo(temporary).Length;
                    using (var input = File.OpenRead(temporary))
                    using (var hash = SHA256.Create())
                        file.Sha256 = BitConverter.ToString(hash.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
                }
                foreach (var file in files)
                {
                    string path = Path.Combine(directory, file.RelativePath);
                    File.Move(path + ".tmp", path);
                }
                complete = true;
            }
            catch { failed = true; throw; }
        }

        public void Dispose()
        {
            // Interrupted data stays .tmp and cannot masquerade as a completed run.
            try
            {
                if (failed)
                {
                    writer = null; // Never retry serialization after a known write failure.
                    var abandoned = ownedOutput;
                    ownedOutput = null;
                    abandoned?.Dispose();
                }
                else Close();
            }
            finally { if (!complete) failed = true; }
        }

        private void Rotate(long nextDay)
        {
            Close();
            day = nextDay;
            Open("physical_contacts/day_" + day.ToString("D3", CultureInfo.InvariantCulture) + ".csv.gz", PhysicalHeader);
        }

        private void Open(string relativePath, string header)
        {
            string path = Path.Combine(directory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (File.Exists(path)) throw new IOException("Scientific contact output already exists: " + path);
            var stream = openOutput(path + ".tmp");
            ownedOutput = stream;
            try
            {
                writer = new StreamWriter(new GZipStream(new CountedStream(stream, ReserveBytes), CompressionMode.Compress), new UTF8Encoding(false), 65536);
                current = new ContactStorageFile { RelativePath = relativePath };
                files.Add(current);
                writer.WriteLine(header);
            }
            catch { stream.Dispose(); failed = true; throw; }
        }

        private void Recorded(DateTime first, DateTime last)
        {
            current.RowCount++;
            if (!current.StartTime.HasValue || first < current.StartTime.Value) current.StartTime = first;
            if (!current.EndTime.HasValue || last > current.EndTime.Value) current.EndTime = last;
            pending++;
            if (pending >= flushRows || DateTime.UtcNow - lastFlush >= TimeSpan.FromSeconds(30))
            {
                writer.Flush();
                pending = 0;
                lastFlush = DateTime.UtcNow;
            }
        }

        private void Close()
        {
            var closing = writer;
            writer = null;
            var output = ownedOutput;
            ownedOutput = null;
            try { if (closing != null) closing.Dispose(); }
            catch { failed = true; throw; }
            finally { output?.Dispose(); }
        }

        private void CheckWritable()
        {
            if (failed || complete) throw new InvalidOperationException("Contact storage is failed or finalized.");
        }

        private void ReserveBytes(int count)
        {
            CheckDiskSpace(count, false);
            if (maximumBytes != 0 && count > maximumBytes - storedBytes)
            {
                failed = true;
                throw new IOException("MaximumRawContactExportGB reached. Scientific run must be marked invalid; data was not silently truncated.");
            }
            storedBytes += count;
        }

        private void CheckDiskSpace(int count, bool force)
        {
            if (force || bytesSinceDiskCheck >= 1048576
                || (lastAvailableBytes.HasValue && lastAvailableBytes.Value - bytesSinceDiskCheck - count < DiskReserveBytes))
            {
                lastAvailableBytes = availableSpace();
                bytesSinceDiskCheck = 0;
            }
            if (lastAvailableBytes.HasValue && lastAvailableBytes.Value - bytesSinceDiskCheck - count < DiskReserveBytes)
            {
                failed = true;
                throw new IOException("Scientific contact export stopped: less than 100 MB disk reserve remains. The run must be marked failed; temporary data is preserved.");
            }
            bytesSinceDiskCheck += count;
        }

        private sealed class CountedStream : Stream
        {
            private readonly Stream output;
            private readonly Action<int> reserve;
            internal CountedStream(Stream output, Action<int> reserve) { this.output = output; this.reserve = reserve; }
            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => output.Length;
            public override long Position { get => output.Position; set => throw new NotSupportedException(); }
            public override void Flush() => output.Flush();
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) { reserve(count); output.Write(buffer, offset, count); }
            protected override void Dispose(bool disposing) { if (disposing) output.Dispose(); base.Dispose(disposing); }
        }
    }
}
