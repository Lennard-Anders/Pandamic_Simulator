namespace RealTime.Experiments
{
    using System;
    using System.Globalization;
    using System.IO;
    using RealTime.Config;

    /// <summary>Planning heuristic, never an occupancy bound or scientific calibration.</summary>
    internal sealed class ContactStorageEstimate
    {
        internal const string FullRawWarning = "Full raw contact export records every physical contact at every epidemiological timestep and can generate tens or hundreds of gigabytes for large cities or long experiments.";
        internal double LowGB { get; private set; }
        internal double HighGB { get; private set; }
        internal string Category => HighGB < 0.1 ? "Low" : HighGB < 1 ? "Moderate" : HighGB < 20 ? "Large" : "Extreme";

        internal static ContactStorageEstimate Calculate(int population, double days, int runs, ExperimentScenarioSnapshot settings)
        {
            if (population < 0 || days < 0 || double.IsNaN(days) || double.IsInfinity(days) || runs < 1 || settings == null || settings.EpidemicStepMinutes == 0)
                throw new ArgumentOutOfRangeException(nameof(population));
            if (settings.ScientificContactExportMode == ScientificContactExportMode.SummaryOnly) return new ContactStorageEstimate();
            uint cap = Math.Max(Math.Max(settings.MaxContactsPerPersonPerStepSchool, settings.MaxContactsPerPersonPerStepWorkplace),
                Math.Max(Math.Max(settings.MaxContactsPerPersonPerStepCommercial, settings.MaxContactsPerPersonPerStepHealthcare),
                    Math.Max(settings.MaxContactsPerPersonPerStepTransit, settings.MaxContactsPerPersonPerStepResidentialSharedArea)));
            // Continuous capped occupancy, 150 uncompressed bytes/row; broad engineering compression/occupancy range.
            // Household/outdoor and occupancy turnover are not bounded by these caps.
            double rawGB = population * (double)cap / 2 * (1440d / settings.EpidemicStepMinutes) * days * runs * 150 / 1e9;
            double aggregation = 1;
            if (settings.ScientificContactExportMode == ScientificContactExportMode.Standard && settings.ContactPersistenceModel == ContactPersistenceModel.ContextWindows)
            {
                uint longest = Math.Max(settings.ContactPersistenceMinutesSchool, settings.ContactPersistenceMinutesWorkplace);
                aggregation = Math.Max(1, longest / (double)settings.EpidemicStepMinutes);
            }
            return new ContactStorageEstimate { LowGB = rawGB * 0.03 / aggregation, HighGB = rawGB * 1.0 };
        }

        internal string Describe() => string.Format(CultureInfo.InvariantCulture,
            "Estimated contact output: {0} (approximately {1:0.###}–{2:0.###} GB compressed; heuristic, not a bound).", Category, LowGB, HighGB);

        internal static long? AvailableBytes(string directory)
        {
            try { return new DriveInfo(Path.GetPathRoot(Path.GetFullPath(directory))).AvailableFreeSpace; }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
            catch (ArgumentException) { return null; }
            catch (NotSupportedException) { return null; }
        }
    }
}
