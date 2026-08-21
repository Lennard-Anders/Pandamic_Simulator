// <copyright file="ExperimentBatchSequencerTests.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTimeTests.Experiments
{
    using System;
    using System.Collections.Generic;
    using RealTime.Experiments;
    using NUnit.Framework;

    public sealed class ExperimentBatchSequencerTests
    {
        [Test]
        public void SequencerAdvancesAcrossRunsAndScenarios()
        {
            ExperimentBatchPlan plan = CreatePlan();
            ExperimentBatchSequencer sequencer = new ExperimentBatchSequencer(new FnvExperimentSeedProvider());
            ExperimentBatchState state = sequencer.CreateInitialState(plan, "session", "2026-01-01T00:00:00Z");

            ExperimentRunDescriptor first = sequencer.SelectCurrentRun(plan, state);
            Assert.That(first.MasterSeed, Is.EqualTo(11));
            Assert.That(sequencer.RecordCompletion(plan, state, first, "2026-01-01T00:01:00Z", "one"), Is.True);
            Assert.That(state.ScenarioIndex, Is.EqualTo(0));
            Assert.That(state.RunIndex, Is.EqualTo(1));

            ExperimentRunDescriptor second = sequencer.SelectCurrentRun(plan, state);
            Assert.That(second.MasterSeed, Is.EqualTo(12));
            Assert.That(sequencer.RecordCompletion(plan, state, second, "2026-01-01T00:02:00Z", "two"), Is.True);
            Assert.That(state.ScenarioIndex, Is.EqualTo(1));
            Assert.That(state.RunIndex, Is.EqualTo(0));

            ExperimentRunDescriptor third = sequencer.SelectCurrentRun(plan, state);
            Assert.That(sequencer.RecordCompletion(plan, state, third, "2026-01-01T00:03:00Z", "three"), Is.False);
            Assert.That(state.CompletedRuns.Count, Is.EqualTo(3));
        }

        [Test]
        public void SequenceIsScenarioMajorAndDisplayNumbersAreOneBased()
        {
            ExperimentBatchPlan plan = CreatePlan();
            plan.Scenarios[0].RunCount = 3;
            plan.Scenarios[1].RunCount = 2;
            ExperimentBatchSequencer sequencer = new ExperimentBatchSequencer(new FnvExperimentSeedProvider());
            ExperimentBatchState state = sequencer.CreateInitialState(plan, "session", "now");
            var order = new List<string>();

            bool hasMore;
            do
            {
                ExperimentRunDescriptor run = sequencer.SelectCurrentRun(plan, state);
                order.Add(run.Scenario.Name + (run.RunIndex + 1));
                hasMore = sequencer.RecordCompletion(plan, state, run, "later", run.RunId);
            }
            while (hasMore);

            Assert.That(order, Is.EqualTo(new[] { "A1", "A2", "A3", "B1", "B2" }));
        }

        [Test]
        public void CompletionReceiptIsIdempotent()
        {
            ExperimentBatchPlan plan = CreatePlan();
            ExperimentBatchSequencer sequencer = new ExperimentBatchSequencer(new FnvExperimentSeedProvider());
            ExperimentBatchState state = sequencer.CreateInitialState(plan, "session", "now");
            ExperimentRunDescriptor run = sequencer.SelectCurrentRun(plan, state);

            sequencer.RecordCompletion(plan, state, run, "later", "output");
            sequencer.RecordCompletion(plan, state, run, "later", "output");

            Assert.That(state.CompletedRuns, Has.Count.EqualTo(1));
            Assert.That(state.RunIndex, Is.EqualTo(1));
        }

        [Test]
        public void StateMachineRejectsSkippedPhases()
        {
            Assert.That(ExperimentBatchStateMachine.CanTransition(
                ExperimentBatchExecutionState.Idle,
                ExperimentBatchExecutionState.ValidatingBatch), Is.True);
            Assert.That(ExperimentBatchStateMachine.CanTransition(
                ExperimentBatchExecutionState.Idle,
                ExperimentBatchExecutionState.Running), Is.False);
            Assert.That(ExperimentBatchStateMachine.CanTransition(
                ExperimentBatchExecutionState.ExportingRun,
                ExperimentBatchExecutionState.CommittingRun), Is.True);
            Assert.That(ExperimentBatchStateMachine.CanTransition(
                ExperimentBatchExecutionState.Running,
                ExperimentBatchExecutionState.Aborted), Is.False);
            Assert.That(ExperimentBatchStateMachine.CanTransition(
                ExperimentBatchExecutionState.Running,
                ExperimentBatchExecutionState.Aborting), Is.True);
            Assert.That(ExperimentBatchStateMachine.CanTransition(
                ExperimentBatchExecutionState.Aborting,
                ExperimentBatchExecutionState.Aborted), Is.True);
        }

        private static ExperimentBatchPlan CreatePlan()
        {
            return new ExperimentBatchPlan
            {
                BatchId = "batch",
                Scenarios = new List<ExperimentScenario>
                {
                    new ExperimentScenario
                    {
                        ScenarioId = "a",
                        Name = "A",
                        FirstSeed = 11,
                        RunCount = 2,
                    },
                    new ExperimentScenario
                    {
                        ScenarioId = "b",
                        Name = "B",
                        FirstSeed = 20,
                        RunCount = 1,
                    },
                },
            };
        }
    }
}
