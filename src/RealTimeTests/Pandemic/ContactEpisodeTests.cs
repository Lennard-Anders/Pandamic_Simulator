namespace RealTimeTests.Pandemic
{
    using System;
    using System.Collections.Generic;
    using NUnit.Framework;
    using RealTime.Pandemic;

    [TestFixture]
    public sealed class ContactEpisodeTests
    {
        private static readonly DateTime Start = new DateTime(2030, 1, 1);

        [TestCase(1)] [TestCase(4)]
        public void ConsecutiveStepsAccumulateWithoutRetainingHistory(int steps)
        {
            var output = new List<ContactEpisode>();
            var tracker = new ContactEpisodeTracker(output.Add);
            for (int i = 1; i <= steps; i++) Step(tracker, i, Contact(i));
            Assert.That(output, Is.Empty);
            Assert.That(tracker.ActiveCount, Is.EqualTo(1));
            tracker.Complete();
            Assert.That(output.Count, Is.EqualTo(1));
            Assert.That(output[0].DurationMinutes, Is.EqualTo(steps * 5));
            Assert.That(output[0].EpidemiologicalSteps, Is.EqualTo(steps));
            Assert.That(tracker.ActiveCount, Is.Zero);
        }

        [TestCase("missing")] [TestCase("context")] [TestCase("building")]
        [TestCase("vehicle")] [TestCase("traceability")]
        public void DiscontinuitiesCloseEpisodes(string change)
        {
            var output = new List<ContactEpisode>();
            var tracker = new ContactEpisodeTracker(output.Add);
            Step(tracker, 1, Contact(1));
            var next = Contact(2);
            if (change == "context") next.Context = PhysicalContactContext.School;
            if (change == "building") next.BuildingId = 2;
            if (change == "vehicle") next.VehicleId = 2;
            if (change == "traceability") next.TraceableByApp = true;
            Step(tracker, 2, change == "missing" ? null : next);
            Assert.That(output.Count, Is.EqualTo(1));
            Assert.That(output[0].DurationMinutes, Is.EqualTo(5));
            tracker.Complete();
            Assert.That(output.Count, Is.EqualTo(change == "missing" ? 1 : 2));
        }

        [Test]
        public void ResetClearsActiveStateAndRestartsDeterministicIds()
        {
            var output = new List<ContactEpisode>();
            var tracker = new ContactEpisodeTracker(output.Add);
            Step(tracker, 1, Contact(1));
            tracker.Reset();
            Step(tracker, 1, Contact(1));
            tracker.Complete();
            tracker.Complete();
            Assert.That(output.Count, Is.EqualTo(1));
            Assert.That(output[0].EpisodeId, Is.EqualTo(1));
        }

        [Test]
        public void CompletedEpisodesFollowCreationOrder()
        {
            var output = new List<ContactEpisode>();
            var tracker = new ContactEpisodeTracker(output.Add);
            tracker.BeginStep(Start.AddMinutes(5));
            for (uint id = 10; id > 1; id--)
            {
                var contact = Contact(1);
                contact.CitizenB = id;
                tracker.Observe(contact, 0);
            }
            tracker.EndStep();
            tracker.Complete();
            for (int i = 0; i < output.Count; i++) Assert.That(output[i].EpisodeId, Is.EqualTo(i + 1));
        }

        private static void Step(ContactEpisodeTracker tracker, int step, PhysicalContactEvent contact)
        {
            tracker.BeginStep(Start.AddMinutes(step * 5));
            if (contact != null) tracker.Observe(contact, 0);
            tracker.EndStep();
        }

        private static PhysicalContactEvent Contact(int step) => new PhysicalContactEvent
        {
            CitizenA = 1, CitizenB = 2, StartTime = Start.AddMinutes((step - 1) * 5),
            EndTime = Start.AddMinutes(step * 5), DurationMinutes = 5,
            Context = PhysicalContactContext.Workplace, BuildingId = 1,
        };
    }
}
