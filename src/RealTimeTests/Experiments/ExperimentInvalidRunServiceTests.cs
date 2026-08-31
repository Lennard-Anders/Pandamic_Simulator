namespace RealTimeTests.Experiments
{
    using System;
    using System.IO;
    using NUnit.Framework;
    using RealTime.Experiments;

    public sealed class ExperimentInvalidRunServiceTests
    {
        [Test]
        public void ArchivePublishesDiagnosticsWithoutSuccessfulBatchSummary()
        {
            string root = CreateTemporaryDirectory();
            try
            {
                string attempt = Path.Combine(root, "run_001.__inprogress_attempt");
                string normal = Path.Combine(root, "run_001");
                string invalid = ExperimentInvalidRunService.ResolveInvalidDirectory(normal, "1234567890abcdef");
                Directory.CreateDirectory(attempt);
                File.WriteAllText(Path.Combine(attempt, "errors.json"), "{\"Status\":\"Invalid\"}");
                File.WriteAllText(Path.Combine(attempt, "state_timeseries.csv"), "header\nrow");
                var service = new ExperimentInvalidRunService(new AtomicJsonFileStore());

                ExperimentInvalidRunArchiveResult result = service.Archive(new ExperimentInvalidRunArchiveRequest
                {
                    AttemptDirectory = attempt,
                    InvalidDirectory = invalid,
                    Manifest = Manifest(),
                });

                Assert.That(result.Success, Is.True, result.Error);
                Assert.That(Directory.Exists(attempt), Is.False);
                Assert.That(Directory.Exists(invalid), Is.True);
                Assert.That(File.Exists(Path.Combine(invalid, ExperimentInvalidRunService.ManifestFileName)), Is.True);
                Assert.That(File.Exists(Path.Combine(root, ExperimentRunCommitService.BatchSummaryFileName)), Is.False);
                string json = File.ReadAllText(Path.Combine(invalid, ExperimentInvalidRunService.ManifestFileName));
                Assert.That(json, Does.Contain("\"Status\":\"Invalid\""));
                Assert.That(json, Does.Contain("state_timeseries.csv"));
                Assert.That(json, Does.Contain("Sha256"));
            }
            finally
            {
                DeleteTemporaryDirectory(root);
            }
        }

        [Test]
        public void ArchiveRejectsTemporaryFilesAndNeverOverwritesDiagnostics()
        {
            string root = CreateTemporaryDirectory();
            try
            {
                string attempt = Path.Combine(root, "attempt");
                string invalid = Path.Combine(root, "invalid");
                Directory.CreateDirectory(attempt);
                File.WriteAllText(Path.Combine(attempt, "physical_contacts.csv.tmp"), "partial");
                var service = new ExperimentInvalidRunService(new AtomicJsonFileStore());
                ExperimentInvalidRunArchiveRequest request = new ExperimentInvalidRunArchiveRequest
                {
                    AttemptDirectory = attempt,
                    InvalidDirectory = invalid,
                    Manifest = Manifest(),
                };

                ExperimentInvalidRunArchiveResult temporary = service.Archive(request);
                Assert.That(temporary.Success, Is.False);
                Assert.That(temporary.Error, Does.Contain("temporary"));

                File.Delete(Path.Combine(attempt, "physical_contacts.csv.tmp"));
                Directory.CreateDirectory(invalid);
                ExperimentInvalidRunArchiveResult overwrite = service.Archive(request);
                Assert.That(overwrite.Success, Is.False);
                Assert.That(overwrite.Error, Does.Contain("never overwritten"));
            }
            finally
            {
                DeleteTemporaryDirectory(root);
            }
        }

        private static ExperimentInvalidRunManifest Manifest()
        {
            return new ExperimentInvalidRunManifest
            {
                BatchId = "batch",
                RunId = "scenario-run-0001",
                AttemptId = "attempt",
                ScenarioId = "scenario",
                ScenarioName = "Scenario",
                MasterSeed = 1,
                DerivedSeeds = new FnvExperimentSeedProvider().DeriveSeeds(1),
                Error = new ExperimentErrorInfo
                {
                    Code = "SimulationStepIntegrityViolation",
                    Message = "A required fixed step was skipped.",
                },
            };
        }

        private static string CreateTemporaryDirectory()
        {
            string path = Path.Combine(Path.GetTempPath(), "TENUS-invalid-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static void DeleteTemporaryDirectory(string path)
        {
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
    }
}
