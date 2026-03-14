// <copyright file="VirtualCitizensLevel.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.Config
{
    /// <summary>
    /// Quantine behavior of citizens.
    /// </summary>
    public enum MaskBehavior
    {
        /// <summary>No masks.</summary>
        None,

        /// <summary>Masks only in buildings.</summary>
        Building,

        /// <summary>Masks only in buildings and vehicles.</summary>
        Vehicle,

        /// <summary>Nothing is allowed.</summary>
        Full,
    }
}
