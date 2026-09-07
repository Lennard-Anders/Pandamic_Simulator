namespace RealTimeTests.Pandemic
{
    using NUnit.Framework;
    using RealTime.Pandemic;

    public sealed class DistrictMetricProjectionTests
    {
        [Test]
        public void DistrictMapPreservesMetricUnitsAndDoesNotSubstituteCityShare()
        {
            var district = new PandemicDistrictSnapshot { ResidentCount = 200, InfectedResidents = 10, InfectedPercent = 5, DetectedPrevalencePercent = 2, IncidencePer100000 = 500, SecondaryTransmissions = 17 };
            Assert.That(PandemicDistrictMetricProjection.Value(district, PandemicXRayMetric.DistrictActiveInfections), Is.EqualTo(10));
            Assert.That(PandemicDistrictMetricProjection.Value(district, PandemicXRayMetric.DistrictPrevalence), Is.EqualTo(5));
            Assert.That(PandemicDistrictMetricProjection.Value(district, PandemicXRayMetric.DistrictDetectedPrevalence), Is.EqualTo(2));
            Assert.That(PandemicDistrictMetricProjection.Value(district, PandemicXRayMetric.DistrictIncidence), Is.EqualTo(500));
            Assert.That(PandemicDistrictMetricProjection.Value(district, PandemicXRayMetric.DistrictTransmissions), Is.EqualTo(17));
        }
    }
}
