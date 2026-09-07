namespace RealTime.Experiments
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    public sealed class ExperimentSensitivityMetadata
    {
        public string Design { get; set; } = "OneAtATime";
        public string Parameter { get; set; }
        public double BaselineValue { get; set; }
        public double AppliedValue { get; set; }
        public double RelativeChange { get; set; }
    }

    /// <summary>Generic OAT design. Repetition seeds remain owned by the batch sequencer.</summary>
    public static class ExperimentSensitivity
    {
        public static IList<ExperimentScenario> Create(ExperimentScenario baseline, string parameter, double lower, double upper)
        {
            if (baseline?.Settings == null) throw new ArgumentNullException(nameof(baseline));
            if (double.IsNaN(lower) || double.IsInfinity(lower) || lower >= 0 || lower < -1
                || double.IsNaN(upper) || double.IsInfinity(upper) || upper <= 0)
                throw new ArgumentOutOfRangeException(nameof(lower), "Supply a negative lower and positive upper relative perturbation.");
            var property = typeof(ExperimentScenarioSnapshot).GetProperty(parameter);
            if (property == null || !property.CanWrite || property.PropertyType.IsEnum
                || (property.PropertyType != typeof(float) && property.PropertyType != typeof(double)
                    && property.PropertyType != typeof(int) && property.PropertyType != typeof(uint)) || parameter == "SchemaVersion")
                throw new ArgumentException("Select an explicit numeric scenario parameter.", nameof(parameter));
            double value = Convert.ToDouble(property.GetValue(baseline.Settings, null), CultureInfo.InvariantCulture);
            var scenarios = new List<ExperimentScenario>();
            var serializer = new ExperimentJsonSerializer();
            foreach (double change in new[] { lower, 0d, upper })
            {
                var scenario = serializer.Deserialize<ExperimentScenario>(serializer.Serialize(baseline));
                double applied = value * (1 + change);
                if (property.PropertyType == typeof(int) || property.PropertyType == typeof(uint)) applied = Math.Round(applied, MidpointRounding.AwayFromZero);
                property.SetValue(scenario.Settings, Convert.ChangeType(applied, property.PropertyType, CultureInfo.InvariantCulture), null);
                applied = Convert.ToDouble(property.GetValue(scenario.Settings, null), CultureInfo.InvariantCulture);
                var validation = ExperimentPlanValidator.ValidateSettings(scenario.Settings);
                if (!validation.IsValid) throw new ArgumentException("Perturbation is outside the valid model domain: " + string.Join("; ", validation.Errors.ToArray()));
                if (scenario.InterventionSchedule != null)
                    throw new ArgumentException("Select an unscheduled baseline for OAT sensitivity so only one parameter changes.");
                scenario.ScenarioId = Guid.NewGuid().ToString("N");
                scenario.Name = baseline.Name + " / " + parameter + " " + applied.ToString("G", CultureInfo.InvariantCulture);
                scenario.CustomizedAfterPreset = baseline.CustomizedAfterPreset || (!string.IsNullOrEmpty(scenario.PresetId) && applied != value);
                scenario.Sensitivity = new ExperimentSensitivityMetadata { Parameter = parameter, BaselineValue = value, AppliedValue = applied, RelativeChange = value == 0 ? 0 : (applied - value) / value };
                scenarios.Add(scenario);
            }
            return scenarios;
        }
    }
}
