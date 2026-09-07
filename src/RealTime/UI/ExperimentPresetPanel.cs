namespace RealTime.UI
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using ColossalFramework.UI;
    using RealTime.Experiments;
    using UnityEngine;

    internal sealed partial class ExperimentBatchPanel
    {
        private UIPanel scenarioPopup;
        private UITextField diagnosticsText;
        private DateTime nextDiagnosticsRefresh;

        private void ShowDiagnostics()
        {
            var popup = OpenScenarioPopup("Performance diagnostics — optional wall-clock instrumentation");
            PopupLabel(popup, "Disabled by default. Rolling windows contain at most 256 calls per subsystem. Timings include child subsystems; do not add nested timings. No epidemic steps, contact events or scientific rows are skipped.", 16, 54, 850, 85);
            CreateButton(popup, 16, 147, 260, "Enable / disable profiler", () =>
            {
                RealTime.Pandemic.PandemicProfiler.Enabled = !RealTime.Pandemic.PandemicProfiler.Enabled;
                nextDiagnosticsRefresh = default(DateTime);
                RefreshDiagnostics();
            });
            diagnosticsText = CreateTextField(popup, 16, 194, 850);
            diagnosticsText.height = 420;
            diagnosticsText.multiline = true;
            diagnosticsText.readOnly = true;
            nextDiagnosticsRefresh = default(DateTime);
            RefreshDiagnostics();
        }

        private void RefreshDiagnostics()
        {
            if (diagnosticsText == null || scenarioPopup == null || !scenarioPopup.isVisible || DateTime.UtcNow < nextDiagnosticsRefresh) return;
            nextDiagnosticsRefresh = DateTime.UtcNow.AddSeconds(1);
            diagnosticsText.text = (RealTime.Pandemic.PandemicProfiler.Enabled ? "ENABLED" : "DISABLED")
                + " — measurements in milliseconds\n" + RealTime.Pandemic.PandemicProfiler.SnapshotCsv();
        }

        private UIPanel OpenScenarioPopup(string title)
        {
            if (scenarioPopup != null) UnityEngine.Object.Destroy(scenarioPopup.gameObject);
            scenarioPopup = panel.AddUIComponent<UIPanel>();
            scenarioPopup.relativePosition = new Vector3(12, 100);
            scenarioPopup.size = new Vector2(886, 650);
            scenarioPopup.backgroundSprite = "GenericPanel";
            scenarioPopup.color = new Color32(27, 39, 53, 255);
            scenarioPopup.isInteractive = true;
            PopupLabel(scenarioPopup, title, 16, 14, 780, 30).textScale = 1f;
            CreateButton(scenarioPopup, 830, 10, 36, "X", () => scenarioPopup.isVisible = false);
            return scenarioPopup;
        }

        private static UILabel PopupLabel(UIPanel parent, string text, float x, float y, float width, float height)
        {
            var label = parent.AddUIComponent<UILabel>();
            label.autoSize = false;
            label.relativePosition = new Vector3(x, y);
            label.size = new Vector2(width, height);
            label.textScale = 0.78f;
            label.wordWrap = true;
            label.text = text;
            return label;
        }

        private void ShowPresetEditor()
        {
            int baselineIndex = selectedScenarioIndex;
            var popup = OpenScenarioPopup("Scenario preset — preview before applying");
            var selector = CreateDropDown(popup, 16, 54, 850);
            selector.items = ExperimentPresetCatalog.DisplayNames;
            var description = PopupLabel(popup, "", 16, 95, 850, 115);
            var changes = CreateTextField(popup, 16, 215, 850);
            changes.relativePosition = new Vector3(16, 215);
            changes.size = new Vector2(850, 320);
            changes.multiline = true;
            changes.readOnly = true;
            changes.textScale = 0.75f;
            Action refresh = () =>
            {
                var preset = controller.PreviewPreset(Math.Max(0, selector.selectedIndex), baselineIndex);
                description.text = preset.Category + " | v" + preset.Version + "\n" + preset.LongDescription;
                var text = new StringBuilder("Parameter changes (baseline -> preset):\n");
                foreach (var change in preset.ParameterChanges) text.AppendLine(change.PropertyName + ": " + change.LeftValue + " -> " + change.RightValue);
                if (selector.selectedIndex == 9) text.AppendLine("At day 7: moderate layered configuration (stored in full in the scenario). Use Edit parameters to inspect both phases.");
                changes.text = text.ToString();
            };
            selector.eventSelectedIndexChanged += (c, i) => refresh();
            selector.selectedIndex = 0;
            refresh();
            CreateButton(popup, 16, 549, 245, "Apply preset: add scenario", () =>
            {
                if (RunAction(() => controller.AddPresetScenario(selector.selectedIndex, baselineIndex))) Refresh();
            });
            CreateButton(popup, 274, 549, 370, "Add Control / Masks / Testing / Strong", () =>
            {
                foreach (int index in new[] { 0, 3, 4, 8 }) if (!RunAction(() => controller.AddPresetScenario(index, baselineIndex))) break;
                Refresh();
            });
            PopupLabel(popup, "Browsing changes no settings. Added scenarios remain editable. Paired seeds allow scenario differences to be compared under matching random experiment conditions; the batch checkbox remains your choice.", 16, 587, 850, 54);
        }

        private void ShowParameterEditor()
        {
            if (!HasSelectedScenario()) { ShowLocalError("Select a scenario first."); return; }
            int scenarioIndex = selectedScenarioIndex;
            var popup = OpenScenarioPopup("Editable scenario configuration");
            var phase = CreateDropDown(popup, 16, 52, 850);
            bool scheduled = viewState.Plan.Scenarios[scenarioIndex].InterventionSchedule != null;
            phase.items = scheduled ? new[] { "Initial configuration", "After scheduled activation" } : new[] { "Initial configuration" };
            phase.selectedIndex = 0;
            var properties = typeof(ExperimentScenarioSnapshot).GetProperties().Where(p => p.CanWrite && p.Name != "SchemaVersion").OrderBy(p => p.Name, StringComparer.Ordinal).ToArray();
            var selector = CreateDropDown(popup, 16, 96, 850);
            selector.items = properties.Select(p => p.Name).Concat(new[] { "MaskPopulationSplit (Ignore,Other,Own)" }).ToArray();
            var value = CreateTextField(popup, 16, 142, 850);
            var hint = PopupLabel(popup, "", 16, 188, 850, 170);
            Action refresh = () =>
            {
                var scenario = controller.GetViewState().Plan.Scenarios[scenarioIndex];
                var settings = phase.selectedIndex == 1 ? scenario.InterventionSchedule.After : scenario.Settings;
                int index = Math.Max(0, selector.selectedIndex);
                if (index == properties.Length)
                {
                    value.text = settings.RatioIgnoreMasks + "," + settings.RatioOtherProtectionMask + "," + settings.RatioOwnProtectionMask;
                    hint.text = "Three percentages, summing to 100. They are applied together.";
                }
                else
                {
                    var property = properties[index];
                    value.text = Convert.ToString(property.GetValue(settings, null), CultureInfo.InvariantCulture);
                    hint.text = property.Name + " (" + property.PropertyType.Name + ")\n"
                        + (property.PropertyType.IsEnum ? string.Join(", ", Enum.GetNames(property.PropertyType)) : "Use invariant decimal notation, e.g. 0.75; booleans use True/False.")
                        + "\nValues are validated before applying. Scheduled phases may differ only in intervention parameters.";
                }
            };
            selector.eventSelectedIndexChanged += (c, i) => refresh();
            phase.eventSelectedIndexChanged += (c, i) => refresh();
            selector.selectedIndex = 0;
            refresh();
            CreateButton(popup, 16, 377, 300, "Apply parameter change", () =>
            {
                string name = selector.selectedIndex == properties.Length ? "MaskPopulationSplit" : properties[selector.selectedIndex].Name;
                if (phase.selectedIndex == 1) name = "Scheduled." + name;
                if (RunAction(() => controller.EditScenarioParameter(scenarioIndex, name, value.text))) { Refresh(); refresh(); }
            });
            PopupLabel(popup, "Changes affect this scenario. Its final configuration hash identifies the scientific inputs. Preset identity remains provenance, and customization is recorded.", 16, 435, 850, 90);
            CreateButton(popup, 16, 475, 300, "Multi-phase intervention plan", () => ShowScheduleEditor(scenarioIndex));
            CreateButton(popup, 540, 377, 325, "External calibration targets", () => ShowCalibrationEditor(scenarioIndex));
            PopupLabel(popup, "Generic OAT perturbations (%): lower / upper. Values are experimental, not calibrated uncertainty bounds.", 16, 510, 850, 45);
            var lower = CreateTextField(popup, 16, 559, 120);
            var upper = CreateTextField(popup, 148, 559, 120);
            lower.text = "-20";
            upper.text = "20";
            CreateButton(popup, 284, 559, 370, "Add paired sensitivity scenarios", () =>
            {
                if (selector.selectedIndex >= properties.Length || !double.TryParse(lower.text, NumberStyles.Float, CultureInfo.InvariantCulture, out double low)
                    || !double.TryParse(upper.text, NumberStyles.Float, CultureInfo.InvariantCulture, out double high))
                { ShowLocalError("Select a numeric parameter and enter two percentage perturbations."); return; }
                RunAction(() => controller.AddSensitivityScenarios(scenarioIndex, properties[selector.selectedIndex].Name, low / 100, high / 100));
            });
        }

        private void ShowCalibrationEditor(int scenarioIndex)
        {
            var popup = OpenScenarioPopup("External calibration targets — infrastructure, not validation");
            PopupLabel(popup, "Paste an explicit target set as JSON. Rates are fractions (0–1); TimeToPeakDays is simulated days. Mortality/hospitalization rates use cumulative infections as denominator. Rt and household secondary attack rate remain unavailable until their required measurements exist.", 16, 54, 850, 94);
            var input = CreateTextField(popup, 16, 158, 850);
            input.height = 335;
            input.multiline = true;
            var existing = controller.GetViewState().Plan.Scenarios[scenarioIndex].CalibrationTargets;
            input.text = existing == null ? "{\"Name\":\"\",\"Source\":\"\",\"Targets\":[]}" : new ExperimentJsonSerializer().Serialize(existing);
            PopupLabel(popup, "Each target requires Metric, TargetValue, AbsoluteTolerance and Weight (>0). Metrics: AttackRate, PeakPrevalence, TimeToPeakDays, HospitalizationRate, MortalityRate, HouseholdSecondaryAttackRate, Rt. No empirical target values are bundled.", 16, 509, 850, 70);
            CreateButton(popup, 16, 595, 240, "Attach target set", () => RunAction(() => controller.SetCalibrationTargets(scenarioIndex, input.text)));
            CreateButton(popup, 270, 595, 240, "Clear targets", () => RunAction(() => controller.SetCalibrationTargets(scenarioIndex, "")));
        }
    }
}
