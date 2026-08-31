namespace RealTimeTests.Pandemic
{
    using System;
    using NUnit.Framework;
    using RealTime.Pandemic;

    [TestFixture]
    public sealed class ExperimentRunIntegrityMonitorTests
    {
        [Test]
        public void FirstFatalFailureIsRetainedUntilReset()
        {
            var monitor = new ExperimentRunIntegrityMonitor();
            DateTime time = new DateTime(2030, 1, 1);

            monitor.Fail("TransmissionEngineException", "first", time, new InvalidOperationException("broken"));
            monitor.Fail("TestingEngineException", "second", time.AddDays(1), null);

            Assert.That(monitor.HasFailed, Is.True);
            Assert.That(monitor.Failure.Code, Is.EqualTo("TransmissionEngineException"));
            Assert.That(monitor.Failure.Detail, Does.Contain("broken"));

            monitor.Reset();
            Assert.That(monitor.HasFailed, Is.False);
        }
    }
}
