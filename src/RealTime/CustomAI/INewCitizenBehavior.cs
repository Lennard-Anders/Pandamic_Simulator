// <copyright file="INewCitizenBehavior.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.CustomAI
{
    /// <summary>
    /// An interface for a behavior that determines the creation of new citizens.
    /// </summary>
    internal interface INewCitizenBehavior
    {
        /// <summary>
        /// Determines whether creating a new citizen is currently allowed.
        /// </summary>
        /// <returns><c>true</c> if a new citizen can be created; otherwise, <c>false</c>.</returns>
        bool CanCreateCitizen();

        /// <summary>
        /// Gets the education level of the new citizen based on their <paramref name="age"/>.
        /// </summary>
        ///
        /// <param name="age">The citizen's age as raw value (0-255).</param>
        /// <param name="currentEducation">The current value of the citizen's education.</param>
        ///
        /// <returns>The education level of the new citizen with the specified age.</returns>
        Citizen.Education GetEducation(int age, Citizen.Education currentEducation);

        /// <summary>
        /// Adjusts the age of the new citizen based on their current <paramref name="age"/>.
        /// </summary>
        /// <param name="age">The citizen's age as raw value (0-255).</param>
        /// <returns>An adjusted raw value (0-255) for the citizen's age.</returns>
        int AdjustCitizenAge(int age);
    }
}
