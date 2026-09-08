// <copyright file="ExperimentBatchService.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.Experiments
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using ColossalFramework;
    using ColossalFramework.IO;
    using RealTime.Config;
    using RealTime.Core;
    using RealTime.Pandemic;
    using RealTime.UI;
    using SkyTools.Tools;
    using UnityEngine;

    /// <summary>
    /// Authoritative, application-lifetime experiment state machine. All methods are invoked on the
    /// Unity main thread; no load or state transition is initiated by a simulation callback.
    /// </summary>
    internal sealed class ExperimentBatchService : IExperimentBatchPanelController, IDisposable
    {
        private const float LevelReadyTimeoutSeconds = 60f;
        private const string ControlOwnerPrefix = "TENUS experiment batch ";
        private const string UnsavedWarning = "Starting the batch reloads the exact selected baseline before every run, including Run 1. Any unsaved changes in the current city will be lost. The runner never saves or overwrites the baseline.";

        private readonly string modPath;
        private readonly string modVersion;
        private readonly ExperimentBuildIdentity buildIdentity;
        private readonly string sessionNonce = Guid.NewGuid().ToString("N");
        private readonly ExperimentJsonSerializer cloner = new ExperimentJsonSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 128 };
        private readonly AtomicJsonFileStore jsonStore = new AtomicJsonFileStore();
        private readonly ExperimentPlanValidator validator = new ExperimentPlanValidator();
        private readonly ExperimentBatchSequencer sequencer = new ExperimentBatchSequencer(new FnvExperimentSeedProvider());
        private readonly SaveGameCatalogService saveCatalog = new SaveGameCatalogService();
        private readonly SaveGameReloadService reloadService;
        private readonly ExperimentPersistenceService persistence;
        private readonly ExperimentRunCommitService runCommitService;
        private readonly ExperimentInvalidRunService invalidRunService;
        private readonly ExperimentActiveBatchLocatorService locator;
        private readonly ExperimentOutputRootResolver outputRootResolver;

        private readonly List<SaveGameCatalogEntry> baselineEntries = new List<SaveGameCatalogEntry>();
        private readonly List<string> catalogWarnings = new List<string>();

        private ExperimentBatchPlan draftPlan;
        private ExperimentBatchPlan activePlan;
        private ExperimentBatchState state;
        private ExperimentRunDescriptor currentRun;
        private PandemicRunContext currentRunContext;
        private PandemicRunExportRequest frozenExportRequest;
        private RealTimeCore levelCore;
        private ExperimentBatchPanel panel;
        private IDisposable controlLease;

        private string batchDirectory;
        private string plannedReloadToken;
        private string currentAttemptDirectory;
        private string lastCompletedRun;
        private float levelReadyDeadline;
        private DateTime runWallClockStartUtc;
        private DateTime lastConfigurationCheckSimulationTime;
        private bool autoSavePaused;
        private bool planFrozen;
        private bool returningToBaseline;
        private bool aborting;
        private bool disposed;
        private bool hostDestroyed;

        internal ExperimentBatchService(string modPath, string modVersion)
        {
            this.modPath = modPath;
            this.modVersion = modVersion;
            buildIdentity = ExperimentBuildIdentity.Read(typeof(ExperimentBatchService).Assembly);
            reloadService = new SaveGameReloadService(saveCatalog);
            persistence = new ExperimentPersistenceService(jsonStore);
            runCommitService = new ExperimentRunCommitService(jsonStore);
            invalidRunService = new ExperimentInvalidRunService(jsonStore);

            string experimentStateRoot = Path.Combine(
                Path.Combine(DataLocation.localApplicationData, "TENUS"),
                "Experiments");
            locator = new ExperimentActiveBatchLocatorService(experimentStateRoot, jsonStore);
            outputRootResolver = new ExperimentOutputRootResolver(DataLocation.localApplicationData, modPath);

            draftPlan = CreateDraftPlan();
            try
            {
                RefreshBaselineCatalog();
            }
            catch (Exception exception)
            {
                catalogWarnings.Add("The baseline save catalog is not available yet: " + exception.Message);
            }

            RecoverActiveBatch();
        }

        internal bool HasActiveControl
        {
            get { return controlLease != null; }
        }

        internal void AttachLevel(RealTimeCore core)
        {
            if (disposed)
            {
                return;
            }

            bool expectedLoad = state != null
                && (state.State == ExperimentBatchExecutionState.WaitingForLevelLoad
                    || state.State == ExperimentBatchExecutionState.WaitingForLevelUnload
                    || state.State == ExperimentBatchExecutionState.RequestingBaselineLoad);
            if (HasControlledBatch()
                && levelCore != null
                && core != null
                && !ReferenceEquals(levelCore, core)
                && !expectedLoad)
            {
                Interrupt("UnexpectedLevelLoad", "A level load not owned by this batch interrupted execution.", null, levelCore);
            }

            DetachLevel(levelCore);
            levelCore = core;
            if (levelCore == null)
            {
                return;
            }

            if (levelCore.PandemicLivePanel != null)
            {
                levelCore.PandemicLivePanel.ExperimentsRequested += ShowPanel;
            }

            panel = new ExperimentBatchPanel(this);

            if (state != null
                && (state.State == ExperimentBatchExecutionState.WaitingForLevelLoad
                    || state.State == ExperimentBatchExecutionState.WaitingForLevelUnload
                    || state.State == ExperimentBatchExecutionState.RequestingBaselineLoad))
            {
                Transition(ExperimentBatchExecutionState.WaitingForLevelReady);
                plannedReloadToken = null;
                levelReadyDeadline = Time.realtimeSinceStartup + LevelReadyTimeoutSeconds;
            }
        }

        internal void DetachLevel(RealTimeCore core)
        {
            RealTimeCore attached = levelCore;
            if (core != null && attached != null && !ReferenceEquals(core, attached))
            {
                return;
            }

            if (attached?.PandemicLivePanel != null)
            {
                attached.PandemicLivePanel.ExperimentsRequested -= ShowPanel;
            }

            panel?.Disable();
            panel = null;
            levelCore = null;
        }

        /// <summary>Handles the synchronous unload callback raised by <c>LoadingManager.LoadLevel</c>.</summary>
        internal void OnLevelUnloading(RealTimeCore core)
        {
            if (disposed)
            {
                DetachLevel(core);
                return;
            }

            bool ownedReload = IsOwnedReloadPending();
            if (ownedReload)
            {
                if (state.State == ExperimentBatchExecutionState.RequestingBaselineLoad)
                {
                    Transition(ExperimentBatchExecutionState.WaitingForLevelUnload);
                    Transition(ExperimentBatchExecutionState.WaitingForLevelLoad);
                }

                DetachLevel(core);
                return;
            }

            if (HasControlledBatch())
            {
                Interrupt("UnexpectedLevelUnload", "A level unload not owned by this batch interrupted execution.", null, core);
            }

            DetachLevel(core);
        }

        internal void Tick()
        {
            if (disposed || state == null)
            {
                return;
            }

            try
            {
                switch (state.State)
                {
                    case ExperimentBatchExecutionState.WaitingForLevelReady:
                        TickLevelReadiness();
                        break;
                    case ExperimentBatchExecutionState.Running:
                    case ExperimentBatchExecutionState.Paused:
                        if (Singleton<SimulationManager>.exists
                            && ExperimentBatchStateMachine.ShouldPollRun(state.State, SimulationManager.instance.SimulationPaused))
                        {
                            if (state.State == ExperimentBatchExecutionState.Paused)
                                Transition(ExperimentBatchExecutionState.Running);
                            TickRunning();
                        }
                        break;
                    case ExperimentBatchExecutionState.FinalizingRun:
                        ExportFrozenRun();
                        break;
                }
            }
            catch (Exception exception)
            {
                Fail(
                    ExperimentBatchExecutionState.Failed,
                    "ControllerFailure",
                    "The experiment controller encountered an unexpected error.",
                    exception,
                    true);
            }
        }

        internal void NotifyHostDestroyed()
        {
            if (hostDestroyed)
            {
                return;
            }

            hostDestroyed = true;
            if (!disposed)
            {
                Dispose();
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            if (HasControlledBatch())
            {
                Interrupt("ModDisabled", "The mod was disabled while the experiment batch was active.", null, levelCore);
            }

            levelCore?.PandemicManager?.AbortBatchRun();
            RestoreOriginalConfiguration(levelCore);
            DetachLevel(levelCore);
            ReleaseRuntimeOwnership();
        }

        public ExperimentBatchPanelViewState GetViewState()
        {
            ExperimentBatchPlan visiblePlan = activePlan ?? draftPlan;
            var view = new ExperimentBatchPanelViewState
            {
                Plan = Clone(visiblePlan),
                ExecutionState = state?.State ?? ExperimentBatchExecutionState.Idle,
                IsPlanReadOnly = planFrozen,
                CanStart = !planFrozen && levelCore != null,
                CanPause = state?.State == ExperimentBatchExecutionState.Running,
                CanResume = state?.State == ExperimentBatchExecutionState.Paused
                    || state?.State == ExperimentBatchExecutionState.Interrupted,
                CanAbort = state != null && planFrozen && !IsFinishedState(state.State),
                CanRetryBaselineLoad = state?.State == ExperimentBatchExecutionState.LoadFailed
                    || state?.State == ExperimentBatchExecutionState.ReturnFailed,
                CanRetryPreparation = state?.State == ExperimentBatchExecutionState.ScenarioApplyFailed
                    || state?.State == ExperimentBatchExecutionState.RunFailed,
                CanRetryExport = state?.State == ExperimentBatchExecutionState.ExportFailed
                    && frozenExportRequest != null
                    && string.Equals(state.SessionId, sessionNonce, StringComparison.Ordinal),
                StatusText = state == null ? "Idle" : StateText(state.State),
                LastCompletedRun = lastCompletedRun,
                ErrorText = state?.Error?.Message ?? state?.LastError,
            };

            foreach (SaveGameCatalogEntry entry in baselineEntries)
            {
                view.Baselines.Add(new ExperimentBaselineOption
                {
                    AssetFullName = entry.Identity.AssetFullName,
                    DisplayName = BuildBaselineDisplayName(entry),
                });
            }

            AddOutputOptions(view.OutputRoots);
            PopulateProgress(view);
            return view;
        }

        public ExperimentBatchPanelActionResult RefreshBaselineCatalog()
        {
            SaveGameCatalogSnapshot snapshot = saveCatalog.GetAvailableSaves();
            baselineEntries.Clear();
            baselineEntries.AddRange(snapshot.Entries);
            catalogWarnings.Clear();
            catalogWarnings.AddRange(snapshot.Warnings);
            return Success(snapshot.Entries.Count.ToString(CultureInfo.CurrentCulture) + " baseline saves found.");
        }

        public ExperimentBatchPanelActionResult SetBatchName(string batchName)
        {
            ExperimentBatchPanelActionResult editable = RequireEditable();
            if (editable != null)
            {
                return editable;
            }

            string value = (batchName ?? string.Empty).Trim();
            if (value.Length == 0)
            {
                return Failure("A batch name is required.");
            }

            draftPlan.BatchName = value;
            return Success("Batch name updated.");
        }

        public ExperimentBatchPanelActionResult SetOutputFolderName(string outputFolderName)
        {
            ExperimentBatchPanelActionResult editable = RequireEditable();
            if (editable != null)
            {
                return editable;
            }

            string value = (outputFolderName ?? string.Empty).Trim();
            if (value.Length == 0)
            {
                return Failure("An output folder name is required.");
            }

            draftPlan.OutputFolderName = value;
            return Success("Output folder name updated. A short uniqueness suffix will be appended.");
        }

        public ExperimentBatchPanelActionResult SelectBaseline(string assetFullName)
        {
            ExperimentBatchPanelActionResult editable = RequireEditable();
            if (editable != null)
            {
                return editable;
            }

            SaveGameCatalogEntry entry = baselineEntries.FirstOrDefault(candidate =>
                string.Equals(candidate.Identity.AssetFullName, assetFullName, StringComparison.Ordinal));
            if (entry == null)
            {
                return Failure("The selected exact save identity is not in the refreshed catalog.");
            }

            draftPlan.Baseline = Clone(entry.Identity);
            return Success("Baseline selected. It will be fingerprint-checked before every reload.");
        }

        public ExperimentBatchPanelActionResult SelectOutputRoot(string outputRoot)
        {
            ExperimentBatchPanelActionResult editable = RequireEditable();
            if (editable != null)
            {
                return editable;
            }

            ExperimentOutputRootKind? selected = FindOutputKind(outputRoot);
            if (!selected.HasValue)
            {
                return Failure("The selected output location is not one of the supported roots.");
            }

            var selection = new ExperimentOutputRootSelection { Kind = selected.Value };
            ExperimentOutputPreflightResult preflight = outputRootResolver.Preflight(selection);
            if (!preflight.Success)
            {
                return Failure("The selected output location is not writable: " + preflight.Error);
            }

            draftPlan.OutputLocation = selection;
            draftPlan.OutputRoot = preflight.ResolvedPath;
            return Success("Output root: " + preflight.ResolvedPath);
        }

        public ExperimentBatchPanelActionResult SetExecutionSpeed(ExperimentSpeedMode speed)
        {
            ExperimentBatchPanelActionResult editable = RequireEditable();
            if (editable != null)
            {
                return editable;
            }

            if (speed != ExperimentSpeedMode.PreserveStartingSpeed
                && speed != ExperimentSpeedMode.Speed1
                && speed != ExperimentSpeedMode.Speed2
                && speed != ExperimentSpeedMode.Speed3)
            {
                return Failure("Choose Preserve, Speed 1, Speed 2, or Speed 3.");
            }

            draftPlan.SpeedMode = speed;
            return Success("Execution speed selected.");
        }

        public ExperimentBatchPanelActionResult SetReturnToBaseline(bool returnToBaseline)
        {
            ExperimentBatchPanelActionResult editable = RequireEditable();
            if (editable != null)
            {
                return editable;
            }

            draftPlan.ReturnToBaseline = returnToBaseline;
            return Success(null);
        }

        public ExperimentBatchPanelActionResult SetPairedSeedMode(bool enabled)
        {
            ExperimentBatchPanelActionResult editable = RequireEditable();
            if (editable != null)
            {
                return editable;
            }

            draftPlan.PairedSeedMode = enabled;
            return Success(enabled
                ? "Paired seed mode enabled. Every scenario shares the repetition seed list."
                : "Paired seed mode disabled.");
        }

        public ExperimentBatchPanelActionResult SetStopBatchOnRunFailure(bool enabled)
        {
            ExperimentBatchPanelActionResult editable = RequireEditable();
            if (editable != null)
            {
                return editable;
            }

            draftPlan.StopBatchOnRunFailure = enabled;
            return Success(enabled
                ? "Invalid runs will be archived and the batch will stop at a freshly loaded baseline."
                : "Invalid runs will be archived and excluded; execution will continue from a fresh baseline.");
        }

        public ExperimentBatchPanelActionResult AddCurrentSettingsAsScenario()
        {
            ExperimentBatchPanelActionResult editable = RequireEditable();
            if (editable != null)
            {
                return editable;
            }

            ExperimentScenarioSnapshot settings;
            string error;
            if (!TryCaptureCurrentScenario(out settings, out error))
            {
                return Failure(error);
            }

            int number = draftPlan.Scenarios.Count + 1;
            draftPlan.Scenarios.Add(new ExperimentScenario
            {
                ScenarioId = Guid.NewGuid().ToString("N"),
                Name = "Scenario " + number.ToString(CultureInfo.CurrentCulture),
                Settings = settings,
            });
            return Success("Scenario added from the normalized current settings.");
        }

        private ExperimentScenarioSnapshot GetPresetBaseline(int baselineIndex)
        {
            if (baselineIndex >= 0 && baselineIndex < draftPlan.Scenarios.Count) return draftPlan.Scenarios[baselineIndex].Settings.Clone();
            if (!TryCaptureCurrentScenario(out ExperimentScenarioSnapshot settings, out string error)) throw new InvalidOperationException(error);
            return settings;
        }

        public ExperimentPreset PreviewPreset(int presetIndex, int baselineIndex) => ExperimentPresetCatalog.Describe(presetIndex, GetPresetBaseline(baselineIndex));

        public ExperimentBatchPanelActionResult SetInterventionSchedule(int scenarioIndex, string json)
        {
            var failure = RequireScenarioForEdit(scenarioIndex, out ExperimentScenario scenario);
            if (failure != null) return failure;
            try
            {
                var schedule = string.IsNullOrEmpty(json) ? null : new ExperimentJsonSerializer().Deserialize<ExperimentInterventionSchedule>(json);
                schedule?.Validate(scenario.Settings);
                scenario.InterventionSchedule = schedule;
                scenario.CustomizedAfterPreset = !string.IsNullOrEmpty(scenario.PresetId);
                return Success("Intervention schedule saved.");
            }
            catch (Exception ex) { return Failure(ex.Message); }
        }

        public ExperimentBatchPanelActionResult SetCalibrationTargets(int scenarioIndex, string json)
        {
            var result = RequireScenarioForEdit(scenarioIndex, out ExperimentScenario scenario);
            if (result != null) return result;
            try
            {
                if (string.IsNullOrEmpty(json)) { scenario.CalibrationTargets = null; return Success("Calibration targets cleared."); }
                var targets = new ExperimentJsonSerializer().Deserialize<CalibrationTargetSet>(json);
                targets.Validate();
                if (string.IsNullOrEmpty(targets.Source) || string.IsNullOrEmpty(targets.Name)) return Failure("Identify the external target set and its source.");
                scenario.CalibrationTargets = targets;
                return Success("External calibration targets attached; this is not external model validation.");
            }
            catch (Exception ex) { return Failure("Invalid target set: " + ex.Message); }
        }

        public ExperimentBatchPanelActionResult AddSensitivityScenarios(int scenarioIndex, string propertyName, double lower, double upper)
        {
            var result = RequireScenarioForEdit(scenarioIndex, out ExperimentScenario scenario);
            if (result != null) return result;
            if (!draftPlan.PairedSeedMode) return Failure("Enable paired seeds before adding an OAT design; existing seed choices were preserved.");
            try
            {
                var variations = ExperimentSensitivity.Create(scenario, propertyName, lower, upper);
                draftPlan.Scenarios.AddRange(variations);
                return Success("Lower, baseline and upper sensitivity scenarios added with paired repetitions.");
            }
            catch (Exception ex) { return Failure(ex.Message); }
        }

        public ExperimentBatchPanelActionResult AddPresetScenario(int presetIndex, int baselineIndex)
        {
            var editable = RequireEditable();
            if (editable != null) return editable;
            var scenario = ExperimentPresetCatalog.Create(presetIndex, GetPresetBaseline(baselineIndex));
            draftPlan.Scenarios.Add(scenario);
            return Success("Editable preset scenario added. Paired seeds match random experiment conditions across scenarios.");
        }

        public ExperimentBatchPanelActionResult EditScenarioParameter(int scenarioIndex, string propertyName, string value)
        {
            var result = RequireScenarioForEdit(scenarioIndex, out ExperimentScenario scenario);
            if (result != null) return result;
            bool scheduledPhase = propertyName.StartsWith("Scheduled.", StringComparison.Ordinal);
            if (scheduledPhase) propertyName = propertyName.Substring("Scheduled.".Length);
            if (scheduledPhase && (scenario.InterventionSchedule == null || (!ExperimentPresetCatalog.IsInterventionProperty(propertyName) && propertyName != "MaskPopulationSplit")))
                return Failure("The scheduled phase only supports intervention parameters.");
            var property = typeof(ExperimentScenarioSnapshot).GetProperty(propertyName);
            bool maskSplit = propertyName == "MaskPopulationSplit";
            if (!maskSplit && (property == null || !property.CanWrite || propertyName == "SchemaVersion")) return Failure("Unknown editable scenario parameter.");
            try
            {
                object converted = maskSplit ? null : property.PropertyType.IsEnum ? Enum.Parse(property.PropertyType, value, true)
                    : Convert.ChangeType(value, property.PropertyType, CultureInfo.InvariantCulture);
                var edited = (scheduledPhase ? scenario.InterventionSchedule.After : scenario.Settings).Clone();
                if (maskSplit)
                {
                    string[] split = value.Split(',');
                    if (split.Length != 3) return Failure("Supply Ignore,Other,Own percentages.");
                    edited.RatioIgnoreMasks = int.Parse(split[0], CultureInfo.InvariantCulture);
                    edited.RatioOtherProtectionMask = int.Parse(split[1], CultureInfo.InvariantCulture);
                    edited.RatioOwnProtectionMask = int.Parse(split[2], CultureInfo.InvariantCulture);
                }
                else property.SetValue(edited, converted, null);
                var validation = ExperimentPlanValidator.ValidateSettings(edited);
                if (!validation.IsValid) return Failure(string.Join("; ", validation.Errors.ToArray()));
                if (scheduledPhase)
                {
                    scenario.InterventionSchedule.After = edited;
                    scenario.CustomizedAfterPreset = !string.IsNullOrEmpty(scenario.PresetId);
                    return Success("Scheduled intervention parameter updated.");
                }
                scenario.Settings = edited;
                if (scenario.InterventionSchedule != null)
                {
                    scenario.InterventionSchedule.Before = edited.Clone();
                    // Biological/input edits apply to both phases; intervention edits apply to the selected initial phase.
                    if (!maskSplit && !ExperimentPresetCatalog.IsInterventionProperty(propertyName))
                        for (int phase = 1; phase <= scenario.InterventionSchedule.PhaseCount; phase++) property.SetValue(scenario.InterventionSchedule.SettingsAfter(phase), converted, null);
                }
                scenario.CustomizedAfterPreset = !string.IsNullOrEmpty(scenario.PresetId);
                return Success("Scenario parameter updated. Global settings are unchanged.");
            }
            catch (Exception ex) { return Failure("Invalid parameter value: " + ex.Message); }
        }

        public ExperimentBatchPanelActionResult DuplicateScenario(int scenarioIndex)
        {
            ExperimentScenario scenario;
            ExperimentBatchPanelActionResult result = RequireScenarioForEdit(scenarioIndex, out scenario);
            if (result != null)
            {
                return result;
            }

            ExperimentScenario copy = Clone(scenario);
            copy.ScenarioId = Guid.NewGuid().ToString("N");
            copy.Name = scenario.Name + " copy";
            draftPlan.Scenarios.Insert(scenarioIndex + 1, copy);
            return Success("Scenario duplicated.");
        }

        public ExperimentBatchPanelActionResult RemoveScenario(int scenarioIndex)
        {
            ExperimentScenario ignored;
            ExperimentBatchPanelActionResult result = RequireScenarioForEdit(scenarioIndex, out ignored);
            if (result != null)
            {
                return result;
            }

            draftPlan.Scenarios.RemoveAt(scenarioIndex);
            return Success("Scenario removed.");
        }

        public ExperimentBatchPanelActionResult MoveScenario(int scenarioIndex, int newIndex)
        {
            ExperimentScenario scenario;
            ExperimentBatchPanelActionResult result = RequireScenarioForEdit(scenarioIndex, out scenario);
            if (result != null)
            {
                return result;
            }

            if (newIndex < 0 || newIndex >= draftPlan.Scenarios.Count)
            {
                return Failure("The requested scenario position is outside the plan.");
            }

            draftPlan.Scenarios.RemoveAt(scenarioIndex);
            draftPlan.Scenarios.Insert(newIndex, scenario);
            return Success("Scenario order updated.");
        }

        public ExperimentBatchPanelActionResult UpdateScenarioFromCurrentSettings(int scenarioIndex)
        {
            ExperimentScenario scenario;
            ExperimentBatchPanelActionResult result = RequireScenarioForEdit(scenarioIndex, out scenario);
            if (result != null)
            {
                return result;
            }

            ExperimentScenarioSnapshot settings;
            string error;
            if (!TryCaptureCurrentScenario(out settings, out error))
            {
                return Failure(error);
            }

            scenario.Settings = settings;
            scenario.CustomizedAfterPreset = !string.IsNullOrEmpty(scenario.PresetId);
            if (scenario.InterventionSchedule != null) scenario.InterventionSchedule.Before = settings.Clone();
            return Success("Scenario settings replaced with the normalized current values.");
        }

        public ExperimentBatchPanelActionResult UpdateScenario(
            int scenarioIndex,
            string name,
            int runCount,
            double durationDays,
            ExperimentEndMode endMode,
            ExperimentSeedStrategy seedStrategy,
            int firstSeed)
        {
            ExperimentScenario scenario;
            ExperimentBatchPanelActionResult result = RequireScenarioForEdit(scenarioIndex, out scenario);
            if (result != null)
            {
                return result;
            }

            string cleanName = (name ?? string.Empty).Trim();
            if (cleanName.Length == 0 || runCount <= 0 || firstSeed < 0
                || durationDays <= 0d || durationDays > 3650d
                || double.IsNaN(durationDays) || double.IsInfinity(durationDays))
            {
                return Failure("Scenario name, repetitions, duration (0 < days <= 3650), and seed are invalid.");
            }

            if (!draftPlan.PairedSeedMode
                && seedStrategy == ExperimentSeedStrategy.Sequential
                && (long)firstSeed + runCount - 1L > int.MaxValue)
            {
                return Failure("The sequential seed range exceeds Int32.MaxValue.");
            }

            scenario.Name = cleanName;
            scenario.RunCount = runCount;
            scenario.DurationDays = durationDays;
            scenario.EndMode = endMode;
            scenario.SeedStrategy = seedStrategy;
            scenario.FirstSeed = firstSeed;
            return Success("Scenario run policy updated.");
        }

        public ExperimentBatchPanelPreflight BuildPreflight()
        {
            var result = new ExperimentBatchPanelPreflight { Warning = UnsavedWarning };
            if (planFrozen)
            {
                result.Error = "The confirmed batch plan is immutable.";
                return result;
            }

            PrepareDraftMetadata();
            ExperimentValidationResult validation = validator.Validate(draftPlan);
            if (!validation.IsValid)
            {
                result.Error = string.Join(Environment.NewLine, validation.Errors.ToArray());
                return result;
            }

            ExperimentOutputPreflightResult output = outputRootResolver.Preflight(draftPlan.OutputLocation);
            if (!output.Success || !PathsEqual(output.ResolvedPath, draftPlan.OutputRoot))
            {
                result.Error = output.Success
                    ? "The selected output root no longer matches the plan. Select it again."
                    : "The selected output root is not writable: " + output.Error;
                return result;
            }

            BaselineSaveResolution baseline = saveCatalog.Resolve(draftPlan.Baseline);
            if (!baseline.Success)
            {
                result.Error = baseline.Error;
                return result;
            }

            string resolvedBatchDirectory;
            try
            {
                var pathResolver = new ExperimentPathResolver(draftPlan.OutputRoot);
                resolvedBatchDirectory = pathResolver.ResolveBatchDirectory(draftPlan);
                for (int scenarioIndex = 0; scenarioIndex < draftPlan.Scenarios.Count; ++scenarioIndex)
                {
                    ExperimentScenario scenario = draftPlan.Scenarios[scenarioIndex];
                    var run = new ExperimentRunDescriptor
                    {
                        Scenario = scenario,
                        ScenarioIndex = scenarioIndex,
                        RunIndex = scenario.RunCount - 1,
                    };
                    pathResolver.ResolveRunDirectory(draftPlan, run);
                    pathResolver.ResolveInProgressRunDirectory(draftPlan, run, Guid.Empty.ToString("N"));
                }
            }
            catch (Exception exception)
            {
                result.Error = "The selected output root cannot represent all unique run paths safely: " + exception.Message;
                return result;
            }

            result.IsValid = true;
            int totalRuns = sequencer.CountTotalRuns(draftPlan);
            result.Summary = string.Format(
                CultureInfo.CurrentCulture,
                "{0} scenario(s), {1} sequential run(s), baseline '{2}', output folder '{3}'.",
                draftPlan.Scenarios.Count,
                totalRuns,
                draftPlan.Baseline.CityName,
                resolvedBatchDirectory);
            int estimatedPopulation = levelCore?.PandemicManager?.GetPopulationForStorageEstimate() ?? 0;
            var storageWarnings = new System.Text.StringBuilder();
            storageWarnings.Append("\nStorage estimate uses the currently loaded city's ").Append(estimatedPopulation)
                .Append(" observed citizens as a proxy; the selected baseline may differ. Household/outdoor density and occupancy turnover can exceed the estimate.\n");
            double estimatedHighGB = 0;
            bool fullRaw = false;
            foreach (var scenario in draftPlan.Scenarios)
            {
                var estimate = ContactStorageEstimate.Calculate(estimatedPopulation, scenario.DurationDays, scenario.RunCount, scenario.Settings);
                estimatedHighGB += estimate.HighGB;
                storageWarnings.Append(scenario.Name).Append(": ").Append(estimate.Describe()).Append('\n');
                fullRaw |= scenario.Settings.ScientificContactExportMode == Config.ScientificContactExportMode.FullRaw;
            }
            if (fullRaw) storageWarnings.AppendLine(ContactStorageEstimate.FullRawWarning);
            long? availableBytes = ContactStorageEstimate.AvailableBytes(draftPlan.OutputRoot);
            if (availableBytes.HasValue)
            {
                storageWarnings.AppendFormat(CultureInfo.InvariantCulture, "Available disk space: {0:0.##} GB.\n", availableBytes.Value / 1e9);
                if (estimatedHighGB > availableBytes.Value / 1e9)
                    storageWarnings.AppendLine("Projected contact output may exceed available space. Choose a smaller export mode, more storage, or an explicit export limit.");
            }
            else
            {
                storageWarnings.AppendLine("Available disk space: unknown (the runtime or filesystem does not support this query). Check free space before starting; export limits and write-error checks remain active.");
            }
            result.Warning += storageWarnings.ToString();
            return result;
        }

        public ExperimentBatchPanelActionResult StartBatch()
        {
            ExperimentBatchPanelPreflight preflight = BuildPreflight();
            if (!preflight.IsValid)
            {
                return Failure(preflight.Error);
            }

            if (levelCore == null || !Singleton<SimulationManager>.exists)
            {
                return Failure("A fully loaded game level is required before starting a batch.");
            }

            try
            {
                activePlan = Clone(draftPlan);
                activePlan.BatchId = Guid.NewGuid().ToString("N");
                activePlan.CreatedUtc = UtcNow();
                activePlan.OriginalConfiguration = RealTimeConfigSnapshot.Capture(levelCore.Configuration);
                activePlan.OriginalInitialLockdownEnabled = QuarantineManager.Instance.InLockDown;
                activePlan.OriginalSimulationSpeed = SimulationManager.instance.SelectedSimulationSpeed;
                activePlan.OriginalSimulationPaused = SimulationManager.instance.SimulationPaused;
                planFrozen = true;

                var resolver = new ExperimentPathResolver(activePlan.OutputRoot);
                batchDirectory = resolver.ResolveBatchDirectory(activePlan);
                Directory.CreateDirectory(batchDirectory);

                state = sequencer.CreateInitialState(activePlan, sessionNonce, UtcNow());
                state.OutputRoot = activePlan.OutputRoot;
                persistence.SavePlan(batchDirectory, activePlan);
                persistence.SaveState(batchDirectory, state);
                locator.SetActive(activePlan.BatchId, batchDirectory, UtcNow());

                AcquireRuntimeOwnership();
                Transition(ExperimentBatchExecutionState.Validating);
                Transition(ExperimentBatchExecutionState.AwaitingConfirmation);
                Transition(ExperimentBatchExecutionState.PreparingRun);
                PrepareCurrentRunAndReload();
                return Success("Batch started. Loading the exact baseline for Run 1.");
            }
            catch (Exception exception)
            {
                Fail(ExperimentBatchExecutionState.Failed, "BatchStartFailed", "The batch could not be started.", exception, true);
                if (controlLease == null)
                {
                    try
                    {
                        locator.Clear(UtcNow());
                    }
                    catch (Exception)
                    {
                    }

                    RestoreStartingSpeedAndPause();
                    RestoreOriginalConfiguration(levelCore);
                    activePlan = null;
                    state = null;
                    batchDirectory = null;
                    planFrozen = false;
                }

                return Failure(state?.Error?.Message ?? exception.Message);
            }
        }

        public ExperimentBatchPanelActionResult PauseBatch()
        {
            if (state?.State != ExperimentBatchExecutionState.Running)
            {
                return Failure("The batch is not currently running.");
            }

            SetSimulationPaused(true);
            Transition(ExperimentBatchExecutionState.Paused);
            return Success("Batch paused. No simulation progress will be made.");
        }

        public ExperimentBatchPanelActionResult ResumeBatch()
        {
            if (state?.State == ExperimentBatchExecutionState.Interrupted)
            {
                return ResumeInterruptedBatch();
            }

            if (state?.State != ExperimentBatchExecutionState.Paused)
            {
                return Failure("The batch is not paused.");
            }

            ApplyExecutionSpeed();
            SetSimulationPaused(false);
            Transition(ExperimentBatchExecutionState.Running);
            return Success("Batch resumed.");
        }

        public ExperimentBatchPanelActionResult AbortBatch()
        {
            return AbortBatchCore(true);
        }

        public ExperimentBatchPanelActionResult AbortBatchInPlace()
        {
            return AbortBatchCore(false);
        }

        private ExperimentBatchPanelActionResult AbortBatchCore(bool returnToBaseline)
        {
            if (state == null || !planFrozen || IsFinishedState(state.State))
            {
                return Failure("There is no active batch to abort.");
            }

            try
            {
                AcquireRuntimeOwnership();
                aborting = true;
                SetState(ExperimentBatchExecutionState.Aborting);
                SetSimulationPaused(true);
                levelCore?.PandemicManager?.AbortBatchRun();
                RestoreOriginalConfiguration(levelCore);

                if (returnToBaseline && levelCore != null)
                {
                    returningToBaseline = true;
                    SetState(ExperimentBatchExecutionState.ReturningToBaseline);
                    RequestBaselineReload();
                    return Success("Batch aborted; returning to the exact baseline.");
                }

                FinishTermination(ExperimentBatchExecutionState.Aborted);
                return Success("Batch aborted in place. Committed runs were preserved.");
            }
            catch (Exception exception)
            {
                Fail(ExperimentBatchExecutionState.ReturnFailed, "AbortFailed", "The batch could not complete its abort sequence.", exception, true);
                return Failure(state.Error.Message);
            }
        }

        public ExperimentBatchPanelActionResult RetryBaselineLoad()
        {
            if (state == null
                || (state.State != ExperimentBatchExecutionState.LoadFailed
                    && state.State != ExperimentBatchExecutionState.ReturnFailed))
            {
                return Failure("There is no failed baseline load to retry.");
            }

            ClearError();
            if (returningToBaseline || state.ScenarioIndex >= activePlan.Scenarios.Count)
            {
                returningToBaseline = true;
                SetState(ExperimentBatchExecutionState.ReturningToBaseline);
                RequestBaselineReload();
            }
            else
            {
                SetState(ExperimentBatchExecutionState.PreparingRun);
                PrepareCurrentRunAndReload();
            }

            return Success("Retrying from the exact baseline.");
        }

        public ExperimentBatchPanelActionResult RetryPreparation()
        {
            if (state == null
                || (state.State != ExperimentBatchExecutionState.ScenarioApplyFailed
                    && state.State != ExperimentBatchExecutionState.RunFailed))
            {
                return Failure("There is no failed scenario preparation to retry.");
            }

            levelCore?.PandemicManager?.AbortBatchRun();
            state.HaltAfterInvalidRunBaselineReload = false;
            ClearError();
            SetState(ExperimentBatchExecutionState.PreparingRun);
            PrepareCurrentRunAndReload();
            return Success("Retrying scenario preparation from the exact baseline.");
        }

        public ExperimentBatchPanelActionResult RetryExport()
        {
            if (state?.State != ExperimentBatchExecutionState.ExportFailed || frozenExportRequest == null)
            {
                return Failure("The frozen export payload is unavailable. After an application restart, resume reruns the incomplete run.");
            }

            ClearError();
            string finalDirectory = new ExperimentPathResolver(activePlan.OutputRoot).ResolveRunDirectory(activePlan, currentRun);
            if (Directory.Exists(finalDirectory))
            {
                ExperimentRunCommitResult recovery = runCommitService.Recover(
                    batchDirectory,
                    finalDirectory,
                    finalDirectory,
                    currentRun.RunId);
                if (!recovery.Success)
                {
                    Fail(ExperimentBatchExecutionState.ExportFailed, "CommitRecoveryFailed", recovery.Error, null, true);
                }
                else
                {
                    CompleteCommittedRun(finalDirectory);
                }
            }
            else
            {
                SetState(ExperimentBatchExecutionState.FinalizingRun);
                ExportFrozenRun();
            }

            return state.State == ExperimentBatchExecutionState.ExportFailed
                ? Failure(state.Error.Message)
                : Success("The frozen run export was retried.");
        }

        private void TickLevelReadiness()
        {
            if (levelCore != null && levelCore.IsExperimentReady && Singleton<SimulationManager>.exists)
            {
                if (state.HaltAfterInvalidRunBaselineReload)
                {
                    SetSimulationPaused(true);
                    RestoreOriginalConfiguration(levelCore);
                    state.HaltAfterInvalidRunBaselineReload = false;
                    SetState(ExperimentBatchExecutionState.RunFailed);
                    return;
                }

                if (returningToBaseline)
                {
                    RestoreOriginalConfiguration(levelCore);
                    FinishTermination(aborting
                        ? ExperimentBatchExecutionState.Aborted
                        : ExperimentBatchExecutionState.Completed);
                    return;
                }

                ApplyCurrentScenarioAndStart();
                return;
            }

            if (Time.realtimeSinceStartup >= levelReadyDeadline)
            {
                Fail(
                    ExperimentBatchExecutionState.LoadFailed,
                    "LevelReadyTimeout",
                    "The baseline loaded, but TENUS and the required game services did not become ready within 60 real seconds.",
                    null,
                    true);
            }
        }

        private void TickRunning()
        {
            PandemicManager manager = levelCore?.PandemicManager;
            if (manager == null || !manager.IsBatchRunOwned)
            {
                Fail(ExperimentBatchExecutionState.RunFailed, "RunOwnershipLost", "The controller-owned pandemic run is no longer available.", null, true);
                return;
            }

            if (manager.TryGetRunIntegrityFailure(out ExperimentRunIntegrityFailure integrityFailure))
            {
                HandleInvalidRun(manager, integrityFailure);
                return;
            }

            if (manager.IsAwaitingBatchFinalization)
            {
                BeginFinalization(manager);
                return;
            }

            if (Singleton<SimulationManager>.exists && SimulationManager.instance.SimulationPaused)
            {
                Transition(ExperimentBatchExecutionState.Paused);
                return;
            }

            DateTime simulationTime = Singleton<SimulationManager>.exists
                ? SimulationManager.instance.m_currentGameTime
                : default(DateTime);
            if (lastConfigurationCheckSimulationTime == default(DateTime)
                || simulationTime - lastConfigurationCheckSimulationTime >= TimeSpan.FromMinutes(5d))
            {
                lastConfigurationCheckSimulationTime = simulationTime;
                ExperimentScenarioSnapshot expected = manager.ScheduledInterventionApplied
                    ? currentRun.Scenario.InterventionSchedule.SettingsAfter(manager.AppliedInterventionCount) : currentRun.Scenario.Settings;
                List<ExperimentSettingDifference> differences = expected.Diff(
                    ExperimentScenarioSnapshot.Capture(levelCore.Configuration, QuarantineManager.Instance.InLockDown));
                if (differences.Count > 0)
                {
                    SetSimulationPaused(true);
                    manager.AbortBatchRun();
                    Fail(
                        ExperimentBatchExecutionState.ScenarioApplyFailed,
                        "ConfigurationDrift",
                        "Simulation settings changed during the run: " + FormatDifferences(differences),
                        null,
                        true);
                }
            }
        }

        private void HandleInvalidRun(PandemicManager manager, ExperimentRunIntegrityFailure failure)
        {
            SetSimulationPaused(true);
            // Persist the original cause before freezing: a secondary archive failure
            // must never replace the only evidence of why the simulation stopped.
            Log.Error("[TENUS Batch] Invalid run: " + failure.Code + " at "
                + failure.SimulationTime.ToString("o") + ": " + failure.Message + " " + failure.Detail);
            try
            {
                jsonStore.Save(Path.Combine(currentAttemptDirectory, "integrity_failure.json"),
                    failure);
            }
            catch (Exception diagnosticException)
            {
                Log.Warning("[TENUS Batch] Could not persist the original integrity failure: " + diagnosticException);
            }
            try
            {
                if (!manager.FreezeInvalidBatchRun())
                {
                    throw new InvalidOperationException("The failed pandemic run could not be frozen for diagnostics.");
                }

                DateTime endUtc = DateTime.UtcNow;
                state.RunSimulationEndedUtc = SimulationIso(manager.GetScientificRunSnapshot().RunEndTime);
                frozenExportRequest = new PandemicRunExportRequest(
                    manager,
                    manager.GetLiveSnapshot(),
                    manager.GetScientificRunSnapshot(),
                    currentRunContext.Output,
                    runWallClockStartUtc,
                    endUtc);
                new ScientificRunExportService().ExportInvalid(frozenExportRequest, failure);

                string normalFinalDirectory = new ExperimentPathResolver(activePlan.OutputRoot)
                    .ResolveRunDirectory(activePlan, currentRun);
                string invalidDirectory = ExperimentInvalidRunService.ResolveInvalidDirectory(
                    normalFinalDirectory,
                    state.CurrentAttemptId);
                string invalidatedUtc = UtcNow();
                ExperimentErrorInfo error = CreateIntegrityError(failure, invalidatedUtc);
                var manifest = new ExperimentInvalidRunManifest
                {
                    BatchId = activePlan.BatchId,
                    BatchName = activePlan.BatchName,
                    RunId = currentRun.RunId,
                    AttemptId = state.CurrentAttemptId,
                    ScenarioIndex = currentRun.ScenarioIndex,
                    ScenarioNumber = currentRun.ScenarioIndex + 1,
                    ScenarioId = currentRun.Scenario.ScenarioId,
                    ScenarioName = currentRun.Scenario.Name,
                    RunIndex = currentRun.RunIndex,
                    RunNumber = currentRun.RunIndex + 1,
                    PairId = currentRun.PairId,
                    MasterSeed = currentRun.MasterSeed,
                    DerivedSeeds = Clone(currentRun.Seeds),
                    Scenario = Clone(currentRun.Scenario),
                    Baseline = Clone(activePlan.Baseline),
                    ConfigurationHashAlgorithm = ExperimentConfigurationHasher.AlgorithmName,
                    ConfigurationHash = ExperimentConfigurationHasher.Compute(currentRun.Scenario),
                    GitCommitSha = activePlan.GitCommitSha,
                    GitBranchOrTag = activePlan.GitBranchOrTag,
                    ModVersion = activePlan.ModVersion,
                    GameVersion = activePlan.GameVersion,
                    RunSimulationStartedUtc = state.RunSimulationStartedUtc,
                    RunSimulationEndedUtc = state.RunSimulationEndedUtc,
                    InvalidatedUtc = invalidatedUtc,
                    FailureSimulationTimeUtc = SimulationIso(failure.SimulationTime),
                    Error = error,
                };
                ExperimentInvalidRunArchiveResult archived = invalidRunService.Archive(
                    new ExperimentInvalidRunArchiveRequest
                    {
                        AttemptDirectory = currentAttemptDirectory,
                        InvalidDirectory = invalidDirectory,
                        Manifest = manifest,
                    });
                if (!archived.Success)
                {
                    throw new IOException(archived.Error);
                }

                var receipt = new ExperimentInvalidRun
                {
                    RunId = currentRun.RunId,
                    AttemptId = state.CurrentAttemptId,
                    ScenarioId = currentRun.Scenario.ScenarioId,
                    ScenarioIndex = currentRun.ScenarioIndex,
                    RunIndex = currentRun.RunIndex,
                    MasterSeed = currentRun.MasterSeed,
                    PairId = currentRun.PairId,
                    InvalidatedUtc = invalidatedUtc,
                    OutputDirectory = archived.PublishedDirectory,
                    Error = error,
                };

                manager.CompleteBatchFinalization();
                SetError(failure.Code, failure.Message, DetailException(failure.Detail), true);
                if (activePlan.StopBatchOnRunFailure)
                {
                    AddInvalidRunReceipt(receipt);
                    state.HaltAfterInvalidRunBaselineReload = true;
                    PersistState();
                    ClearFrozenRunReferences();
                    RequestBaselineReload();
                    return;
                }

                bool hasMore = sequencer.RecordInvalidRun(activePlan, state, currentRun, receipt);
                PersistState();
                ClearFrozenRunReferences();
                ClearError();
                if (hasMore)
                {
                    SetState(ExperimentBatchExecutionState.PreparingNextRun);
                    SetState(ExperimentBatchExecutionState.PreparingRun);
                    PrepareCurrentRunAndReload();
                }
                else
                {
                    CompleteBatchRuns();
                }
            }
            catch (Exception exception)
            {
                manager.AbortBatchRun();
                Fail(
                    ExperimentBatchExecutionState.RunFailed,
                    "InvalidRunArchiveFailed",
                    "The invalid run could not be frozen and archived safely; it was not counted as completed.",
                    exception,
                    true);
            }
        }

        private void AddInvalidRunReceipt(ExperimentInvalidRun receipt)
        {
            if (state.InvalidRuns == null)
            {
                state.InvalidRuns = new List<ExperimentInvalidRun>();
            }

            if (!state.InvalidRuns.Any(item => item != null
                && string.Equals(item.AttemptId, receipt.AttemptId, StringComparison.Ordinal)))
            {
                state.InvalidRuns.Add(receipt);
            }
        }

        private void ClearFrozenRunReferences()
        {
            currentRun = null;
            currentRunContext = null;
            frozenExportRequest = null;
            currentAttemptDirectory = null;
        }

        private static ExperimentErrorInfo CreateIntegrityError(ExperimentRunIntegrityFailure failure, string occurredUtc)
        {
            return new ExperimentErrorInfo
            {
                Code = failure.Code,
                Message = failure.Message,
                Detail = failure.Detail,
                OccurredUtc = occurredUtc,
                Retryable = true,
            };
        }

        private static Exception DetailException(string detail)
        {
            return string.IsNullOrEmpty(detail) ? null : new InvalidOperationException(detail);
        }

        private void PrepareCurrentRunAndReload()
        {
            if (activePlan == null || state == null)
            {
                throw new InvalidOperationException("The active batch plan or state is missing.");
            }

            if (state.ScenarioIndex >= activePlan.Scenarios.Count)
            {
                CompleteBatchRuns();
                return;
            }

            currentRun = sequencer.SelectCurrentRun(activePlan, state);
            state.CurrentAttemptId = Guid.NewGuid().ToString("N");
            state.CommitId = null;
            state.Error = null;
            state.LastError = null;
            persistence.SaveState(batchDirectory, state);
            RequestBaselineReload();
        }

        private void RequestBaselineReload()
        {
            SetSimulationPaused(true);
            BaselineSaveResolution resolution = saveCatalog.Resolve(activePlan.Baseline);
            if (!resolution.Success)
            {
                Fail(
                    returningToBaseline ? ExperimentBatchExecutionState.ReturnFailed : ExperimentBatchExecutionState.LoadFailed,
                    "BaselineChanged",
                    resolution.Error,
                    null,
                    true);
                return;
            }

            state.ReloadGeneration = checked(state.ReloadGeneration + 1);
            state.ReloadToken = Guid.NewGuid().ToString("N");
            plannedReloadToken = state.ReloadToken;
            SetState(ExperimentBatchExecutionState.RequestingBaselineLoad);

            string error;
            if (!reloadService.TryRequestReload(activePlan.Baseline, out error))
            {
                plannedReloadToken = null;
                Fail(
                    returningToBaseline ? ExperimentBatchExecutionState.ReturnFailed : ExperimentBatchExecutionState.LoadFailed,
                    "LoadRequestRejected",
                    error,
                    null,
                    true);
            }
        }

        private void ApplyCurrentScenarioAndStart()
        {
            SetState(ExperimentBatchExecutionState.ApplyingScenario);
            SetSimulationPaused(true);

            try
            {
                if (currentRun == null)
                {
                    currentRun = sequencer.SelectCurrentRun(activePlan, state);
                }

                RealTimeConfig configuration = levelCore.Configuration;
                currentRun.Scenario.Settings.ApplyTo(configuration);
                configuration.Validate();
                PandemicManager.NormalizeRuntimeConfiguration(configuration);
                QuarantineManager.Instance.InLockDown = currentRun.Scenario.Settings.InitialLockdownEnabled;
                levelCore.RefreshExperimentConfiguration();

                ExperimentScenarioSnapshot actual = ExperimentScenarioSnapshot.Capture(
                    configuration,
                    QuarantineManager.Instance.InLockDown);
                List<ExperimentSettingDifference> differences = currentRun.Scenario.Settings.Diff(actual);
                if (differences.Count > 0)
                {
                    throw new InvalidOperationException("Scenario verification failed: " + FormatDifferences(differences));
                }

                ExperimentPathResolver pathResolver = new ExperimentPathResolver(activePlan.OutputRoot);
                currentAttemptDirectory = pathResolver.ResolveInProgressRunDirectory(
                    activePlan,
                    currentRun,
                    state.CurrentAttemptId);
                if (Directory.Exists(currentAttemptDirectory) || File.Exists(currentAttemptDirectory))
                {
                    throw new IOException("The unique attempt directory already exists: " + currentAttemptDirectory);
                }

                Directory.CreateDirectory(currentAttemptDirectory);
                string scenarioSlug = ExperimentPathSanitizer.SanitizeSegment(currentRun.Scenario.Name, "scenario", 32);
                string richCsv = Path.Combine(
                    currentAttemptDirectory,
                    string.Format(CultureInfo.InvariantCulture, "pandemic_run_{0}_{1:D3}.csv", scenarioSlug, currentRun.RunIndex + 1));
                string dataCsv = Path.Combine(currentAttemptDirectory, "data.csv");
                string contactsCsv = Path.Combine(currentAttemptDirectory, "contacts.csv");
                var output = new PandemicOutputContext(
                    richCsv,
                    dataCsv,
                    contactsCsv,
                    true,
                    false);
                state.OutputContext = new ExperimentOutputContext
                {
                    RootKind = activePlan.OutputLocation.Kind,
                    OutputRoot = activePlan.OutputRoot,
                    BatchDirectory = batchDirectory,
                    ScenarioDirectory = pathResolver.ResolveScenarioDirectory(activePlan, currentRun.ScenarioIndex),
                    AttemptDirectory = currentAttemptDirectory,
                    FinalDirectory = pathResolver.ResolveRunDirectory(activePlan, currentRun),
                    RichCsvPath = richCsv,
                    DataCsvPath = dataCsv,
                    ContactsCsvPath = contactsCsv,
                };
                var metadata = new PandemicBatchExportMetadata
                {
                    PresetId = currentRun.Scenario.PresetId,
                    PresetVersion = currentRun.Scenario.PresetVersion,
                    CustomizedAfterPreset = currentRun.Scenario.CustomizedAfterPreset,
                    Sensitivity = currentRun.Scenario.Sensitivity,
                    BatchId = activePlan.BatchId,
                    BatchName = activePlan.BatchName,
                    ScenarioId = currentRun.Scenario.ScenarioId,
                    ScenarioName = currentRun.Scenario.Name,
                    ScenarioIndex = currentRun.ScenarioIndex + 1,
                    RunNumber = currentRun.RunIndex + 1,
                    OverallRunNumber = CalculateOverallRunNumber(currentRun.ScenarioIndex, currentRun.RunIndex),
                    PairId = currentRun.PairId,
                    ConfigurationHash = ExperimentConfigurationHasher.Compute(currentRun.Scenario),
                    GitCommitSha = activePlan.GitCommitSha,
                    GitBranchOrTag = activePlan.GitBranchOrTag,
                };
                currentRunContext = new PandemicRunContext(
                    PandemicRunMode.Batch,
                    PandemicRunPolicy.CreateBatch(
                        currentRun.Scenario.DurationDays,
                        currentRun.Scenario.EndMode == ExperimentEndMode.DurationOrExtinction),
                    output,
                    PandemicComponentSeeds.FromExperimentSeedSet(currentRun.Seeds),
                    DateTime.UtcNow,
                    metadata);
                currentRunContext.InterventionSchedule = currentRun.Scenario.InterventionSchedule;
                currentRunContext.CalibrationTargets = currentRun.Scenario.CalibrationTargets;

                SetState(ExperimentBatchExecutionState.StartingRun);
                runWallClockStartUtc = DateTime.UtcNow;
                PandemicManager manager = levelCore.PandemicManager;
                if (!manager.StartPandemic(currentRunContext) || !manager.IsBatchRunOwned)
                {
                    throw new InvalidOperationException("The pandemic manager rejected the controller-owned run.");
                }

                differences = currentRun.Scenario.Settings.Diff(ExperimentScenarioSnapshot.Capture(
                    configuration,
                    QuarantineManager.Instance.InLockDown));
                if (differences.Count > 0)
                {
                    manager.AbortBatchRun();
                    throw new InvalidOperationException("Scenario drifted during bootstrap: " + FormatDifferences(differences));
                }

                state.RunStartedUtc = runWallClockStartUtc.ToString("o", CultureInfo.InvariantCulture);
                state.RunSimulationStartedUtc = SimulationIso(manager.GetPandemicRunStartedAt());
                state.TargetSimulationTimeUtc = SimulationIso(manager.GetRunTargetSimulationTime());
                lastConfigurationCheckSimulationTime = default(DateTime);
                ApplyExecutionSpeed();
                SetSimulationPaused(false);
                Transition(ExperimentBatchExecutionState.Running);
            }
            catch (Exception exception)
            {
                levelCore?.PandemicManager?.AbortBatchRun();
                Fail(
                    ExperimentBatchExecutionState.ScenarioApplyFailed,
                    "ScenarioPreparationFailed",
                    "The scenario could not be applied and verified against the live configuration.",
                    exception,
                    true);
            }
        }

        private void BeginFinalization(PandemicManager manager)
        {
            SetSimulationPaused(true);
            SetState(ExperimentBatchExecutionState.FinalizingRun);
            DateTime endUtc = DateTime.UtcNow;
            // The UI may finalize on a later game frame. Export the frozen scientific
            // endpoint, not that later wall/game frame, to keep durations consistent.
            state.RunSimulationEndedUtc = SimulationIso(manager.GetScientificRunSnapshot().RunEndTime);
            frozenExportRequest = new PandemicRunExportRequest(
                manager,
                manager.GetLiveSnapshot(),
                manager.GetScientificRunSnapshot(),
                currentRunContext.Output,
                runWallClockStartUtc,
                endUtc);
        }

        private void ExportFrozenRun()
        {
            if (frozenExportRequest == null)
            {
                Fail(ExperimentBatchExecutionState.ExportFailed, "FrozenPayloadMissing", "The frozen export payload is unavailable; rerun this incomplete run from its baseline.", null, false);
                return;
            }

            try
            {
                SetState(ExperimentBatchExecutionState.ExportingRun);
                PandemicRunExportService.Instance.Export(frozenExportRequest);
                CommitExportedRun();
            }
            catch (Exception exception)
            {
                Fail(ExperimentBatchExecutionState.ExportFailed, "ExportFailed", "The frozen run could not be exported or committed.", exception, true);
            }
        }

        private void CommitExportedRun()
        {
            string finalDirectory = new ExperimentPathResolver(activePlan.OutputRoot)
                .ResolveAvailableFinalRunDirectory(activePlan, currentRun);
            ExperimentRunManifest manifest = runCommitService.CreateManifest(
                activePlan,
                currentRun,
                state.RunSimulationStartedUtc,
                state.TargetSimulationTimeUtc,
                state.RunSimulationEndedUtc,
                UtcNow(),
                levelCore.PandemicManager.CompletionReason.ToString(),
                finalDirectory);
            state.CommitId = Guid.NewGuid().ToString("N");
            manifest.ScientificExtensionsVersion = 1;
            manifest.OverallRunNumber = CalculateOverallRunNumber(currentRun.ScenarioIndex, currentRun.RunIndex);
            manifest.AttemptId = state.CurrentAttemptId;
            manifest.CommitId = state.CommitId;
            manifest.SessionNonce = state.SessionId;
            manifest.ReloadGeneration = state.ReloadGeneration;
            manifest.OutputRootKind = activePlan.OutputLocation.Kind.ToString();
            manifest.OutputRoot = activePlan.OutputRoot;
            ExperimentRecorderSnapshot recorderSnapshot = frozenExportRequest.ScientificSnapshot;
            ExperimentRunCommitService.PopulateContactMetadata(manifest, recorderSnapshot, currentRun.Scenario.Settings);
            ScientificRunSummary scientific = ScientificRunExportService.CalculateSummary(
                recorderSnapshot,
                frozenExportRequest.Manager.GetTestRecords(),
                frozenExportRequest.Manager.GetActualMaskUsagePercent());
            PandemicStateTimePoint finalState = recorderSnapshot.StateTimeSeries.Count > 0
                ? recorderSnapshot.StateTimeSeries[recorderSnapshot.StateTimeSeries.Count - 1]
                : new PandemicStateTimePoint();
            manifest.FinalTrackedPopulation = scientific.TrackedPopulation;
            manifest.FinalSusceptible = finalState.Susceptible;
            manifest.FinalExposed = finalState.Exposed;
            manifest.FinalInfectious = finalState.Infectious;
            manifest.FinalPostInfectiousIll = finalState.PostInfectiousIll;
            manifest.FinalSymptomatic = finalState.Symptomatic;
            manifest.FinalSick = finalState.Infectious + finalState.PostInfectiousIll;
            manifest.FinalRecovered = finalState.Recovered;
            manifest.FinalDead = finalState.Dead;
            manifest.FinalTransmissionsTotal = scientific.SecondaryTransmissionsTotal;
            manifest.InitialSeedCount = scientific.InitialSeedCount;
            manifest.SecondaryTransmissionsTotal = scientific.SecondaryTransmissionsTotal;
            manifest.CumulativeInfections = scientific.CumulativeInfections;
            manifest.HospitalizationsTotal = scientific.HospitalizationsTotal;
            manifest.FinalAttackRatePercent = scientific.AttackRatePercent;
            manifest.FinalFatalityRatePercent = scientific.ResolvedCaseFatalityRatioPercent ?? 0d;
            manifest.FinalPrevalencePercent = scientific.FinalPrevalencePercent;
            manifest.ResolvedCaseFatalityRatioPercent = scientific.ResolvedCaseFatalityRatioPercent;
            manifest.EmpiricalSecondaryInfectionsPerInfector = scientific.EmpiricalSecondaryInfectionsPerInfector;
            manifest.ActualMaskUsagePercent = scientific.ActualMaskUsagePercent;
            manifest.TotalIsolationPersonDays = scientific.TotalIsolationPersonDays;
            manifest.TotalQuarantinePersonDays = scientific.TotalQuarantinePersonDays;
            manifest.PhysicalContactsTotal = scientific.PhysicalContactsTotal;
            manifest.TraceableContactsTotal = scientific.TraceableContactsTotal;
            SetState(ExperimentBatchExecutionState.CommittingRun);
            ExperimentRunCommitResult commit = runCommitService.Commit(new ExperimentRunCommitRequest
            {
                BatchDirectory = batchDirectory,
                AttemptDirectory = currentAttemptDirectory,
                FinalDirectory = finalDirectory,
                Manifest = manifest,
            });
            if (!commit.Success && commit.Published)
            {
                commit = runCommitService.Recover(
                    batchDirectory,
                    finalDirectory,
                    finalDirectory,
                    currentRun.RunId);
            }

            if (!commit.Success)
            {
                throw new IOException(commit.Error);
            }

            CompleteCommittedRun(finalDirectory);
        }

        private void CompleteCommittedRun(string finalDirectory)
        {

            SetState(ExperimentBatchExecutionState.CompletingRun);
            bool hasMore = sequencer.RecordCompletion(
                activePlan,
                state,
                currentRun,
                UtcNow(),
                finalDirectory);
            lastCompletedRun = string.Format(
                CultureInfo.CurrentCulture,
                "{0} / Run {1}",
                currentRun.Scenario.Name,
                currentRun.RunIndex + 1);
            persistence.SaveState(batchDirectory, state);

            levelCore?.PandemicManager?.CompleteBatchFinalization();
            currentRun = null;
            currentRunContext = null;
            frozenExportRequest = null;
            currentAttemptDirectory = null;

            if (hasMore)
            {
                SetState(ExperimentBatchExecutionState.PreparingNextRun);
                SetState(ExperimentBatchExecutionState.PreparingRun);
                PrepareCurrentRunAndReload();
            }
            else
            {
                CompleteBatchRuns();
            }
        }

        private void CompleteBatchRuns()
        {
            RestoreOriginalConfiguration(levelCore);
            if (activePlan.ReturnToBaseline && levelCore != null)
            {
                returningToBaseline = true;
                SetState(ExperimentBatchExecutionState.ReturningToBaseline);
                RequestBaselineReload();
            }
            else
            {
                FinishTermination(ExperimentBatchExecutionState.Completed);
            }
        }

        private ExperimentBatchPanelActionResult ResumeInterruptedBatch()
        {
            if (activePlan == null || state == null)
            {
                return Failure("The recoverable batch plan is unavailable.");
            }

            try
            {
                ExperimentValidationResult planValidation = validator.Validate(activePlan);
                if (!planValidation.IsValid)
                {
                    return Failure(string.Join(Environment.NewLine, planValidation.Errors.ToArray()));
                }

                BaselineSaveResolution baseline = saveCatalog.Resolve(activePlan.Baseline);
                if (!baseline.Success)
                {
                    return Failure(baseline.Error);
                }

                ExperimentOutputPreflightResult output = outputRootResolver.Preflight(activePlan.OutputLocation);
                if (!output.Success || !PathsEqual(output.ResolvedPath, activePlan.OutputRoot))
                {
                    return Failure(output.Success ? "The stored output root no longer matches." : output.Error);
                }

                state.SessionId = sessionNonce;
                state.ReloadToken = null;
                state.CurrentAttemptId = Guid.NewGuid().ToString("N");
                state.Error = null;
                state.LastError = null;
                AcquireRuntimeOwnership();
                SetState(ExperimentBatchExecutionState.Validating);

                bool reconciled;
                string reconciliationError;
                if (!TryReconcileInterruptedCommit(out reconciled, out reconciliationError))
                {
                    Fail(ExperimentBatchExecutionState.ExportFailed, "CommitRecoveryFailed", reconciliationError, null, true);
                    return Failure(reconciliationError);
                }

                if (reconciled)
                {
                    return Success("Recovered a valid committed run; continuing from the next incomplete run.");
                }

                if (state.ScenarioIndex >= activePlan.Scenarios.Count)
                {
                    returningToBaseline = activePlan.ReturnToBaseline;
                    if (returningToBaseline)
                    {
                        SetState(ExperimentBatchExecutionState.ReturningToBaseline);
                        RequestBaselineReload();
                    }
                    else
                    {
                        FinishTermination(ExperimentBatchExecutionState.Completed);
                    }
                }
                else
                {
                    SetState(ExperimentBatchExecutionState.PreparingRun);
                    PrepareCurrentRunAndReload();
                }

                return Success("The incomplete run will restart from the exact baseline with the same indexes and seed.");
            }
            catch (Exception exception)
            {
                ReleaseRuntimeOwnership();
                return Failure("The interrupted batch could not resume: " + exception.Message);
            }
        }

        private bool TryReconcileInterruptedCommit(out bool reconciled, out string error)
        {
            reconciled = false;
            error = null;
            if (state.ScenarioIndex < 0 || state.ScenarioIndex >= activePlan.Scenarios.Count
                || string.IsNullOrEmpty(state.CurrentRunId))
            {
                return true;
            }

            currentRun = sequencer.SelectCurrentRun(activePlan, state);
            var resolver = new ExperimentPathResolver(activePlan.OutputRoot);
            string finalDirectory = resolver.ResolveRunDirectory(activePlan, currentRun);
            string attemptDirectory = string.IsNullOrEmpty(state.CurrentAttemptId)
                ? null
                : resolver.ResolveInProgressRunDirectory(activePlan, currentRun, state.CurrentAttemptId);
            bool finalExists = Directory.Exists(finalDirectory);
            bool preparedAttemptExists = !string.IsNullOrEmpty(attemptDirectory)
                && File.Exists(Path.Combine(attemptDirectory, ExperimentRunCommitService.RunManifestFileName));
            if (!finalExists && !preparedAttemptExists)
            {
                currentRun = null;
                return true;
            }

            ExperimentRunCommitResult recovery = runCommitService.Recover(
                batchDirectory,
                finalExists ? finalDirectory : attemptDirectory,
                finalDirectory,
                currentRun.RunId);
            if (!recovery.Success)
            {
                error = recovery.Error;
                return false;
            }

            bool hasMore = sequencer.RecordCompletion(activePlan, state, currentRun, UtcNow(), finalDirectory);
            persistence.SaveState(batchDirectory, state);
            currentRun = null;
            reconciled = true;
            if (hasMore)
            {
                SetState(ExperimentBatchExecutionState.PreparingRun);
                PrepareCurrentRunAndReload();
            }
            else
            {
                CompleteBatchRuns();
            }

            return true;
        }

        private void FinishTermination(ExperimentBatchExecutionState terminalState)
        {
            RestoreOriginalConfiguration(levelCore);
            RestoreStartingSpeedAndPause();
            SetState(terminalState);
            locator.Clear(UtcNow());
            ReleaseRuntimeOwnership();
            planFrozen = false;
            returningToBaseline = false;
            aborting = false;
            plannedReloadToken = null;
            currentRun = null;
            currentRunContext = null;
            frozenExportRequest = null;
            currentAttemptDirectory = null;
            batchDirectory = null;
            activePlan = null;
            state = null;
        }

        private void Interrupt(string code, string message, Exception exception, RealTimeCore core)
        {
            core?.PandemicManager?.AbortBatchRun();
            RestoreOriginalConfiguration(core);
            RestoreStartingSpeedAndPause();
            SetError(code, message, exception, true);
            SetState(ExperimentBatchExecutionState.Interrupted);
            plannedReloadToken = null;
            returningToBaseline = false;
            aborting = false;
            ReleaseRuntimeOwnership();
        }

        private void Fail(
            ExperimentBatchExecutionState failureState,
            string code,
            string message,
            Exception exception,
            bool retryable)
        {
            if (state != null && !string.IsNullOrEmpty(state.CurrentAttemptId))
            {
                if (state.FailedAttemptIds == null) state.FailedAttemptIds = new List<string>();
                if (!state.FailedAttemptIds.Contains(state.CurrentAttemptId)) state.FailedAttemptIds.Add(state.CurrentAttemptId);
            }
            SetSimulationPaused(true);
            SetError(code, message, exception, retryable);
            SetState(failureState);
            Log.Warning("[TENUS Batch] " + message + (exception == null ? string.Empty : " " + exception));
        }

        private void SetError(string code, string message, Exception exception, bool retryable)
        {
            if (state == null)
            {
                return;
            }

            state.LastError = exception == null ? message : message + " " + exception.Message;
            state.Error = new ExperimentErrorInfo
            {
                Code = code,
                Message = message,
                Detail = exception?.ToString(),
                OccurredUtc = UtcNow(),
                Retryable = retryable,
            };
        }

        private void ClearError()
        {
            if (state != null)
            {
                state.LastError = null;
                state.Error = null;
            }
        }

        private void Transition(ExperimentBatchExecutionState target)
        {
            if (state == null)
            {
                throw new InvalidOperationException("No durable experiment state exists.");
            }

            string error;
            if (!ExperimentBatchStateMachine.TryTransition(state, target, UtcNow(), out error))
            {
                throw new InvalidOperationException(error);
            }

            PersistState();
        }

        private void SetState(ExperimentBatchExecutionState target)
        {
            if (state == null)
            {
                throw new InvalidOperationException("No durable experiment state exists.");
            }

            state.State = target;
            state.UpdatedUtc = UtcNow();
            PersistState();
        }

        private void PersistState()
        {
            if (!string.IsNullOrEmpty(batchDirectory) && state != null)
            {
                persistence.SaveState(batchDirectory, state);
            }
        }

        private void AcquireRuntimeOwnership()
        {
            if (controlLease == null)
            {
                controlLease = ExperimentControlGate.Acquire(ControlOwnerPrefix + activePlan.BatchId);
            }

            if (!autoSavePaused && Singleton<LoadingManager>.exists)
            {
                LoadingManager.instance.autoSaveTimer.Pause();
                autoSavePaused = true;
            }
        }

        private void ReleaseRuntimeOwnership()
        {
            if (autoSavePaused && Singleton<LoadingManager>.exists)
            {
                try
                {
                    LoadingManager.instance.autoSaveTimer.UnPause();
                }
                catch (Exception exception)
                {
                    Log.Warning("[TENUS Batch] Could not release the autosave pause: " + exception);
                }
            }

            autoSavePaused = false;
            controlLease?.Dispose();
            controlLease = null;
        }

        private void ApplyExecutionSpeed()
        {
            if (!Singleton<SimulationManager>.exists || activePlan == null)
            {
                return;
            }

            int speed = activePlan.SpeedMode == ExperimentSpeedMode.PreserveStartingSpeed
                ? activePlan.OriginalSimulationSpeed
                : (int)activePlan.SpeedMode;
            SimulationManager manager = SimulationManager.instance;
            int selectedSpeed = Math.Max(1, Math.Min(3, speed));
            // The setter may dispatch guide UI work and must run on the simulation thread.
            manager.AddAction(() => manager.SelectedSimulationSpeed = selectedSpeed);
        }

        private static void SetSimulationPaused(bool paused)
        {
            if (Singleton<SimulationManager>.exists)
            {
                SimulationManager.instance.SimulationPaused = paused;
            }
        }

        private void RestoreStartingSpeedAndPause()
        {
            if (activePlan == null || !Singleton<SimulationManager>.exists)
            {
                return;
            }

            SimulationManager manager = SimulationManager.instance;
            int originalSpeed = activePlan.OriginalSimulationSpeed;
            bool originalPaused = activePlan.OriginalSimulationPaused;
            manager.AddAction(() =>
            {
                manager.SelectedSimulationSpeed = originalSpeed;
                manager.SimulationPaused = originalPaused;
            });
        }

        private void RestoreOriginalConfiguration(RealTimeCore core)
        {
            if (activePlan?.OriginalConfiguration == null || core?.Configuration == null)
            {
                return;
            }

            activePlan.OriginalConfiguration.ApplyTo(core.Configuration);
            QuarantineManager.Instance.InLockDown = activePlan.OriginalInitialLockdownEnabled;
            core.RefreshExperimentConfiguration();
        }

        private bool IsOwnedReloadPending()
        {
            return state != null
                && !string.IsNullOrEmpty(plannedReloadToken)
                && string.Equals(state.SessionId, sessionNonce, StringComparison.Ordinal)
                && string.Equals(state.ReloadToken, plannedReloadToken, StringComparison.Ordinal)
                && (state.State == ExperimentBatchExecutionState.RequestingBaselineLoad
                    || state.State == ExperimentBatchExecutionState.WaitingForLevelUnload
                    || state.State == ExperimentBatchExecutionState.WaitingForLevelLoad);
        }

        private bool HasControlledBatch()
        {
            return state != null
                && planFrozen
                && !IsFinishedState(state.State)
                && state.State != ExperimentBatchExecutionState.Interrupted;
        }

        private static bool IsFinishedState(ExperimentBatchExecutionState value)
        {
            return value == ExperimentBatchExecutionState.Completed || value == ExperimentBatchExecutionState.Aborted;
        }

        private ExperimentBatchPanelActionResult RequireEditable()
        {
            if (planFrozen)
            {
                return Failure("The confirmed batch plan is immutable.");
            }

            if (draftPlan == null || ReferenceEquals(draftPlan, activePlan))
            {
                draftPlan = CreateDraftPlan();
            }

            return null;
        }

        private ExperimentBatchPanelActionResult RequireScenarioForEdit(int scenarioIndex, out ExperimentScenario scenario)
        {
            scenario = null;
            ExperimentBatchPanelActionResult editable = RequireEditable();
            if (editable != null)
            {
                return editable;
            }

            if (scenarioIndex < 0 || scenarioIndex >= draftPlan.Scenarios.Count)
            {
                return Failure("Select a valid scenario first.");
            }

            scenario = draftPlan.Scenarios[scenarioIndex];
            return null;
        }

        private bool TryCaptureCurrentScenario(out ExperimentScenarioSnapshot settings, out string error)
        {
            settings = null;
            if (levelCore?.Configuration == null)
            {
                error = "A fully initialized level is required to capture current settings.";
                return false;
            }

            RealTimeConfig normalized = ExperimentConfigurationMapper.CloneConfiguration(levelCore.Configuration);
            normalized.Validate();
            PandemicManager.NormalizeRuntimeConfiguration(normalized);
            settings = ExperimentScenarioSnapshot.Capture(normalized, QuarantineManager.Instance.InLockDown);
            error = null;
            return true;
        }

        private ExperimentBatchPlan CreateDraftPlan()
        {
            var plan = new ExperimentBatchPlan
            {
                BatchId = Guid.NewGuid().ToString("N"),
                BatchName = "Experiment Batch",
                OutputFolderName = "Experiment Batch",
                CreatedUtc = UtcNow(),
                ModVersion = modVersion,
                GameVersion = BuildConfig.applicationVersionFull,
                GitCommitSha = buildIdentity.CommitSha,
                GitBranchOrTag = buildIdentity.BranchOrTag,
                SpeedMode = ExperimentSpeedMode.Speed3,
                ReturnToBaseline = true,
                StopBatchOnRunFailure = true,
                PairedSeedMode = false,
            };

            ExperimentOutputRootSelection selection = ChooseDefaultOutputRoot();
            plan.OutputLocation = selection;
            try
            {
                plan.OutputRoot = outputRootResolver.Resolve(selection);
            }
            catch (Exception)
            {
                plan.OutputRoot = null;
            }

            return plan;
        }

        private ExperimentOutputRootSelection ChooseDefaultOutputRoot()
        {
            var mod = new ExperimentOutputRootSelection { Kind = ExperimentOutputRootKind.ModPandemicData };
            if (outputRootResolver.Preflight(mod).Success)
            {
                return mod;
            }

            return new ExperimentOutputRootSelection { Kind = ExperimentOutputRootKind.UserPandemicData };
        }

        private void PrepareDraftMetadata()
        {
            draftPlan.ModVersion = modVersion;
            draftPlan.GameVersion = BuildConfig.applicationVersionFull;
            draftPlan.GitCommitSha = buildIdentity.CommitSha;
            draftPlan.GitBranchOrTag = buildIdentity.BranchOrTag;
            if (draftPlan.OutputLocation != null)
            {
                draftPlan.OutputRoot = outputRootResolver.Resolve(draftPlan.OutputLocation);
            }
        }

        private void RecoverActiveBatch()
        {
            JsonLoadResult<ActiveExperimentBatchLocator> located = locator.Load();
            if (!located.Success || located.Value == null || !located.Value.IsActive)
            {
                return;
            }

            JsonLoadResult<ExperimentBatchPlan> loadedPlan = persistence.LoadPlan(located.Value.BatchDirectory);
            JsonLoadResult<ExperimentBatchState> loadedState = persistence.LoadState(located.Value.BatchDirectory);
            if (!loadedPlan.Success || !loadedState.Success
                || loadedPlan.Value == null
                || loadedState.Value == null
                || loadedPlan.Value.SchemaVersion != ExperimentSchema.CurrentVersion
                || loadedState.Value.SchemaVersion != ExperimentSchema.CurrentVersion)
            {
                Log.Warning("[TENUS Batch] The active batch locator could not be recovered safely. "
                    + (loadedPlan.Error ?? string.Empty) + " " + (loadedState.Error ?? string.Empty));
                return;
            }

            if (IsFinishedState(loadedState.Value.State))
            {
                locator.Clear(UtcNow());
                return;
            }

            if (string.IsNullOrEmpty(loadedPlan.Value.GitCommitSha))
            {
                loadedPlan.Value.GitCommitSha = buildIdentity.CommitSha;
            }

            if (string.IsNullOrEmpty(loadedPlan.Value.GitBranchOrTag))
            {
                loadedPlan.Value.GitBranchOrTag = buildIdentity.BranchOrTag;
            }

            activePlan = loadedPlan.Value;
            draftPlan = Clone(activePlan);
            state = loadedState.Value;
            batchDirectory = Path.GetFullPath(located.Value.BatchDirectory);
            planFrozen = true;
            string previousSession = state.SessionId;
            state.SessionId = sessionNonce;
            SetError(
                "ApplicationRestart",
                "The previous application session ended before this batch completed. Resume restarts only the incomplete run from the baseline; committed runs are retained.",
                null,
                true);
            state.State = ExperimentBatchExecutionState.Interrupted;
            state.ReloadToken = null;
            state.UpdatedUtc = UtcNow();
            persistence.SaveState(batchDirectory, state);
            Log.Info("[TENUS Batch] Recovered interrupted batch from session " + previousSession + ".");
        }

        private void AddOutputOptions(ICollection<ExperimentOutputRootOption> options)
        {
            AddOutputOption(options, ExperimentOutputRootKind.ModPandemicData, "Mod Pandemic Data");
            AddOutputOption(options, ExperimentOutputRootKind.UserPandemicData, "User Pandemic Data");
        }

        private void ShowPanel()
        {
            if (panel == null && levelCore != null)
            {
                panel = new ExperimentBatchPanel(this);
            }

            panel?.Show();
        }

        private void AddOutputOption(
            ICollection<ExperimentOutputRootOption> options,
            ExperimentOutputRootKind kind,
            string label)
        {
            try
            {
                string path = outputRootResolver.Resolve(new ExperimentOutputRootSelection { Kind = kind });
                options.Add(new ExperimentOutputRootOption
                {
                    OutputRoot = path,
                    DisplayName = label + " — " + path,
                });
            }
            catch (Exception)
            {
            }
        }

        private ExperimentOutputRootKind? FindOutputKind(string path)
        {
            foreach (ExperimentOutputRootKind kind in new[]
            {
                ExperimentOutputRootKind.ModPandemicData,
                ExperimentOutputRootKind.UserPandemicData,
            })
            {
                try
                {
                    if (PathsEqual(outputRootResolver.Resolve(new ExperimentOutputRootSelection { Kind = kind }), path))
                    {
                        return kind;
                    }
                }
                catch (Exception)
                {
                }
            }

            return null;
        }

        private void PopulateProgress(ExperimentBatchPanelViewState view)
        {
            ExperimentBatchPlan plan = activePlan ?? draftPlan;
            view.ScenarioCount = plan?.Scenarios?.Count ?? 0;
            view.TotalRuns = plan == null ? 0 : sequencer.CountTotalRuns(plan);
            view.CompletedRuns = state?.CompletedRuns?.Count ?? 0;
            view.InvalidRuns = state?.InvalidRuns?.Count ?? 0;
            if (state?.State == ExperimentBatchExecutionState.Completed && view.InvalidRuns > 0)
                view.StatusText = "Batch finished with invalid runs: " + view.CompletedRuns + "/" + view.TotalRuns
                    + " completed, " + view.InvalidRuns + " invalid. Results are incomplete.";
            view.FailedAttempts = state?.FailedAttemptIds?.Count ?? 0;
            view.CurrentPairId = state?.CurrentPairId ?? 0;
            if (state != null && plan?.Scenarios != null && state.ScenarioIndex >= 0 && state.ScenarioIndex < plan.Scenarios.Count)
            {
                view.CurrentScenarioNumber = state.ScenarioIndex + 1;
                view.CurrentRunNumber = state.RunIndex + 1;
                view.CurrentScenarioRunCount = plan.Scenarios[state.ScenarioIndex].RunCount;
                view.CurrentSeed = state.CurrentMasterSeed;
                view.CurrentScenarioName = plan.Scenarios[state.ScenarioIndex].Name;
                view.CurrentPresetId = plan.Scenarios[state.ScenarioIndex].PresetId;
                view.TargetSimulationDays = plan.Scenarios[state.ScenarioIndex].DurationDays;
            }

            PandemicManager manager = levelCore?.PandemicManager;
            if (manager != null && manager.IsBatchRunOwned && Singleton<SimulationManager>.exists)
            {
                DateTime start = manager.GetPandemicRunStartedAt();
                view.ElapsedSimulationDays = start == default(DateTime)
                    ? 0d
                    : Math.Max(0d, (SimulationManager.instance.m_currentGameTime - start).TotalDays);
            }
        }

        private int CalculateOverallRunNumber(int scenarioIndex, int runIndex)
        {
            int number = runIndex + 1;
            for (int i = 0; i < scenarioIndex; ++i)
            {
                number = checked(number + activePlan.Scenarios[i].RunCount);
            }

            return number;
        }

        private static string BuildBaselineDisplayName(SaveGameCatalogEntry entry)
        {
            DateTime timestamp;
            string time = DateTime.TryParse(
                entry.Identity.SaveTimestampUtc,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out timestamp)
                ? timestamp.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)
                : "unknown time";
            string location = string.IsNullOrEmpty(entry.Identity.LocalFileSha256) ? "Cloud" : "Local";
            return string.Format(CultureInfo.CurrentCulture, "{0} — {1} — {2}", entry.DisplayName, time, location);
        }

        private static string FormatDifferences(IEnumerable<ExperimentSettingDifference> differences)
        {
            return string.Join(
                "; ",
                differences.Take(8).Select(difference => string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} expected {1}, found {2}",
                    difference.PropertyName,
                    difference.LeftValue,
                    difference.RightValue)).ToArray());
        }

        private static string StateText(ExperimentBatchExecutionState value)
        {
            switch (value)
            {
                case ExperimentBatchExecutionState.Interrupted:
                    return "Interrupted — choose Resume from baseline or Abort.";
                case ExperimentBatchExecutionState.LoadFailed:
                    return "Baseline load failed — Retry Load or Abort.";
                case ExperimentBatchExecutionState.ScenarioApplyFailed:
                case ExperimentBatchExecutionState.RunFailed:
                    return "Scenario preparation failed — Retry Preparation or Abort.";
                case ExperimentBatchExecutionState.ExportFailed:
                    return "Export failed — Retry Export in this session, or Abort.";
                default:
                    return value.ToString();
            }
        }

        private static bool PathsEqual(string left, string right)
        {
            if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
            {
                return false;
            }

            return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
        }

        private T Clone<T>(T value)
        {
            return value == null ? default(T) : cloner.Deserialize<T>(cloner.Serialize(value));
        }

        private static string UtcNow()
        {
            return DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        }

        private static string SimulationIso(DateTime value)
        {
            return value == default(DateTime)
                ? null
                : DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("o", CultureInfo.InvariantCulture);
        }

        private static ExperimentBatchPanelActionResult Success(string message)
        {
            return new ExperimentBatchPanelActionResult { Succeeded = true, Message = message };
        }

        private static ExperimentBatchPanelActionResult Failure(string message)
        {
            return new ExperimentBatchPanelActionResult { Message = message };
        }

    }
}
