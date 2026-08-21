// <copyright file="ExperimentPathResolverTests.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTimeTests.Experiments
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using RealTime.Experiments;
    using NUnit.Framework;

    public sealed class ExperimentPathResolverTests
    {
        [Test]
        public void SanitizerRemovesTraversalAndReservedNames()
        {
            Assert.That(ExperimentPathSanitizer.SanitizeSegment("CON", "fallback"), Is.EqualTo("_CON"));
            Assert.That(ExperimentPathSanitizer.SanitizeSegment("../outside/file", "fallback"), Is.EqualTo("outside_file"));
        }

        [Test]
        public void ResolverKeepsEveryLevelInsideItsParent()
        {
            string root = Path.Combine(Path.GetTempPath(), "experiment-path-tests");
            ExperimentBatchPlan plan = new ExperimentBatchPlan
            {
                BatchId = "../../batch-id",
                BatchName = "../batch:name",
                Scenarios = new List<ExperimentScenario>
                {
                    new ExperimentScenario { ScenarioId = "../scenario", Name = "../../scenario" },
                },
            };
            ExperimentBatchState state = new ExperimentBatchState { BatchId = plan.BatchId };
            ExperimentRunDescriptor run = new ExperimentBatchSequencer(new FnvExperimentSeedProvider())
                .SelectCurrentRun(plan, state);
            ExperimentPathResolver resolver = new ExperimentPathResolver(root);

            string batch = resolver.ResolveBatchDirectory(plan);
            string scenario = resolver.ResolveScenarioDirectory(plan, 0);
            string runDirectory = resolver.ResolveRunDirectory(plan, run);
            string attemptDirectory = resolver.ResolveInProgressRunDirectory(plan, run, "attempt-123");

            Assert.That(ExperimentPathResolver.IsContained(root, batch), Is.True);
            Assert.That(ExperimentPathResolver.IsContained(batch, scenario), Is.True);
            Assert.That(ExperimentPathResolver.IsContained(scenario, runDirectory), Is.True);
            Assert.That(ExperimentPathResolver.IsContained(scenario, attemptDirectory), Is.True);
            Assert.That(Path.GetFileName(runDirectory), Is.EqualTo("run_001"));
            Assert.That(Path.GetFileName(attemptDirectory), Is.EqualTo("run_001.__inprogress_attempt-123"));
        }

        [Test]
        public void OutputRootChoicesResolveToDistinctSupportedLocations()
        {
            string root = Path.Combine(Path.GetTempPath(), "experiment-output-root-tests");
            string userData = Path.Combine(root, "user-data");
            string mod = Path.Combine(root, "mod");
            var resolver = new ExperimentOutputRootResolver(userData, mod);

            string modOutput = resolver.Resolve(new ExperimentOutputRootSelection
            {
                Kind = ExperimentOutputRootKind.ModPandemicData,
            });
            string userOutput = resolver.Resolve(new ExperimentOutputRootSelection
            {
                Kind = ExperimentOutputRootKind.UserPandemicData,
            });

            Assert.That(modOutput, Is.EqualTo(Path.GetFullPath(Path.Combine(mod, "Pandemic Data", "Experiments"))));
            Assert.That(userOutput, Is.EqualTo(Path.GetFullPath(Path.Combine(userData, "Pandemic Data", "Experiments"))));
            Assert.That(userOutput, Is.Not.EqualTo(modOutput));
        }

        [Test]
        public void BatchDirectoryUsesExplicitOutputFolderName()
        {
            string root = Path.Combine(Path.GetTempPath(), "experiment-folder-name-tests");
            ExperimentBatchPlan plan = new ExperimentBatchPlan
            {
                BatchId = "1234567890abcdef",
                BatchName = "Scientific display title",
                OutputFolderName = "My readable folder",
                CreatedUtc = "2026-08-21T15:32:50.0000000Z",
            };

            string directory = new ExperimentPathResolver(root).ResolveBatchDirectory(plan);

            Assert.That(Path.GetFileName(directory), Does.StartWith("My readable folder_20260821T153250Z_12345678"));
            Assert.That(Path.GetFileName(directory), Does.Not.Contain("Scientific display title"));
        }

        [Test]
        public void ResolverLeavesRoomForRichCsvAndAtomicBackupOnLegacyMono()
        {
            string root = Path.Combine("C:\\", new string('r', 101));
            ExperimentBatchPlan plan = new ExperimentBatchPlan
            {
                BatchId = Guid.NewGuid().ToString("N"),
                BatchName = new string('b', 100),
                CreatedUtc = "2026-08-21T15:32:50.0000000Z",
                Scenarios = new List<ExperimentScenario>
                {
                    new ExperimentScenario
                    {
                        ScenarioId = Guid.NewGuid().ToString("N"),
                        Name = new string('s', 100),
                    },
                },
            };
            ExperimentBatchState state = new ExperimentBatchState { BatchId = plan.BatchId };
            ExperimentRunDescriptor run = new ExperimentBatchSequencer(new FnvExperimentSeedProvider())
                .SelectCurrentRun(plan, state);
            ExperimentPathResolver resolver = new ExperimentPathResolver(root);

            string attempt = resolver.ResolveInProgressRunDirectory(
                plan,
                run,
                Guid.NewGuid().ToString("N"));
            string scenarioSlug = ExperimentPathSanitizer.SanitizeSegment(plan.Scenarios[0].Name, "scenario", 32);
            string richCsv = Path.Combine(attempt, "pandemic_run_" + scenarioSlug + "_001.csv");

            Assert.That((richCsv + ".bak").Length, Is.LessThanOrEqualTo(259));
            Assert.That(AtomicFileUtilities.CreateTemporarySiblingPath(richCsv).Length, Is.LessThanOrEqualTo(259));
        }
    }
}
