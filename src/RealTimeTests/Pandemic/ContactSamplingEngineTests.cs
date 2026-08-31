namespace RealTimeTests.Pandemic
{
    using System.Collections.Generic;
    using System.Linq;
    using RealTime.Pandemic;
    using NUnit.Framework;

    public sealed class ContactSamplingEngineTests
    {
        [Test]
        public void SamplingIsOrderIndependentBoundedAndDuplicateFree()
        {
            var engine = new ContactSamplingEngine();
            uint[] forward = Enumerable.Range(1, 20).Select(value => (uint)value).ToArray();
            uint[] reverse = forward.Reverse().ToArray();

            IList<ContactPair> first = engine.SampleBounded(forward, 4, 123, 99L, 7);
            IList<ContactPair> second = engine.SampleBounded(reverse, 4, 123, 99L, 7);

            Assert.That(PairKeys(second), Is.EqualTo(PairKeys(first)));
            Assert.That(PairKeys(first), Is.Unique);
            Dictionary<uint, int> counts = CountContacts(first);
            Assert.That(counts.Values.All(value => value <= 4), Is.True);
        }

        [Test]
        public void SamplingDoesNotMaterializeAllPairsForLargeContext()
        {
            var engine = new ContactSamplingEngine();
            uint[] citizens = Enumerable.Range(1, 1000).Select(value => (uint)value).ToArray();

            IList<ContactPair> pairs = engine.SampleBounded(citizens, 10, 1, 1L, 1);

            Assert.That(pairs, Has.Count.EqualTo(5000));
            Assert.That(pairs.Count, Is.LessThan(1000 * 999 / 2));
        }

        private static string[] PairKeys(IEnumerable<ContactPair> pairs)
        {
            return pairs.Select(pair => pair.CitizenA + ":" + pair.CitizenB).ToArray();
        }

        private static Dictionary<uint, int> CountContacts(IEnumerable<ContactPair> pairs)
        {
            var counts = new Dictionary<uint, int>();
            foreach (ContactPair pair in pairs)
            {
                counts[pair.CitizenA] = counts.ContainsKey(pair.CitizenA) ? counts[pair.CitizenA] + 1 : 1;
                counts[pair.CitizenB] = counts.ContainsKey(pair.CitizenB) ? counts[pair.CitizenB] + 1 : 1;
            }

            return counts;
        }
    }
}
