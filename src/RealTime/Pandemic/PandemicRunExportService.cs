// <copyright file="PandemicRunExportService.cs" company="dymanoid">
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
    using RealTime.Config;
    using RealTime.Core;
    using RealTime.Experiments;

    /// <summary>Immutable input captured before any pandemic cleanup mutates the terminal state.</summary>
    internal sealed class PandemicRunExportRequest
    {
        public PandemicRunExportRequest(
            PandemicManager manager,
            PandemicLiveSnapshot snapshot,
            PandemicOutputContext output,
            DateTime wallClockStart,
            DateTime wallClockEnd)
        {
            Manager = manager ?? throw new ArgumentNullException(nameof(manager));
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            Output = output ?? throw new ArgumentNullException(nameof(output));
            WallClockStart = wallClockStart;
            WallClockEnd = wallClockEnd;
        }

        public PandemicManager Manager { get; }

        public PandemicLiveSnapshot Snapshot { get; }

        public PandemicOutputContext Output { get; }

        public DateTime WallClockStart { get; }

        public DateTime WallClockEnd { get; }
    }

    /// <summary>Verified output paths produced by one export operation.</summary>
    internal sealed class PandemicRunExportResult
    {
        public PandemicRunExportResult(string richCsvPath, string observerCsvPath, string contactsCsvPath)
        {
            RichCsvPath = richCsvPath;
            ObserverCsvPath = observerCsvPath;
            ContactsCsvPath = contactsCsvPath;
        }

        public string RichCsvPath { get; }

        public string ObserverCsvPath { get; }

        public string ContactsCsvPath { get; }
    }

    /// <summary>Single manual and batch path for rich, raw-observer, and contact exports.</summary>
    internal sealed class PandemicRunExportService
    {
        public static PandemicRunExportService Instance { get; } = new PandemicRunExportService();

        public PandemicRunExportResult Export(PandemicRunExportRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            PandemicRunContext context = request.Manager.CurrentRunContext;
            if (context != null && context.IsBatch && !request.Manager.IsAwaitingBatchFinalization)
            {
                throw new InvalidOperationException("A batch run must be frozen before its terminal data can be exported.");
            }

            if (context != null && context.IsBatch && !ReferenceEquals(request.Output, context.Output))
            {
                throw new InvalidOperationException("The export output context does not own the active batch run.");
            }

            string richPath = ResolveRichCsvPath(request.Output.RichCsvPath, request.WallClockEnd);
            ValidateRequiredPaths(context, richPath, request.Output.ObserverCsvPath, request.Output.ContactsCsvPath);
            EnsureDestinationsAvailable(
                request.Output.AllowReplaceExisting,
                richPath,
                request.Output.ObserverCsvPath,
                request.Output.ContactsCsvPath);

            string richCsv = BuildRichCsv(request);
            string observerCsv = request.Manager.BuildObserverCsv();
            string contactsCsv = request.Manager.BuildContactsCsv();

            WriteAllText(richPath, richCsv, request.Output.AllowReplaceExisting);
            WriteAllText(request.Output.ObserverCsvPath, observerCsv, request.Output.AllowReplaceExisting);
            WriteAllText(request.Output.ContactsCsvPath, contactsCsv, request.Output.AllowReplaceExisting);

            return new PandemicRunExportResult(richPath, request.Output.ObserverCsvPath, request.Output.ContactsCsvPath);
        }

        internal static string BuildRichCsv(PandemicRunExportRequest request)
        {
            var csv = new StringBuilder();
            PandemicManager manager = request.Manager;
            PandemicLiveSnapshot snapshot = request.Snapshot;
            DateTime gameStart = manager.GetPandemicRunStartedAt();

            AppendRunMetadata(csv, request, gameStart);
            AppendCoreMetrics(csv, snapshot);
            AppendPolicyState(csv, manager);
            AppendBreakdowns(csv, snapshot);

            IList<PandemicObservation> observations = manager.GetAllObservations();
            List<PandemicObservation> sorted = observations != null && observations.Count > 0
                ? observations.OrderBy(observation => observation.SimulationTime).ToList()
                : new List<PandemicObservation>();
            AppendSeirdSeries(csv, sorted, gameStart);
            AppendPolicyTimeline(csv, snapshot, gameStart);
            AppendHealthcareSeries(csv, manager, gameStart);
            AppendPeakStatistics(csv, sorted, gameStart);
            AppendSettings(csv, manager.RuntimeConfig);
            AppendOriginSeries(csv, observations, gameStart);
            AppendLocationSeries(csv, observations, gameStart);
            return csv.ToString();
        }

        private static void AppendRunMetadata(StringBuilder csv, PandemicRunExportRequest request, DateTime gameStart)
        {
            DateTime gameEnd = request.Snapshot.SimulationTime;
            TimeSpan elapsed = request.WallClockStart == default(DateTime)
                ? TimeSpan.Zero
                : request.WallClockEnd - request.WallClockStart;
            csv.AppendLine("[RUN METADATA]");
            csv.AppendLine("start_wall_time,end_wall_time,elapsed_wall_seconds,game_start_time,game_end_time,pandemic_day,lifecycle_state");
            csv.AppendFormat(
                CultureInfo.InvariantCulture,
                "{0},{1},{2},{3},{4},{5},{6}",
                request.WallClockStart == default(DateTime) ? "unknown" : request.WallClockStart.ToString("o", CultureInfo.InvariantCulture),
                request.WallClockEnd.ToString("o", CultureInfo.InvariantCulture),
                Math.Round(elapsed.TotalSeconds, 1).ToString(CultureInfo.InvariantCulture),
                gameStart == default(DateTime) ? "unknown" : gameStart.ToString("o", CultureInfo.InvariantCulture),
                gameEnd == default(DateTime) ? "unknown" : gameEnd.ToString("o", CultureInfo.InvariantCulture),
                request.Snapshot.PandemicDay,
                request.Snapshot.LifecycleState);
            csv.AppendLine();
            csv.AppendLine();

            AppendBatchMetadata(csv, request.Manager.CurrentRunContext, request.Manager.CompletionReason);
        }

        private static void AppendBatchMetadata(
            StringBuilder csv,
            PandemicRunContext context,
            PandemicCompletionReason completionReason)
        {
            if (context == null || !context.IsBatch)
            {
                return;
            }

            PandemicBatchExportMetadata metadata = context.BatchMetadata;
            PandemicComponentSeeds seeds = context.ComponentSeeds;
            csv.AppendLine("[BATCH METADATA]");
            csv.AppendLine("batch_id,batch_name,scenario_id,scenario_name,scenario_index,run_number,overall_run_number,master_seed,duration_days,completion_policy,completion_reason,pandemic_seed,mask_seed,test_seed,contact_seed");
            csv.AppendFormat(
                CultureInfo.InvariantCulture,
                "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14}",
                CsvEscape(metadata?.BatchId),
                CsvEscape(metadata?.BatchName),
                CsvEscape(metadata?.ScenarioId),
                CsvEscape(metadata?.ScenarioName),
                metadata?.ScenarioIndex ?? 0,
                metadata?.RunNumber ?? 0,
                metadata?.OverallRunNumber ?? 0,
                seeds.MasterSeed,
                context.Policy.DurationDays.ToString("0.########", CultureInfo.InvariantCulture),
                context.Policy.StopOnExtinction ? "DurationOrExtinction" : "FixedDuration",
                completionReason,
                seeds.PandemicManagerSeed,
                seeds.MaskManagerSeed,
                seeds.TestManagerSeed,
                seeds.ContactManagerSeed);
            csv.AppendLine();
            csv.AppendLine();
        }

        private static void AppendCoreMetrics(StringBuilder csv, PandemicLiveSnapshot snapshot)
        {
            csv.AppendLine("[CORE METRICS]");
            csv.AppendLine("tracked_population,healthy,exposed,sick,recovered,dead,delta_sick,delta_recovered,delta_dead,quarantine_citizens,positive_tests,tested_citizens,contacts_tracked_citizens,contacts_tracked_pairs,contacts_recorded_total,transmissions_total,transmissions_indoor,transmissions_outdoor,transmissions_vehicle,hotspot_buildings,hub_buildings,hospital_usage_pct,ambulance_usage_pct,observation_count");
            csv.AppendFormat(
                CultureInfo.InvariantCulture,
                "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20},{21},{22},{23}",
                snapshot.TrackedPopulation,
                snapshot.Healthy,
                snapshot.Exposed,
                snapshot.Sick,
                snapshot.Recovered,
                snapshot.Dead,
                snapshot.DeltaSick,
                snapshot.DeltaRecovered,
                snapshot.DeltaDead,
                snapshot.QuarantineCitizens,
                snapshot.PositiveTests,
                snapshot.TestedCitizens,
                snapshot.ContactsTrackedCitizens,
                snapshot.ContactsTrackedPairs,
                snapshot.ContactsRecordedTotal,
                snapshot.TransmissionsTotal,
                snapshot.TransmissionsIndoor,
                snapshot.TransmissionsOutdoor,
                snapshot.TransmissionsVehicle,
                snapshot.HotspotBuildings,
                snapshot.HubBuildings,
                snapshot.HospitalUsagePercent.ToString("F2", CultureInfo.InvariantCulture),
                snapshot.AmbulanceUsagePercent.ToString("F2", CultureInfo.InvariantCulture),
                snapshot.ObservationCount);
            csv.AppendLine();
            csv.AppendLine();
        }

        private static void AppendPolicyState(StringBuilder csv, PandemicManager manager)
        {
            csv.AppendLine("[POLICY STATE]");
            csv.AppendLine("policy,enabled");
            csv.AppendLine("Masks," + (manager.IsMasksEnabled() ? "1" : "0"));
            csv.AppendLine("Quarantine," + (manager.IsQuarantineEnabled() ? "1" : "0"));
            csv.AppendLine("Lockdown," + (manager.IsLockdownEnabled() ? "1" : "0"));
            csv.AppendLine("WorldOverlays," + (manager.AreWorldOverlaysEnabled() ? "1" : "0"));
            csv.AppendLine();
        }

        private static void AppendBreakdowns(StringBuilder csv, PandemicLiveSnapshot snapshot)
        {
            csv.AppendLine("[AGE GROUPS]");
            csv.AppendLine("age_group,infected_count,infected_percent");
            foreach (PandemicAgeGroupSnapshot age in snapshot.AgeGroups)
            {
                csv.AppendFormat(CultureInfo.InvariantCulture, "{0},{1},{2}", CsvEscape(age.Label), age.InfectedCount, age.InfectedPercent.ToString("F2", CultureInfo.InvariantCulture));
                csv.AppendLine();
            }

            csv.AppendLine();
            csv.AppendLine("[LOCKDOWN FAMILIES]");
            csv.AppendLine("family,is_closed,infected_pct,threshold_pct,manual_closed");
            foreach (PandemicLockdownFamilySnapshot family in snapshot.LockdownFamilies)
            {
                csv.AppendFormat(
                    CultureInfo.InvariantCulture,
                    "{0},{1},{2},{3},{4}",
                    CsvEscape(family.Label),
                    family.IsClosed ? "1" : "0",
                    family.CurrentInfectedPercent.ToString("F2", CultureInfo.InvariantCulture),
                    family.AutoCloseThresholdPercent.ToString("F2", CultureInfo.InvariantCulture),
                    family.ManualClosed ? "1" : "0");
                csv.AppendLine();
            }

            csv.AppendLine();
            csv.AppendLine("[INFECTION ORIGINS]");
            csv.AppendLine("origin,count,percent");
            foreach (PandemicOriginSnapshot origin in snapshot.Origins.OrderByDescending(item => item.Count))
            {
                csv.AppendFormat(CultureInfo.InvariantCulture, "{0},{1},{2}", CsvEscape(origin.Label), origin.Count, origin.Percent.ToString("F2", CultureInfo.InvariantCulture));
                csv.AppendLine();
            }

            csv.AppendLine();
            csv.AppendLine("[DISTRICT INFECTION RATES]");
            csv.AppendLine("district_name,district_id,infected_residents,resident_count,infected_percent");
            foreach (PandemicDistrictSnapshot district in snapshot.Districts)
            {
                csv.AppendFormat(
                    CultureInfo.InvariantCulture,
                    "{0},{1},{2},{3},{4}",
                    CsvEscape(district.DistrictName),
                    district.DistrictId,
                    district.InfectedResidents,
                    district.ResidentCount,
                    district.InfectedPercent.ToString("F2", CultureInfo.InvariantCulture));
                csv.AppendLine();
            }

            csv.AppendLine();
            csv.AppendLine("[TOP SPREADERS]");
            csv.AppendLine("rank,label,infection_count,is_superspreader");
            for (int i = 0; i < snapshot.TopSpreaders.Count; i++)
            {
                PandemicSuperspreaderCitizenSnapshot spreader = snapshot.TopSpreaders[i];
                csv.AppendFormat(CultureInfo.InvariantCulture, "{0},{1},{2},{3}", i + 1, CsvEscape(spreader.Label), spreader.InfectionCount, spreader.IsSuperspreader ? "1" : "0");
                csv.AppendLine();
            }

            csv.AppendLine();
            csv.AppendLine("[TOP ORIGIN LOCATIONS]");
            csv.AppendLine("rank,label,infection_count,is_superspreader");
            for (int i = 0; i < snapshot.TopOriginLocations.Count; i++)
            {
                PandemicSuperspreaderLocationSnapshot location = snapshot.TopOriginLocations[i];
                csv.AppendFormat(CultureInfo.InvariantCulture, "{0},{1},{2},{3}", i + 1, CsvEscape(location.Label), location.InfectionCount, location.IsSuperspreader ? "1" : "0");
                csv.AppendLine();
            }

            csv.AppendLine();
        }

        private static void AppendSeirdSeries(StringBuilder csv, IEnumerable<PandemicObservation> observations, DateTime gameStart)
        {
            csv.AppendLine("[SEIRD TIME SERIES]");
            csv.AppendLine("sim_time,pandemic_day,healthy,exposed,sick,recovered,dead,total,delta_sick,delta_dead");
            int previousSick = 0;
            int previousDead = 0;
            foreach (PandemicObservation observation in observations)
            {
                int sick = (int)observation.SickCitizens;
                int dead = (int)observation.DeadCitizens;
                int exposed = (int)observation.ExposedCitizens;
                int total = (int)(observation.HealthyCitizens + observation.ExposedCitizens + observation.SickCitizens + observation.RecoveredCitizens + observation.DeadCitizens);
                csv.AppendFormat(
                    CultureInfo.InvariantCulture,
                    "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9}",
                    observation.SimulationTime.ToString("o", CultureInfo.InvariantCulture),
                    PandemicDay(gameStart, observation.SimulationTime),
                    observation.HealthyCitizens,
                    exposed,
                    sick,
                    observation.RecoveredCitizens,
                    dead,
                    total,
                    sick - previousSick,
                    dead - previousDead);
                csv.AppendLine();
                previousSick = sick;
                previousDead = dead;
            }

            csv.AppendLine();
        }

        private static void AppendPolicyTimeline(StringBuilder csv, PandemicLiveSnapshot snapshot, DateTime gameStart)
        {
            csv.AppendLine("[POLICY TIMELINE]");
            csv.AppendLine("sim_time,pandemic_day,policy_type,enabled,short_label");
            foreach (PandemicPolicyMarkerSnapshot marker in snapshot.PolicyMarkers)
            {
                csv.AppendFormat(
                    CultureInfo.InvariantCulture,
                    "{0},{1},{2},{3},{4}",
                    marker.SimulationTime.ToString("o", CultureInfo.InvariantCulture),
                    PandemicDay(gameStart, marker.SimulationTime),
                    marker.Type,
                    marker.Enabled ? "1" : "0",
                    CsvEscape(marker.ShortLabel ?? marker.Type.ToString()));
                csv.AppendLine();
            }

            csv.AppendLine();
        }

        private static void AppendHealthcareSeries(StringBuilder csv, PandemicManager manager, DateTime gameStart)
        {
            csv.AppendLine("[HEALTHCARE TIME SERIES]");
            csv.AppendLine("sim_time,pandemic_day,hospital_usage_pct,ambulance_usage_pct");
            IList<PandemicHealthcareTimePoint> series = manager.GetHealthcareTimeSeries();
            foreach (PandemicHealthcareTimePoint point in series.OrderBy(item => item.SimulationTime))
            {
                csv.AppendFormat(
                    CultureInfo.InvariantCulture,
                    "{0},{1},{2},{3}",
                    point.SimulationTime.ToString("o", CultureInfo.InvariantCulture),
                    PandemicDay(gameStart, point.SimulationTime),
                    point.HospitalUsagePercent.ToString("F2", CultureInfo.InvariantCulture),
                    point.AmbulanceUsagePercent.ToString("F2", CultureInfo.InvariantCulture));
                csv.AppendLine();
            }

            csv.AppendLine();
        }

        private static void AppendPeakStatistics(StringBuilder csv, IList<PandemicObservation> observations, DateTime gameStart)
        {
            csv.AppendLine("[PEAK STATISTICS]");
            csv.AppendLine("peak_sick_count,peak_sick_day,final_exposed,final_sick,final_recovered,final_dead,total_tracked,attack_rate_pct,case_fatality_rate_pct");
            if (observations.Count > 0)
            {
                PandemicObservation peak = observations.OrderByDescending(observation => observation.SickCitizens).First();
                PandemicObservation final = observations[observations.Count - 1];
                int exposed = (int)final.ExposedCitizens;
                int sick = (int)final.SickCitizens;
                int recovered = (int)final.RecoveredCitizens;
                int dead = (int)final.DeadCitizens;
                int total = exposed + sick + recovered + dead + (int)final.HealthyCitizens;
                int infected = exposed + sick + recovered + dead;
                float attackRate = total > 0 ? (float)infected / total * 100f : 0f;
                float fatalityRate = recovered + dead > 0 ? (float)dead / (recovered + dead) * 100f : 0f;
                csv.AppendFormat(
                    CultureInfo.InvariantCulture,
                    "{0},{1},{2},{3},{4},{5},{6},{7},{8}",
                    peak.SickCitizens,
                    PandemicDay(gameStart, peak.SimulationTime),
                    exposed,
                    sick,
                    recovered,
                    dead,
                    total,
                    attackRate.ToString("F2", CultureInfo.InvariantCulture),
                    fatalityRate.ToString("F2", CultureInfo.InvariantCulture));
                csv.AppendLine();
            }

            csv.AppendLine();
        }

        private static void AppendSettings(StringBuilder csv, RealTimeConfig config)
        {
            csv.AppendLine("[PANDEMIC SETTINGS]");
            csv.AppendLine("parameter,value");
            if (config != null)
            {
                csv.AppendLine("DiseaseDuration," + config.DiseaseDuration);
                csv.AppendLine("DetectionTime," + config.DetectionTime);
                csv.AppendLine("StartSymptoms," + config.StartSymptoms);
                csv.AppendLine("EndSymptoms," + config.EndSymptoms);
                csv.AppendLine("StartInfection," + config.StartInfection);
                csv.AppendLine("EndInfection," + config.EndInfection);
                AppendSetting(csv, "IndoorTransmissionProbability", config.IndoorDiseaseTransmissionProbability);
                AppendSetting(csv, "OutdoorTransmissionProbability", config.OutdoorDiseaseTransmissionProbability);
                AppendSetting(csv, "TransmissionRange", config.DiseaseTransmissionRange);
                AppendSetting(csv, "InitialInfectionRatio", config.DiseaseStartInfectionRatio);
                AppendSetting(csv, "DeathRateChild", config.DeathChild);
                AppendSetting(csv, "DeathRateTeen", config.DeathTeen);
                AppendSetting(csv, "DeathRateYoung", config.DeathYoung);
                AppendSetting(csv, "DeathRateAdult", config.DeathAdult);
                AppendSetting(csv, "DeathRateSenior", config.DeathSenior);
                AppendSetting(csv, "SymptomProbability", config.SymptomProbability);
                csv.AppendLine("TransmissionProbabilityReduction," + config.TransmissionProbabilityReduction);
                csv.AppendLine("RatioIgnoreMasks," + config.RatioIgnoreMasks);
                csv.AppendLine("RatioOtherProtectionMask," + config.RatioOtherProtectionMask);
                csv.AppendLine("RatioOwnProtectionMask," + config.RatioOwnProtectionMask);
                csv.AppendLine("MaskBehavior," + config.MaskBehavior);
                AppendSetting(csv, "BuildingContactTracingProbability", config.BuildingContactTracingProbability);
                AppendSetting(csv, "AppBasedContactTracingProbability", config.AppBasedContactTracingProbability);
                AppendSetting(csv, "RelativeTestCapacity", config.RelativeTestCapacity);
                AppendSetting(csv, "PercentOfTestsForSick", config.PercentageOfTestsReservedForSickCitizens);
                csv.AppendLine("MaximumTestDuration," + config.MaximumTestDuration);
                csv.AppendLine("MinimumTestDuration," + config.MinimumTestDuration);
                csv.AppendLine("QuarantineBehavior," + config.QuarantineBehavior);
                csv.AppendLine("OnlyTestedCitizensToQuarantine," + (config.OnlyTestedCitizensToQuarantine ? "1" : "0"));
                csv.AppendLine("LockdownBehavior," + config.LockdownBehavior);
                csv.AppendLine("HubHighlightThreshold," + config.HubHighlightThreshold);
                csv.AppendLine("SuperspreaderCitizenThreshold," + config.SuperspreaderCitizenThreshold);
                csv.AppendLine("SuperspreaderLocationThreshold," + config.SuperspreaderLocationThreshold);
            }

            csv.AppendLine();
        }

        private static void AppendOriginSeries(StringBuilder csv, IEnumerable<PandemicObservation> observations, DateTime gameStart)
        {
            csv.AppendLine("[INFECTION ORIGINS TIME SERIES]");
            csv.AppendLine("sim_time,pandemic_day,home,work,school,healthcare,commercial,transit,outdoor,other");
            if (observations != null)
            {
                foreach (PandemicObservation observation in observations.OrderBy(item => item.SimulationTime))
                {
                    int home = 0;
                    int work = 0;
                    int school = 0;
                    int healthcare = 0;
                    int commercial = 0;
                    int transit = 0;
                    int outdoor = 0;
                    int other = 0;
                    foreach (List<Infection> infections in observation.Infections.Values)
                    {
                        foreach (Infection infection in infections)
                        {
                            switch (infection.OriginCategory)
                            {
                                case PandemicInfectionOriginCategory.ResidentialHome:
                                    home++;
                                    break;
                                case PandemicInfectionOriginCategory.WorkplaceOfficeIndustry:
                                    work++;
                                    break;
                                case PandemicInfectionOriginCategory.SchoolUniversity:
                                    school++;
                                    break;
                                case PandemicInfectionOriginCategory.Healthcare:
                                    healthcare++;
                                    break;
                                case PandemicInfectionOriginCategory.CommercialLeisureTourism:
                                    commercial++;
                                    break;
                                case PandemicInfectionOriginCategory.OutdoorStreet:
                                    outdoor++;
                                    break;
                                case PandemicInfectionOriginCategory.Bus:
                                case PandemicInfectionOriginCategory.Tram:
                                case PandemicInfectionOriginCategory.Metro:
                                case PandemicInfectionOriginCategory.Train:
                                case PandemicInfectionOriginCategory.ShipFerry:
                                case PandemicInfectionOriginCategory.Plane:
                                case PandemicInfectionOriginCategory.Taxi:
                                case PandemicInfectionOriginCategory.CarOtherVehicle:
                                case PandemicInfectionOriginCategory.StopPlatform:
                                    transit++;
                                    break;
                                default:
                                    other++;
                                    break;
                            }
                        }
                    }

                    csv.AppendFormat(
                        CultureInfo.InvariantCulture,
                        "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9}",
                        observation.SimulationTime.ToString("o", CultureInfo.InvariantCulture),
                        PandemicDay(gameStart, observation.SimulationTime),
                        home,
                        work,
                        school,
                        healthcare,
                        commercial,
                        transit,
                        outdoor,
                        other);
                    csv.AppendLine();
                }
            }

            csv.AppendLine();
        }

        private static void AppendLocationSeries(StringBuilder csv, IEnumerable<PandemicObservation> observations, DateTime gameStart)
        {
            csv.AppendLine("[CITIZEN LOCATIONS TIME SERIES]");
            csv.AppendLine("sim_time,pandemic_day,game_hour,at_home,at_work,visiting,in_transit,on_foot");
            if (observations != null)
            {
                foreach (PandemicObservation observation in observations.OrderBy(item => item.SimulationTime))
                {
                    csv.AppendFormat(
                        CultureInfo.InvariantCulture,
                        "{0},{1},{2},{3},{4},{5},{6},{7}",
                        observation.SimulationTime.ToString("o", CultureInfo.InvariantCulture),
                        PandemicDay(gameStart, observation.SimulationTime),
                        observation.SimulationTime.Hour,
                        observation.CitizensAtHome,
                        observation.CitizensAtWork,
                        observation.CitizensVisiting,
                        observation.CitizensInTransit,
                        observation.CitizensOnFoot);
                    csv.AppendLine();
                }
            }

            csv.AppendLine();
        }

        private static int PandemicDay(DateTime gameStart, DateTime simulationTime)
        {
            return gameStart == default(DateTime)
                ? 0
                : Math.Max(1, (int)Math.Floor((simulationTime - gameStart).TotalDays) + 1);
        }

        private static void AppendSetting(StringBuilder csv, string name, float value)
        {
            csv.AppendFormat(CultureInfo.InvariantCulture, "{0},{1}" + Environment.NewLine, name, value.ToString("F2", CultureInfo.InvariantCulture));
        }

        private static string CsvEscape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            if (value.IndexOf(',') >= 0 || value.IndexOf('"') >= 0 || value.IndexOf('\n') >= 0)
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }

            return value;
        }

        private static string ResolveRichCsvPath(string explicitPath, DateTime wallClockEnd)
        {
            if (!string.IsNullOrEmpty(explicitPath))
            {
                return explicitPath;
            }

            string modRoot = ModPaths.GetModRoot();
            if (string.IsNullOrEmpty(modRoot))
            {
                throw new InvalidOperationException("The mod root path could not be resolved.");
            }

            string directory = Path.Combine(modRoot, "Pandemic Data");
            string timestamp = wallClockEnd.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
            return Path.Combine(directory, "pandemic_run_" + timestamp + ".csv");
        }

        private static void ValidateRequiredPaths(PandemicRunContext context, string richPath, string observerPath, string contactsPath)
        {
            if (string.IsNullOrEmpty(richPath))
            {
                throw new InvalidOperationException("A rich pandemic CSV destination is required.");
            }

            if (context != null && context.IsBatch && (string.IsNullOrEmpty(observerPath) || string.IsNullOrEmpty(contactsPath)))
            {
                throw new InvalidOperationException("Batch exports require explicit rich, observer, and contact CSV destinations.");
            }

            var destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string[] paths = { richPath, observerPath, contactsPath };
            foreach (string path in paths)
            {
                if (!string.IsNullOrEmpty(path) && !destinations.Add(Path.GetFullPath(path)))
                {
                    throw new InvalidOperationException("Pandemic export destinations must be distinct.");
                }
            }
        }

        private static void EnsureDestinationsAvailable(bool allowReplaceExisting, params string[] paths)
        {
            if (allowReplaceExisting)
            {
                return;
            }

            foreach (string path in paths)
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    throw new IOException("Refusing to overwrite an existing completed-run export: " + path);
                }
            }
        }

        private static void WriteAllText(string path, string contents, bool allowReplaceExisting)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (!allowReplaceExisting && File.Exists(path))
            {
                throw new IOException("Refusing to overwrite an existing completed-run export: " + path);
            }

            string temporaryPath = AtomicFileUtilities.CreateTemporarySiblingPath(path);
            string backupPath = path + ".bak";
            try
            {
                using (FileStream stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    writer.Write(contents ?? string.Empty);
                    writer.Flush();
                    stream.Flush();
                }

                if (File.Exists(path))
                {
                    Replace(temporaryPath, path, backupPath);
                }
                else
                {
                    File.Move(temporaryPath, path);
                }

                if (!File.Exists(path))
                {
                    throw new IOException("The export could not be verified after writing: " + path);
                }

                DeleteIfPresent(backupPath);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
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
            if (!File.Exists(path))
            {
                return;
            }

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
