// <copyright file="TouristAIPatch.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.GameConnection.Patches
{
    using System;
    using System.Linq;
    using System.Reflection;
    using HarmonyAccessTools = Harmony.AccessTools;
    using RealTime.CustomAI;
    using SkyTools.Patching;
    using SkyTools.Tools;
    using static HumanAIConnectionBase<TouristAI, Citizen>;
    using static TouristAIConnection<TouristAI, Citizen>;

    /// <summary>
    /// A static class that provides the patch objects and the game connection objects for the tourist AI .
    /// </summary>
    internal static class TouristAIPatch
    {
        private static bool touristUpdateErrorLogged;
        private static readonly MethodInfo FastDelegateCreateMethod = typeof(FastDelegateFactory)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == "Create" && m.IsGenericMethodDefinition && m.GetParameters().Length == 3);

        /// <summary>Gets or sets the custom AI object for tourists.</summary>
        public static RealTimeTouristAI<TouristAI, Citizen> RealTimeAI { get; set; }

        /// <summary>Gets the patch object for the location method.</summary>
        public static IPatch Location { get; } = new TouristAI_UpdateLocation();

        /// <summary>Creates a game connection object for the tourist AI class.</summary>
        /// <returns>A new <see cref="TouristAIConnection{TouristAI, Citizen}"/> object.</returns>
        public static TouristAIConnection<TouristAI, Citizen> GetTouristAIConnection()
        {
            var getRandomTargetType = CreateDelegateIfAvailable<GetRandomTargetTypeDelegate>("GetRandomTargetType");
            var getLeavingReason = CreateDelegateIfAvailable<GetLeavingReasonDelegate>("GetLeavingReason");
            var addTouristVisit = CreateDelegateIfAvailable<AddTouristVisitDelegate>("AddTouristVisit");
            var doRandomMove = CreateDelegateIfAvailable<DoRandomMoveDelegate>("DoRandomMove");
            var findEvacuationPlace = CreateDelegateIfAvailable<FindEvacuationPlaceDelegate>("FindEvacuationPlace");
            var findVisitPlace = CreateDelegateIfAvailable<FindVisitPlaceDelegate>("FindVisitPlace");
            var getEntertainmentReason = CreateDelegateIfAvailable<GetEntertainmentReasonDelegate>("GetEntertainmentReason");
            var getEvacuationReason = CreateDelegateIfAvailable<GetEvacuationReasonDelegate>("GetEvacuationReason");
            var getShoppingReason = CreateDelegateIfAvailable<GetShoppingReasonDelegate>("GetShoppingReason");
            var startMoving = CreateDelegateIfAvailable<StartMovingDelegate>("StartMoving");

            if (getRandomTargetType == null
                || getLeavingReason == null
                || addTouristVisit == null
                || doRandomMove == null
                || findEvacuationPlace == null
                || findVisitPlace == null
                || getEntertainmentReason == null
                || getEvacuationReason == null
                || getShoppingReason == null
                || startMoving == null)
            {
                Log.Warning("The 'Real Time' mod skipped TouristAI customization because one or more original methods were not found.");
                return null;
            }

            return new TouristAIConnection<TouristAI, Citizen>(
                getRandomTargetType,
                getLeavingReason,
                addTouristVisit,
                doRandomMove,
                findEvacuationPlace,
                findVisitPlace,
                getEntertainmentReason,
                getEvacuationReason,
                getShoppingReason,
                startMoving);
        }

        private sealed class TouristAI_UpdateLocation : PatchBase
        {
            protected override MethodInfo GetMethod() =>
                typeof(TouristAI).GetMethod(
                    "UpdateLocation",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(uint), typeof(Citizen).MakeByRefType() },
                    new ParameterModifier[0]);

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Redundancy", "RCS1213", Justification = "Harmony patch")]
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming Rules", "SA1313", Justification = "Harmony patch")]
            private static bool Prefix(TouristAI __instance, uint citizenID, ref Citizen data)
            {
                if (RealTimeAI == null)
                {
                    return true;
                }

                try
                {
                    RealTimeAI.UpdateLocation(__instance, citizenID, ref data);
                    return false;
                }
                catch (Exception ex)
                {
                    if (!touristUpdateErrorLogged)
                    {
                        touristUpdateErrorLogged = true;
                        Log.Warning("The 'Real Time' mod disabled TouristAI custom update after an error. Falling back to vanilla behavior. Error: " + ex);
                    }

                    return true;
                }
            }
        }

        private static TDelegate CreateDelegateIfAvailable<TDelegate>(string methodName)
            where TDelegate : class
        {
            try
            {
                MethodInfo invokeMethod = typeof(TDelegate).GetMethod("Invoke");
                if (invokeMethod == null)
                {
                    return null;
                }

                Type[] delegateParameters = invokeMethod.GetParameters().Select(p => p.ParameterType).ToArray();
                Type[] targetParameters = delegateParameters.Skip(1).ToArray();
                MethodInfo targetMethod = HarmonyAccessTools.Method(typeof(TouristAI), methodName, targetParameters);
                if (targetMethod == null)
                {
                    Log.Warning($"The 'Real Time' mod could not find TouristAI.{methodName} and will skip related tourist patching.");
                    return null;
                }

                if (FastDelegateCreateMethod == null)
                {
                    Log.Warning("The 'Real Time' mod could not access FastDelegateFactory.Create and will skip tourist patching.");
                    return null;
                }

                var createMethod = FastDelegateCreateMethod.MakeGenericMethod(typeof(TDelegate));
                return createMethod.Invoke(null, new object[] { typeof(TouristAI), methodName, true }) as TDelegate;
            }
            catch (Exception ex)
            {
                Log.Warning($"The 'Real Time' mod failed to create a delegate for TouristAI.{methodName}: {ex}");
                return null;
            }
        }
    }
}
