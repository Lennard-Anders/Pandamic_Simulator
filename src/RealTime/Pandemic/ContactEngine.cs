// <copyright file="ContactEngine.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.Pandemic
{
    using System;
    using System.Collections.Generic;

    internal enum PhysicalContactContext
    {
        Household,
        School,
        University,
        Workplace,
        Healthcare,
        Commercial,
        Leisure,
        PublicTransport,
        Outdoor,
        Other,
    }

    internal sealed class PhysicalContactRequest
    {
        public uint CitizenA { get; set; }

        public uint CitizenB { get; set; }

        public DateTime EndTime { get; set; }

        public double DurationMinutes { get; set; }

        public PhysicalContactContext Context { get; set; }

        public ushort BuildingId { get; set; }

        public ushort VehicleId { get; set; }

        public float PositionX { get; set; }

        public float PositionY { get; set; }

        public float PositionZ { get; set; }

        public double? Distance { get; set; }
    }

    /// <summary>One immutable physical encounter, independent of traceability and transmission.</summary>
    internal sealed class PhysicalContactEvent
    {
        public long ContactId { get; internal set; }

        public uint CitizenA { get; internal set; }

        public uint CitizenB { get; internal set; }

        public DateTime StartTime { get; internal set; }

        public DateTime EndTime { get; internal set; }

        public double DurationMinutes { get; internal set; }

        public PhysicalContactContext Context { get; internal set; }

        public ushort BuildingId { get; internal set; }

        public ushort VehicleId { get; internal set; }

        public float PositionX { get; internal set; }

        public float PositionY { get; internal set; }

        public float PositionZ { get; internal set; }

        public double? Distance { get; internal set; }

        public bool TraceableByApp { get; internal set; }

        public bool TraceableByManual { get; internal set; }
    }

    /// <summary>Canonical, history-preserving physical-contact recorder.</summary>
    internal sealed class ContactEngine
    {
        private readonly List<PhysicalContactEvent> events = new List<PhysicalContactEvent>();
        private readonly Dictionary<ContactStepKey, PhysicalContactEvent> eventsByStep = new Dictionary<ContactStepKey, PhysicalContactEvent>();
        private long nextContactId = 1L;
        private long currentStepTicks = long.MinValue;
        private bool retainHistory = true;

        public PhysicalContactEvent Record(PhysicalContactRequest request, out bool created)
        {
            using (PandemicProfiler.Measure("ContactEngine")) return RecordCore(request, out created);
        }

        private PhysicalContactEvent RecordCore(PhysicalContactRequest request, out bool created)
        {
            Validate(request);
            uint citizenA = Math.Min(request.CitizenA, request.CitizenB);
            uint citizenB = Math.Max(request.CitizenA, request.CitizenB);
            if (request.EndTime.Ticks != currentStepTicks)
            {
                eventsByStep.Clear();
                currentStepTicks = request.EndTime.Ticks;
            }

            var key = new ContactStepKey(
                request.EndTime.Ticks,
                citizenA,
                citizenB,
                request.Context,
                request.BuildingId,
                request.VehicleId);
            if (eventsByStep.TryGetValue(key, out PhysicalContactEvent existing))
            {
                created = false;
                return existing;
            }

            long durationTicks = checked((long)Math.Round(
                request.DurationMinutes * TimeSpan.TicksPerMinute,
                MidpointRounding.AwayFromZero));
            if (durationTicks <= 0L || request.EndTime.Ticks < durationTicks)
            {
                throw new ArgumentOutOfRangeException(nameof(request), "The physical-contact interval is invalid.");
            }

            var contact = new PhysicalContactEvent
            {
                ContactId = nextContactId++,
                CitizenA = citizenA,
                CitizenB = citizenB,
                StartTime = request.EndTime.AddTicks(-durationTicks),
                EndTime = request.EndTime,
                DurationMinutes = request.DurationMinutes,
                Context = request.Context,
                BuildingId = request.BuildingId,
                VehicleId = request.VehicleId,
                PositionX = request.PositionX,
                PositionY = request.PositionY,
                PositionZ = request.PositionZ,
                Distance = request.Distance,
            };
            eventsByStep.Add(key, contact);
            if (retainHistory)
            {
                events.Add(contact);
            }

            created = true;
            return contact;
        }

        public IList<PhysicalContactEvent> GetEvents()
        {
            return new List<PhysicalContactEvent>(events);
        }

        public void Reset()
        {
            events.Clear();
            eventsByStep.Clear();
            nextContactId = 1L;
            currentStepTicks = long.MinValue;
        }

        public void SetRetainHistory(bool value)
        {
            retainHistory = value;
            if (!retainHistory)
            {
                events.Clear();
            }
        }

        private static void Validate(PhysicalContactRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.CitizenA == 0u || request.CitizenB == 0u || request.CitizenA == request.CitizenB)
            {
                throw new ArgumentException("A physical contact requires two distinct, nonzero citizen IDs.", nameof(request));
            }

            if (request.EndTime == default(DateTime))
            {
                throw new ArgumentException("A physical contact requires an end time.", nameof(request));
            }

            if (double.IsNaN(request.DurationMinutes)
                || double.IsInfinity(request.DurationMinutes)
                || request.DurationMinutes <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(request), "Contact duration must be finite and positive.");
            }

            if (request.Distance.HasValue
                && (double.IsNaN(request.Distance.Value)
                    || double.IsInfinity(request.Distance.Value)
                    || request.Distance.Value < 0d))
            {
                throw new ArgumentOutOfRangeException(nameof(request), "Contact distance must be finite and nonnegative.");
            }
        }

        private struct ContactStepKey : IEquatable<ContactStepKey>
        {
            private readonly long endTicks;
            private readonly uint citizenA;
            private readonly uint citizenB;
            private readonly PhysicalContactContext context;
            private readonly ushort buildingId;
            private readonly ushort vehicleId;

            public ContactStepKey(
                long endTicks,
                uint citizenA,
                uint citizenB,
                PhysicalContactContext context,
                ushort buildingId,
                ushort vehicleId)
            {
                this.endTicks = endTicks;
                this.citizenA = citizenA;
                this.citizenB = citizenB;
                this.context = context;
                this.buildingId = buildingId;
                this.vehicleId = vehicleId;
            }

            public bool Equals(ContactStepKey other)
            {
                return endTicks == other.endTicks
                    && citizenA == other.citizenA
                    && citizenB == other.citizenB
                    && context == other.context
                    && buildingId == other.buildingId
                    && vehicleId == other.vehicleId;
            }

            public override bool Equals(object obj)
            {
                return obj is ContactStepKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = endTicks.GetHashCode();
                    hash = (hash * 397) ^ (int)citizenA;
                    hash = (hash * 397) ^ (int)citizenB;
                    hash = (hash * 397) ^ (int)context;
                    hash = (hash * 397) ^ buildingId;
                    return (hash * 397) ^ vehicleId;
                }
            }
        }
    }
}
