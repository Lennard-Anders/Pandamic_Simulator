namespace RealTimeTests.Pandemic
{
    using NUnit.Framework;
    using RealTime.Pandemic;

    [TestFixture]
    public sealed class IsolationQuarantineEngineTests
    {
        [Test]
        public void OnlyTestedDoesNotIsolateBecauseOfSymptomsOrImplicitInfectionAge()
        {
            var engine = new IsolationQuarantineEngine();

            Assert.That(engine.ShouldIsolateCase(true, true, false, false), Is.False);
        }

        [Test]
        public void AvailablePositiveAndConfiguredPendingTestCanIsolate()
        {
            var engine = new IsolationQuarantineEngine();

            Assert.That(engine.ShouldIsolateCase(true, false, true, false), Is.True);
            Assert.That(engine.ShouldIsolateCase(true, false, false, true), Is.True);
        }

        [Test]
        public void InterventionDecisionCannotChangeDiseaseState()
        {
            var disease = new DiseaseStateEngine();
            disease.RegisterCitizen(1);
            var engine = new IsolationQuarantineEngine();

            Assert.That(engine.ShouldIsolateCase(false, true, false, false), Is.True);
            Assert.That(disease.GetState(1, new System.DateTime(2030, 1, 1)), Is.EqualTo(DiseaseState.Susceptible));
        }

        [Test]
        public void RestrictedCitizenAtHomeKeepsHouseholdButLosesExternalContacts()
        {
            var engine = new IsolationQuarantineEngine();

            Assert.That(engine.IsPhysicalContactAllowed(true, true, false, true, PhysicalContactContext.Household), Is.True);
            Assert.That(engine.IsPhysicalContactAllowed(true, true, false, false, PhysicalContactContext.Workplace), Is.False);
        }

        [Test]
        public void RestrictionDoesNotEraseContactWhileCitizenIsStillTravellingHome()
        {
            var engine = new IsolationQuarantineEngine();

            Assert.That(engine.IsPhysicalContactAllowed(true, false, false, false, PhysicalContactContext.PublicTransport), Is.True);
        }
    }
}
