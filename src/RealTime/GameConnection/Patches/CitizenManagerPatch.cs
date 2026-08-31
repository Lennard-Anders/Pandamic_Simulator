// <copyright file="CitizenManagerPatch.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.GameConnection.Patches
{
    using System;
    using System.Reflection;
    using ColossalFramework.Math;
    using RealTime.CustomAI;
    using SkyTools.Patching;

    /// <summary>
    /// A static class that provides the patch objects for the game's citizen manager.
    /// </summary>
    internal static class CitizenManagerPatch
    {
        /// <summary>
        /// Gets or sets the implementation of the <see cref="INewCitizenBehavior"/> interface.
        /// </summary>
        public static INewCitizenBehavior NewCitizenBehavior { get; set; }

        /// <summary>Gets the patch object for the method that creates a new citizen.</summary>
        public static IPatch CreateCitizenPatch1 { get; } = new CitizenManager_CreateCitizen1();

        /// <summary>Gets the patch object for the method that creates a new citizen (with specified gender).</summary>
        public static IPatch CreateCitizenPatch2 { get; } = new CitizenManager_CreateCitizen2();

        /// <summary>Gets the observational patch that reports a citizen before its buffer slot is released.</summary>
        public static IPatch ReleaseCitizenPatch { get; } = new CitizenManager_ReleaseCitizen();

        /// <summary>Gets or sets the level-owned observer for exact citizen lifecycle releases.</summary>
        public static Action<uint> CitizenReleased { get; set; }

        private static void UpdateCizizenAge(uint citizenId)
        {
            ref var citizen = ref CitizenManager.instance.m_citizens.m_buffer[citizenId];
            citizen.Age = NewCitizenBehavior.AdjustCitizenAge(citizen.Age);
        }

        private static void UpdateCitizenEducation(uint citizenId)
        {
            ref var citizen = ref CitizenManager.instance.m_citizens.m_buffer[citizenId];
            var newEducation = NewCitizenBehavior.GetEducation(citizen.Age, citizen.EducationLevel);
            citizen.Education3 = newEducation == Citizen.Education.ThreeSchools;
            citizen.Education2 = newEducation == Citizen.Education.TwoSchools || newEducation == Citizen.Education.ThreeSchools;
            citizen.Education1 = newEducation != Citizen.Education.Uneducated;
        }

        private sealed class CitizenManager_CreateCitizen1 : PatchBase
        {
            protected override MethodInfo GetMethod() =>
                typeof(CitizenManager).GetMethod(
                    "CreateCitizen",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[] { typeof(uint).MakeByRefType(), typeof(int), typeof(int), typeof(Randomizer).MakeByRefType() },
                    new ParameterModifier[0]);

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Redundancy", "RCS1213", Justification = "Harmony patch")]
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming Rules", "SA1313", Justification = "Harmony patch")]
            private static bool Prefix(ref uint citizen, ref bool __result)
            {
                if (NewCitizenBehavior != null && !NewCitizenBehavior.CanCreateCitizen())
                {
                    citizen = 0;
                    __result = false;
                    return false;
                }

                return true;
            }

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Redundancy", "RCS1213", Justification = "Harmony patch")]
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming Rules", "SA1313", Justification = "Harmony patch")]
            private static void Postfix(ref uint citizen, bool __result)
            {
                if (__result)
                {
                    // This method is called by the game in two cases only: a new child is born or a citizen joins the city.
                    // So we tailor the age here.
                    UpdateCizizenAge(citizen);
                    UpdateCitizenEducation(citizen);
                }
            }
        }

        private sealed class CitizenManager_CreateCitizen2 : PatchBase
        {
            protected override MethodInfo GetMethod() =>
                typeof(CitizenManager).GetMethod(
                    "CreateCitizen",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[] { typeof(uint).MakeByRefType(), typeof(int), typeof(int), typeof(Randomizer).MakeByRefType(), typeof(Citizen.Gender) },
                    new ParameterModifier[0]);

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Redundancy", "RCS1213", Justification = "Harmony patch")]
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming Rules", "SA1313", Justification = "Harmony patch")]
            private static bool Prefix(ref uint citizen, ref bool __result)
            {
                if (NewCitizenBehavior != null && !NewCitizenBehavior.CanCreateCitizen())
                {
                    citizen = 0;
                    __result = false;
                    return false;
                }

                return true;
            }

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Redundancy", "RCS1213", Justification = "Harmony patch")]
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming Rules", "SA1313", Justification = "Harmony patch")]
            private static void Postfix(ref uint citizen, bool __result)
            {
                if (__result)
                {
                    UpdateCitizenEducation(citizen);
                }
            }
        }

        private sealed class CitizenManager_ReleaseCitizen : PatchBase
        {
            protected override MethodInfo GetMethod() =>
                typeof(CitizenManager).GetMethod(
                    "ReleaseCitizen",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[] { typeof(uint) },
                    new ParameterModifier[0]);

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming Rules", "SA1313", Justification = "Harmony patch")]
            private static void Prefix(uint citizen)
            {
                CitizenReleased?.Invoke(citizen);
            }
        }
    }
}
