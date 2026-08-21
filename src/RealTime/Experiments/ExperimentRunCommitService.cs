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

        public int PandemicSeed { get; set; }

        public int MaskSeed { get; set; }

        public int TestSeed { get; set; }

        public int ContactSeed { get; set; }

        public int FinalTrackedPopulation { get; set; }

        public int FinalExposed { get; set; }

        public int FinalSick { get; set; }

        public int FinalRecovered { get; set; }

        public int FinalDead { get; set; }

        public int FinalTransmissionsTotal { get; set; }

        public double FinalAttackRatePercent { get; set; }

        public double FinalFatalityRatePercent { get; set; }

        public double ConfiguredDurationDays { get; set; }

        public string EndMode { get; set; }

        public string EndReason { get; set; }

        public string StartedUtc { get; set; }

        public string TargetSimulationTimeUtc { get; set; }

        public string SimulationEndedUtc { get; set; }

        public string CompletedUtc { get; set; }

        public string ModVersion { get; set; }

        public string GameVersion { get; set; }

        public string OutputDirectory { get; set; }

        public string OutputRootKind { get; set; }

        public string OutputRoot { get; set; }

        public List<ExperimentGeneratedFileEntry> OutputFiles { get; set; }
    }

    /// <summary>Cryptographic identity of one generated run file.</summary>
    public sealed class ExperimentGeneratedFileEntry
    {
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

        public int PandemicSeed { get; set; }

        public int MaskSeed { get; set; }

        public int TestSeed { get; set; }

        public int ContactSeed { get; set; }

        public int FinalTrackedPopulation { get; set; }

        public int FinalExposed { get; set; }

        public int FinalSick { get; set; }

        public int FinalRecovered { get; set; }

        public int FinalDead { get; set; }

        public int FinalTransmissionsTotal { get; set; }

        public double FinalAttackRatePercent { get; set; }

        public double FinalFatalityRatePercent { get; set; }

        public double ConfiguredDurationDays { get; set; }

        public string EndMode { get; set; }

        public string EndReason { get; set; }

        public string StartedUtc { get; set; }

        public string TargetSimulationTimeUtc { get; set; }

        public string SimulationEndedUtc { get; set; }

        public string CompletedUtc { get; set; }

        public string ModVersion { get; set; }

        public string GameVersion { get; set; }

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
        public const string SeedAlgorithmName = "TENUS-RNG-v1/FNV-1a-32";

        private const string PandemicRunPrefix = "pandemic_run_";
        private const string CsvSuffix = ".csv";
        private readonly IAtomicJsonFileStore jsonStore;

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
                PandemicSeed = run.Seeds.Pandemic,
                MaskSeed = run.Seeds.Mask,
                TestSeed = run.Seeds.Test,
                ContactSeed = run.Seeds.Contact,
                ConfiguredDurationDays = run.Scenario.DurationDays,
                EndMode = run.Scenario.EndMode.ToString(),
                EndReason = endReason,
                StartedUtc = startedUtc,
                TargetSimulationTimeUtc = targetSimulationTimeUtc,
                SimulationEndedUtc = simulationEndedUtc,
                CompletedUtc = completedUtc,
                ModVersion = plan.ModVersion,
                GameVersion = plan.GameVersion,
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
            if (!TryFingerprintMandatoryFiles(attemptDirectory, out files, out error))
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
            out List<ExperimentGeneratedFileEntry> entries,
            out string error)
        {
            entries = new List<ExperimentGeneratedFileEntry>();
            try
            {
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

                string dataPath = Path.Combine(directory, DataFileName);
                string contactsPath = Path.Combine(directory, ContactsFileName);
                if (!File.Exists(dataPath) || !File.Exists(contactsPath))
                {
                    error = "Both mandatory data.csv and contacts.csv exports are required.";
                    return false;
                }

                entries.Add(CreateFileEntry("PandemicRun", directory, pandemicFiles[0]));
                entries.Add(CreateFileEntry("ObserverData", directory, dataPath));
                entries.Add(CreateFileEntry("Contacts", directory, contactsPath));
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
                RelativePath = Path.GetFileName(fullPath),
                LengthBytes = info.Length,
                Sha256 = hash,
            };
        }

        private static bool ValidateManifestFields(ExperimentRunManifest manifest, out string error)
        {
            if (manifest == null)
            {
                error = "The run manifest is missing.";
                return false;
            }

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
                || manifest.FinalExposed < 0
                || manifest.FinalSick < 0
                || manifest.FinalRecovered < 0
                || manifest.FinalDead < 0
                || manifest.FinalTransmissionsTotal < 0
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

            if (manifest.ScenarioIndex < 0
                || manifest.RunIndex < 0
                || manifest.ScenarioNumber != manifest.ScenarioIndex + 1
                || manifest.RunNumber != manifest.RunIndex + 1)
            {
                error = "The run manifest contains inconsistent zero-based and one-based indices.";
                return false;
            }

            if (manifest.MasterSeed < 0
                || manifest.PandemicSeed < 0
                || manifest.MaskSeed < 0
                || manifest.TestSeed < 0
                || manifest.ContactSeed < 0)
            {
                error = "The run manifest contains a negative deterministic seed.";
                return false;
            }

            ExperimentSeedSet expectedSeeds = new FnvExperimentSeedProvider().DeriveSeeds(manifest.MasterSeed);
            if (manifest.PandemicSeed != expectedSeeds.Pandemic
                || manifest.MaskSeed != expectedSeeds.Mask
                || manifest.TestSeed != expectedSeeds.Test
                || manifest.ContactSeed != expectedSeeds.Contact)
            {
                error = "The component seeds do not match the deterministic master-seed derivation.";
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
            int pandemicCount = 0;
            bool hasData = false;
            bool hasContacts = false;
            Dictionary<string, bool> paths = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (ExperimentGeneratedFileEntry entry in manifest.OutputFiles)
            {
                if (entry == null
                    || string.IsNullOrEmpty(entry.RelativePath)
                    || string.IsNullOrEmpty(entry.Sha256)
                    || entry.LengthBytes < 0L
                    || Path.IsPathRooted(entry.RelativePath)
                    || !string.Equals(entry.RelativePath, Path.GetFileName(entry.RelativePath), StringComparison.Ordinal))
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
                else if (string.Equals(name, DataFileName, StringComparison.OrdinalIgnoreCase))
                {
                    hasData = true;
                }
                else if (string.Equals(name, ContactsFileName, StringComparison.OrdinalIgnoreCase))
                {
                    hasContacts = true;
                }
            }

            if (pandemicCount != 1 || !hasData || !hasContacts)
            {
                error = "The run manifest does not identify all three mandatory CSV exports.";
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

            if (physicalPandemicCount != 1
                || !File.Exists(Path.Combine(directory, DataFileName))
                || !File.Exists(Path.Combine(directory, ContactsFileName)))
            {
                error = "The run directory does not contain exactly the required mandatory CSV exports.";
                return false;
            }

            error = null;
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
                PandemicSeed = manifest.PandemicSeed,
                MaskSeed = manifest.MaskSeed,
                TestSeed = manifest.TestSeed,
                ContactSeed = manifest.ContactSeed,
                FinalTrackedPopulation = manifest.FinalTrackedPopulation,
                FinalExposed = manifest.FinalExposed,
                FinalSick = manifest.FinalSick,
                FinalRecovered = manifest.FinalRecovered,
                FinalDead = manifest.FinalDead,
                FinalTransmissionsTotal = manifest.FinalTransmissionsTotal,
                FinalAttackRatePercent = manifest.FinalAttackRatePercent,
                FinalFatalityRatePercent = manifest.FinalFatalityRatePercent,
                ConfiguredDurationDays = manifest.ConfiguredDurationDays,
                EndMode = manifest.EndMode,
                EndReason = manifest.EndReason,
                StartedUtc = manifest.StartedUtc,
                TargetSimulationTimeUtc = manifest.TargetSimulationTimeUtc,
                SimulationEndedUtc = manifest.SimulationEndedUtc,
                CompletedUtc = manifest.CompletedUtc,
                ModVersion = manifest.ModVersion,
                GameVersion = manifest.GameVersion,
                OutputDirectory = manifest.OutputDirectory,
            };
        }

        private static string BuildSummaryCsv(IList<ExperimentBatchRunSummaryRow> rows)
        {
            StringBuilder csv = new StringBuilder();
            csv.Append("SchemaVersion,BatchId,BatchName,BaselineAssetFullName,BaselineAssetChecksum,");
            csv.Append("ScenarioIndex,ScenarioNumber,ScenarioId,ScenarioName,RunIndex,RunNumber,RunId,");
            csv.Append("SeedAlgorithm,MasterSeed,PandemicSeed,MaskSeed,TestSeed,ContactSeed,");
            csv.Append("FinalTrackedPopulation,FinalExposed,FinalSick,FinalRecovered,FinalDead,");
            csv.Append("FinalTransmissionsTotal,FinalAttackRatePercent,FinalFatalityRatePercent,ConfiguredDurationDays,");
            csv.Append("EndMode,EndReason,StartedUtc,TargetSimulationTimeUtc,SimulationEndedUtc,CompletedUtc,");
            csv.Append("ModVersion,GameVersion,OutputDirectory\r\n");
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
                    row.PandemicSeed.ToString(CultureInfo.InvariantCulture),
                    row.MaskSeed.ToString(CultureInfo.InvariantCulture),
                    row.TestSeed.ToString(CultureInfo.InvariantCulture),
                    row.ContactSeed.ToString(CultureInfo.InvariantCulture),
                    row.FinalTrackedPopulation.ToString(CultureInfo.InvariantCulture),
                    row.FinalExposed.ToString(CultureInfo.InvariantCulture),
                    row.FinalSick.ToString(CultureInfo.InvariantCulture),
                    row.FinalRecovered.ToString(CultureInfo.InvariantCulture),
                    row.FinalDead.ToString(CultureInfo.InvariantCulture),
                    row.FinalTransmissionsTotal.ToString(CultureInfo.InvariantCulture),
                    row.FinalAttackRatePercent.ToString("R", CultureInfo.InvariantCulture),
                    row.FinalFatalityRatePercent.ToString("R", CultureInfo.InvariantCulture),
                    row.ConfiguredDurationDays.ToString("R", CultureInfo.InvariantCulture),
                    row.EndMode,
                    row.EndReason,
                    row.StartedUtc,
                    row.TargetSimulationTimeUtc,
                    row.SimulationEndedUtc,
                    row.CompletedUtc,
                    row.ModVersion,
                    row.GameVersion,
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
