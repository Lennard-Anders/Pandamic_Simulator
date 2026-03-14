// <copyright file="VirtualCitizensLevel.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.Config
{
    /// <summary>
    /// Quantine behavior of citizens.
    /// </summary>
    public enum LockdownBehavior
    {
        /// <summary>Nothing is true, everything is permitted.</summary>
        None,

        /// <summary>Only work is allowed.</summary>
        Work,

        /// <summary>Nothing is allowed.</summary>
        Full,
    }
}
