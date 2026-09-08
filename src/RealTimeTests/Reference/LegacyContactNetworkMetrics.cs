namespace RealTimeTests.Reference
{
    using RealTime.Pandemic;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;

    /// <summary>Streaming daily network statistics. Only the current day's unique pairs are retained.</summary>
    internal sealed class LegacyContactNetworkMetrics
    {
        private readonly Dictionary<uint, long> contactCounts = new Dictionary<uint, long>();
        private readonly Dictionary<uint, int> degrees = new Dictionary<uint, int>();
        private readonly HashSet<ulong> pairs = new HashSet<ulong>();
        private readonly long[] contextCounts = new long[10];
        private readonly double[] contextMinutes = new double[10];
        private readonly long[,] ageMixing = new long[6, 6];
        private readonly long[] hours = new long[24];
        private readonly SortedDictionary<double, long> durations = new SortedDictionary<double, long>();
        private readonly StringBuilder daily = new StringBuilder("day,observed_days,tracked_person_days,citizens_present,contact_events,mean_contacts_per_person_day,median_contact_events_per_citizen,unique_contacts_per_person_day,repeated_contacts_per_person_day,repeated_contact_ratio,context,context_contacts,context_mean_duration_minutes\n");
        private readonly StringBuilder degreeRows = new StringBuilder("day,degree,citizens\n");
        private DateTime start;
        private DateTime dayStart;
        private DateTime populationTime;
        private int population;
        private int citizensPresent;
        private decimal personTicks;
        private decimal totalPersonTicks;
        internal double TotalPersonDays => (double)(totalPersonTicks / TimeSpan.TicksPerDay);
        private double personDays => (double)(personTicks / TimeSpan.TicksPerDay);
        private long events;
        private int day;
        private bool completed;

        internal DateTime LatestTime => populationTime;

        public void Begin(DateTime time)
        {
            start = dayStart = populationTime = time;
        }

        public void Population(PandemicPopulationEvent item)
        {
            Advance(item.SimulationTime);
            int delta = item.Action == "InitialPopulation" || item.Action == "Added" ? item.Count : item.Action == "Removed" || item.Action == "Detached" ? -item.Count : 0;
            population += delta;
            if (population < 0) throw new InvalidOperationException("Network population became negative.");
            if (delta > 0) citizensPresent += delta;
        }

        public void Record(PhysicalContactEvent contact, int ageA, int ageB)
        {
            Advance(contact.EndTime);
            events++;
            Increment(contactCounts, contact.CitizenA);
            Increment(contactCounts, contact.CitizenB);
            ulong key = ((ulong)Math.Min(contact.CitizenA, contact.CitizenB) << 32) | Math.Max(contact.CitizenA, contact.CitizenB);
            if (pairs.Add(key))
            {
                degrees[contact.CitizenA] = degrees.TryGetValue(contact.CitizenA, out int a) ? a + 1 : 1;
                degrees[contact.CitizenB] = degrees.TryGetValue(contact.CitizenB, out int b) ? b + 1 : 1;
            }
            int context = (int)contact.Context;
            contextCounts[context]++;
            contextMinutes[context] += contact.DurationMinutes;
            int first = ageA < 0 || ageA > 4 ? 5 : ageA;
            int second = ageB < 0 || ageB > 4 ? 5 : ageB;
            ageMixing[first, second]++;
            ageMixing[second, first]++;
            hours[contact.EndTime.Hour]++;
            durations[contact.DurationMinutes] = durations.TryGetValue(contact.DurationMinutes, out long count) ? count + 1 : 1;
        }

        public void Complete(DateTime time)
        {
            if (completed) return;
            Advance(time);
            EmitDay(time);
            completed = true;
        }

        public string SummaryCsv => daily.ToString();
        public string DegreeCsv => degreeRows.ToString();

        public string AgeMixingCsv()
        {
            var csv = new StringBuilder("age_a,age_b,contact_participations\n");
            string[] names = { "Child", "Teen", "Young", "Adult", "Senior", "Unknown" };
            for (int a = 0; a < 6; a++) for (int b = 0; b < 6; b++) Row(csv, names[a], names[b], ageMixing[a, b]);
            return csv.ToString();
        }

        public string DurationCsv()
        {
            var csv = new StringBuilder("duration_minutes,contact_events\n");
            foreach (var pair in durations) Row(csv, pair.Key, pair.Value);
            return csv.ToString();
        }

        public string HourCsv()
        {
            var csv = new StringBuilder("end_hour,contact_events\n");
            for (int hour = 0; hour < 24; hour++) Row(csv, hour, hours[hour]);
            return csv.ToString();
        }

        private void Advance(DateTime time)
        {
            if (completed || time < populationTime) throw new InvalidOperationException(
                "Network recording requires monotonic time. Incoming=" + time.ToString("o")
                + ", latest=" + populationTime.ToString("o") + ", completed=" + completed);
            while (time > dayStart.AddDays(1))
            {
                DateTime boundary = dayStart.AddDays(1);
                personTicks += population * (decimal)(boundary - populationTime).Ticks;
                totalPersonTicks += population * (decimal)(boundary - populationTime).Ticks;
                populationTime = boundary;
                EmitDay(boundary);
                dayStart = boundary;
                day++;
                citizensPresent = population;
                personTicks = 0;
                events = 0;
                pairs.Clear(); contactCounts.Clear(); degrees.Clear();
                Array.Clear(contextCounts, 0, contextCounts.Length);
                Array.Clear(contextMinutes, 0, contextMinutes.Length);
            }
            personTicks += population * (decimal)(time - populationTime).Ticks;
            totalPersonTicks += population * (decimal)(time - populationTime).Ticks;
            populationTime = time;
        }

        private void EmitDay(DateTime end)
        {
            int denominator = Math.Max(citizensPresent, contactCounts.Count);
            var counts = new List<long>(contactCounts.Values);
            counts.Sort();
            int zeroCount = denominator - counts.Count;
            double? median = denominator == 0 ? (double?)null : (At(counts, (denominator - 1) / 2 - zeroCount) + At(counts, denominator / 2 - zeroCount)) / 2d;
            for (int context = 0; context < contextCounts.Length; context++)
                Row(daily, day, (end - dayStart).TotalDays, personDays, denominator, events,
                    Rate(2d * events), median, Rate(2d * pairs.Count), Rate(2d * (events - pairs.Count)),
                    events == 0 ? (double?)null : (events - pairs.Count) / (double)events,
                    (PhysicalContactContext)context, contextCounts[context], contextCounts[context] == 0 ? (double?)null : contextMinutes[context] / contextCounts[context]);
            var histogram = new SortedDictionary<int, int>();
            histogram[0] = denominator - degrees.Count;
            foreach (int degree in degrees.Values) histogram[degree] = histogram.TryGetValue(degree, out int count) ? count + 1 : 1;
            foreach (var pair in histogram) Row(degreeRows, day, pair.Key, pair.Value);
        }

        private double? Rate(double value) => personDays <= 0 ? (double?)null : value / personDays;
        private static long At(List<long> values, int index) => index < 0 ? 0 : values[index];
        private static void Increment(Dictionary<uint, long> counts, uint id) => counts[id] = counts.TryGetValue(id, out long count) ? count + 1 : 1;
        private static void Row(StringBuilder csv, params object[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (i != 0) csv.Append(',');
                csv.Append(values[i] is double number ? number.ToString("R", CultureInfo.InvariantCulture) : Convert.ToString(values[i], CultureInfo.InvariantCulture));
            }
            csv.AppendLine();
        }
    }
}
