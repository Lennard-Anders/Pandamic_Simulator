namespace RealTimeTests.Pandemic
{
    using System;
    using System.Linq;
    using NUnit.Framework;
    using RealTime.Config;
    using RealTime.Experiments;
    using RealTime.Pandemic;

    public sealed class ContactPersistenceTests
    {
        private static readonly DateTime Start = new DateTime(2030, 1, 1, 8, 0, 0);

        [Test]
        public void StableOccupancyPreservesPartnersUntilWindowExpires()
        {
            var config = new RealTimeConfig(true);
            var first = Partners(config, Start.AddMinutes(5), 42);
            CollectionAssert.AreEqual(first, Partners(config, Start.AddMinutes(60), 42));
            CollectionAssert.AreEqual(first, Partners(config, Start.AddMinutes(5), 42));
            Assert.That(Partners(config, Start.AddMinutes(65), 42), Is.Not.EqualTo(first));
            Assert.That(Partners(config, Start.AddMinutes(5), 43), Is.Not.EqualTo(first));
            Assert.That(first.Length, Is.EqualTo(500)); // Same cap, no skipped contacts.
        }

        [Test]
        public void LegacyAndZeroWindowPreserveExactSamplingKey()
        {
            var config = new RealTimeConfig(true) { ContactPersistenceModel = ContactPersistenceModel.LegacyPerStep };
            Assert.That(ContactPersistencePolicy.SamplingKey(config, PhysicalContactContext.Workplace, Start), Is.EqualTo(Start.Ticks));
            config.ContactPersistenceModel = ContactPersistenceModel.ContextWindows;
            config.ContactPersistenceMinutesWorkplace = 0;
            Assert.That(ContactPersistencePolicy.SamplingKey(config, PhysicalContactContext.Workplace, Start), Is.EqualTo(Start.Ticks));
            Assert.That(ContactPersistencePolicy.SamplingKey(config, PhysicalContactContext.Outdoor, Start), Is.EqualTo(Start.Ticks));
        }

        [Test]
        public void EveryContextWindowIsIndependentAndSnapshotRoundTrips()
        {
            var config = new RealTimeConfig(true);
            config.ContactPersistenceMinutesSchool = 120;
            config.ContactPersistenceMinutesUniversity = 90;
            config.ContactPersistenceMinutesWorkplace = 45;
            config.ContactPersistenceMinutesHealthcare = 30;
            config.ContactPersistenceMinutesCommercial = 10;
            config.ContactPersistenceMinutesLeisure = 20;
            config.ContactPersistenceMinutesTransit = 15;
            config.ContactPersistenceMinutesResidentialSharedArea = 25;
            var restored = new RealTimeConfig(true);
            ExperimentConfigurationMapper.ApplyScenario(ExperimentConfigurationMapper.CaptureScenario(config, false), restored);
            uint[] minutes = { 120, 90, 45, 30, 10, 20, 15, 25 };
            PhysicalContactContext[] contexts = { PhysicalContactContext.School, PhysicalContactContext.University,
                PhysicalContactContext.Workplace, PhysicalContactContext.Healthcare, PhysicalContactContext.Commercial,
                PhysicalContactContext.Leisure, PhysicalContactContext.PublicTransport, PhysicalContactContext.Other };
            for (int i = 0; i < contexts.Length; i++)
                Assert.That(ContactPersistencePolicy.SamplingKey(restored, contexts[i], Start, i == 7),
                    Is.EqualTo((Start.Ticks - 1) / (minutes[i] * TimeSpan.TicksPerMinute)));
        }

        [Test]
        public void SamplingNeverIncludesDepartedVehicleOccupant()
        {
            var sampler = new ContactSamplingEngine();
            var config = new RealTimeConfig(true);
            long key = ContactPersistencePolicy.SamplingKey(config, PhysicalContactContext.PublicTransport, Start.AddMinutes(5));
            var occupied = sampler.SampleBounded(new uint[] { 1, 2, 3, 4 }, 10, 42, key, 100001);
            Assert.That(occupied.Count, Is.EqualTo(6));
            var departed = sampler.SampleBounded(new uint[] { 1, 2, 3 }, 10, 42, key, 100001);
            Assert.That(departed.Any(p => p.CitizenA == 4 || p.CitizenB == 4), Is.False);
            Assert.That(departed.Count, Is.EqualTo(3));
        }

        private static string[] Partners(RealTimeConfig config, DateTime time, int seed) =>
            new ContactSamplingEngine().SampleBounded(Enumerable.Range(1, 100).Select(i => (uint)i), 10, seed,
                ContactPersistencePolicy.SamplingKey(config, PhysicalContactContext.Workplace, time), 7)
                .Select(p => p.CitizenA + ":" + p.CitizenB).ToArray();
    }
}
