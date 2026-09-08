// Frozen pre-episode recorder for measured baseline comparisons; production code must not use this.
namespace RealTimeTests.Reference
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using RealTime.Pandemic;
    internal sealed class LegacyExperimentRecorder : IDisposable
    {
        private static readonly char[] CsvEscapeCharacters = { ',', '"', '\r', '\n' };
        private readonly ContactCsvWriter contactCsvWriter = new ContactCsvWriter();
        private DateTime cachedContactStart;
        private DateTime cachedContactEnd;
        private string cachedContactStartIso;
        private string cachedContactEndIso;
        private LegacyContactNetworkMetrics network;
        private readonly Dictionary<byte, int> districtTransmissions = new Dictionary<byte, int>();
        internal int GetDistrictTransmissions(byte district) => districtTransmissions.TryGetValue(district, out int count) ? count : 0;
        internal IList<PandemicStateTimePoint> StateTimeSeries => stateTimeSeries;
        internal IList<PandemicTransmissionEvent> TransmissionEvents => transmissionEvents;
        internal PandemicStateTimePoint LatestState => stateTimeSeries.Count == 0 ? null : stateTimeSeries[stateTimeSeries.Count - 1];
        public const string TransmissionEventsFileName = "transmission_events.csv";
        public const string PhysicalContactsFileName = "physical_contacts.csv";
        public const string TraceableContactsFileName = "traceable_contacts.csv";

        private readonly List<PandemicStateTimePoint> stateTimeSeries = new List<PandemicStateTimePoint>();
        private readonly List<PandemicTransmissionEvent> transmissionEvents = new List<PandemicTransmissionEvent>();
        private readonly List<PandemicInterventionEvent> interventionEvents = new List<PandemicInterventionEvent>();
        private readonly List<PandemicPopulationEvent> populationEvents = new List<PandemicPopulationEvent>();
        private StreamWriter transmissionWriter;
        private StreamWriter physicalContactWriter;
        private StreamWriter traceableContactWriter;
        private string outputDirectory;
        private long nextTransmissionEventId;
        private long nextInterventionEventId;
        private long nextPopulationEventId;
        private int lastSecondaryTransmissionCount;
        private int lastDetectedCasesTotal;
        private int pendingStreamRows;
        private bool frozen;
        private long physicalContactsTotal;
        private long traceableContactsTotal;
        private long householdContactsTotal;
        private long workContactsTotal;
        private long schoolContactsTotal;
        private long transitContactsTotal;
        private long contactsPreventedByIntervention;

        public DateTime RunStartTime { get; private set; }

        public void BeginRun(DateTime startTime, string batchOutputDirectory)
        {
            Reset();
            if (startTime == default(DateTime))
            {
                throw new ArgumentException("A scientific run requires a simulation start time.", nameof(startTime));
            }

            RunStartTime = startTime;
            network = new LegacyContactNetworkMetrics();
            network.Begin(startTime);
            outputDirectory = string.IsNullOrEmpty(batchOutputDirectory)
                ? null
                : Path.GetFullPath(batchOutputDirectory);
            nextTransmissionEventId = 1L;
            nextInterventionEventId = 1L;
            nextPopulationEventId = 1L;
            if (outputDirectory == null)
            {
                return;
            }

            Directory.CreateDirectory(outputDirectory);
            transmissionWriter = CreateStreamingWriter(
                TransmissionEventsFileName,
                "event_id,simulation_time,pandemic_day,source_citizen_id,target_citizen_id,source_infection_age_days,target_previous_state,target_new_state,context,origin_category,building_id,vehicle_id,district_id,source_mask_type,target_mask_type,transmission_probability,source_probability,infectiousness_multiplier,is_initial_seed,position_x,position_y,position_z,has_position");
            physicalContactWriter = CreateStreamingWriter(
                PhysicalContactsFileName,
                "contact_id,start_time,end_time,duration_minutes,citizen_a,citizen_b,context,building_id,vehicle_id,district_id,position_x,position_y,position_z,distance,traceable_by_app,traceable_by_manual");
            traceableContactWriter = CreateStreamingWriter(
                TraceableContactsFileName,
                "contact_id,start_time,end_time,duration_minutes,citizen_a,citizen_b,context,building_id,vehicle_id,district_id,traceable_by_app,traceable_by_manual");
        }

        public void RecordState(
            DateTime simulationTime,
            DiseaseStateCounts counts,
            int symptomatic,
            int initialSeedCount,
            int secondaryTransmissionCount,
            int hospitalizationsTotal,
            int isolatedCitizens,
            int quarantinedCitizens,
            EpidemicMetricsSnapshot surveillance = null,
            int? isolationFollowingCitizens = null,
            int? quarantineFollowingCitizens = null)
        {
            EnsureMutable();
            if (counts == null)
            {
                throw new ArgumentNullException(nameof(counts));
            }

            EnsureRunTime(simulationTime);
            if (stateTimeSeries.Count > 0
                && simulationTime < stateTimeSeries[stateTimeSeries.Count - 1].SimulationTime)
            {
                throw new InvalidOperationException("Scientific state time points must be monotonic.");
            }
            if (counts.Susceptible < 0
                || counts.Exposed < 0
                || counts.Infectious < 0
                || counts.PostInfectiousIll < 0
                || counts.Recovered < 0
                || counts.Dead < 0
                || counts.Removed < 0
                || symptomatic < 0
                || initialSeedCount < 0
                || secondaryTransmissionCount < 0
                || hospitalizationsTotal < 0
                || isolatedCitizens < 0
                || quarantinedCitizens < 0)
            {
                throw new InvalidOperationException("Scientific state counters cannot be negative.");
            }

            if (symptomatic > counts.Exposed + counts.Infectious + counts.PostInfectiousIll)
            {
                throw new InvalidOperationException("Symptomatic is a parallel attribute of unresolved infection and cannot exceed exposed, infectious, and post-infectious compartments.");
            }

            int newExposures = secondaryTransmissionCount - lastSecondaryTransmissionCount;
            if (newExposures < 0)
            {
                throw new InvalidOperationException("The cumulative secondary-transmission count decreased.");
            }

            stateTimeSeries.Add(new PandemicStateTimePoint
            {
                SimulationTime = simulationTime,
                Susceptible = counts.Susceptible,
                Exposed = counts.Exposed,
                Infectious = counts.Infectious,
                PostInfectiousIll = counts.PostInfectiousIll,
                Symptomatic = symptomatic,
                Recovered = counts.Recovered,
                Dead = counts.Dead,
                Removed = counts.Removed,
                TrackedPopulation = counts.Susceptible + counts.Exposed + counts.Infectious
                    + counts.PostInfectiousIll + counts.Recovered + counts.Dead,
                InitialSeedCount = initialSeedCount,
                SecondaryTransmissionsTotal = secondaryTransmissionCount,
                NewExposures = newExposures,
                DetectedNewCases = (surveillance?.DetectedCasesTotal ?? lastDetectedCasesTotal) - lastDetectedCasesTotal,
                DetectedActiveCases = surveillance?.DetectedActiveCases ?? 0,
                UndetectedActiveInfections = surveillance?.UndetectedActiveInfections ?? counts.Exposed + counts.Infectious + counts.PostInfectiousIll,
                CaseDetectionRatio = surveillance?.CaseDetectionRatio,
                HospitalizationsTotal = hospitalizationsTotal,
                IsolatedCitizens = isolatedCitizens,
                QuarantinedCitizens = quarantinedCitizens,
                IsolationFollowingCitizens = isolationFollowingCitizens,
                QuarantineFollowingCitizens = quarantineFollowingCitizens,
            });
            lastSecondaryTransmissionCount = secondaryTransmissionCount;
            lastDetectedCasesTotal = surveillance?.DetectedCasesTotal ?? lastDetectedCasesTotal;
        }

        public void RecordTransmission(PandemicTransmissionEvent transmission)
        {
            EnsureMutable();
            if (transmission == null)
            {
                throw new ArgumentNullException(nameof(transmission));
            }

            EnsureRunTime(transmission.SimulationTime);
            if (transmission.SourceCitizenId == 0u
                || transmission.TargetCitizenId == 0u
                || transmission.SourceCitizenId == transmission.TargetCitizenId)
            {
                throw new InvalidOperationException("A transmission requires distinct, nonzero source and target citizen IDs.");
            }

            if (transmission.TargetPreviousState != DiseaseState.Susceptible
                || transmission.TargetNewState != DiseaseState.Exposed
                || transmission.IsInitialSeed)
            {
                throw new InvalidOperationException("Each transmission event must represent exactly one non-seed Susceptible-to-Exposed transition.");
            }

            ValidateProbability(transmission.TransmissionProbability, "combined transmission probability");
            ValidateProbability(transmission.SourceProbability, "source transmission probability");
            if (double.IsNaN(transmission.InfectiousnessMultiplier)
                || double.IsInfinity(transmission.InfectiousnessMultiplier)
                || transmission.InfectiousnessMultiplier < 0d)
            {
                throw new InvalidOperationException("The infectiousness multiplier must be finite and nonnegative.");
            }

            transmission.EventId = nextTransmissionEventId++;
            transmissionEvents.Add(transmission);
            districtTransmissions[transmission.DistrictId] = GetDistrictTransmissions(transmission.DistrictId) + 1;
            if (transmissionWriter != null)
            {
                WriteCsvRow(
                    transmissionWriter,
                    transmission.EventId,
                    Iso(transmission.SimulationTime),
                    PandemicDay(transmission.SimulationTime),
                    transmission.SourceCitizenId,
                    transmission.TargetCitizenId,
                    Number(transmission.SourceInfectionAgeDays),
                    transmission.TargetPreviousState,
                    transmission.TargetNewState,
                    transmission.Context,
                    transmission.OriginCategory,
                    transmission.BuildingId,
                    transmission.VehicleId,
                    transmission.DistrictId,
                    transmission.SourceMaskType,
                    transmission.TargetMaskType,
                    Number(transmission.TransmissionProbability),
                    Number(transmission.SourceProbability),
                    Number(transmission.InfectiousnessMultiplier),
                    transmission.IsInitialSeed ? 1 : 0,
                    Number(transmission.PositionX), Number(transmission.PositionY), Number(transmission.PositionZ), transmission.HasPosition ? 1 : 0);
                FlushStreamsPeriodically();
            }
        }

        public void RecordPhysicalContact(PhysicalContactEvent contact, byte districtId, int ageA = -1, int ageB = -1)
        {
            using (PandemicProfiler.Measure("RecorderPhysicalContact")) RecordPhysicalContactCore(contact, districtId, ageA, ageB);
        }

        private void RecordPhysicalContactCore(PhysicalContactEvent contact, byte districtId, int ageA, int ageB)
        {
            EnsureMutable();
            if (contact == null)
            {
                throw new ArgumentNullException(nameof(contact));
            }

            EnsureRunTime(contact.EndTime);
            if (contact.StartTime < RunStartTime || contact.EndTime < contact.StartTime)
            {
                throw new InvalidOperationException("A physical contact must lie within the run and have a nonnegative interval.");
            }

            physicalContactsTotal++;
            network.Record(contact, ageA, ageB);
            if (contact.TraceableByApp || contact.TraceableByManual)
            {
                traceableContactsTotal++;
            }

            switch (contact.Context)
            {
                case PhysicalContactContext.Household:
                    householdContactsTotal++;
                    break;
                case PhysicalContactContext.Workplace:
                    workContactsTotal++;
                    break;
                case PhysicalContactContext.School:
                case PhysicalContactContext.University:
                    schoolContactsTotal++;
                    break;
                case PhysicalContactContext.PublicTransport:
                    transitContactsTotal++;
                    break;
            }

            if (physicalContactWriter == null)
            {
                return;
            }

            // All contacts in a step share these timestamps. Keep only the last pair;
            // changing intervals still produces the original round-trip CSV text.
            if (cachedContactStartIso == null || contact.StartTime.ToBinary() != cachedContactStart.ToBinary())
            {
                cachedContactStart = contact.StartTime;
                cachedContactStartIso = Iso(contact.StartTime);
            }
            if (cachedContactEndIso == null || contact.EndTime.ToBinary() != cachedContactEnd.ToBinary())
            {
                cachedContactEnd = contact.EndTime;
                cachedContactEndIso = Iso(contact.EndTime);
            }
            contactCsvWriter.Write(physicalContactWriter, traceableContactWriter, contact,
                districtId, cachedContactStartIso, cachedContactEndIso);

            FlushStreamsPeriodically();
        }

        public void RecordPreventedContact()
        {
            EnsureMutable();
            contactsPreventedByIntervention++;
        }

        public void RecordIntervention(PandemicInterventionEvent intervention)
        {
            EnsureMutable();
            if (intervention == null)
            {
                throw new ArgumentNullException(nameof(intervention));
            }

            EnsureRunTime(intervention.SimulationTime);

            intervention.EventId = nextInterventionEventId++;
            interventionEvents.Add(intervention);
        }

        public void RecordPopulation(PandemicPopulationEvent populationEvent)
        {
            EnsureMutable();
            if (populationEvent == null)
            {
                throw new ArgumentNullException(nameof(populationEvent));
            }

            EnsureRunTime(populationEvent.SimulationTime);
            if (populationEvent.Count < 0)
            {
                throw new InvalidOperationException("Population-event counts cannot be negative.");
            }

            populationEvent.EventId = nextPopulationEventId++;
            network.Population(populationEvent);
            populationEvents.Add(populationEvent);
        }

        public ExperimentRecorderSnapshot Freeze(DateTime endTime)
        {
            return Freeze(endTime, true);
        }

        public ExperimentRecorderSnapshot Freeze(DateTime endTime, bool validateTransmissionCount)
        {
            // An invalid run may have failed because its clock moved backwards. Preserve
            // its recorded prefix for diagnostics; successful exports remain strict.
            if (!validateTransmissionCount && endTime < network.LatestTime)
            {
                endTime = network.LatestTime;
            }
            EnsureRunTime(endTime);
            if (validateTransmissionCount && stateTimeSeries.Count > 0)
            {
                int expected = stateTimeSeries[stateTimeSeries.Count - 1].SecondaryTransmissionsTotal;
                if (transmissionEvents.Count != expected)
                {
                    throw new InvalidOperationException(
                        "Every successful secondary transmission must have exactly one event row; expected "
                        + expected + ", recorded " + transmissionEvents.Count + ".");
                }
            }

            if (!frozen)
            {
                network.Complete(endTime);
                CloseAndPublishStreamingFiles();
                frozen = true;
            }

            var result = new ExperimentRecorderSnapshot
            {
                RunStartTime = RunStartTime,
                RunEndTime = endTime,
                PhysicalContactsTotal = physicalContactsTotal,
                TraceableContactsTotal = traceableContactsTotal,
                HouseholdContactsTotal = householdContactsTotal,
                WorkContactsTotal = workContactsTotal,
                SchoolContactsTotal = schoolContactsTotal,
                TransitContactsTotal = transitContactsTotal,
                ContactsPreventedByIntervention = contactsPreventedByIntervention,
                ContactNetworkSummaryCsv = network.SummaryCsv,
                AgeMixingCsv = network.AgeMixingCsv(),
                DegreeDistributionCsv = network.DegreeCsv,
                ContactDurationCsv = network.DurationCsv(),
                ContactTimeOfDayCsv = network.HourCsv(),
            };
            result.StateTimeSeries.AddRange(stateTimeSeries);
            result.TransmissionEvents.AddRange(transmissionEvents);
            result.InterventionEvents.AddRange(interventionEvents);
            result.PopulationEvents.AddRange(populationEvents);
            return result;
        }

        public void Reset()
        {
            DisposeWriters();
            outputDirectory = null;
            RunStartTime = default(DateTime);
            stateTimeSeries.Clear();
            transmissionEvents.Clear();
            districtTransmissions.Clear();
            interventionEvents.Clear();
            populationEvents.Clear();
            nextTransmissionEventId = 1L;
            nextInterventionEventId = 1L;
            nextPopulationEventId = 1L;
            lastSecondaryTransmissionCount = 0;
            lastDetectedCasesTotal = 0;
            pendingStreamRows = 0;
            physicalContactsTotal = 0L;
            traceableContactsTotal = 0L;
            householdContactsTotal = 0L;
            workContactsTotal = 0L;
            schoolContactsTotal = 0L;
            transitContactsTotal = 0L;
            contactsPreventedByIntervention = 0L;
            frozen = false;
        }

        public void Dispose()
        {
            DisposeWriters();
        }

        private StreamWriter CreateStreamingWriter(string finalName, string header)
        {
            string temporaryPath = Path.Combine(outputDirectory, finalName + ".tmp");
            var writer = new StreamWriter(
                new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read),
                new UTF8Encoding(false));
            writer.WriteLine(header);
            writer.Flush();
            return writer;
        }

        private void CloseAndPublishStreamingFiles()
        {
            if (outputDirectory == null)
            {
                DisposeWriters();
                return;
            }

            FlushAndClose(ref transmissionWriter);
            FlushAndClose(ref physicalContactWriter);
            FlushAndClose(ref traceableContactWriter);
            PublishTemporaryFile(TransmissionEventsFileName);
            PublishTemporaryFile(PhysicalContactsFileName);
            PublishTemporaryFile(TraceableContactsFileName);
        }

        private void PublishTemporaryFile(string finalName)
        {
            string finalPath = Path.Combine(outputDirectory, finalName);
            string temporaryPath = finalPath + ".tmp";
            if (File.Exists(finalPath))
            {
                if (File.Exists(temporaryPath))
                {
                    throw new IOException("A scientific output exists beside its temporary file: " + finalPath);
                }

                return;
            }

            if (!File.Exists(temporaryPath))
            {
                throw new FileNotFoundException("A streamed scientific output is missing.", temporaryPath);
            }

            File.Move(temporaryPath, finalPath);
        }

        private void FlushStreamsPeriodically()
        {
            pendingStreamRows++;
            if (pendingStreamRows < 256)
            {
                return;
            }

            transmissionWriter?.Flush();
            physicalContactWriter?.Flush();
            traceableContactWriter?.Flush();
            pendingStreamRows = 0;
        }

        private void DisposeWriters()
        {
            FlushAndClose(ref transmissionWriter);
            FlushAndClose(ref physicalContactWriter);
            FlushAndClose(ref traceableContactWriter);
        }

        private static void FlushAndClose(ref StreamWriter writer)
        {
            if (writer == null)
            {
                return;
            }

            writer.Flush();
            writer.Dispose();
            writer = null;
        }

        private void EnsureMutable()
        {
            if (frozen)
            {
                throw new InvalidOperationException("The scientific run recorder is frozen.");
            }
        }

        private void EnsureRunTime(DateTime simulationTime)
        {
            if (RunStartTime == default(DateTime))
            {
                throw new InvalidOperationException("The scientific run recorder has not been started.");
            }

            if (simulationTime < RunStartTime)
            {
                throw new InvalidOperationException("A scientific event cannot precede the run start time.");
            }
        }

        private static void ValidateProbability(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d || value > 1d)
            {
                throw new InvalidOperationException("The " + name + " must be finite and between zero and one.");
            }
        }

        private int PandemicDay(DateTime simulationTime)
        {
            return RunStartTime == default(DateTime)
                ? 0
                : Math.Max(0, (int)Math.Floor((simulationTime - RunStartTime).TotalDays));
        }

        private static string Iso(DateTime value)
        {
            return value.ToString("o", CultureInfo.InvariantCulture);
        }

        private static string Number(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static void WriteCsvRow(StreamWriter writer, params object[] values)
        {
            using (PandemicProfiler.Measure("ScientificCsvRow")) WriteCsvRowCore(writer, values);
        }

        private static void WriteCsvRowCore(StreamWriter writer, object[] values)
        {
            for (int i = 0; i < values.Length; ++i)
            {
                if (i > 0)
                {
                    writer.Write(',');
                }

                string value = Convert.ToString(values[i], CultureInfo.InvariantCulture) ?? string.Empty;
                if (value.IndexOfAny(CsvEscapeCharacters) >= 0)
                {
                    writer.Write('"');
                    writer.Write(value.Replace("\"", "\"\""));
                    writer.Write('"');
                }
                else
                {
                    writer.Write(value);
                }
            }

            writer.WriteLine();
        }
    }
}
