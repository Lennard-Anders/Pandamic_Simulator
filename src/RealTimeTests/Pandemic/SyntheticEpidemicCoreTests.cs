namespace RealTimeTests.Pandemic
{
    using System;
    using NUnit.Framework;
    using RealTime.Config;
    using RealTime.Pandemic;

    [TestFixture]
    public sealed class SyntheticEpidemicCoreTests
    {
        private static readonly DateTime Start = new DateTime(2040, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        [Test]
        public void ScenarioAZeroProbabilityProducesNoSecondaryTransmission()
        {
            SyntheticEpidemicCore core = CreateCore(DiseaseState.Infectious);
            Assert.That(core.Step(Start.AddDays(2), Contact(0d)), Is.Zero);
            Assert.That(core.GetState(2u, Start.AddDays(2)), Is.EqualTo(DiseaseState.Susceptible));
            Assert.That(core.TransmissionEvents, Is.Empty);
        }

        [Test]
        public void ScenarioBExposedCannotTransmitAtOneHundredPercent()
        {
            SyntheticEpidemicCore core = CreateCore(DiseaseState.Exposed);
            Assert.That(core.Step(Start, Contact(1d)), Is.Zero);
            Assert.That(core.GetState(2u, Start), Is.EqualTo(DiseaseState.Susceptible));
        }

        [Test]
        public void ScenarioCInfectiousTransmitsExactlyOnce()
        {
            SyntheticEpidemicCore core = CreateCore(DiseaseState.Infectious);
            Assert.That(core.Step(Start.AddDays(2), Contact(1d)), Is.EqualTo(1));
            Assert.That(core.GetState(2u, Start.AddDays(2)), Is.EqualTo(DiseaseState.Exposed));
            Assert.That(core.TransmissionEvents, Has.Count.EqualTo(1));
            Assert.That(core.Step(Start.AddDays(2), Contact(1d)), Is.Zero);
            Assert.That(core.TransmissionEvents, Has.Count.EqualTo(1));
        }

        [Test]
        public void ScenarioDPostInfectiousCannotTransmit()
        {
            SyntheticEpidemicCore core = CreateCore(DiseaseState.PostInfectiousIll);
            Assert.That(core.Step(Start.AddDays(5), Contact(1d)), Is.Zero);
        }

        [Test]
        public void ScenarioERecoveredCannotBeReinfected()
        {
            var core = new SyntheticEpidemicCore(DiseaseProgressionEngineTests.CreatePolicy(PandemicDistributionType.Deterministic), 1, 2);
            core.RegisterCourse(Course(1u), Start.AddDays(2));
            core.RegisterCourse(Course(2u), Start.AddDays(8));
            Assert.That(core.Step(Start.AddDays(8), Contact(1d)), Is.Zero);
            Assert.That(core.GetState(2u, Start.AddDays(8)), Is.EqualTo(DiseaseState.Recovered));
        }

        [Test]
        public void ScenariosFAndGAllowHouseholdButBlockExternalIsolationContact()
        {
            SyntheticEpidemicCore household = CreateCore(DiseaseState.Infectious);
            SyntheticContact householdContact = Contact(1d)[0];
            householdContact.Context = PhysicalContactContext.Household;
            householdContact.CitizenAInterventionActive = true;
            householdContact.CitizenAAtHome = true;
            householdContact.CitizenBAtHome = true;
            Assert.That(household.Step(Start.AddDays(2), new[] { householdContact }), Is.EqualTo(1));

            SyntheticEpidemicCore workplace = CreateCore(DiseaseState.Infectious);
            SyntheticContact workplaceContact = Contact(1d)[0];
            workplaceContact.Context = PhysicalContactContext.Workplace;
            workplaceContact.CitizenAInterventionActive = true;
            workplaceContact.CitizenAAtHome = true;
            Assert.That(workplace.Step(Start.AddDays(2), new[] { workplaceContact }), Is.Zero);
        }

        private static SyntheticEpidemicCore CreateCore(DiseaseState sourceState)
        {
            var core = new SyntheticEpidemicCore(DiseaseProgressionEngineTests.CreatePolicy(PandemicDistributionType.Deterministic), 1, 2);
            DateTime time = sourceState == DiseaseState.Exposed
                ? Start
                : sourceState == DiseaseState.Infectious ? Start.AddDays(2) : Start.AddDays(5);
            core.RegisterCourse(Course(1u), time);
            core.RegisterSusceptible(2u);
            return core;
        }

        private static DiseaseCourse Course(uint citizenId)
        {
            return new DiseaseCourse(
                citizenId,
                Start,
                Start.AddDays(1),
                Start.AddDays(4),
                null,
                null,
                Start.AddDays(7),
                false,
                DiseaseExposureKind.InitialSeed);
        }

        private static SyntheticContact[] Contact(double probability)
        {
            return new[]
            {
                new SyntheticContact
                {
                    CitizenA = 1u,
                    CitizenB = 2u,
                    TransmissionProbability = probability,
                    Context = PhysicalContactContext.Workplace,
                },
            };
        }
    }
}
