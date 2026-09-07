// <copyright file="PandemicRunContext.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.Pandemic
{
    using System;
    using System.IO;
    using RealTime.Core;
    using RealTime.Experiments;

    /// <summary>Identifies who owns the lifecycle of a pandemic run.</summary>
    internal enum PandemicRunMode
    {
        Manual,
        Batch,
    }

    /// <summary>Describes why a pandemic run stopped mutating simulation state.</summary>
    internal enum PandemicCompletionReason
    {
        None,
        DurationReached,
        EpidemicExtinct,
        ManualStop,
        Aborted,
    }

    /// <summary>Immutable end-of-run policy evaluated using Cities: Skylines simulation time.</summary>
    internal sealed class PandemicRunPolicy
    {
        private static readonly TimeSpan LegacyManualDuration = TimeSpan.FromDays(30d);

        private PandemicRunPolicy(TimeSpan duration, bool stopOnExtinction)
        {
            if (duration <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(duration));
            }

            Duration = duration;
            StopOnExtinction = stopOnExtinction;
        }

        public TimeSpan Duration { get; }

        public double DurationDays => Duration.TotalDays;

        public bool StopOnExtinction { get; }

        public static PandemicRunPolicy CreateLegacyManual() => new PandemicRunPolicy(LegacyManualDuration, stopOnExtinction: true);

        public static PandemicRunPolicy CreateBatch(double durationDays, bool stopOnExtinction)
        {
            if (double.IsNaN(durationDays) || double.IsInfinity(durationDays) || durationDays <= 0d || durationDays > 3650d)
            {
                throw new ArgumentOutOfRangeException(nameof(durationDays));
            }

            double ticks = durationDays * TimeSpan.TicksPerDay;
            if (ticks > TimeSpan.MaxValue.Ticks)
            {
                throw new ArgumentOutOfRangeException(nameof(durationDays));
            }

            return new PandemicRunPolicy(TimeSpan.FromTicks((long)Math.Round(ticks, MidpointRounding.AwayFromZero)), stopOnExtinction);
        }

        public DateTime CalculateTarget(DateTime startTime)
        {
            if (startTime == default(DateTime) || startTime.Ticks > DateTime.MaxValue.Ticks - Duration.Ticks)
            {
                throw new ArgumentOutOfRangeException(nameof(startTime));
            }

            return startTime.Add(Duration);
        }

        public PandemicCompletionReason Evaluate(
            DateTime startTime,
            DateTime currentTime,
            int sickCitizens,
            int exposedCitizens,
            bool hadAnyInfections)
        {
            if (startTime != default(DateTime) && currentTime >= CalculateTarget(startTime))
            {
                return PandemicCompletionReason.DurationReached;
            }

            if (StopOnExtinction && hadAnyInfections && sickCitizens == 0 && exposedCitizens == 0)
            {
                return PandemicCompletionReason.EpidemicExtinct;
            }

            return PandemicCompletionReason.None;
        }
    }

    /// <summary>All deterministic random seeds owned by one TENUS pandemic run.</summary>
    internal sealed class PandemicComponentSeeds
    {
        public PandemicComponentSeeds(
            int masterSeed,
            int initialPopulationSeed,
            int diseaseProgressionSeed,
            int transmissionSeed,
            int symptomSeed,
            int mortalitySeed,
            int maskSeed,
            int testingSeed,
            int contactTracingSeed,
            int interventionSeed)
        {
            EnsureNonnegative(masterSeed, nameof(masterSeed));
            EnsureNonnegative(initialPopulationSeed, nameof(initialPopulationSeed));
            EnsureNonnegative(diseaseProgressionSeed, nameof(diseaseProgressionSeed));
            EnsureNonnegative(transmissionSeed, nameof(transmissionSeed));
            EnsureNonnegative(symptomSeed, nameof(symptomSeed));
            EnsureNonnegative(mortalitySeed, nameof(mortalitySeed));
            EnsureNonnegative(maskSeed, nameof(maskSeed));
            EnsureNonnegative(testingSeed, nameof(testingSeed));
            EnsureNonnegative(contactTracingSeed, nameof(contactTracingSeed));
            EnsureNonnegative(interventionSeed, nameof(interventionSeed));

            MasterSeed = masterSeed;
            InitialPopulationSeed = initialPopulationSeed;
            DiseaseProgressionSeed = diseaseProgressionSeed;
            TransmissionSeed = transmissionSeed;
            SymptomSeed = symptomSeed;
            MortalitySeed = mortalitySeed;
            MaskSeed = maskSeed;
            TestingSeed = testingSeed;
            ContactTracingSeed = contactTracingSeed;
            InterventionSeed = interventionSeed;
        }

        public int MasterSeed { get; }

        public int InitialPopulationSeed { get; }

        public int DiseaseProgressionSeed { get; }

        public int TransmissionSeed { get; }

        public int SymptomSeed { get; }

        public int MortalitySeed { get; }

        public int MaskSeed { get; }

        public int TestingSeed { get; }

        public int ContactTracingSeed { get; }

        public int InterventionSeed { get; }

        /// <summary>Adapts the seed-provider result to the nine independent TENUS random streams.</summary>
        public static PandemicComponentSeeds FromExperimentSeedSet(ExperimentSeedSet seeds)
        {
            if (seeds == null)
            {
                throw new ArgumentNullException(nameof(seeds));
            }

            return new PandemicComponentSeeds(
                seeds.Master,
                seeds.InitialPopulation,
                seeds.DiseaseProgression,
                seeds.Transmission,
                seeds.Symptom,
                seeds.Mortality,
                seeds.Mask,
                seeds.Testing,
                seeds.ContactTracing,
                seeds.Intervention);
        }

        private static void EnsureNonnegative(int seed, string parameterName)
        {
            if (seed < 0)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Experiment seeds must be nonnegative.");
            }
        }
    }

    /// <summary>Explicit destinations for all files produced by a pandemic run.</summary>
    internal sealed class PandemicOutputContext
    {
        public PandemicOutputContext(
            string richCsvPath,
            string observerCsvPath,
            string contactsCsvPath,
            bool allowReplaceExisting,
            bool writeManagerSnapshots)
        {
            RichCsvPath = richCsvPath;
            ObserverCsvPath = observerCsvPath;
            ContactsCsvPath = contactsCsvPath;
            AllowReplaceExisting = allowReplaceExisting;
            WriteManagerSnapshots = writeManagerSnapshots;
        }

        public string RichCsvPath { get; }

        public string ObserverCsvPath { get; }

        public string ContactsCsvPath { get; }

        public bool AllowReplaceExisting { get; }

        /// <summary>Gets whether PandemicManager should retain legacy initial, periodic, and terminal raw writes.</summary>
        public bool WriteManagerSnapshots { get; }

        public static PandemicOutputContext CreateManual()
        {
            string modRoot = ModPaths.GetModRoot();
            return new PandemicOutputContext(
                richCsvPath: null,
                observerCsvPath: string.IsNullOrEmpty(modRoot) ? null : Path.Combine(modRoot, "data.csv"),
                contactsCsvPath: string.IsNullOrEmpty(modRoot) ? null : Path.Combine(modRoot, "contacts.csv"),
                allowReplaceExisting: true,
                writeManagerSnapshots: true);
        }
    }

    /// <summary>Metadata emitted in a separate rich CSV section for controller-owned runs.</summary>
    internal sealed class PandemicBatchExportMetadata
    {
        public string PresetId { get; set; }
        public int PresetVersion { get; set; }
        public bool CustomizedAfterPreset { get; set; }
        public RealTime.Experiments.ExperimentSensitivityMetadata Sensitivity { get; set; }

        public string BatchId { get; set; }

        public string BatchName { get; set; }

        public string ScenarioId { get; set; }

        public string ScenarioName { get; set; }

        public int ScenarioIndex { get; set; }

        public int RunNumber { get; set; }

        public int OverallRunNumber { get; set; }

        public int PairId { get; set; }

        public string ConfigurationHash { get; set; }

        public string GitCommitSha { get; set; }

        public string GitBranchOrTag { get; set; }
    }

    /// <summary>Immutable ownership, policy, output, and seed context for one pandemic run.</summary>
    internal sealed class PandemicRunContext
    {
        internal RealTime.Experiments.ExperimentInterventionSchedule InterventionSchedule { get; set; }
        internal CalibrationTargetSet CalibrationTargets { get; set; }

        public PandemicRunContext(
            PandemicRunMode mode,
            PandemicRunPolicy policy,
            PandemicOutputContext output,
            PandemicComponentSeeds componentSeeds,
            DateTime wallClockStartUtc,
            PandemicBatchExportMetadata batchMetadata)
        {
            Mode = mode;
            Policy = policy ?? throw new ArgumentNullException(nameof(policy));
            Output = output ?? throw new ArgumentNullException(nameof(output));
            if (mode == PandemicRunMode.Batch && componentSeeds == null)
            {
                throw new ArgumentNullException(nameof(componentSeeds));
            }

            if (mode == PandemicRunMode.Batch && batchMetadata == null)
            {
                throw new ArgumentNullException(nameof(batchMetadata));
            }

            if (mode == PandemicRunMode.Batch
                && (string.IsNullOrEmpty(output.RichCsvPath)
                    || string.IsNullOrEmpty(output.ObserverCsvPath)
                    || string.IsNullOrEmpty(output.ContactsCsvPath)))
            {
                throw new ArgumentException("Batch runs require explicit rich, observer, and contact CSV destinations.", nameof(output));
            }

            if (mode == PandemicRunMode.Batch
                && (wallClockStartUtc == default(DateTime) || wallClockStartUtc.Kind != DateTimeKind.Utc))
            {
                throw new ArgumentException("Batch wall-clock start time must be a non-default UTC value.", nameof(wallClockStartUtc));
            }

            ComponentSeeds = componentSeeds;
            WallClockStartUtc = wallClockStartUtc;
            BatchMetadata = batchMetadata;
        }

        public PandemicRunMode Mode { get; }

        public PandemicRunPolicy Policy { get; }

        public PandemicOutputContext Output { get; }

        public PandemicComponentSeeds ComponentSeeds { get; }

        public DateTime WallClockStartUtc { get; }

        public PandemicBatchExportMetadata BatchMetadata { get; }

        public bool IsBatch => Mode == PandemicRunMode.Batch;

        public static PandemicRunContext CreateManual(DateTime wallClockStart)
        {
            DateTime utc = wallClockStart.Kind == DateTimeKind.Utc ? wallClockStart : wallClockStart.ToUniversalTime();
            return new PandemicRunContext(
                PandemicRunMode.Manual,
                PandemicRunPolicy.CreateLegacyManual(),
                PandemicOutputContext.CreateManual(),
                componentSeeds: null,
                wallClockStartUtc: utc,
                batchMetadata: null);
        }
    }
}
