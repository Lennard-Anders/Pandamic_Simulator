// <copyright file="ContactTracingEngine.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.Pandemic
{
    using System;
    using RealTime.Experiments;

    internal sealed class ContactTracingPolicy
    {
        public double AppAdoptionPercent { get; set; }

        public double ManualTraceabilityPercent { get; set; }
    }

    internal sealed class ContactTraceability
    {
        public bool TraceableByApp { get; set; }

        public bool TraceableByManual { get; set; }

        public bool IsTraceable => TraceableByApp || TraceableByManual;
    }

    /// <summary>Determines traceability after, and independently from, a physical encounter.</summary>
    internal sealed class ContactTracingEngine
    {
        private ContactTracingPolicy policy;
        private int masterSeed;

        public ContactTracingEngine(ContactTracingPolicy policy, int masterSeed)
        {
            Reset(policy, masterSeed);
        }

        public void Reset(ContactTracingPolicy newPolicy, int newMasterSeed)
        {
            if (newPolicy == null)
            {
                throw new ArgumentNullException(nameof(newPolicy));
            }

            ValidatePercent(newPolicy.AppAdoptionPercent, nameof(newPolicy.AppAdoptionPercent));
            ValidatePercent(newPolicy.ManualTraceabilityPercent, nameof(newPolicy.ManualTraceabilityPercent));
            if (newMasterSeed < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(newMasterSeed));
            }

            policy = newPolicy;
            masterSeed = newMasterSeed;
        }

        public bool UsesApp(uint citizenId)
        {
            return DeterministicCitizenTraitAssigner.IsAssigned(
                masterSeed,
                citizenId,
                "tracing-app",
                policy.AppAdoptionPercent);
        }

        public bool IsManuallyTraceable(uint citizenId)
        {
            return DeterministicCitizenTraitAssigner.IsAssigned(
                masterSeed,
                citizenId,
                "manual-tracing",
                policy.ManualTraceabilityPercent);
        }

        public ContactTraceability Evaluate(PhysicalContactEvent contact)
        {
            if (contact == null)
            {
                throw new ArgumentNullException(nameof(contact));
            }

            bool app = UsesApp(contact.CitizenA) && UsesApp(contact.CitizenB);
            bool manual = contact.BuildingId != 0
                && IsManuallyTraceable(contact.CitizenA)
                && IsManuallyTraceable(contact.CitizenB);
            return new ContactTraceability
            {
                TraceableByApp = app,
                TraceableByManual = manual,
            };
        }

        private static void ValidatePercent(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d || value > 100d)
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }
    }
}
