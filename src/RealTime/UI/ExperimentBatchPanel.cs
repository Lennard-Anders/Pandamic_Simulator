// <copyright file="ExperimentBatchPanel.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.UI
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;
    using ColossalFramework.UI;
    using RealTime.Experiments;
    using UnityEngine;

    /// <summary>
    /// Provides all operations used by <see cref="ExperimentBatchPanel"/>. The panel never owns
    /// execution state and never changes plan objects returned by <see cref="GetViewState"/>.
    /// </summary>
    internal interface IExperimentBatchPanelController
    {
        ExperimentBatchPanelViewState GetViewState();

        ExperimentBatchPanelActionResult RefreshBaselineCatalog();

        ExperimentBatchPanelActionResult SetBatchName(string batchName);

        ExperimentBatchPanelActionResult SetOutputFolderName(string outputFolderName);

        ExperimentBatchPanelActionResult SelectBaseline(string assetFullName);

        ExperimentBatchPanelActionResult SelectOutputRoot(string outputRoot);

        ExperimentBatchPanelActionResult SetExecutionSpeed(ExperimentSpeedMode speed);

        ExperimentBatchPanelActionResult SetReturnToBaseline(bool returnToBaseline);

        ExperimentBatchPanelActionResult AddCurrentSettingsAsScenario();

        ExperimentBatchPanelActionResult DuplicateScenario(int scenarioIndex);

        ExperimentBatchPanelActionResult RemoveScenario(int scenarioIndex);

        ExperimentBatchPanelActionResult MoveScenario(int scenarioIndex, int newIndex);

        ExperimentBatchPanelActionResult UpdateScenarioFromCurrentSettings(int scenarioIndex);

        ExperimentBatchPanelActionResult UpdateScenario(
            int scenarioIndex,
            string name,
            int runCount,
            double durationDays,
            ExperimentEndMode endMode,
            ExperimentSeedStrategy seedStrategy,
            int firstSeed);

        ExperimentBatchPanelPreflight BuildPreflight();

        ExperimentBatchPanelActionResult StartBatch();

        ExperimentBatchPanelActionResult PauseBatch();

        ExperimentBatchPanelActionResult ResumeBatch();

        ExperimentBatchPanelActionResult AbortBatch();

        ExperimentBatchPanelActionResult AbortBatchInPlace();

        ExperimentBatchPanelActionResult RetryBaselineLoad();

        ExperimentBatchPanelActionResult RetryExport();

        ExperimentBatchPanelActionResult RetryPreparation();
    }

    /// <summary>A baseline entry displayed by the exact-save selector.</summary>
    internal sealed class ExperimentBaselineOption
    {
        public string AssetFullName { get; set; }

        public string DisplayName { get; set; }
    }

    /// <summary>An output root entry displayed by the output selector.</summary>
    internal sealed class ExperimentOutputRootOption
    {
        public string OutputRoot { get; set; }

        public string DisplayName { get; set; }
    }

    /// <summary>Read-only data rendered by the batch panel.</summary>
    internal sealed class ExperimentBatchPanelViewState
    {
        public ExperimentBatchPanelViewState()
        {
            Baselines = new List<ExperimentBaselineOption>();
            OutputRoots = new List<ExperimentOutputRootOption>();
        }

        public ExperimentBatchPlan Plan { get; set; }

        public IList<ExperimentBaselineOption> Baselines { get; set; }

        public IList<ExperimentOutputRootOption> OutputRoots { get; set; }

        public ExperimentBatchExecutionState ExecutionState { get; set; }

        public bool IsPlanReadOnly { get; set; }

        public bool CanStart { get; set; }

        public bool CanPause { get; set; }

        public bool CanResume { get; set; }

        public bool CanAbort { get; set; }

        public bool CanRetryBaselineLoad { get; set; }

        public bool CanRetryExport { get; set; }

        public bool CanRetryPreparation { get; set; }

        public int CurrentScenarioNumber { get; set; }

        public int ScenarioCount { get; set; }

        public int CurrentRunNumber { get; set; }

        public int CurrentScenarioRunCount { get; set; }

        public int CompletedRuns { get; set; }

        public int TotalRuns { get; set; }

        public int CurrentSeed { get; set; }

        public double ElapsedSimulationDays { get; set; }

        public double TargetSimulationDays { get; set; }

        public string StatusText { get; set; }

        public string LastCompletedRun { get; set; }

        public string ErrorText { get; set; }
    }

    /// <summary>Result of a controller operation initiated by the panel.</summary>
    internal sealed class ExperimentBatchPanelActionResult
    {
        public bool Succeeded { get; set; }

        public string Message { get; set; }
    }

    /// <summary>Information shown before the user confirms a batch.</summary>
    internal sealed class ExperimentBatchPanelPreflight
    {
        public bool IsValid { get; set; }

        public string Summary { get; set; }

        public string Warning { get; set; }

        public string Error { get; set; }
    }

    /// <summary>A separate, hideable editor and monitor for automated experiment batches.</summary>
    internal sealed class ExperimentBatchPanel
    {
        private const string PanelName = "RealTimeExperimentBatchPanel";
        private const float PanelWidth = 910f;
        private const float PanelHeight = 850f;
        private const float PanelMargin = 15f;
        private const float ScenarioRowHeight = 28f;
        private const float ScenarioRowPitch = 30f;

        private static readonly string[] SpeedNames =
        {
            "Preserve current speed",
            "Speed 1",
            "Speed 2",
            "Speed 3",
        };

        private static readonly string[] EndModeNames =
        {
            "Fixed duration",
            "Duration or epidemic extinction",
        };

        private static readonly string[] SeedStrategyNames =
        {
            "Fixed seed",
            "Sequential seeds",
        };

        private readonly IExperimentBatchPanelController controller;
        private readonly List<UIButton> scenarioButtons = new List<UIButton>();
        private readonly List<string> baselineIds = new List<string>();
        private readonly List<string> outputRoots = new List<string>();

        private UIPanel panel;
        private UITextField batchNameField;
        private UITextField outputFolderNameField;
        private UIDropDown baselineDropDown;
        private UIButton refreshBaselinesButton;
        private UIDropDown outputRootDropDown;
        private UIButton switchOutputRootButton;
        private UIDropDown speedDropDown;
        private UICheckBox returnToBaselineCheckBox;
        private UIScrollablePanel scenarioList;
        private UILabel planSummaryLabel;
        private UITextField scenarioNameField;
        private UITextField runCountField;
        private UITextField durationField;
        private UIDropDown endModeDropDown;
        private UIDropDown seedStrategyDropDown;
        private UITextField firstSeedField;
        private UIButton addScenarioButton;
        private UIButton duplicateScenarioButton;
        private UIButton removeScenarioButton;
        private UIButton moveUpButton;
        private UIButton moveDownButton;
        private UIButton saveScenarioButton;
        private UIButton updateSettingsButton;
        private UILabel editorHintLabel;
        private UILabel stateLabel;
        private UILabel progressLabel;
        private UIPanel progressTrack;
        private UIPanel progressFill;
        private UILabel detailLabel;
        private UILabel errorLabel;
        private UIButton startButton;
        private UIButton pauseButton;
        private UIButton resumeButton;
        private UIButton abortButton;
        private UIButton abortInPlaceButton;
        private UIButton retryLoadButton;
        private UIButton retryExportButton;
        private UIButton retryPreparationButton;

        private ExperimentBatchPanelViewState viewState;
        private int selectedScenarioIndex = -1;
        private int lastEditorScenarioIndex = -2;
        private string lastScenarioListSignature;
        private string lastEditorSignature;
        private string lastBaselineOptionsSignature;
        private string lastOutputRootOptionsSignature;
        private string localMessage;
        private string localError;
        private bool suppressEvents;

        public ExperimentBatchPanel(IExperimentBatchPanelController controller)
        {
            this.controller = controller ?? throw new ArgumentNullException(nameof(controller));
        }

        public bool IsVisible
        {
            get { return panel != null && panel.isVisible; }
        }

        /// <summary>Creates the static widgets once and shows the panel.</summary>
        public void Enable()
        {
            if (panel != null)
            {
                Show();
                return;
            }

            UIView view = UIView.GetAView();
            if (view == null)
            {
                return;
            }

            UIPanel existing = view.FindUIComponent<UIPanel>(PanelName);
            if (existing != null)
            {
                UnityEngine.Object.Destroy(existing.gameObject);
            }

            panel = view.AddUIComponent(typeof(UIPanel)) as UIPanel;
            if (panel == null)
            {
                return;
            }

            panel.name = PanelName;
            panel.autoSize = false;
            panel.width = PanelWidth;
            panel.height = PanelHeight;
            panel.backgroundSprite = "MenuPanel2";
            panel.opacity = 0.96f;
            panel.canFocus = true;
            panel.isInteractive = true;
            panel.clipChildren = true;
            PositionPanel(view);

            CreateHeader();
            CreatePlanControls();
            CreateScenarioControls();
            CreateProgressControls();

            ExperimentBatchPanelUpdateBehavior updater = panel.gameObject.AddComponent<ExperimentBatchPanelUpdateBehavior>();
            updater.Owner = this;
            Refresh();
        }

        /// <summary>Shows the existing panel, creating it when necessary.</summary>
        public void Show()
        {
            if (panel == null)
            {
                Enable();
                return;
            }

            panel.Show();
            panel.BringToFront();
            Refresh();
        }

        /// <summary>Hides the panel without changing or aborting the active batch.</summary>
        public void Hide()
        {
            if (panel != null)
            {
                panel.Hide();
            }
        }

        /// <summary>Destroys only the UI. Batch execution remains owned by the controller.</summary>
        public void Disable()
        {
            if (panel != null)
            {
                UnityEngine.Object.Destroy(panel.gameObject);
            }

            panel = null;
            viewState = null;
            scenarioButtons.Clear();
            baselineIds.Clear();
            outputRoots.Clear();
            lastScenarioListSignature = null;
            lastEditorSignature = null;
            lastBaselineOptionsSignature = null;
            lastOutputRootOptionsSignature = null;
            lastEditorScenarioIndex = -2;
        }

        /// <summary>Refreshes the panel from the controller's immutable view snapshot.</summary>
        public void Refresh()
        {
            if (panel == null)
            {
                return;
            }

            ExperimentBatchPanelViewState snapshot;
            try
            {
                snapshot = controller.GetViewState();
            }
            catch (Exception exception)
            {
                localError = "Unable to refresh the batch runner: " + exception.Message;
                RenderMessages();
                return;
            }

            viewState = snapshot ?? new ExperimentBatchPanelViewState();
            ExperimentBatchPlan plan = viewState.Plan;
            bool readOnly = viewState.IsPlanReadOnly;

            suppressEvents = true;
            try
            {
                RefreshPlanControls(plan);
                RefreshScenarioList(plan);
                RefreshScenarioEditor(plan, readOnly);
                RefreshExecutionControls();
            }
            finally
            {
                suppressEvents = false;
            }

            RenderMessages();
        }

        private void CreateHeader()
        {
            UIDragHandle dragHandle = panel.AddUIComponent<UIDragHandle>();
            dragHandle.autoSize = false;
            dragHandle.width = PanelWidth - 52f;
            dragHandle.height = 36f;
            dragHandle.relativePosition = Vector3.zero;
            dragHandle.target = panel;
            dragHandle.constrainToScreen = true;

            UILabel title = dragHandle.AddUIComponent<UILabel>();
            title.autoSize = false;
            title.width = dragHandle.width - 20f;
            title.height = 24f;
            title.relativePosition = new Vector3(12f, 8f);
            title.text = "Experiment Batch Runner";
            title.textScale = 1.05f;
            title.textColor = new Color32(255, 255, 255, 255);
            title.textAlignment = UIHorizontalAlignment.Left;

            UIButton closeButton = CreateButton(panel, PanelWidth - 40f, 7f, 28f, "X", Hide);
            closeButton.tooltip = "Hide this panel. Closing does not pause or abort the batch.";
        }

        private void CreatePlanControls()
        {
            CreateSectionLabel("Batch setup", 12f, 40f, PanelWidth - 24f);

            CreateFieldLabel("Batch name", 12f, 68f, 125f);
            batchNameField = CreateTextField(panel, 145f, 64f, 245f);
            batchNameField.tooltip = "The descriptive batch name stored in manifests and summaries.";
            batchNameField.eventTextSubmitted += (component, value) => CommitBatchName();
            batchNameField.eventLostFocus += (component, parameter) => CommitBatchName();

            CreateFieldLabel("Folder name", 405f, 68f, 105f);
            outputFolderNameField = CreateTextField(panel, 518f, 64f, 372f);
            outputFolderNameField.tooltip = "Readable output-folder prefix. Invalid filename characters are sanitized; a short timestamp and ID are appended to prevent overwrites.";
            outputFolderNameField.eventTextSubmitted += (component, value) => CommitOutputFolderName();
            outputFolderNameField.eventLostFocus += (component, parameter) => CommitOutputFolderName();

            CreateFieldLabel("Baseline save", 12f, 100f, 125f);
            baselineDropDown = CreateDropDown(panel, 145f, 96f, 637f);
            baselineDropDown.tooltip = "Select an exact save-game asset identity, not a display-name match.";
            baselineDropDown.eventSelectedIndexChanged += OnBaselineChanged;
            refreshBaselinesButton = CreateButton(panel, 790f, 96f, 100f, "Refresh", RefreshBaselines);
            refreshBaselinesButton.tooltip = "Rescan exact SaveGameMetaData assets.";

            CreateFieldLabel("Output root", 12f, 132f, 125f);
            outputRootDropDown = CreateDropDown(panel, 145f, 128f, 637f);
            outputRootDropDown.tooltip = "Choose the configured root under which batch artifacts are written.";
            outputRootDropDown.eventSelectedIndexChanged += OnOutputRootChanged;
            switchOutputRootButton = CreateButton(panel, 790f, 128f, 100f, "Switch", SwitchOutputRoot);
            switchOutputRootButton.tooltip = "Switch between the mod Pandemic Data folder and the user-data Pandemic Data folder.";

            CreateFieldLabel("Execution speed", 12f, 164f, 125f);
            speedDropDown = CreateDropDown(panel, 145f, 160f, 220f);
            speedDropDown.items = SpeedNames;
            speedDropDown.eventSelectedIndexChanged += OnSpeedChanged;

            returnToBaselineCheckBox = CreateCheckBox(panel, 392f, 164f, 430f, "Return to the exact baseline after the final run");
            returnToBaselineCheckBox.tooltip = "The batch remains complete even if the optional final return load fails.";
            returnToBaselineCheckBox.eventCheckChanged += OnReturnToBaselineChanged;
        }

        private void CreateScenarioControls()
        {
            CreateSectionLabel("Scenarios", 12f, 202f, PanelWidth - 24f);

            planSummaryLabel = panel.AddUIComponent<UILabel>();
            planSummaryLabel.autoSize = false;
            planSummaryLabel.width = 360f;
            planSummaryLabel.height = 22f;
            planSummaryLabel.relativePosition = new Vector3(530f, 202f);
            planSummaryLabel.textScale = 0.72f;
            planSummaryLabel.textColor = new Color32(190, 205, 220, 255);
            planSummaryLabel.textAlignment = UIHorizontalAlignment.Right;

            scenarioList = panel.AddUIComponent<UIScrollablePanel>();
            scenarioList.autoSize = false;
            scenarioList.width = 330f;
            scenarioList.height = 290f;
            scenarioList.relativePosition = new Vector3(12f, 228f);
            scenarioList.backgroundSprite = "GenericPanel";
            scenarioList.color = new Color32(42, 52, 65, 255);
            scenarioList.opacity = 0.92f;
            scenarioList.clipChildren = true;
            scenarioList.autoLayout = false;
            scenarioList.wrapLayout = false;
            scenarioList.scrollWheelAmount = 60;

            addScenarioButton = CreateButton(panel, 12f, 528f, 160f, "Add current settings", AddScenario);
            duplicateScenarioButton = CreateButton(panel, 180f, 528f, 162f, "Duplicate selected", DuplicateScenario);
            removeScenarioButton = CreateButton(panel, 12f, 562f, 100f, "Remove", RemoveScenario);
            moveUpButton = CreateButton(panel, 120f, 562f, 107f, "Move up", MoveScenarioUp);
            moveDownButton = CreateButton(panel, 235f, 562f, 107f, "Move down", MoveScenarioDown);

            const float labelX = 366f;
            const float fieldX = 510f;
            const float fieldWidth = 380f;

            CreateFieldLabel("Scenario name", labelX, 232f, 135f);
            scenarioNameField = CreateTextField(panel, fieldX, 228f, fieldWidth);

            CreateFieldLabel("Runs", labelX, 266f, 135f);
            runCountField = CreateTextField(panel, fieldX, 262f, fieldWidth);
            runCountField.tooltip = "Positive number of repetitions.";

            CreateFieldLabel("Duration (days)", labelX, 300f, 135f);
            durationField = CreateTextField(panel, fieldX, 296f, fieldWidth);
            durationField.tooltip = "Finite, positive simulated days. Fractional days are supported.";

            CreateFieldLabel("End mode", labelX, 334f, 135f);
            endModeDropDown = CreateDropDown(panel, fieldX, 330f, fieldWidth);
            endModeDropDown.items = EndModeNames;

            CreateFieldLabel("Seed strategy", labelX, 368f, 135f);
            seedStrategyDropDown = CreateDropDown(panel, fieldX, 364f, fieldWidth);
            seedStrategyDropDown.items = SeedStrategyNames;

            CreateFieldLabel("First seed", labelX, 402f, 135f);
            firstSeedField = CreateTextField(panel, fieldX, 398f, fieldWidth);
            firstSeedField.tooltip = "Non-negative master seed; sequential runs increment from this value.";

            saveScenarioButton = CreateButton(panel, labelX, 444f, 252f, "Save scenario fields", SaveScenario);
            updateSettingsButton = CreateButton(panel, 626f, 444f, 264f, "Replace with current settings", UpdateScenarioSettings);
            updateSettingsButton.tooltip = "Replace only the selected scenario's captured mod settings.";

            editorHintLabel = panel.AddUIComponent<UILabel>();
            editorHintLabel.autoSize = false;
            editorHintLabel.width = 524f;
            editorHintLabel.height = 70f;
            editorHintLabel.relativePosition = new Vector3(labelX, 482f);
            editorHintLabel.textScale = 0.72f;
            editorHintLabel.textColor = new Color32(190, 205, 220, 255);
            editorHintLabel.wordWrap = true;
            editorHintLabel.text = "Select a scenario to edit its run policy. During an active batch the frozen plan is read-only; closing this window does not stop execution.";
        }

        private void CreateProgressControls()
        {
            CreateSectionLabel("Execution", 12f, 604f, PanelWidth - 24f);

            stateLabel = panel.AddUIComponent<UILabel>();
            stateLabel.autoSize = false;
            stateLabel.width = PanelWidth - 24f;
            stateLabel.height = 40f;
            stateLabel.relativePosition = new Vector3(12f, 630f);
            stateLabel.textScale = 0.78f;
            stateLabel.textColor = new Color32(235, 240, 245, 255);
            stateLabel.wordWrap = true;

            progressTrack = panel.AddUIComponent<UIPanel>();
            progressTrack.autoSize = false;
            progressTrack.width = PanelWidth - 24f;
            progressTrack.height = 14f;
            progressTrack.relativePosition = new Vector3(12f, 676f);
            progressTrack.backgroundSprite = "GenericPanel";
            progressTrack.color = new Color32(28, 35, 44, 255);

            progressFill = progressTrack.AddUIComponent<UIPanel>();
            progressFill.autoSize = false;
            progressFill.width = 0f;
            progressFill.height = progressTrack.height;
            progressFill.relativePosition = Vector3.zero;
            progressFill.backgroundSprite = "GenericPanel";
            progressFill.color = new Color32(65, 160, 105, 255);

            progressLabel = progressTrack.AddUIComponent<UILabel>();
            progressLabel.autoSize = false;
            progressLabel.width = progressTrack.width;
            progressLabel.height = progressTrack.height;
            progressLabel.relativePosition = Vector3.zero;
            progressLabel.textScale = 0.65f;
            progressLabel.textAlignment = UIHorizontalAlignment.Center;
            progressLabel.verticalAlignment = UIVerticalAlignment.Middle;
            progressLabel.textColor = new Color32(255, 255, 255, 255);

            detailLabel = panel.AddUIComponent<UILabel>();
            detailLabel.autoSize = false;
            detailLabel.width = PanelWidth - 24f;
            detailLabel.height = 42f;
            detailLabel.relativePosition = new Vector3(12f, 698f);
            detailLabel.textScale = 0.72f;
            detailLabel.textColor = new Color32(195, 210, 225, 255);
            detailLabel.wordWrap = true;

            errorLabel = panel.AddUIComponent<UILabel>();
            errorLabel.autoSize = false;
            errorLabel.width = PanelWidth - 24f;
            errorLabel.height = 46f;
            errorLabel.relativePosition = new Vector3(12f, 744f);
            errorLabel.textScale = 0.72f;
            errorLabel.textColor = new Color32(255, 145, 135, 255);
            errorLabel.wordWrap = true;

            startButton = CreateButton(panel, 12f, 806f, 105f, "Preflight / Start", StartBatch);
            pauseButton = CreateButton(panel, 124f, 806f, 70f, "Pause", PauseBatch);
            resumeButton = CreateButton(panel, 201f, 806f, 75f, "Resume", ResumeBatch);
            abortButton = CreateButton(panel, 283f, 806f, 110f, "Abort + return", ConfirmAbort);
            abortInPlaceButton = CreateButton(panel, 400f, 806f, 90f, "Abort here", ConfirmAbortInPlace);
            abortInPlaceButton.tooltip = "Abort without loading the baseline. Committed runs remain published.";
            retryLoadButton = CreateButton(panel, 497f, 806f, 115f, "Retry load", RetryBaselineLoad);
            retryExportButton = CreateButton(panel, 619f, 806f, 90f, "Retry export", RetryExport);
            retryPreparationButton = CreateButton(panel, 716f, 806f, 162f, "Retry preparation", RetryPreparation);
        }

        private void RefreshPlanControls(ExperimentBatchPlan plan)
        {
            bool readOnly = viewState.IsPlanReadOnly;
            string batchName = plan == null ? string.Empty : plan.BatchName ?? string.Empty;
            if (!batchNameField.hasFocus && !string.Equals(batchNameField.text, batchName, StringComparison.Ordinal))
            {
                batchNameField.text = batchName;
            }

            string outputFolderName = plan == null ? string.Empty : plan.OutputFolderName ?? plan.BatchName ?? string.Empty;
            if (!outputFolderNameField.hasFocus
                && !string.Equals(outputFolderNameField.text, outputFolderName, StringComparison.Ordinal))
            {
                outputFolderNameField.text = outputFolderName;
            }

            RefreshBaselineDropDown(plan);
            RefreshOutputRootDropDown(plan);

            int speedIndex = plan == null ? 0 : (int)plan.SpeedMode;
            speedDropDown.selectedIndex = ClampIndex(speedIndex, SpeedNames.Length);
            returnToBaselineCheckBox.isChecked = plan == null || plan.ReturnToBaseline;
            int scenarioCount = plan == null || plan.Scenarios == null ? 0 : plan.Scenarios.Count;
            int totalRuns = CountPlannedRuns(plan);
            planSummaryLabel.text = string.Format(
                CultureInfo.CurrentCulture,
                "{0} scenario{1} · {2} total run{3}",
                scenarioCount,
                scenarioCount == 1 ? string.Empty : "s",
                totalRuns,
                totalRuns == 1 ? string.Empty : "s");

            SetEditable(batchNameField, !readOnly);
            SetEditable(outputFolderNameField, !readOnly);
            SetEnabled(baselineDropDown, !readOnly);
            SetEnabled(refreshBaselinesButton, !readOnly);
            SetEnabled(outputRootDropDown, !readOnly);
            SetEnabled(switchOutputRootButton, !readOnly);
            SetEnabled(speedDropDown, !readOnly);
            returnToBaselineCheckBox.readOnly = readOnly;
            SetEnabled(returnToBaselineCheckBox, !readOnly);
        }

        private void RefreshBaselineDropDown(ExperimentBatchPlan plan)
        {
            string selected = plan == null || plan.Baseline == null ? null : plan.Baseline.AssetFullName;
            var labels = new List<string>();
            baselineIds.Clear();

            IList<ExperimentBaselineOption> options = viewState.Baselines;
            if (options != null)
            {
                for (int i = 0; i < options.Count; ++i)
                {
                    ExperimentBaselineOption option = options[i];
                    if (option == null || string.IsNullOrEmpty(option.AssetFullName))
                    {
                        continue;
                    }

                    baselineIds.Add(option.AssetFullName);
                    labels.Add(string.IsNullOrEmpty(option.DisplayName) ? option.AssetFullName : option.DisplayName);
                }
            }

            int selectedIndex = IndexOfOrdinal(baselineIds, selected);
            if (!string.IsNullOrEmpty(selected) && selectedIndex < 0)
            {
                baselineIds.Add(selected);
                labels.Add("[not in current catalog] " + selected);
                selectedIndex = baselineIds.Count - 1;
            }

            if (labels.Count == 0)
            {
                labels.Add("<no exact save selected>");
                baselineIds.Add(null);
                selectedIndex = 0;
            }

            string signature = BuildChoiceSignature(labels, baselineIds);
            if (!string.Equals(signature, lastBaselineOptionsSignature, StringComparison.Ordinal))
            {
                baselineDropDown.items = labels.ToArray();
                lastBaselineOptionsSignature = signature;
            }

            int nextSelectedIndex = selectedIndex < 0 ? 0 : selectedIndex;
            if (baselineDropDown.selectedIndex != nextSelectedIndex)
            {
                baselineDropDown.selectedIndex = nextSelectedIndex;
            }
        }

        private void RefreshOutputRootDropDown(ExperimentBatchPlan plan)
        {
            string selected = plan == null ? null : plan.OutputRoot;
            var labels = new List<string>();
            outputRoots.Clear();

            IList<ExperimentOutputRootOption> options = viewState.OutputRoots;
            if (options != null)
            {
                for (int i = 0; i < options.Count; ++i)
                {
                    ExperimentOutputRootOption option = options[i];
                    if (option == null || string.IsNullOrEmpty(option.OutputRoot))
                    {
                        continue;
                    }

                    outputRoots.Add(option.OutputRoot);
                    labels.Add(string.IsNullOrEmpty(option.DisplayName) ? option.OutputRoot : option.DisplayName);
                }
            }

            int selectedIndex = IndexOfOrdinal(outputRoots, selected);
            if (!string.IsNullOrEmpty(selected) && selectedIndex < 0)
            {
                outputRoots.Add(selected);
                labels.Add(selected);
                selectedIndex = outputRoots.Count - 1;
            }

            if (labels.Count == 0)
            {
                labels.Add("<no output root selected>");
                outputRoots.Add(null);
                selectedIndex = 0;
            }

            string signature = BuildChoiceSignature(labels, outputRoots);
            if (!string.Equals(signature, lastOutputRootOptionsSignature, StringComparison.Ordinal))
            {
                outputRootDropDown.items = labels.ToArray();
                lastOutputRootOptionsSignature = signature;
            }

            int nextSelectedIndex = selectedIndex < 0 ? 0 : selectedIndex;
            if (outputRootDropDown.selectedIndex != nextSelectedIndex)
            {
                outputRootDropDown.selectedIndex = nextSelectedIndex;
            }
        }

        private void RefreshScenarioList(ExperimentBatchPlan plan)
        {
            IList<ExperimentScenario> scenarios = plan == null ? null : plan.Scenarios;
            int count = scenarios == null ? 0 : scenarios.Count;
            if (count == 0)
            {
                selectedScenarioIndex = -1;
            }
            else if (selectedScenarioIndex < 0)
            {
                selectedScenarioIndex = 0;
            }
            else if (selectedScenarioIndex >= count)
            {
                selectedScenarioIndex = count - 1;
            }

            string signature = BuildScenarioListSignature(scenarios);
            if (!string.Equals(signature, lastScenarioListSignature, StringComparison.Ordinal))
            {
                EnsureScenarioButtons(count);
                for (int i = 0; i < scenarioButtons.Count; ++i)
                {
                    UIButton button = scenarioButtons[i];
                    if (i < count)
                    {
                        ExperimentScenario scenario = scenarios[i];
                        button.text = FormatScenarioRow(i, scenario);
                        button.tooltip = FormatScenarioTooltip(scenario);
                        button.isVisible = true;
                    }
                    else
                    {
                        button.isVisible = false;
                    }
                }

                lastScenarioListSignature = signature;
            }

            UpdateScenarioSelectionAppearance();
        }

        private void RefreshScenarioEditor(ExperimentBatchPlan plan, bool readOnly)
        {
            IList<ExperimentScenario> scenarios = plan == null ? null : plan.Scenarios;
            ExperimentScenario selected = scenarios != null
                && selectedScenarioIndex >= 0
                && selectedScenarioIndex < scenarios.Count
                    ? scenarios[selectedScenarioIndex]
                    : null;

            string signature = BuildEditorSignature(selected);
            bool editorHasFocus = scenarioNameField.hasFocus
                || runCountField.hasFocus
                || durationField.hasFocus
                || firstSeedField.hasFocus;
            if ((selectedScenarioIndex != lastEditorScenarioIndex
                    || !string.Equals(signature, lastEditorSignature, StringComparison.Ordinal))
                && !editorHasFocus)
            {
                PopulateScenarioEditor(selected);
                lastEditorScenarioIndex = selectedScenarioIndex;
                lastEditorSignature = signature;
            }

            bool hasSelection = selected != null;
            bool canEdit = hasSelection && !readOnly;
            SetEditable(scenarioNameField, canEdit);
            SetEditable(runCountField, canEdit);
            SetEditable(durationField, canEdit);
            SetEnabled(endModeDropDown, canEdit);
            SetEnabled(seedStrategyDropDown, canEdit);
            SetEditable(firstSeedField, canEdit);
            SetEnabled(saveScenarioButton, canEdit);
            SetEnabled(updateSettingsButton, canEdit);

            SetEnabled(addScenarioButton, !readOnly);
            SetEnabled(duplicateScenarioButton, hasSelection && !readOnly);
            SetEnabled(removeScenarioButton, hasSelection && !readOnly);
            SetEnabled(moveUpButton, hasSelection && !readOnly && selectedScenarioIndex > 0);
            SetEnabled(moveDownButton, hasSelection && !readOnly && selectedScenarioIndex + 1 < (scenarios == null ? 0 : scenarios.Count));
        }

        private void RefreshExecutionControls()
        {
            int totalRuns = Math.Max(0, viewState.TotalRuns);
            int completedRuns = Math.Max(0, Math.Min(viewState.CompletedRuns, totalRuns));
            float fraction = totalRuns == 0 ? 0f : (float)completedRuns / totalRuns;
            progressFill.width = progressTrack.width * Mathf.Clamp01(fraction);
            progressLabel.text = string.Format(
                CultureInfo.CurrentCulture,
                "{0} / {1} runs complete ({2:0}%)",
                completedRuns,
                totalRuns,
                fraction * 100f);

            var stateText = new StringBuilder();
            stateText.Append("State: ").Append(viewState.ExecutionState);
            if (!string.IsNullOrEmpty(viewState.StatusText))
            {
                stateText.Append(" — ").Append(viewState.StatusText);
            }

            stateLabel.text = stateText.ToString();

            var details = new StringBuilder();
            if (viewState.CurrentScenarioNumber > 0)
            {
                details.AppendFormat(
                    CultureInfo.CurrentCulture,
                    "Scenario {0}/{1}, run {2}/{3}; seed {4}",
                    viewState.CurrentScenarioNumber,
                    Math.Max(viewState.ScenarioCount, viewState.CurrentScenarioNumber),
                    viewState.CurrentRunNumber,
                    Math.Max(viewState.CurrentScenarioRunCount, viewState.CurrentRunNumber),
                    viewState.CurrentSeed);
            }

            if (viewState.TargetSimulationDays > 0d)
            {
                if (details.Length > 0)
                {
                    details.Append("\n");
                }

                details.AppendFormat(
                    CultureInfo.CurrentCulture,
                    "Simulated duration: {0:0.###}/{1:0.###} days",
                    Math.Max(0d, viewState.ElapsedSimulationDays),
                    viewState.TargetSimulationDays);
            }

            if (!string.IsNullOrEmpty(viewState.LastCompletedRun))
            {
                if (details.Length > 0)
                {
                    details.Append("; ");
                }

                details.Append("last committed: ").Append(viewState.LastCompletedRun);
            }

            detailLabel.text = details.ToString();

            SetEnabled(startButton, viewState.CanStart);
            SetEnabled(pauseButton, viewState.CanPause);
            SetEnabled(resumeButton, viewState.CanResume);
            SetEnabled(abortButton, viewState.CanAbort);
            SetEnabled(abortInPlaceButton, viewState.CanAbort);
            SetEnabled(retryLoadButton, viewState.CanRetryBaselineLoad);
            SetEnabled(retryExportButton, viewState.CanRetryExport);
            SetEnabled(retryPreparationButton, viewState.CanRetryPreparation);
            retryLoadButton.isVisible = viewState.CanRetryBaselineLoad;
            retryExportButton.isVisible = viewState.CanRetryExport;
            retryPreparationButton.isVisible = viewState.CanRetryPreparation;
        }

        private void PopulateScenarioEditor(ExperimentScenario scenario)
        {
            if (scenario == null)
            {
                scenarioNameField.text = string.Empty;
                runCountField.text = string.Empty;
                durationField.text = string.Empty;
                endModeDropDown.selectedIndex = 0;
                seedStrategyDropDown.selectedIndex = 0;
                firstSeedField.text = string.Empty;
                return;
            }

            scenarioNameField.text = scenario.Name ?? string.Empty;
            runCountField.text = scenario.RunCount.ToString(CultureInfo.CurrentCulture);
            durationField.text = scenario.DurationDays.ToString("0.###", CultureInfo.CurrentCulture);
            endModeDropDown.selectedIndex = scenario.EndMode == ExperimentEndMode.DurationOrExtinction ? 1 : 0;
            seedStrategyDropDown.selectedIndex = scenario.SeedStrategy == ExperimentSeedStrategy.Sequential ? 1 : 0;
            firstSeedField.text = scenario.FirstSeed.ToString(CultureInfo.CurrentCulture);
        }

        private void EnsureScenarioButtons(int count)
        {
            while (scenarioButtons.Count < count)
            {
                int index = scenarioButtons.Count;
                UIButton button = CreateButton(
                    scenarioList,
                    4f,
                    4f + (index * ScenarioRowPitch),
                    scenarioList.width - 8f,
                    string.Empty,
                    () => SelectScenario(index));
                button.height = ScenarioRowHeight;
                button.textHorizontalAlignment = UIHorizontalAlignment.Left;
                button.textPadding = new RectOffset(8, 5, 4, 2);
                scenarioButtons.Add(button);
            }
        }

        private void SelectScenario(int index)
        {
            if (viewState == null || viewState.Plan == null || viewState.Plan.Scenarios == null)
            {
                return;
            }

            if (index < 0 || index >= viewState.Plan.Scenarios.Count)
            {
                return;
            }

            selectedScenarioIndex = index;
            lastEditorScenarioIndex = -2;
            lastEditorSignature = null;
            suppressEvents = true;
            try
            {
                PopulateScenarioEditor(viewState.Plan.Scenarios[index]);
                lastEditorScenarioIndex = index;
                lastEditorSignature = BuildEditorSignature(viewState.Plan.Scenarios[index]);
                UpdateScenarioSelectionAppearance();
                EnsureScenarioVisible(index);
            }
            finally
            {
                suppressEvents = false;
            }

            RefreshScenarioEditor(viewState.Plan, viewState.IsPlanReadOnly);
        }

        private void EnsureScenarioVisible(int index)
        {
            float rowTop = 4f + (index * ScenarioRowPitch);
            float rowBottom = rowTop + ScenarioRowHeight;
            float visibleTop = scenarioList.scrollPosition.y;
            float visibleBottom = visibleTop + scenarioList.height;
            if (rowTop < visibleTop)
            {
                scenarioList.scrollPosition = new Vector2(0f, rowTop);
            }
            else if (rowBottom > visibleBottom)
            {
                scenarioList.scrollPosition = new Vector2(0f, rowBottom - scenarioList.height);
            }
        }

        private void UpdateScenarioSelectionAppearance()
        {
            for (int i = 0; i < scenarioButtons.Count; ++i)
            {
                bool selected = i == selectedScenarioIndex;
                scenarioButtons[i].normalBgSprite = selected ? "ButtonMenuPressed" : "ButtonMenu";
                scenarioButtons[i].color = selected
                    ? new Color32(100, 155, 210, 255)
                    : new Color32(255, 255, 255, 255);
            }
        }

        private void CommitBatchName()
        {
            if (suppressEvents || viewState == null || viewState.IsPlanReadOnly)
            {
                return;
            }

            string requested = (batchNameField.text ?? string.Empty).Trim();
            string current = viewState.Plan == null ? null : viewState.Plan.BatchName;
            if (string.Equals(requested, current ?? string.Empty, StringComparison.Ordinal))
            {
                return;
            }

            RunAction(() => controller.SetBatchName(requested));
        }

        private void CommitOutputFolderName()
        {
            if (suppressEvents || viewState == null || viewState.IsPlanReadOnly)
            {
                return;
            }

            string requested = (outputFolderNameField.text ?? string.Empty).Trim();
            string current = viewState.Plan == null ? null : viewState.Plan.OutputFolderName;
            if (string.Equals(requested, current ?? string.Empty, StringComparison.Ordinal))
            {
                return;
            }

            RunAction(() => controller.SetOutputFolderName(requested));
        }

        private void OnBaselineChanged(UIComponent component, int index)
        {
            if (suppressEvents || viewState == null || viewState.IsPlanReadOnly || index < 0 || index >= baselineIds.Count)
            {
                return;
            }

            string assetFullName = baselineIds[index];
            if (string.IsNullOrEmpty(assetFullName))
            {
                return;
            }

            string current = viewState.Plan == null || viewState.Plan.Baseline == null
                ? null
                : viewState.Plan.Baseline.AssetFullName;
            if (!string.Equals(assetFullName, current, StringComparison.Ordinal))
            {
                RunAction(() => controller.SelectBaseline(assetFullName));
            }
        }

        private void OnOutputRootChanged(UIComponent component, int index)
        {
            if (suppressEvents || viewState == null || viewState.IsPlanReadOnly || index < 0 || index >= outputRoots.Count)
            {
                return;
            }

            string outputRoot = outputRoots[index];
            if (string.IsNullOrEmpty(outputRoot))
            {
                return;
            }

            string current = viewState.Plan == null ? null : viewState.Plan.OutputRoot;
            if (!string.Equals(outputRoot, current, StringComparison.Ordinal))
            {
                RunAction(() => controller.SelectOutputRoot(outputRoot));
            }
        }

        private void SwitchOutputRoot()
        {
            if (viewState == null || viewState.IsPlanReadOnly || outputRoots.Count < 2)
            {
                ShowLocalError(outputRoots.Count < 2
                    ? "Only one writable output root is currently available."
                    : "The confirmed batch plan is immutable.");
                return;
            }

            int currentIndex = IndexOfOrdinal(
                outputRoots,
                viewState.Plan == null ? null : viewState.Plan.OutputRoot);
            int nextIndex = currentIndex < 0 ? 0 : (currentIndex + 1) % outputRoots.Count;
            string nextRoot = outputRoots[nextIndex];
            if (!string.IsNullOrEmpty(nextRoot))
            {
                RunAction(() => controller.SelectOutputRoot(nextRoot));
            }
        }

        private void OnSpeedChanged(UIComponent component, int index)
        {
            if (suppressEvents || viewState == null || viewState.IsPlanReadOnly)
            {
                return;
            }

            int safeIndex = ClampIndex(index, SpeedNames.Length);
            ExperimentSpeedMode requested = (ExperimentSpeedMode)safeIndex;
            if (viewState.Plan == null || requested != viewState.Plan.SpeedMode)
            {
                RunAction(() => controller.SetExecutionSpeed(requested));
            }
        }

        private void OnReturnToBaselineChanged(UIComponent component, bool value)
        {
            if (suppressEvents || viewState == null || viewState.IsPlanReadOnly)
            {
                return;
            }

            if (viewState.Plan == null || value != viewState.Plan.ReturnToBaseline)
            {
                RunAction(() => controller.SetReturnToBaseline(value));
            }
        }

        private void RefreshBaselines()
        {
            RunAction(controller.RefreshBaselineCatalog);
        }

        private void AddScenario()
        {
            int previousCount = GetScenarioCount();
            RunAction(controller.AddCurrentSettingsAsScenario);
            if (GetScenarioCount() > previousCount)
            {
                selectedScenarioIndex = GetScenarioCount() - 1;
                lastEditorScenarioIndex = -2;
                Refresh();
            }
        }

        private void DuplicateScenario()
        {
            if (!HasSelectedScenario())
            {
                return;
            }

            int duplicatedIndex = selectedScenarioIndex;
            int previousCount = GetScenarioCount();
            RunAction(() => controller.DuplicateScenario(duplicatedIndex));
            if (GetScenarioCount() > previousCount)
            {
                selectedScenarioIndex = Math.Min(duplicatedIndex + 1, GetScenarioCount() - 1);
                lastEditorScenarioIndex = -2;
                Refresh();
            }
        }

        private void RemoveScenario()
        {
            if (!HasSelectedScenario())
            {
                return;
            }

            int removedIndex = selectedScenarioIndex;
            if (RunAction(() => controller.RemoveScenario(removedIndex)))
            {
                selectedScenarioIndex = Math.Min(removedIndex, GetScenarioCount() - 1);
                lastEditorScenarioIndex = -2;
                Refresh();
            }
        }

        private void MoveScenarioUp()
        {
            MoveSelectedScenario(-1);
        }

        private void MoveScenarioDown()
        {
            MoveSelectedScenario(1);
        }

        private void MoveSelectedScenario(int direction)
        {
            if (!HasSelectedScenario())
            {
                return;
            }

            int oldIndex = selectedScenarioIndex;
            int newIndex = oldIndex + direction;
            if (newIndex < 0 || newIndex >= GetScenarioCount())
            {
                return;
            }

            if (RunAction(() => controller.MoveScenario(oldIndex, newIndex)))
            {
                selectedScenarioIndex = newIndex;
                lastEditorScenarioIndex = -2;
                Refresh();
            }
        }

        private void UpdateScenarioSettings()
        {
            if (HasSelectedScenario())
            {
                int index = selectedScenarioIndex;
                RunAction(() => controller.UpdateScenarioFromCurrentSettings(index));
            }
        }

        private void SaveScenario()
        {
            if (!HasSelectedScenario() || viewState.IsPlanReadOnly)
            {
                return;
            }

            string name = (scenarioNameField.text ?? string.Empty).Trim();
            int runCount;
            double durationDays;
            int firstSeed;
            if (name.Length == 0)
            {
                ShowLocalError("Scenario name is required.");
                return;
            }

            if (!TryParseInt(runCountField.text, out runCount) || runCount <= 0)
            {
                ShowLocalError("Runs must be a positive whole number.");
                return;
            }

            if (!TryParseDouble(durationField.text, out durationDays)
                || durationDays <= 0d
                || double.IsNaN(durationDays)
                || double.IsInfinity(durationDays))
            {
                ShowLocalError("Duration must be a finite, positive number of simulated days.");
                return;
            }

            if (!TryParseInt(firstSeedField.text, out firstSeed) || firstSeed < 0)
            {
                ShowLocalError("First seed must be a non-negative whole number.");
                return;
            }

            ExperimentEndMode endMode = endModeDropDown.selectedIndex == 1
                ? ExperimentEndMode.DurationOrExtinction
                : ExperimentEndMode.FixedDuration;
            ExperimentSeedStrategy seedStrategy = seedStrategyDropDown.selectedIndex == 0
                ? ExperimentSeedStrategy.Fixed
                : ExperimentSeedStrategy.Sequential;
            if (seedStrategy == ExperimentSeedStrategy.Sequential
                && (long)firstSeed + runCount - 1L > int.MaxValue)
            {
                ShowLocalError("The sequential seed range exceeds Int32.MaxValue.");
                return;
            }

            int index = selectedScenarioIndex;
            RunAction(() => controller.UpdateScenario(
                index,
                name,
                runCount,
                durationDays,
                endMode,
                seedStrategy,
                firstSeed));
        }

        private void StartBatch()
        {
            ExperimentBatchPanelPreflight preflight;
            try
            {
                preflight = controller.BuildPreflight();
            }
            catch (Exception exception)
            {
                ShowLocalError("Preflight failed: " + exception.Message);
                return;
            }

            if (preflight == null)
            {
                ShowLocalError("Preflight did not return a result.");
                return;
            }

            if (!preflight.IsValid)
            {
                ShowLocalError(string.IsNullOrEmpty(preflight.Error) ? "The batch plan did not pass preflight." : preflight.Error);
                return;
            }

            var message = new StringBuilder();
            message.Append(string.IsNullOrEmpty(preflight.Summary)
                ? "Start this experiment batch?"
                : preflight.Summary.Trim());
            if (!string.IsNullOrEmpty(preflight.Warning))
            {
                message.Append("\n\nWARNING: ").Append(preflight.Warning.Trim());
            }

            message.Append("\n\nThe exact baseline will be reloaded before every run. Unsaved changes in the current city may be lost. The selected baseline save itself will not be modified by TENUS.");
            ConfirmPanel.ShowModal("Start Experiment Batch", message.ToString(), (component, result) =>
            {
                if (result == 1)
                {
                    RunAction(controller.StartBatch);
                }
            });
        }

        private void PauseBatch()
        {
            RunAction(controller.PauseBatch);
        }

        private void ResumeBatch()
        {
            RunAction(controller.ResumeBatch);
        }

        private void ConfirmAbort()
        {
            ConfirmPanel.ShowModal(
                "Abort Experiment Batch",
                "Abort the batch after the runner reaches its safe abort point? Already committed runs are preserved.",
                (component, result) =>
                {
                    if (result == 1)
                    {
                        RunAction(controller.AbortBatch);
                    }
                });
        }

        private void ConfirmAbortInPlace()
        {
            ConfirmPanel.ShowModal(
                "Abort Experiment Batch In Place",
                "Abort without returning to the baseline? The current city will remain loaded; TENUS will clean up the run and restore the pre-batch configuration. Already committed runs are preserved.",
                (component, result) =>
                {
                    if (result == 1)
                    {
                        RunAction(controller.AbortBatchInPlace);
                    }
                });
        }

        private void RetryBaselineLoad()
        {
            RunAction(controller.RetryBaselineLoad);
        }

        private void RetryExport()
        {
            RunAction(controller.RetryExport);
        }

        private void RetryPreparation()
        {
            RunAction(controller.RetryPreparation);
        }

        private bool RunAction(Func<ExperimentBatchPanelActionResult> action)
        {
            if (action == null)
            {
                return false;
            }

            bool succeeded;
            try
            {
                ExperimentBatchPanelActionResult result = action();
                succeeded = result == null || result.Succeeded;
                localMessage = result == null ? null : result.Message;
                localError = !succeeded
                    ? (string.IsNullOrEmpty(result.Message) ? "The requested batch action failed." : result.Message)
                    : null;
            }
            catch (Exception exception)
            {
                succeeded = false;
                localMessage = null;
                localError = "The requested batch action failed: " + exception.Message;
            }

            Refresh();
            return succeeded;
        }

        private void ShowLocalError(string message)
        {
            localMessage = null;
            localError = message;
            RenderMessages();
        }

        private void RenderMessages()
        {
            if (errorLabel == null)
            {
                return;
            }

            string controllerError = viewState == null ? null : viewState.ErrorText;
            if (!string.IsNullOrEmpty(localError))
            {
                errorLabel.text = "Error: " + localError;
                errorLabel.textColor = new Color32(255, 145, 135, 255);
            }
            else if (!string.IsNullOrEmpty(controllerError))
            {
                errorLabel.text = "Error: " + controllerError;
                errorLabel.textColor = new Color32(255, 145, 135, 255);
            }
            else if (!string.IsNullOrEmpty(localMessage))
            {
                errorLabel.text = localMessage;
                errorLabel.textColor = new Color32(145, 220, 165, 255);
            }
            else
            {
                errorLabel.text = string.Empty;
            }
        }

        private int GetScenarioCount()
        {
            return viewState == null || viewState.Plan == null || viewState.Plan.Scenarios == null
                ? 0
                : viewState.Plan.Scenarios.Count;
        }

        private static int CountPlannedRuns(ExperimentBatchPlan plan)
        {
            if (plan == null || plan.Scenarios == null)
            {
                return 0;
            }

            long total = 0L;
            for (int i = 0; i < plan.Scenarios.Count; ++i)
            {
                ExperimentScenario scenario = plan.Scenarios[i];
                if (scenario != null && scenario.RunCount > 0)
                {
                    total += scenario.RunCount;
                    if (total >= int.MaxValue)
                    {
                        return int.MaxValue;
                    }
                }
            }

            return (int)total;
        }

        private bool HasSelectedScenario()
        {
            return selectedScenarioIndex >= 0 && selectedScenarioIndex < GetScenarioCount();
        }

        private static string BuildScenarioListSignature(IList<ExperimentScenario> scenarios)
        {
            if (scenarios == null || scenarios.Count == 0)
            {
                return "0";
            }

            var signature = new StringBuilder();
            signature.Append(scenarios.Count);
            for (int i = 0; i < scenarios.Count; ++i)
            {
                ExperimentScenario scenario = scenarios[i];
                signature.Append('|').Append(i).Append(':');
                if (scenario != null)
                {
                    signature.Append(scenario.ScenarioId)
                        .Append(':').Append(scenario.Name)
                        .Append(':').Append(scenario.RunCount)
                        .Append(':').Append(scenario.DurationDays.ToString("R", CultureInfo.InvariantCulture))
                        .Append(':').Append((int)scenario.EndMode)
                        .Append(':').Append((int)scenario.SeedStrategy)
                        .Append(':').Append(scenario.FirstSeed);
                }
            }

            return signature.ToString();
        }

        private static string BuildChoiceSignature(IList<string> labels, IList<string> values)
        {
            var signature = new StringBuilder();
            signature.Append(labels == null ? 0 : labels.Count);
            if (labels == null)
            {
                return signature.ToString();
            }

            for (int i = 0; i < labels.Count; ++i)
            {
                signature.Append('|').Append(labels[i]).Append(':');
                if (values != null && i < values.Count)
                {
                    signature.Append(values[i]);
                }
            }

            return signature.ToString();
        }

        private static string BuildEditorSignature(ExperimentScenario scenario)
        {
            if (scenario == null)
            {
                return string.Empty;
            }

            return string.Concat(
                scenario.ScenarioId,
                "|",
                scenario.Name,
                "|",
                scenario.RunCount.ToString(CultureInfo.InvariantCulture),
                "|",
                scenario.DurationDays.ToString("R", CultureInfo.InvariantCulture),
                "|",
                ((int)scenario.EndMode).ToString(CultureInfo.InvariantCulture),
                "|",
                ((int)scenario.SeedStrategy).ToString(CultureInfo.InvariantCulture),
                "|",
                scenario.FirstSeed.ToString(CultureInfo.InvariantCulture));
        }

        private static string FormatScenarioRow(int index, ExperimentScenario scenario)
        {
            if (scenario == null)
            {
                return string.Format(CultureInfo.CurrentCulture, "  {0}. <invalid scenario>", index + 1);
            }

            return string.Format(
                CultureInfo.CurrentCulture,
                "  {0}. {1}  ·  {2} run{3}",
                index + 1,
                string.IsNullOrEmpty(scenario.Name) ? "<unnamed>" : scenario.Name,
                scenario.RunCount,
                scenario.RunCount == 1 ? string.Empty : "s");
        }

        private static string FormatScenarioTooltip(ExperimentScenario scenario)
        {
            if (scenario == null)
            {
                return "This scenario entry is invalid.";
            }

            return string.Format(
                CultureInfo.CurrentCulture,
                "{0:0.###} days; {1}; {2} from {3}",
                scenario.DurationDays,
                scenario.EndMode == ExperimentEndMode.DurationOrExtinction
                    ? "duration or extinction"
                    : "fixed duration",
                scenario.SeedStrategy == ExperimentSeedStrategy.Sequential
                    ? "sequential seeds"
                    : "fixed seed",
                scenario.FirstSeed);
        }

        private UILabel CreateSectionLabel(string text, float x, float y, float width)
        {
            UILabel label = panel.AddUIComponent<UILabel>();
            label.autoSize = false;
            label.width = width;
            label.height = 22f;
            label.relativePosition = new Vector3(x, y);
            label.text = text;
            label.textScale = 0.82f;
            label.textColor = new Color32(115, 195, 245, 255);
            label.textAlignment = UIHorizontalAlignment.Left;
            return label;
        }

        private UILabel CreateFieldLabel(string text, float x, float y, float width)
        {
            UILabel label = panel.AddUIComponent<UILabel>();
            label.autoSize = false;
            label.width = width;
            label.height = 24f;
            label.relativePosition = new Vector3(x, y);
            label.text = text;
            label.textScale = 0.74f;
            label.textColor = new Color32(225, 230, 235, 255);
            label.textAlignment = UIHorizontalAlignment.Left;
            label.verticalAlignment = UIVerticalAlignment.Middle;
            return label;
        }

        private static UIButton CreateButton(UIComponent parent, float x, float y, float width, string text, Action clicked)
        {
            UIButton button = parent.AddUIComponent<UIButton>();
            button.autoSize = false;
            button.width = width;
            button.height = 28f;
            button.relativePosition = new Vector3(x, y);
            button.text = text;
            button.textScale = 0.72f;
            button.textColor = new Color32(255, 255, 255, 255);
            button.disabledTextColor = new Color32(145, 150, 155, 255);
            button.normalBgSprite = "ButtonMenu";
            button.hoveredBgSprite = "ButtonMenuHovered";
            button.pressedBgSprite = "ButtonMenuPressed";
            button.disabledBgSprite = "ButtonMenu";
            button.textHorizontalAlignment = UIHorizontalAlignment.Center;
            button.textVerticalAlignment = UIVerticalAlignment.Middle;
            if (clicked != null)
            {
                button.eventClicked += (component, parameter) => clicked();
            }

            return button;
        }

        private static UITextField CreateTextField(UIComponent parent, float x, float y, float width)
        {
            UITextField field = parent.AddUIComponent<UITextField>();
            field.autoSize = false;
            field.width = width;
            field.height = 28f;
            field.relativePosition = new Vector3(x, y);
            field.textScale = 0.72f;
            field.textColor = new Color32(255, 255, 255, 255);
            field.disabledTextColor = new Color32(155, 160, 165, 255);
            field.color = new Color32(60, 92, 140, 255);
            field.horizontalAlignment = UIHorizontalAlignment.Left;
            field.verticalAlignment = UIVerticalAlignment.Middle;
            field.padding = new RectOffset(7, 7, 4, 4);
            field.normalBgSprite = "TextFieldPanel";
            field.hoveredBgSprite = "TextFieldPanelHovered";
            field.focusedBgSprite = "TextFieldPanelHovered";
            field.disabledBgSprite = "TextFieldPanel";
            field.selectionSprite = "EmptySprite";
            field.builtinKeyNavigation = true;
            field.isInteractive = true;
            return field;
        }

        private static UIDropDown CreateDropDown(UIComponent parent, float x, float y, float width)
        {
            UIDropDown dropDown = parent.AddUIComponent<UIDropDown>();
            dropDown.autoSize = false;
            dropDown.isInteractive = true;
            dropDown.width = width;
            dropDown.height = 28f;
            dropDown.relativePosition = new Vector3(x, y);
            dropDown.textScale = 0.72f;
            dropDown.textColor = new Color32(255, 255, 255, 255);
            dropDown.disabledTextColor = new Color32(155, 160, 165, 255);
            dropDown.normalBgSprite = "TextFieldPanel";
            dropDown.hoveredBgSprite = "TextFieldPanelHovered";
            dropDown.focusedBgSprite = "TextFieldPanelHovered";
            dropDown.disabledBgSprite = "TextFieldPanel";
            dropDown.listBackground = "GenericPanel";
            dropDown.itemHover = "ListItemHover";
            dropDown.itemHighlight = "ListItemHighlight";
            dropDown.itemHeight = 26;
            dropDown.listWidth = (int)width;
            dropDown.listHeight = 260;
            dropDown.popupColor = new Color32(45, 55, 68, 255);
            dropDown.horizontalAlignment = UIHorizontalAlignment.Left;
            dropDown.verticalAlignment = UIVerticalAlignment.Middle;
            return dropDown;
        }

        private static UICheckBox CreateCheckBox(UIComponent parent, float x, float y, float width, string text)
        {
            UICheckBox checkBox = parent.AddUIComponent<UICheckBox>();
            checkBox.autoSize = false;
            checkBox.width = width;
            checkBox.height = 24f;
            checkBox.relativePosition = new Vector3(x, y);
            checkBox.isInteractive = true;

            UISprite box = checkBox.AddUIComponent<UISprite>();
            box.autoSize = false;
            box.width = 20f;
            box.height = 20f;
            box.relativePosition = new Vector3(0f, 2f);
            box.spriteName = "check-unchecked";

            UISprite check = box.AddUIComponent<UISprite>();
            check.autoSize = false;
            check.width = 20f;
            check.height = 20f;
            check.relativePosition = Vector3.zero;
            check.spriteName = "check-checked";
            checkBox.checkedBoxObject = check;

            UILabel label = checkBox.AddUIComponent<UILabel>();
            label.autoSize = false;
            label.width = width - 28f;
            label.height = 24f;
            label.relativePosition = new Vector3(28f, 0f);
            label.text = text;
            label.textScale = 0.74f;
            label.textColor = new Color32(225, 230, 235, 255);
            label.verticalAlignment = UIVerticalAlignment.Middle;
            checkBox.label = label;
            return checkBox;
        }

        private static void SetEditable(UITextField field, bool enabled)
        {
            field.readOnly = !enabled;
            SetEnabled(field, enabled);
        }

        private static void SetEnabled(UIComponent component, bool enabled)
        {
            component.isEnabled = enabled;
            component.opacity = enabled ? 1f : 0.55f;
        }

        private static int ClampIndex(int index, int count)
        {
            if (count <= 0 || index < 0)
            {
                return 0;
            }

            return index >= count ? count - 1 : index;
        }

        private static int IndexOfOrdinal(IList<string> values, string target)
        {
            if (values == null || string.IsNullOrEmpty(target))
            {
                return -1;
            }

            for (int i = 0; i < values.Count; ++i)
            {
                if (string.Equals(values[i], target, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool TryParseInt(string text, out int value)
        {
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out value)
                || int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryParseDouble(string text, out double value)
        {
            return double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
                || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private void PositionPanel(UIView view)
        {
            float x = Mathf.Max(PanelMargin, (view.fixedWidth - PanelWidth) / 2f);
            float maxY = view.fixedHeight - PanelHeight - PanelMargin;
            float y = Mathf.Max(PanelMargin, Mathf.Min(60f, maxY));
            panel.relativePosition = new Vector3(x, y);
        }

        private sealed class ExperimentBatchPanelUpdateBehavior : MonoBehaviour
        {
            private float elapsed;

            public ExperimentBatchPanel Owner { get; set; }

            private void Update()
            {
                elapsed += Time.unscaledDeltaTime;
                if (elapsed < 0.5f)
                {
                    return;
                }

                elapsed = 0f;
                if (Owner != null && Owner.IsVisible)
                {
                    Owner.Refresh();
                }
            }
        }
    }
}
