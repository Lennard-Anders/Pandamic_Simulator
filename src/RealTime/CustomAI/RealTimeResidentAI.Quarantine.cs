// <copyright file="RealTimeResidentAI.Home.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.CustomAI
{
    using System;
    using System.Collections.Generic;
    using RealTime.Pandemic;
    using SkyTools.Tools;
    using UnityEngine;

    internal sealed partial class RealTimeResidentAI<TAI, TCitizen>
    {
        SimulationManager simulation = null;

        private void DoScheduledQuarantine(ref CitizenSchedule schedule, TAI instance, uint citizenId, ref TCitizen citizen)
        {
            ushort homeBuilding = CitizenProxy.GetHomeBuilding(ref citizen);
            if (homeBuilding == 0)
            {
                Log.Debug(LogCategory.State, $"{GetCitizenDesc(citizenId, ref citizen)} is currently homeless. Cannot move home, waiting for the next opportunity");
                return;
            }

            ushort currentBuilding = CitizenProxy.GetCurrentBuilding(ref citizen);
            CitizenProxy.RemoveFlags(ref citizen, Citizen.Flags.Evacuating);

#if DEBUG
            string logEntry = $"{GetCitizenDesc(citizenId, ref citizen)} is going from {currentBuilding} back home";
#else
            const string logEntry = null;
#endif

            if (residentAI.StartMoving(instance, citizenId, ref citizen, currentBuilding, homeBuilding))
            {
                CitizenProxy.SetVisitPlace(ref citizen, citizenId, 0);
                schedule.Schedule(ResidentState.Unknown);
                Log.Debug(LogCategory.Movement, TimeInfo.Now, logEntry);
            }
            else
            {
                Log.Debug(LogCategory.Movement, TimeInfo.Now, $"{GetCitizenDesc(citizenId, ref citizen)} wanted to go home from {currentBuilding} but can't, waiting for the next opportunity");
            }
        }

        private bool RescheduleInQuarantine(ref CitizenSchedule schedule, uint citizenId, ref TCitizen citizen)
        {
            if (simulation == null)
            {
                simulation = GameObject.Find("SimulationManager").GetComponent<SimulationManager>();
            }

            if (!QuarantineManager.Instance.IsInQuarantine(citizenId, simulation.m_currentGameTime) && !QuarantineManager.Instance.IsInProphylacticQuarantine(citizenId, simulation.m_currentGameTime))
            {
                schedule.Schedule(ResidentState.Unknown);
                return true;
            } else
            {
                return false;
            }
        }

        private bool IsShoppingAllowed(bool goodsNeeded)
        {
            return !(QuarantineManager.Instance.InLockDown
                && (Config.LockdownBehavior == RealTime.Config.LockdownBehavior.Work || Config.LockdownBehavior == RealTime.Config.LockdownBehavior.Full)) || goodsNeeded;
        }

        private bool IsWorkAllowed()
        {
            return !(QuarantineManager.Instance.InLockDown
                && Config.LockdownBehavior == RealTime.Config.LockdownBehavior.Full);
        }

        private bool IsRelaxingAllowed()
        {
            return !(QuarantineManager.Instance.InLockDown
                && (Config.LockdownBehavior == RealTime.Config.LockdownBehavior.Work || Config.LockdownBehavior == RealTime.Config.LockdownBehavior.Full)) ;
        }

        private bool ShouldBeInQuarantine(uint citizenID)
        {
            var manager = PandemicManager.Instance;
            return manager != null && manager.ShouldBeInQuarantine(citizenID);
        }
    }
}
