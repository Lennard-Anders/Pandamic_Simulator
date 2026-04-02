// <copyright file="StaticBaselineMode.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.Config
{
    /// <summary>
    /// Defines the behavior of the static baseline controller.
    /// </summary>
    public enum StaticBaselineMode
    {
        /// <summary>
        /// Keeps the configured demand values but still allows the city to grow.
        /// </summary>
        Growth,

        /// <summary>
        /// Keeps the configured demand values without any additional implicit freeze behavior.
        /// </summary>
        Stabilization,
    }
}
