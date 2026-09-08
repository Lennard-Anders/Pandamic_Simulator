// <copyright file="ExperimentInvalidRunService.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.Experiments
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Security.Cryptography;

    /// <summary>Durable metadata for a scientifically unusable run, never included in batch summaries.</summary>
    public sealed class ExperimentInvalidRunManifest
    {
        public ExperimentInvalidRunManifest()
        {
            SchemaVersion = ExperimentSchema.CurrentVersion;
            Status = "Invalid";
            OutputFiles = new List<ExperimentGeneratedFileEntry>();
        }

        public int SchemaVersion { get; set; }

        public string Status { get; set; }

        public string BatchId { get; set; }

        public string BatchName { get; set; }

        public string RunId { get; set; }

        public string AttemptId { get; set; }

        public int ScenarioIndex { get; set; }

        public int ScenarioNumber { get; set; }

        public string ScenarioId { get; set; }

        public string ScenarioName { get; set; }

        public int RunIndex { get; set; }

        public int RunNumber { get; set; }

        public int PairId { get; set; }

        public int MasterSeed { get; set; }

        public ExperimentSeedSet DerivedSeeds { get; set; }

        public ExperimentScenario Scenario { get; set; }

        public BaselineSaveIdentity Baseline { get; set; }

        public string ConfigurationHashAlgorithm { get; set; }

        public string ConfigurationHash { get; set; }

        public string GitCommitSha { get; set; }

        public string GitBranchOrTag { get; set; }

        public string ModVersion { get; set; }

        public string GameVersion { get; set; }

        public string RunSimulationStartedUtc { get; set; }

        public string RunSimulationEndedUtc { get; set; }

        public string InvalidatedUtc { get; set; }

        public string FailureSimulationTimeUtc { get; set; }

        public ExperimentErrorInfo Error { get; set; }

        public List<ExperimentGeneratedFileEntry> OutputFiles { get; set; }
    }

    internal sealed class ExperimentInvalidRunArchiveRequest
    {
        public string AttemptDirectory { get; set; }

        public string InvalidDirectory { get; set; }

        public ExperimentInvalidRunManifest Manifest { get; set; }
    }

    internal sealed class ExperimentInvalidRunArchiveResult
    {
        public bool Success { get; set; }

        public string Error { get; set; }

        public string PublishedDirectory { get; set; }
    }

    /// <summary>Atomically archives failed-run diagnostics without touching successful summaries.</summary>
    internal sealed class ExperimentInvalidRunService
    {
        public const string ManifestFileName = "run_manifest.json";

        private readonly AtomicJsonFileStore jsonStore;

        public ExperimentInvalidRunService(AtomicJsonFileStore jsonStore)
        {
            this.jsonStore = jsonStore ?? throw new ArgumentNullException(nameof(jsonStore));
        }

        public ExperimentInvalidRunArchiveResult Archive(ExperimentInvalidRunArchiveRequest request)
        {
            try
            {
                Validate(request);
                string attempt = Path.GetFullPath(request.AttemptDirectory);
                string destination = Path.GetFullPath(request.InvalidDirectory);
                request.Manifest.OutputFiles.Clear();
                string[] archiveFiles = Directory.GetFiles(attempt, "*", SearchOption.AllDirectories);
                Array.Sort(archiveFiles, StringComparer.Ordinal);
                foreach (string path in archiveFiles)
                {
                    string name = Path.GetFileName(path);
                    if (string.Equals(name, ManifestFileName, StringComparison.OrdinalIgnoreCase)
                        || name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)
                        || name.StartsWith(".tmp-", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var entry = Fingerprint(path);
                    entry.RelativePath = path.Substring(attempt.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length + 1)
                        .Replace(Path.DirectorySeparatorChar, '/');
                    request.Manifest.OutputFiles.Add(entry);
                }

                jsonStore.Save(Path.Combine(attempt, ManifestFileName), request.Manifest);
                Directory.Move(attempt, destination);
                return new ExperimentInvalidRunArchiveResult
                {
                    Success = true,
                    PublishedDirectory = destination,
                };
            }
            catch (Exception exception)
            {
                return new ExperimentInvalidRunArchiveResult
                {
                    Success = false,
                    Error = exception.Message,
                };
            }
        }

        public static string ResolveInvalidDirectory(string normalFinalDirectory, string attemptId)
        {
            if (string.IsNullOrEmpty(normalFinalDirectory)
                || normalFinalDirectory.Trim().Length == 0
                || string.IsNullOrEmpty(attemptId)
                || attemptId.Trim().Length == 0)
            {
                throw new ArgumentException("A normal run path and attempt ID are required.");
            }

            string fullPath = Path.GetFullPath(normalFinalDirectory);
            string parent = Path.GetDirectoryName(fullPath);
            string name = Path.GetFileName(fullPath);
            string suffix = attemptId.Length > 12 ? attemptId.Substring(0, 12) : attemptId;
            return Path.Combine(parent, name + ".__invalid_" + suffix);
        }

        private static void Validate(ExperimentInvalidRunArchiveRequest request)
        {
            if (request == null || request.Manifest == null)
            {
                throw new ArgumentException("Invalid-run metadata is required.", nameof(request));
            }

            string attempt = Path.GetFullPath(request.AttemptDirectory);
            string destination = Path.GetFullPath(request.InvalidDirectory);
            if (!Directory.Exists(attempt))
            {
                throw new DirectoryNotFoundException("The failed run attempt directory is missing: " + attempt);
            }

            if (Directory.Exists(destination) || File.Exists(destination))
            {
                throw new IOException("Invalid-run diagnostics are never overwritten: " + destination);
            }

            if (Directory.GetFiles(attempt, "*.tmp", SearchOption.AllDirectories).Length > 0
                || Directory.GetFiles(attempt, ".tmp-*", SearchOption.AllDirectories).Length > 0)
            {
                throw new IOException("The failed run still contains unpublished temporary outputs.");
            }

            if (!string.Equals(request.Manifest.Status, "Invalid", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("A failed-run diagnostic manifest must have status Invalid.");
            }
        }

        private static ExperimentGeneratedFileEntry Fingerprint(string path)
        {
            var info = new FileInfo(path);
            string hash;
            using (SHA256 sha256 = SHA256.Create())
            using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                hash = BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
            }

            return new ExperimentGeneratedFileEntry
            {
                Role = "invalid-run-diagnostic",
                RelativePath = info.Name,
                LengthBytes = info.Length,
                Sha256 = hash,
            };
        }
    }
}
