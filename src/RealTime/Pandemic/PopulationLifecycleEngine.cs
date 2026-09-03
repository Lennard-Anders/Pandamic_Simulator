// <copyright file="PopulationLifecycleEngine.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.Pandemic
{
    internal enum PandemicPopulationCategory
    {
        Resident,
        Tourist,
        Commuter,
        Immigrant,
        Emigrant,
    }

    internal enum PandemicPopulationDepartureKind
    {
        ExpectedTransient,
        ExpectedEmigration,
        UnexpectedDrift,
    }

    /// <summary>Pure classification rules for population lifecycle exports.</summary>
    internal sealed class PopulationLifecycleEngine
    {
        public PandemicPopulationCategory ClassifyCurrent(
            bool isTourist,
            bool isMovingIn,
            bool isDummyTraffic,
            bool hasHome)
        {
            if (isTourist)
            {
                return PandemicPopulationCategory.Tourist;
            }

            if (isMovingIn)
            {
                return PandemicPopulationCategory.Immigrant;
            }

            return isDummyTraffic || !hasHome
                ? PandemicPopulationCategory.Commuter
                : PandemicPopulationCategory.Resident;
        }

        public PandemicPopulationCategory ClassifyRemoval(PandemicPopulationCategory previous)
        {
            return previous == PandemicPopulationCategory.Resident
                || previous == PandemicPopulationCategory.Immigrant
                ? PandemicPopulationCategory.Emigrant
                : previous;
        }

        /// <summary>
        /// Determines whether a departure is ordinary transient-population turnover rather than
        /// unexpected drift of the resident scientific cohort.
        /// </summary>
        public bool IsExpectedTransientDeparture(PandemicPopulationCategory previous)
        {
            return previous == PandemicPopulationCategory.Tourist
                || previous == PandemicPopulationCategory.Commuter;
        }

        /// <summary>
        /// Classifies a departure using the exact game release notification. Reconciliation-only
        /// disappearance or slot reuse is drift because no intentional lifecycle release was observed.
        /// </summary>
        public PandemicPopulationDepartureKind ClassifyDeparture(
            PandemicPopulationCategory previous,
            bool observedGameRelease)
        {
            if (!observedGameRelease)
            {
                return PandemicPopulationDepartureKind.UnexpectedDrift;
            }

            return IsExpectedTransientDeparture(previous)
                ? PandemicPopulationDepartureKind.ExpectedTransient
                : PandemicPopulationDepartureKind.ExpectedEmigration;
        }
    }
}
