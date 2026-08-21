// <copyright file="ExperimentPathResolver.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.Experiments
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;

    /// <summary>Sanitizes untrusted labels before they become path segments.</summary>
    public static class ExperimentPathSanitizer
    {
        private const string ExplicitlyInvalid = "<>:\"/\\|?*";
        private static readonly Dictionary<string, bool> ReservedNames = CreateReservedNames();

        public static string SanitizeSegment(string value, string fallback, int maximumLength)
        {
            if (maximumLength <= 0)
            {
                throw new ArgumentOutOfRangeException("maximumLength");
            }

            string source = string.IsNullOrEmpty(value) ? fallback : value;
            if (string.IsNullOrEmpty(source))
            {
                source = "unnamed";
            }

            char[] platformInvalid = Path.GetInvalidFileNameChars();
            StringBuilder result = new StringBuilder(source.Length);
            bool previousWasReplacement = false;
            foreach (char character in source)
            {
                bool invalid = char.IsControl(character)
                    || ExplicitlyInvalid.IndexOf(character) >= 0
                    || Array.IndexOf(platformInvalid, character) >= 0;
                char output = invalid ? '_' : character;
                if (output == '_' && previousWasReplacement)
                {
                    continue;
                }

                result.Append(output);
                previousWasReplacement = output == '_';
            }

            string sanitized = result.ToString().Trim().Trim('.', ' ', '_');
            if (sanitized.Length == 0 || sanitized == "." || sanitized == "..")
            {
                sanitized = "unnamed";
            }

            string stem = sanitized;
            int dotIndex = stem.IndexOf('.');
            if (dotIndex >= 0)
            {
                stem = stem.Substring(0, dotIndex);
            }

            if (ReservedNames.ContainsKey(stem))
            {
                sanitized = "_" + sanitized;
            }

            if (sanitized.Length > maximumLength)
            {
                sanitized = sanitized.Substring(0, maximumLength).TrimEnd('.', ' ');
            }

            return sanitized.Length == 0 ? "unnamed" : sanitized;
        }

        public static string SanitizeSegment(string value, string fallback)
        {
            return SanitizeSegment(value, fallback, 64);
        }

        private static Dictionary<string, bool> CreateReservedNames()
        {
            Dictionary<string, bool> names = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            string[] fixedNames = { "CON", "PRN", "AUX", "NUL" };
            foreach (string name in fixedNames)
            {
                names.Add(name, true);
            }

            for (int i = 1; i <= 9; ++i)
            {
                names.Add("COM" + i.ToString(CultureInfo.InvariantCulture), true);
                names.Add("LPT" + i.ToString(CultureInfo.InvariantCulture), true);
            }

            return names;
        }
    }

    /// <summary>Computes contained, deterministic output paths without touching the file system.</summary>
    public sealed class ExperimentPathResolver
    {
        private const int ConservativeFullPathLimit = 259;
        private const int MaximumRichCsvLeafWithBackup = 57;
        private const int MaximumAttemptIdentifierLength = 16;
        private const int ScenarioNestedPathReservation = 97;
        private const int BatchNestedPathReservation = 112;
        private readonly string outputRoot;

        public ExperimentPathResolver(string outputRoot)
        {
            if (string.IsNullOrEmpty(outputRoot))
            {
                throw new ArgumentException("An output root is required.", "outputRoot");
            }

            this.outputRoot = Path.GetFullPath(outputRoot);
        }

        public string OutputRoot
        {
            get { return outputRoot; }
        }

        public string ResolveBatchDirectory(ExperimentBatchPlan plan)
        {
            if (plan == null)
            {
                throw new ArgumentNullException("plan");
            }

            string id = ShortIdentifier(plan.BatchId);
            DateTime created;
            string timestamp = DateTime.TryParse(
                plan.CreatedUtc,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out created)
                ? created.ToUniversalTime().ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture)
                : "unknown_time";
            string suffix = "_" + timestamp + "_" + id;
            string folderName = string.IsNullOrEmpty(plan.OutputFolderName)
                ? plan.BatchName
                : plan.OutputFolderName;
            string segment = FitNamedSegment(
                outputRoot,
                string.Empty,
                folderName,
                "batch",
                suffix,
                60,
                BatchNestedPathReservation);
            return CombineContained(outputRoot, segment);
        }

        public string ResolveScenarioDirectory(ExperimentBatchPlan plan, int scenarioIndex)
        {
            if (plan == null || plan.Scenarios == null || scenarioIndex < 0 || scenarioIndex >= plan.Scenarios.Count)
            {
                throw new ArgumentOutOfRangeException("scenarioIndex");
            }

            ExperimentScenario scenario = plan.Scenarios[scenarioIndex];
            string id = ShortIdentifier(scenario.ScenarioId);
            string batch = ResolveBatchDirectory(plan);
            string prefix = string.Format(CultureInfo.InvariantCulture, "{0:D3}_", scenarioIndex + 1);
            string segment = FitNamedSegment(
                batch,
                prefix,
                scenario.Name,
                "scenario",
                "_" + id,
                52,
                ScenarioNestedPathReservation);
            return CombineContained(batch, segment);
        }

        public string ResolveRunDirectory(ExperimentBatchPlan plan, ExperimentRunDescriptor run)
        {
            if (run == null)
            {
                throw new ArgumentNullException("run");
            }

            string segment = string.Format(
                CultureInfo.InvariantCulture,
                "run_{0:D3}",
                run.RunIndex + 1);
            return CombineContained(ResolveScenarioDirectory(plan, run.ScenarioIndex), segment);
        }

        public string ResolveInProgressRunDirectory(
            ExperimentBatchPlan plan,
            ExperimentRunDescriptor run,
            string attemptId)
        {
            if (string.IsNullOrEmpty(attemptId))
            {
                throw new ArgumentException("A unique run attempt ID is required.", "attemptId");
            }

            string scenarioDirectory = ResolveScenarioDirectory(plan, run.ScenarioIndex);
            string prefix = string.Format(
                CultureInfo.InvariantCulture,
                "run_{0:D3}.__inprogress_",
                run.RunIndex + 1);
            int available = ConservativeFullPathLimit
                - MaximumRichCsvLeafWithBackup
                - scenarioDirectory.Length
                - 1
                - prefix.Length;
            if (available < 8)
            {
                throw new PathTooLongException("The experiment output root leaves no room for a unique attempt directory.");
            }

            string safeAttempt = ExperimentPathSanitizer.SanitizeSegment(
                attemptId,
                null,
                Math.Min(MaximumAttemptIdentifierLength, available));
            string segment = prefix + safeAttempt;
            return CombineContained(scenarioDirectory, segment);
        }

        public string ResolveAvailableFinalRunDirectory(ExperimentBatchPlan plan, ExperimentRunDescriptor run)
        {
            string path = ResolveRunDirectory(plan, run);
            if (Directory.Exists(path) || File.Exists(path))
            {
                throw new IOException("The final run directory already exists and will not be overwritten: " + path);
            }

            return path;
        }

        public static bool IsContained(string root, string candidate)
        {
            if (string.IsNullOrEmpty(root) || string.IsNullOrEmpty(candidate))
            {
                return false;
            }

            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            string fullCandidate = Path.GetFullPath(candidate);
            return fullCandidate.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
        }

        private static string CombineContained(string root, string segment)
        {
            string combined = Path.GetFullPath(Path.Combine(root, segment));
            if (!IsContained(root, combined))
            {
                throw new InvalidOperationException("The resolved experiment path escaped its output root.");
            }

            if (combined.Length > ConservativeFullPathLimit)
            {
                throw new PathTooLongException("The resolved experiment path exceeds the conservative Windows path limit: " + combined);
            }

            return combined;
        }

        private static string FitNamedSegment(
            string root,
            string prefix,
            string displayName,
            string fallback,
            string suffix,
            int preferredNameLength,
            int reservedNestedLength)
        {
            int available = ConservativeFullPathLimit
                - Path.GetFullPath(root).Length
                - 1
                - prefix.Length
                - suffix.Length
                - reservedNestedLength;
            if (available < 1)
            {
                throw new PathTooLongException("The experiment output root is too long for unique output names.");
            }

            string name = ExperimentPathSanitizer.SanitizeSegment(
                displayName,
                fallback,
                Math.Min(preferredNameLength, available));
            return prefix + name + suffix;
        }

        private static string ShortIdentifier(string value)
        {
            string sanitized = ExperimentPathSanitizer.SanitizeSegment(value, "unknown", 40).Replace("-", string.Empty);
            return sanitized.Length <= 8 ? sanitized : sanitized.Substring(0, 8);
        }
    }
}
