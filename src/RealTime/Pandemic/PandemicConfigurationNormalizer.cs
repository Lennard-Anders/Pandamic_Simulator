// <copyright file="PandemicConfigurationNormalizer.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.Pandemic
{
    using RealTime.Config;

    /// <summary>Applies shared validation without interpreting a valid zero as a missing value.</summary>
    internal static class PandemicConfigurationNormalizer
    {
        public static void Normalize(RealTimeConfig config)
        {
            if (config == null)
            {
                return;
            }

            // Missing-property defaults are assigned only by versioned migration.
            config.Validate();
        }
    }
}
