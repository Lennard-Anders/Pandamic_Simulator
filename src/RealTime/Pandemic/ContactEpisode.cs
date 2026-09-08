namespace RealTime.Pandemic
{
    using System;
    using System.Collections.Generic;

    /// <summary>Measurement of contiguous contact steps; never used to evaluate transmission.</summary>
    internal sealed class ContactEpisode
    {
        public long EpisodeId { get; internal set; }
        public uint CitizenA { get; internal set; }
        public uint CitizenB { get; internal set; }
        public DateTime StartTime { get; internal set; }
        public DateTime EndTime { get; internal set; }
        public double DurationMinutes => (EndTime - StartTime).TotalMinutes;
        public PhysicalContactContext Context { get; internal set; }
        public ushort BuildingId { get; internal set; }
        public ushort VehicleId { get; internal set; }
        public byte DistrictId { get; internal set; }
        public bool TraceableByApp { get; internal set; }
        public bool TraceableByManual { get; internal set; }
        public long EpidemiologicalSteps { get; internal set; }
    }

    /// <summary>Only active episodes are retained. The caller supplies every step, including empty steps.</summary>
    internal sealed class ContactEpisodeTracker
    {
        private readonly Dictionary<EpisodeKey, ContactEpisode> active =
            new Dictionary<EpisodeKey, ContactEpisode>();
        private readonly List<EpisodeKey> closing =
            new List<EpisodeKey>();
        private readonly Action<ContactEpisode> emit;
        private DateTime stepEnd;
        private long nextId = 1;

        private struct EpisodeKey : IEquatable<EpisodeKey>
        {
            internal readonly uint A;
            internal readonly uint B;
            private readonly PhysicalContactContext context;
            private readonly ushort building;
            private readonly ushort vehicle;
            private readonly byte district;

            internal EpisodeKey(uint a, uint b, PhysicalContactContext context, ushort building, ushort vehicle, byte district)
            {
                A = a; B = b; this.context = context; this.building = building;
                this.vehicle = vehicle; this.district = district;
            }

            public bool Equals(EpisodeKey other) => A == other.A && B == other.B && context == other.context
                && building == other.building && vehicle == other.vehicle && district == other.district;
            public override bool Equals(object obj) => obj is EpisodeKey other && Equals(other);
            public override int GetHashCode()
            {
                unchecked { return (((((int)A * 397 ^ (int)B) * 397 ^ (int)context) * 397 ^ building) * 397 ^ vehicle) * 397 ^ district; }
            }
        }

        public ContactEpisodeTracker(Action<ContactEpisode> emit)
        {
            this.emit = emit ?? throw new ArgumentNullException(nameof(emit));
        }

        public int ActiveCount => active.Count;

        public void BeginStep(DateTime endTime)
        {
            if (endTime <= stepEnd) throw new InvalidOperationException("Episode step times must increase.");
            stepEnd = endTime;
        }

        public void Observe(PhysicalContactEvent contact, byte district)
        {
            if (contact == null) throw new ArgumentNullException(nameof(contact));
            if (contact.EndTime != stepEnd) throw new InvalidOperationException("Contact is outside the current episode step.");
            var key = new EpisodeKey(Math.Min(contact.CitizenA, contact.CitizenB), Math.Max(contact.CitizenA, contact.CitizenB),
                contact.Context, contact.BuildingId, contact.VehicleId, district);
            if (active.TryGetValue(key, out ContactEpisode episode))
            {
                if (episode.EndTime == contact.EndTime) throw new InvalidOperationException("Duplicate contact in episode step.");
                // Split on traceability changes so filtering episodes remains lossless in time.
                if (episode.EndTime != contact.StartTime || episode.TraceableByApp != contact.TraceableByApp
                    || episode.TraceableByManual != contact.TraceableByManual)
                {
                    emit(episode);
                    active.Remove(key);
                    episode = null;
                }
            }
            if (episode == null)
            {
                episode = new ContactEpisode { EpisodeId = nextId++, CitizenA = key.A, CitizenB = key.B,
                    StartTime = contact.StartTime, Context = contact.Context, BuildingId = contact.BuildingId,
                    VehicleId = contact.VehicleId, DistrictId = district, TraceableByApp = contact.TraceableByApp,
                    TraceableByManual = contact.TraceableByManual };
                active.Add(key, episode);
            }
            episode.EndTime = contact.EndTime;
            episode.EpidemiologicalSteps++;
        }

        public void EndStep() => Close(false);
        public void Complete() => Close(true);

        public void Reset()
        {
            active.Clear();
            closing.Clear();
            nextId = 1;
            stepEnd = default(DateTime);
        }

        private void Close(bool all)
        {
            closing.Clear();
            foreach (var item in active)
                if (all || item.Value.EndTime != stepEnd) closing.Add(item.Key);
            closing.Sort((a, b) => active[a].EpisodeId.CompareTo(active[b].EpisodeId));
            foreach (var key in closing)
            {
                emit(active[key]);
                active.Remove(key);
            }
            closing.Clear();
        }
    }
}

