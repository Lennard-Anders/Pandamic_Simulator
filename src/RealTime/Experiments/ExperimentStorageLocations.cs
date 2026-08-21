// <copyright file="ExperimentStorageLocations.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.Experiments
{
    using System;
    using System.IO;

    /// <summary>Supported ownership locations for batch artifacts.</summary>
    public enum ExperimentOutputRootKind
    {
        ModPandemicData,
        ModDirectory = ModPandemicData,
        UserPandemicData,
        UserData = UserPandemicData,
    }

    /// <summary>Serializable output-root choice.</summary>
    public sealed class ExperimentOutputRootSelection
    {
        public ExperimentOutputRootSelection()
        {
            SchemaVersion = ExperimentSchema.CurrentVersion;
            Kind = ExperimentOutputRootKind.ModPandemicData;
        }

        public int SchemaVersion { get; set; }

        public ExperimentOutputRootKind Kind { get; set; }

    }

    /// <summary>Result of checking that an output root can be created and written.</summary>
    public sealed class ExperimentOutputPreflightResult
    {
        public bool Success { get; internal set; }

        public string ResolvedPath { get; internal set; }

        public string Error { get; internal set; }
    }

    /// <summary>Resolves user-data, mod-local, or explicit experiment output roots.</summary>
    public sealed class ExperimentOutputRootResolver
    {
        private readonly string userDataDirectory;
        private readonly string modDirectory;

        public ExperimentOutputRootResolver(string userDataDirectory, string modDirectory)
        {
            this.userDataDirectory = userDataDirectory;
            this.modDirectory = modDirectory;
        }

        public string Resolve(ExperimentOutputRootSelection selection)
        {
            if (selection == null)
            {
                throw new ArgumentNullException("selection");
            }

            if (selection.SchemaVersion != ExperimentSchema.CurrentVersion)
            {
                throw new InvalidOperationException("The output-root selection schema is unsupported.");
            }

            string result;
            switch (selection.Kind)
            {
                case ExperimentOutputRootKind.UserPandemicData:
                    result = RequireRoot(userDataDirectory, "user data directory");
                    result = Path.Combine(Path.Combine(result, "Pandemic Data"), "Experiments");
                    break;
                case ExperimentOutputRootKind.ModPandemicData:
                    result = RequireRoot(modDirectory, "mod directory");
                    result = Path.Combine(Path.Combine(result, "Pandemic Data"), "Experiments");
                    break;
                default:
                    throw new ArgumentOutOfRangeException("selection", "Unknown experiment output-root kind.");
            }

            return Path.GetFullPath(result);
        }

        public ExperimentOutputPreflightResult Preflight(ExperimentOutputRootSelection selection)
        {
            string root;
            try
            {
                root = Resolve(selection);
                Directory.CreateDirectory(root);
                string probe = Path.Combine(root, ".write-test-" + Guid.NewGuid().ToString("N"));
                try
                {
                    using (FileStream stream = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        stream.WriteByte(0x2a);
                        stream.Flush();
                    }
                }
                finally
                {
                    if (File.Exists(probe))
                    {
                        File.Delete(probe);
                    }
                }

                return new ExperimentOutputPreflightResult { Success = true, ResolvedPath = root };
            }
            catch (Exception exception)
            {
                return new ExperimentOutputPreflightResult { Error = exception.Message };
            }
        }

        private static string RequireRoot(string value, string name)
        {
            if (string.IsNullOrEmpty(value) || value.Trim().Length == 0)
            {
                throw new InvalidOperationException("The " + name + " is unavailable.");
            }

            return value;
        }
    }

    /// <summary>Durable pointer to the one active batch, independent of level lifetime.</summary>
    public sealed class ActiveExperimentBatchLocator
    {
        public ActiveExperimentBatchLocator()
        {
            SchemaVersion = ExperimentSchema.CurrentVersion;
        }

        public int SchemaVersion { get; set; }

        public string BatchId { get; set; }

        public string BatchDirectory { get; set; }

        public string UpdatedUtc { get; set; }

        public bool IsActive
        {
            get { return !string.IsNullOrEmpty(BatchId) && !string.IsNullOrEmpty(BatchDirectory); }
        }
    }

    /// <summary>Atomically stores and loads the active batch pointer.</summary>
    public sealed class ExperimentActiveBatchLocatorService
    {
        public const string LocatorFileName = "active_batch.json";

        private readonly string locatorPath;
        private readonly IAtomicJsonFileStore store;

        public ExperimentActiveBatchLocatorService(string locatorDirectory, IAtomicJsonFileStore store)
        {
            if (string.IsNullOrEmpty(locatorDirectory))
            {
                throw new ArgumentException("A locator directory is required.", "locatorDirectory");
            }

            if (store == null)
            {
                throw new ArgumentNullException("store");
            }

            locatorPath = Path.Combine(Path.GetFullPath(locatorDirectory), LocatorFileName);
            this.store = store;
        }

        public void SetActive(string batchId, string batchDirectory, string utcNow)
        {
            if (string.IsNullOrEmpty(batchId) || string.IsNullOrEmpty(batchDirectory))
            {
                throw new ArgumentException("An active batch ID and directory are required.");
            }

            store.Save(locatorPath, new ActiveExperimentBatchLocator
            {
                BatchId = batchId,
                BatchDirectory = Path.GetFullPath(batchDirectory),
                UpdatedUtc = utcNow,
            });
        }

        public void Clear(string utcNow)
        {
            store.Save(locatorPath, new ActiveExperimentBatchLocator { UpdatedUtc = utcNow });
        }

        public JsonLoadResult<ActiveExperimentBatchLocator> Load()
        {
            JsonLoadResult<ActiveExperimentBatchLocator> result = store.TryLoad<ActiveExperimentBatchLocator>(locatorPath);
            return result.Success && result.Value.SchemaVersion != ExperimentSchema.CurrentVersion
                ? JsonLoadResult<ActiveExperimentBatchLocator>.Failed("The active batch locator schema version is unsupported.")
                : result;
        }
    }
}
