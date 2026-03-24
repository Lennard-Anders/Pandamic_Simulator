// <copyright file="PublicTransportAIPatch.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.GameConnection.Patches
{
    using System.Reflection;
    using RealTime.Pandemic;
    using SkyTools.Patching;

    internal static class PublicTransportAIPatch
    {
        public static IPatch DepotCreateVehicle { get; } = new DepotAI_CreateVehicle();

        public static IPatch DepotStartTransfer { get; } = new DepotAI_StartTransfer();

        public static IPatch BusArriveAtTarget { get; } = new VehicleAI_ArriveAtTarget<BusAI>();

        public static IPatch BusArriveAtSource { get; } = new VehicleAI_ArriveAtSource<BusAI>();

        public static IPatch TramArriveAtTarget { get; } = new VehicleAI_ArriveAtTarget<TramAI>();

        public static IPatch TramArriveAtSource { get; } = new VehicleAI_ArriveAtSource<TramAI>();

        public static IPatch TrolleybusArriveAtTarget { get; } = new VehicleAI_ArriveAtTarget<TrolleybusAI>();

        public static IPatch TrolleybusArriveAtSource { get; } = new VehicleAI_ArriveAtSource<TrolleybusAI>();

        public static IPatch PassengerTrainArriveAtTarget { get; } = new VehicleAI_ArriveAtTarget<PassengerTrainAI>();

        public static IPatch PassengerTrainArriveAtSource { get; } = new VehicleAI_ArriveAtSource<PassengerTrainAI>();

        public static IPatch PassengerShipArriveAtTarget { get; } = new VehicleAI_ArriveAtTarget<PassengerShipAI>();

        public static IPatch PassengerShipArriveAtSource { get; } = new VehicleAI_ArriveAtSource<PassengerShipAI>();

        public static IPatch PassengerPlaneArriveAtTarget { get; } = new VehicleAI_ArriveAtTarget<PassengerPlaneAI>();

        public static IPatch PassengerPlaneArriveAtSource { get; } = new VehicleAI_ArriveAtSource<PassengerPlaneAI>();

        public static IPatch PassengerHelicopterArriveAtTarget { get; } = new VehicleAI_ArriveAtTarget<PassengerHelicopterAI>();

        public static IPatch PassengerHelicopterArriveAtSource { get; } = new VehicleAI_ArriveAtSource<PassengerHelicopterAI>();

        public static IPatch CableCarArriveAtTarget { get; } = new VehicleAI_ArriveAtTarget<CableCarAI>();

        public static IPatch CableCarArriveAtSource { get; } = new VehicleAI_ArriveAtSource<CableCarAI>();

        public static IPatch TaxiArriveAtTarget { get; } = new VehicleAI_ArriveAtTarget<TaxiAI>();

        public static IPatch TaxiArriveAtSource { get; } = new VehicleAI_ArriveAtSource<TaxiAI>();

        private sealed class DepotAI_CreateVehicle : PatchBase
        {
            protected override MethodInfo GetMethod() =>
                typeof(DepotAI).GetMethod(
                    "CreateVehicle",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[]
                    {
                        typeof(ushort),
                        typeof(Building).MakeByRefType(),
                        typeof(VehicleInfo),
                        typeof(TransferManager.TransferReason),
                        typeof(TransferManager.TransferOffer),
                    },
                    new ParameterModifier[0]);

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Redundancy", "RCS1213", Justification = "Harmony patch")]
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming Rules", "SA1313", Justification = "Harmony patch")]
            private static bool Prefix(ushort buildingID, VehicleInfo vehicleInfo)
            {
                PandemicManager manager = PandemicManager.Instance;
                return !(manager?.ShouldBlockPublicTransportDepotSpawn(buildingID, vehicleInfo) ?? false);
            }
        }

        private sealed class DepotAI_StartTransfer : PatchBase
        {
            protected override MethodInfo GetMethod() =>
                typeof(DepotAI).GetMethod(
                    "StartTransfer",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[]
                    {
                        typeof(ushort),
                        typeof(Building).MakeByRefType(),
                        typeof(TransferManager.TransferReason),
                        typeof(TransferManager.TransferOffer),
                    },
                    new ParameterModifier[0]);

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Redundancy", "RCS1213", Justification = "Harmony patch")]
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming Rules", "SA1313", Justification = "Harmony patch")]
            private static bool Prefix(ushort buildingID)
            {
                PandemicManager manager = PandemicManager.Instance;
                return !(manager?.ShouldBlockPublicTransportDepotTransfer(buildingID) ?? false);
            }
        }

        private abstract class VehicleAI_ArrivalPatchBase<TAI> : PatchBase
            where TAI : VehicleAI
        {
            protected static MethodInfo GetArrivalMethod(string name) =>
                typeof(TAI).GetMethod(
                    name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(ushort), typeof(Vehicle).MakeByRefType() },
                    new ParameterModifier[0]);
        }

        private sealed class VehicleAI_ArriveAtTarget<TAI> : VehicleAI_ArrivalPatchBase<TAI>
            where TAI : VehicleAI
        {
            protected override MethodInfo GetMethod() => GetArrivalMethod("ArriveAtTarget");

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Redundancy", "RCS1213", Justification = "Harmony patch")]
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming Rules", "SA1313", Justification = "Harmony patch")]
            private static void Postfix(ushort vehicleID, ref Vehicle data, bool __result)
            {
                if (__result && vehicleID != 0)
                {
                    PandemicManager.Instance?.NotifyPublicTransportVehicleArrivedAtTarget(vehicleID, ref data);
                }
            }
        }

        private sealed class VehicleAI_ArriveAtSource<TAI> : VehicleAI_ArrivalPatchBase<TAI>
            where TAI : VehicleAI
        {
            protected override MethodInfo GetMethod() => GetArrivalMethod("ArriveAtSource");

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Redundancy", "RCS1213", Justification = "Harmony patch")]
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming Rules", "SA1313", Justification = "Harmony patch")]
            private static void Postfix(ushort vehicleID, ref Vehicle data, bool __result)
            {
                if (__result && vehicleID != 0)
                {
                    PandemicManager.Instance?.NotifyPublicTransportVehicleArrivedAtSource(vehicleID, ref data);
                }
            }
        }
    }
}
