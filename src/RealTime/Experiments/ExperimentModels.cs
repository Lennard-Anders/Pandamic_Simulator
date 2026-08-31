// <copyright file="ExperimentModels.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.Experiments
{
    using System;
    using System.Collections.Generic;

    /// <summary>Shared schema version information for experiment artifacts.</summary>
    public static class ExperimentSchema
    {
        /// <summary>The schema version understood by this build.</summary>
        public const int CurrentVersion = 4;
    }

    /// <summary>Defines how seeds are assigned to repeated runs.</summary>
    public enum ExperimentSeedStrategy
    {
        /// <summary>Use the same master seed for every repetition.</summary>
        Fixed,

        /// <summary>Add the zero-based repetition index to the first seed.</summary>
        Sequential,
    }

    /// <summary>Defines when an experiment run ends.</summary>
    public enum ExperimentEndMode
    {
        /// <summary>End only after the configured simulated duration.</summary>
        FixedDuration,

        /// <summary>End at the configured duration or when the epidemic becomes extinct.</summary>
        DurationOrExtinction,
    }

    /// <summary>Defines the requested simulation speed for a batch.</summary>
    public enum ExperimentSpeedMode
    {
        /// <summary>No execution speed has been selected yet.</summary>
        Unspecified = -1,

        /// <summary>Leave the current simulation speed unchanged.</summary>
        PreserveStartingSpeed = 0,

        /// <summary>Use speed one.</summary>
        Speed1 = 1,

        /// <summary>Use speed two.</summary>
        Speed2 = 2,

        /// <summary>Use speed three.</summary>
        Speed3 = 3,
    }

    /// <summary>Durable states in the batch execution state machine.</summary>
    public enum ExperimentBatchExecutionState
    {
        Idle,
        Validating,
        ValidatingBatch = Validating,
        AwaitingConfirmation,
        PreparingRun,
        RequestingBaselineLoad,
        WaitingForLevelUnload,
        WaitingForLevelLoad,
        WaitingForLevelReady,
        ApplyingScenario,
        StartingRun,
        Running,
        Paused,
        FinalizingRun,
        ExportingRun,
        CommittingRun,
        CompletingRun,
        PreparingNextRun,
        Aborting,
        ReturningToBaseline,
        Completed,
        LoadFailed,
        BaselineLoadFailed = LoadFailed,
        ScenarioApplyFailed,
        ExportFailed,
        ReturnFailed,
        ReturnToBaselineFailed = ReturnFailed,
        Interrupted,
        Aborted,

        // Retained as explicit internal validation/run failures for schema v1 diagnostics.
        Failed,
        BaselineValidationFailed,
        RunFailed,
    }

    /// <summary>Serializable identity of an exact baseline save asset.</summary>
    public sealed class BaselineSaveIdentity
    {
        public BaselineSaveIdentity()
        {
            SchemaVersion = ExperimentSchema.CurrentVersion;
        }

        public int SchemaVersion { get; set; }

        public string AssetFullName { get; set; }

        public string AssetName { get; set; }

        public string AssetChecksum { get; set; }

        public long AssetSize { get; set; }

        public string AssetType { get; set; }

        public bool AssetEnabled { get; set; }

        public string AssetUpdatedUtc { get; set; }

        public string AssetDataTimestampUtc { get; set; }

        public string PackageName { get; set; }

        public string PackagePath { get; set; }

        public int PackageFormatVersion { get; set; }

        public long PackageVersion { get; set; }

        public ulong PublishedFileId { get; set; }

        public string CityName { get; set; }

        public string SaveTimestampUtc { get; set; }

        public string Environment { get; set; }

        public string MapThemeAssetFullName { get; set; }

        public bool AchievementsDisabled { get; set; }

        public string DataAssetFullName { get; set; }

        public string DataAssetName { get; set; }

        public string DataAssetType { get; set; }

        public string DataAssetChecksum { get; set; }

        public long DataAssetSize { get; set; }

        public string DataAssetUpdatedUtc { get; set; }

        public string DataAssetDataTimestampUtc { get; set; }

        public string LocalFileSha256 { get; set; }

        public long LocalFileLength { get; set; }

        public string LocalFileLastWriteUtc { get; set; }
    }

    /// <summary>A serializable experiment batch definition.</summary>
    public sealed class ExperimentBatchPlan
    {
        public ExperimentBatchPlan()
        {
            SchemaVersion = ExperimentSchema.CurrentVersion;
            Scenarios = new List<ExperimentScenario>();
            ReturnToBaseline = true;
            StopBatchOnRunFailure = true;
            OutputLocation = new ExperimentOutputRootSelection();
            ModelProvenance = new ExperimentModelProvenance();
        }

        public int SchemaVersion { get; set; }

        public string BatchId { get; set; }

        public string BatchName { get; set; }

        public string OutputFolderName { get; set; }

        public string CreatedUtc { get; set; }

        public string ModVersion { get; set; }

        public string GameVersion { get; set; }

        public string GitCommitSha { get; set; }

        public string GitBranchOrTag { get; set; }

        public ExperimentModelProvenance ModelProvenance { get; set; }

        public BaselineSaveIdentity Baseline { get; set; }

        public RealTimeConfigSnapshot OriginalConfiguration { get; set; }

        public bool OriginalInitialLockdownEnabled { get; set; }

        public int OriginalSimulationSpeed { get; set; }

        public bool OriginalSimulationPaused { get; set; }

        public List<ExperimentScenario> Scenarios { get; set; }

        public ExperimentSpeedMode SpeedMode { get; set; }

        /// <summary>
        /// Gets or sets whether repetition N in every scenario uses the same master seed. The
        /// sequence starts at the first scenario's FirstSeed and advances by repetition index.
        /// </summary>
        public bool PairedSeedMode { get; set; }

        public bool ReturnToBaseline { get; set; }

        /// <summary>Stops at the reloaded baseline after archiving an invalid run; otherwise continues with the next run.</summary>
        public bool StopBatchOnRunFailure { get; set; }

        public string OutputRoot { get; set; }

        public ExperimentOutputRootSelection OutputLocation { get; set; }
    }

    /// <summary>Resolved run-owned paths; no arbitrary destination is accepted.</summary>
    public sealed class ExperimentOutputContext
    {
        public ExperimentOutputContext()
        {
            SchemaVersion = ExperimentSchema.CurrentVersion;
        }

        public int SchemaVersion { get; set; }
        public ExperimentOutputRootKind RootKind { get; set; }
        public string OutputRoot { get; set; }
        public string BatchDirectory { get; set; }
        public string ScenarioDirectory { get; set; }
        public string AttemptDirectory { get; set; }
        public string FinalDirectory { get; set; }
        public string RichCsvPath { get; set; }
        public string DataCsvPath { get; set; }
        public string ContactsCsvPath { get; set; }
    }

    /// <summary>Fixed model values recorded as provenance rather than editable scenario inputs.</summary>
    public sealed class ExperimentModelProvenance
    {
        public ExperimentModelProvenance()
        {
            SchemaVersion = ExperimentSchema.CurrentVersion;
            RandomAlgorithm = "TENUS-RNG-v1/FNV-1a-32";
            TraitAssignmentAlgorithm = DeterministicCitizenTraitAssigner.AlgorithmName;
            ConfigurationHashAlgorithm = ExperimentConfigurationHasher.AlgorithmName;
            RandomStreamCount = 9;
            DiseaseProgressionModel = "TENUS-DISEASE-v2/configured-distributions";
            TransmissionModel = "TENUS-TRANSMISSION-v2/competing-hazards";
            EpidemicStepPolicy = "scenario-configured simulation minutes";
            CitizenReconciliationIntervalMinutes = 360;
            ObservationStoreIntervalMinutes = 5;
            ResidentialSharedAreaWindowMinutes = 15;
            QuarantineDurationDays = 10;
        }

        public int SchemaVersion { get; set; }
        public string RandomAlgorithm { get; set; }
        public string TraitAssignmentAlgorithm { get; set; }
        public string ConfigurationHashAlgorithm { get; set; }
        public int RandomStreamCount { get; set; }
        public string DiseaseProgressionModel { get; set; }
        public string TransmissionModel { get; set; }
        public string EpidemicStepPolicy { get; set; }
        public int CitizenReconciliationIntervalMinutes { get; set; }
        public int ObservationStoreIntervalMinutes { get; set; }
        public int ResidentialSharedAreaWindowMinutes { get; set; }
        public int QuarantineDurationDays { get; set; }
    }

    /// <summary>A scenario and its repetition policy.</summary>
    public sealed class ExperimentScenario
    {
        public ExperimentScenario()
        {
            SchemaVersion = ExperimentSchema.CurrentVersion;
            RunCount = 1;
            DurationDays = 30d;
            EndMode = ExperimentEndMode.FixedDuration;
            SeedStrategy = ExperimentSeedStrategy.Sequential;
            FirstSeed = 1;
            Settings = new ExperimentScenarioSnapshot();
        }

        public int SchemaVersion { get; set; }

        public string ScenarioId { get; set; }

        public string Name { get; set; }

        public int RunCount { get; set; }

        public double DurationDays { get; set; }

        public ExperimentEndMode EndMode { get; set; }

        public ExperimentSeedStrategy SeedStrategy { get; set; }

        public int FirstSeed { get; set; }

        public ExperimentScenarioSnapshot Settings { get; set; }
    }

    /// <summary>Durable progress for an executing batch.</summary>
    public sealed class ExperimentBatchState
    {
        public ExperimentBatchState()
        {
            SchemaVersion = ExperimentSchema.CurrentVersion;
            State = ExperimentBatchExecutionState.Idle;
            CompletedRuns = new List<ExperimentCompletedRun>();
            InvalidRuns = new List<ExperimentInvalidRun>();
        }

        public int SchemaVersion { get; set; }

        public string BatchId { get; set; }

        public ExperimentBatchExecutionState State { get; set; }

        public int ScenarioIndex { get; set; }

        public int RunIndex { get; set; }

        public int CurrentMasterSeed { get; set; }

        public int CurrentPairId { get; set; }

        public string CurrentRunId { get; set; }

        public string CurrentAttemptId { get; set; }

        public string SessionId { get; set; }

        public string ReloadToken { get; set; }

        public int ReloadGeneration { get; set; }

        public string RunStartedUtc { get; set; }

        public string TargetSimulationTimeUtc { get; set; }

        public string UpdatedUtc { get; set; }

        public string LastError { get; set; }

        public ExperimentErrorInfo Error { get; set; }

        public string OutputRoot { get; set; }

        public ExperimentOutputContext OutputContext { get; set; }

        public string RunSimulationStartedUtc { get; set; }

        public string RunSimulationEndedUtc { get; set; }

        public string CommitId { get; set; }

        /// <summary>Persists a requested halt while the controller reloads the exact baseline after an invalid run.</summary>
        public bool HaltAfterInvalidRunBaselineReload { get; set; }

        public List<ExperimentCompletedRun> CompletedRuns { get; set; }

        public List<ExperimentInvalidRun> InvalidRuns { get; set; }
    }

    /// <summary>Structured, durable error information suitable for recovery UI.</summary>
    public sealed class ExperimentErrorInfo
    {
        public string Code { get; set; }

        public string Message { get; set; }

        public string Detail { get; set; }

        public string OccurredUtc { get; set; }

        public bool Retryable { get; set; }
    }

    /// <summary>Durable receipt for a successfully completed run.</summary>
    public sealed class ExperimentCompletedRun
    {
        public string RunId { get; set; }

        public string ScenarioId { get; set; }

        public int ScenarioIndex { get; set; }

        public int RunIndex { get; set; }

        public int MasterSeed { get; set; }

        public int PairId { get; set; }

        public string CompletedUtc { get; set; }

        public string OutputDirectory { get; set; }
    }

    /// <summary>Durable receipt for an archived run excluded from successful aggregation.</summary>
    public sealed class ExperimentInvalidRun
    {
        public string RunId { get; set; }

        public string AttemptId { get; set; }

        public string ScenarioId { get; set; }

        public int ScenarioIndex { get; set; }

        public int RunIndex { get; set; }

        public int MasterSeed { get; set; }

        public int PairId { get; set; }

        public string InvalidatedUtc { get; set; }

        public string OutputDirectory { get; set; }

        public ExperimentErrorInfo Error { get; set; }
    }

    /// <summary>Resolved immutable identity of the run currently selected by the sequencer.</summary>
    public sealed class ExperimentRunDescriptor
    {
        public string RunId { get; set; }

        public int ScenarioIndex { get; set; }

        public int RunIndex { get; set; }

        public ExperimentScenario Scenario { get; set; }

        public int MasterSeed { get; set; }

        /// <summary>One-based paired repetition identifier, or zero outside paired mode.</summary>
        public int PairId { get; set; }

        public ExperimentSeedSet Seeds { get; set; }
    }

    /// <summary>Result of validating a plan or durable state.</summary>
    public sealed class ExperimentValidationResult
    {
        public ExperimentValidationResult()
        {
            Errors = new List<string>();
        }

        public List<string> Errors { get; private set; }

        public bool IsValid
        {
            get { return Errors.Count == 0; }
        }
    }
}
