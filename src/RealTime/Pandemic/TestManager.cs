using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace RealTime.Pandemic
{
    class TestManager
    {
        public static TestManager Instance { get; } = new TestManager();

        private System.Random random = new System.Random(13337);

        private bool sendTestedCitizensToQuarantine = true;

        private Dictionary<uint, DateTime> positive = new Dictionary<uint, DateTime>();
        private Dictionary<uint, DateTime> testedCitizens = new Dictionary<uint, DateTime>();

        private double falsePositiveRate = 0;
        private double falseNegativeRate = 0;

        private Config.RealTimeConfig config;
        private int population;

        private DateTime lastPerformedTestSick = default;
        private DateTime lastPerformedTestNonSick = default;

        public void Init(Config.RealTimeConfig config, int population, DateTime initialTime)
        {
            this.config = config;
            this.population = population;

            lastPerformedTestSick = initialTime;
            lastPerformedTestNonSick = initialTime;
        }

        public DateTime GetLastTestDate(uint citizenId)
        {
            if (testedCitizens.ContainsKey(citizenId))
            {
                return testedCitizens[citizenId];
            }
            return default;
        }

        public DateTime GetPositiveDate(uint citizenId)
        {
            if (positive.ContainsKey(citizenId))
            {
                return positive[citizenId];
            }
            return default;
        }

        public void TestCitizen(uint citizenId, bool sick, bool knownSick, DateTime currentDate)
        {
            if (!testedCitizens.ContainsKey(citizenId) || ((currentDate.Ticks - testedCitizens[citizenId].Ticks) > 7 * TimeSpan.TicksPerDay && !positive.ContainsKey(citizenId)))
            {
                bool scheduleAsSick = false;
                DateTime plannedDate = default;
                if (ShouldBeTested(citizenId, knownSick, currentDate, out plannedDate, out scheduleAsSick))
                {
                    Test(citizenId, sick, plannedDate, scheduleAsSick);
                }
            }
        }

        public bool IsTestedPositive(uint citizenId, DateTime currentDate)
        {
            return positive.ContainsKey(citizenId) && positive[citizenId] <= currentDate
                    && new DateTime(positive[citizenId].Ticks + config.DiseaseDuration * TimeSpan.TicksPerDay) > currentDate;
        }

        public bool IsBlocked(uint citizenId, DateTime currentDate)
        {
            if (testedCitizens.ContainsKey(citizenId))
            {
                return positive.ContainsKey(citizenId)
                    && ((positive[citizenId] <= currentDate && new DateTime(positive[citizenId].Ticks + config.DiseaseDuration * TimeSpan.TicksPerDay) > currentDate)
                    || (sendTestedCitizensToQuarantine && testedCitizens[citizenId] < currentDate));
            }
            return false;
        }

        public int GetTrackedTestsCount()
        {
            return testedCitizens.Count;
        }

        public int GetCurrentPositiveCount(DateTime currentDate)
        {
            if (config == null)
            {
                return 0;
            }

            return positive.Count(p => p.Value <= currentDate && new DateTime(p.Value.Ticks + config.DiseaseDuration * TimeSpan.TicksPerDay) > currentDate);
        }

        private void Test(uint citizenId, bool sick, DateTime plannedDate, bool scheduledAsSick)
        {
            testedCitizens[citizenId] = plannedDate;
            if ((sick && random.NextDouble() >= falseNegativeRate) || (!sick && random.NextDouble() < falsePositiveRate))
            {
                positive[citizenId] = new DateTime(plannedDate.Ticks + TimeSpan.TicksPerDay * config.MinimumTestDuration);
            }
            
            if (scheduledAsSick)
            {
                lastPerformedTestSick = plannedDate;
            } else
            {
                lastPerformedTestNonSick = plannedDate;
            }
        }

        private bool ShouldBeTested(uint citizenId, bool knownSick, DateTime currentDate, out DateTime plannedDate, out bool scheduleAsSick)
        {
            plannedDate = default;
            scheduleAsSick = false;
            if (testedCitizens.ContainsKey(citizenId) && (currentDate.Ticks - testedCitizens[citizenId].Ticks) < 7 * TimeSpan.TicksPerDay ||  positive.ContainsKey(citizenId))
            {
                return false;
            }

            double allowedTestsSick = Math.Ceiling((config.RelativeTestCapacity / 100.0) * (config.PercentageOfTestsReservedForSickCitizens / 100.0) * population);
            double allowedTestsNonSick = Math.Floor((config.RelativeTestCapacity / 100.0) * (1 - config.PercentageOfTestsReservedForSickCitizens / 100.0) * population);

            if (knownSick)
            {
                if (allowedTestsSick > 0)
                {
                    long durationBetweenTests = (long) (TimeSpan.TicksPerDay * 7L / allowedTestsSick);
                    plannedDate = new DateTime(Math.Max(lastPerformedTestSick.Ticks + durationBetweenTests, currentDate.Ticks - TimeSpan.TicksPerDay));
                    scheduleAsSick = true;
                    if ((plannedDate.Ticks - currentDate.Ticks) / TimeSpan.TicksPerDay <= config.MaximumTestDuration) {
                        return true;
                    }
                }
            }
            if (allowedTestsNonSick > 0)
            {
                long durationBetweenTests = (long)(TimeSpan.TicksPerDay * 7L / allowedTestsNonSick);
                plannedDate = new DateTime(Math.Max(lastPerformedTestNonSick.Ticks + durationBetweenTests, currentDate.Ticks - TimeSpan.TicksPerDay));
                scheduleAsSick = false;
                if ((plannedDate.Ticks - currentDate.Ticks) / TimeSpan.TicksPerDay <= config.MaximumTestDuration)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
