namespace RealTime.Experiments
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;

    internal sealed class ExperimentResultRun
    {
        public string Directory;
        public ExperimentRunManifest Manifest;
        public readonly Dictionary<string, double> Metrics = new Dictionary<string, double>(StringComparer.Ordinal);
        public string GroupKey => Manifest.BatchId + "/" + Manifest.ScenarioId + "/" + Manifest.ConfigurationHash;
        public string Label => Manifest.BatchName + " / " + Manifest.ScenarioName;
    }

    internal sealed class ExperimentResultsCatalog
    {
        public readonly List<ExperimentResultRun> Runs = new List<ExperimentResultRun>();
        public readonly List<string> Issues = new List<string>();
        public int Excluded;
        public readonly List<ExperimentBatchPlan> Plans = new List<ExperimentBatchPlan>();
    }

    internal sealed class ExperimentMetricStatistics
    {
        public int Count;
        public double Mean, Median, StandardDeviation, Minimum, Maximum;
        public double? Lower95, Upper95;
        public static ExperimentMetricStatistics Describe(IEnumerable<double> input)
        {
            var values = input.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).OrderBy(v => v).ToArray();
            var result = new ExperimentMetricStatistics { Count = values.Length };
            if (values.Length == 0) return result;
            result.Mean = values.Average(); result.Minimum = values[0]; result.Maximum = values[values.Length - 1];
            result.Median = (values[(values.Length - 1) / 2] + values[values.Length / 2]) / 2;
            if (values.Length > 1)
            {
                result.StandardDeviation = Math.Sqrt(values.Sum(v => (v - result.Mean) * (v - result.Mean)) / (values.Length - 1));
                double margin = Critical95(values.Length - 1) * result.StandardDeviation / Math.Sqrt(values.Length);
                result.Lower95 = result.Mean - margin; result.Upper95 = result.Mean + margin;
            }
            return result;
        }
        private static double Critical95(int degrees)
        {
            double[] t = { 0, 12.7063, 4.3027, 3.1825, 2.7765, 2.5706, 2.4469, 2.3646, 2.3060, 2.2622, 2.2281, 2.2010, 2.1788, 2.1604, 2.1448, 2.1314, 2.1199, 2.1098, 2.1009, 2.0930, 2.0860, 2.0796, 2.0739, 2.0687, 2.0639, 2.0595, 2.0555, 2.0518, 2.0484, 2.0452, 2.0423 };
            // Conservative t bound for df > 30, rather than overconfident normal intervals.
            return t[Math.Min(degrees, 30)];
        }
    }

    internal sealed class ExperimentReplayLink
    {
        public DateTime Time;
        public float X, Z;
        public string Source, Target;
    }

    internal sealed class ExperimentReplayData
    {
        public List<Dictionary<string, string>> Frames;
        public readonly List<ExperimentReplayLink> Links = new List<ExperimentReplayLink>();
        public long TotalLinks, TotalFrames;
        public readonly List<Dictionary<string, string>> Policies = new List<Dictionary<string, string>>();
        public bool PoliciesTruncated;
        public static ExperimentReplayData Load(string directory)
        {
            var data = new ExperimentReplayData { Frames = new List<Dictionary<string, string>>() };
            int frameStride = 1; Dictionary<string, string> last = null;
            foreach (var frame in ExperimentResultsAnalysis.ReadCsv(Path.Combine(directory, "state_timeseries.csv")))
            {
                last = frame; data.TotalFrames++;
                if (data.TotalFrames % frameStride != 0) continue;
                if (data.Frames.Count >= 10000) { for (int i = data.Frames.Count - 1; i >= 0; i--) if (i % 2 != 0) data.Frames.RemoveAt(i); frameStride *= 2; }
                data.Frames.Add(frame);
            }
            if (last != null && (data.Frames.Count == 0 || !ReferenceEquals(last, data.Frames[data.Frames.Count - 1]))) data.Frames.Add(last);
            int stride = 1;
            foreach (var row in ExperimentResultsAnalysis.ReadCsv(Path.Combine(directory, "transmission_events.csv")))
            {
                data.TotalLinks++;
                if (data.TotalLinks % stride != 0) continue;
                if (!row.TryGetValue("position_x", out string x) || !row.TryGetValue("position_z", out string z) || !ExperimentResultsAnalysis.TryNumber(x, out double px) || !ExperimentResultsAnalysis.TryNumber(z, out double pz)) continue;
                if (row.TryGetValue("has_position", out string has) && has == "0") continue;
                if (!row.TryGetValue("simulation_time", out string stamp) || !DateTime.TryParse(stamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime time)) continue;
                row.TryGetValue("source_citizen_id", out string source); row.TryGetValue("target_citizen_id", out string target);
                if (data.Links.Count >= 256)
                {
                    for (int i = data.Links.Count - 1; i >= 0; i--) if (i % 2 != 0) data.Links.RemoveAt(i);
                    stride *= 2;
                }
                data.Links.Add(new ExperimentReplayLink { Time = time, X = (float)px, Z = (float)pz, Source = source, Target = target });
            }
            foreach (var row in ExperimentResultsAnalysis.ReadCsv(Path.Combine(directory, "intervention_events.csv")))
            {
                if (row.TryGetValue("citizen_id", out string citizen) && !string.IsNullOrEmpty(citizen)) continue;
                if (!row.ContainsKey("simulation_time") || !row.ContainsKey("intervention_type") || !row.ContainsKey("action")) continue;
                if (data.Policies.Count < 10000) data.Policies.Add(row); else data.PoliciesTruncated = true;
            }
            return data;
        }
    }

    /// <summary>File-only analysis; never accesses simulation objects or modifies completed runs.</summary>
    internal static class ExperimentResultsAnalysis
    {
        public static readonly string[] MetricNames = { "cumulative_infections", "deaths_total", "hospitalizations_total", "peak_prevalence_pct", "attack_rate_pct", "detected_active_cases", "physical_contacts_total", "total_isolation_person_days", "total_quarantine_person_days", "closure_family_days" };
        public static readonly string[] MetricLabels = { "Cumulative infections", "Deaths", "Hospital admissions", "Peak prevalence (%)", "Attack rate (%)", "Final detected active", "Physical contacts", "Assigned isolation person-days", "Assigned quarantine person-days", "Closure family-days" };

        public static ExperimentResultsCatalog Scan(IEnumerable<string> roots)
        {
            var result = new ExperimentResultsCatalog();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var identities = new HashSet<string>(StringComparer.Ordinal);
            var service = new ExperimentRunCommitService(new AtomicJsonFileStore());
            foreach (string root in roots.Where(r => !string.IsNullOrEmpty(r)).Distinct(StringComparer.OrdinalIgnoreCase))
            foreach (string directory in Directories(root, visited, result.Issues))
            {
                string planPath = Path.Combine(directory, "batch_manifest.json");
                if (File.Exists(planPath))
                {
                    var plan = new AtomicJsonFileStore().TryLoad<ExperimentBatchPlan>(planPath);
                    if (plan.Success && plan.Value?.Scenarios != null) result.Plans.Add(plan.Value);
                    else result.Issues.Add(planPath + ": unreadable batch plan");
                }
                string manifestPath = Path.Combine(directory, ExperimentRunCommitService.RunManifestFileName);
                if (!File.Exists(manifestPath)) continue;
                string leaf = Path.GetFileName(directory);
                if (leaf.IndexOf(".__", StringComparison.Ordinal) >= 0 || leaf.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
                { result.Excluded++; result.Issues.Add(leaf + ": temporary/invalid attempt excluded"); continue; }
                try
                {
                    var validation = service.ValidateRunDirectory(directory);
                    if (!validation.Success) { result.Excluded++; result.Issues.Add(directory + ": " + validation.Error); continue; }
                    var run = new ExperimentResultRun { Directory = directory, Manifest = validation.Manifest };
                    string identity = run.Manifest.BatchId + "/" + run.Manifest.RunId;
                    if (!identities.Add(identity)) { result.Excluded++; result.Issues.Add(directory + ": duplicate logical run excluded"); continue; }
                    var rows = ReadCsv(Path.Combine(directory, "run_summary.csv")).Take(2).ToArray();
                    if (rows.Length != 1) throw new InvalidDataException("Expected exactly one run summary row.");
                    foreach (var pair in rows[0])
                        if (TryNumber(pair.Value, out double value)) run.Metrics[pair.Key] = value;
                    double peak = 0; int pointCount = 0; DateTime previousTime = DateTime.MinValue;
                    foreach (var row in ReadCsv(Path.Combine(directory, "state_timeseries.csv")))
                    {
                        if (!row.TryGetValue("simulation_time", out string timeText) || !DateTime.TryParse(timeText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime time) || time < previousTime) throw new InvalidDataException("Invalid/nonmonotonic state time.");
                        previousTime = time; pointCount++;
                        double population = Number(row, "tracked_population");
                        double total = Number(row, "susceptible") + Number(row, "exposed") + Number(row, "infectious") + Number(row, "post_infectious_ill") + Number(row, "recovered") + Number(row, "dead");
                        if (population != total || population < 0) throw new InvalidDataException("State compartment sum does not match population.");
                        double active = Number(row, "exposed") + Number(row, "infectious") + Number(row, "post_infectious_ill");
                        if (population > 0) peak = Math.Max(peak, active * 100 / population);
                    }
                    if (pointCount == 0) throw new InvalidDataException("No recorded state points.");
                    run.Metrics["peak_prevalence_pct"] = peak;
                    // Older manifests used the later export frame as their endpoint.
                    // Burden must stop at the final recorded scientific state.
                    double? closureDays = ClosureDays(ReadCsv(Path.Combine(directory, "intervention_events.csv")), previousTime);
                    if (closureDays.HasValue) run.Metrics["closure_family_days"] = closureDays.Value;
                    result.Runs.Add(run);
                }
                catch (Exception ex) { result.Excluded++; result.Issues.Add(directory + ": " + ex.Message); }
            }
            result.Runs.Sort((a, b) => string.Compare(a.Label + a.Manifest.RunNumber.ToString("D8"), b.Label + b.Manifest.RunNumber.ToString("D8"), StringComparison.Ordinal));
            return result;
        }

        private static IEnumerable<string> Directories(string root, HashSet<string> visited, List<string> issues)
        {
            var pending = new Stack<string>(); pending.Push(root);
            while (pending.Count > 0)
            {
                string path = pending.Pop();
                string[] children;
                try
                {
                    path = Path.GetFullPath(path);
                    if (!Directory.Exists(path) || !visited.Add(path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) continue;
                    children = Directory.GetDirectories(path); Array.Sort(children, StringComparer.Ordinal);
                }
                catch (Exception ex) { issues.Add(path + ": " + ex.Message); continue; }
                yield return path;
                if (Path.GetFileName(path).IndexOf(".__", StringComparison.Ordinal) >= 0) continue;
                foreach (string child in children) pending.Push(child);
            }
        }

        internal static double? ClosureDays(IEnumerable<Dictionary<string, string>> events, DateTime end)
        {
            var starts = new Dictionary<string, DateTime>(StringComparer.Ordinal); bool measured = false; double days = 0;
            foreach (var row in events)
            {
                if (!row.TryGetValue("reason", out string reason) || reason != "EffectiveFamilyState") continue;
                measured = true;
                string family = row["context"];
                DateTime time = DateTime.Parse(row["simulation_time"], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                if (time > end) throw new InvalidDataException("Closure event exceeds run end.");
                if (row["action"] == "Close") { if (!starts.ContainsKey(family)) starts.Add(family, time); }
                else if (row["action"] == "Reopen" && starts.TryGetValue(family, out DateTime opened)) { days += Math.Max(0, (time - opened).TotalDays); starts.Remove(family); }
            }
            foreach (DateTime start in starts.Values) days += Math.Max(0, (end - start).TotalDays);
            return measured ? (double?)days : null;
        }

        internal static string ManifestQuality(IEnumerable<ExperimentRunManifest> manifests, ExperimentBatchPlan plan = null)
        {
            var report = new StringBuilder("TENUS automatic batch quality report\nManifest consistency only; open Experiment results for full file checksum/state verification.\n");
            foreach (var group in manifests.GroupBy(m => m.ScenarioId))
            {
                var first = group.First(); int expected = first.Scenario?.RunCount ?? 0;
                report.AppendLine(first.ScenarioName + ": " + group.Count() + "/" + expected + " planned repetitions; missing " + Math.Max(0, expected - group.Count()));
                if (group.Select(m => m.ConfigurationHash).Distinct().Count() > 1) report.AppendLine("WARNING: configuration hashes differ within scenario.");
                if (group.GroupBy(m => m.PairId + "/" + m.MasterSeed).Any(g => g.Count() > 1)) report.AppendLine("WARNING: duplicate pair/seed identities; inspect repetitions before inference.");
                if (group.Count() < 2) report.AppendLine("Insufficient repetitions for an uncertainty interval.");
                report.Append(ScheduleHorizonWarnings(first.Scenario));
            }
            if (plan?.Scenarios != null)
                foreach (var scenario in plan.Scenarios)
                    if (!manifests.Any(m => m.ScenarioId == scenario.ScenarioId)) report.AppendLine(scenario.Name + ": 0/" + scenario.RunCount + " published repetitions; not started, unfinished or failed.");
            report.AppendLine("Invalid attempt details remain in batch_state.json and the full library quality report. Software consistency is not epidemiological validation.");
            return report.ToString();
        }

        internal static string ScheduleHorizonWarnings(ExperimentScenario scenario)
        {
            var warnings = new StringBuilder();
            var schedule = scenario?.InterventionSchedule;
            if (schedule != null)
                for (int phase = 0; phase < schedule.PhaseCount; phase++)
                    if (schedule.DayAt(phase) >= scenario.DurationDays)
                        warnings.AppendFormat(CultureInfo.InvariantCulture,
                            "WARNING: {0}: phase at day {1} has no observation time after activation within the {2}-day run horizon.\n",
                            scenario.Name, schedule.DayAt(phase), scenario.DurationDays);
            return warnings.ToString();
        }

        public static ExperimentMetricStatistics Paired(IEnumerable<ExperimentResultRun> control, IEnumerable<ExperimentResultRun> intervention, string metric)
        {
            Func<ExperimentResultRun, string> key = r => r.Manifest.BatchId + "/" + r.Manifest.PairId + "/" + r.Manifest.MasterSeed;
            var left = control.Where(r => r.Manifest.PairedSeedMode && r.Manifest.PairId > 0 && r.Metrics.ContainsKey(metric)).GroupBy(key).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.First());
            var differences = new List<double>();
            var seeds = new HashSet<int>(); bool duplicateSeed = false;
            foreach (var group in intervention.Where(r => r.Manifest.PairedSeedMode && r.Manifest.PairId > 0 && r.Metrics.ContainsKey(metric)).GroupBy(key).Where(g => g.Count() == 1))
            {
                var right = group.First();
                if (left.TryGetValue(group.Key, out ExperimentResultRun baseline) && Comparable(baseline.Manifest, right.Manifest))
                {
                    differences.Add(right.Metrics[metric] - baseline.Metrics[metric]);
                    if (!seeds.Add(right.Manifest.MasterSeed)) duplicateSeed = true;
                }
            }
            var result = ExperimentMetricStatistics.Describe(differences);
            if (duplicateSeed) result.Lower95 = result.Upper95 = null;
            return result;
        }

        private static bool Comparable(ExperimentRunManifest a, ExperimentRunManifest b) => a.BaselineAssetFullName == b.BaselineAssetFullName && a.BaselineLocalFileSha256 == b.BaselineLocalFileSha256 && a.BaselineAssetChecksum == b.BaselineAssetChecksum && a.BaselineDataAssetChecksum == b.BaselineDataAssetChecksum && a.GitCommitSha == b.GitCommitSha && a.ModVersion == b.ModVersion && a.GameVersion == b.GameVersion && a.SeedAlgorithm == b.SeedAlgorithm && a.ConfiguredDurationDays == b.ConfiguredDurationDays && a.EndMode == b.EndMode;

        public static string Comparison(IList<ExperimentResultRun> left, IList<ExperimentResultRun> right)
        {
            var text = new StringBuilder("A = reference; B = comparison. Paired difference = B - A.\n95% t intervals assume independent repeated seeds; n < 2 or reused seeds gives no interval.\n");
            if (left.Count == 0 || right.Count == 0) return text.Append("Select two scenarios with verified runs.").ToString();
            if (!Comparable(left[0].Manifest, right[0].Manifest)) text.AppendLine("WARNING: baseline, build or horizon differs. Descriptive comparison only; no paired inference.");
            for (int i = 0; i < MetricNames.Length; i++)
            {
                string metric = MetricNames[i];
                var a = ExperimentMetricStatistics.Describe(left.Where(r => r.Metrics.ContainsKey(metric)).Select(r => r.Metrics[metric]));
                var b = ExperimentMetricStatistics.Describe(right.Where(r => r.Metrics.ContainsKey(metric)).Select(r => r.Metrics[metric]));
                if (left.GroupBy(r => r.Manifest.MasterSeed).Any(g => g.Count() > 1)) a.Lower95 = a.Upper95 = null;
                if (right.GroupBy(r => r.Manifest.MasterSeed).Any(g => g.Count() > 1)) b.Lower95 = b.Upper95 = null;
                var paired = Paired(left, right, metric);
                text.AppendLine(MetricLabels[i]);
                text.Append("  A ").Append(Format(a)).Append("\n  B ").Append(Format(b)).Append("\n  Paired B-A ").Append(Format(paired)).AppendLine();
            }
            text.AppendLine("Burden measures are assigned durations, not economic cost. Fewer infections with more restriction is a trade-off, not an automatic policy recommendation.");
            var differences = left[0].Manifest.Scenario?.Settings?.Diff(right[0].Manifest.Scenario?.Settings);
            if (differences != null) foreach (var difference in differences) text.AppendLine(difference.PropertyName + ": " + difference.LeftValue + " -> " + difference.RightValue);
            return text.ToString();
        }

        public static string Format(ExperimentMetricStatistics s) => s.Count == 0 ? "unavailable (n=0)" : string.Format(CultureInfo.InvariantCulture, "n={0}; mean {1:0.###}; median {2:0.###}; SD {3:0.###}; min/max {4:0.###}/{5:0.###}; 95% [{6}, {7}]", s.Count, s.Mean, s.Median, s.StandardDeviation, s.Minimum, s.Maximum, s.Lower95?.ToString("0.###", CultureInfo.InvariantCulture) ?? "n/a", s.Upper95?.ToString("0.###", CultureInfo.InvariantCulture) ?? "n/a");

        public static string Quality(ExperimentResultsCatalog catalog)
        {
            var text = new StringBuilder("TECHNICAL QUALITY REPORT ? not epidemiological validation\n");
            text.AppendLine("Verified completed runs: " + catalog.Runs.Count + "; excluded attempts: " + catalog.Excluded);
            foreach (var group in catalog.Runs.GroupBy(r => r.GroupKey))
            {
                var first = group.First();
                text.AppendLine(first.Label + ": " + group.Count() + "/" + (first.Manifest.Scenario?.RunCount.ToString() ?? "?") + " planned; preset " + (first.Manifest.Scenario?.PresetId ?? "custom"));
                if (group.Count() < (first.Manifest.Scenario?.RunCount ?? 0)) text.AppendLine("  Missing or unfinished repetitions.");
                text.Append(ScheduleHorizonWarnings(first.Manifest.Scenario));
                foreach (string metric in MetricNames) if (group.Any(r => !r.Metrics.ContainsKey(metric))) text.AppendLine("  Unavailable in some runs: " + metric);
            }
            foreach (var plan in catalog.Plans)
                foreach (var scenario in plan.Scenarios)
                {
                    int completed = catalog.Runs.Count(r => r.Manifest.BatchId == plan.BatchId && r.Manifest.ScenarioId == scenario.ScenarioId);
                    if (completed < scenario.RunCount) text.AppendLine(plan.BatchName + " / " + scenario.Name + ": " + (scenario.RunCount - completed) + " planned repetitions not verified/completed.");
                }
            foreach (string issue in catalog.Issues) text.AppendLine(issue);
            text.AppendLine("Rt/household secondary attack rate require additional estimators. A matching seed alone does not validate model assumptions.");
            return text.ToString();
        }

        public static bool TryNumber(string text, out double value) => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && !double.IsNaN(value) && !double.IsInfinity(value);
        private static double Number(Dictionary<string, string> row, string key)
        {
            if (!row.TryGetValue(key, out string raw) || !TryNumber(raw, out double value) || value < 0) throw new InvalidDataException("Missing/invalid state counter: " + key);
            return value;
        }

        public static IEnumerable<Dictionary<string, string>> ReadCsv(string path)
        {
            using (var reader = new StreamReader(path, Encoding.UTF8, true))
            {
                string[] header = null;
                foreach (string[] fields in CsvRows(reader))
                {
                    if (header == null) { header = fields; continue; }
                    if (fields.Length == 1 && fields[0].Length == 0) continue;
                    if (fields.Length != header.Length) throw new InvalidDataException("CSV column count mismatch: " + path);
                    var row = new Dictionary<string, string>(StringComparer.Ordinal);
                    for (int i = 0; i < fields.Length; i++) row.Add(header[i], fields[i]);
                    yield return row;
                }
            }
        }

        private static IEnumerable<string[]> CsvRows(TextReader reader)
        {
            var fields = new List<string>(); var field = new StringBuilder(); bool quoted = false;
            int raw;
            while ((raw = reader.Read()) >= 0)
            {
                char c = (char)raw;
                if (c == '"')
                {
                    if (quoted && reader.Peek() == '"') { reader.Read(); field.Append('"'); }
                    else quoted = !quoted;
                }
                else if (!quoted && (c == ',' || c == '\n' || c == '\r'))
                {
                    fields.Add(field.ToString()); field.Length = 0;
                    if (c != ',') { if (c == '\r' && reader.Peek() == '\n') reader.Read(); yield return fields.ToArray(); fields.Clear(); }
                }
                else field.Append(c);
            }
            if (quoted) throw new InvalidDataException("Unterminated CSV quoted field.");
            if (field.Length > 0 || fields.Count > 0) { fields.Add(field.ToString()); yield return fields.ToArray(); }
        }
    }
}
