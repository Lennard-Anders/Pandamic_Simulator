namespace RealTime.Pandemic
{
    internal static class PandemicDistrictMetricProjection
    {
        internal static float Value(PandemicDistrictSnapshot district, PandemicXRayMetric metric)
        {
            switch (metric)
            {
                case PandemicXRayMetric.DistrictActiveInfections: return district.InfectedResidents;
                case PandemicXRayMetric.DistrictPrevalence: return district.InfectedPercent;
                case PandemicXRayMetric.DistrictDetectedPrevalence: return district.DetectedPrevalencePercent;
                case PandemicXRayMetric.DistrictIncidence: return district.IncidencePer100000;
                case PandemicXRayMetric.DistrictTransmissions: return district.SecondaryTransmissions;
                default: throw new System.ArgumentOutOfRangeException(nameof(metric));
            }
        }
    }
}
