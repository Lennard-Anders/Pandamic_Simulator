// <copyright file="CitizenAIPatch.cs" company="dymanoid">Copyright (c) dymanoid. All rights reserved.</copyright>

namespace RealTime.GameConnection.Patches
{
    using System;
    using System.Reflection;
    using RealTime.Pandemic;
    using SkyTools.Patching;
    using UnityEngine;

    /// <summary>
    /// A static class that provides the patch objects for the citizen AI color rendering.
    /// </summary>
    internal static class CitizenAIPatch
    {
        /// <summary>Gets the patch for the HumanAI GetColor method (colors infected citizens red).</summary>
        public static IPatch GetColor { get; } = new HumanAI_GetColor();

        private sealed class HumanAI_GetColor : PatchBase
        {
            protected override MethodInfo GetMethod() =>
                typeof(HumanAI).GetMethod(
                    "GetColor",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[] { typeof(ushort), typeof(CitizenInstance).MakeByRefType(), typeof(InfoManager.InfoMode) },
                    new ParameterModifier[0]);

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Redundancy", "RCS1213", Justification = "Harmony patch")]
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming Rules", "SA1313", Justification = "Harmony patch")]
            private static void Postfix(ushort instanceID, ref CitizenInstance data, InfoManager.InfoMode infoMode, ref Color __result)
            {
                if (infoMode != InfoManager.InfoMode.None)
                {
                    return;
                }

                var manager = PandemicManager.Instance;
                if (manager == null)
                {
                    return;
                }

                uint citizenId = data.m_citizen;
                if (citizenId != 0 && manager.IsCitizenInfected(citizenId))
                {
                    __result = new Color(1f, 0.1f, 0.1f, __result.a);
                }
            }
        }
    }
}
