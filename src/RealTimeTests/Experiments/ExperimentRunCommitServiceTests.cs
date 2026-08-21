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
                Assert.That(validation.Manifest.FinalTrackedPopulation, Is.EqualTo(1000));
                Assert.That(validation.Manifest.FinalExposed, Is.EqualTo(20));
                Assert.That(validation.Manifest.FinalSick, Is.EqualTo(15));
                Assert.That(validation.Manifest.FinalRecovered, Is.EqualTo(100));
                Assert.That(validation.Manifest.FinalDead, Is.EqualTo(5));
                Assert.That(validation.Manifest.FinalTransmissionsTotal, Is.EqualTo(140));
                Assert.That(validation.Manifest.FinalAttackRatePercent, Is.EqualTo(14.0d));
                Assert.That(validation.Manifest.FinalFatalityRatePercent, Is.EqualTo(3.5714285714285716d));
                Assert.That(validation.Manifest.OutputFiles, Has.Count.EqualTo(3));
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
                Assert.That(summary, Does.Contain("FinalTrackedPopulation,FinalExposed,FinalSick,FinalRecovered,FinalDead"));
                Assert.That(summary, Does.Contain("1000,20,15,100,5,140,14"));
                Assert.That(Directory.GetFiles(request.BatchDirectory, "*.tmp.*", SearchOption.TopDirectoryOnly), Is.Empty);
            }
            finally
            {
                DeleteTemporaryDirectory(root);
            }
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
            File.WriteAllText(Path.Combine(attemptDirectory, ExperimentRunCommitService.DataFileName), "time,value\r\n1,2\r\n", Encoding.UTF8);
            File.WriteAllText(Path.Combine(attemptDirectory, ExperimentRunCommitService.ContactsFileName), "source,target\r\n1,2\r\n", Encoding.UTF8);

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
            manifest.FinalExposed = 20;
            manifest.FinalSick = 15;
            manifest.FinalRecovered = 100;
            manifest.FinalDead = 5;
            manifest.FinalTransmissionsTotal = 140;
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
