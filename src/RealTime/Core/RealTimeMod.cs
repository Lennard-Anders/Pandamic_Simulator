// <copyright file="RealTimeMod.cs" company="dymanoid">Copyright (c) dymanoid. All rights reserved.</copyright>

namespace RealTime.Core
{
    using System;
    using System.Linq;
    using System.Reflection;
    using ColossalFramework;
    using ColossalFramework.Globalization;
    using ColossalFramework.UI;
    using ICities;
    using RealTime.Config;
    using RealTime.Experiments;
    using RealTime.Localization;
    using RealTime.UI;
    using SkyTools.Configuration;
    using SkyTools.Localization;
    using SkyTools.Tools;
    using SkyTools.UI;
    using UnityEngine;

    /// <summary>The main class of the Real Time mod.</summary>
    public sealed class RealTimeMod : LoadingExtensionBase, IUserMod
    {
        private const string MissingModPathMessage = "Real Time could not resolve its installation path and cannot start.";

        private readonly string modVersion = GitVersion.GetAssemblyVersion(typeof(RealTimeMod).Assembly);
        private string modPath;

        private ConfigurationProvider<RealTimeConfig> configProvider;
        private RealTimeCore core;
        private ConfigUI configUI;
        private LocalizationProvider localizationProvider;
        private ExperimentBatchService experimentBatchService;
        private GameObject experimentBatchHostObject;

#if BENCHMARK
        /// <summary>
        /// Initializes a new instance of the <see cref="RealTimeMod"/> class.
        /// </summary>
        public RealTimeMod()
        {
            RealTimeBenchmark.Setup();
        }
#endif

        /// <summary>Gets the name of this mod.</summary>
        public string Name => "Real Time";

        /// <summary>Gets the description string of this mod.</summary>
        public string Description => "Adjusts the time flow and the Cims behavior to make them more real. Version: " + modVersion;

        /// <summary>Called when this mod is enabled.</summary>
        public void OnEnabled()
        {
            Log.SetupDebug(Name, LogCategory.Generic, LogCategory.Simulation);

            string currentModPath = GetModPath();
            if (string.IsNullOrEmpty(currentModPath))
            {
                Log.Warning($"The 'Real Time' mod version {modVersion} could not resolve its installation path.");
                return;
            }

            Log.Info("The 'Real Time' mod has been enabled, version: " + modVersion);
            Log.Info("The 'Real Time' mod path: " + currentModPath);
            configProvider = new ConfigurationProvider<RealTimeConfig>(RealTimeConfig.StorageId, Name, () => new RealTimeConfig(latestVersion: true));
            configProvider.LoadDefaultConfiguration();
            localizationProvider = new LocalizationProvider(Name, currentModPath);
            EnsureExperimentBatchHost(currentModPath);
        }

        /// <summary>Called when this mod is disabled.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Must be instance method due to C:S API")]
        public void OnDisabled()
        {
            CloseConfigUI();
            experimentBatchService?.Dispose();
            experimentBatchService = null;
            if (experimentBatchHostObject != null)
            {
                UnityEngine.Object.Destroy(experimentBatchHostObject);
                experimentBatchHostObject = null;
            }

            if (core != null)
            {
                core.Stop();
                core = null;
            }

            if (!ExperimentControlGate.IsControlLocked && configProvider?.IsDefault == true)
            {
                configProvider.SaveDefaultConfiguration();
            }

            Log.Info("The 'Real Time' mod has been disabled.");
        }

        /// <summary>Called when this mod's settings page needs to be created.</summary>
        /// <param name="helper">
        /// An <see cref="UIHelperBase"/> reference that can be used to construct the mod's settings page.
        /// </param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Must be instance method due to C:S API")]
        public void OnSettingsUI(UIHelperBase helper)
        {
            if (string.IsNullOrEmpty(GetModPath()))
            {
                helper?.AddGroup(MissingModPathMessage);
                return;
            }

            if (helper == null || configProvider == null)
            {
                return;
            }

            if (ExperimentControlGate.IsControlLocked)
            {
                CloseConfigUI();
                helper.AddGroup("Controlled by active experiment batch. Simulation-affecting settings are read-only until the batch completes or is aborted.");
                return;
            }

            if (configProvider.Configuration == null)
            {
                Log.Warning("The 'Real Time' mod wants to display the configuration page, but the configuration is unexpectedly missing.");
                configProvider.LoadDefaultConfiguration();
            }

            // Validate and repair configuration to ensure core Real Time features are active
            if (configProvider?.Configuration != null)
            {
                configProvider.Configuration.Validate();
            }

            IViewItemFactory itemFactory = new CitiesViewItemFactory(helper);
            CloseConfigUI();
            Compatibility compatibility = localizationProvider == null ? null : Compatibility.Create(localizationProvider);
            configUI = ConfigUI.Create(configProvider, itemFactory, compatibility);
            ApplyLanguage();
            CompactSettingsTabs(helper);
        }

        /// <summary>
        /// Called when a game level is loaded. If applicable, activates the Real Time mod for the loaded level.
        /// </summary>
        /// <param name="mode">The <see cref="LoadMode"/> a game level is loaded in.</param>
        public override void OnLevelLoaded(LoadMode mode)
        {
            string currentModPath = GetModPath();
            if (string.IsNullOrEmpty(currentModPath))
            {
                MessageBox.Show("Sorry", MissingModPathMessage);
                return;
            }

            switch (mode)
            {
                case LoadMode.LoadGame:
                case LoadMode.NewGame:
                case LoadMode.LoadScenario:
                case LoadMode.NewGameFromScenario:
                    break;

                default:
                    return;
            }

            Log.Info($"The 'Real Time' mod starts, game mode {mode}.");
            core?.Stop();

            if (configProvider == null)
            {
                configProvider = new ConfigurationProvider<RealTimeConfig>(RealTimeConfig.StorageId, Name, () => new RealTimeConfig(latestVersion: true));
                configProvider.LoadDefaultConfiguration();
            }

            // Validate and repair configuration to ensure core Real Time features are active
            if (configProvider?.Configuration != null)
            {
                configProvider.Configuration.Validate();
            }

            if (localizationProvider == null)
            {
                localizationProvider = new LocalizationProvider(Name, currentModPath);
            }

            EnsureExperimentBatchHost(currentModPath);

            var compatibility = Compatibility.Create(localizationProvider);

            bool isNewGame = mode == LoadMode.NewGame || mode == LoadMode.NewGameFromScenario;
            core = RealTimeCore.Run(configProvider, currentModPath, localizationProvider, isNewGame, compatibility);
            if (core == null)
            {
                Log.Warning("Showing a warning message to user because the mod isn't working");
                MessageBox.Show(
                    localizationProvider.Translate(TranslationKeys.Warning),
                    localizationProvider.Translate(TranslationKeys.ModNotWorkingMessage));
            }
            else
            {
                CheckCompatibility(compatibility);
            }

            if (!ExperimentControlGate.IsControlLocked)
            {
                configProvider.SaveDefaultConfiguration();
            }

            experimentBatchService?.AttachLevel(core);
        }

        /// <summary>
        /// Called when a game level is about to be unloaded. If the Real Time mod was activated for this level,
        /// deactivates the mod for this level.
        /// </summary>
        public override void OnLevelUnloading()
        {
            experimentBatchService?.OnLevelUnloading(core);
            if (core != null)
            {
                Log.Info("The 'Real Time' mod stops.");
                core.Stop();
                core = null;
            }

            configProvider?.LoadDefaultConfiguration();
        }

        private void EnsureExperimentBatchHost(string currentModPath)
        {
            if (experimentBatchService != null)
            {
                return;
            }

            experimentBatchService = new ExperimentBatchService(currentModPath, modVersion);
            experimentBatchHostObject = new GameObject("TENUS Experiment Batch Host");
            UnityEngine.Object.DontDestroyOnLoad(experimentBatchHostObject);
            ExperimentBatchHost host = experimentBatchHostObject.AddComponent<ExperimentBatchHost>();
            host.Initialize(experimentBatchService);
        }

        private string GetModPath()
        {
            string resolvedModPath = ModPaths.GetModRoot();
            if (string.IsNullOrEmpty(resolvedModPath))
            {
                resolvedModPath = ModPaths.GetModRootFromUserModInstance(this);
            }

            if (!string.IsNullOrEmpty(resolvedModPath))
            {
                modPath = resolvedModPath;
            }

            return modPath ?? string.Empty;
        }

        private void CheckCompatibility(Compatibility compatibility)
        {
            if (core == null || configProvider?.Configuration == null)
            {
                return;
            }

            string message = null;
            bool incompatibilitiesDetected = configProvider.Configuration.ShowIncompatibilityNotifications
                && compatibility.AreAnyIncompatibleModsActive(out message);

            if (core.IsRestrictedMode)
            {
                message = (message ?? string.Empty) + localizationProvider.Translate(TranslationKeys.RestrictedMode);
            }

            if (incompatibilitiesDetected || core.IsRestrictedMode)
            {
                Notification.Notify(Name + " - " + localizationProvider.Translate(TranslationKeys.Warning), message);
            }
        }

        private void ApplyLanguage()
        {
            if (!SingletonLite<LocaleManager>.exists)
            {
                return;
            }

            // Try to load the current game language
            string currentLanguage = LocaleManager.instance.language;
            bool translationLoaded = localizationProvider.LoadTranslation(currentLanguage);

            // Fall back to English if the current language fails to load
            if (!translationLoaded)
            {
                Log.Warning($"Real Time: Failed to load localization for language '{currentLanguage}', falling back to English.");
                translationLoaded = localizationProvider.LoadTranslation("en");
                if (!translationLoaded)
                {
                    Log.Warning("Real Time: Failed to load English localization as fallback. UI may show empty text.");
                    return;
                }
            }

            localizationProvider.SetEnglishUSFormatsState(configProvider.Configuration.UseEnglishUSFormats);
            core?.Translate(localizationProvider);
            configUI?.Translate(localizationProvider);
        }

        private void CloseConfigUI()
        {
            if (configUI != null)
            {
                configUI.Close();
                configUI = null;
            }
        }

        private static void CompactSettingsTabs(UIHelperBase helper)
        {
            try
            {
                UIComponent helperRoot = ResolveHelperRoot(helper);
                if (helperRoot == null)
                {
                    return;
                }

                // Only process tab strips within the mod's settings root, not global UI
                UITabstrip[] tabStrips = helperRoot.GetComponentsInChildren<UITabstrip>() ?? new UITabstrip[0];
                if (tabStrips.Length == 0)
                {
                    return;
                }

                var target = tabStrips
                    .Select(t => new { Strip = t, Buttons = GetTabButtons(t) })
                    .Where(x => x.Buttons.Length >= 4)
                    .OrderByDescending(x => x.Buttons.Length)
                    .ThenByDescending(x => x.Strip.width)
                    .FirstOrDefault();

                if (target == null)
                {
                    return;
                }

                CompactTabStrip(target.Strip, target.Buttons);
                EnsureUIReadability(helperRoot);
            }
            catch (Exception ex)
            {
                Log.Warning("The 'Real Time' mod could not compact settings tabs, error message: " + ex);
            }
        }


        private static UIComponent ResolveHelperRoot(UIHelperBase helper)
        {
            object current = helper;
            for (int i = 0; i < 6 && current != null; i++)
            {
                var component = current as UIComponent;
                if (component != null)
                {
                    return component;
                }

                Type type = current.GetType();
                FieldInfo selfField = type.GetField("m_Self", BindingFlags.Instance | BindingFlags.NonPublic);
                if (selfField != null)
                {
                    current = selfField.GetValue(current);
                    continue;
                }

                PropertyInfo selfProperty = type.GetProperty("self", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (selfProperty != null)
                {
                    current = selfProperty.GetValue(current, null);
                    continue;
                }

                break;
            }

            return null;
        }

        private static void EnsureUIReadability(UIComponent modSettingsRoot)
        {
            if (modSettingsRoot == null)
            {
                return;
            }

            try
            {
                // Ensure all labels are visible with proper colors and scaling
                var labels = modSettingsRoot.GetComponentsInChildren<UILabel>();
                foreach (var label in labels)
                {
                    if (label != null)
                    {
                        // Set text color to white if it's black or transparent
                        if (label.textColor.a < 0.1f || (label.textColor.r < 0.1f && label.textColor.g < 0.1f && label.textColor.b < 0.1f))
                        {
                            label.textColor = new Color32(255, 255, 255, 255);
                        }

                        // Ensure disabled color is visible
                        label.disabledTextColor = new Color32(128, 128, 128, 255);

                        // Ensure text scale is readable
                        if (label.textScale < 0.7f)
                        {
                            label.textScale = 0.8f;
                        }

                        // Enable word wrap for long text
                        label.wordWrap = true;
                    }
                }

                // Ensure all buttons have visible text
                var buttons = modSettingsRoot.GetComponentsInChildren<UIButton>();
                foreach (var button in buttons)
                {
                    if (button != null)
                    {
                        // Set text color to white if it's black or transparent
                        if (button.textColor.a < 0.1f || (button.textColor.r < 0.1f && button.textColor.g < 0.1f && button.textColor.b < 0.1f))
                        {
                            button.textColor = new Color32(255, 255, 255, 255);
                        }

                        // Ensure disabled color is visible
                        button.disabledTextColor = new Color32(128, 128, 128, 255);

                        // Ensure text scale is readable
                        if (button.textScale < 0.7f)
                        {
                            button.textScale = 0.8f;
                        }

                        // Set reasonable text padding
                        if (button.textPadding == null || button.textPadding.top + button.textPadding.bottom == 0)
                        {
                            button.textPadding = new RectOffset(2, 2, 2, 2);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning("The 'Real Time' mod could not ensure UI readability, error message: " + ex);
            }
        }

        private static UIButton[] GetTabButtons(UITabstrip tabStrip)
        {
            if (tabStrip?.components == null)
            {
                return new UIButton[0];
            }

            return tabStrip.components
                .OfType<UIButton>()
                .Where(b => b != null)
                .OrderBy(b => b.relativePosition.x)
                .ToArray();
        }

        private static void CompactTabStrip(UITabstrip tabStrip, UIButton[] buttons)
        {
            if (tabStrip == null || buttons == null || buttons.Length < 2)
            {
                return;
            }

            float availableWidth = tabStrip.width;
            if (availableWidth < 10f && tabStrip.parent != null)
            {
                availableWidth = tabStrip.parent.width;
            }

            if (availableWidth < 10f)
            {
                return;
            }

            const float spacing = 2f;
            float buttonWidth = (availableWidth - ((buttons.Length - 1) * spacing)) / buttons.Length;
            buttonWidth = Mathf.Max(45f, buttonWidth);

            float textScale = buttonWidth < 70f ? 0.62f : buttonWidth < 90f ? 0.72f : 0.82f;
            float startX = buttons[0].relativePosition.x;

            for (int i = 0; i < buttons.Length; i++)
            {
                UIButton button = buttons[i];
                button.autoSize = false;
                button.width = buttonWidth;
                if (button.height < 26f)
                {
                    button.height = 26f;
                }

                button.textScale = textScale;
                button.wordWrap = false;
                button.textPadding = new RectOffset(2, 2, 2, 2);
                button.relativePosition = new Vector3(startX + (i * (buttonWidth + spacing)), button.relativePosition.y);
            }
        }
    }
}
