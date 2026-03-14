// <copyright file="VirtualCitizensLevel.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.Config
{
    /// <summary>
    /// Quantine behavior of citizens.
    /// </summary>
    public enum ContactTracingBehavior
    {
        /// <summary>No masks.</summary>
        None,

        /// <summary>Masks only in buildings.</summary>
        Building,

        /// <summary>Nothing is allowed.</summary>
        Full,
    }
}
