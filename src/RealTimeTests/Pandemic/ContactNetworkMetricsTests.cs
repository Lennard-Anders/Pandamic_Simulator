namespace RealTimeTests.Pandemic
{
    using System;
    using System.Globalization;
    using NUnit.Framework;
    using RealTime.Pandemic;

    public sealed class ContactNetworkMetricsTests
    {
        [Test]
        public void PopulationEventCannotMoveBehindRecordedContacts()
        {
            var start = new DateTime(2030, 1, 1);
            var network = new ContactNetworkMetrics();
            network.Begin(start);
            network.Population(new PandemicPopulationEvent { SimulationTime = start, Action = "InitialPopulation", Count = 2 });
            var contacts = new ContactEngine();
            network.Record(contacts.Record(new PhysicalContactRequest { CitizenA = 1, CitizenB = 2,
                EndTime = start.AddMinutes(5), DurationMinutes = 5, Context = PhysicalContactContext.Household }, out bool created), 0, 4);
            Assert.That(() => network.Population(new PandemicPopulationEvent {
                SimulationTime = start.AddMinutes(4), Action = "Removed", Count = 1 }), Throws.InvalidOperationException);
            Assert.That(network.LatestTime, Is.EqualTo(start.AddMinutes(5)));
            network.Complete(start.AddMinutes(5));
            Assert.That(network.SummaryCsv.Split('\n')[1].Split(',')[3], Is.EqualTo("2"));
        }
        [Test]
        public void DayBoundaryPreservesStepEventsAndResetsDailyUniquePairs()
        {
            var start = new DateTime(2030, 1, 1);
            var network = new ContactNetworkMetrics();
            network.Begin(start);
            network.Population(new PandemicPopulationEvent { SimulationTime = start, Action = "InitialPopulation", Count = 2 });
            var contacts = new ContactEngine();
            foreach (DateTime time in new[] { start.AddDays(1), start.AddDays(1).AddMinutes(5) })
                network.Record(contacts.Record(new PhysicalContactRequest { CitizenA = 1, CitizenB = 2, EndTime = time, DurationMinutes = 5, Context = PhysicalContactContext.Household }, out bool created), 0, 4);
            network.Complete(start.AddDays(2));
            string[] rows = network.SummaryCsv.Trim().Split('\n');
            Assert.That(rows.Length, Is.EqualTo(21));
            foreach (int rowIndex in new[] { 1, 11 })
            {
                var row = rows[rowIndex].Split(',');
                Assert.That(row[4], Is.EqualTo("1"));
                Assert.That(double.Parse(row[7], CultureInfo.InvariantCulture), Is.EqualTo(1));
                Assert.That(double.Parse(row[9], CultureInfo.InvariantCulture), Is.Zero);
            }
            Assert.That(network.DurationCsv(), Does.Contain("5,2"));
        }
        [Test]
        public void DailyStatisticsIncludeZeroContactsAndKeepRepeatedPairs()
        {
            var start = new DateTime(2030, 1, 1);
            var network = new ContactNetworkMetrics();
            network.Begin(start);
            network.Population(new PandemicPopulationEvent { SimulationTime = start, Action = "InitialPopulation", Count = 4 });
            var contacts = new ContactEngine();
            for (int i = 1; i <= 2; i++)
            {
                var contact = contacts.Record(new PhysicalContactRequest { CitizenA = 1, CitizenB = 2, EndTime = start.AddMinutes(i * 5), DurationMinutes = 5, Context = PhysicalContactContext.Household }, out bool created);
                network.Record(contact, 0, 4);
            }
            network.Complete(start.AddDays(1));
            var row = network.SummaryCsv.Split('\n')[1].TrimEnd('\r').Split(',');
            Assert.That(double.Parse(row[2], CultureInfo.InvariantCulture), Is.EqualTo(4));
            Assert.That(row[4], Is.EqualTo("2"));
            Assert.That(double.Parse(row[5], CultureInfo.InvariantCulture), Is.EqualTo(1));
            Assert.That(double.Parse(row[6], CultureInfo.InvariantCulture), Is.EqualTo(1));
            Assert.That(double.Parse(row[9], CultureInfo.InvariantCulture), Is.EqualTo(0.5));
            Assert.That(network.DegreeCsv, Does.Contain("0,0,2"));
            Assert.That(network.AgeMixingCsv(), Does.Contain("Child,Senior,2"));
            Assert.That(network.DurationCsv(), Does.Contain("5,2"));
        }

        [Test]
        public void TurnoverAndPartialDayUseIntegratedTrackedPersonDays()
        {
            var start = new DateTime(2030, 1, 1);
            var network = new ContactNetworkMetrics();
            network.Begin(start);
            network.Population(new PandemicPopulationEvent { SimulationTime = start, Action = "InitialPopulation", Count = 4 });
            network.Population(new PandemicPopulationEvent { SimulationTime = start.AddHours(12), Action = "Removed", Count = 2 });
            network.Complete(start.AddHours(18));
            var row = network.SummaryCsv.Split('\n')[1].Split(',');
            Assert.That(double.Parse(row[2], CultureInfo.InvariantCulture), Is.EqualTo(2.5));
            Assert.That(row[3], Is.EqualTo("4"));
        }
    }
}
