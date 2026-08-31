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
    }
}
