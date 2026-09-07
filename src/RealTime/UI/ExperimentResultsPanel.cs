namespace RealTime.UI
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using ColossalFramework.UI;
    using RealTime.Experiments;
    using UnityEngine;

    internal sealed partial class ExperimentBatchPanel
    {
        private UIPanel resultsWindow;
        private UITextField resultsRoot, resultsText;
        private UILabel resultsStatus, replayLabel;
        private UIDropDown resultsA, resultsB, replayRun;
        private UISlider replayTime;
        private readonly object resultsGate = new object();
        private ExperimentResultsCatalog resultsCatalog, pendingCatalog;
        private string pendingResultsError;
        private bool resultsLoading;
        private int resultScanGeneration;
        private List<List<ExperimentResultRun>> resultGroups = new List<List<ExperimentResultRun>>();
        private List<Dictionary<string, string>> replayFrames;
        private ExperimentReplayData pendingReplay, replayData;
        private readonly List<UISprite> replayDots = new List<UISprite>();
        private UILabel replayChains;
        private int replayGeneration;
        private bool fillingResults;
        private string comparisonReport;

        private void ShowResultsWindow()
        {
            if (resultsWindow != null) { resultsWindow.Show(); resultsWindow.BringToFront(); return; }
            resultsWindow = (UIPanel)UIView.GetAView().AddUIComponent(typeof(UIPanel));
            resultsWindow.name = "TENUS Experiment Results";
            resultsWindow.size = new Vector2(1000, 780);
            resultsWindow.relativePosition = new Vector3(24, 24);
            resultsWindow.backgroundSprite = "GenericPanel";
            resultsWindow.color = new Color32(24, 34, 47, 255);
            resultsWindow.isInteractive = true;
            var title = PopupLabel(resultsWindow, "Experiment library & comparison", 18, 12, 910, 32);
            title.textScale = 1.1f;
            var drag = resultsWindow.AddUIComponent<UIDragHandle>();
            drag.relativePosition = Vector3.zero; drag.size = new Vector2(930, 45); drag.target = resultsWindow;
            CreateButton(resultsWindow, 947, 10, 36, "X", () => resultsWindow.Hide());
            resultsRoot = CreateTextField(resultsWindow, 18, 56, 780);
            resultsRoot.text = viewState?.Plan?.OutputRoot ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Colossal Order\Cities_Skylines\Addons\Mods\RealTime\Pandemic Data");
            resultsRoot.tooltip = "Additional root containing completed experiment folders. The known standard output roots are also scanned. Scanning verifies checksums in a file-only background worker. No active run is loaded or modified.";
            CreateButton(resultsWindow, 810, 56, 170, "Scan / refresh", ScanResults);
            resultsStatus = PopupLabel(resultsWindow, "Select an experiment root and scan.", 18, 94, 960, 34);
            resultsA = CreateDropDown(resultsWindow, 18, 136, 475);
            resultsB = CreateDropDown(resultsWindow, 505, 136, 475);
            resultsA.tooltip = "Reference scenario A (all verified repetitions)";
            resultsB.tooltip = "Comparison scenario B (all verified repetitions)";
            resultsA.eventSelectedIndexChanged += (c, i) => CompareResults();
            resultsB.eventSelectedIndexChanged += (c, i) => CompareResults();
            CreateButton(resultsWindow, 18, 176, 205, "Compare key numbers", CompareResults);
            CreateButton(resultsWindow, 235, 176, 205, "Quality report", () => { if (resultsCatalog != null) resultsText.text = ExperimentResultsAnalysis.Quality(resultsCatalog); });
            CreateButton(resultsWindow, 452, 176, 165, "All scenarios", ShowScenarioOverview);
            CreateButton(resultsWindow, 629, 176, 165, "Individual runs", ShowIndividualRuns);
            CreateButton(resultsWindow, 806, 176, 174, "Save report", SaveResultsReport);
            resultsText = CreateTextField(resultsWindow, 18, 218, 962);
            resultsText.height = 330; resultsText.multiline = true; resultsText.readOnly = true;
            resultsText.textScale = 0.8f;
            replayChains = PopupLabel(resultsWindow, "Replay map: sampled transmission locations; world bounds, north up. No city simulation is replayed.", 18, 560, 750, 83);
            var map = resultsWindow.AddUIComponent<UIPanel>(); map.relativePosition = new Vector3(800, 554); map.size = new Vector2(180, 90); map.backgroundSprite = "GenericPanel"; map.color = new Color32(40, 55, 70, 255);
            for (int i = 0; i < 256; i++) { var dot = map.AddUIComponent<UISprite>(); dot.spriteName = "GenericPanel"; dot.size = new Vector2(3, 3); dot.color = new Color32(248, 130, 70, 255); dot.isVisible = false; replayDots.Add(dot); }
            PopupLabel(resultsWindow, "Recorded timeline - analysis replay, no city reload", 18, 650, 960, 25);
            replayRun = CreateDropDown(resultsWindow, 18, 680, 470);
            replayRun.eventSelectedIndexChanged += (c, i) => LoadReplay();
            replayTime = resultsWindow.AddUIComponent<UISlider>();
            replayTime.relativePosition = new Vector3(505, 686); replayTime.size = new Vector2(475, 20);
            replayTime.minValue = 0; replayTime.maxValue = 1; replayTime.stepSize = 1;
            var track = replayTime.AddUIComponent<UISprite>(); track.spriteName = "GenericPanel"; track.size = replayTime.size; track.color = new Color32(66, 88, 112, 255);
            var thumb = replayTime.AddUIComponent<UISprite>(); thumb.spriteName = "GenericPanel"; thumb.size = new Vector2(14, 24); thumb.color = new Color32(90, 198, 221, 255); replayTime.thumbObject = thumb;
            replayTime.eventValueChanged += (c, v) => ShowReplayFrame();
            replayLabel = PopupLabel(resultsWindow, "Choose a run to load its recorded state timeline.", 18, 718, 960, 50);
            var poller = resultsWindow.gameObject.AddComponent<ExperimentResultsPoller>(); poller.Owner = this;
            ScanResults();
        }

        private void ScanResults()
        {
            int generation;
            lock (resultsGate)
            {
                if (resultsLoading) return;
                resultsLoading = true; generation = ++resultScanGeneration;
                replayGeneration++; pendingReplay = null; pendingResultsError = null;
            }
            replayFrames = null; replayData = null;
            foreach (var dot in replayDots) dot.isVisible = false;
            replayLabel.text = "Refreshing run catalog...";
            string root = resultsRoot.text;
            var roots = new List<string> { root };
            if (viewState?.OutputRoots != null) roots.AddRange(viewState.OutputRoots.Select(option => option.OutputRoot));
            resultsStatus.text = "Scanning completed runs and verifying hashes in background...";
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try { var catalog = ExperimentResultsAnalysis.Scan(roots); lock (resultsGate) if (generation == resultScanGeneration) pendingCatalog = catalog; }
                catch (Exception ex) { lock (resultsGate) if (generation == resultScanGeneration) pendingResultsError = ex.Message; }
                finally { lock (resultsGate) if (generation == resultScanGeneration) resultsLoading = false; }
            });
        }

        private void PollResults()
        {
            if (resultsWindow == null) return;
            ExperimentResultsCatalog ready; ExperimentReplayData frames; string error;
            lock (resultsGate) { ready = pendingCatalog; pendingCatalog = null; frames = pendingReplay; pendingReplay = null; error = pendingResultsError; pendingResultsError = null; }
            if (error != null) resultsStatus.text = error;
            if (ready != null)
            {
                resultsCatalog = ready; fillingResults = true;
                resultGroups = ready.Runs.GroupBy(r => r.GroupKey).Select(g => g.ToList()).ToList();
                string[] labels = resultGroups.Select(g => g[0].Label + " [" + g.Count + " runs, " + g[0].Manifest.ConfigurationHash.Substring(0, 8) + "]").ToArray();
                resultsA.items = labels; resultsB.items = labels;
                resultsA.selectedIndex = labels.Length > 0 ? 0 : -1; resultsB.selectedIndex = labels.Length > 1 ? 1 : resultsA.selectedIndex;
                replayRun.items = ready.Runs.Select(r => r.Label + " / run " + r.Manifest.RunNumber + " / seed " + r.Manifest.MasterSeed).ToArray();
                replayRun.selectedIndex = -1; replayFrames = null; fillingResults = false;
                resultsStatus.text = ready.Runs.Count + " verified completed runs; " + ready.Excluded + " excluded. Snapshot updates only on Scan / refresh.";
                CompareResults();
            }
            if (frames != null) { replayData = frames; replayFrames = frames.Frames; replayTime.maxValue = Math.Max(1, replayFrames.Count - 1); replayTime.value = 0; ShowReplayFrame(); }
        }

        private void CompareResults()
        {
            if (fillingResults || resultsText == null) return;
            if (resultsA.selectedIndex < 0 || resultsB.selectedIndex < 0 || resultsA.selectedIndex >= resultGroups.Count || resultsB.selectedIndex >= resultGroups.Count)
            { resultsText.text = "No verified completed experiments found. See Quality report for excluded attempts."; return; }
            comparisonReport = ExperimentResultsAnalysis.Comparison(resultGroups[resultsA.selectedIndex], resultGroups[resultsB.selectedIndex]);
            resultsText.text = comparisonReport;
        }

        private void ShowScenarioOverview()
        {
            var report = new StringBuilder("ALL VERIFIED SCENARIOS ? mean values; counts/rates retain their units\nDifferent cities, builds and horizons are descriptive only.\n\n");
            foreach (var group in resultGroups)
            {
                report.AppendLine(group[0].Label + " / " + group.Count + " repetitions");
                for (int i = 0; i < ExperimentResultsAnalysis.MetricNames.Length; i++)
                {
                    string metric = ExperimentResultsAnalysis.MetricNames[i];
                    var stats = ExperimentMetricStatistics.Describe(group.Where(r => r.Metrics.ContainsKey(metric)).Select(r => r.Metrics[metric]));
                    report.Append(ExperimentResultsAnalysis.MetricLabels[i]).Append(": ").Append(stats.Count == 0 ? "unavailable" : stats.Mean.ToString("0.###", CultureInfo.InvariantCulture) + " (n=" + stats.Count + ")").Append("; ");
                }
                report.AppendLine().AppendLine();
            }
            resultsText.text = report.ToString();
        }

        private void ShowIndividualRuns()
        {
            if (resultsCatalog == null) return;
            var report = new StringBuilder("Individual verified runs - no averaging hides outliers\n");
            foreach (var run in resultsCatalog.Runs)
            {
                report.AppendLine(run.Label + " / run " + run.Manifest.RunNumber + " / seed " + run.Manifest.MasterSeed + " / pair " + run.Manifest.PairId);
                for (int i = 0; i < ExperimentResultsAnalysis.MetricNames.Length; i++)
                    if (run.Metrics.TryGetValue(ExperimentResultsAnalysis.MetricNames[i], out double value)) report.Append(ExperimentResultsAnalysis.MetricLabels[i]).Append(": ").Append(value.ToString("0.###", CultureInfo.InvariantCulture)).Append("; ");
                report.AppendLine();
            }
            resultsText.text = report.ToString();
        }

        private void SaveResultsReport()
        {
            try
            {
                string directory = Path.Combine(resultsRoot.text, "Analysis Reports"); Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "comparison_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture) + ".txt");
                using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false))) writer.Write(resultsText.text);
                resultsStatus.text = "Saved: " + path;
            }
            catch (Exception ex) { resultsStatus.text = "Report export failed: " + ex.Message; }
        }

        private void LoadReplay()
        {
            if (fillingResults || resultsCatalog == null || replayRun.selectedIndex < 0 || replayRun.selectedIndex >= resultsCatalog.Runs.Count) return;
            string path = resultsCatalog.Runs[replayRun.selectedIndex].Directory;
            int generation;
            lock (resultsGate) { if (resultsLoading) return; generation = ++replayGeneration; pendingReplay = null; }
            replayFrames = null; replayLabel.text = "Loading recorded timeline...";
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try { var frames = ExperimentReplayData.Load(path); lock (resultsGate) if (generation == replayGeneration) pendingReplay = frames; }
                catch (Exception ex) { lock (resultsGate) if (generation == replayGeneration) pendingResultsError = ex.Message; }
            });
        }

        private void ShowReplayFrame()
        {
            if (replayFrames == null || replayFrames.Count == 0) return;
            var row = replayFrames[Math.Min(replayFrames.Count - 1, Math.Max(0, (int)replayTime.value))];
            var text = new StringBuilder();
            foreach (string key in new[] { "simulation_time", "exposed", "infectious", "detected_active_cases", "recovered", "dead", "isolated_citizens", "quarantined_citizens" })
                if (row.TryGetValue(key, out string value)) text.Append(key).Append(": ").Append(value).Append("   ");
            replayLabel.text = text + " [displayed frames " + replayFrames.Count + "/" + (replayData?.TotalFrames ?? 0) + "]";
            if (replayData != null && row.TryGetValue("simulation_time", out string stamp) && DateTime.TryParse(stamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime time))
            {
                var visible = replayData.Links.Where(link => link.Time <= time && link.Time > time.AddDays(-1)).ToArray();
                for (int i = 0; i < replayDots.Count; i++)
                {
                    replayDots[i].isVisible = i < visible.Length;
                    if (i < visible.Length) replayDots[i].relativePosition = new Vector3(Mathf.Clamp01((visible[i].X + 8640) / 17280) * 177, (1 - Mathf.Clamp01((visible[i].Z + 8640) / 17280)) * 87);
                }
                replayChains.text = "Transmission locations, last 24 simulated hours. Deterministic display sample: " + replayData.Links.Count + " / " + replayData.TotalLinks + " events.\n" + string.Join("; ", visible.Reverse().Take(6).Select(link => link.Source + " -> " + link.Target + " (" + link.Time.ToString("HH:mm") + ")").ToArray());
                var policies = replayData.Policies.Where(policy => DateTime.TryParse(policy["simulation_time"], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime policyTime) && policyTime <= time).Reverse().Take(4).Select(policy => policy["intervention_type"] + " " + policy["action"] + (policy.ContainsKey("context") ? " / " + policy["context"] : ""));
                resultsText.text = "RECORDED STATE AT " + stamp + "\n\n" + string.Join("\n", row.Select(pair => pair.Key + ": " + pair.Value).ToArray()) + "\n\nLatest recorded policy changes:\n" + string.Join("\n", policies.ToArray()) + (replayData.PoliciesTruncated ? "\nPolicy display capped at first 10,000 changes; later changes are unavailable here." : "") + "\n\nUse Compare key numbers to return to scenario statistics.";
            }
        }

        private sealed class ExperimentResultsPoller : MonoBehaviour
        {
            internal ExperimentBatchPanel Owner;
            private float next;
            private void Update() { if (Time.unscaledTime < next) return; next = Time.unscaledTime + 0.2f; Owner?.PollResults(); }
        }
    }
}
