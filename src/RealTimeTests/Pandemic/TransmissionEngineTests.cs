namespace RealTimeTests.Pandemic
{
    using System;
    using System.Collections.Generic;
    using NUnit.Framework;
    using RealTime.Pandemic;

    [TestFixture]
    public sealed class TransmissionEngineTests
    {
        [Test]
        public void ZeroProbabilityNeverCreatesSecondaryTransmission()
        {
            var engine = new TransmissionEngine();
            IList<ResolvedTransmission<string>> result = engine.Resolve(
                new[] { Exposure(1, 2, 0d, "zero") },
                new Random(1));

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void MultipleSourcesCreateAtMostOneInfection()
        {
            var engine = new TransmissionEngine();
            IList<ResolvedTransmission<string>> result = engine.Resolve(
                new[]
                {
                    Exposure(1, 3, 1d, "first"),
                    Exposure(2, 3, 1d, "second"),
                },
                new Random(7));

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].TargetCitizenId, Is.EqualTo(3));
            Assert.That(result[0].CombinedProbability, Is.EqualTo(1d));
        }

        [Test]
        public void SourceAttributionIsIndependentFromInputIterationOrder()
        {
            var engine = new TransmissionEngine();
            var forward = new[]
            {
                Exposure(9, 3, 0.35d, "nine"),
                Exposure(2, 3, 0.65d, "two"),
            };
            var reverse = new[] { forward[1], forward[0] };

            IList<ResolvedTransmission<string>> first = engine.Resolve(forward, new Random(22));
            IList<ResolvedTransmission<string>> second = engine.Resolve(reverse, new Random(22));

            Assert.That(first, Has.Count.EqualTo(second.Count));
            Assert.That(first[0].SourceCitizenId, Is.EqualTo(second[0].SourceCitizenId));
            Assert.That(first[0].Context, Is.EqualTo(second[0].Context));
        }

        [Test]
        public void InvalidProbabilityFailsClosed()
        {
            var engine = new TransmissionEngine();
            Assert.Throws<ArgumentOutOfRangeException>(() => engine.Resolve(
                new[] { Exposure(1, 2, double.NaN, "invalid") },
                new Random(1)));
        }

        private static TransmissionExposure<string> Exposure(uint source, uint target, double probability, string context)
        {
            return new TransmissionExposure<string>
            {
                SourceCitizenId = source,
                TargetCitizenId = target,
                Probability = probability,
                StableContextKey = context.GetHashCode(),
                Context = context,
            };
        }
    }
}
