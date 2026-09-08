// <copyright file="ExperimentRunCommitService.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.Experiments
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;

    /// <summary>Durable manifest for one completed experiment run.</summary>
    public sealed class ExperimentRunManifest
    {
        /// <summary>Zero identifies legacy packages; version one requires network and calibration outputs.</summary>
        public int ScientificExtensionsVersion { get; set; }
        public int ScientificExportSchemaVersion { get; set; }
        public string ContactExportMode { get; set; }
        public string ContactRepresentation { get; set; }
        public string Compression { get; set; }
        public string Partitioning { get; set; }
        public uint EpidemicStepMinutes { get; set; }
        public string ContactPersistenceModel { get; set; }
        public Dictionary<string, uint> ContactPersistenceMinutes { get; set; }
        public List<ExperimentGeneratedFileEntry> ContactFiles { get; set; }
        public long ContactEpisodesTotal { get; set; }
        public const string CompletedStatus = "Completed";

        public ExperimentRunManifest()
        {
            SchemaVersion = ExperimentSchema.CurrentVersion;
            OutputFiles = new List<ExperimentGeneratedFileEntry>();
        }

        public int SchemaVersion { get; set; }

        public string Status { get; set; }

        public string BatchId { get; set; }

        public string BatchName { get; set; }

        public string OutputFolderName { get; set; }

        public BaselineSaveIdentity Baseline { get; set; }

        public string BaselineAssetFullName { get; set; }

        public string BaselineAssetChecksum { get; set; }

        public string BaselineDataAssetChecksum { get; set; }

        public string BaselineLocalFileSha256 { get; set; }

        public int ScenarioIndex { get; set; }

        public int ScenarioNumber { get; set; }

        public string ScenarioId { get; set; }

        public string ScenarioName { get; set; }

        public ExperimentScenario Scenario { get; set; }

        public int RunIndex { get; set; }

        public int RunNumber { get; set; }

        public string RunId { get; set; }

        public int OverallRunNumber { get; set; }

        public string AttemptId { get; set; }

        public string CommitId { get; set; }

        public string SessionNonce { get; set; }

        public int ReloadGeneration { get; set; }

        public string SeedAlgorithm { get; set; }

        public int MasterSeed { get; set; }

        public int PairId { get; set; }

        public bool PairedSeedMode { get; set; }

        public int InitialPopulationSeed { get; set; }

        public int DiseaseProgressionSeed { get; set; }

        public int TransmissionSeed { get; set; }

        public int SymptomSeed { get; set; }

        public int MortalitySeed { get; set; }

        public int PandemicSeed { get; set; }

        public int MaskSeed { get; set; }

        public int TestSeed { get; set; }

        public int ContactSeed { get; set; }

        public int TestingSeed { get; set; }

        public int ContactTracingSeed { get; set; }

        public int InterventionSeed { get; set; }

        public string TraitAssignmentAlgorithm { get; set; }

        public string ConfigurationHashAlgorithm { get; set; }

        public string ConfigurationHash { get; set; }

        public int FinalTrackedPopulation { get; set; }

        public int FinalSusceptible { get; set; }

        public int FinalExposed { get; set; }

        public int FinalInfectious { get; set; }

        public int FinalPostInfectiousIll { get; set; }

        public int FinalSymptomatic { get; set; }

        public int FinalSick { get; set; }

        public int FinalRecovered { get; set; }

        public int FinalDead { get; set; }

        public int FinalTransmissionsTotal { get; set; }

        public int InitialSeedCount { get; set; }

        public int SecondaryTransmissionsTotal { get; set; }

        public int CumulativeInfections { get; set; }

        public int HospitalizationsTotal { get; set; }

        public double FinalAttackRatePercent { get; set; }

        public double FinalFatalityRatePercent { get; set; }

        public double FinalPrevalencePercent { get; set; }

        public double? ResolvedCaseFatalityRatioPercent { get; set; }

        public double? EmpiricalSecondaryInfectionsPerInfector { get; set; }

        public double ActualMaskUsagePercent { get; set; }

        public double TotalIsolationPersonDays { get; set; }

        public double TotalQuarantinePersonDays { get; set; }

        public long PhysicalContactsTotal { get; set; }

        public long TraceableContactsTotal { get; set; }

        public double ConfiguredDurationDays { get; set; }

        public string EndMode { get; set; }

        public string EndReason { get; set; }

        public string StartedUtc { get; set; }

        public string TargetSimulationTimeUtc { get; set; }

        public string SimulationEndedUtc { get; set; }

        public string CompletedUtc { get; set; }

        public string ModVersion { get; set; }

        public string GameVersion { get; set; }

        public string GitCommitSha { get; set; }

        public string GitBranchOrTag { get; set; }

        public string OutputDirectory { get; set; }

        public string OutputRootKind { get; set; }

        public string OutputRoot { get; set; }

        public List<ExperimentGeneratedFileEntry> OutputFiles { get; set; }
    }

    /// <summary>Cryptographic identity of one generated run file.</summary>
    public sealed class ExperimentGeneratedFileEntry
    {
        public long? RowCount { get; set; }
        public string SimulationStartTime { get; set; }
        public string SimulationEndTime { get; set; }
        public string Role { get; set; }

        public string RelativePath { get; set; }

        public long LengthBytes { get; set; }

        public string Sha256 { get; set; }
    }

    /// <summary>One row in the deterministic batch run summary.</summary>
    public sealed class ExperimentBatchRunSummaryRow
    {
        public int SchemaVersion { get; set; }

        public string BatchId { get; set; }

        public string BatchName { get; set; }

        public string BaselineAssetFullName { get; set; }

        public string BaselineAssetChecksum { get; set; }

        public int ScenarioIndex { get; set; }

        public int ScenarioNumber { get; set; }

        public string ScenarioId { get; set; }

        public string ScenarioName { get; set; }

        public int RunIndex { get; set; }

        public int RunNumber { get; set; }

        public string RunId { get; set; }

        public string SeedAlgorithm { get; set; }

        public int MasterSeed { get; set; }

        public int PairId { get; set; }

        public bool PairedSeedMode { get; set; }

        public int InitialPopulationSeed { get; set; }

        public int DiseaseProgressionSeed { get; set; }

        public int TransmissionSeed { get; set; }

        public int SymptomSeed { get; set; }

        public int MortalitySeed { get; set; }

        public int PandemicSeed { get; set; }

        public int MaskSeed { get; set; }

        public int TestSeed { get; set; }

        public int ContactSeed { get; set; }

        public int TestingSeed { get; set; }

        public int ContactTracingSeed { get; set; }

        public int InterventionSeed { get; set; }

        public string ConfigurationHash { get; set; }

        public int FinalTrackedPopulation { get; set; }

        public int FinalSusceptible { get; set; }

        public int FinalExposed { get; set; }

        public int FinalInfectious { get; set; }

        public int FinalPostInfectiousIll { get; set; }

        public int FinalSymptomatic { get; set; }

        public int FinalSick { get; set; }

        public int FinalRecovered { get; set; }

        public int FinalDead { get; set; }

        public int FinalTransmissionsTotal { get; set; }

        public int InitialSeedCount { get; set; }

        public int SecondaryTransmissionsTotal { get; set; }

        public int CumulativeInfections { get; set; }

        public int HospitalizationsTotal { get; set; }

        public double FinalAttackRatePercent { get; set; }

        public double FinalFatalityRatePercent { get; set; }

        public double FinalPrevalencePercent { get; set; }

        public double? ResolvedCaseFatalityRatioPercent { get; set; }

        public double? EmpiricalSecondaryInfectionsPerInfector { get; set; }

        public double ActualMaskUsagePercent { get; set; }

        public double TotalIsolationPersonDays { get; set; }

        public double TotalQuarantinePersonDays { get; set; }

        public long PhysicalContactsTotal { get; set; }

        public long TraceableContactsTotal { get; set; }

        public double ConfiguredDurationDays { get; set; }

        public string EndMode { get; set; }

        public string EndReason { get; set; }

        public string StartedUtc { get; set; }

        public string TargetSimulationTimeUtc { get; set; }

        public string SimulationEndedUtc { get; set; }

        public string CompletedUtc { get; set; }

        public string ModVersion { get; set; }

        public string GameVersion { get; set; }

        public string GitCommitSha { get; set; }

        public string GitBranchOrTag { get; set; }

        public string OutputDirectory { get; set; }
    }

    /// <summary>Input paths and metadata for an atomic run publication.</summary>
    public sealed class ExperimentRunCommitRequest
    {
        public string BatchDirectory { get; set; }

        public string AttemptDirectory { get; set; }

        public string FinalDirectory { get; set; }

        public ExperimentRunManifest Manifest { get; set; }
    }

    /// <summary>Result of committing or recovering a run publication.</summary>
    public sealed class ExperimentRunCommitResult
    {
        public bool Success { get; internal set; }

        public bool Published { get; internal set; }

        public bool AlreadyPublished { get; internal set; }

        public string Error { get; internal set; }

        public string PublishedDirectory { get; internal set; }

        public string SummaryPath { get; internal set; }

        public ExperimentRunManifest Manifest { get; internal set; }
    }

    /// <summary>Result of validating a prepared or published run directory.</summary>
    public sealed class ExperimentRunValidationResult
    {
        public bool Success { get; internal set; }

        public bool RecoveredManifestBackup { get; internal set; }

        public string Error { get; internal set; }

        public ExperimentRunManifest Manifest { get; internal set; }
    }

    /// <summary>Result of rebuilding the canonical batch run summary.</summary>
    public sealed class ExperimentBatchSummaryResult
    {
        public ExperimentBatchSummaryResult()
        {
            Rows = new List<ExperimentBatchRunSummaryRow>();
        }

        public bool Success { get; internal set; }

        public string Error { get; internal set; }

        public string SummaryPath { get; internal set; }

        public List<ExperimentBatchRunSummaryRow> Rows { get; private set; }
    }

    /// <summary>
    /// Verifies mandatory exports, commits a run manifest, publishes by directory rename, and rebuilds the batch summary.
    /// </summary>
    public sealed class ExperimentRunCommitService
    {
        public const string RunManifestFileName = "run_manifest.json";
        public const string BatchSummaryFileName = "batch_runs.csv";
        public const string DataFileName = "data.csv";
        public const string ContactsFileName = "contacts.csv";
        public const string RunSummaryFileName = "run_summary.csv";
        public const string StateTimeSeriesFileName = "state_timeseries.csv";
        public const string TransmissionEventsFileName = "transmission_events.csv";
        public const string PhysicalContactsFileName = "physical_contacts.csv";
        public const string TraceableContactsFileName = "traceable_contacts.csv";
        public const string TestEventsFileName = "test_events.csv";
        public const string InterventionEventsFileName = "intervention_events.csv";
        public const string HealthcareTimeSeriesFileName = "healthcare_timeseries.csv";
        public const string PopulationEventsFileName = "population_events.csv";
        public const string ErrorsFileName = "errors.json";
        public const string SeedAlgorithmName = "TENUS-RNG-v1/FNV-1a-32";

        private const string PandemicRunPrefix = "pandemic_run_";
        private const string CsvSuffix = ".csv";
        private static readonly string[] MandatoryNamedFiles =
        {
            DataFileName,
            ContactsFileName,
            RunSummaryFileName,
            StateTimeSeriesFileName,
            TransmissionEventsFileName,
            PhysicalContactsFileName,
            TraceableContactsFileName,
            TestEventsFileName,
            InterventionEventsFileName,
            HealthcareTimeSeriesFileName,
            PopulationEventsFileName,
            ErrorsFileName,
        };
        private readonly IAtomicJsonFileStore jsonStore;

        internal static readonly string[] ScientificExtensionFiles =
        {
            "contact_network_summary.csv", "age_mixing_matrix.csv", "contact_degree_distribution.csv",
            "contact_duration_distribution.csv", "contacts_by_time_of_day.csv", "calibration_results.csv",
        };

        public ExperimentRunCommitService(IAtomicJsonFileStore jsonStore)
        {
            if (jsonStore == null)
            {
                throw new ArgumentNullException("jsonStore");
            }

            this.jsonStore = jsonStore;
        }

        /// <summary>Creates the invariant manifest fields from a plan and selected run.</summary>
        public ExperimentRunManifest CreateManifest(
            ExperimentBatchPlan plan,
            ExperimentRunDescriptor run,
            string startedUtc,
            string targetSimulationTimeUtc,
            string simulationEndedUtc,
            string completedUtc,
            string endReason,
            string outputDirectory)
        {
            if (plan == null)
            {
                throw new ArgumentNullException("plan");
            }

            if (run == null || run.Scenario == null || run.Seeds == null)
            {
                throw new ArgumentNullException("run");
            }

            BaselineSaveIdentity baseline = plan.Baseline;
            return new ExperimentRunManifest
            {
                BatchId = plan.BatchId,
                BatchName = plan.BatchName,
                OutputFolderName = plan.OutputFolderName,
                Baseline = baseline,
                BaselineAssetFullName = baseline == null ? null : baseline.AssetFullName,
                BaselineAssetChecksum = baseline == null ? null : baseline.AssetChecksum,
                BaselineDataAssetChecksum = baseline == null ? null : baseline.DataAssetChecksum,
                BaselineLocalFileSha256 = baseline == null ? null : baseline.LocalFileSha256,
                ScenarioIndex = run.ScenarioIndex,
                ScenarioNumber = run.ScenarioIndex + 1,
                ScenarioId = run.Scenario.ScenarioId,
                ScenarioName = run.Scenario.Name,
                Scenario = run.Scenario,
                RunIndex = run.RunIndex,
                RunNumber = run.RunIndex + 1,
                RunId = run.RunId,
                SeedAlgorithm = SeedAlgorithmName,
                MasterSeed = run.MasterSeed,
                PairId = run.PairId,
                PairedSeedMode = plan.PairedSeedMode,
                InitialPopulationSeed = run.Seeds.InitialPopulation,
                DiseaseProgressionSeed = run.Seeds.DiseaseProgression,
                TransmissionSeed = run.Seeds.Transmission,
                SymptomSeed = run.Seeds.Symptom,
                MortalitySeed = run.Seeds.Mortality,
                PandemicSeed = run.Seeds.Pandemic,
                MaskSeed = run.Seeds.Mask,
                TestSeed = run.Seeds.Test,
                ContactSeed = run.Seeds.Contact,
                TestingSeed = run.Seeds.Testing,
                ContactTracingSeed = run.Seeds.ContactTracing,
                InterventionSeed = run.Seeds.Intervention,
                TraitAssignmentAlgorithm = DeterministicCitizenTraitAssigner.AlgorithmName,
                ConfigurationHashAlgorithm = ExperimentConfigurationHasher.AlgorithmName,
                ConfigurationHash = ExperimentConfigurationHasher.Compute(run.Scenario),
                ConfiguredDurationDays = run.Scenario.DurationDays,
                EndMode = run.Scenario.EndMode.ToString(),
                EndReason = endReason,
                StartedUtc = startedUtc,
                TargetSimulationTimeUtc = targetSimulationTimeUtc,
                SimulationEndedUtc = simulationEndedUtc,
                CompletedUtc = completedUtc,
                ModVersion = plan.ModVersion,
                GameVersion = plan.GameVersion,
                GitCommitSha = plan.GitCommitSha,
                GitBranchOrTag = plan.GitBranchOrTag,
                OutputDirectory = outputDirectory,
            };
        }

        /// <summary>Commits and publishes a fully exported attempt directory.</summary>
        public ExperimentRunCommitResult Commit(ExperimentRunCommitRequest request)
        {
            string error;
            if (!ValidateRequest(request, out error))
            {
                return Failure(error);
            }

            string batchDirectory = Path.GetFullPath(request.BatchDirectory);
            string attemptDirectory = Path.GetFullPath(request.AttemptDirectory);
            string finalDirectory = Path.GetFullPath(request.FinalDirectory);
            if (Directory.Exists(finalDirectory) || File.Exists(finalDirectory))
            {
                return Failure("The final run directory already exists and will not be overwritten: " + finalDirectory);
            }

            List<ExperimentGeneratedFileEntry> files;
            if (!TryFingerprintMandatoryFiles(attemptDirectory, request.Manifest, out files, out error))
            {
                return Failure(error);
            }

            ExperimentRunManifest manifest = request.Manifest;
            manifest.Status = ExperimentRunManifest.CompletedStatus;
            manifest.OutputDirectory = finalDirectory;
            manifest.OutputFiles = files;
            if (!ValidateManifestFields(manifest, out error))
            {
                return Failure(error);
            }

            string manifestPath = Path.Combine(attemptDirectory, RunManifestFileName);
            try
            {
                jsonStore.Save(manifestPath, manifest);
            }
            catch (Exception exception)
            {
                return Failure("The run manifest could not be committed: " + exception.Message);
            }

            ExperimentRunValidationResult validation = ValidateRunDirectory(attemptDirectory);
            if (!validation.Success)
            {
                return Failure("The committed run could not be verified: " + validation.Error);
            }

            try
            {
                if (Directory.Exists(finalDirectory) || File.Exists(finalDirectory))
                {
                    return Failure("The final run directory appeared during publication and was not overwritten: " + finalDirectory);
                }

                Directory.Move(attemptDirectory, finalDirectory);
            }
            catch (Exception exception)
            {
                return Failure("The verified run could not be published: " + exception.Message);
            }

            ExperimentBatchSummaryResult summary = RebuildBatchSummary(batchDirectory);
            if (!summary.Success)
            {
                return new ExperimentRunCommitResult
                {
                    Error = "The run was published, but the batch summary could not be rebuilt: " + summary.Error,
                    Published = true,
                    PublishedDirectory = finalDirectory,
                    Manifest = manifest,
                };
            }

            return new ExperimentRunCommitResult
            {
                Success = true,
                Published = true,
                PublishedDirectory = finalDirectory,
                SummaryPath = summary.SummaryPath,
                Manifest = manifest,
            };
        }

        /// <summary>Validates an attempt or final directory and completes publication without overwriting.</summary>
        public ExperimentRunCommitResult Recover(
            string batchDirectory,
            string sourceDirectory,
            string finalDirectory,
            string expectedRunId)
        {
            string error;
            if (!TryNormalizeRecoveryPaths(batchDirectory, sourceDirectory, finalDirectory, out error))
            {
                return Failure(error);
            }

            string batch = Path.GetFullPath(batchDirectory);
            string source = Path.GetFullPath(sourceDirectory);
            string destination = Path.GetFullPath(finalDirectory);
            bool sameDirectory = PathsEqual(source, destination);

            if (Directory.Exists(destination))
            {
                ExperimentRunValidationResult publishedValidation = ValidateRunDirectory(destination);
                if (!publishedValidation.Success)
                {
                    return Failure("The existing final run directory is invalid and was not overwritten: " + publishedValidation.Error);
                }

                if (!MatchesExpectedRun(publishedValidation.Manifest, expectedRunId))
                {
                    return Failure("The existing final run directory belongs to a different RunId and was not overwritten.");
                }

                if (!sameDirectory && Directory.Exists(source))
                {
                    ExperimentRunValidationResult sourceValidation = ValidateRunDirectory(source);
                    if (!sourceValidation.Success
                        || !string.Equals(
                            sourceValidation.Manifest.RunId,
                            publishedValidation.Manifest.RunId,
                            StringComparison.Ordinal))
                    {
                        return Failure("An unrelated or invalid attempt remains beside the existing final run directory.");
                    }
                }

                ExperimentBatchSummaryResult existingSummary = RebuildBatchSummary(batch);
                return FromRecoveredSummary(existingSummary, destination, publishedValidation.Manifest, true);
            }

            if (!Directory.Exists(source))
            {
                return Failure("Neither the recovery source nor the final run directory exists.");
            }

            ExperimentRunValidationResult validation = ValidateRunDirectory(source);
            if (!validation.Success)
            {
                return Failure("The recovery source is invalid: " + validation.Error);
            }

            if (!MatchesExpectedRun(validation.Manifest, expectedRunId))
            {
                return Failure("The recovery source belongs to a different RunId.");
            }

            if (!PathsEqual(validation.Manifest.OutputDirectory, destination))
            {
                return Failure("The recovery destination does not match the committed run manifest.");
            }

            if (validation.RecoveredManifestBackup)
            {
                try
                {
                    jsonStore.Save(Path.Combine(source, RunManifestFileName), validation.Manifest);
                }
                catch (Exception exception)
                {
                    return Failure("The recovered run manifest could not be repaired: " + exception.Message);
                }
            }

            try
            {
                Directory.Move(source, destination);
            }
            catch (Exception exception)
            {
                return Failure("The recovered run could not be published: " + exception.Message);
            }

            ExperimentBatchSummaryResult summary = RebuildBatchSummary(batch);
            return FromRecoveredSummary(summary, destination, validation.Manifest, false);
        }

        /// <summary>Validates the manifest and every mandatory file fingerprint in a run directory.</summary>
        public ExperimentRunValidationResult ValidateRunDirectory(string runDirectory)
        {
            if (string.IsNullOrEmpty(runDirectory) || !Directory.Exists(runDirectory))
            {
                return ValidationFailure("The run directory does not exist.");
            }

            string directory = Path.GetFullPath(runDirectory);
            JsonLoadResult<ExperimentRunManifest> load = jsonStore.TryLoad<ExperimentRunManifest>(
                Path.Combine(directory, RunManifestFileName));
            if (!load.Success)
            {
                return ValidationFailure("The run manifest could not be loaded: " + load.Error);
            }

            string error;
            if (!ValidateManifestFields(load.Value, out error))
            {
                return ValidationFailure(error);
            }

            try
            {
                if (!ValidateManifestFiles(directory, load.Value, out error))
                {
                    return ValidationFailure(error);
                }
            }
            catch (Exception exception)
            {
                return ValidationFailure("The generated run files could not be verified: " + exception.Message);
            }

            return new ExperimentRunValidationResult
            {
                Success = true,
                Manifest = load.Value,
                RecoveredManifestBackup = load.RecoveredFromBackup,
            };
        }

        /// <summary>Atomically rebuilds batch_runs.csv from published run manifests.</summary>
        public ExperimentBatchSummaryResult RebuildBatchSummary(string batchDirectory)
        {
            ExperimentBatchSummaryResult result = new ExperimentBatchSummaryResult();
            if (string.IsNullOrEmpty(batchDirectory) || !Directory.Exists(batchDirectory))
            {
                result.Error = "The batch directory does not exist.";
                return result;
            }

            string batch = Path.GetFullPath(batchDirectory);
            string[] manifestPaths;
            try
            {
                manifestPaths = Directory.GetFiles(batch, RunManifestFileName, SearchOption.AllDirectories);
                Array.Sort<string>(manifestPaths, StringComparer.Ordinal);
            }
            catch (Exception exception)
            {
                result.Error = "Published run manifests could not be enumerated: " + exception.Message;
                return result;
            }

            Dictionary<string, ExperimentRunManifest> uniqueRuns = new Dictionary<string, ExperimentRunManifest>(StringComparer.Ordinal);
            foreach (string manifestPath in manifestPaths)
            {
                string runDirectory = Path.GetDirectoryName(manifestPath);
                if (IsTransientDirectory(batch, runDirectory))
                {
                    continue;
                }

                JsonLoadResult<ExperimentRunManifest> load = jsonStore.TryLoad<ExperimentRunManifest>(manifestPath);
                string error = null;
                if (!load.Success || !ValidateManifestFields(load.Value, out error))
                {
                    result.Error = "A published run manifest is invalid: " + manifestPath + ". " + (load.Success ? error : load.Error);
                    return result;
                }

                if (!uniqueRuns.ContainsKey(load.Value.RunId))
                {
                    uniqueRuns.Add(load.Value.RunId, load.Value);
                }
            }

            foreach (ExperimentRunManifest manifest in uniqueRuns.Values)
            {
                result.Rows.Add(ToSummaryRow(manifest));
            }

            result.Rows.Sort(CompareSummaryRows);
            string summaryPath = Path.Combine(batch, BatchSummaryFileName);
            try
            {
                AtomicWriteText(summaryPath, BuildSummaryCsv(result.Rows));
                AtomicWriteText(Path.Combine(batch, "batch_quality_report.txt"), ExperimentResultsAnalysis.ManifestQuality(uniqueRuns.Values, jsonStore.TryLoad<ExperimentBatchPlan>(Path.Combine(batch, "batch_manifest.json")).Value));
            }
            catch (Exception exception)
            {
                result.Error = exception.Message;
                return result;
            }

            result.Success = true;
            result.SummaryPath = summaryPath;
            return result;
        }

        private static bool ValidateRequest(ExperimentRunCommitRequest request, out string error)
        {
            if (request == null || request.Manifest == null)
            {
                error = "A run commit request and manifest are required.";
                return false;
            }

            if (string.IsNullOrEmpty(request.BatchDirectory)
                || string.IsNullOrEmpty(request.AttemptDirectory)
                || string.IsNullOrEmpty(request.FinalDirectory))
            {
                error = "Batch, attempt, and final directories are required.";
                return false;
            }

            string batch;
            string attempt;
            string final;
            try
            {
                batch = Path.GetFullPath(request.BatchDirectory);
                attempt = Path.GetFullPath(request.AttemptDirectory);
                final = Path.GetFullPath(request.FinalDirectory);
            }
            catch (Exception exception)
            {
                error = "A run commit path is invalid: " + exception.Message;
                return false;
            }

            if (!Directory.Exists(attempt))
            {
                error = "The in-progress run directory does not exist.";
                return false;
            }

            if (!ExperimentPathResolver.IsContained(batch, attempt)
                || !ExperimentPathResolver.IsContained(batch, final)
                || PathsEqual(attempt, final)
                || !PathsEqual(Path.GetDirectoryName(attempt), Path.GetDirectoryName(final)))
            {
                error = "Attempt and final directories must be distinct siblings inside the batch directory.";
                return false;
            }

            error = null;
            return true;
        }

        private static bool TryNormalizeRecoveryPaths(
            string batchDirectory,
            string sourceDirectory,
            string finalDirectory,
            out string error)
        {
            if (string.IsNullOrEmpty(batchDirectory)
                || string.IsNullOrEmpty(sourceDirectory)
                || string.IsNullOrEmpty(finalDirectory))
            {
                error = "Batch, source, and final recovery directories are required.";
                return false;
            }

            try
            {
                string batch = Path.GetFullPath(batchDirectory);
                string source = Path.GetFullPath(sourceDirectory);
                string final = Path.GetFullPath(finalDirectory);
                if (!ExperimentPathResolver.IsContained(batch, source)
                    || !ExperimentPathResolver.IsContained(batch, final)
                    || (!PathsEqual(source, final)
                        && !PathsEqual(Path.GetDirectoryName(source), Path.GetDirectoryName(final))))
                {
                    error = "Recovery directories must be the final directory or sibling directories inside the batch.";
                    return false;
                }
            }
            catch (Exception exception)
            {
                error = "A recovery path is invalid: " + exception.Message;
                return false;
            }

            error = null;
            return true;
        }

        private static bool TryFingerprintMandatoryFiles(
            string directory,
            ExperimentRunManifest manifest,
            out List<ExperimentGeneratedFileEntry> entries,
            out string error)
        {
            entries = new List<ExperimentGeneratedFileEntry>();
            try
            {
                if (!ValidateContactInventory(manifest, out error)) return false;
                if (!ValidateStoredContactInventory(directory, manifest, out error)) return false;
                if (Directory.GetFiles(directory, "*.tmp", SearchOption.AllDirectories).Length != 0
                    || Directory.GetFiles(directory, ".tmp-*", SearchOption.AllDirectories).Length != 0)
                    throw new IOException("Unpublished temporary output prevents scientific completion.");
                string[] files = Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly);
                List<string> pandemicFiles = new List<string>();
                foreach (string file in files)
                {
                    string name = Path.GetFileName(file);
                    if (name.StartsWith(PandemicRunPrefix, StringComparison.OrdinalIgnoreCase)
                        && name.EndsWith(CsvSuffix, StringComparison.OrdinalIgnoreCase))
                    {
                        pandemicFiles.Add(file);
                    }
                }

                if (pandemicFiles.Count != 1)
                {
                    error = "Exactly one mandatory pandemic_run_*.csv export is required.";
                    return false;
                }

                entries.Add(CreateFileEntry("PandemicRun", directory, pandemicFiles[0]));
                foreach (string fileName in RequiredNamedFiles(manifest))
                {
                    string path = Path.Combine(directory, fileName);
                    if (!File.Exists(path))
                    {
                        error = "A mandatory scientific run export is missing: " + fileName;
                        return false;
                    }

                    entries.Add(CreateFileEntry(GetFileRole(fileName), directory, path));
                }

                if (manifest.ScientificExtensionsVersion >= 1)
                    foreach (string fileName in ScientificExtensionFiles)
                    {
                        string path = Path.Combine(directory, fileName);
                        if (!File.Exists(path)) { error = "A mandatory scientific extension is missing: " + fileName; return false; }
                        entries.Add(CreateFileEntry("ScientificExtension", directory, path));
                    }
                if (manifest.ScientificExportSchemaVersion >= 2)
                    foreach (var contact in manifest.ContactFiles)
                    {
                        var entry = CreateFileEntry("ScientificContacts", directory, Path.Combine(directory, contact.RelativePath));
                        if (entry.LengthBytes != contact.LengthBytes || !string.Equals(entry.Sha256, contact.Sha256, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidOperationException("Contact storage identity changed before commit.");
                        entry.RowCount = contact.RowCount;
                        entry.SimulationStartTime = contact.SimulationStartTime;
                        entry.SimulationEndTime = contact.SimulationEndTime;
                        entries.Add(entry);
                    }
                string diagnostics = Path.Combine(directory, "performance_diagnostics.csv");
                if (File.Exists(diagnostics)) entries.Add(CreateFileEntry("PerformanceDiagnostics", directory, diagnostics));
                entries.Sort(CompareFileEntries);
            }
            catch (Exception exception)
            {
                error = "A mandatory run export could not be fingerprinted: " + exception.Message;
                return false;
            }

            error = null;
            return true;
        }

        private static ExperimentGeneratedFileEntry CreateFileEntry(string role, string directory, string path)
        {
            string fullPath = Path.GetFullPath(path);
            if (!ExperimentPathResolver.IsContained(directory, fullPath))
            {
                throw new InvalidOperationException("A generated run file escaped its run directory.");
            }

            FileInfo info = new FileInfo(fullPath);
            string hash;
            using (SHA256 algorithm = SHA256.Create())
            using (FileStream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                hash = ToHex(algorithm.ComputeHash(stream));
            }

            info.Refresh();
            return new ExperimentGeneratedFileEntry
            {
                Role = role,
                RelativePath = fullPath.Substring(Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length + 1).Replace(Path.DirectorySeparatorChar, '/'),
                LengthBytes = info.Length,
                Sha256 = hash,
            };
        }

        private static string GetFileRole(string fileName)
        {
            if (fileName == "contact_step_summary.csv" || fileName == "contact_episode_summary.csv" || fileName == "contact_episode_duration_distribution.csv") return "ScientificExtension";
            if (string.Equals(fileName, DataFileName, StringComparison.OrdinalIgnoreCase)) return "ObserverData";
            if (string.Equals(fileName, ContactsFileName, StringComparison.OrdinalIgnoreCase)) return "LegacyContacts";
            if (string.Equals(fileName, RunSummaryFileName, StringComparison.OrdinalIgnoreCase)) return "RunSummary";
            if (string.Equals(fileName, StateTimeSeriesFileName, StringComparison.OrdinalIgnoreCase)) return "StateTimeSeries";
            if (string.Equals(fileName, TransmissionEventsFileName, StringComparison.OrdinalIgnoreCase)) return "TransmissionEvents";
            if (string.Equals(fileName, PhysicalContactsFileName, StringComparison.OrdinalIgnoreCase)) return "PhysicalContacts";
            if (string.Equals(fileName, TraceableContactsFileName, StringComparison.OrdinalIgnoreCase)) return "TraceableContacts";
            if (string.Equals(fileName, TestEventsFileName, StringComparison.OrdinalIgnoreCase)) return "TestEvents";
            if (string.Equals(fileName, InterventionEventsFileName, StringComparison.OrdinalIgnoreCase)) return "InterventionEvents";
            if (string.Equals(fileName, HealthcareTimeSeriesFileName, StringComparison.OrdinalIgnoreCase)) return "HealthcareTimeSeries";
            if (string.Equals(fileName, PopulationEventsFileName, StringComparison.OrdinalIgnoreCase)) return "PopulationEvents";
            if (string.Equals(fileName, ErrorsFileName, StringComparison.OrdinalIgnoreCase)) return "Errors";
            throw new ArgumentOutOfRangeException(nameof(fileName), "The scientific file role is unknown.");
        }

        private static bool ValidateManifestFields(ExperimentRunManifest manifest, out string error)
        {
            if (manifest == null)
            {
                error = "The run manifest is missing.";
                return false;
            }

            if (manifest.ScientificExtensionsVersion < 0 || manifest.ScientificExtensionsVersion > 1)
            {
                error = "The scientific extension version is unsupported.";
                return false;
            }

            if (!ValidateContactInventory(manifest, out error)) return false;

            if (manifest.SchemaVersion != ExperimentSchema.CurrentVersion)
            {
                error = "The run manifest schema version is unsupported.";
                return false;
            }

            if (!string.Equals(manifest.Status, ExperimentRunManifest.CompletedStatus, StringComparison.Ordinal))
            {
                error = "The run manifest is not completed.";
                return false;
            }

            if (string.IsNullOrEmpty(manifest.BatchId)
                || string.IsNullOrEmpty(manifest.BatchName)
                || string.IsNullOrEmpty(manifest.BaselineAssetFullName)
                || string.IsNullOrEmpty(manifest.BaselineAssetChecksum)
                || string.IsNullOrEmpty(manifest.ScenarioId)
                || string.IsNullOrEmpty(manifest.ScenarioName)
                || string.IsNullOrEmpty(manifest.RunId)
                || string.IsNullOrEmpty(manifest.SeedAlgorithm)
                || string.IsNullOrEmpty(manifest.EndMode)
                || string.IsNullOrEmpty(manifest.EndReason)
                || string.IsNullOrEmpty(manifest.StartedUtc)
                || string.IsNullOrEmpty(manifest.SimulationEndedUtc)
                || string.IsNullOrEmpty(manifest.CompletedUtc)
                || string.IsNullOrEmpty(manifest.ModVersion)
                || string.IsNullOrEmpty(manifest.GameVersion)
                || string.IsNullOrEmpty(manifest.GitCommitSha)
                || string.IsNullOrEmpty(manifest.GitBranchOrTag)
                || string.IsNullOrEmpty(manifest.TraitAssignmentAlgorithm)
                || string.IsNullOrEmpty(manifest.ConfigurationHashAlgorithm)
                || string.IsNullOrEmpty(manifest.ConfigurationHash)
                || string.IsNullOrEmpty(manifest.OutputDirectory)
                || manifest.Baseline == null)
            {
                error = "The run manifest is missing required identity, completion, baseline, or output metadata.";
                return false;
            }

            if (!string.Equals(manifest.SeedAlgorithm, SeedAlgorithmName, StringComparison.Ordinal))
            {
                error = "The run manifest names an unsupported deterministic seed algorithm.";
                return false;
            }

            if (!string.Equals(manifest.TraitAssignmentAlgorithm, DeterministicCitizenTraitAssigner.AlgorithmName, StringComparison.Ordinal)
                || (!string.Equals(manifest.ConfigurationHashAlgorithm, ExperimentConfigurationHasher.AlgorithmName, StringComparison.Ordinal)
                    && !(manifest.ScientificExportSchemaVersion < 2 && manifest.ConfigurationHashAlgorithm == ExperimentConfigurationHasher.LegacyAlgorithmName)))
            {
                error = "The run manifest names an unsupported deterministic trait or configuration-hash algorithm.";
                return false;
            }

            if (!string.Equals(manifest.BaselineAssetFullName, manifest.Baseline.AssetFullName, StringComparison.Ordinal)
                || !string.Equals(manifest.BaselineAssetChecksum, manifest.Baseline.AssetChecksum, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(
                    manifest.BaselineDataAssetChecksum ?? string.Empty,
                    manifest.Baseline.DataAssetChecksum ?? string.Empty,
                    StringComparison.OrdinalIgnoreCase)
                || !string.Equals(
                    manifest.BaselineLocalFileSha256 ?? string.Empty,
                    manifest.Baseline.LocalFileSha256 ?? string.Empty,
                    StringComparison.OrdinalIgnoreCase))
            {
                error = "The flattened baseline identity does not match the embedded baseline descriptor.";
                return false;
            }

            if (manifest.FinalTrackedPopulation < 0
                || manifest.FinalSusceptible < 0
                || manifest.FinalExposed < 0
                || manifest.FinalInfectious < 0
                || manifest.FinalPostInfectiousIll < 0
                || manifest.FinalSymptomatic < 0
                || manifest.FinalSick < 0
                || manifest.FinalRecovered < 0
                || manifest.FinalDead < 0
                || manifest.FinalTransmissionsTotal < 0
                || manifest.InitialSeedCount < 0
                || manifest.SecondaryTransmissionsTotal < 0
                || manifest.CumulativeInfections < 0
                || manifest.HospitalizationsTotal < 0
                || manifest.PhysicalContactsTotal < 0L
                || manifest.TraceableContactsTotal < 0L
                || double.IsNaN(manifest.FinalAttackRatePercent)
                || double.IsInfinity(manifest.FinalAttackRatePercent)
                || manifest.FinalAttackRatePercent < 0d
                || double.IsNaN(manifest.FinalFatalityRatePercent)
                || double.IsInfinity(manifest.FinalFatalityRatePercent)
                || manifest.FinalFatalityRatePercent < 0d)
            {
                error = "The run manifest contains invalid final snapshot metrics.";
                return false;
            }

            if (!IsFiniteNonnegative(manifest.FinalPrevalencePercent)
                || !IsFiniteNonnegative(manifest.ActualMaskUsagePercent)
                || !IsFiniteNonnegative(manifest.TotalIsolationPersonDays)
                || !IsFiniteNonnegative(manifest.TotalQuarantinePersonDays)
                || (manifest.ResolvedCaseFatalityRatioPercent.HasValue
                    && !IsFiniteNonnegative(manifest.ResolvedCaseFatalityRatioPercent.Value))
                || (manifest.EmpiricalSecondaryInfectionsPerInfector.HasValue
                    && !IsFiniteNonnegative(manifest.EmpiricalSecondaryInfectionsPerInfector.Value)))
            {
                error = "The run manifest contains invalid scientific outcome metrics.";
                return false;
            }

            if (manifest.FinalSusceptible + manifest.FinalExposed + manifest.FinalInfectious
                + manifest.FinalPostInfectiousIll + manifest.FinalRecovered + manifest.FinalDead
                != manifest.FinalTrackedPopulation)
            {
                error = "The final disease compartments do not equal the tracked population.";
                return false;
            }

            if (manifest.SecondaryTransmissionsTotal != manifest.FinalTransmissionsTotal
                || manifest.CumulativeInfections != manifest.InitialSeedCount + manifest.SecondaryTransmissionsTotal)
            {
                error = "The run manifest contains inconsistent seed and secondary-transmission metrics.";
                return false;
            }

            if (manifest.ScenarioIndex < 0
                || manifest.RunIndex < 0
                || manifest.ScenarioNumber != manifest.ScenarioIndex + 1
                || manifest.RunNumber != manifest.RunIndex + 1)
            {
                error = "The run manifest contains inconsistent zero-based and one-based indices.";
                return false;
            }

            if ((manifest.PairedSeedMode && manifest.PairId != manifest.RunIndex + 1)
                || (!manifest.PairedSeedMode && manifest.PairId != 0))
            {
                error = "The run manifest contains an inconsistent paired-seed identifier.";
                return false;
            }

            if (manifest.MasterSeed < 0
                || manifest.PairId < 0
                || manifest.InitialPopulationSeed < 0
                || manifest.DiseaseProgressionSeed < 0
                || manifest.TransmissionSeed < 0
                || manifest.SymptomSeed < 0
                || manifest.MortalitySeed < 0
                || manifest.PandemicSeed < 0
                || manifest.MaskSeed < 0
                || manifest.TestSeed < 0
                || manifest.ContactSeed < 0
                || manifest.TestingSeed < 0
                || manifest.ContactTracingSeed < 0
                || manifest.InterventionSeed < 0)
            {
                error = "The run manifest contains a negative deterministic seed.";
                return false;
            }

            ExperimentSeedSet expectedSeeds = new FnvExperimentSeedProvider().DeriveSeeds(manifest.MasterSeed);
            if (manifest.InitialPopulationSeed != expectedSeeds.InitialPopulation
                || manifest.DiseaseProgressionSeed != expectedSeeds.DiseaseProgression
                || manifest.TransmissionSeed != expectedSeeds.Transmission
                || manifest.SymptomSeed != expectedSeeds.Symptom
                || manifest.MortalitySeed != expectedSeeds.Mortality
                || manifest.PandemicSeed != expectedSeeds.Pandemic
                || manifest.MaskSeed != expectedSeeds.Mask
                || manifest.TestSeed != expectedSeeds.Test
                || manifest.ContactSeed != expectedSeeds.Contact
                || manifest.TestingSeed != expectedSeeds.Testing
                || manifest.ContactTracingSeed != expectedSeeds.ContactTracing
                || manifest.InterventionSeed != expectedSeeds.Intervention)
            {
                error = "The component seeds do not match the deterministic master-seed derivation.";
                return false;
            }

            if (manifest.Scenario == null
                || !string.Equals(
                    manifest.ConfigurationHash,
                    manifest.ConfigurationHashAlgorithm == ExperimentConfigurationHasher.LegacyAlgorithmName
                        ? ExperimentConfigurationHasher.ComputeLegacy(manifest.Scenario) : ExperimentConfigurationHasher.Compute(manifest.Scenario),
                    StringComparison.OrdinalIgnoreCase))
            {
                error = "The configuration hash does not match the embedded scenario snapshot.";
                return false;
            }

            if (manifest.ConfiguredDurationDays <= 0d
                || double.IsNaN(manifest.ConfiguredDurationDays)
                || double.IsInfinity(manifest.ConfiguredDurationDays))
            {
                error = "The run manifest contains an invalid configured duration.";
                return false;
            }

            if (manifest.OutputFiles == null || manifest.OutputFiles.Count == 0)
            {
                error = "The run manifest contains no generated file identities.";
                return false;
            }

            error = null;
            return true;
        }

        private static bool ValidateManifestFiles(string directory, ExperimentRunManifest manifest, out string error)
        {
            if (!ValidateStoredContactInventory(directory, manifest, out error)) return false;
            if (Directory.GetFiles(directory, "*.tmp", SearchOption.AllDirectories).Length != 0)
            { error = "Unpublished temporary output prevents scientific completion."; return false; }
            int pandemicCount = 0;
            var mandatoryNames = new HashSet<string>(RequiredNamedFiles(manifest), StringComparer.OrdinalIgnoreCase);
            if (manifest.ScientificExtensionsVersion >= 1)
                foreach (string name in ScientificExtensionFiles) mandatoryNames.Add(name);
            var foundMandatoryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, bool> paths = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (ExperimentGeneratedFileEntry entry in manifest.OutputFiles)
            {
                if (entry == null
                    || string.IsNullOrEmpty(entry.RelativePath)
                    || string.IsNullOrEmpty(entry.Sha256)
                    || entry.LengthBytes < 0L
                    || Path.IsPathRooted(entry.RelativePath)
                    || !IsAllowedFilePath(entry.RelativePath, manifest.ScientificExportSchemaVersion))
                {
                    error = "A generated file identity in the run manifest is invalid.";
                    return false;
                }

                string path = Path.GetFullPath(Path.Combine(directory, entry.RelativePath));
                if (!ExperimentPathResolver.IsContained(directory, path)
                    || paths.ContainsKey(path)
                    || !File.Exists(path))
                {
                    error = "A generated file is missing, duplicated, or outside the run directory: " + entry.RelativePath;
                    return false;
                }

                paths.Add(path, true);
                FileInfo info = new FileInfo(path);
                if (info.Length != entry.LengthBytes || !HashMatches(path, entry.Sha256))
                {
                    error = "A generated file failed its SHA-256 or length check: " + entry.RelativePath;
                    return false;
                }

                string name = Path.GetFileName(entry.RelativePath);
                if (name.StartsWith(PandemicRunPrefix, StringComparison.OrdinalIgnoreCase)
                    && name.EndsWith(CsvSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    ++pandemicCount;
                }
                else if (mandatoryNames.Contains(name))
                {
                    foundMandatoryNames.Add(name);
                }
            }

            if (manifest.ScientificExportSchemaVersion >= 2)
            {
                foreach (var contact in manifest.ContactFiles)
                {
                    var found = manifest.OutputFiles.Find(entry => entry.RelativePath == contact.RelativePath);
                    if (found == null || found.LengthBytes != contact.LengthBytes || found.Sha256 != contact.Sha256
                        || found.RowCount != contact.RowCount || found.SimulationStartTime != contact.SimulationStartTime
                        || found.SimulationEndTime != contact.SimulationEndTime)
                    { error = "Contact partition inventory disagrees with generated-file identities."; return false; }
                }
            }

            if (pandemicCount != 1 || foundMandatoryNames.Count != mandatoryNames.Count)
            {
                error = "The run manifest does not identify the complete mandatory scientific output package.";
                return false;
            }

            string[] physicalFiles = Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly);
            int physicalPandemicCount = 0;
            foreach (string physicalFile in physicalFiles)
            {
                string fileName = Path.GetFileName(physicalFile);
                if (fileName.StartsWith(PandemicRunPrefix, StringComparison.OrdinalIgnoreCase)
                    && fileName.EndsWith(CsvSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    ++physicalPandemicCount;
                }
            }

            bool allMandatoryFilesExist = physicalPandemicCount == 1;
            foreach (string name in RequiredNamedFiles(manifest))
                allMandatoryFilesExist &= File.Exists(Path.Combine(directory, name));

            if (!allMandatoryFilesExist)
            {
                error = "The run directory does not contain the complete mandatory scientific output package.";
                return false;
            }

            error = null;
            return true;
        }

        private static IEnumerable<string> RequiredNamedFiles(ExperimentRunManifest manifest)
        {
            foreach (string name in MandatoryNamedFiles)
                if (manifest.ScientificExportSchemaVersion < 2 || (name != PhysicalContactsFileName && name != TraceableContactsFileName))
                    yield return name;
            if (manifest.ScientificExportSchemaVersion >= 2)
            {
                yield return "contact_episode_summary.csv";
                yield return "contact_step_summary.csv";
                yield return "contact_episode_duration_distribution.csv";
            }
        }

        internal static void PopulateContactMetadata(ExperimentRunManifest manifest,
            RealTime.Pandemic.ExperimentRecorderSnapshot snapshot, ExperimentScenarioSnapshot settings)
        {
            manifest.ScientificExportSchemaVersion = 2;
            manifest.ContactExportMode = snapshot.ContactExportMode.ToString();
            manifest.ContactRepresentation = snapshot.ContactExportMode == Config.ScientificContactExportMode.Standard ? "episodes"
                : snapshot.ContactExportMode == Config.ScientificContactExportMode.FullRaw ? "epidemiological_steps" : "summaries";
            manifest.Compression = "gzip";
            manifest.Partitioning = snapshot.ContactExportMode == Config.ScientificContactExportMode.FullRaw ? "simulation_day" : "none";
            manifest.EpidemicStepMinutes = settings.EpidemicStepMinutes;
            manifest.ContactPersistenceModel = settings.ContactPersistenceModel.ToString();
            manifest.ContactPersistenceMinutes = new Dictionary<string, uint>
            {
                { "school", settings.ContactPersistenceMinutesSchool }, { "university", settings.ContactPersistenceMinutesUniversity },
                { "workplace", settings.ContactPersistenceMinutesWorkplace }, { "healthcare", settings.ContactPersistenceMinutesHealthcare },
                { "commercial", settings.ContactPersistenceMinutesCommercial }, { "leisure", settings.ContactPersistenceMinutesLeisure },
                { "public_transport", settings.ContactPersistenceMinutesTransit }, { "residential_shared_area", settings.ContactPersistenceMinutesResidentialSharedArea },
            };
            manifest.ContactEpisodesTotal = snapshot.ContactEpisodesTotal;
            manifest.PhysicalContactsTotal = snapshot.PhysicalContactsTotal;
            manifest.ContactFiles = new List<ExperimentGeneratedFileEntry>();
            foreach (var file in snapshot.ContactFiles)
                manifest.ContactFiles.Add(new ExperimentGeneratedFileEntry { RelativePath = file.RelativePath,
                    LengthBytes = file.LengthBytes, Sha256 = file.Sha256, RowCount = file.RowCount, Role = "ScientificContacts",
                    SimulationStartTime = file.StartTime?.ToString("o", CultureInfo.InvariantCulture),
                    SimulationEndTime = file.EndTime?.ToString("o", CultureInfo.InvariantCulture) });
        }

        private static bool IsAllowedFilePath(string relative, int version)
        {
            if (relative == Path.GetFileName(relative)) return true;
            return version == 2 && System.Text.RegularExpressions.Regex.IsMatch(relative, @"\Aphysical_contacts/day_[0-9]{3,}\.csv\.gz\z");
        }

        private static bool ValidateStoredContactInventory(string directory, ExperimentRunManifest manifest, out string error)
        {
            error = null;
            if (manifest.ScientificExportSchemaVersion < 2) return true;
            var expected = new HashSet<string>(StringComparer.Ordinal);
            foreach (var file in manifest.ContactFiles) expected.Add(file.RelativePath);
            var actual = new HashSet<string>(StringComparer.Ordinal);
            if (File.Exists(Path.Combine(directory, "contact_episodes.csv.gz"))) actual.Add("contact_episodes.csv.gz");
            string partitions = Path.Combine(directory, "physical_contacts");
            if (Directory.Exists(partitions))
                foreach (string file in Directory.GetFiles(partitions, "*", SearchOption.AllDirectories))
                    actual.Add("physical_contacts/" + file.Substring(partitions.Length + 1).Replace(Path.DirectorySeparatorChar, '/'));
            if (!actual.SetEquals(expected) || File.Exists(Path.Combine(directory, PhysicalContactsFileName))
                || File.Exists(Path.Combine(directory, TraceableContactsFileName)))
            { error = "Stored contact files do not match the authoritative mode inventory."; return false; }
            return true;
        }

        private static bool ValidateContactInventory(ExperimentRunManifest manifest, out string error)
        {
            error = null;
            if (manifest.ScientificExportSchemaVersion == 0 || manifest.ScientificExportSchemaVersion == 1) return true;
            if (manifest.ScientificExportSchemaVersion != 2 || manifest.ContactFiles == null
                || manifest.Compression != "gzip" || manifest.EpidemicStepMinutes == 0
                || manifest.ContactPersistenceMinutes == null || string.IsNullOrEmpty(manifest.ContactPersistenceModel))
            { error = "Scientific contact schema metadata is incomplete or unsupported."; return false; }
            var settings = manifest.Scenario?.Settings;
            if (settings == null || manifest.EpidemicStepMinutes != settings.EpidemicStepMinutes
                || manifest.ContactExportMode != settings.ScientificContactExportMode.ToString()
                || manifest.ContactPersistenceModel != settings.ContactPersistenceModel.ToString()
                || manifest.ContactEpisodesTotal < 0)
            { error = "Contact metadata disagrees with the scientific scenario."; return false; }
            string[] windowNames = { "school", "university", "workplace", "healthcare", "commercial", "leisure", "public_transport", "residential_shared_area" };
            uint[] windowValues = { settings.ContactPersistenceMinutesSchool, settings.ContactPersistenceMinutesUniversity,
                settings.ContactPersistenceMinutesWorkplace, settings.ContactPersistenceMinutesHealthcare,
                settings.ContactPersistenceMinutesCommercial, settings.ContactPersistenceMinutesLeisure,
                settings.ContactPersistenceMinutesTransit, settings.ContactPersistenceMinutesResidentialSharedArea };
            if (manifest.ContactPersistenceMinutes.Count != windowNames.Length)
            { error = "Contact persistence inventory is incomplete."; return false; }
            for (int i = 0; i < windowNames.Length; i++)
                if (!manifest.ContactPersistenceMinutes.TryGetValue(windowNames[i], out uint minutes) || minutes != windowValues[i])
                { error = "Contact persistence metadata disagrees with the scientific scenario."; return false; }
            bool standard = manifest.ContactExportMode == "Standard";
            bool raw = manifest.ContactExportMode == "FullRaw";
            bool summary = manifest.ContactExportMode == "SummaryOnly";
            if ((!standard && !raw && !summary)
                || (standard && (manifest.ContactFiles.Count != 1 || manifest.ContactRepresentation != "episodes" || manifest.Partitioning != "none"))
                || (raw && (manifest.ContactFiles.Count == 0 || manifest.ContactRepresentation != "epidemiological_steps" || manifest.Partitioning != "simulation_day"))
                || (summary && (manifest.ContactFiles.Count != 0 || manifest.ContactRepresentation != "summaries" || manifest.Partitioning != "none")))
            { error = "Contact mode and representation/partition inventory disagree."; return false; }
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long storedRows = 0;
            foreach (var file in manifest.ContactFiles)
            {
                if (file == null || string.IsNullOrEmpty(file.RelativePath) || !seen.Add(file.RelativePath)
                    || (standard && file.RelativePath != "contact_episodes.csv.gz")
                    || (raw && !System.Text.RegularExpressions.Regex.IsMatch(file.RelativePath, @"\Aphysical_contacts/day_[0-9]{3,}\.csv\.gz\z"))
                    || !file.RowCount.HasValue || file.RowCount.Value < 0 || file.LengthBytes <= 0 || string.IsNullOrEmpty(file.Sha256))
                { error = "Contact partition metadata is invalid."; return false; }
                if (file.RowCount.Value > long.MaxValue - storedRows)
                { error = "Contact partition row counts overflow."; return false; }
                storedRows += file.RowCount.Value;
                if (file.RowCount.Value > 0)
                {
                    DateTime first, last;
                    if (!DateTime.TryParse(file.SimulationStartTime, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out first)
                        || !DateTime.TryParse(file.SimulationEndTime, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out last)
                        || last <= first)
                    { error = "Contact partition time range is invalid."; return false; }
                }
                else if (file.SimulationStartTime != null || file.SimulationEndTime != null)
                { error = "Empty contact partitions cannot have observed time ranges."; return false; }
            }
            if ((standard && storedRows != manifest.ContactEpisodesTotal) || (raw && storedRows != manifest.PhysicalContactsTotal))
            { error = "Contact partition row counts disagree with the recorded run totals."; return false; }
            return true;
        }

        private static bool HashMatches(string path, string expected)
        {
            using (SHA256 algorithm = SHA256.Create())
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return string.Equals(ToHex(algorithm.ComputeHash(stream)), expected, StringComparison.OrdinalIgnoreCase);
            }
        }

        private static bool IsFiniteNonnegative(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d;
        }

        private static string ToHex(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static ExperimentBatchRunSummaryRow ToSummaryRow(ExperimentRunManifest manifest)
        {
            return new ExperimentBatchRunSummaryRow
            {
                SchemaVersion = manifest.SchemaVersion,
                BatchId = manifest.BatchId,
                BatchName = manifest.BatchName,
                BaselineAssetFullName = manifest.BaselineAssetFullName,
                BaselineAssetChecksum = manifest.BaselineAssetChecksum,
                ScenarioIndex = manifest.ScenarioIndex,
                ScenarioNumber = manifest.ScenarioNumber,
                ScenarioId = manifest.ScenarioId,
                ScenarioName = manifest.ScenarioName,
                RunIndex = manifest.RunIndex,
                RunNumber = manifest.RunNumber,
                RunId = manifest.RunId,
                SeedAlgorithm = manifest.SeedAlgorithm,
                MasterSeed = manifest.MasterSeed,
                PairId = manifest.PairId,
                PairedSeedMode = manifest.PairedSeedMode,
                InitialPopulationSeed = manifest.InitialPopulationSeed,
                DiseaseProgressionSeed = manifest.DiseaseProgressionSeed,
                TransmissionSeed = manifest.TransmissionSeed,
                SymptomSeed = manifest.SymptomSeed,
                MortalitySeed = manifest.MortalitySeed,
                PandemicSeed = manifest.PandemicSeed,
                MaskSeed = manifest.MaskSeed,
                TestSeed = manifest.TestSeed,
                ContactSeed = manifest.ContactSeed,
                TestingSeed = manifest.TestingSeed,
                ContactTracingSeed = manifest.ContactTracingSeed,
                InterventionSeed = manifest.InterventionSeed,
                ConfigurationHash = manifest.ConfigurationHash,
                FinalTrackedPopulation = manifest.FinalTrackedPopulation,
                FinalSusceptible = manifest.FinalSusceptible,
                FinalExposed = manifest.FinalExposed,
                FinalInfectious = manifest.FinalInfectious,
                FinalPostInfectiousIll = manifest.FinalPostInfectiousIll,
                FinalSymptomatic = manifest.FinalSymptomatic,
                FinalSick = manifest.FinalSick,
                FinalRecovered = manifest.FinalRecovered,
                FinalDead = manifest.FinalDead,
                FinalTransmissionsTotal = manifest.FinalTransmissionsTotal,
                InitialSeedCount = manifest.InitialSeedCount,
                SecondaryTransmissionsTotal = manifest.SecondaryTransmissionsTotal,
                CumulativeInfections = manifest.CumulativeInfections,
                HospitalizationsTotal = manifest.HospitalizationsTotal,
                FinalAttackRatePercent = manifest.FinalAttackRatePercent,
                FinalFatalityRatePercent = manifest.FinalFatalityRatePercent,
                FinalPrevalencePercent = manifest.FinalPrevalencePercent,
                ResolvedCaseFatalityRatioPercent = manifest.ResolvedCaseFatalityRatioPercent,
                EmpiricalSecondaryInfectionsPerInfector = manifest.EmpiricalSecondaryInfectionsPerInfector,
                ActualMaskUsagePercent = manifest.ActualMaskUsagePercent,
                TotalIsolationPersonDays = manifest.TotalIsolationPersonDays,
                TotalQuarantinePersonDays = manifest.TotalQuarantinePersonDays,
                PhysicalContactsTotal = manifest.PhysicalContactsTotal,
                TraceableContactsTotal = manifest.TraceableContactsTotal,
                ConfiguredDurationDays = manifest.ConfiguredDurationDays,
                EndMode = manifest.EndMode,
                EndReason = manifest.EndReason,
                StartedUtc = manifest.StartedUtc,
                TargetSimulationTimeUtc = manifest.TargetSimulationTimeUtc,
                SimulationEndedUtc = manifest.SimulationEndedUtc,
                CompletedUtc = manifest.CompletedUtc,
                ModVersion = manifest.ModVersion,
                GameVersion = manifest.GameVersion,
                GitCommitSha = manifest.GitCommitSha,
                GitBranchOrTag = manifest.GitBranchOrTag,
                OutputDirectory = manifest.OutputDirectory,
            };
        }

        private static string BuildSummaryCsv(IList<ExperimentBatchRunSummaryRow> rows)
        {
            StringBuilder csv = new StringBuilder();
            csv.Append("SchemaVersion,BatchId,BatchName,BaselineAssetFullName,BaselineAssetChecksum,");
            csv.Append("ScenarioIndex,ScenarioNumber,ScenarioId,ScenarioName,RunIndex,RunNumber,RunId,");
            csv.Append("SeedAlgorithm,MasterSeed,PairId,PairedSeedMode,InitialPopulationSeed,DiseaseProgressionSeed,TransmissionSeed,SymptomSeed,MortalitySeed,");
            csv.Append("PandemicSeed,MaskSeed,TestSeed,ContactSeed,TestingSeed,ContactTracingSeed,InterventionSeed,ConfigurationHash,");
            csv.Append("FinalTrackedPopulation,FinalSusceptible,FinalExposed,FinalInfectious,FinalPostInfectiousIll,FinalSymptomatic,FinalSick,FinalRecovered,FinalDead,");
            csv.Append("FinalTransmissionsTotal,InitialSeedCount,SecondaryTransmissionsTotal,CumulativeInfections,HospitalizationsTotal,FinalAttackRatePercent,FinalFatalityRatePercent,FinalPrevalencePercent,ResolvedCaseFatalityRatioPercent,EmpiricalSecondaryInfectionsPerInfector,ActualMaskUsagePercent,TotalIsolationPersonDays,TotalQuarantinePersonDays,PhysicalContactsTotal,TraceableContactsTotal,ConfiguredDurationDays,");
            csv.Append("EndMode,EndReason,StartedUtc,TargetSimulationTimeUtc,SimulationEndedUtc,CompletedUtc,");
            csv.Append("ModVersion,GameVersion,GitCommitSha,GitBranchOrTag,OutputDirectory\r\n");
            foreach (ExperimentBatchRunSummaryRow row in rows)
            {
                AppendCsvRow(csv, new[]
                {
                    row.SchemaVersion.ToString(CultureInfo.InvariantCulture),
                    row.BatchId,
                    row.BatchName,
                    row.BaselineAssetFullName,
                    row.BaselineAssetChecksum,
                    row.ScenarioIndex.ToString(CultureInfo.InvariantCulture),
                    row.ScenarioNumber.ToString(CultureInfo.InvariantCulture),
                    row.ScenarioId,
                    row.ScenarioName,
                    row.RunIndex.ToString(CultureInfo.InvariantCulture),
                    row.RunNumber.ToString(CultureInfo.InvariantCulture),
                    row.RunId,
                    row.SeedAlgorithm,
                    row.MasterSeed.ToString(CultureInfo.InvariantCulture),
                    row.PairId.ToString(CultureInfo.InvariantCulture),
                    row.PairedSeedMode ? "1" : "0",
                    row.InitialPopulationSeed.ToString(CultureInfo.InvariantCulture),
                    row.DiseaseProgressionSeed.ToString(CultureInfo.InvariantCulture),
                    row.TransmissionSeed.ToString(CultureInfo.InvariantCulture),
                    row.SymptomSeed.ToString(CultureInfo.InvariantCulture),
                    row.MortalitySeed.ToString(CultureInfo.InvariantCulture),
                    row.PandemicSeed.ToString(CultureInfo.InvariantCulture),
                    row.MaskSeed.ToString(CultureInfo.InvariantCulture),
                    row.TestSeed.ToString(CultureInfo.InvariantCulture),
                    row.ContactSeed.ToString(CultureInfo.InvariantCulture),
                    row.TestingSeed.ToString(CultureInfo.InvariantCulture),
                    row.ContactTracingSeed.ToString(CultureInfo.InvariantCulture),
                    row.InterventionSeed.ToString(CultureInfo.InvariantCulture),
                    row.ConfigurationHash,
                    row.FinalTrackedPopulation.ToString(CultureInfo.InvariantCulture),
                    row.FinalSusceptible.ToString(CultureInfo.InvariantCulture),
                    row.FinalExposed.ToString(CultureInfo.InvariantCulture),
                    row.FinalInfectious.ToString(CultureInfo.InvariantCulture),
                    row.FinalPostInfectiousIll.ToString(CultureInfo.InvariantCulture),
                    row.FinalSymptomatic.ToString(CultureInfo.InvariantCulture),
                    row.FinalSick.ToString(CultureInfo.InvariantCulture),
                    row.FinalRecovered.ToString(CultureInfo.InvariantCulture),
                    row.FinalDead.ToString(CultureInfo.InvariantCulture),
                    row.FinalTransmissionsTotal.ToString(CultureInfo.InvariantCulture),
                    row.InitialSeedCount.ToString(CultureInfo.InvariantCulture),
                    row.SecondaryTransmissionsTotal.ToString(CultureInfo.InvariantCulture),
                    row.CumulativeInfections.ToString(CultureInfo.InvariantCulture),
                    row.HospitalizationsTotal.ToString(CultureInfo.InvariantCulture),
                    row.FinalAttackRatePercent.ToString("R", CultureInfo.InvariantCulture),
                    row.FinalFatalityRatePercent.ToString("R", CultureInfo.InvariantCulture),
                    row.FinalPrevalencePercent.ToString("R", CultureInfo.InvariantCulture),
                    row.ResolvedCaseFatalityRatioPercent.HasValue ? row.ResolvedCaseFatalityRatioPercent.Value.ToString("R", CultureInfo.InvariantCulture) : string.Empty,
                    row.EmpiricalSecondaryInfectionsPerInfector.HasValue ? row.EmpiricalSecondaryInfectionsPerInfector.Value.ToString("R", CultureInfo.InvariantCulture) : string.Empty,
                    row.ActualMaskUsagePercent.ToString("R", CultureInfo.InvariantCulture),
                    row.TotalIsolationPersonDays.ToString("R", CultureInfo.InvariantCulture),
                    row.TotalQuarantinePersonDays.ToString("R", CultureInfo.InvariantCulture),
                    row.PhysicalContactsTotal.ToString(CultureInfo.InvariantCulture),
                    row.TraceableContactsTotal.ToString(CultureInfo.InvariantCulture),
                    row.ConfiguredDurationDays.ToString("R", CultureInfo.InvariantCulture),
                    row.EndMode,
                    row.EndReason,
                    row.StartedUtc,
                    row.TargetSimulationTimeUtc,
                    row.SimulationEndedUtc,
                    row.CompletedUtc,
                    row.ModVersion,
                    row.GameVersion,
                    row.GitCommitSha,
                    row.GitBranchOrTag,
                    row.OutputDirectory,
                });
            }

            return csv.ToString();
        }

        private static void AppendCsvRow(StringBuilder csv, string[] values)
        {
            for (int i = 0; i < values.Length; ++i)
            {
                if (i > 0)
                {
                    csv.Append(',');
                }

                csv.Append(EscapeCsv(values[i]));
            }

            csv.Append("\r\n");
        }

        private static string EscapeCsv(string value)
        {
            string text = value ?? string.Empty;
            if (text.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0)
            {
                return text;
            }

            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }

        private static void AtomicWriteText(string path, string contents)
        {
            string fullPath = Path.GetFullPath(path);
            string temporaryPath = AtomicFileUtilities.CreateTemporarySiblingPath(fullPath);
            string backupPath = fullPath + ".bak";
            try
            {
                using (FileStream stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    writer.Write(contents);
                    writer.Flush();
                    stream.Flush();
                }

                if (!File.Exists(fullPath))
                {
                    File.Move(temporaryPath, fullPath);
                    return;
                }

                DeleteFileIfPresent(backupPath);
                try
                {
                    File.Replace(temporaryPath, fullPath, backupPath, true);
                }
                catch (PlatformNotSupportedException)
                {
                    ReplaceWithRenameFallback(temporaryPath, fullPath, backupPath);
                }
                catch (NotSupportedException)
                {
                    ReplaceWithRenameFallback(temporaryPath, fullPath, backupPath);
                }
            }
            finally
            {
                DeleteFileIfPresent(temporaryPath);
            }
        }

        private static void ReplaceWithRenameFallback(string temporaryPath, string destinationPath, string backupPath)
        {
            File.Move(destinationPath, backupPath);
            try
            {
                File.Move(temporaryPath, destinationPath);
            }
            catch
            {
                if (!File.Exists(destinationPath) && File.Exists(backupPath))
                {
                    File.Move(backupPath, destinationPath);
                }

                throw;
            }
        }

        private static void DeleteFileIfPresent(string path)
        {
            if (File.Exists(path))
            {
                try
                {
                    File.Delete(path);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        private static int CompareFileEntries(ExperimentGeneratedFileEntry left, ExperimentGeneratedFileEntry right)
        {
            return string.Compare(left.RelativePath, right.RelativePath, StringComparison.Ordinal);
        }

        private static int CompareSummaryRows(ExperimentBatchRunSummaryRow left, ExperimentBatchRunSummaryRow right)
        {
            int scenario = left.ScenarioIndex.CompareTo(right.ScenarioIndex);
            if (scenario != 0)
            {
                return scenario;
            }

            int run = left.RunIndex.CompareTo(right.RunIndex);
            return run != 0 ? run : string.Compare(left.RunId, right.RunId, StringComparison.Ordinal);
        }

        private static bool IsTransientDirectory(string batchDirectory, string directory)
        {
            string relative = directory.Substring(batchDirectory.Length).Trim(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string[] parts = relative.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
            foreach (string part in parts)
            {
                string normalized = part.ToLowerInvariant();
                if (normalized.IndexOf("__inprogress") >= 0
                    || normalized.IndexOf(".__invalid_", StringComparison.Ordinal) >= 0
                    || normalized.EndsWith(".in-progress", StringComparison.Ordinal)
                    || normalized.EndsWith(".inprogress", StringComparison.Ordinal)
                    || normalized.EndsWith(".partial", StringComparison.Ordinal)
                    || normalized.EndsWith(".tmp", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool PathsEqual(string left, string right)
        {
            if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
            {
                return false;
            }

            return string.Equals(
                Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool MatchesExpectedRun(ExperimentRunManifest manifest, string expectedRunId)
        {
            return string.IsNullOrEmpty(expectedRunId)
                || string.Equals(manifest.RunId, expectedRunId, StringComparison.Ordinal);
        }

        private static ExperimentRunCommitResult FromRecoveredSummary(
            ExperimentBatchSummaryResult summary,
            string destination,
            ExperimentRunManifest manifest,
            bool alreadyPublished)
        {
            if (!summary.Success)
            {
                return new ExperimentRunCommitResult
                {
                    Error = "The run is published, but the batch summary could not be rebuilt: " + summary.Error,
                    Published = true,
                    AlreadyPublished = alreadyPublished,
                    PublishedDirectory = destination,
                    Manifest = manifest,
                };
            }

            return new ExperimentRunCommitResult
            {
                Success = true,
                Published = true,
                AlreadyPublished = alreadyPublished,
                PublishedDirectory = destination,
                SummaryPath = summary.SummaryPath,
                Manifest = manifest,
            };
        }

        private static ExperimentRunCommitResult Failure(string error)
        {
            return new ExperimentRunCommitResult { Error = error };
        }

        private static ExperimentRunValidationResult ValidationFailure(string error)
        {
            return new ExperimentRunValidationResult { Error = error };
        }
    }
}
