// <copyright file="DiseaseProgressionEngine.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.Pandemic
{
    using System;
    using RealTime.Config;

    /// <summary>A unit-bearing, bounded scalar distribution. Mean/standard deviation are arithmetic values.</summary>
    internal sealed class DistributionSpec
    {
        public PandemicDistributionType Type { get; set; }

        public double Mean { get; set; }

        public double StandardDeviation { get; set; }

        public double Minimum { get; set; }

        public double Maximum { get; set; }

        public double FixedValue { get; set; }

        public void Validate(string name)
        {
            if (!Enum.IsDefined(typeof(PandemicDistributionType), Type))
            {
                throw new ArgumentOutOfRangeException(name, "The distribution type is unsupported.");
            }

            RequireFinite(Mean, name + ".Mean");
            RequireFinite(StandardDeviation, name + ".StandardDeviation");
            RequireFinite(Minimum, name + ".Minimum");
            RequireFinite(Maximum, name + ".Maximum");
            RequireFinite(FixedValue, name + ".FixedValue");
            if (StandardDeviation < 0d || Maximum < Minimum)
            {
                throw new ArgumentOutOfRangeException(name, "Distribution bounds or standard deviation are invalid.");
            }

            if (Type == PandemicDistributionType.Deterministic
                && (FixedValue < Minimum || FixedValue > Maximum))
            {
                throw new ArgumentOutOfRangeException(name, "The fixed value must lie within the configured bounds.");
            }

            if ((Type == PandemicDistributionType.LogNormal || Type == PandemicDistributionType.Gamma)
                && Mean <= 0d)
            {
                throw new ArgumentOutOfRangeException(name, "Log-normal and gamma arithmetic means must be positive.");
            }
        }

        private static void RequireFinite(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, "Distribution values must be finite.");
            }
        }
    }

    /// <summary>Deterministic sampling implementations independent of Unity and Cities: Skylines.</summary>
    internal static class DistributionSampler
    {
        private const int MaximumResampleAttempts = 64;

        public static double Sample(DistributionSpec spec, Random random)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            spec.Validate(nameof(spec));
            if (spec.Type == PandemicDistributionType.Deterministic)
            {
                return spec.FixedValue;
            }

            double last = spec.Mean;
            for (int attempt = 0; attempt < MaximumResampleAttempts; ++attempt)
            {
                last = Draw(spec, random);
                if (IsFinite(last) && last >= spec.Minimum && last <= spec.Maximum)
                {
                    return last;
                }
            }

            if (!IsFinite(last))
            {
                last = spec.Mean;
            }

            return Math.Max(spec.Minimum, Math.Min(spec.Maximum, last));
        }

        private static double Draw(DistributionSpec spec, Random random)
        {
            switch (spec.Type)
            {
                case PandemicDistributionType.Normal:
                    return spec.Mean + (spec.StandardDeviation * StandardNormal(random));
                case PandemicDistributionType.LogNormal:
                    if (spec.StandardDeviation == 0d)
                    {
                        return spec.Mean;
                    }

                    double variance = spec.StandardDeviation * spec.StandardDeviation;
                    double sigmaSquared = Math.Log(1d + (variance / (spec.Mean * spec.Mean)));
                    double mu = Math.Log(spec.Mean) - (sigmaSquared / 2d);
                    return Math.Exp(mu + (Math.Sqrt(sigmaSquared) * StandardNormal(random)));
                case PandemicDistributionType.Gamma:
                    if (spec.StandardDeviation == 0d)
                    {
                        return spec.Mean;
                    }

                    double shape = (spec.Mean * spec.Mean)
                        / (spec.StandardDeviation * spec.StandardDeviation);
                    double scale = (spec.StandardDeviation * spec.StandardDeviation) / spec.Mean;
                    if (!IsFinite(shape) || !IsFinite(scale) || shape <= 0d || scale <= 0d)
                    {
                        // At machine precision this is indistinguishable from a deterministic
                        // distribution; do not enter an unbounded rejection loop.
                        return spec.Mean;
                    }

                    return Gamma(shape, scale, random);
                default:
                    return spec.FixedValue;
            }
        }

        private static double Gamma(double shape, double scale, Random random)
        {
            if (shape < 1d)
            {
                double unit = Math.Max(double.Epsilon, random.NextDouble());
                return Gamma(shape + 1d, scale, random) * Math.Pow(unit, 1d / shape);
            }

            double d = shape - (1d / 3d);
            double c = 1d / Math.Sqrt(9d * d);
            for (int attempt = 0; attempt < 1024; ++attempt)
            {
                double normal = StandardNormal(random);
                double value = 1d + (c * normal);
                if (value <= 0d)
                {
                    continue;
                }

                value = value * value * value;
                double unit = random.NextDouble();
                if (unit < 1d - (0.0331d * normal * normal * normal * normal)
                    || Math.Log(Math.Max(double.Epsilon, unit))
                        < (0.5d * normal * normal) + (d * (1d - value + Math.Log(value))))
                {
                    return d * value * scale;
                }
            }

            // A valid Marsaglia-Tsang draw normally succeeds within a handful of attempts.
            // The deterministic mean is a finite fail-safe for pathological RNG/numeric input.
            return shape * scale;
        }

        private static double StandardNormal(Random random)
        {
            double first = Math.Max(double.Epsilon, random.NextDouble());
            double second = random.NextDouble();
            return Math.Sqrt(-2d * Math.Log(first)) * Math.Cos(2d * Math.PI * second);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }

    internal sealed class DiseaseTimelinePolicy
    {
        private const double MinimumIntervalDays = 1d / (24d * 60d * 60d * 1000d);

        public DistributionSpec ExposedDurationDays { get; set; }

        public DistributionSpec InfectiousStartDays { get; set; }

        public DistributionSpec InfectiousEndDays { get; set; }

        public DistributionSpec SymptomStartDays { get; set; }

        public DistributionSpec SymptomEndDays { get; set; }

        public DistributionSpec RecoveryDays { get; set; }

        public void Validate()
        {
            Require(ExposedDurationDays, nameof(ExposedDurationDays));
            Require(InfectiousStartDays, nameof(InfectiousStartDays));
            Require(InfectiousEndDays, nameof(InfectiousEndDays));
            Require(SymptomStartDays, nameof(SymptomStartDays));
            Require(SymptomEndDays, nameof(SymptomEndDays));
            Require(RecoveryDays, nameof(RecoveryDays));

            double earliestInfectiousStart = Math.Max(
                SupportMinimum(ExposedDurationDays),
                SupportMinimum(InfectiousStartDays));
            double latestInfectiousStart = Math.Min(
                SupportMaximum(InfectiousStartDays),
                Math.Min(
                    SupportMaximum(InfectiousEndDays) - MinimumIntervalDays,
                    SupportMaximum(RecoveryDays) - MinimumIntervalDays));
            if (earliestInfectiousStart > latestInfectiousStart)
            {
                throw new ArgumentException("The configured distributions cannot produce Exposure <= InfectiousStart < InfectiousEnd <= Recovery.");
            }

            double earliestSymptomStart = SupportMinimum(SymptomStartDays);
            double latestSymptomStart = Math.Min(
                SupportMaximum(SymptomStartDays),
                Math.Min(
                    SupportMaximum(SymptomEndDays) - MinimumIntervalDays,
                    SupportMaximum(RecoveryDays) - MinimumIntervalDays));
            if (earliestSymptomStart > latestSymptomStart)
            {
                throw new ArgumentException("The configured distributions cannot produce SymptomStart < SymptomEnd <= Recovery.");
            }
        }

        private static void Require(DistributionSpec spec, string name)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(name);
            }

            spec.Validate(name);
            if (spec.Minimum < 0d || spec.Mean < spec.Minimum || spec.Mean > spec.Maximum)
            {
                throw new ArgumentOutOfRangeException(
                    name,
                    "Disease-duration bounds must be nonnegative and contain the arithmetic mean.");
            }
        }

        private static double SupportMinimum(DistributionSpec spec)
        {
            return spec.Type == PandemicDistributionType.Deterministic ? spec.FixedValue : spec.Minimum;
        }

        private static double SupportMaximum(DistributionSpec spec)
        {
            return spec.Type == PandemicDistributionType.Deterministic ? spec.FixedValue : spec.Maximum;
        }
    }

    internal sealed class DiseaseTimelineSample
    {
        public double ExposedDurationDays { get; set; }

        public double InfectiousStartDays { get; set; }

        public double InfectiousEndDays { get; set; }

        public double? SymptomStartDays { get; set; }

        public double? SymptomEndDays { get; set; }

        public double MortalityStartDays { get; set; }

        public double RecoveryDays { get; set; }
    }

    /// <summary>Samples and validates individual disease courses without Unity dependencies.</summary>
    internal sealed class DiseaseProgressionEngine
    {
        // DateTime.AddDays on the supported .NET Framework rounds to milliseconds. A one-tick
        // epsilon would therefore collapse adjacent boundaries back onto the same instant.
        private const double MinimumIntervalDays = 1d / (24d * 60d * 60d * 1000d);
        private const int MaximumTimelineAttempts = 64;
        private DiseaseTimelinePolicy policy;
        private Random random;

        public DiseaseProgressionEngine(DiseaseTimelinePolicy policy, Random random)
        {
            Reset(policy, random);
        }

        public void Reset(DiseaseTimelinePolicy newPolicy, Random newRandom)
        {
            if (newPolicy == null)
            {
                throw new ArgumentNullException(nameof(newPolicy));
            }

            newPolicy.Validate();
            policy = newPolicy;
            random = newRandom ?? throw new ArgumentNullException(nameof(newRandom));
        }

        public DiseaseTimelineSample SampleTimeline(bool symptomatic)
        {
            for (int attempt = 0; attempt < MaximumTimelineAttempts; ++attempt)
            {
                double exposedDuration = DistributionSampler.Sample(policy.ExposedDurationDays, random);
                double infectiousStart = DistributionSampler.Sample(policy.InfectiousStartDays, random);
                double infectiousEnd = DistributionSampler.Sample(policy.InfectiousEndDays, random);
                double symptomStart = DistributionSampler.Sample(policy.SymptomStartDays, random);
                double symptomEnd = DistributionSampler.Sample(policy.SymptomEndDays, random);
                double recovery = DistributionSampler.Sample(policy.RecoveryDays, random);
                if (exposedDuration <= infectiousStart
                    && infectiousStart + MinimumIntervalDays <= infectiousEnd
                    && infectiousEnd <= recovery
                    && (!symptomatic
                        || (symptomStart + MinimumIntervalDays <= symptomEnd && symptomEnd <= recovery)))
                {
                    return CreateTimelineSample(
                        exposedDuration,
                        infectiousStart,
                        infectiousEnd,
                        symptomStart,
                        symptomEnd,
                        recovery,
                        symptomatic);
                }
            }

            return ProjectValidTimeline(symptomatic);
        }

        private DiseaseTimelineSample ProjectValidTimeline(bool symptomatic)
        {
            double infectiousStartLower = Math.Max(
                SupportMinimum(policy.ExposedDurationDays),
                SupportMinimum(policy.InfectiousStartDays));
            double infectiousStartUpper = Math.Min(
                SupportMaximum(policy.InfectiousStartDays),
                Math.Min(
                    SupportMaximum(policy.InfectiousEndDays) - MinimumIntervalDays,
                    SupportMaximum(policy.RecoveryDays) - MinimumIntervalDays));
            double infectiousStart = Clamp(
                PreferredValue(policy.InfectiousStartDays),
                infectiousStartLower,
                infectiousStartUpper);
            double exposedDuration = Clamp(
                PreferredValue(policy.ExposedDurationDays),
                SupportMinimum(policy.ExposedDurationDays),
                Math.Min(SupportMaximum(policy.ExposedDurationDays), infectiousStart));
            double infectiousEnd = Clamp(
                PreferredValue(policy.InfectiousEndDays),
                Math.Max(SupportMinimum(policy.InfectiousEndDays), infectiousStart + MinimumIntervalDays),
                Math.Min(SupportMaximum(policy.InfectiousEndDays), SupportMaximum(policy.RecoveryDays)));

            double symptomStart = Clamp(
                PreferredValue(policy.SymptomStartDays),
                SupportMinimum(policy.SymptomStartDays),
                Math.Min(
                    SupportMaximum(policy.SymptomStartDays),
                    Math.Min(
                        SupportMaximum(policy.SymptomEndDays) - MinimumIntervalDays,
                        SupportMaximum(policy.RecoveryDays) - MinimumIntervalDays)));
            double symptomEnd = Clamp(
                PreferredValue(policy.SymptomEndDays),
                Math.Max(SupportMinimum(policy.SymptomEndDays), symptomStart + MinimumIntervalDays),
                Math.Min(SupportMaximum(policy.SymptomEndDays), SupportMaximum(policy.RecoveryDays)));
            double recoveryLower = Math.Max(
                SupportMinimum(policy.RecoveryDays),
                Math.Max(infectiousEnd, symptomatic ? symptomEnd : infectiousEnd));
            double recovery = Clamp(
                PreferredValue(policy.RecoveryDays),
                recoveryLower,
                SupportMaximum(policy.RecoveryDays));
            return CreateTimelineSample(
                exposedDuration,
                infectiousStart,
                infectiousEnd,
                symptomStart,
                symptomEnd,
                recovery,
                symptomatic);
        }

        private static DiseaseTimelineSample CreateTimelineSample(
            double exposedDuration,
            double infectiousStart,
            double infectiousEnd,
            double symptomStart,
            double symptomEnd,
            double recovery,
            bool symptomatic)
        {
            var result = new DiseaseTimelineSample
            {
                ExposedDurationDays = exposedDuration,
                InfectiousStartDays = infectiousStart,
                InfectiousEndDays = infectiousEnd,
                RecoveryDays = recovery,
                MortalityStartDays = Clamp(symptomStart, 0d, recovery - MinimumIntervalDays),
            };
            if (symptomatic)
            {
                result.SymptomStartDays = symptomStart;
                result.SymptomEndDays = symptomEnd;
            }

            return result;
        }

        private static double PreferredValue(DistributionSpec spec)
        {
            return spec.Type == PandemicDistributionType.Deterministic ? spec.FixedValue : spec.Mean;
        }

        private static double SupportMinimum(DistributionSpec spec)
        {
            return spec.Type == PandemicDistributionType.Deterministic ? spec.FixedValue : spec.Minimum;
        }

        private static double SupportMaximum(DistributionSpec spec)
        {
            return spec.Type == PandemicDistributionType.Deterministic ? spec.FixedValue : spec.Maximum;
        }

        public DiseaseCourse CreateCourse(
            uint citizenId,
            DateTime exposureTime,
            bool symptomatic,
            DiseaseExposureKind exposureKind)
        {
            DiseaseTimelineSample sample = SampleTimeline(symptomatic);
            return CreateCourseFromSample(citizenId, exposureTime, symptomatic, exposureKind, sample);
        }

        public DiseaseCourse CreateInitialCourse(
            uint citizenId,
            DateTime simulationTime,
            double requestedInfectionAgeDays,
            bool symptomatic,
            DiseaseExposureKind exposureKind,
            out double actualInfectionAgeDays)
        {
            if (double.IsNaN(requestedInfectionAgeDays)
                || double.IsInfinity(requestedInfectionAgeDays)
                || requestedInfectionAgeDays < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(requestedInfectionAgeDays));
            }

            DiseaseTimelineSample sample = SampleTimeline(symptomatic);
            actualInfectionAgeDays = Math.Min(
                requestedInfectionAgeDays,
                sample.RecoveryDays - MinimumIntervalDays);
            DateTime exposureTime = simulationTime.AddDays(-actualInfectionAgeDays);
            return CreateCourseFromSample(citizenId, exposureTime, symptomatic, exposureKind, sample);
        }

        private static DiseaseCourse CreateCourseFromSample(
            uint citizenId,
            DateTime exposureTime,
            bool symptomatic,
            DiseaseExposureKind exposureKind,
            DiseaseTimelineSample sample)
        {
            DiseaseCourse course = new DiseaseCourse(
                citizenId,
                exposureTime,
                exposureTime.AddDays(sample.InfectiousStartDays),
                exposureTime.AddDays(sample.InfectiousEndDays),
                sample.SymptomStartDays.HasValue
                    ? (DateTime?)exposureTime.AddDays(sample.SymptomStartDays.Value)
                    : null,
                sample.SymptomEndDays.HasValue
                    ? (DateTime?)exposureTime.AddDays(sample.SymptomEndDays.Value)
                    : null,
                exposureTime.AddDays(sample.RecoveryDays),
                symptomatic,
                exposureKind);
            course.MortalityStartTime = exposureTime.AddDays(sample.MortalityStartDays);
            return course;
        }

        public static double CalculateStepMortalityProbability(
            double courseMortalityProbability,
            TimeSpan mortalityWindow,
            TimeSpan stepDuration,
            double hazardMultiplier)
        {
            if (double.IsNaN(courseMortalityProbability)
                || double.IsInfinity(courseMortalityProbability)
                || courseMortalityProbability < 0d
                || courseMortalityProbability > 1d
                || mortalityWindow <= TimeSpan.Zero
                || stepDuration < TimeSpan.Zero
                || double.IsNaN(hazardMultiplier)
                || double.IsInfinity(hazardMultiplier)
                || hazardMultiplier < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(courseMortalityProbability));
            }

            if (courseMortalityProbability <= 0d || stepDuration == TimeSpan.Zero || hazardMultiplier == 0d)
            {
                return 0d;
            }

            if (courseMortalityProbability >= 1d)
            {
                return 1d;
            }

            double exposureFraction = stepDuration.TotalDays / mortalityWindow.TotalDays;
            return 1d - Math.Exp(Math.Log(1d - courseMortalityProbability) * exposureFraction * hazardMultiplier);
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }

    internal sealed class InfectiousnessProfilePolicy
    {
        public PandemicInfectiousnessProfileType Type { get; set; }

        public double StartMultiplier { get; set; }

        public double PeakTimeFraction { get; set; }

        public double PeakMultiplier { get; set; }

        public double EndMultiplier { get; set; }

        public void Validate()
        {
            if (!Enum.IsDefined(typeof(PandemicInfectiousnessProfileType), Type)
                || !FiniteNonnegative(StartMultiplier)
                || !FiniteNonnegative(PeakMultiplier)
                || !FiniteNonnegative(EndMultiplier)
                || double.IsNaN(PeakTimeFraction)
                || double.IsInfinity(PeakTimeFraction)
                || PeakTimeFraction < 0d
                || PeakTimeFraction > 1d)
            {
                throw new ArgumentOutOfRangeException(nameof(InfectiousnessProfilePolicy));
            }
        }

        private static bool FiniteNonnegative(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d;
        }
    }

    internal static class InfectiousnessProfileEngine
    {
        public static double GetMultiplier(
            DiseaseCourse course,
            DateTime simulationTime,
            InfectiousnessProfilePolicy policy)
        {
            if (course == null || policy == null)
            {
                throw new ArgumentNullException(course == null ? nameof(course) : nameof(policy));
            }

            policy.Validate();
            if (!course.IsWithinInfectiousInterval(simulationTime)
                || course.CurrentDiseaseState == DiseaseState.Dead
                || course.CurrentDiseaseState == DiseaseState.Recovered
                || course.CurrentDiseaseState == DiseaseState.Removed)
            {
                return 0d;
            }

            if (policy.Type == PandemicInfectiousnessProfileType.Flat)
            {
                return 1d;
            }

            double durationTicks = course.InfectiousEndTime.Ticks - course.InfectiousStartTime.Ticks;
            double fraction = durationTicks <= 0d
                ? 0d
                : (simulationTime.Ticks - course.InfectiousStartTime.Ticks) / durationTicks;
            fraction = Math.Max(0d, Math.Min(1d, fraction));
            if (fraction <= policy.PeakTimeFraction)
            {
                double local = policy.PeakTimeFraction <= 0d ? 1d : fraction / policy.PeakTimeFraction;
                return Lerp(policy.StartMultiplier, policy.PeakMultiplier, local);
            }

            double remaining = 1d - policy.PeakTimeFraction;
            double tail = remaining <= 0d ? 1d : (fraction - policy.PeakTimeFraction) / remaining;
            return Lerp(policy.PeakMultiplier, policy.EndMultiplier, tail);
        }

        public static double ApplyToProbability(double probability, double multiplier)
        {
            if (double.IsNaN(probability) || double.IsInfinity(probability) || probability < 0d || probability > 1d
                || double.IsNaN(multiplier) || double.IsInfinity(multiplier) || multiplier < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(probability));
            }

            if (probability <= 0d || multiplier <= 0d)
            {
                return 0d;
            }

            if (probability >= 1d)
            {
                return 1d;
            }

            return 1d - Math.Exp(Math.Log(1d - probability) * multiplier);
        }

        private static double Lerp(double left, double right, double fraction)
        {
            return left + ((right - left) * fraction);
        }
    }
}
