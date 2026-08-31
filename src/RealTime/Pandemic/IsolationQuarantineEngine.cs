// <copyright file="IsolationQuarantineEngine.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.Pandemic
{
    /// <summary>Pure intervention decisions that never mutate a disease course.</summary>
    internal sealed class IsolationQuarantineEngine
    {
        public bool ShouldIsolateCase(
            bool onlyTestedCitizens,
            bool isCurrentlySymptomatic,
            bool hasAvailablePositiveResult,
            bool shouldBlockForPendingTest)
        {
            return hasAvailablePositiveResult
                || shouldBlockForPendingTest
                || (!onlyTestedCitizens && isCurrentlySymptomatic);
        }

        public bool ShouldQuarantineForPendingTest(bool shouldBlockForPendingTest)
        {
            return shouldBlockForPendingTest;
        }

        /// <summary>
        /// Applies behavioral restrictions only after the citizen is at home. Household contacts
        /// remain possible; external contacts are then disabled for both cases and contacts.
        /// </summary>
        public bool IsPhysicalContactAllowed(
            bool firstRestricted,
            bool firstAtHome,
            bool secondRestricted,
            bool secondAtHome,
            PhysicalContactContext context)
        {
            if (context == PhysicalContactContext.Household)
            {
                return true;
            }

            return !(firstRestricted && firstAtHome) && !(secondRestricted && secondAtHome);
        }
    }
}
