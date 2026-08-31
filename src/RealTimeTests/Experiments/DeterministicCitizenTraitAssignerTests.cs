namespace RealTimeTests.Experiments
{
    using System;
    using RealTime.Experiments;
    using NUnit.Framework;

    public sealed class DeterministicCitizenTraitAssignerTests
    {
        [Test]
        public void AssignmentIsStableAcrossLookupOrder()
        {
            uint[] forward = { 1u, 2u, 12345u, uint.MaxValue };
            uint[] reverse = { uint.MaxValue, 12345u, 2u, 1u };
            var expected = new double[forward.Length];
            for (int i = 0; i < forward.Length; ++i)
            {
                expected[i] = DeterministicCitizenTraitAssigner.GetUnitInterval(42, forward[i], "mask-assignment");
            }

            for (int i = 0; i < reverse.Length; ++i)
            {
                int expectedIndex = Array.IndexOf(forward, reverse[i]);
                Assert.That(
                    DeterministicCitizenTraitAssigner.GetUnitInterval(42, reverse[i], "mask-assignment"),
                    Is.EqualTo(expected[expectedIndex]));
            }
        }

        [Test]
        public void FeatureNamespacesAndMasterSeedsAreSeparated()
        {
            double mask = DeterministicCitizenTraitAssigner.GetUnitInterval(42, 100u, "mask-assignment");
            double app = DeterministicCitizenTraitAssigner.GetUnitInterval(42, 100u, "tracing-app");
            double otherSeed = DeterministicCitizenTraitAssigner.GetUnitInterval(43, 100u, "mask-assignment");

            Assert.That(mask, Is.InRange(0d, 1d));
            Assert.That(app, Is.Not.EqualTo(mask));
            Assert.That(otherSeed, Is.Not.EqualTo(mask));
        }

        [Test]
        public void PercentageBoundariesPreserveValidZeroAndHundred()
        {
            Assert.That(DeterministicCitizenTraitAssigner.IsAssigned(1, 1u, "feature", 0d), Is.False);
            Assert.That(DeterministicCitizenTraitAssigner.IsAssigned(1, 1u, "feature", 100d), Is.True);
        }
    }
}
