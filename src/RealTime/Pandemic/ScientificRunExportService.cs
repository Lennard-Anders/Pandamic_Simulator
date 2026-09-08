// <copyright file="ScientificRunExportService.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.Pandemic
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using RealTime.Experiments;

    internal sealed class ScientificRunErrorReport
    {
        public ScientificRunErrorReport()
        {
            SchemaVersion = ExperimentSchema.CurrentVersion;
            Errors = new List<ExperimentRunIntegrityFailure>();
        }

        public int SchemaVersion { get; set; }

        public string Status { get; set; }

        public List<ExperimentRunIntegrityFailure> Errors { get; private set; }
    }

    internal sealed class ScientificRunSummary
    {
        public PandemicStateTimePoint FinalState { get; set; }

        public int InitialSeedCount { get; set; }

        public int SecondaryTransmissionsTotal { get; set; }

        public int CumulativeInfections { get; set; }

        public int ActiveExposed { get; set; }

        public int ActiveInfectious { get; set; }

        public int ActivePostInfectiousIll { get; set; }

        public int ActiveSymptomatic { get; set; }

        public int RecoveredTotal { get; set; }

        public int DeathsTotal { get; set; }

        public int HospitalizationsTotal { get; set; }

        public int TrackedPopulation { get; set; }

        public double FinalIncidencePer100000PerInterval { get; set; }

        public double FinalPrevalencePercent { get; set; }

        public double AttackRatePercent { get; set; }

        public double? ResolvedCaseFatalityRatioPercent { get; set; }

        public double? EmpiricalSecondaryInfectionsPerInfector { get; set; }

        public double ActualMaskUsagePercent { get; set; }

        public double TotalIsolationPersonDays { get; set; }

        public double TotalQuarantinePersonDays { get; set; }

        public int TestsRequested { get; set; }

        public int TestsPerformed { get; set; }

        public int TestsPositive { get; set; }

        public int TestsNegative { get; set; }

        public double? MeanTestWaitDays { get; set; }

        public double? MedianTestWaitDays { get; set; }

        public long ContactsPreventedByIntervention { get; set; }

        public long PhysicalContactsTotal { get; set; }

        public long TraceableContactsTotal { get; set; }

        public long HouseholdContactsTotal { get; set; }

        public long WorkContactsTotal { get; set; }

        public long SchoolContactsTotal { get; set; }

        public long TransitContactsTotal { get; set; }

        public int AddedPopulation { get; set; }

        public int RemovedPopulation { get; set; }
    }

    /// <summary>Writes the batch-only scientific data package beside the legacy exports.</summary>
    internal sealed class ScientificRunExportService
    {
        public const string RunSummaryFileName = "run_summary.csv";
        public const string StateTimeSeriesFileName = "state_timeseries.csv";
        public const string TestEventsFileName = "test_events.csv";
        public const string InterventionEventsFileName = "intervention_events.csv";
        public const string HealthcareTimeSeriesFileName = "healthcare_timeseries.csv";
        public const string PopulationEventsFileName = "population_events.csv";
        public const string ErrorsFileName = "errors.json";

        public void Export(PandemicRunExportRequest request)
        {
            ExperimentRunIntegrityFailure failure = request?.Manager?.GetRunIntegrityFailure();
            if (failure != null)
            {
                throw new InvalidOperationException("An invalid scientific run cannot be exported as a successful run: " + failure.Code);
            }

            ExportCore(request, null);
        }

        public void ExportInvalid(PandemicRunExportRequest request, ExperimentRunIntegrityFailure failure)
        {
            if (failure == null)
            {
                throw new ArgumentNullException(nameof(failure));
            }

            ExportCore(request, failure);
        }

        private void ExportCore(PandemicRunExportRequest request, ExperimentRunIntegrityFailure forcedFailure)
        {
            using (PandemicProfiler.Measure("ScientificExport")) ExportMeasured(request, forcedFailure);
        }

        private void ExportMeasured(PandemicRunExportRequest request, ExperimentRunIntegrityFailure forcedFailure)
        {
            if (request == null || request.ScientificSnapshot == null)
            {
                throw new ArgumentException("A frozen scientific snapshot is required.", nameof(request));
            }

            string directory = Path.GetDirectoryName(request.Output.RichCsvPath);
            if (string.IsNullOrEmpty(directory))
            {
                throw new InvalidOperationException("The scientific run output directory is unavailable.");
            }

            directory = Path.GetFullPath(directory);
            RequireStreamedFile(directory, ExperimentRecorder.TransmissionEventsFileName);
            if (request.ScientificSnapshot.ContactFiles == null)
                throw new InvalidOperationException("The frozen contact storage inventory is missing.");
            foreach (var file in request.ScientificSnapshot.ContactFiles)
                RequireStreamedFile(directory, file.RelativePath);

            IList<PandemicTestRecord> tests = request.Manager.GetTestRecords();
            IList<PandemicHealthcareTimePoint> healthcare = request.Manager.GetHealthcareTimeSeries();
            ScientificRunSummary summary = CalculateSummary(
                request.ScientificSnapshot,
                tests,
                request.Manager.GetActualMaskUsagePercent());

            WriteAtomic(Path.Combine(directory, RunSummaryFileName), BuildRunSummaryCsv(summary));
            WriteAtomic(Path.Combine(directory, StateTimeSeriesFileName), BuildStateTimeSeriesCsv(request.ScientificSnapshot));
            WriteAtomic(Path.Combine(directory, TestEventsFileName), BuildTestEventsCsv(tests));
            WriteAtomic(Path.Combine(directory, InterventionEventsFileName), BuildInterventionEventsCsv(request.ScientificSnapshot));
            WriteAtomic(Path.Combine(directory, HealthcareTimeSeriesFileName), BuildHealthcareTimeSeriesCsv(healthcare, request.ScientificSnapshot.RunStartTime));
            WriteAtomic(Path.Combine(directory, PopulationEventsFileName), BuildPopulationEventsCsv(request.ScientificSnapshot));
            WriteAtomic(Path.Combine(directory, "contact_network_summary.csv"), request.ScientificSnapshot.ContactNetworkSummaryCsv);
            WriteAtomic(Path.Combine(directory, "contact_step_summary.csv"), BuildContactStepSummary(request.ScientificSnapshot.ContactNetworkSummaryCsv));
            WriteAtomic(Path.Combine(directory, "contact_episode_summary.csv"), request.ScientificSnapshot.ContactEpisodeSummaryCsv);
            WriteAtomic(Path.Combine(directory, "contact_episode_duration_distribution.csv"), request.ScientificSnapshot.ContactEpisodeDurationCsv);
            WriteAtomic(Path.Combine(directory, "age_mixing_matrix.csv"), request.ScientificSnapshot.AgeMixingCsv);
            WriteAtomic(Path.Combine(directory, "contact_degree_distribution.csv"), request.ScientificSnapshot.DegreeDistributionCsv);
            WriteAtomic(Path.Combine(directory, "contact_duration_distribution.csv"), request.ScientificSnapshot.ContactDurationCsv);
            WriteAtomic(Path.Combine(directory, "contacts_by_time_of_day.csv"), request.ScientificSnapshot.ContactTimeOfDayCsv);
            WriteAtomic(Path.Combine(directory, "calibration_results.csv"), BuildCalibrationResultsCsv(request.ScientificSnapshot, summary, request.Manager.CurrentRunContext?.CalibrationTargets));
            if (PandemicProfiler.Enabled) WriteAtomic(Path.Combine(directory, "performance_diagnostics.csv"), PandemicProfiler.SnapshotCsv());

            var errors = new ScientificRunErrorReport { Status = forcedFailure == null ? "Valid" : "Invalid" };
            ExperimentRunIntegrityFailure failure = forcedFailure;
            if (failure != null)
            {
                errors.Errors.Add(failure);
            }

            new AtomicJsonFileStore().Save(Path.Combine(directory, ErrorsFileName), errors);
        }

        internal static string BuildContactStepSummary(string legacyCsv) => legacyCsv
            .Replace("contact_events", "raw_contact_steps")
            .Replace("mean_contacts_per_person_day", "mean_contact_step_participations_per_person_day")
            .Replace("unique_contacts_per_person_day", "mean_unique_contacts_per_person_day")
            .Replace("repeated_contacts_per_person_day", "repeated_contact_step_participations_per_person_day")
            .Replace("context_contacts", "context_contact_steps");

        internal static ScientificRunSummary CalculateSummary(
            ExperimentRecorderSnapshot snapshot,
            IEnumerable<PandemicTestRecord> testRecords,
            double actualMaskUsagePercent)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            PandemicStateTimePoint final = snapshot.StateTimeSeries.Count > 0
                ? snapshot.StateTimeSeries[snapshot.StateTimeSeries.Count - 1]
                : new PandemicStateTimePoint();
            List<PandemicTestRecord> tests = (testRecords ?? new List<PandemicTestRecord>()).ToList();
            List<double> waits = tests
                .Where(record => record.SampleTakenAt.HasValue)
                .Select(record => Math.Max(0d, (record.SampleTakenAt.Value - record.RequestedAt).TotalDays))
                .OrderBy(value => value)
                .ToList();
            int resolved = final.Recovered + final.Dead;
            int active = final.Exposed + final.Infectious + final.PostInfectiousIll;
            int cumulativeInfections = final.InitialSeedCount + final.SecondaryTransmissionsTotal;
            int distinctInfectors = snapshot.TransmissionEvents
                .Select(item => item.SourceCitizenId)
                .Distinct()
                .Count();

            return new ScientificRunSummary
            {
                FinalState = final,
                InitialSeedCount = final.InitialSeedCount,
                SecondaryTransmissionsTotal = final.SecondaryTransmissionsTotal,
                CumulativeInfections = cumulativeInfections,
                ActiveExposed = final.Exposed,
                ActiveInfectious = final.Infectious,
                ActivePostInfectiousIll = final.PostInfectiousIll,
                ActiveSymptomatic = final.Symptomatic,
                RecoveredTotal = final.Recovered,
                DeathsTotal = final.Dead,
                HospitalizationsTotal = final.HospitalizationsTotal,
                TrackedPopulation = final.TrackedPopulation,
                FinalIncidencePer100000PerInterval = final.TrackedPopulation > 0
                    ? final.NewExposures * 100000d / final.TrackedPopulation
                    : 0d,
                FinalPrevalencePercent = final.TrackedPopulation > 0
                    ? active * 100d / final.TrackedPopulation
                    : 0d,
                AttackRatePercent = final.TrackedPopulation > 0
                    ? cumulativeInfections * 100d / final.TrackedPopulation
                    : 0d,
                ResolvedCaseFatalityRatioPercent = resolved > 0
                    ? (double?)(final.Dead * 100d / resolved)
                    : null,
                EmpiricalSecondaryInfectionsPerInfector = distinctInfectors > 0
                    ? (double?)(final.SecondaryTransmissionsTotal / (double)distinctInfectors)
                    : null,
                ActualMaskUsagePercent = actualMaskUsagePercent,
                TotalIsolationPersonDays = CalculatePersonDays(snapshot.InterventionEvents, PandemicInterventionType.Isolation, snapshot.RunEndTime),
                TotalQuarantinePersonDays = CalculatePersonDays(snapshot.InterventionEvents, PandemicInterventionType.Quarantine, snapshot.RunEndTime),
                TestsRequested = tests.Count,
                TestsPerformed = tests.Count(record => record.SampleTakenAt.HasValue),
                TestsPositive = tests.Count(record => record.State == PandemicTestState.ResultAvailable && record.Result == PandemicTestResult.Positive),
                TestsNegative = tests.Count(record => record.State == PandemicTestState.ResultAvailable && record.Result == PandemicTestResult.Negative),
                MeanTestWaitDays = waits.Count > 0 ? (double?)waits.Average() : null,
                MedianTestWaitDays = waits.Count > 0 ? (double?)Median(waits) : null,
                ContactsPreventedByIntervention = snapshot.ContactsPreventedByIntervention,
                PhysicalContactsTotal = snapshot.PhysicalContactsTotal,
                TraceableContactsTotal = snapshot.TraceableContactsTotal,
                HouseholdContactsTotal = snapshot.HouseholdContactsTotal,
                WorkContactsTotal = snapshot.WorkContactsTotal,
                SchoolContactsTotal = snapshot.SchoolContactsTotal,
                TransitContactsTotal = snapshot.TransitContactsTotal,
                AddedPopulation = snapshot.PopulationEvents
                    .Where(item => string.Equals(item.Action, "Added", StringComparison.Ordinal))
                    .Sum(item => item.Count),
                RemovedPopulation = snapshot.PopulationEvents
                    .Where(item => string.Equals(item.Action, "Removed", StringComparison.Ordinal))
                    .Sum(item => item.Count),
            };
        }

        internal static string BuildCalibrationResultsCsv(ExperimentRecorderSnapshot snapshot, ScientificRunSummary summary, CalibrationTargetSet targets)
        {
            var csv = new StringBuilder("target_set,source,metric,target_value,tolerance,weight,observed_value,absolute_error,is_available,within_tolerance,status,rt_definition,generation_interval_assumptions\n");
            if (targets == null)
            {
                AppendCsvRow(csv, "", "", "", "", "", "", "", "", 0, 0, "NotConfigured", "", "");
                return csv.ToString();
            }
            double? peak = null;
            double? timeToPeak = null;
            foreach (var point in snapshot.StateTimeSeries)
            {
                if (point.TrackedPopulation <= 0) continue;
                double prevalence = (point.Exposed + point.Infectious + point.PostInfectiousIll) / (double)point.TrackedPopulation;
                if (!peak.HasValue || prevalence > peak.Value)
                {
                    peak = prevalence;
                    timeToPeak = (point.SimulationTime - snapshot.RunStartTime).TotalDays;
                }
            }
            var observations = new Dictionary<CalibrationMetric, double?>
            {
                { CalibrationMetric.AttackRate, summary.TrackedPopulation > 0 ? (double?)(summary.AttackRatePercent / 100d) : null },
                { CalibrationMetric.PeakPrevalence, peak },
                { CalibrationMetric.TimeToPeakDays, timeToPeak },
                { CalibrationMetric.HospitalizationRate, summary.CumulativeInfections > 0 ? (double?)summary.HospitalizationsTotal / summary.CumulativeInfections : null },
                { CalibrationMetric.MortalityRate, summary.CumulativeInfections > 0 ? (double?)summary.DeathsTotal / summary.CumulativeInfections : null },
                { CalibrationMetric.Rt, null },
                { CalibrationMetric.HouseholdSecondaryAttackRate, null },
            };
            var evaluation = new CalibrationEngine().Evaluate(targets, observations);
            for (int i = 0; i < evaluation.Results.Count; i++)
            {
                var result = evaluation.Results[i];
                var target = targets.Targets[i];
                AppendCsvRow(csv, targets.Name, targets.Source, result.Metric, Number(target.TargetValue), Number(target.AbsoluteTolerance), Number(target.Weight),
                    NullableNumber(result.ObservedValue), NullableNumber(result.AbsoluteError), result.IsAvailable ? 1 : 0, result.IsWithinTolerance ? 1 : 0,
                    result.IsAvailable ? "Evaluated" : "Unavailable", targets.RtDefinition, targets.GenerationIntervalAssumptions);
            }
            return csv.ToString();
        }

        internal static string BuildRunSummaryCsv(ScientificRunSummary summary)
        {
            var csv = new StringBuilder();
            csv.AppendLine("initial_seed_count,secondary_transmissions_total,cumulative_infections,active_exposed,active_infectious,active_post_infectious_ill,active_symptomatic,recovered_total,deaths_total,hospitalizations_total,tracked_population,final_incidence_per_100000_per_interval,final_prevalence_pct,attack_rate_pct,resolved_case_fatality_ratio_pct,rt,rt_method,empirical_secondary_infections_per_infector,actual_mask_usage_pct,total_isolation_person_days,total_quarantine_person_days,tests_requested,tests_performed,tests_positive,tests_negative,mean_test_wait_days,median_test_wait_days,contacts_prevented_by_intervention,physical_contacts_total,traceable_contacts_total,household_contacts_total,work_contacts_total,school_contacts_total,transit_contacts_total,added_population,removed_population,true_new_infections,true_prevalence,true_active_infectious,detected_new_cases,detected_active_cases,observed_incidence,undetected_active_infections,case_detection_ratio,isolation_following_citizens,quarantine_following_citizens");
            AppendCsvRow(csv,
                summary.InitialSeedCount,
                summary.SecondaryTransmissionsTotal,
                summary.CumulativeInfections,
                summary.ActiveExposed,
                summary.ActiveInfectious,
                summary.ActivePostInfectiousIll,
                summary.ActiveSymptomatic,
                summary.RecoveredTotal,
                summary.DeathsTotal,
                summary.HospitalizationsTotal,
                summary.TrackedPopulation,
                Number(summary.FinalIncidencePer100000PerInterval),
                Number(summary.FinalPrevalencePercent),
                Number(summary.AttackRatePercent),
                NullableNumber(summary.ResolvedCaseFatalityRatioPercent),
                string.Empty,
                "unavailable_no_generation_interval_distribution",
                NullableNumber(summary.EmpiricalSecondaryInfectionsPerInfector),
                Number(summary.ActualMaskUsagePercent),
                Number(summary.TotalIsolationPersonDays),
                Number(summary.TotalQuarantinePersonDays),
                summary.TestsRequested,
                summary.TestsPerformed,
                summary.TestsPositive,
                summary.TestsNegative,
                NullableNumber(summary.MeanTestWaitDays),
                NullableNumber(summary.MedianTestWaitDays),
                summary.ContactsPreventedByIntervention,
                summary.PhysicalContactsTotal,
                summary.TraceableContactsTotal,
                summary.HouseholdContactsTotal,
                summary.WorkContactsTotal,
                summary.SchoolContactsTotal,
                summary.TransitContactsTotal,
                summary.AddedPopulation,
                summary.RemovedPopulation,
                summary.FinalState?.NewExposures ?? 0,
                Number(summary.FinalPrevalencePercent / 100d),
                summary.ActiveInfectious,
                summary.FinalState?.DetectedNewCases ?? 0,
                summary.FinalState?.DetectedActiveCases ?? 0,
                Number(summary.TrackedPopulation > 0 ? (summary.FinalState?.DetectedNewCases ?? 0) * 100000d / summary.TrackedPopulation : 0d),
                summary.FinalState?.UndetectedActiveInfections ?? 0,
                NullableNumber(summary.FinalState?.CaseDetectionRatio), summary.FinalState?.IsolationFollowingCitizens, summary.FinalState?.QuarantineFollowingCitizens);
            return csv.ToString();
        }

        internal static string BuildStateTimeSeriesCsv(ExperimentRecorderSnapshot snapshot)
        {
            var csv = new StringBuilder();
            csv.AppendLine("simulation_time,pandemic_day,susceptible,exposed,infectious,post_infectious_ill,symptomatic,recovered,dead,removed,tracked_population,initial_seed_count,secondary_transmissions_total,new_exposures_per_interval,hospitalizations_total,isolated_citizens,quarantined_citizens,incidence_per_100000_per_interval,prevalence_pct,attack_rate_pct,true_new_infections,true_prevalence,true_active_infectious,detected_new_cases,detected_active_cases,observed_incidence,undetected_active_infections,case_detection_ratio,isolation_following_citizens,quarantine_following_citizens");
            foreach (PandemicStateTimePoint point in snapshot.StateTimeSeries.OrderBy(item => item.SimulationTime))
            {
                int active = point.Exposed + point.Infectious + point.PostInfectiousIll;
                int cumulative = point.InitialSeedCount + point.SecondaryTransmissionsTotal;
                AppendCsvRow(csv,
                    Iso(point.SimulationTime),
                    PandemicDay(snapshot.RunStartTime, point.SimulationTime),
                    point.Susceptible,
                    point.Exposed,
                    point.Infectious,
                    point.PostInfectiousIll,
                    point.Symptomatic,
                    point.Recovered,
                    point.Dead,
                    point.Removed,
                    point.TrackedPopulation,
                    point.InitialSeedCount,
                    point.SecondaryTransmissionsTotal,
                    point.NewExposures,
                    point.HospitalizationsTotal,
                    point.IsolatedCitizens,
                    point.QuarantinedCitizens,
                    Number(point.TrackedPopulation > 0 ? point.NewExposures * 100000d / point.TrackedPopulation : 0d),
                    Number(point.TrackedPopulation > 0 ? active * 100d / point.TrackedPopulation : 0d),
                    Number(point.TrackedPopulation > 0 ? cumulative * 100d / point.TrackedPopulation : 0d),
                    point.NewExposures,
                    Number(point.TrackedPopulation > 0 ? active / (double)point.TrackedPopulation : 0d),
                    point.Infectious, point.DetectedNewCases, point.DetectedActiveCases,
                    Number(point.TrackedPopulation > 0 ? point.DetectedNewCases * 100000d / point.TrackedPopulation : 0d),
                    point.UndetectedActiveInfections, NullableNumber(point.CaseDetectionRatio), point.IsolationFollowingCitizens, point.QuarantineFollowingCitizens);
            }

            return csv.ToString();
        }

        internal static string BuildTestEventsCsv(IEnumerable<PandemicTestRecord> records)
        {
            var csv = new StringBuilder();
            csv.AppendLine("test_id,citizen_id,request_time,scheduled_time,sample_time,result_available_time,state,result,reason,priority,wait_duration_days,result_duration_days,citizen_disease_state_at_sample,infection_age_at_sample_days");
            foreach (PandemicTestRecord record in (records ?? new List<PandemicTestRecord>()).OrderBy(item => item.TestId))
            {
                double? wait = record.SampleTakenAt.HasValue
                    ? (double?)Math.Max(0d, (record.SampleTakenAt.Value - record.RequestedAt).TotalDays)
                    : null;
                double? resultDuration = record.SampleTakenAt.HasValue && record.ResultAvailableAt.HasValue
                    ? (double?)Math.Max(0d, (record.ResultAvailableAt.Value - record.SampleTakenAt.Value).TotalDays)
                    : null;
                AppendCsvRow(csv,
                    record.TestId,
                    record.CitizenId,
                    Iso(record.RequestedAt),
                    NullableIso(record.ScheduledAt),
                    NullableIso(record.SampleTakenAt),
                    NullableIso(record.ResultAvailableAt),
                    record.State,
                    record.Result,
                    record.Reason,
                    record.Priority,
                    NullableNumber(wait),
                    NullableNumber(resultDuration),
                    record.DiseaseStateAtSample,
                    NullableNumber(record.InfectionAgeDaysAtSample));
            }

            return csv.ToString();
        }

        internal static string BuildInterventionEventsCsv(ExperimentRecorderSnapshot snapshot)
        {
            var csv = new StringBuilder();
            csv.AppendLine("event_id,simulation_time,pandemic_day,citizen_id,intervention_type,action,reason,context,trigger_metric,trigger_value,trigger_threshold,actually_followed");
            foreach (PandemicInterventionEvent item in snapshot.InterventionEvents.OrderBy(value => value.EventId))
            {
                AppendCsvRow(csv,
                    item.EventId,
                    Iso(item.SimulationTime),
                    PandemicDay(snapshot.RunStartTime, item.SimulationTime),
                    item.CitizenId.HasValue ? item.CitizenId.Value.ToString(CultureInfo.InvariantCulture) : string.Empty,
                    item.InterventionType,
                    item.Action,
                    item.Reason,
                    item.Context, item.TriggerMetric, NullableNumber(item.TriggerValue), NullableNumber(item.TriggerThreshold), item.ActuallyFollowed.HasValue ? (item.ActuallyFollowed.Value ? "1" : "0") : "");
            }

            return csv.ToString();
        }

        internal static string BuildHealthcareTimeSeriesCsv(
            IEnumerable<PandemicHealthcareTimePoint> points,
            DateTime runStart)
        {
            var csv = new StringBuilder();
            csv.AppendLine("simulation_time,pandemic_day,hospital_usage_pct,ambulance_usage_pct");
            foreach (PandemicHealthcareTimePoint point in (points ?? new List<PandemicHealthcareTimePoint>()).OrderBy(item => item.SimulationTime))
            {
                AppendCsvRow(csv,
                    Iso(point.SimulationTime),
                    PandemicDay(runStart, point.SimulationTime),
                    Number(point.HospitalUsagePercent),
                    Number(point.AmbulanceUsagePercent));
            }

            return csv.ToString();
        }

        internal static string BuildPopulationEventsCsv(ExperimentRecorderSnapshot snapshot)
        {
            var csv = new StringBuilder();
            csv.AppendLine("event_id,simulation_time,pandemic_day,citizen_id,action,population_category,count,reason");
            foreach (PandemicPopulationEvent item in snapshot.PopulationEvents.OrderBy(value => value.EventId))
            {
                AppendCsvRow(csv,
                    item.EventId,
                    Iso(item.SimulationTime),
                    PandemicDay(snapshot.RunStartTime, item.SimulationTime),
                    item.CitizenId.HasValue ? item.CitizenId.Value.ToString(CultureInfo.InvariantCulture) : string.Empty,
                    item.Action,
                    item.PopulationCategory,
                    item.Count,
                    item.Reason);
            }

            return csv.ToString();
        }

        private static double CalculatePersonDays(
            IEnumerable<PandemicInterventionEvent> events,
            PandemicInterventionType type,
            DateTime runEnd)
        {
            var activeStarts = new Dictionary<uint, DateTime>();
            var activeCounts = new Dictionary<uint, int>();
            double totalDays = 0d;
            foreach (PandemicInterventionEvent item in events
                .Where(value => value.InterventionType == type && value.CitizenId.HasValue)
                .OrderBy(value => value.SimulationTime)
                .ThenBy(value => value.EventId))
            {
                uint citizenId = item.CitizenId.Value;
                if (string.Equals(item.Action, "Start", StringComparison.OrdinalIgnoreCase))
                {
                    int count = activeCounts.TryGetValue(citizenId, out int existingCount)
                        ? existingCount
                        : 0;
                    if (count == 0)
                    {
                        activeStarts[citizenId] = item.SimulationTime;
                    }

                    activeCounts[citizenId] = count + 1;
                }
                else if (string.Equals(item.Action, "End", StringComparison.OrdinalIgnoreCase)
                    && activeCounts.TryGetValue(citizenId, out int count)
                    && count > 0)
                {
                    count--;
                    if (count == 0)
                    {
                        if (activeStarts.TryGetValue(citizenId, out DateTime started))
                        {
                            totalDays += Math.Max(0d, (item.SimulationTime - started).TotalDays);
                        }

                        activeCounts.Remove(citizenId);
                        activeStarts.Remove(citizenId);
                    }
                    else
                    {
                        activeCounts[citizenId] = count;
                    }
                }
            }

            foreach (DateTime started in activeStarts.Values)
            {
                totalDays += Math.Max(0d, (runEnd - started).TotalDays);
            }

            return totalDays;
        }

        private static double Median(IList<double> sorted)
        {
            int middle = sorted.Count / 2;
            return sorted.Count % 2 == 0
                ? (sorted[middle - 1] + sorted[middle]) / 2d
                : sorted[middle];
        }

        private static void RequireStreamedFile(string directory, string name)
        {
            string path = Path.Combine(directory, name);
            if (!File.Exists(path) || File.Exists(path + ".tmp"))
            {
                throw new IOException("The streamed scientific output is incomplete: " + path);
            }
        }

        internal static void WriteAtomic(string path, string contents)
        {
            string fullPath = Path.GetFullPath(path);
            string temporaryPath = AtomicFileUtilities.CreateTemporarySiblingPath(fullPath);
            string backupPath = AtomicFileUtilities.CreateTemporarySiblingPath(fullPath);
            try
            {
                using (FileStream stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    writer.Write(contents ?? string.Empty);
                    writer.Flush();
                    stream.Flush();
                }

                if (File.Exists(fullPath))
                {
                    try
                    {
                        File.Replace(temporaryPath, fullPath, backupPath, true);
                    }
                    catch (PlatformNotSupportedException)
                    {
                        ReplaceWithRenameFallback(temporaryPath, fullPath, backupPath);
                    }
                    catch (NotSupportedException)
                    {
                        ReplaceWithRenameFallback(temporaryPath, fullPath, backupPath);
                    }
                }
                else
                {
                    File.Move(temporaryPath, fullPath);
                }

                if (!File.Exists(fullPath))
                {
                    throw new IOException("The scientific output could not be verified after writing: " + fullPath);
                }

                DeleteIfPresent(backupPath);
            }
            finally
            {
                DeleteIfPresent(temporaryPath);
                DeleteIfPresent(backupPath);
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
                File.Delete(path);
            }
        }

        private static int PandemicDay(DateTime runStart, DateTime simulationTime)
        {
            return runStart == default(DateTime)
                ? 0
                : Math.Max(0, (int)Math.Floor((simulationTime - runStart).TotalDays));
        }

        private static string Iso(DateTime value) => value.ToString("o", CultureInfo.InvariantCulture);

        private static string NullableIso(DateTime? value) => value.HasValue ? Iso(value.Value) : string.Empty;

        private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

        private static string NullableNumber(double? value) => value.HasValue ? Number(value.Value) : string.Empty;

        private static void AppendCsvRow(StringBuilder csv, params object[] values)
        {
            for (int i = 0; i < values.Length; ++i)
            {
                if (i > 0)
                {
                    csv.Append(',');
                }

                string value = Convert.ToString(values[i], CultureInfo.InvariantCulture) ?? string.Empty;
                if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0)
                {
                    csv.Append('"');
                    csv.Append(value.Replace("\"", "\"\""));
                    csv.Append('"');
                }
                else
                {
                    csv.Append(value);
                }
            }

            csv.AppendLine();
        }
    }
}
