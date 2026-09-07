namespace RealTime.Pandemic
{
    /// <summary>Percent-based automatic policy signals. Zero preserves the legacy idealized experiment.</summary>
    public enum PolicyTriggerMetric
    {
        IdealizedTruePrevalence,
        DetectedPrevalence,
        TestPositivityRate,
        HospitalOccupancy,
    }
}
