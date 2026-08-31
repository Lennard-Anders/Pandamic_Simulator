// <copyright file="CalibrationEngine.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.Pandemic
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal enum CalibrationMetric
    {
        AttackRate,
        PeakPrevalence,
        TimeToPeakDays,
        Rt,
        HospitalizationRate,
        MortalityRate,
        HouseholdSecondaryAttackRate,
    }

    internal sealed class CalibrationTarget
    {
        public CalibrationMetric Metric { get; set; }

        public double TargetValue { get; set; }

        public double AbsoluteTolerance { get; set; }

        public double Weight { get; set; } = 1d;
    }

    /// <summary>An explicit, serializable set of externally supplied calibration targets.</summary>
    internal sealed class CalibrationTargetSet
    {
        public CalibrationTargetSet()
        {
            Targets = new List<CalibrationTarget>();
        }

        public string Name { get; set; }

        public string Source { get; set; }

        public IList<CalibrationTarget> Targets { get; private set; }

        public void Validate()
        {
            var metrics = new HashSet<CalibrationMetric>();
            foreach (CalibrationTarget target in Targets)
            {
                if (target == null
                    || !Enum.IsDefined(typeof(CalibrationMetric), target.Metric)
                    || !Finite(target.TargetValue)
                    || !Finite(target.AbsoluteTolerance)
                    || target.AbsoluteTolerance < 0d
                    || !Finite(target.Weight)
                    || target.Weight <= 0d)
                {
                    throw new ArgumentOutOfRangeException(nameof(Targets), "Calibration targets must be finite and explicitly weighted.");
                }

                if (!metrics.Add(target.Metric))
                {
                    throw new InvalidOperationException("A calibration target set cannot contain duplicate metrics.");
                }
            }
        }

        private static bool Finite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }

    internal sealed class CalibrationMetricResult
    {
        public CalibrationMetric Metric { get; set; }

        public double TargetValue { get; set; }

        public double? ObservedValue { get; set; }

        public double? AbsoluteError { get; set; }

        public bool IsAvailable { get; set; }

        public bool IsWithinTolerance { get; set; }
    }

    internal sealed class CalibrationEvaluation
    {
        public CalibrationEvaluation()
        {
            Results = new List<CalibrationMetricResult>();
        }

        public IList<CalibrationMetricResult> Results { get; private set; }

        public bool AllAvailable => Results.All(result => result.IsAvailable);

        public bool AllWithinTolerance => AllAvailable && Results.All(result => result.IsWithinTolerance);

        public double? WeightedMeanAbsoluteError { get; set; }
    }

    /// <summary>Compares model outputs with explicit targets; unavailable metrics remain unavailable.</summary>
    internal sealed class CalibrationEngine
    {
        public CalibrationEvaluation Evaluate(
            CalibrationTargetSet targetSet,
            IDictionary<CalibrationMetric, double?> observations)
        {
            if (targetSet == null || observations == null)
            {
                throw new ArgumentNullException(targetSet == null ? nameof(targetSet) : nameof(observations));
            }

            targetSet.Validate();
            var evaluation = new CalibrationEvaluation();
            double weightedError = 0d;
            double totalWeight = 0d;
            foreach (CalibrationTarget target in targetSet.Targets)
            {
                double? observed = observations.TryGetValue(target.Metric, out double? value) ? value : null;
                bool available = observed.HasValue && !double.IsNaN(observed.Value) && !double.IsInfinity(observed.Value);
                double? error = available ? Math.Abs(observed.Value - target.TargetValue) : (double?)null;
                evaluation.Results.Add(new CalibrationMetricResult
                {
                    Metric = target.Metric,
                    TargetValue = target.TargetValue,
                    ObservedValue = available ? observed : null,
                    AbsoluteError = error,
                    IsAvailable = available,
                    IsWithinTolerance = available && error.Value <= target.AbsoluteTolerance,
                });
                if (available)
                {
                    weightedError += error.Value * target.Weight;
                    totalWeight += target.Weight;
                }
            }

            evaluation.WeightedMeanAbsoluteError = totalWeight == 0d
                ? (double?)null
                : weightedError / totalWeight;
            return evaluation;
        }
    }
}
