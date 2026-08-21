// <copyright file="AtomicJsonFileStore.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.Experiments
{
    using System;
    using System.IO;
    using System.Text;

    /// <summary>Abstraction over durable JSON storage.</summary>
    public interface IAtomicJsonFileStore
    {
        void Save<T>(string path, T value);

        JsonLoadResult<T> TryLoad<T>(string path);
    }

    /// <summary>Non-throwing result of loading a JSON artifact.</summary>
    public sealed class JsonLoadResult<T>
    {
        public bool Success { get; private set; }

        public bool FileExists { get; private set; }

        public T Value { get; private set; }

        public string Error { get; private set; }

        public bool RecoveredFromBackup { get; private set; }

        internal static JsonLoadResult<T> Missing()
        {
            return new JsonLoadResult<T> { FileExists = false, Error = "The JSON file does not exist." };
        }

        internal static JsonLoadResult<T> Loaded(T value, bool recoveredFromBackup)
        {
            return new JsonLoadResult<T>
            {
                Success = true,
                FileExists = true,
                Value = value,
                RecoveredFromBackup = recoveredFromBackup,
            };
        }

        internal static JsonLoadResult<T> Failed(string error)
        {
            return new JsonLoadResult<T> { FileExists = true, Error = error };
        }
    }

    /// <summary>Writes sibling temporary files and atomically replaces committed JSON artifacts.</summary>
    public sealed class AtomicJsonFileStore : IAtomicJsonFileStore
    {
        private readonly ExperimentJsonSerializer serializer;

        public AtomicJsonFileStore()
        {
            serializer = new ExperimentJsonSerializer
            {
                MaxJsonLength = int.MaxValue,
                RecursionLimit = 128,
            };
        }

        public void Save<T>(string path, T value)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("A JSON path is required.", "path");
            }

            string fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(directory))
            {
                throw new ArgumentException("The JSON path must have a parent directory.", "path");
            }

            Directory.CreateDirectory(directory);
            string temporaryPath = AtomicFileUtilities.CreateTemporarySiblingPath(fullPath);
            string backupPath = fullPath + ".bak";

            try
            {
                string json = serializer.Serialize(value);
                using (FileStream stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    writer.Write(json);
                    writer.Flush();
                    stream.Flush();
                }

                if (File.Exists(fullPath))
                {
                    Replace(temporaryPath, fullPath, backupPath);
                }
                else
                {
                    File.Move(temporaryPath, fullPath);
                }
            }
            finally
            {
                DeleteIfPresent(temporaryPath);
            }
        }

        public JsonLoadResult<T> TryLoad<T>(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return JsonLoadResult<T>.Failed("A JSON path is required.");
            }

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(path);
            }
            catch (Exception exception)
            {
                return JsonLoadResult<T>.Failed(exception.Message);
            }

            string backupPath = fullPath + ".bak";
            if (!File.Exists(fullPath) && !File.Exists(backupPath))
            {
                return JsonLoadResult<T>.Missing();
            }

            JsonLoadResult<T> primary = Read<T>(fullPath, false);
            if (primary.Success)
            {
                return primary;
            }

            JsonLoadResult<T> backup = Read<T>(backupPath, true);
            if (backup.Success)
            {
                return backup;
            }

            return JsonLoadResult<T>.Failed("Primary JSON failed: " + primary.Error + " Backup JSON failed: " + backup.Error);
        }

        private static void Replace(string temporaryPath, string destinationPath, string backupPath)
        {
            DeleteIfPresent(backupPath);
            try
            {
                File.Replace(temporaryPath, destinationPath, backupPath, true);
            }
            catch (PlatformNotSupportedException)
            {
                ReplaceWithRenameFallback(temporaryPath, destinationPath, backupPath);
            }
            catch (NotSupportedException)
            {
                ReplaceWithRenameFallback(temporaryPath, destinationPath, backupPath);
            }
        }

        private JsonLoadResult<T> Read<T>(string path, bool recoveredFromBackup)
        {
            if (!File.Exists(path))
            {
                return JsonLoadResult<T>.Failed("The JSON file does not exist.");
            }

            try
            {
                string json;
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, true))
                {
                    json = reader.ReadToEnd();
                }

                T value = serializer.Deserialize<T>(json);
                if (object.Equals(value, default(T)))
                {
                    return JsonLoadResult<T>.Failed("The JSON artifact contains no value.");
                }

                return JsonLoadResult<T>.Loaded(value, recoveredFromBackup);
            }
            catch (Exception exception)
            {
                return JsonLoadResult<T>.Failed(exception.Message);
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

        private static void DeleteIfPresent(string path)
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
    }

    /// <summary>Persists the canonical plan and durable progress files for one batch directory.</summary>
    public sealed class ExperimentPersistenceService
    {
        public const string PlanFileName = "batch_manifest.json";
        public const string StateFileName = "batch_state.json";

        private readonly IAtomicJsonFileStore store;

        public ExperimentPersistenceService(IAtomicJsonFileStore store)
        {
            if (store == null)
            {
                throw new ArgumentNullException("store");
            }

            this.store = store;
        }

        public void SavePlan(string batchDirectory, ExperimentBatchPlan plan)
        {
            store.Save(Path.Combine(batchDirectory, PlanFileName), plan);
        }

        public void SaveState(string batchDirectory, ExperimentBatchState state)
        {
            store.Save(Path.Combine(batchDirectory, StateFileName), state);
        }

        public JsonLoadResult<ExperimentBatchPlan> LoadPlan(string batchDirectory)
        {
            JsonLoadResult<ExperimentBatchPlan> result = store.TryLoad<ExperimentBatchPlan>(Path.Combine(batchDirectory, PlanFileName));
            return result.Success && result.Value.SchemaVersion != ExperimentSchema.CurrentVersion
                ? JsonLoadResult<ExperimentBatchPlan>.Failed("The batch plan schema version is unsupported.")
                : result;
        }

        public JsonLoadResult<ExperimentBatchState> LoadState(string batchDirectory)
        {
            JsonLoadResult<ExperimentBatchState> result = store.TryLoad<ExperimentBatchState>(Path.Combine(batchDirectory, StateFileName));
            return result.Success && result.Value.SchemaVersion != ExperimentSchema.CurrentVersion
                ? JsonLoadResult<ExperimentBatchState>.Failed("The batch state schema version is unsupported.")
                : result;
        }
    }
}
