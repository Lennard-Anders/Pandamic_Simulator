namespace RealTimeTests.Reference { using System; using System.Collections.Generic; using RealTime.Pandemic;     internal sealed class BaselineTestingEngine
    {
        private readonly List<PandemicTestRecord> records = new List<PandemicTestRecord>();
        private readonly Dictionary<uint, PandemicTestRecord> activeByCitizen = new Dictionary<uint, PandemicTestRecord>();
        private readonly Dictionary<uint, PandemicTestRecord> latestCompletedByCitizen = new Dictionary<uint, PandemicTestRecord>();
        private readonly Dictionary<uint, PandemicTestRecord> latestAvailableResultByCitizen = new Dictionary<uint, PandemicTestRecord>();
        private readonly Dictionary<uint, DateTime> latestSampleTimeByCitizen = new Dictionary<uint, DateTime>();
        private PandemicTestingPolicy policy;
        private Random random;
        private long nextTestId;
        private DateTime nextSymptomaticSlot;
        private DateTime nextRoutineSlot;

        public BaselineTestingEngine(PandemicTestingPolicy policy, Random random, DateTime initialTime)
        {
            Initialize(policy, random, initialTime);
        }

        public int Count => records.Count;

        public IEnumerable<PandemicTestRecord> Records => records;

        public void Initialize(PandemicTestingPolicy newPolicy, Random newRandom, DateTime initialTime)
        {
            if (newPolicy == null)
            {
                throw new ArgumentNullException(nameof(newPolicy));
            }

            newPolicy.Validate();
            policy = newPolicy;
            random = newRandom ?? throw new ArgumentNullException(nameof(newRandom));
            records.Clear();
            activeByCitizen.Clear();
            latestCompletedByCitizen.Clear();
            latestAvailableResultByCitizen.Clear();
            latestSampleTimeByCitizen.Clear();
            nextTestId = 1;
            nextSymptomaticSlot = initialTime;
            nextRoutineSlot = initialTime;
        }

        public bool RequestTest(
            uint citizenId,
            DateTime requestedAt,
            PandemicTestPriority priority,
            PandemicTestReason reason)
        {
            if (citizenId == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(citizenId));
            }

            if (activeByCitizen.TryGetValue(citizenId, out PandemicTestRecord active))
            {
                // A routine request may wait for days. If symptoms appear before sampling, replace
                // it with a fresh symptomatic request so reserved symptomatic capacity has real
                // effect. A sample that has already been taken is immutable.
                if (priority <= active.Priority
                    || (active.State != PandemicTestState.Requested
                        && active.State != PandemicTestState.Queued
                        && active.State != PandemicTestState.Scheduled))
                {
                    return false;
                }

                CancelActive(active);
            }

            if (latestSampleTimeByCitizen.TryGetValue(citizenId, out DateTime latestSampleTime)
                && requestedAt < latestSampleTime.AddDays(policy.RetestIntervalDays))
            {
                return false;
            }

            var record = new PandemicTestRecord
            {
                TestId = nextTestId++,
                CitizenId = citizenId,
                RequestedAt = requestedAt,
                Priority = priority,
                Reason = reason,
                Result = PandemicTestResult.Unknown,
                PendingResult = PandemicTestResult.Unknown,
                State = PandemicTestState.Requested,
            };
            records.Add(record);
            activeByCitizen.Add(citizenId, record);
            return true;
        }

        public void Advance(DateTime simulationTime, Func<uint, PandemicTestSampleContext> sampleContextProvider)
        {
            if (sampleContextProvider == null)
            {
                throw new ArgumentNullException(nameof(sampleContextProvider));
            }

            ScheduleQueuedTests(simulationTime, sampleContextProvider);
            for (int i = 0; i < records.Count; i++)
            {
                PandemicTestRecord record = records[i];
                if (record.State == PandemicTestState.Scheduled
                    && record.ScheduledAt.HasValue
                    && simulationTime >= record.ScheduledAt.Value)
                {
                    if (simulationTime > record.RequestedAt.AddDays(policy.MaximumRequestToSampleDays))
                    {
                        Expire(record);
                    }
                    else
                    {
                        TakeSample(record, simulationTime, sampleContextProvider(record.CitizenId));
                    }
                }

                if (record.State == PandemicTestState.ResultPending
                    && record.ResultAvailableAt.HasValue
                    && simulationTime >= record.ResultAvailableAt.Value)
                {
                    PublishResult(record);
                }
            }
        }

        public bool IsPositiveResultAvailable(uint citizenId, DateTime simulationTime)
        {
            return latestAvailableResultByCitizen.TryGetValue(citizenId, out PandemicTestRecord record)
                && record.State == PandemicTestState.ResultAvailable
                && record.Result == PandemicTestResult.Positive
                && record.ResultAvailableAt.HasValue
                && simulationTime >= record.ResultAvailableAt.Value
                && simulationTime < record.ResultAvailableAt.Value.AddDays(policy.PositiveBlockingDays);
        }

        public bool ShouldBlockCitizen(uint citizenId, DateTime simulationTime)
        {
            if (policy.QuarantineWhileAwaitingResult && IsAwaitingResult(citizenId))
            {
                return true;
            }

            return IsPositiveResultAvailable(citizenId, simulationTime);
        }

        public bool IsAwaitingResult(uint citizenId)
        {
            return activeByCitizen.TryGetValue(citizenId, out PandemicTestRecord active)
                && (active.State == PandemicTestState.SampleTaken
                    || active.State == PandemicTestState.ResultPending);
        }

        public PandemicTestRecord GetLatestRecord(uint citizenId)
        {
            if (activeByCitizen.TryGetValue(citizenId, out PandemicTestRecord active))
            {
                return active;
            }

            latestCompletedByCitizen.TryGetValue(citizenId, out PandemicTestRecord completed);
            return completed;
        }

        public int GetCurrentPositiveCount(DateTime simulationTime)
        {
            int count = 0;
            foreach (uint citizenId in latestAvailableResultByCitizen.Keys)
            {
                if (IsPositiveResultAvailable(citizenId, simulationTime))
                {
                    count++;
                }
            }

            return count;
        }

        public void CancelCitizen(uint citizenId)
        {
            if (!activeByCitizen.TryGetValue(citizenId, out PandemicTestRecord record))
            {
                return;
            }

            CancelActive(record);
        }

        private void ScheduleQueuedTests(
            DateTime simulationTime,
            Func<uint, PandemicTestSampleContext> sampleContextProvider)
        {
            List<PandemicTestRecord> queue = records.FindAll(r => r.State == PandemicTestState.Requested || r.State == PandemicTestState.Queued);
            for (int i = 0; i < queue.Count; i++)
            {
                PandemicTestRecord record = queue[i];
                record.State = PandemicTestState.Queued;
                if (simulationTime > record.RequestedAt.AddDays(policy.MaximumRequestToSampleDays))
                {
                    Expire(record);
                }
            }

            SchedulePriority(PandemicTestPriority.Symptomatic, simulationTime, sampleContextProvider);
            SchedulePriority(PandemicTestPriority.Routine, simulationTime, sampleContextProvider);
        }

        private void SchedulePriority(
            PandemicTestPriority priority,
            DateTime simulationTime,
            Func<uint, PandemicTestSampleContext> sampleContextProvider)
        {
            double slotsPerSevenDays = GetSlotsPerSevenDays(priority);
            if (slotsPerSevenDays <= 0d)
            {
                return;
            }

            DateTime nextSlot = priority == PandemicTestPriority.Symptomatic
                ? nextSymptomaticSlot
                : nextRoutineSlot;
            while (true)
            {
                PandemicTestRecord record = GetNextQueued(priority);
                if (record == null)
                {
                    break;
                }

                DateTime scheduledAt = record.RequestedAt > nextSlot ? record.RequestedAt : nextSlot;
                if (scheduledAt > simulationTime)
                {
                    break;
                }

                if (simulationTime > record.RequestedAt.AddDays(policy.MaximumRequestToSampleDays))
                {
                    Expire(record);
                    continue;
                }

                record.ScheduledAt = scheduledAt;
                record.State = PandemicTestState.Scheduled;
                TakeSample(record, simulationTime, sampleContextProvider(record.CitizenId));
                nextSlot = scheduledAt.AddDays(7d / slotsPerSevenDays);
            }

            if (priority == PandemicTestPriority.Symptomatic)
            {
                nextSymptomaticSlot = nextSlot;
            }
            else
            {
                nextRoutineSlot = nextSlot;
            }
        }

        private PandemicTestRecord GetNextQueued(PandemicTestPriority priority)
        {
            PandemicTestRecord selected = null;
            for (int i = 0; i < records.Count; ++i)
            {
                PandemicTestRecord candidate = records[i];
                if (candidate.State != PandemicTestState.Queued || candidate.Priority != priority)
                {
                    continue;
                }

                if (selected == null || CompareQueueRecords(candidate, selected) < 0)
                {
                    selected = candidate;
                }
            }

            return selected;
        }

        private void TakeSample(
            PandemicTestRecord record,
            DateTime simulationTime,
            PandemicTestSampleContext context)
        {
            context = context ?? new PandemicTestSampleContext { DiseaseState = DiseaseState.Susceptible };
            record.SampleTakenAt = simulationTime;
            latestSampleTimeByCitizen[record.CitizenId] = simulationTime;
            record.State = PandemicTestState.SampleTaken;
            record.DiseaseStateAtSample = context.DiseaseState;
            record.InfectionAgeDaysAtSample = context.ExposureTime.HasValue
                ? (double?)(simulationTime - context.ExposureTime.Value).TotalDays
                : null;

            bool infectionDetectable = context.ExposureTime.HasValue
                && record.InfectionAgeDaysAtSample.Value >= policy.DetectionTimeDays
                && context.DiseaseState != DiseaseState.Susceptible
                && context.DiseaseState != DiseaseState.Recovered
                && context.DiseaseState != DiseaseState.Dead
                && context.DiseaseState != DiseaseState.Removed;
            double positiveProbability = infectionDetectable
                ? policy.SensitivityPercent / 100d
                : 1d - (policy.SpecificityPercent / 100d);
            record.PendingResult = random.NextDouble() < positiveProbability
                ? PandemicTestResult.Positive
                : PandemicTestResult.Negative;
            record.Result = PandemicTestResult.Unknown;
            record.ResultAvailableAt = simulationTime.AddDays(policy.ResultDelayDays);
            record.State = PandemicTestState.ResultPending;
        }

        private void PublishResult(PandemicTestRecord record)
        {
            record.Result = record.PendingResult;
            record.PendingResult = PandemicTestResult.Unknown;
            record.State = PandemicTestState.ResultAvailable;
            activeByCitizen.Remove(record.CitizenId);
            latestCompletedByCitizen[record.CitizenId] = record;
            latestAvailableResultByCitizen[record.CitizenId] = record;
        }

        private void Expire(PandemicTestRecord record)
        {
            record.State = PandemicTestState.Expired;
            record.Result = PandemicTestResult.Unknown;
            record.PendingResult = PandemicTestResult.Unknown;
            activeByCitizen.Remove(record.CitizenId);
            latestCompletedByCitizen[record.CitizenId] = record;
        }

        private void CancelActive(PandemicTestRecord record)
        {
            record.State = PandemicTestState.Cancelled;
            record.Result = PandemicTestResult.Unknown;
            record.PendingResult = PandemicTestResult.Unknown;
            activeByCitizen.Remove(record.CitizenId);
            latestCompletedByCitizen[record.CitizenId] = record;
        }

        private double GetSlotsPerSevenDays(PandemicTestPriority priority)
        {
            double total = (policy.RelativeCapacityPercentPerSevenDays / 100d) * policy.Population;
            double reserved = policy.ReservedForSymptomaticPercent / 100d;
            return priority == PandemicTestPriority.Symptomatic
                ? total * reserved
                : total * (1d - reserved);
        }

        private static int CompareQueueRecords(PandemicTestRecord left, PandemicTestRecord right)
        {
            int priority = right.Priority.CompareTo(left.Priority);
            if (priority != 0)
            {
                return priority;
            }

            int requested = left.RequestedAt.CompareTo(right.RequestedAt);
            return requested != 0 ? requested : left.TestId.CompareTo(right.TestId);
        }

    }
}
