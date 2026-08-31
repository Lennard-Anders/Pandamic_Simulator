namespace RealTimeTests.Pandemic
{
    using System;
    using NUnit.Framework;
    using RealTime.Pandemic;

    [TestFixture]
    public sealed class DiseaseStateEngineTests
    {
        private static readonly DateTime Start = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        [Test]
        public void SusceptibleToExposedSucceedsExactlyOnce()
        {
            var engine = CreateEngine(1);
            DiseaseCourse course = CreateCourse(1, DiseaseExposureKind.SecondaryTransmission);

            Assert.That(engine.TryExpose(course), Is.True);
            Assert.That(engine.TryExpose(CreateCourse(1, DiseaseExposureKind.SecondaryTransmission)), Is.False);
            Assert.That(engine.GetState(1, Start), Is.EqualTo(DiseaseState.Exposed));
            Assert.That(engine.SecondaryTransmissionCount, Is.EqualTo(1));
        }

        [Test]
        public void RecoveredCitizenCannotBeExposedAgain()
        {
            var engine = CreateEngine(1);
            Assert.That(engine.TryExpose(CreateCourse(1, DiseaseExposureKind.SecondaryTransmission)), Is.True);
            Assert.That(engine.GetState(1, Start.AddDays(2)), Is.EqualTo(DiseaseState.Infectious));
            Assert.That(engine.GetState(1, Start.AddDays(5)), Is.EqualTo(DiseaseState.PostInfectiousIll));
            Assert.That(engine.GetState(1, Start.AddDays(10)), Is.EqualTo(DiseaseState.Recovered));

            Assert.That(engine.TryExpose(CreateCourse(1, DiseaseExposureKind.SecondaryTransmission)), Is.False);
            Assert.That(engine.SecondaryTransmissionCount, Is.EqualTo(1));
        }

        [Test]
        public void DeadCitizenCannotBeExposedAgain()
        {
            var engine = CreateEngine(1);
            Assert.That(engine.TryExpose(CreateCourse(1, DiseaseExposureKind.SecondaryTransmission)), Is.True);
            Assert.That(engine.TryMarkDead(1, Start.AddDays(2)), Is.True);

            Assert.That(engine.TryExpose(CreateCourse(1, DiseaseExposureKind.SecondaryTransmission)), Is.False);
            Assert.That(engine.SecondaryTransmissionCount, Is.EqualTo(1));
        }

        [Test]
        public void InfectiousIntervalIsHalfOpen()
        {
            var engine = CreateEngine(1);
            engine.TryExpose(CreateCourse(1, DiseaseExposureKind.SecondaryTransmission));

            Assert.That(engine.IsInfectious(1, Start.AddDays(1).AddTicks(-1)), Is.False);
            Assert.That(engine.IsInfectious(1, Start.AddDays(1)), Is.True);
            Assert.That(engine.IsInfectious(1, Start.AddDays(4).AddTicks(-1)), Is.True);
            Assert.That(engine.IsInfectious(1, Start.AddDays(4)), Is.False);
            Assert.That(engine.GetState(1, Start.AddDays(4)), Is.EqualTo(DiseaseState.PostInfectiousIll));
        }

        [Test]
        public void ExposedRecoveredAndDeadNeverTransmit()
        {
            var engine = CreateEngine(1);
            engine.TryExpose(CreateCourse(1, DiseaseExposureKind.SecondaryTransmission));

            Assert.That(engine.IsInfectious(1, Start), Is.False);
            Assert.That(engine.IsInfectious(1, Start.AddDays(2)), Is.True);
            Assert.That(engine.IsInfectious(1, Start.AddDays(5)), Is.False);
            Assert.That(engine.IsInfectious(1, Start.AddDays(10)), Is.False);

            var deadEngine = CreateEngine(1);
            deadEngine.TryExpose(CreateCourse(1, DiseaseExposureKind.SecondaryTransmission));
            deadEngine.TryMarkDead(1, Start.AddDays(2));
            Assert.That(deadEngine.IsInfectious(1, Start.AddDays(2)), Is.False);
        }

        [Test]
        public void InitialSeedsAreSeparateFromSecondaryTransmissions()
        {
            var engine = CreateEngine(1, 2);
            engine.TryExpose(CreateCourse(1, DiseaseExposureKind.InitialSeed));
            engine.TryExpose(CreateCourse(2, DiseaseExposureKind.SecondaryTransmission));

            Assert.That(engine.InitialSeedCount, Is.EqualTo(1));
            Assert.That(engine.SecondaryTransmissionCount, Is.EqualTo(1));
            Assert.That(engine.ValidateInvariants(Start.AddDays(2)), Is.Empty);
        }

        [Test]
        public void SymptomIntervalIsHalfOpenAndIndependent()
        {
            DiseaseCourse course = CreateCourse(1, DiseaseExposureKind.InitialSeed);

            Assert.That(course.GetSymptomState(Start.AddDays(2).AddTicks(-1)), Is.EqualTo(SymptomState.PreSymptomatic));
            Assert.That(course.GetSymptomState(Start.AddDays(2)), Is.EqualTo(SymptomState.Symptomatic));
            Assert.That(course.GetSymptomState(Start.AddDays(5).AddTicks(-1)), Is.EqualTo(SymptomState.Symptomatic));
            Assert.That(course.GetSymptomState(Start.AddDays(5)), Is.EqualTo(SymptomState.PostSymptomatic));
        }

        [Test]
        public void ChronologicalPollingMayAdvanceAcrossSeveralValidBoundaries()
        {
            var engine = CreateEngine(1);
            engine.TryExpose(CreateCourse(1, DiseaseExposureKind.SecondaryTransmission));

            Assert.That(engine.GetState(1, Start.AddDays(10)), Is.EqualTo(DiseaseState.Recovered));
            Assert.That(engine.GetState(1, Start.AddDays(10)), Is.EqualTo(DiseaseState.Recovered));
        }

        [Test]
        public void BackdatedCourseStartsInResolvedCurrentStateAndCanDiePostInfectiously()
        {
            var engine = CreateEngine(1);
            DiseaseCourse course = CreateCourse(1, DiseaseExposureKind.InitialSeed);

            Assert.That(engine.TryExpose(course, Start.AddDays(5)), Is.True);
            Assert.That(engine.GetState(1, Start.AddDays(5)), Is.EqualTo(DiseaseState.PostInfectiousIll));
            Assert.That(engine.TryMarkDead(1, Start.AddDays(5)), Is.True);
            Assert.That(engine.GetState(1, Start.AddDays(6)), Is.EqualTo(DiseaseState.Dead));
        }

        [Test]
        public void DeathAtRecoveryBoundaryIsRejected()
        {
            DiseaseCourse course = CreateCourse(1, DiseaseExposureKind.SecondaryTransmission);

            Assert.Throws<ArgumentOutOfRangeException>(() => course.ScheduleDeath(course.RecoveryTime));
        }

        [Test]
        public void FutureExposureCannotBeRegisteredEarly()
        {
            var engine = CreateEngine(1);
            DiseaseCourse course = CreateCourse(1, DiseaseExposureKind.SecondaryTransmission);

            Assert.Throws<ArgumentOutOfRangeException>(() => engine.TryExpose(course, Start.AddTicks(-1)));
            Assert.That(engine.GetState(1, Start), Is.EqualTo(DiseaseState.Susceptible));
        }

        private static DiseaseStateEngine CreateEngine(params uint[] citizenIds)
        {
            var engine = new DiseaseStateEngine();
            foreach (uint citizenId in citizenIds)
            {
                engine.RegisterCitizen(citizenId);
            }

            return engine;
        }

        private static DiseaseCourse CreateCourse(uint citizenId, DiseaseExposureKind kind)
        {
            return new DiseaseCourse(
                citizenId,
                Start,
                Start.AddDays(1),
                Start.AddDays(4),
                Start.AddDays(2),
                Start.AddDays(5),
                Start.AddDays(7),
                true,
                kind);
        }
    }
}
