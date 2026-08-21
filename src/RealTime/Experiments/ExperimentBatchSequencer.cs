// <copyright file="ExperimentBatchSequencer.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.Experiments
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    /// <summary>Validates durable state transitions.</summary>
    public static class ExperimentBatchStateMachine
    {
        public static bool TryTransition(
            ExperimentBatchState state,
            ExperimentBatchExecutionState target,
            string utcNow,
            out string error)
        {
            if (state == null)
            {
                throw new ArgumentNullException("state");
            }

            if (!CanTransition(state.State, target))
            {
                error = string.Format(
                    CultureInfo.InvariantCulture,
                    "The experiment batch cannot transition from {0} to {1}.",
                    state.State,
                    target);
                return false;
            }

            state.State = target;
            state.UpdatedUtc = utcNow;
            error = null;
            return true;
        }

        public static bool CanTransition(ExperimentBatchExecutionState source, ExperimentBatchExecutionState target)
        {
            if (source == target)
            {
                return true;
            }

            if (!IsTerminal(source)
                && (IsFailure(target)
                    || target == ExperimentBatchExecutionState.Interrupted))
            {
                return true;
            }

            if (!IsTerminal(source) && target == ExperimentBatchExecutionState.Aborting)
            {
                return true;
            }

            switch (source)
            {
                case ExperimentBatchExecutionState.Idle:
                    return target == ExperimentBatchExecutionState.Validating;
                case ExperimentBatchExecutionState.Validating:
                    return target == ExperimentBatchExecutionState.AwaitingConfirmation;
                case ExperimentBatchExecutionState.AwaitingConfirmation:
                    return target == ExperimentBatchExecutionState.PreparingRun;
                case ExperimentBatchExecutionState.PreparingRun:
                    return target == ExperimentBatchExecutionState.RequestingBaselineLoad;
                case ExperimentBatchExecutionState.RequestingBaselineLoad:
                    return target == ExperimentBatchExecutionState.WaitingForLevelUnload;
                case ExperimentBatchExecutionState.WaitingForLevelUnload:
                    return target == ExperimentBatchExecutionState.WaitingForLevelLoad;
                case ExperimentBatchExecutionState.WaitingForLevelLoad:
                    return target == ExperimentBatchExecutionState.WaitingForLevelReady;
                case ExperimentBatchExecutionState.WaitingForLevelReady:
                    return target == ExperimentBatchExecutionState.ApplyingScenario;
                case ExperimentBatchExecutionState.ApplyingScenario:
                    return target == ExperimentBatchExecutionState.StartingRun;
                case ExperimentBatchExecutionState.StartingRun:
                    return target == ExperimentBatchExecutionState.Running;
                case ExperimentBatchExecutionState.Running:
                    return target == ExperimentBatchExecutionState.Paused
                        || target == ExperimentBatchExecutionState.FinalizingRun;
                case ExperimentBatchExecutionState.Paused:
                    return target == ExperimentBatchExecutionState.Running
                        || target == ExperimentBatchExecutionState.FinalizingRun;
                case ExperimentBatchExecutionState.FinalizingRun:
                    return target == ExperimentBatchExecutionState.ExportingRun;
                case ExperimentBatchExecutionState.ExportingRun:
                    return target == ExperimentBatchExecutionState.CommittingRun;
                case ExperimentBatchExecutionState.CommittingRun:
                    return target == ExperimentBatchExecutionState.CompletingRun;
                case ExperimentBatchExecutionState.CompletingRun:
                    return target == ExperimentBatchExecutionState.PreparingNextRun
                        || target == ExperimentBatchExecutionState.ReturningToBaseline
                        || target == ExperimentBatchExecutionState.Completed;
                case ExperimentBatchExecutionState.PreparingNextRun:
                    return target == ExperimentBatchExecutionState.PreparingRun;
                case ExperimentBatchExecutionState.Aborting:
                    return target == ExperimentBatchExecutionState.ReturningToBaseline
                        || target == ExperimentBatchExecutionState.Aborted;
                case ExperimentBatchExecutionState.ReturningToBaseline:
                    return target == ExperimentBatchExecutionState.RequestingBaselineLoad
                        || target == ExperimentBatchExecutionState.Completed;
                case ExperimentBatchExecutionState.Interrupted:
                    return target == ExperimentBatchExecutionState.Validating
                        || target == ExperimentBatchExecutionState.PreparingRun
                        || target == ExperimentBatchExecutionState.Aborting;
                case ExperimentBatchExecutionState.LoadFailed:
                    return target == ExperimentBatchExecutionState.PreparingRun
                        || target == ExperimentBatchExecutionState.ReturningToBaseline
                        || target == ExperimentBatchExecutionState.Aborting;
                case ExperimentBatchExecutionState.ScenarioApplyFailed:
                case ExperimentBatchExecutionState.RunFailed:
                    return target == ExperimentBatchExecutionState.PreparingRun
                        || target == ExperimentBatchExecutionState.Aborting;
                case ExperimentBatchExecutionState.ExportFailed:
                    return target == ExperimentBatchExecutionState.FinalizingRun
                        || target == ExperimentBatchExecutionState.Aborting;
                case ExperimentBatchExecutionState.ReturnFailed:
                    return target == ExperimentBatchExecutionState.ReturningToBaseline
                        || target == ExperimentBatchExecutionState.Aborting
                        || target == ExperimentBatchExecutionState.Aborted;
                default:
                    return false;
            }
        }

        public static bool IsTerminal(ExperimentBatchExecutionState state)
        {
            return state == ExperimentBatchExecutionState.Completed
                || IsFailure(state)
                || state == ExperimentBatchExecutionState.Interrupted
                || state == ExperimentBatchExecutionState.Aborted;
        }

        private static bool IsFailure(ExperimentBatchExecutionState state)
        {
            return state == ExperimentBatchExecutionState.Failed
                || state == ExperimentBatchExecutionState.BaselineValidationFailed
                || state == ExperimentBatchExecutionState.LoadFailed
                || state == ExperimentBatchExecutionState.ScenarioApplyFailed
                || state == ExperimentBatchExecutionState.RunFailed
                || state == ExperimentBatchExecutionState.ExportFailed
                || state == ExperimentBatchExecutionState.ReturnFailed;
        }
    }

    /// <summary>Pure, deterministic scenario/repetition sequencer.</summary>
    public sealed class ExperimentBatchSequencer
    {
        private readonly IExperimentSeedProvider seedProvider;

        public ExperimentBatchSequencer(IExperimentSeedProvider seedProvider)
        {
            if (seedProvider == null)
            {
                throw new ArgumentNullException("seedProvider");
            }

            this.seedProvider = seedProvider;
        }

        public ExperimentBatchState CreateInitialState(ExperimentBatchPlan plan, string sessionId, string utcNow)
        {
            if (plan == null)
            {
                throw new ArgumentNullException("plan");
            }

            return new ExperimentBatchState
            {
                BatchId = plan.BatchId,
                SessionId = sessionId,
                State = ExperimentBatchExecutionState.Idle,
                ScenarioIndex = 0,
                RunIndex = 0,
                UpdatedUtc = utcNow,
            };
        }

        public ExperimentRunDescriptor SelectCurrentRun(ExperimentBatchPlan plan, ExperimentBatchState state)
        {
            EnsurePosition(plan, state);
            ExperimentScenario scenario = plan.Scenarios[state.ScenarioIndex];
            int masterSeed = seedProvider.GetMasterSeed(scenario, state.RunIndex);
            string runId = CreateRunId(scenario.ScenarioId, state.RunIndex);

            state.CurrentMasterSeed = masterSeed;
            state.CurrentRunId = runId;

            return new ExperimentRunDescriptor
            {
                RunId = runId,
                ScenarioIndex = state.ScenarioIndex,
                RunIndex = state.RunIndex,
                Scenario = scenario,
                MasterSeed = masterSeed,
                Seeds = seedProvider.DeriveSeeds(masterSeed),
            };
        }

        public bool RecordCompletion(
            ExperimentBatchPlan plan,
            ExperimentBatchState state,
            ExperimentRunDescriptor run,
            string completedUtc,
            string outputDirectory)
        {
            if (run == null)
            {
                throw new ArgumentNullException("run");
            }

            EnsureCollections(state);
            if (ContainsCompletion(state.CompletedRuns, run.RunId))
            {
                return HasRunAtCurrentPosition(plan, state);
            }

            if (!string.Equals(state.CurrentRunId, run.RunId, StringComparison.Ordinal)
                || state.ScenarioIndex != run.ScenarioIndex
                || state.RunIndex != run.RunIndex)
            {
                throw new InvalidOperationException("The completed run does not match the durable current run.");
            }

            state.CompletedRuns.Add(new ExperimentCompletedRun
            {
                RunId = run.RunId,
                ScenarioId = run.Scenario.ScenarioId,
                ScenarioIndex = run.ScenarioIndex,
                RunIndex = run.RunIndex,
                MasterSeed = run.MasterSeed,
                CompletedUtc = completedUtc,
                OutputDirectory = outputDirectory,
            });

            state.CurrentRunId = null;
            state.RunStartedUtc = null;
            state.TargetSimulationTimeUtc = null;

            if (state.RunIndex + 1 < run.Scenario.RunCount)
            {
                ++state.RunIndex;
                return true;
            }

            ++state.ScenarioIndex;
            state.RunIndex = 0;
            return state.ScenarioIndex < plan.Scenarios.Count;
        }

        public int CountTotalRuns(ExperimentBatchPlan plan)
        {
            if (plan == null || plan.Scenarios == null)
            {
                return 0;
            }

            int count = 0;
            foreach (ExperimentScenario scenario in plan.Scenarios)
            {
                count = checked(count + scenario.RunCount);
            }

            return count;
        }

        private static string CreateRunId(string scenarioId, int runIndex)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}-run-{1:D4}", scenarioId, runIndex + 1);
        }

        private static void EnsurePosition(ExperimentBatchPlan plan, ExperimentBatchState state)
        {
            if (plan == null)
            {
                throw new ArgumentNullException("plan");
            }

            if (state == null)
            {
                throw new ArgumentNullException("state");
            }

            if (!string.Equals(plan.BatchId, state.BatchId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The durable state belongs to a different batch.");
            }

            if (!HasRunAtCurrentPosition(plan, state))
            {
                throw new InvalidOperationException("The durable run position is outside the batch plan.");
            }
        }

        private static bool HasRunAtCurrentPosition(ExperimentBatchPlan plan, ExperimentBatchState state)
        {
            return plan != null
                && state != null
                && plan.Scenarios != null
                && state.ScenarioIndex >= 0
                && state.ScenarioIndex < plan.Scenarios.Count
                && state.RunIndex >= 0
                && plan.Scenarios[state.ScenarioIndex] != null
                && state.RunIndex < plan.Scenarios[state.ScenarioIndex].RunCount;
        }

        private static void EnsureCollections(ExperimentBatchState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException("state");
            }

            if (state.CompletedRuns == null)
            {
                state.CompletedRuns = new List<ExperimentCompletedRun>();
            }
        }

        private static bool ContainsCompletion(IEnumerable<ExperimentCompletedRun> completedRuns, string runId)
        {
            foreach (ExperimentCompletedRun completed in completedRuns)
            {
                if (completed != null && string.Equals(completed.RunId, runId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
