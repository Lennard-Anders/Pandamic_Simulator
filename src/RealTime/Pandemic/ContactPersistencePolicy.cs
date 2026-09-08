namespace RealTime.Pandemic
{
    using System;
    using RealTime.Config;

    /// <summary>Fixed clock windows; unchanged occupancy produces identical partners within a window.</summary>
    internal static class ContactPersistencePolicy
    {
        internal static long SamplingKey(RealTimeConfig config, PhysicalContactContext context,
            DateTime endTime, bool residentialSharedArea = false)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (config.ContactPersistenceModel == ContactPersistenceModel.LegacyPerStep) return endTime.Ticks;
            if (config.ContactPersistenceModel != ContactPersistenceModel.ContextWindows)
                throw new ArgumentOutOfRangeException(nameof(config), "Unknown contact persistence model.");
            uint minutes;
            if (residentialSharedArea) minutes = config.ContactPersistenceMinutesResidentialSharedArea;
            else switch (context)
            {
                case PhysicalContactContext.School: minutes = config.ContactPersistenceMinutesSchool; break;
                case PhysicalContactContext.University: minutes = config.ContactPersistenceMinutesUniversity; break;
                case PhysicalContactContext.Workplace: minutes = config.ContactPersistenceMinutesWorkplace; break;
                case PhysicalContactContext.Healthcare: minutes = config.ContactPersistenceMinutesHealthcare; break;
                case PhysicalContactContext.Commercial: minutes = config.ContactPersistenceMinutesCommercial; break;
                case PhysicalContactContext.Leisure: minutes = config.ContactPersistenceMinutesLeisure; break;
                case PhysicalContactContext.PublicTransport: minutes = config.ContactPersistenceMinutesTransit; break;
                // Household uses actual household all-pairs; outdoor uses spatial proximity.
                default: return endTime.Ticks;
            }
            if (minutes > 1440) throw new ArgumentOutOfRangeException(nameof(config), "Contact persistence windows must not exceed 1440 minutes.");
            if (minutes == 0) return endTime.Ticks;
            // Attribute the interval ending exactly at a boundary to the preceding window.
            // Absolute simulated clock anchoring makes save/reload and start-time shifts unambiguous.
            return (endTime.Ticks - 1) / (minutes * TimeSpan.TicksPerMinute);
        }
    }
}
