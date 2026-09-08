namespace RealTimeTests.Pandemic
{
    using System;
    using System.IO;
    using NUnit.Framework;
    using RealTime.Config;
    using RealTime.Experiments;
    using RealTime.Pandemic;

    internal sealed class DashboardExportContractTests
    {
        [TestCase(ScientificContactExportMode.Standard)]
        [TestCase(ScientificContactExportMode.FullRaw)]
        [TestCase(ScientificContactExportMode.SummaryOnly)]
        [Explicit("Generate real exporter fixtures for dashboard/verify_export_contract.py")]
        public void ExportDashboardContract(ScientificContactExportMode mode)
        {
            string root = Environment.GetEnvironmentVariable("TENUS_DASHBOARD_CONTRACT_ROOT");
            Assert.That(root, Is.Not.Null.And.Not.Empty, "Set TENUS_DASHBOARD_CONTRACT_ROOT to a new fixture directory");
            string directory = Path.Combine(root, mode.ToString());
            var start = new DateTime(2030, 1, 1);
            using (var recorder = new ExperimentRecorder())
            {
                recorder.BeginRun(start, directory, mode);
                recorder.RecordState(start, new DiseaseStateCounts { Susceptible = 7, Infectious = 3 }, 0, 3, 0, 0, 0, 0);
                for (int step = 1; step <= 4; step++)
                {
                    DateTime end = start.AddMinutes(step * 5);
                    recorder.BeginContactStep(end);
                    recorder.RecordPhysicalContact(new PhysicalContactEvent { ContactId = step, CitizenA = 1, CitizenB = 2,
                        StartTime = end.AddMinutes(-5), EndTime = end, DurationMinutes = 5,
                        Context = PhysicalContactContext.Workplace, BuildingId = 7, TraceableByApp = true }, 0);
                    recorder.EndContactStep();
                }
                recorder.RecordTransmission(new PandemicTransmissionEvent { SimulationTime = start.AddMinutes(5), SourceCitizenId = 1, TargetCitizenId = 2,
                    Context = PhysicalContactContext.Workplace, OriginCategory = PandemicInfectionOriginCategory.WorkplaceOfficeIndustry,
                    TargetPreviousState = DiseaseState.Susceptible, TargetNewState = DiseaseState.Exposed, TransmissionProbability = 0.1 });
                recorder.RecordState(start.AddDays(1), new DiseaseStateCounts { Susceptible = 6, Exposed = 1, Infectious = 1,
                    PostInfectiousIll = 1, Recovered = 1 }, 0, 3, 1, 0, 0, 0);
                var snapshot = recorder.Freeze(start.AddDays(1));
                var summary = ScientificRunExportService.CalculateSummary(snapshot, null, 0);
                File.WriteAllText(Path.Combine(directory, "run_summary.csv"), ScientificRunExportService.BuildRunSummaryCsv(summary));
                File.WriteAllText(Path.Combine(directory, "state_timeseries.csv"), ScientificRunExportService.BuildStateTimeSeriesCsv(snapshot));
                File.WriteAllText(Path.Combine(directory, "intervention_events.csv"), ScientificRunExportService.BuildInterventionEventsCsv(snapshot));
                File.WriteAllText(Path.Combine(directory, "test_events.csv"), ScientificRunExportService.BuildTestEventsCsv(null));
                File.WriteAllText(Path.Combine(directory, "population_events.csv"), ScientificRunExportService.BuildPopulationEventsCsv(snapshot));
                File.WriteAllText(Path.Combine(directory, "contact_network_summary.csv"), snapshot.ContactNetworkSummaryCsv);
                File.WriteAllText(Path.Combine(directory, "contact_episode_summary.csv"), snapshot.ContactEpisodeSummaryCsv);
                var settings = ExperimentScenarioSnapshot.Capture(new RealTimeConfig(true) { ScientificContactExportMode = mode }, false);
                var manifest = new ExperimentRunManifest { Status = "Completed", Scenario = new ExperimentScenario { Settings = settings } };
                ExperimentRunCommitService.PopulateContactMetadata(manifest, snapshot, settings);
                File.WriteAllText(Path.Combine(directory, "run_manifest.json"), new ExperimentJsonSerializer().Serialize(manifest));
            }
        }
    }
}
