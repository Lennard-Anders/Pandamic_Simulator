namespace RealTime.Pandemic
{
    using System;
    using UnityEngine;

    internal enum PandemicLifecycleState
    {
        Dormant,
        Running,
        Finished,
    }

    internal enum PandemicXRayMode
    {
        Off,
        LivePositions,
        HomeLocations,
    }

    internal enum PandemicInfectionOriginCategory
    {
        InitialSeed,
        ResidentialHome,
        WorkplaceOfficeIndustry,
        SchoolUniversity,
        Healthcare,
        CommercialLeisureTourism,
        Bus,
        Tram,
        Metro,
        Train,
        ShipFerry,
        Plane,
        Taxi,
        CarOtherVehicle,
        OutdoorStreet,
        StopPlatform,
        OtherUnknown,
    }

    internal enum PandemicLockdownFamily
    {
        Education,
        PublicTransport,
        Commercial,
        LeisureTourismParks,
        Office,
        IndustryPlayerIndustry,
        GovernmentOtherPublic,
        EssentialServices,
        Healthcare,
    }

    internal enum PandemicPolicyMarkerType
    {
        Masks,
        Lockdown,
    }

    internal sealed class PandemicInfectionOriginInfo
    {
        public PandemicInfectionOriginCategory Category { get; set; }

        public InfectionType LegacyType { get; set; }

        public ushort BuildingId { get; set; }

        public ushort VehicleId { get; set; }

        public ItemClass.Service BuildingService { get; set; }

        public ItemClass.SubService BuildingSubService { get; set; }

        public Vector3 Position { get; set; }
    }

    internal static class PandemicTaxonomy
    {
        public static PandemicInfectionOriginCategory GetBuildingOriginCategory(ItemClass.Service service, ItemClass.SubService subService)
        {
            switch (service)
            {
                case ItemClass.Service.Residential:
                    return PandemicInfectionOriginCategory.ResidentialHome;

                case ItemClass.Service.Office:
                case ItemClass.Service.Industrial:
                case ItemClass.Service.PlayerIndustry:
                    return PandemicInfectionOriginCategory.WorkplaceOfficeIndustry;

                case ItemClass.Service.Education:
                case ItemClass.Service.PlayerEducation:
                    return PandemicInfectionOriginCategory.SchoolUniversity;

                case ItemClass.Service.HealthCare:
                    return PandemicInfectionOriginCategory.Healthcare;

                case ItemClass.Service.PublicTransport:
                    return PandemicInfectionOriginCategory.StopPlatform;

                case ItemClass.Service.Commercial:
                case ItemClass.Service.Tourism:
                case ItemClass.Service.Monument:
                case ItemClass.Service.Museums:
                case ItemClass.Service.VarsitySports:
                    return PandemicInfectionOriginCategory.CommercialLeisureTourism;

                case ItemClass.Service.Beautification:
                    return subService == ItemClass.SubService.BeautificationParks
                        ? PandemicInfectionOriginCategory.CommercialLeisureTourism
                        : PandemicInfectionOriginCategory.OtherUnknown;

                default:
                    return PandemicInfectionOriginCategory.OtherUnknown;
            }
        }

        public static PandemicLockdownFamily GetBuildingFamily(ItemClass.Service service, ItemClass.SubService subService)
        {
            switch (service)
            {
                case ItemClass.Service.Education:
                case ItemClass.Service.PlayerEducation:
                    return PandemicLockdownFamily.Education;

                case ItemClass.Service.PublicTransport:
                    return PandemicLockdownFamily.PublicTransport;

                case ItemClass.Service.Commercial:
                    return PandemicLockdownFamily.Commercial;

                case ItemClass.Service.Tourism:
                case ItemClass.Service.Monument:
                case ItemClass.Service.Museums:
                case ItemClass.Service.VarsitySports:
                    return PandemicLockdownFamily.LeisureTourismParks;

                case ItemClass.Service.Beautification:
                    return subService == ItemClass.SubService.BeautificationParks
                        ? PandemicLockdownFamily.LeisureTourismParks
                        : PandemicLockdownFamily.GovernmentOtherPublic;

                case ItemClass.Service.Office:
                    return PandemicLockdownFamily.Office;

                case ItemClass.Service.Industrial:
                case ItemClass.Service.PlayerIndustry:
                    return PandemicLockdownFamily.IndustryPlayerIndustry;

                case ItemClass.Service.HealthCare:
                    return PandemicLockdownFamily.Healthcare;

                case ItemClass.Service.Electricity:
                case ItemClass.Service.Water:
                case ItemClass.Service.Garbage:
                case ItemClass.Service.FireDepartment:
                case ItemClass.Service.PoliceDepartment:
                case ItemClass.Service.Disaster:
                    return PandemicLockdownFamily.EssentialServices;

                default:
                    return PandemicLockdownFamily.GovernmentOtherPublic;
            }
        }

        public static PandemicInfectionOriginCategory GetVehicleOriginCategory(ushort vehicleId)
        {
            if (vehicleId == 0 || VehicleManager.instance == null)
            {
                return PandemicInfectionOriginCategory.CarOtherVehicle;
            }

            VehicleInfo info = VehicleManager.instance.m_vehicles.m_buffer[vehicleId].Info;
            string typeName = info?.m_vehicleAI?.GetType().Name ?? string.Empty;
            if (typeName.IndexOf("Bus", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return PandemicInfectionOriginCategory.Bus;
            }

            if (typeName.IndexOf("Tram", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return PandemicInfectionOriginCategory.Tram;
            }

            if (typeName.IndexOf("Metro", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return PandemicInfectionOriginCategory.Metro;
            }

            if (typeName.IndexOf("Train", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return PandemicInfectionOriginCategory.Train;
            }

            if (typeName.IndexOf("Ship", StringComparison.OrdinalIgnoreCase) >= 0
                || typeName.IndexOf("Ferry", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return PandemicInfectionOriginCategory.ShipFerry;
            }

            if (typeName.IndexOf("Plane", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return PandemicInfectionOriginCategory.Plane;
            }

            if (typeName.IndexOf("Taxi", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return PandemicInfectionOriginCategory.Taxi;
            }

            return PandemicInfectionOriginCategory.CarOtherVehicle;
        }

        public static string GetOriginCategoryLabel(PandemicInfectionOriginCategory category)
        {
            switch (category)
            {
                case PandemicInfectionOriginCategory.InitialSeed:
                    return "Initial seed";
                case PandemicInfectionOriginCategory.ResidentialHome:
                    return "Residential/Home";
                case PandemicInfectionOriginCategory.WorkplaceOfficeIndustry:
                    return "Workplace/Office/Industry";
                case PandemicInfectionOriginCategory.SchoolUniversity:
                    return "School/University";
                case PandemicInfectionOriginCategory.Healthcare:
                    return "Healthcare";
                case PandemicInfectionOriginCategory.CommercialLeisureTourism:
                    return "Commercial/Leisure/Tourism";
                case PandemicInfectionOriginCategory.Bus:
                    return "Bus";
                case PandemicInfectionOriginCategory.Tram:
                    return "Tram";
                case PandemicInfectionOriginCategory.Metro:
                    return "Metro";
                case PandemicInfectionOriginCategory.Train:
                    return "Train";
                case PandemicInfectionOriginCategory.ShipFerry:
                    return "Ship/Ferry";
                case PandemicInfectionOriginCategory.Plane:
                    return "Plane";
                case PandemicInfectionOriginCategory.Taxi:
                    return "Taxi";
                case PandemicInfectionOriginCategory.CarOtherVehicle:
                    return "Car/Other vehicle";
                case PandemicInfectionOriginCategory.OutdoorStreet:
                    return "Outdoor/Street";
                case PandemicInfectionOriginCategory.StopPlatform:
                    return "Stop/Platform";
                default:
                    return "Other/Unknown";
            }
        }

        public static string GetLockdownFamilyLabel(PandemicLockdownFamily family)
        {
            switch (family)
            {
                case PandemicLockdownFamily.Education:
                    return "Education";
                case PandemicLockdownFamily.PublicTransport:
                    return "Public Transport";
                case PandemicLockdownFamily.Commercial:
                    return "Commercial";
                case PandemicLockdownFamily.LeisureTourismParks:
                    return "Leisure/Tourism/Parks";
                case PandemicLockdownFamily.Office:
                    return "Office";
                case PandemicLockdownFamily.IndustryPlayerIndustry:
                    return "Industry/Player Industry";
                case PandemicLockdownFamily.GovernmentOtherPublic:
                    return "Government/Other Public";
                case PandemicLockdownFamily.EssentialServices:
                    return "Essential Services";
                case PandemicLockdownFamily.Healthcare:
                    return "Healthcare";
                default:
                    return family.ToString();
            }
        }

        public static bool IsProtectedFamily(PandemicLockdownFamily family) => family == PandemicLockdownFamily.Healthcare;
    }
}
