namespace RealTimeTests.Experiments
{
    using RealTime.Experiments;
    using NUnit.Framework;

    public sealed class ExperimentBuildIdentityTests
    {
        [Test]
        public void ProductionAssemblyContainsBuildTimeGitIdentity()
        {
            ExperimentBuildIdentity identity = ExperimentBuildIdentity.Read(typeof(ExperimentBuildIdentity).Assembly);

            Assert.That(identity.CommitSha, Does.Match("^[0-9a-fA-F]{40}$"));
            Assert.That(identity.BranchOrTag, Is.Not.Null.And.Not.Empty.And.Not.EqualTo("unavailable"));
        }
    }
}
