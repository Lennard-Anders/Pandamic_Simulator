namespace RealTimeTests.Pandemic
{
    using System;
    using System.Linq;
    using NUnit.Framework;
    using RealTime.Config;
    using RealTime.Pandemic;

    public sealed class ContactPruningTests
    {
        [Test]
        public void PruningPreservesEligibleContactsAndEveryPhysicalEvent()
        {
            var manager = new ContactManager();
            manager.Init(new RealTimeConfig(true) { AppBasedContactTracingProbability = 100, BuildingContactTracingProbability = 100 });
            DateTime now = new DateTime(2030, 1, 20);
            TimeSpan lookback = TimeSpan.FromDays(7);
            manager.AddContact(1, 2, true, now - lookback - TimeSpan.FromMinutes(1));
            manager.AddContact(1, 3, true, now - lookback);
            manager.AddContact(1, 4, true, now - lookback + TimeSpan.FromMinutes(1));
            manager.AddContact(1, 5, true, now);
            uint[] eligible = manager.GetContactsForCitizen(1).Where(c => c.Value > now - lookback).Select(c => c.Key).OrderBy(id => id).ToArray();
            manager.PruneExpired(now, lookback);
            Assert.That(manager.GetContactsForCitizen(1).Keys.OrderBy(id => id), Is.EqualTo(eligible));
            Assert.That(manager.GetContactsForCitizen(2), Is.Null);
            Assert.That(manager.GetContactsForCitizen(3), Is.Null);
            Assert.That(manager.GetTrackedPairCount(), Is.EqualTo(2));
            Assert.That(manager.GetTotalRecordedContacts(), Is.EqualTo(4));
            Assert.That(manager.GetTotalTraceableContacts(), Is.EqualTo(4));
            Assert.That(manager.GetPhysicalContacts().Count, Is.EqualTo(4));
            manager.AddContact(1, 2, true, now.AddMinutes(1));
            Assert.That(manager.GetContactsForCitizen(2).ContainsKey(1), Is.True);
            Assert.That(manager.GetTrackedPairCount(), Is.EqualTo(3));
        }
    }
}
