// <copyright file="ExperimentRunIntegrityMonitor.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.Pandemic
{
    using System;

    /// <summary>A fatal integrity failure that makes a scientific run unusable.</summary>
    internal sealed class ExperimentRunIntegrityFailure
    {
        public string Code { get; set; }

        public string Message { get; set; }

        public string Detail { get; set; }

        public DateTime SimulationTime { get; set; }
    }

    /// <summary>Central, fail-closed integrity state for one pandemic run.</summary>
    internal sealed class ExperimentRunIntegrityMonitor
    {
        public ExperimentRunIntegrityFailure Failure { get; private set; }

        public bool HasFailed => Failure != null;

        public void Reset()
        {
            Failure = null;
        }

        public void Fail(string code, string message, DateTime simulationTime, Exception exception)
        {
            if (HasFailed)
            {
                return;
            }

            if (string.IsNullOrEmpty(code))
            {
                throw new ArgumentException("An integrity failure requires a code.", nameof(code));
            }

            Failure = new ExperimentRunIntegrityFailure
            {
                Code = code,
                Message = message ?? string.Empty,
                Detail = exception?.ToString(),
                SimulationTime = simulationTime,
            };
        }

        public void ValidateDiseaseState(DiseaseStateEngine engine, DateTime simulationTime)
        {
            if (engine == null)
            {
                throw new ArgumentNullException(nameof(engine));
            }

            System.Collections.Generic.IList<string> errors = engine.ValidateInvariants(simulationTime);
            if (errors.Count > 0)
            {
                Fail("StateInvariantViolation", string.Join(" ", new System.Collections.Generic.List<string>(errors).ToArray()), simulationTime, null);
            }
        }
    }
}
