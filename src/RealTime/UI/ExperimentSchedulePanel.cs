namespace RealTime.UI
{
    using System;
    using System.Globalization;
    using System.Linq;
    using RealTime.Experiments;

    internal sealed partial class ExperimentBatchPanel
    {
        private void ShowScheduleEditor(int scenarioIndex)
        {
            var scenario = controller.GetViewState().Plan.Scenarios[scenarioIndex];
            var serializer = new ExperimentJsonSerializer();
            var initial = scenario.Settings.Clone();
            var schedule = scenario.InterventionSchedule == null ? new ExperimentInterventionSchedule { Before = initial.Clone(), After = initial.Clone(), ActivationDay = 1 } : serializer.Deserialize<ExperimentInterventionSchedule>(serializer.Serialize(scenario.InterventionSchedule));
            var popup = OpenScenarioPopup("Multi-phase intervention schedule");
            PopupLabel(popup, "Each phase stores its full intervention configuration. Days must increase and align with the epidemic step. Changes here remain a draft until Save schedule.", 16, 52, 850, 64);
            var phases = CreateDropDown(popup, 16, 127, 650);
            var day = CreateTextField(popup, 680, 127, 185); day.tooltip = "Activation day from simulation start";
            var properties = typeof(ExperimentScenarioSnapshot).GetProperties().Where(p => p.CanWrite && ExperimentPresetCatalog.IsInterventionProperty(p.Name) && !p.Name.StartsWith("Ratio", StringComparison.Ordinal)).OrderBy(p => p.Name, StringComparer.Ordinal).ToArray();
            var parameter = CreateDropDown(popup, 16, 171, 850);
            parameter.items = properties.Select(p => p.Name).Concat(new[] { "MaskPopulationSplit" }).ToArray(); parameter.selectedIndex = 0;
            var value = CreateTextField(popup, 16, 215, 850);
            var preview = CreateTextField(popup, 16, 310, 850); preview.multiline = true; preview.readOnly = true; preview.height = 235;
            Action render = () =>
            {
                int i = Math.Max(0, phases.selectedIndex);
                if (i >= schedule.PhaseCount) i = 0;
                day.text = schedule.DayAt(i).ToString("R", CultureInfo.InvariantCulture);
                var settings = schedule.SettingsAfter(i + 1);
                int propertyIndex = Math.Max(0, parameter.selectedIndex);
                value.text = propertyIndex == properties.Length ? settings.RatioIgnoreMasks + "," + settings.RatioOtherProtectionMask + "," + settings.RatioOwnProtectionMask : Convert.ToString(properties[propertyIndex].GetValue(settings, null), CultureInfo.InvariantCulture);
                value.tooltip = propertyIndex < properties.Length && properties[propertyIndex].PropertyType.IsEnum ? string.Join(", ", Enum.GetNames(properties[propertyIndex].PropertyType)) : "Invariant numeric values; mask split: Ignore,Source,Personal summing to 100.";
                preview.text = string.Join("\n", initial.Diff(settings).Select(d => d.PropertyName + ": " + d.LeftValue + " -> " + d.RightValue).ToArray());
            };
            Action relist = () => { int index = Math.Max(0, phases.selectedIndex); phases.items = Enumerable.Range(0, schedule.PhaseCount).Select(i => "Phase " + (i + 1) + " ? day " + schedule.DayAt(i).ToString("0.###", CultureInfo.InvariantCulture)).ToArray(); phases.selectedIndex = Math.Min(index, schedule.PhaseCount - 1); render(); };
            phases.eventSelectedIndexChanged += (c, i) => render(); parameter.eventSelectedIndexChanged += (c, i) => render(); relist();
            CreateButton(popup, 16, 260, 270, "Apply phase value / day", () =>
            {
                try
                {
                    var draft = serializer.Deserialize<ExperimentInterventionSchedule>(serializer.Serialize(schedule));
                    int index = Math.Max(0, phases.selectedIndex);
                    double activation = double.Parse(day.text, CultureInfo.InvariantCulture);
                    if (index == 0) draft.ActivationDay = activation; else draft.AdditionalPhases[index - 1].ActivationDay = activation;
                    var settings = draft.SettingsAfter(index + 1);
                    if (parameter.selectedIndex == properties.Length)
                    {
                        string[] split = value.text.Split(','); if (split.Length != 3) throw new ArgumentException("Supply three mask percentages.");
                        settings.RatioIgnoreMasks = int.Parse(split[0], CultureInfo.InvariantCulture); settings.RatioOtherProtectionMask = int.Parse(split[1], CultureInfo.InvariantCulture); settings.RatioOwnProtectionMask = int.Parse(split[2], CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        var property = properties[parameter.selectedIndex];
                        property.SetValue(settings, property.PropertyType.IsEnum ? Enum.Parse(property.PropertyType, value.text, true) : Convert.ChangeType(value.text, property.PropertyType, CultureInfo.InvariantCulture), null);
                    }
                    draft.Validate(initial); schedule = draft; relist();
                }
                catch (Exception ex) { preview.text = "Not applied: " + ex.Message; }
            });
            CreateButton(popup, 302, 260, 270, "Add next phase (+1 day)", () => { schedule.AdditionalPhases.Add(new ExperimentInterventionPhase { ActivationDay = schedule.DayAt(schedule.PhaseCount - 1) + 1, Settings = schedule.SettingsAfter(schedule.PhaseCount).Clone() }); relist(); phases.selectedIndex = schedule.PhaseCount - 1; });
            CreateButton(popup, 588, 260, 277, "Remove selected phase", () =>
            {
                if (schedule.PhaseCount == 1) { preview.text = "Use Clear schedule to remove the only phase."; return; }
                int index = Math.Max(0, phases.selectedIndex);
                if (index == 0) { schedule.ActivationDay = schedule.AdditionalPhases[0].ActivationDay; schedule.After = schedule.AdditionalPhases[0].Settings; schedule.AdditionalPhases.RemoveAt(0); }
                else schedule.AdditionalPhases.RemoveAt(index - 1);
                phases.selectedIndex = 0; relist();
            });
            CreateButton(popup, 16, 575, 300, "Save schedule to scenario", () => RunAction(() => controller.SetInterventionSchedule(scenarioIndex, serializer.Serialize(schedule))));
            CreateButton(popup, 340, 575, 260, "Clear scenario schedule", () => RunAction(() => controller.SetInterventionSchedule(scenarioIndex, "")));
        }
    }
}
