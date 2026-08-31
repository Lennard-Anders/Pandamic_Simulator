namespace RealTimeTests.Pandemic
{
    using System;
    using RealTime.Pandemic;
    using NUnit.Framework;

    public sealed class ContactEngineTests
    {
        private static readonly DateTime StepOne = new DateTime(2040, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        [Test]
        public void PhysicalContactIsCanonicalSymmetricAndIndependentOfTracing()
        {
            var contacts = new ContactEngine();
            bool created;
            PhysicalContactEvent physical = contacts.Record(Request(9u, 2u, StepOne), out created);
            var tracing = new ContactTracingEngine(new ContactTracingPolicy
            {
                AppAdoptionPercent = 0d,
                ManualTraceabilityPercent = 0d,
            }, 1);

            ContactTraceability result = tracing.Evaluate(physical);

            Assert.That(created, Is.True);
            Assert.That(physical.CitizenA, Is.EqualTo(2u));
            Assert.That(physical.CitizenB, Is.EqualTo(9u));
            Assert.That(result.IsTraceable, Is.False);
            Assert.That(contacts.GetEvents(), Has.Count.EqualTo(1));
        }

        [Test]
        public void SamePairInSameStepIsDeduplicatedButHistoryAcrossStepsIsPreserved()
        {
            var contacts = new ContactEngine();
            bool firstCreated;
            bool duplicateCreated;
            bool laterCreated;
            PhysicalContactEvent first = contacts.Record(Request(1u, 2u, StepOne), out firstCreated);
            PhysicalContactEvent duplicate = contacts.Record(Request(2u, 1u, StepOne), out duplicateCreated);
            contacts.Record(Request(1u, 2u, StepOne.AddMinutes(5d)), out laterCreated);

            Assert.That(firstCreated, Is.True);
            Assert.That(duplicateCreated, Is.False);
            Assert.That(duplicate.ContactId, Is.EqualTo(first.ContactId));
            Assert.That(laterCreated, Is.True);
            Assert.That(contacts.GetEvents(), Has.Count.EqualTo(2));
        }

        [Test]
        public void AppRequiresBothUsersAndManualTracingDoesNotDependOnApp()
        {
            var contacts = new ContactEngine();
            bool created;
            PhysicalContactEvent physical = contacts.Record(Request(1u, 2u, StepOne), out created);

            var app = new ContactTracingEngine(new ContactTracingPolicy
            {
                AppAdoptionPercent = 100d,
                ManualTraceabilityPercent = 0d,
            }, 1);
            ContactTraceability appResult = app.Evaluate(physical);
            Assert.That(appResult.TraceableByApp, Is.True);
            Assert.That(appResult.TraceableByManual, Is.False);

            var manual = new ContactTracingEngine(new ContactTracingPolicy
            {
                AppAdoptionPercent = 0d,
                ManualTraceabilityPercent = 100d,
            }, 1);
            ContactTraceability manualResult = manual.Evaluate(physical);
            Assert.That(manualResult.TraceableByApp, Is.False);
            Assert.That(manualResult.TraceableByManual, Is.True);
        }

        [Test]
        public void AppOnlyAndManualOnlyCitizensDoNotFormAMixedTracingMechanism()
        {
            var engine = new ContactTracingEngine(new ContactTracingPolicy
            {
                AppAdoptionPercent = 50d,
                ManualTraceabilityPercent = 50d,
            }, 77);
            uint appOnly = FindCitizen(engine, usesApp: true, usesManual: false);
            uint manualOnly = FindCitizen(engine, usesApp: false, usesManual: true);
            var contacts = new ContactEngine();
            bool created;
            PhysicalContactEvent physical = contacts.Record(Request(appOnly, manualOnly, StepOne), out created);

            ContactTraceability result = engine.Evaluate(physical);

            Assert.That(result.TraceableByApp, Is.False);
            Assert.That(result.TraceableByManual, Is.False);
        }

        private static uint FindCitizen(ContactTracingEngine engine, bool usesApp, bool usesManual)
        {
            for (uint citizen = 1u; citizen < 100000u; ++citizen)
            {
                if (engine.UsesApp(citizen) == usesApp && engine.IsManuallyTraceable(citizen) == usesManual)
                {
                    return citizen;
                }
            }

            Assert.Fail("No deterministic citizen trait fixture was found.");
            return 0u;
        }

        private static PhysicalContactRequest Request(uint citizenA, uint citizenB, DateTime endTime)
        {
            return new PhysicalContactRequest
            {
                CitizenA = citizenA,
                CitizenB = citizenB,
                EndTime = endTime,
                DurationMinutes = 5d,
                Context = PhysicalContactContext.Workplace,
                BuildingId = 12,
            };
        }
    }
}
