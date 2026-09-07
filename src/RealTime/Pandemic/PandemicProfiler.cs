namespace RealTime.Pandemic
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Text;

    /// <summary>Optional wall-clock diagnostics. Never changes simulation scheduling or recording.</summary>
    internal static class PandemicProfiler
    {
        private const int WindowSize = 256;
        private static readonly object Gate = new object();
        private static readonly SortedDictionary<string, Window> Windows = new SortedDictionary<string, Window>(StringComparer.Ordinal);

        public static bool Enabled { get; set; }

        public static Scope Measure(string name) => new Scope(Enabled ? name : null);

        public static void Reset()
        {
            lock (Gate) Windows.Clear();
        }

        public static string SnapshotCsv()
        {
            var csv = new StringBuilder("subsystem,calls,window_samples,average_ms,maximum_ms,p50_ms,p95_ms\n");
            lock (Gate)
            {
                foreach (var pair in Windows)
                {
                    Window w = pair.Value;
                    var sorted = new double[w.Count];
                    Array.Copy(w.Values, sorted, w.Count);
                    Array.Sort(sorted);
                    double sum = 0;
                    for (int i = 0; i < sorted.Length; i++) sum += sorted[i];
                    csv.AppendFormat(CultureInfo.InvariantCulture, "{0},{1},{2},{3:R},{4:R},{5:R},{6:R}\n", pair.Key, w.Calls, w.Count,
                        sum / w.Count, sorted[w.Count - 1], sorted[(w.Count - 1) / 2], sorted[(int)Math.Ceiling(w.Count * 0.95) - 1]);
                }
            }
            return csv.ToString();
        }

        internal struct Scope : IDisposable
        {
            private readonly string name;
            private readonly long start;

            internal Scope(string name)
            {
                this.name = name;
                start = name == null ? 0 : Stopwatch.GetTimestamp();
            }

            public void Dispose()
            {
                if (name == null) return;
                double elapsed = (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency;
                lock (Gate)
                {
                    if (!Windows.TryGetValue(name, out Window window))
                    {
                        window = new Window();
                        Windows.Add(name, window);
                    }
                    window.Values[window.Next] = elapsed;
                    window.Next = (window.Next + 1) % WindowSize;
                    window.Count = Math.Min(WindowSize, window.Count + 1);
                    window.Calls++;
                }
            }
        }

        private sealed class Window
        {
            public readonly double[] Values = new double[WindowSize];
            public int Next;
            public int Count;
            public long Calls;
        }
    }
}
