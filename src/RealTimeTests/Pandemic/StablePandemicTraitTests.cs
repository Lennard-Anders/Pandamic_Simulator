namespace RealTimeTests.Pandemic
{
    using RealTime.Config;
    using RealTime.Pandemic;
    using NUnit.Framework;

    public sealed class StablePandemicTraitTests
    {
        [Test]
        public void MaskAssignmentIsIndependentOfLookupOrderAndUnrelatedMaskPolicy()
        {
            RealTimeConfig firstConfig = CreateConfig();
            RealTimeConfig secondConfig = CreateConfig();
            secondConfig.MaskBehavior = MaskBehavior.None;
            secondConfig.TransmissionProbabilityReduction = 99;
            var first = new MaskManager();
            var second = new MaskManager();
            first.Init(firstConfig);
            second.Init(secondConfig);
            first.ResetStableTraits(42);
            second.ResetStableTraits(42);

            int firstOne = first.GetMaskBehaviorForTesting(1u);
            int firstTwo = first.GetMaskBehaviorForTesting(2u);
            int secondTwo = second.GetMaskBehaviorForTesting(2u);
            int secondOne = second.GetMaskBehaviorForTesting(1u);

            Assert.That(secondOne, Is.EqualTo(firstOne));
            Assert.That(secondTwo, Is.EqualTo(firstTwo));
        }

        [Test]
        public void TracingTraitsAreIndependentOfLookupOrder()
        {
            RealTimeConfig config = CreateConfig();
            config.AppBasedContactTracingProbability = 50f;
            config.BuildingContactTracingProbability = 50f;
            var first = new ContactManager();
            var second = new ContactManager();
            first.Init(config);
            second.Init(config);
            first.ResetStableTraits(17);
            second.ResetStableTraits(17);

            bool firstApp = first.CitizenUsesAppForTesting(100u);
            bool firstManual = first.CitizenUsesManualTracingForTesting(100u);
            bool ignoredApp = second.CitizenUsesAppForTesting(200u);
            bool secondManual = second.CitizenUsesManualTracingForTesting(100u);
            bool secondApp = second.CitizenUsesAppForTesting(100u);

            Assert.That(secondApp, Is.EqualTo(firstApp));
            Assert.That(secondManual, Is.EqualTo(firstManual));
            Assert.That(ignoredApp, Is.EqualTo(second.CitizenUsesAppForTesting(200u)));
        }

        private static RealTimeConfig CreateConfig()
        {
            return new RealTimeConfig(true)
            {
                RatioIgnoreMasks = 30,
                RatioOtherProtectionMask = 35,
                RatioOwnProtectionMask = 35,
            };
        }
    }
}
