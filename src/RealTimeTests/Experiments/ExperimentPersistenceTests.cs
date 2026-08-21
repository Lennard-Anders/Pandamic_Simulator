// <copyright file="ExperimentPersistenceTests.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTimeTests.Experiments
{
    using System;
    using System.IO;
    using RealTime.Experiments;
    using NUnit.Framework;

    public sealed class ExperimentPersistenceTests
    {
        [Test]
        public void AtomicStoreRoundTripsDurableStateAndReplacesIt()
        {
            string directory = Path.Combine(Path.GetTempPath(), "realtime-experiment-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(directory, "state.json");
            try
            {
                AtomicJsonFileStore store = new AtomicJsonFileStore();
                ExperimentBatchState state = new ExperimentBatchState
                {
                    BatchId = "batch",
                    ScenarioIndex = 2,
                    State = ExperimentBatchExecutionState.Running,
                };
                store.Save(path, state);
                state.ScenarioIndex = 3;
                store.Save(path, state);

                JsonLoadResult<ExperimentBatchState> loaded = store.TryLoad<ExperimentBatchState>(path);

                Assert.That(loaded.Success, Is.True, loaded.Error);
                Assert.That(loaded.Value.BatchId, Is.EqualTo("batch"));
                Assert.That(loaded.Value.ScenarioIndex, Is.EqualTo(3));
                Assert.That(loaded.Value.State, Is.EqualTo(ExperimentBatchExecutionState.Running));
                Assert.That(Directory.GetFiles(directory, "*.tmp.*"), Is.Empty);
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        [Test]
        public void MissingArtifactReturnsAUsefulResult()
        {
            JsonLoadResult<ExperimentBatchState> result = new AtomicJsonFileStore()
                .TryLoad<ExperimentBatchState>(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.json"));

            Assert.That(result.Success, Is.False);
            Assert.That(result.FileExists, Is.False);
            Assert.That(result.Error, Is.Not.Empty);
        }

        [Test]
        public void CorruptPrimaryRecoversLastCommittedBackup()
        {
            string directory = Path.Combine(Path.GetTempPath(), "realtime-experiment-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(directory, "artifact.json");
            try
            {
                AtomicJsonFileStore store = new AtomicJsonFileStore();
                store.Save(path, new ExperimentBatchState { BatchId = "first", ScenarioIndex = 1 });
                store.Save(path, new ExperimentBatchState { BatchId = "second", ScenarioIndex = 2 });
                File.WriteAllText(path, "not-json");

                JsonLoadResult<ExperimentBatchState> loaded = store.TryLoad<ExperimentBatchState>(path);

                Assert.That(loaded.Success, Is.True, loaded.Error);
                Assert.That(loaded.RecoveredFromBackup, Is.True);
                Assert.That(loaded.Value.BatchId, Is.EqualTo("first"));
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        [Test]
        public void PersistenceUsesCanonicalArtifactNames()
        {
            Assert.That(ExperimentPersistenceService.PlanFileName, Is.EqualTo("batch_manifest.json"));
            Assert.That(ExperimentPersistenceService.StateFileName, Is.EqualTo("batch_state.json"));
        }

        [Test]
        public void PersistenceRejectsFutureSchemasWithoutMigration()
        {
            string directory = Path.Combine(Path.GetTempPath(), "realtime-future-schema-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new AtomicJsonFileStore();
                var persistence = new ExperimentPersistenceService(store);
                persistence.SaveState(directory, new ExperimentBatchState
                {
                    SchemaVersion = ExperimentSchema.CurrentVersion + 1,
                    BatchId = "future",
                });

                JsonLoadResult<ExperimentBatchState> loaded = persistence.LoadState(directory);
                Assert.That(loaded.Success, Is.False);
                Assert.That(loaded.Error, Does.Contain("unsupported"));
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }
    }
}
