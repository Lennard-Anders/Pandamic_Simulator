// <copyright file="TestManager.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.Pandemic
{
    using System;
    using RealTime.Config;

    /// <summary>Game-facing facade over the Unity-independent testing state machine.</summary>
    internal sealed class TestManager
    {
        private Random random = new Random(13337);
        private RealTimeConfig config;
        private TestingEngine engine;

        private TestManager()
        {
        }

        public static TestManager Instance { get; } = new TestManager();

        internal TestingEngine Engine => engine;

        internal void UpdatePolicy(RealTimeConfig source, int population, DateTime activationTime)
        {
            config = source;
            engine.UpdatePolicy(CreatePolicy(source, population), activationTime);
        }

        internal void Reset()
        {
            engine = null;
            config = null;
        }

        /// <summary>Starts a new deterministic testing random stream.</summary>
        internal void ResetRandom(int seed)
        {
            random = new Random(seed);
        }

        /// <summary>Clears references and cached state retained by this process-wide singleton.</summary>
        internal void ResetForLevelUnload()
        {
            Reset();
        }

        public void Init(RealTimeConfig newConfig, int population, DateTime initialTime)
        {
            config = newConfig ?? throw new ArgumentNullException(nameof(newConfig));
            engine = new TestingEngine(CreatePolicy(config, population), random, initialTime);
        }

        public DateTime GetLastTestDate(uint citizenId)
        {
            PandemicTestRecord record = engine?.GetLatestRecord(citizenId);
            return record?.SampleTakenAt
                ?? record?.ScheduledAt
                ?? record?.RequestedAt
                ?? default(DateTime);
        }

        public DateTime GetPositiveDate(uint citizenId)
        {
            PandemicTestRecord record = engine?.GetLatestRecord(citizenId);
            return record != null
                && record.State == PandemicTestState.ResultAvailable
                && record.Result == PandemicTestResult.Positive
                && record.ResultAvailableAt.HasValue
                    ? record.ResultAvailableAt.Value
                    : default(DateTime);
        }

        /// <summary>Queues a citizen for testing; disease truth is deliberately not evaluated until sampling.</summary>
        public void TestCitizen(uint citizenId, bool sick, bool knownSick, DateTime currentDate)
        {
            if (engine == null)
            {
                return;
            }

            engine.RequestTest(
                citizenId,
                currentDate,
                knownSick ? PandemicTestPriority.Symptomatic : PandemicTestPriority.Routine,
                knownSick ? PandemicTestReason.Symptoms : PandemicTestReason.Screening);
        }

        public void ProcessPendingTests(DateTime currentDate, Func<uint, PandemicTestSampleContext> sampleContextProvider)
        {
            engine?.Advance(currentDate, sampleContextProvider);
        }

        public bool IsTestedPositive(uint citizenId, DateTime currentDate)
        {
            return engine != null && engine.IsPositiveResultAvailable(citizenId, currentDate);
        }

        public bool IsBlocked(uint citizenId, DateTime currentDate)
        {
            return engine != null && engine.ShouldBlockCitizen(citizenId, currentDate);
        }

        public bool IsAwaitingResult(uint citizenId)
        {
            return engine != null && engine.IsAwaitingResult(citizenId);
        }

        public int GetTrackedTestsCount()
        {
            return engine?.Count ?? 0;
        }

        public int GetCurrentPositiveCount(DateTime currentDate)
        {
            return engine?.GetCurrentPositiveCount(currentDate) ?? 0;
        }

        public void CancelCitizen(uint citizenId)
        {
            engine?.CancelCitizen(citizenId);
        }

        private static PandemicTestingPolicy CreatePolicy(RealTimeConfig source, int population)
        {
            return new PandemicTestingPolicy
            {
                Population = population,
                RelativeCapacityPercentPerSevenDays = source.RelativeTestCapacity,
                ReservedForSymptomaticPercent = source.PercentageOfTestsReservedForSickCitizens,
                MaximumRequestToSampleDays = source.MaximumTestDuration,
                ResultDelayDays = source.MinimumTestDuration,
                DetectionTimeDays = source.DetectionTime,
                SensitivityPercent = source.TestSensitivityPercent,
                SpecificityPercent = source.TestSpecificityPercent,
                RetestIntervalDays = source.RetestIntervalDays,
                PositiveBlockingDays = QuarantineManager.RestrictionDurationDays,
                QuarantineWhileAwaitingResult = source.QuarantineWhileAwaitingTestResult,
            };
        }
    }
}
