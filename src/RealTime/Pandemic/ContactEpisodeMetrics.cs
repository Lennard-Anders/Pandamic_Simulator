namespace RealTime.Pandemic
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;

    /// <summary>Completed-episode measurements, independent of storage mode; retains only aggregate bins.</summary>
    internal sealed class ContactEpisodeMetrics
    {
        private readonly long[] counts = new long[10];
        private readonly double[] minutes = new double[10];
        private readonly SortedDictionary<long, long> durations = new SortedDictionary<long, long>();
        internal void Record(ContactEpisode episode)
        {
            counts[(int)episode.Context]++;
            minutes[(int)episode.Context] += episode.DurationMinutes;
            long ticks = (episode.EndTime - episode.StartTime).Ticks;
            durations[ticks] = durations.TryGetValue(ticks, out long count) ? count + 1 : 1;
        }

        internal string SummaryCsv(double personDays)
        {
            var result = new StringBuilder("context,contact_episodes,episode_minutes,tracked_person_days,mean_contact_episodes_per_person_day,mean_episode_minutes_per_person_day\n");
            for (int i = 0; i < counts.Length; i++)
                result.AppendFormat(CultureInfo.InvariantCulture, "{0},{1},{2:R},{3:R},{4},{5}\n", (PhysicalContactContext)i, counts[i], minutes[i], personDays,
                    personDays > 0 ? (2d * counts[i] / personDays).ToString("R", CultureInfo.InvariantCulture) : "",
                    personDays > 0 ? (2d * minutes[i] / personDays).ToString("R", CultureInfo.InvariantCulture) : "");
            return result.ToString();
        }

        internal string DurationCsv()
        {
            var result = new StringBuilder("episode_duration_minutes,contact_episodes\n");
            foreach (var pair in durations)
                result.AppendFormat(CultureInfo.InvariantCulture, "{0:R},{1}\n", pair.Key / (double)TimeSpan.TicksPerMinute, pair.Value);
            return result.ToString();
        }
    }
}
