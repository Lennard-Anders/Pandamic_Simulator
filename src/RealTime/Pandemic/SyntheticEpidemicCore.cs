// <copyright file="SyntheticEpidemicCore.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.Pandemic
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal sealed class SyntheticContact
    {
        public uint CitizenA { get; set; }

        public uint CitizenB { get; set; }

        public double TransmissionProbability { get; set; }

        public PhysicalContactContext Context { get; set; }

        public bool CitizenAInterventionActive { get; set; }

        public bool CitizenBInterventionActive { get; set; }

        public bool CitizenAAtHome { get; set; }

        public bool CitizenBAtHome { get; set; }
    }

    internal sealed class SyntheticTransmissionEvent
    {
        public uint SourceCitizenId { get; set; }

        public uint TargetCitizenId { get; set; }

        public DateTime SimulationTime { get; set; }
    }

    /// <summary>
    /// Small Unity-independent orchestration kernel used for behavior, regression, and calibration tests.
    /// It intentionally accepts an explicit contact graph and makes no claims about real-world calibration.
    /// </summary>
    internal sealed class SyntheticEpidemicCore
    {
        private readonly DiseaseStateEngine stateEngine = new DiseaseStateEngine();
        private readonly TransmissionEngine transmissionEngine = new TransmissionEngine();
        private readonly IsolationQuarantineEngine interventionEngine = new IsolationQuarantineEngine();
        private readonly DiseaseProgressionEngine progressionEngine;
        private readonly Random transmissionRandom;
        private readonly List<SyntheticTransmissionEvent> transmissionEvents = new List<SyntheticTransmissionEvent>();

        public SyntheticEpidemicCore(
            DiseaseTimelinePolicy timelinePolicy,
            int diseaseProgressionSeed,
            int transmissionSeed)
        {
            if (diseaseProgressionSeed < 0 || transmissionSeed < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(diseaseProgressionSeed));
            }

            progressionEngine = new DiseaseProgressionEngine(timelinePolicy, new Random(diseaseProgressionSeed));
            transmissionRandom = new Random(transmissionSeed);
        }

        public IList<SyntheticTransmissionEvent> TransmissionEvents => transmissionEvents.AsReadOnly();

        public void RegisterSusceptible(uint citizenId)
        {
            stateEngine.RegisterCitizen(citizenId);
        }

        public bool RegisterCourse(DiseaseCourse course, DateTime simulationTime)
        {
            if (course == null)
            {
                throw new ArgumentNullException(nameof(course));
            }

            stateEngine.RegisterCitizen(course.CitizenId);
            return stateEngine.TryExpose(course, simulationTime);
        }

        public DiseaseState GetState(uint citizenId, DateTime simulationTime)
        {
            return stateEngine.GetState(citizenId, simulationTime);
        }

        public int Step(DateTime simulationTime, IEnumerable<SyntheticContact> contacts)
        {
            if (contacts == null)
            {
                throw new ArgumentNullException(nameof(contacts));
            }

            var exposures = new List<TransmissionExposure<SyntheticContact>>();
            foreach (SyntheticContact contact in contacts
                .Where(contact => contact != null)
                .OrderBy(contact => Math.Min(contact.CitizenA, contact.CitizenB))
                .ThenBy(contact => Math.Max(contact.CitizenA, contact.CitizenB))
                .ThenBy(contact => contact.Context))
            {
                if (!interventionEngine.IsPhysicalContactAllowed(
                    contact.CitizenAInterventionActive,
                    contact.CitizenAAtHome,
                    contact.CitizenBInterventionActive,
                    contact.CitizenBAtHome,
                    contact.Context))
                {
                    continue;
                }

                QueueDirection(contact.CitizenA, contact.CitizenB, contact, simulationTime, exposures);
                QueueDirection(contact.CitizenB, contact.CitizenA, contact, simulationTime, exposures);
            }

            IList<ResolvedTransmission<SyntheticContact>> resolved = transmissionEngine.Resolve(exposures, transmissionRandom);
            int successful = 0;
            foreach (ResolvedTransmission<SyntheticContact> transmission in resolved)
            {
                DiseaseCourse course = progressionEngine.CreateCourse(
                    transmission.TargetCitizenId,
                    simulationTime,
                    false,
                    DiseaseExposureKind.SecondaryTransmission);
                if (!stateEngine.TryExpose(course, simulationTime))
                {
                    continue;
                }

                transmissionEvents.Add(new SyntheticTransmissionEvent
                {
                    SourceCitizenId = transmission.SourceCitizenId,
                    TargetCitizenId = transmission.TargetCitizenId,
                    SimulationTime = simulationTime,
                });
                successful++;
            }

            return successful;
        }

        public EpidemicMetricsSnapshot CaptureMetrics(DateTime simulationTime)
        {
            return new MetricsEngine().Capture(stateEngine, simulationTime);
        }

        private void QueueDirection(
            uint source,
            uint target,
            SyntheticContact contact,
            DateTime simulationTime,
            ICollection<TransmissionExposure<SyntheticContact>> exposures)
        {
            if (!stateEngine.IsInfectious(source, simulationTime)
                || !stateEngine.IsSusceptible(target, simulationTime))
            {
                return;
            }

            exposures.Add(new TransmissionExposure<SyntheticContact>
            {
                SourceCitizenId = source,
                TargetCitizenId = target,
                Probability = contact.TransmissionProbability,
                StableContextKey = (int)contact.Context,
                Context = contact,
            });
        }
    }
}
