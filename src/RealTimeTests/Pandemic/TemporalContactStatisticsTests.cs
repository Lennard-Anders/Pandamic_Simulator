namespace RealTimeTests.Pandemic
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;
    using RealTime.Config;
    using RealTime.Pandemic;

    internal sealed class TemporalContactStatisticsTests
    {
        [Test]
        public void OutdoorProximityBoundaryDepartureAndReturnProduceSeparateEpisodes()
        {
            var episodes = new List<ContactEpisode>();
            var tracker = new ContactEpisodeTracker(episodes.Add);
            var start = new DateTime(2030, 1, 1);
            var origin = new UnityEngine.Vector3(1, 2, 3);
            var positions = new[] { new UnityEngine.Vector3(1, 2, 4), new UnityEngine.Vector3(1, 2, 5),
                new UnityEngine.Vector3(1, 5, 3), new UnityEngine.Vector3(1, 2, 4) };
            for (int step = 0; step < positions.Length; step++)
            {
                DateTime end = start.AddMinutes((step + 1) * 5);
                tracker.BeginStep(end);
                if (PandemicManager.IsWithinOutdoorContactRange(origin, positions[step], 4, out float squaredDistance))
                    tracker.Observe(new PhysicalContactEvent { CitizenA = 1, CitizenB = 2,
                        StartTime = end.AddMinutes(-5), EndTime = end, DurationMinutes = 5,
                        Context = PhysicalContactContext.Outdoor, Distance = Math.Sqrt(squaredDistance) }, 0);
                tracker.EndStep();
            }
            tracker.Complete();
            Assert.That(episodes.Select(episode => episode.DurationMinutes), Is.EqualTo(new[] { 10d, 5d }));
            Assert.That(episodes[1].StartTime, Is.EqualTo(start.AddMinutes(15)));
        }

        [TestCase(PhysicalContactContext.School)]
        [TestCase(PhysicalContactContext.Workplace)]
        public void OccupiedEightHourGroupPreservesExposureAndReportsTurnover(PhysicalContactContext context)
        {
            var start = new DateTime(2030, 1, 1, 8, 0, 0);
            var config = new RealTimeConfig(true);
            var sampler = new ContactSamplingEngine();
            var episodes = new List<ContactEpisode>();
            var tracker = new ContactEpisodeTracker(episodes.Add);
            var ids = Enumerable.Range(1, 100).Select(id => (uint)id).ToArray();
            var pairs = new HashSet<ulong>();
            long steps = 0;
            for (int step = 1; step <= 96; step++)
            {
                DateTime end = start.AddMinutes(step * 5);
                tracker.BeginStep(end);
                foreach (var pair in sampler.SampleBounded(ids, 10, 42, ContactPersistencePolicy.SamplingKey(config, context, end), 7))
                {
                    tracker.Observe(new PhysicalContactEvent { CitizenA = pair.CitizenA, CitizenB = pair.CitizenB,
                        StartTime = end.AddMinutes(-5), EndTime = end, DurationMinutes = 5, Context = context, BuildingId = 7 }, 0);
                    pairs.Add(((ulong)Math.Min(pair.CitizenA, pair.CitizenB) << 32) | Math.Max(pair.CitizenA, pair.CitizenB));
                    steps++;
                }
                tracker.EndStep();
            }
            tracker.Complete();
            Assert.That(steps, Is.EqualTo(48000));
            Assert.That(episodes.Sum(episode => episode.EpidemiologicalSteps), Is.EqualTo(steps));
            Assert.That(episodes.Sum(episode => episode.DurationMinutes), Is.EqualTo(steps * 5));
            Assert.That(episodes.All(episode => episode.DurationMinutes >= 60), Is.True);
            Assert.That(tracker.ActiveCount, Is.Zero);
            TestContext.WriteLine("context={0},population=100,occupied_hours=8,raw_steps={1},episodes={2},unique_pairs={3},unique_per_person_day={4},episodes_per_person_day={5},episode_minutes_per_person_day={6},repeated_ratio={7}",
                context, steps, episodes.Count, pairs.Count, 2.0 * pairs.Count / 100, 2.0 * episodes.Count / 100,
                2.0 * episodes.Sum(episode => episode.DurationMinutes) / 100, (steps - pairs.Count) / (double)steps);
        }

        [TestCase(PhysicalContactContext.Household, 96)]
        [TestCase(PhysicalContactContext.PublicTransport, 6)]
        [TestCase(PhysicalContactContext.Outdoor, 2)]
        public void ObservedColocationEndsEpisodeAtDeparture(PhysicalContactContext context, int observedSteps)
        {
            // Scripted observations isolate the measurement lifecycle from Unity position/occupancy acquisition.
            var start = new DateTime(2030, 1, 1, 8, 0, 0);
            var episodes = new List<ContactEpisode>();
            var tracker = new ContactEpisodeTracker(episodes.Add);
            for (int step = 1; step <= observedSteps + 1; step++)
            {
                DateTime end = start.AddMinutes(step * 5);
                tracker.BeginStep(end);
                if (step <= observedSteps)
                    tracker.Observe(new PhysicalContactEvent { CitizenA = 1, CitizenB = 2,
                        StartTime = end.AddMinutes(-5), EndTime = end, DurationMinutes = 5, Context = context,
                        VehicleId = context == PhysicalContactContext.PublicTransport ? (ushort)7 : (ushort)0 }, 0);
                tracker.EndStep();
            }
            Assert.That(episodes.Count, Is.EqualTo(1));
            Assert.That(episodes[0].DurationMinutes, Is.EqualTo(observedSteps * 5));
            Assert.That(tracker.ActiveCount, Is.Zero);
            TestContext.WriteLine("scripted context={0},raw_steps={1},episodes=1,duration_minutes={2}", context, observedSteps, episodes[0].DurationMinutes);
        }
    }
}
