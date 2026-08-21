// <copyright file="AtomicFileUtilitiesTests.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTimeTests.Experiments
{
    using System.IO;
    using NUnit.Framework;
    using RealTime.Experiments;

    public sealed class AtomicFileUtilitiesTests
    {
        [Test]
        public void TemporarySiblingDoesNotAppendToLongDestinationName()
        {
            string directory = Path.Combine("C:\\", new string('d', 210));
            string destination = Path.Combine(directory, new string('f', 30) + ".csv");

            string temporary = AtomicFileUtilities.CreateTemporarySiblingPath(destination);

            Assert.That(Path.GetDirectoryName(temporary), Is.EqualTo(directory));
            Assert.That(Path.GetFileName(temporary), Does.StartWith(".tmp-"));
            Assert.That(temporary.Length, Is.LessThan(destination.Length));
            Assert.That(temporary.Length, Is.LessThan(260));
        }

        [Test]
        public void TemporarySiblingNamesAreUnique()
        {
            string destination = Path.Combine("C:\\exports", "run_manifest.json");

            string first = AtomicFileUtilities.CreateTemporarySiblingPath(destination);
            string second = AtomicFileUtilities.CreateTemporarySiblingPath(destination);

            Assert.That(second, Is.Not.EqualTo(first));
        }
    }
}
