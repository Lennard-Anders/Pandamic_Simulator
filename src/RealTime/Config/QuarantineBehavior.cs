// <copyright file="VirtualCitizensLevel.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.Config
{
    /// <summary>
    /// Quantine behavior of citizens.
    /// </summary>
    public enum QuarantineBehavior
    {
        /// <summary>No quarantine.</summary>
        None,

        /// <summary>Only the sick citizen himself.</summary>
        Self,

        /// <summary>Only the sick citizen himself, but no contact to other persons inside the building.</summary>
        Family,

        /// <summary>The sick citizen himself and his contacts.</summary>
        Contacts,
    }
}
