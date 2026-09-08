// <copyright file="ExperimentRunCommitServiceTests.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTimeTests.Experiments
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using NUnit.Framework;
    using RealTime.Experiments;

    public sealed class ExperimentRunCommitServiceTests
    {
        [Test]
        public void LegacyConfigHashRemainsVerifiableForLegacyContactSchema()
        {
            string root = CreateTemporaryDirectory();
            try
            {
                var request = CreateRequest(root, 0, 0, "legacy", "Batch", "Scenario");
                request.Manifest.ConfigurationHashAlgorithm = ExperimentConfigurationHasher.LegacyAlgorithmName;
                request.Manifest.ConfigurationHash = ExperimentConfigurationHasher.ComputeLegacy(request.Manifest.Scenario);
                var service = CreateService();
                var result = service.Commit(request);
                Assert.That(result.Success, Is.True, result.Error);
                Assert.That(service.ValidateRunDirectory(request.FinalDirectory).Success, Is.True);
            }
            finally { DeleteTemporaryDirectory(root); }
        }
        [TestCase(RealTime.Config.ScientificContactExportMode.Standard)]
        [TestCase(RealTime.Config.ScientificContactExportMode.FullRaw)]
        [TestCase(RealTime.Config.ScientificContactExportMode.SummaryOnly)]
        public void ContactModesCommitActualRecorderOutputWithPartitionInventory(RealTime.Config.ScientificContactExportMode mode)
        {
            string root = CreateTemporaryDirectory();
            try
            {
                var service = CreateService();
                var request = CreateRequest(root, 0, 0, "new-contact-run", "Batch", "Scenario");
                foreach (string legacy in new[] { "physical_contacts.csv", "traceable_contacts.csv", "transmission_events.csv" })
                    File.Delete(Path.Combine(request.AttemptDirectory, legacy));
                var config = new RealTime.Config.RealTimeConfig(true) { ScientificContactExportMode = mode };
                request.Manifest.Scenario.Settings = ExperimentScenarioSnapshot.Capture(config, false);
                request.Manifest.ConfigurationHash = ExperimentConfigurationHasher.Compute(request.Manifest.Scenario);
                var start = new DateTime(2030, 1, 1);
                using (var recorder = new RealTime.Pandemic.ExperimentRecorder())
                {
                    recorder.BeginRun(start, request.AttemptDirectory, mode);
                    var engine = new RealTime.Pandemic.ContactEngine();
                    foreach (int minute in new[] { 5, 10, 1445 })
                    {
                        var contact = engine.Record(new RealTime.Pandemic.PhysicalContactRequest { CitizenA = 1, CitizenB = 2,
                            EndTime = start.AddMinutes(minute), DurationMinutes = 5, Context = RealTime.Pandemic.PhysicalContactContext.Workplace }, out bool created);
                        recorder.RecordPhysicalContact(contact, 0);
                    }
                    var snapshot = recorder.Freeze(start.AddMinutes(1445));
                    ExperimentRunCommitService.PopulateContactMetadata(request.Manifest, snapshot, request.Manifest.Scenario.Settings);
                    File.WriteAllText(Path.Combine(request.AttemptDirectory, "contact_episode_summary.csv"), snapshot.ContactEpisodeSummaryCsv);
                    File.WriteAllText(Path.Combine(request.AttemptDirectory, "contact_step_summary.csv"), RealTime.Pandemic.ScientificRunExportService.BuildContactStepSummary(snapshot.ContactNetworkSummaryCsv));
                    File.WriteAllText(Path.Combine(request.AttemptDirectory, "contact_episode_duration_distribution.csv"), snapshot.ContactEpisodeDurationCsv);
                }
                request.Manifest.ContactPersistenceMinutes["school"]++;
                Assert.That(service.Commit(request).Success, Is.False, "Mismatched persistence must not publish");
                request.Manifest.ContactPersistenceMinutes["school"]--;
                request.Manifest.EpidemicStepMinutes++;
                Assert.That(service.Commit(request).Success, Is.False, "Mismatched timestep must not publish");
                request.Manifest.EpidemicStepMinutes--;
                if (request.Manifest.ContactFiles.Count > 0)
                {
                    request.Manifest.ContactFiles[0].RowCount++;
                    Assert.That(service.Commit(request).Success, Is.False, "Partition rows must reconcile with run totals");
                    request.Manifest.ContactFiles[0].RowCount--;
                }
                var result = service.Commit(request);
                Assert.That(result.Success, Is.True, result.Error);
                var validation = service.ValidateRunDirectory(request.FinalDirectory);
                Assert.That(validation.Success, Is.True, validation.Error);
                Assert.That(validation.Manifest.ScientificExportSchemaVersion, Is.EqualTo(2));
                int expected = mode == RealTime.Config.ScientificContactExportMode.FullRaw ? 2 : mode == RealTime.Config.ScientificContactExportMode.Standard ? 1 : 0;
                Assert.That(validation.Manifest.ContactFiles.Count, Is.EqualTo(expected));
                string extraDirectory = Path.Combine(request.FinalDirectory, "physical_contacts");
                Directory.CreateDirectory(extraDirectory);
                string extraPartition = Path.Combine(extraDirectory, "day_999.csv.gz");
                File.WriteAllText(extraPartition, "unlisted partition");
                Assert.That(service.ValidateRunDirectory(request.FinalDirectory).Success, Is.False);
                File.Delete(extraPartition);
                Assert.That(service.ValidateRunDirectory(request.FinalDirectory).Success, Is.True);
                foreach (var file in validation.Manifest.ContactFiles)
                {
                    Assert.That(file.RowCount, Is.GreaterThan(0));
                    Assert.That(file.SimulationStartTime, Is.Not.Null);
                    Assert.That(file.Sha256, Has.Length.EqualTo(64));
                }
                if (expected > 0)
                {
                    File.AppendAllText(Path.Combine(request.FinalDirectory, validation.Manifest.ContactFiles[0].RelativePath), "corrupt");
                    Assert.That(service.ValidateRunDirectory(request.FinalDirectory).Success, Is.False);
                }
            }
            finally { DeleteTemporaryDirectory(root); }
        }

        [Test]
        public void InvalidArchiveDoesNotPreventNextScenarioCommitOrSummaryRecovery()
        {
            string root = CreateTemporaryDirectory();
            try
            {
                var service = CreateService();
                var invalid = CreateRequest(root, 0, 0, "failed-run", "Batch A", "Baseline");
                string archive = invalid.FinalDirectory + ".__invalid_attempt";
                Directory.CreateDirectory(archive);
                string manifestPath = Path.Combine(archive, ExperimentRunCommitService.RunManifestFileName);
                const string diagnostic = "{\"Status\":\"Invalid\",\"RunId\":\"failed-run\"}";
                File.WriteAllText(manifestPath, diagnostic);
                var next = CreateRequest(root, 1, 0, "valid-run", "Batch A", "Next scenario");
                var committed = service.Commit(next);
                Assert.That(committed.Success, Is.True, committed.Error);
                var summary = service.RebuildBatchSummary(next.BatchDirectory);
                Assert.That(summary.Success, Is.True, summary.Error);
                Assert.That(summary.Rows.Count, Is.EqualTo(1));
                Assert.That(File.ReadAllText(manifestPath), Is.EqualTo(diagnostic));
                // Corrupt manifests in actual final directories must still fail closed.
                File.WriteAllText(Path.Combine(next.FinalDirectory, ExperimentRunCommitService.RunManifestFileName), diagnostic);
                Assert.That(service.RebuildBatchSummary(next.BatchDirectory).Success, Is.False);
            }
            finally { DeleteTemporaryDirectory(root); }
        }
        [Test]
        public void CommitVerifiesWritesManifestMovesDirectoryAndBuildsSummary()
        {
            string root = CreateTemporaryDirectory();
            try
            {
                ExperimentRunCommitService service = CreateService();
                ExperimentRunCommitRequest request = CreateRequest(root, 0, 0, "run-a", "Batch A", "Baseline");

                ExperimentRunCommitResult result = service.Commit(request);

                Assert.That(result.Success, Is.True, result.Error);
                Assert.That(result.Published, Is.True);
                Assert.That(Directory.Exists(request.AttemptDirectory), Is.False);
                Assert.That(Directory.Exists(request.FinalDirectory), Is.True);
                Assert.That(File.Exists(Path.Combine(request.FinalDirectory, ExperimentRunCommitService.RunManifestFileName)), Is.True);
                Assert.That(File.Exists(Path.Combine(request.BatchDirectory, ExperimentRunCommitService.BatchSummaryFileName)), Is.True);

                ExperimentRunValidationResult validation = service.ValidateRunDirectory(request.FinalDirectory);
                Assert.That(validation.Success, Is.True, validation.Error);
                Assert.That(validation.Manifest.BatchName, Is.EqualTo("Batch A"));
                Assert.That(validation.Manifest.ScenarioName, Is.EqualTo("Baseline"));
                Assert.That(validation.Manifest.RunNumber, Is.EqualTo(1));
                Assert.That(validation.Manifest.SeedAlgorithm, Is.EqualTo(ExperimentRunCommitService.SeedAlgorithmName));
                ExperimentSeedSet expectedSeeds = new FnvExperimentSeedProvider().DeriveSeeds(validation.Manifest.MasterSeed);
                Assert.That(validation.Manifest.InitialPopulationSeed, Is.EqualTo(expectedSeeds.InitialPopulation));
                Assert.That(validation.Manifest.DiseaseProgressionSeed, Is.EqualTo(expectedSeeds.DiseaseProgression));
                Assert.That(validation.Manifest.TransmissionSeed, Is.EqualTo(expectedSeeds.Transmission));
                Assert.That(validation.Manifest.SymptomSeed, Is.EqualTo(expectedSeeds.Symptom));
                Assert.That(validation.Manifest.MortalitySeed, Is.EqualTo(expectedSeeds.Mortality));
                Assert.That(validation.Manifest.MaskSeed, Is.EqualTo(expectedSeeds.Mask));
                Assert.That(validation.Manifest.TestingSeed, Is.EqualTo(expectedSeeds.Testing));
                Assert.That(validation.Manifest.ContactTracingSeed, Is.EqualTo(expectedSeeds.ContactTracing));
                Assert.That(validation.Manifest.InterventionSeed, Is.EqualTo(expectedSeeds.Intervention));
                Assert.That(validation.Manifest.ConfigurationHash, Has.Length.EqualTo(64));
                Assert.That(validation.Manifest.GitCommitSha, Has.Length.EqualTo(40));
                Assert.That(validation.Manifest.GitBranchOrTag, Is.EqualTo("Beta"));
                Assert.That(validation.Manifest.FinalTrackedPopulation, Is.EqualTo(1000));
                Assert.That(validation.Manifest.FinalExposed, Is.EqualTo(20));
                Assert.That(validation.Manifest.FinalSick, Is.EqualTo(15));
                Assert.That(validation.Manifest.FinalRecovered, Is.EqualTo(100));
                Assert.That(validation.Manifest.FinalDead, Is.EqualTo(5));
                Assert.That(validation.Manifest.FinalTransmissionsTotal, Is.EqualTo(140));
                Assert.That(validation.Manifest.FinalAttackRatePercent, Is.EqualTo(14.0d));
                Assert.That(validation.Manifest.FinalFatalityRatePercent, Is.EqualTo(3.5714285714285716d));
                Assert.That(validation.Manifest.OutputFiles, Has.Count.EqualTo(13));
                foreach (ExperimentGeneratedFileEntry file in validation.Manifest.OutputFiles)
                {
                    Assert.That(file.Sha256, Has.Length.EqualTo(64));
                    Assert.That(file.LengthBytes, Is.GreaterThan(0L));
                }

                string summary = File.ReadAllText(result.SummaryPath);
                Assert.That(summary, Does.Contain("RunId"));
                Assert.That(summary, Does.Contain("run-a"));
                Assert.That(summary, Does.Contain("SeedAlgorithm"));
                Assert.That(summary, Does.Contain(ExperimentRunCommitService.SeedAlgorithmName));
                Assert.That(summary, Does.Contain("FinalTrackedPopulation,FinalSusceptible,FinalExposed,FinalInfectious"));
                Assert.That(summary, Does.Contain("1000,860,20,15,0,0,15,100,5,140"));
                Assert.That(Directory.GetFiles(request.BatchDirectory, "*.tmp.*", SearchOption.TopDirectoryOnly), Is.Empty);
            }
            finally
            {
                DeleteTemporaryDirectory(root);
            }
        }

        [Test]
        public void ExperimentLibraryLoadsVerifiedRunsAndRejectsChangedScientificFiles()
        {
            string root = CreateTemporaryDirectory();
            try
            {
                var service = CreateService();
                var request = CreateRequest(root, 0, 0, "library-run", "Batch", "Scenario");
                File.WriteAllText(Path.Combine(request.AttemptDirectory, "run_summary.csv"), "cumulative_infections,deaths_total\n140,5\n");
                File.WriteAllText(Path.Combine(request.AttemptDirectory, "state_timeseries.csv"), "simulation_time,susceptible,exposed,infectious,post_infectious_ill,recovered,dead,tracked_population\n2030-01-01T00:00:00,860,20,15,0,100,5,1000\n");
                Assert.That(service.Commit(request).Success, Is.True);
                Assert.That(File.Exists(Path.Combine(request.BatchDirectory, "batch_quality_report.txt")), Is.True);
                var library = ExperimentResultsAnalysis.Scan(new[] { root, root });
                Assert.That(library.Runs.Count, Is.EqualTo(1));
                Assert.That(library.Runs[0].Metrics["peak_prevalence_pct"], Is.EqualTo(3.5));
                Assert.That(library.Runs[0].Metrics.ContainsKey("closure_family_days"), Is.False);
                File.AppendAllText(Path.Combine(request.FinalDirectory, "run_summary.csv"), "broken");
                library = ExperimentResultsAnalysis.Scan(new[] { root });
                Assert.That(library.Runs, Is.Empty);
                Assert.That(library.Excluded, Is.EqualTo(1));
            }
            finally { DeleteTemporaryDirectory(root); }
        }

        [Test]
        public void ClosureBurdenStopsAtScientificEndpointEvenWhenLegacyManifestEndsLater()
        {
            string root = CreateTemporaryDirectory();
            try
            {
                var service = CreateService();
                var request = CreateRequest(root, 0, 0, "closure-run", "Batch", "Scenario");
                File.WriteAllText(Path.Combine(request.AttemptDirectory, "run_summary.csv"), "cumulative_infections,deaths_total\n140,5\n");
                File.WriteAllText(Path.Combine(request.AttemptDirectory, "state_timeseries.csv"),
                    "simulation_time,susceptible,exposed,infectious,post_infectious_ill,recovered,dead,tracked_population\n2026-09-17T00:00:00,860,20,15,0,100,5,1000\n");
                File.WriteAllText(Path.Combine(request.AttemptDirectory, "intervention_events.csv"),
                    "simulation_time,reason,context,action\n2026-09-16T00:00:00,EffectiveFamilyState,Schools,Close\n");
                Assert.That(service.Commit(request).Success, Is.True);
                var library = ExperimentResultsAnalysis.Scan(new[] { root });
                Assert.That(library.Runs.Count, Is.EqualTo(1));
                Assert.That(library.Runs[0].Metrics["closure_family_days"], Is.EqualTo(1));
            }
            finally { DeleteTemporaryDirectory(root); }
        }

        [Test]
        public void CommitRejectsMissingMandatoryFileWithoutPublishing()
        {
            string root = CreateTemporaryDirectory();
            try
            {
                ExperimentRunCommitService service = CreateService();
                ExperimentRunCommitRequest request = CreateRequest(root, 0, 0, "run-a", "Batch", "Scenario");
                File.Delete(Path.Combine(request.AttemptDirectory, ExperimentRunCommitService.ContactsFileName));

                ExperimentRunCommitResult result = service.Commit(request);

                Assert.That(result.Success, Is.False);
                Assert.That(result.Error, Does.Contain("contacts.csv"));
                Assert.That(Directory.Exists(request.AttemptDirectory), Is.True);
                Assert.That(Directory.Exists(request.FinalDirectory), Is.False);
                Assert.That(File.Exists(Path.Combine(request.AttemptDirectory, ExperimentRunCommitService.RunManifestFileName)), Is.False);
            }
            finally
            {
                DeleteTemporaryDirectory(root);
            }
        }

        [Test]
        public void ExtendedPackageRequiresAndFingerprintsNetworkAndCalibrationFiles()
        {
            string root = CreateTemporaryDirectory();
            try
            {
                ExperimentRunCommitService service = CreateService();
                ExperimentRunCommitRequest request = CreateRequest(root, 0, 0, "extended", "Batch", "Scenario");
                request.Manifest.ScientificExtensionsVersion = 1;
                Assert.That(service.Commit(request).Success, Is.False);
                foreach (string name in ExperimentRunCommitService.ScientificExtensionFiles)
                    File.WriteAllText(Path.Combine(request.AttemptDirectory, name), "column\nvalue\n");
                Assert.That(service.Commit(request).Success, Is.True);
                Assert.That(service.ValidateRunDirectory(request.FinalDirectory).Success, Is.True);
                File.AppendAllText(Path.Combine(request.FinalDirectory, "contact_network_summary.csv"), "corrupt\n");
                Assert.That(service.ValidateRunDirectory(request.FinalDirectory).Success, Is.False);
            }
            finally { DeleteTemporaryDirectory(root); }
        }

        [Test]
        public void CommitNeverOverwritesAnExistingFinalDirectory()
        {
            string root = CreateTemporaryDirectory();
            try
            {
                ExperimentRunCommitService service = CreateService();
                ExperimentRunCommitRequest request = CreateRequest(root, 0, 0, "run-a", "Batch", "Scenario");
                Directory.CreateDirectory(request.FinalDirectory);
                string sentinel = Path.Combine(request.FinalDirectory, "sentinel.txt");
                File.WriteAllText(sentinel, "keep-me");

                ExperimentRunCommitResult result = service.Commit(request);

                Assert.That(result.Success, Is.False);
                Assert.That(result.Error, Does.Contain("not be overwritten"));
                Assert.That(File.ReadAllText(sentinel), Is.EqualTo("keep-me"));
                Assert.That(Directory.Exists(request.AttemptDirectory), Is.True);
            }
            finally
            {
                DeleteTemporaryDirectory(root);
            }
        }

        [Test]
        public void SummaryIsEscapedDeduplicatedAndDeterministicallyOrdered()
        {
            string root = CreateTemporaryDirectory();
            try
            {
                ExperimentRunCommitService service = CreateService();
                string batchName = "Batch, \"Quoted\"\r\nLine";
                ExperimentRunCommitRequest lastScenario = CreateRequest(root, 1, 0, "run-z", batchName, "Scenario Z");
                ExperimentRunCommitRequest secondRun = CreateRequest(root, 0, 1, "run-b", batchName, "Scenario A");
                ExperimentRunCommitRequest firstRun = CreateRequest(root, 0, 0, "run-a", batchName, "Scenario A");
                ExperimentRunCommitRequest duplicate = CreateRequest(root, 2, 0, "run-a", batchName, "Duplicate");

                Assert.That(service.Commit(lastScenario).Success, Is.True);
                Assert.That(service.Commit(secondRun).Success, Is.True);
                Assert.That(service.Commit(firstRun).Success, Is.True);
                ExperimentRunCommitResult duplicateResult = service.Commit(duplicate);
                Assert.That(duplicateResult.Success, Is.True, duplicateResult.Error);

                string csv = File.ReadAllText(duplicateResult.SummaryPath);
                Assert.That(csv, Does.Contain("\"Batch, \"\"Quoted\"\"\r\nLine\""));
                Assert.That(CountOccurrences(csv, "run-a"), Is.EqualTo(1));
                Assert.That(csv.IndexOf("run-a", StringComparison.Ordinal), Is.LessThan(csv.IndexOf("run-b", StringComparison.Ordinal)));
                Assert.That(csv.IndexOf("run-b", StringComparison.Ordinal), Is.LessThan(csv.IndexOf("run-z", StringComparison.Ordinal)));

                byte[] firstBuild = File.ReadAllBytes(duplicateResult.SummaryPath);
                ExperimentBatchSummaryResult rebuilt = service.RebuildBatchSummary(duplicate.BatchDirectory);
                Assert.That(rebuilt.Success, Is.True, rebuilt.Error);
                Assert.That(File.ReadAllBytes(rebuilt.SummaryPath), Is.EqualTo(firstBuild));
                Assert.That(rebuilt.Rows, Has.Count.EqualTo(3));
            }
            finally
            {
                DeleteTemporaryDirectory(root);
            }
        }

        [Test]
        public void RecoveryPublishesPreparedAttemptAndIsIdempotentForFinalDirectory()
        {
            string root = CreateTemporaryDirectory();
            try
            {
                ExperimentRunCommitService service = CreateService();
                ExperimentRunCommitRequest request = CreateRequest(root, 0, 0, "run-a", "Batch", "Scenario");
                ExperimentRunCommitResult committed = service.Commit(request);
                Assert.That(committed.Success, Is.True, committed.Error);

                File.Delete(committed.SummaryPath);
                Directory.Move(request.FinalDirectory, request.AttemptDirectory);

                ExperimentRunCommitResult recovered = service.Recover(
                    request.BatchDirectory,
                    request.AttemptDirectory,
                    request.FinalDirectory,
                    "run-a");

                Assert.That(recovered.Success, Is.True, recovered.Error);
                Assert.That(recovered.AlreadyPublished, Is.False);
                Assert.That(Directory.Exists(request.AttemptDirectory), Is.False);
                Assert.That(Directory.Exists(request.FinalDirectory), Is.True);
                Assert.That(File.Exists(recovered.SummaryPath), Is.True);

                ExperimentRunCommitResult repeated = service.Recover(
                    request.BatchDirectory,
                    request.AttemptDirectory,
                    request.FinalDirectory,
                    "run-a");
                Assert.That(repeated.Success, Is.True, repeated.Error);
                Assert.That(repeated.AlreadyPublished, Is.True);
            }
            finally
            {
                DeleteTemporaryDirectory(root);
            }
        }

        [Test]
        public void ValidationAndRecoveryRejectTamperedPublishedOutput()
        {
            string root = CreateTemporaryDirectory();
            try
            {
                ExperimentRunCommitService service = CreateService();
                ExperimentRunCommitRequest request = CreateRequest(root, 0, 0, "run-a", "Batch", "Scenario");
                ExperimentRunCommitResult committed = service.Commit(request);
                Assert.That(committed.Success, Is.True, committed.Error);
                File.AppendAllText(Path.Combine(request.FinalDirectory, ExperimentRunCommitService.DataFileName), "tampered");

                ExperimentRunValidationResult validation = service.ValidateRunDirectory(request.FinalDirectory);
                ExperimentRunCommitResult recovery = service.Recover(
                    request.BatchDirectory,
                    request.FinalDirectory,
                    request.FinalDirectory,
                    "run-a");

                Assert.That(validation.Success, Is.False);
                Assert.That(validation.Error, Does.Contain("SHA-256 or length"));
                Assert.That(recovery.Success, Is.False);
                Assert.That(Directory.Exists(request.FinalDirectory), Is.True);
            }
            finally
            {
                DeleteTemporaryDirectory(root);
            }
        }

        private static ExperimentRunCommitService CreateService()
        {
            return new ExperimentRunCommitService(new AtomicJsonFileStore());
        }

        private static ExperimentRunCommitRequest CreateRequest(
            string root,
            int scenarioIndex,
            int runIndex,
            string runId,
            string batchName,
            string scenarioName)
        {
            string batchDirectory = Path.Combine(root, "batch");
            string scenarioDirectory = Path.Combine(batchDirectory, "scenario_" + scenarioIndex);
            string finalDirectory = Path.Combine(scenarioDirectory, "run_" + (runIndex + 1).ToString("D3"));
            string attemptDirectory = finalDirectory + ".__inprogress_attempt-" + scenarioIndex + "-" + runIndex;
            Directory.CreateDirectory(attemptDirectory);
            File.WriteAllText(Path.Combine(attemptDirectory, "pandemic_run_result.csv"), "metric,value\r\ninfections,5\r\n", Encoding.UTF8);
            string[] mandatoryFiles =
            {
                ExperimentRunCommitService.DataFileName,
                ExperimentRunCommitService.ContactsFileName,
                ExperimentRunCommitService.RunSummaryFileName,
                ExperimentRunCommitService.StateTimeSeriesFileName,
                ExperimentRunCommitService.TransmissionEventsFileName,
                ExperimentRunCommitService.PhysicalContactsFileName,
                ExperimentRunCommitService.TraceableContactsFileName,
                ExperimentRunCommitService.TestEventsFileName,
                ExperimentRunCommitService.InterventionEventsFileName,
                ExperimentRunCommitService.HealthcareTimeSeriesFileName,
                ExperimentRunCommitService.PopulationEventsFileName,
                ExperimentRunCommitService.ErrorsFileName,
            };
            foreach (string mandatoryFile in mandatoryFiles)
            {
                File.WriteAllText(Path.Combine(attemptDirectory, mandatoryFile), "header\r\nvalue\r\n", Encoding.UTF8);
            }

            ExperimentScenario scenario = new ExperimentScenario
            {
                ScenarioId = "scenario-" + scenarioIndex,
                Name = scenarioName,
                DurationDays = 30d,
                EndMode = ExperimentEndMode.DurationOrExtinction,
            };
            ExperimentRunDescriptor run = new ExperimentRunDescriptor
            {
                RunId = runId,
                ScenarioIndex = scenarioIndex,
                RunIndex = runIndex,
                Scenario = scenario,
                MasterSeed = 100 + runIndex,
                Seeds = new FnvExperimentSeedProvider().DeriveSeeds(100 + runIndex),
            };
            ExperimentBatchPlan plan = new ExperimentBatchPlan
            {
                BatchId = "batch-id",
                BatchName = batchName,
                ModVersion = "1.2.3",
                GameVersion = "1.21.1-f9",
                GitCommitSha = "0123456789012345678901234567890123456789",
                GitBranchOrTag = "Beta",
                Baseline = new BaselineSaveIdentity
                {
                    AssetFullName = "baseline.SaveGameMetaData",
                    AssetChecksum = "baseline-meta-checksum",
                    DataAssetChecksum = "baseline-data-checksum",
                    LocalFileSha256 = "baseline-sha256",
                },
                Scenarios = new List<ExperimentScenario> { scenario },
            };

            ExperimentRunCommitService service = CreateService();
            ExperimentRunManifest manifest = service.CreateManifest(
                plan,
                run,
                "2026-08-21T10:00:00.0000000Z",
                "2026-09-20T10:00:00.0000000Z",
                "2026-09-18T10:00:00.0000000Z",
                "2026-08-21T10:05:00.0000000Z",
                "Extinction",
                finalDirectory);
            manifest.FinalTrackedPopulation = 1000;
            manifest.FinalSusceptible = 860;
            manifest.FinalExposed = 20;
            manifest.FinalInfectious = 15;
            manifest.FinalSick = 15;
            manifest.FinalRecovered = 100;
            manifest.FinalDead = 5;
            manifest.FinalTransmissionsTotal = 140;
            manifest.SecondaryTransmissionsTotal = 140;
            manifest.CumulativeInfections = 140;
            manifest.FinalAttackRatePercent = 14.0d;
            manifest.FinalFatalityRatePercent = 3.5714285714285716d;

            return new ExperimentRunCommitRequest
            {
                BatchDirectory = batchDirectory,
                AttemptDirectory = attemptDirectory,
                FinalDirectory = finalDirectory,
                Manifest = manifest,
            };
        }

        private static string CreateTemporaryDirectory()
        {
            string path = Path.Combine(Path.GetTempPath(), "realtime-run-commit-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static void DeleteTemporaryDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }

        private static int CountOccurrences(string value, string search)
        {
            int count = 0;
            int index = 0;
            while ((index = value.IndexOf(search, index, StringComparison.Ordinal)) >= 0)
            {
                ++count;
                index += search.Length;
            }

            return count;
        }
    }
}
