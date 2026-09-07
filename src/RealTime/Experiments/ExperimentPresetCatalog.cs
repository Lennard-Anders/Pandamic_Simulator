namespace RealTime.Experiments
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using RealTime.Config;
    using RealTime.Pandemic;

    public sealed class ExperimentInterventionPhase
    {
        public double ActivationDay { get; set; }
        public ExperimentScenarioSnapshot Settings { get; set; }
    }

    public sealed class ExperimentInterventionSchedule
    {
        public List<ExperimentInterventionPhase> AdditionalPhases { get; set; } = new List<ExperimentInterventionPhase>();
        public int PhaseCount => 1 + (AdditionalPhases?.Count ?? 0);
        public double DayAt(int index) => index == 0 ? ActivationDay : AdditionalPhases[index - 1].ActivationDay;
        public ExperimentScenarioSnapshot SettingsAfter(int appliedCount) => appliedCount == 0 ? Before : appliedCount == 1 ? After : AdditionalPhases[appliedCount - 2].Settings;
        public void Validate(ExperimentScenarioSnapshot initial)
        {
            if (initial == null || Before == null || Before.Diff(initial).Count != 0) throw new ArgumentException("Schedule Before must equal initial settings.");
            double previous = 0;
            for (int i = 0; i < PhaseCount; i++)
            {
                double day = DayAt(i), steps = day * 1440d / initial.EpidemicStepMinutes;
                if (double.IsNaN(steps) || double.IsInfinity(steps) || day <= previous || steps != Math.Floor(steps)) throw new ArgumentException("Phases require strictly increasing positive days on exact epidemic steps.");
                var settings = SettingsAfter(i + 1);
                if (settings == null) throw new ArgumentException("Every phase requires a full configuration.");
                var validation = ExperimentPlanValidator.ValidateSettings(settings);
                if (!validation.IsValid) throw new ArgumentException(string.Join("; ", validation.Errors.ToArray()));
                foreach (var difference in initial.Diff(settings))
                    if (!ExperimentPresetCatalog.IsInterventionProperty(difference.PropertyName)) throw new ArgumentException("Scheduled biological changes are not supported: " + difference.PropertyName);
                previous = day;
            }
        }

        public double ActivationDay { get; set; }
        public ExperimentScenarioSnapshot Before { get; set; }
        public ExperimentScenarioSnapshot After { get; set; }
    }

    public sealed class ExperimentPreset
    {
        public string PresetId { get; set; }
        public int Version { get; set; } = 1;
        public string DisplayName { get; set; }
        public string ShortDescription { get; set; }
        public string LongDescription { get; set; }
        public string Category { get; set; }
        public IList<ExperimentSettingDifference> ParameterChanges { get; set; }
    }

    /// <summary>Controlled perturbations of an explicit baseline; these are not calibrated scenarios.</summary>
    public static class ExperimentPresetCatalog
    {
        private static readonly string[] Names =
        {
            "Control — No Interventions", "Low Transmission — Control", "High Transmission — Control",
            "Masks Only — Broad Adoption", "Testing & Isolation — Fast Detection",
            "Tracing & Quarantine — Contact Containment", "Lockdown Only — Reactive Closures",
            "Layered Response — Moderate", "Layered Response — Strong", "Delayed Response — Day 7 Intervention",
        };
        private static readonly string[] Ids = { "control", "low-transmission", "high-transmission", "masks-only", "fast-detection", "contact-containment", "reactive-closures", "layered-moderate", "layered-strong", "delayed-day-7" };
        private static readonly string[] Descriptions =
        {
            "Natural epidemic progression without active containment measures. Biological and transmission parameters remain unchanged.",
            "Control with 0.75 × baseline indoor and outdoor transmission. The 25% reduction is a generic experimental perturbation.",
            "Control with 1.25 × baseline indoor and outdoor transmission, bounded to valid percentages. The 25% increase is a stress test.",
            "Masks only: ignore/source control/personal protection = 20/40/40%. Mask factor 2 is an editable model assumption, not a clinically calibrated efficacy.",
            "Testing and positive-case isolation: 2 × baseline capacity, 0.5 × result delay, pending-result quarantine; diagnostic accuracy unchanged.",
            "Testing, isolation and contact quarantine with 70% app and 70% manual tracing adoption. Masks and lockdown disabled.",
            "Reactive non-essential closures at 5% detected prevalence; reopen at 2%, minimum closure 3 days, cooldown 2 days. Baseline testing supports observation.",
            "Masks 20/40/40%; testing capacity 1.5 × baseline, delay 0.75 × baseline; 60% app/manual tracing, isolation and pending/contact quarantine. No broad lockdown.",
            "Masks 10/45/45%; testing capacity 2 × baseline, delay 0.5 × baseline; 80% app/manual tracing, isolation and quarantine; detected-prevalence closures at 3%, reopen at 1%, minimum 3 days, cooldown 2 days.",
            "Control through day 7, then the exact moderate layered response. The immutable scenario records both configurations and the activation time.",
        };

        public static string[] DisplayNames => (string[])Names.Clone();

        internal static bool IsInterventionProperty(string name)
        {
            return name.EndsWith("CompliancePercent", StringComparison.Ordinal) || name == "MaskBehavior" || name.StartsWith("Ratio", StringComparison.Ordinal)
                || name == "TransmissionProbabilityReduction" || name == "QuarantineBehavior"
                || name == "OnlyTestedCitizensToQuarantine" || name == "LockdownBehavior"
                || name == "InitialLockdownEnabled" || name == "AutomaticPolicyTriggerMetric"
                || name == "BuildingContactTracingProbability" || name == "AppBasedContactTracingProbability"
                || name == "RelativeTestCapacity" || name == "PercentageOfTestsReservedForSickCitizens"
                || name == "MinimumTestDuration" || name == "MaximumTestDuration"
                || name == "TestSensitivityPercent" || name == "TestSpecificityPercent"
                || name == "RetestIntervalDays" || name == "QuarantineWhileAwaitingTestResult"
                || name.StartsWith("Close", StringComparison.Ordinal) || name.StartsWith("Reopen", StringComparison.Ordinal)
                || name.EndsWith("ClosureDurationDays", StringComparison.Ordinal) || name.EndsWith("LockdownCooldownDurationDays", StringComparison.Ordinal);
        }

        public static ExperimentPreset Describe(int index, ExperimentScenarioSnapshot baseline)
        {
            var scenario = Create(index, baseline);
            return new ExperimentPreset
            {
                PresetId = Ids[index], Version = scenario.PresetVersion, DisplayName = Names[index], ShortDescription = Descriptions[index],
                LongDescription = Descriptions[index] + " All values are editable experimental settings, not validated population estimates or policy recommendations. Baseline means the configuration before applying this preset. Integer delays are rounded to the nearest day; percentage values are clamped to 0–100%."
                    + (HasMaskPhase(index) ? " Mask phases use factor 2: hourly transmission probability is divided by 2 for effective source control and by 2 for effective wearer protection (by 4 when both apply). This shared-factor assumption is not a measured infection-risk reduction." : string.Empty)
                    + (index == 6 || index == 8 ? " Essential Services retains a 100% close/reopen threshold; it does not close at zero detected cases." : string.Empty),
                Category = index < 3 ? "Control / sensitivity" : index == 9 ? "Timing" : "Interventions",
                ParameterChanges = baseline.Diff(scenario.Settings),
            };
        }

        public static ExperimentScenario Create(int index, ExperimentScenarioSnapshot baseline)
        {
            if (baseline == null) throw new ArgumentNullException(nameof(baseline));
            if (index < 0 || index >= Names.Length) throw new ArgumentOutOfRangeException(nameof(index));
            var settings = baseline.Clone();
            Control(settings);
            switch (index)
            {
                case 1:
                case 2:
                    double multiplier = index == 1 ? 0.75 : 1.25;
                    settings.IndoorDiseaseTransmissionProbability = Percent(baseline.IndoorDiseaseTransmissionProbability * multiplier);
                    settings.OutdoorDiseaseTransmissionProbability = Percent(baseline.OutdoorDiseaseTransmissionProbability * multiplier);
                    break;
                case 3: Masks(settings, 20); break;
                case 4: Testing(settings, baseline, 2, 0.5); break;
                case 5: Testing(settings, baseline, 1, 1); Tracing(settings, 70); break;
                case 6:
                    settings.RelativeTestCapacity = baseline.RelativeTestCapacity;
                    Closures(settings, 5, 2);
                    break;
                case 7: Moderate(settings, baseline); break;
                case 8:
                    Masks(settings, 10); Testing(settings, baseline, 2, 0.5); Tracing(settings, 80); Closures(settings, 3, 1);
                    break;
            }
            var scenario = new ExperimentScenario
            {
                ScenarioId = Guid.NewGuid().ToString("N"), Name = Names[index], Settings = settings,
                PresetId = Ids[index], PresetVersion = index == 8 ? 3 : index == 6 || HasMaskPhase(index) ? 2 : 1,
            };
            if (index == 9)
            {
                var after = settings.Clone();
                Moderate(after, baseline);
                scenario.InterventionSchedule = new ExperimentInterventionSchedule { ActivationDay = 7, Before = settings.Clone(), After = after };
            }
            return scenario;
        }

        private static void Control(ExperimentScenarioSnapshot s)
        {
            s.MaskBehavior = MaskBehavior.None;
            s.QuarantineBehavior = QuarantineBehavior.None;
            s.QuarantineWhileAwaitingTestResult = false;
            s.InitialLockdownEnabled = false;
            s.LockdownBehavior = LockdownBehavior.None;
            s.RelativeTestCapacity = 0;
            s.AppBasedContactTracingProbability = 0;
            s.BuildingContactTracingProbability = 0;
            foreach (PropertyInfo p in typeof(ExperimentScenarioSnapshot).GetProperties())
            {
                if (p.Name.EndsWith("DuringLockdown", StringComparison.Ordinal)) p.SetValue(s, false, null);
                if (p.Name.EndsWith("ThresholdPercent", StringComparison.Ordinal) && (p.Name.StartsWith("Close", StringComparison.Ordinal) || p.Name.StartsWith("Reopen", StringComparison.Ordinal))) p.SetValue(s, 0f, null);
            }
        }

        private static void Masks(ExperimentScenarioSnapshot s, int ignore)
        {
            s.MaskBehavior = MaskBehavior.Full;
            s.TransmissionProbabilityReduction = RealTimeConfig.DefaultMaskReductionFactor;
            s.RatioIgnoreMasks = ignore;
            s.RatioOtherProtectionMask = (100 - ignore) / 2;
            s.RatioOwnProtectionMask = (100 - ignore) / 2;
        }

        private static void Testing(ExperimentScenarioSnapshot s, ExperimentScenarioSnapshot baseline, double capacity, double delay)
        {
            s.RelativeTestCapacity = Percent(baseline.RelativeTestCapacity * capacity);
            s.MinimumTestDuration = (uint)Math.Round(baseline.MinimumTestDuration * delay, MidpointRounding.AwayFromZero);
            s.QuarantineWhileAwaitingTestResult = true;
            s.QuarantineBehavior = QuarantineBehavior.Self;
            s.OnlyTestedCitizensToQuarantine = true;
        }

        private static void Tracing(ExperimentScenarioSnapshot s, float adoption)
        {
            s.AppBasedContactTracingProbability = adoption;
            s.BuildingContactTracingProbability = adoption;
            s.QuarantineBehavior = QuarantineBehavior.Contacts;
        }

        private static void Moderate(ExperimentScenarioSnapshot s, ExperimentScenarioSnapshot baseline)
        {
            Masks(s, 20); Testing(s, baseline, 1.5, 0.75); Tracing(s, 60);
        }

        private static void Closures(ExperimentScenarioSnapshot s, float close, float reopen)
        {
            s.InitialLockdownEnabled = true;
            // Control clears thresholds. Restore the ordinary essential-services
            // threshold instead of accidentally closing this family at zero cases.
            s.CloseEssentialServicesThresholdPercent = 100f;
            s.ReopenEssentialServicesThresholdPercent = 100f;
            s.AutomaticPolicyTriggerMetric = PolicyTriggerMetric.DetectedPrevalence;
            foreach (string family in new[] { "Education", "PublicTransport", "Commercial", "LeisureTourismParks", "Office", "Industry", "GovernmentOtherPublic" })
            {
                Set(s, "Close" + family + "ThresholdPercent", close);
                Set(s, "Reopen" + family + "ThresholdPercent", reopen);
                Set(s, "Minimum" + family + "ClosureDurationDays", 3f);
                Set(s, family + "LockdownCooldownDurationDays", 2f);
            }
        }

        private static void Set(ExperimentScenarioSnapshot s, string property, float value) => typeof(ExperimentScenarioSnapshot).GetProperty(property).SetValue(s, value, null);
        private static bool HasMaskPhase(int index) => index == 3 || index == 7 || index == 8 || index == 9;
        private static float Percent(double value) => (float)Math.Max(0, Math.Min(100, value));
    }
}
