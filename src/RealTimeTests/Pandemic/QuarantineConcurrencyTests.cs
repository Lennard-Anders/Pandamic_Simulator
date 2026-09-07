namespace RealTimeTests.Pandemic
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using RealTime.Pandemic;

    public sealed class QuarantineConcurrencyTests
    {
        [Test]
        public void OverlappingRestrictionsCountOnceAndReturnedListsAreDetached()
        {
            var manager = new QuarantineManager();
            DateTime now = new DateTime(2030, 1, 1);
            manager.AddCitizenInIsolation(1, now);
            manager.AddContactQuarantine(1, now);
            manager.AddPendingTestQuarantine(1, now);
            manager.AddContactQuarantine(2, now);
            manager.AddPendingTestQuarantine(3, now);
            manager.AddCitizenInIsolation(4, now);
            var snapshot = manager.GetContactQuarantinedCitizens();
            Assert.That(manager.CitizensInQuarantine(), Is.EqualTo(4));
            Assert.That(manager.GetContactQuarantinedCount(now), Is.EqualTo(3));
            Assert.That(manager.GetIsolatedCount(now), Is.EqualTo(2));
            manager.Reset();
            Assert.That(snapshot.OrderBy(id => id), Is.EqualTo(new uint[] { 1, 2, 3 }));
            Assert.That(manager.CitizensInQuarantine(), Is.Zero);
        }

        [Test]
        public void ConcurrentDuplicateAssignmentsRecordExactlyOneStartAndExpiryPerCitizen()
        {
            var manager = new QuarantineManager();
            DateTime now = new DateTime(2030, 1, 1);
            int starts = 0, ends = 0;
            manager.SetInterventionSink(e =>
            {
                // A synchronous observer may query the manager reentrantly.
                manager.CitizensInQuarantine();
                if (e.Action == "Start") Interlocked.Increment(ref starts);
                if (e.Action == "End") Interlocked.Increment(ref ends);
            });
            Parallel.For(0, 4, worker =>
            {
                for (uint citizen = 1; citizen <= 256; citizen++)
                    manager.AddCitizenInIsolation(citizen, now);
            });
            Assert.That(starts, Is.EqualTo(256));
            Assert.That(manager.GetIsolatedCount(now.AddDays(9)), Is.EqualTo(256));
            Parallel.For(0, 4, worker => manager.GetIsolatedCount(now.AddDays(10)));
            Assert.That(ends, Is.EqualTo(256));
            Assert.That(manager.CitizensInQuarantine(), Is.Zero);
        }

        [Test]
        public void LiveReadersRemainSafeDuringRestrictionChangesAndExpiry()
        {
            var manager = new QuarantineManager();
            DateTime now = new DateTime(2030, 1, 1);
            using (var start = new ManualResetEventSlim(false))
            {
                Task writer = Task.Run(() =>
                {
                    start.Wait();
                    for (uint i = 0; i < 5000; i++)
                    {
                        uint id = i % 128;
                        manager.AddCitizenInIsolation(id, now);
                        manager.AddContactQuarantine(id, now);
                        manager.AddPendingTestQuarantine(id, now);
                        manager.RemoveAllRestrictions(id, now, "TestRemoval");
                    }
                });
                Task reader = Task.Run(() =>
                {
                    start.Wait();
                    for (int i = 0; i < 5000; i++)
                    {
                        int count = manager.CitizensInQuarantine();
                        if (count < 0 || count > 128) throw new InvalidOperationException("Invalid union count");
                        manager.GetContactQuarantinedCitizens().ToArray();
                        manager.GetFollowingCount(now, false);
                        manager.IsRestrictedReadOnly((uint)(i % 128), now);
                        manager.GetContactQuarantinedCount(now.AddDays(10));
                    }
                });
                start.Set();
                Task.WaitAll(writer, reader);
            }
            Assert.That(manager.CitizensInQuarantine(), Is.Zero);
        }
    }
}
