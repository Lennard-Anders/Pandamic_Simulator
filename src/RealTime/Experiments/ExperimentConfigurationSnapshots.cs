// <copyright file="ExperimentConfigurationSnapshots.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.Experiments
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Reflection;
    using RealTime.Config;
    using RealTime.Pandemic;

    /// <summary>One stable property-level settings difference.</summary>
    public sealed class ExperimentSettingDifference
    {
        public string PropertyName { get; set; }

        public string LeftValue { get; set; }

        public string RightValue { get; set; }
    }

    /// <summary>
    /// Full configuration snapshot used only to restore the user's configuration after batch ownership ends.
    /// Unlike scenario settings, this intentionally includes every public writable <see cref="RealTimeConfig"/> property.
    /// </summary>
    public sealed class RealTimeConfigSnapshot
    {
        public RealTimeConfigSnapshot()
        {
            SchemaVersion = ExperimentSchema.CurrentVersion;
            Configuration = new RealTimeConfig();
        }

        public int SchemaVersion { get; set; }

        public RealTimeConfig Configuration { get; set; }

        public static RealTimeConfigSnapshot Capture(RealTimeConfig configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException("configuration");
            }

            return new RealTimeConfigSnapshot
            {
                Configuration = ExperimentConfigurationMapper.CloneConfiguration(configuration),
            };
        }

        public RealTimeConfigSnapshot Clone()
        {
            if (Configuration == null)
            {
                throw new InvalidOperationException("The full configuration snapshot has no configuration value.");
            }

            return new RealTimeConfigSnapshot
            {
                SchemaVersion = SchemaVersion,
                Configuration = ExperimentConfigurationMapper.CloneConfiguration(Configuration),
            };
        }

        public void ApplyTo(RealTimeConfig configuration)
        {
            if (SchemaVersion != ExperimentSchema.CurrentVersion)
            {
                throw new InvalidOperationException("The full configuration snapshot schema is unsupported.");
            }

            if (Configuration == null)
            {
                throw new InvalidOperationException("The full configuration snapshot has no configuration value.");
            }

            ExperimentConfigurationMapper.CopyConfiguration(Configuration, configuration);
        }
    }

    internal static class ExperimentConfigurationMapper
    {
        private static readonly PropertyInfo[] ScenarioProperties = GetScenarioProperties();
        private static readonly PropertyInfo[] ConfigurationProperties = GetConfigurationProperties();
        private static readonly string[] ExcludedConfigurationPropertyNames =
        {
            "ShowIncompatibilityNotifications",
            "StaticBaselineSaveAsDefault",
            "SwitchOffLightsAtNight",
            "SwitchOffLightsMaxHeight",
            "UseEnglishUSFormats",
            "Version",
        };

        public static ExperimentScenarioSnapshot CaptureScenario(RealTimeConfig configuration, bool initialLockdownEnabled)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException("configuration");
            }

            ExperimentScenarioSnapshot result = new ExperimentScenarioSnapshot
            {
                InitialLockdownEnabled = initialLockdownEnabled,
            };
            CopyMatchingProperties(configuration, result, ScenarioProperties);
            return result;
        }

        public static ExperimentScenarioSnapshot CloneScenario(ExperimentScenarioSnapshot source)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            ExperimentScenarioSnapshot result = new ExperimentScenarioSnapshot
            {
                SchemaVersion = source.SchemaVersion,
                InitialLockdownEnabled = source.InitialLockdownEnabled,
            };
            foreach (PropertyInfo property in ScenarioProperties)
            {
                property.SetValue(result, property.GetValue(source, null), null);
            }

            return result;
        }

        public static ExperimentScenarioSnapshot NormalizeScenario(ExperimentScenarioSnapshot source)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            RealTimeConfig normalizedConfiguration = new RealTimeConfig(true);
            ApplyScenario(source, normalizedConfiguration);
            normalizedConfiguration.Validate();
            PandemicManager.NormalizeRuntimeConfiguration(normalizedConfiguration);
            return CaptureScenario(normalizedConfiguration, source.InitialLockdownEnabled);
        }

        /// <summary>Returns future configuration properties not explicitly included or excluded.</summary>
        internal static IList<string> GetUncategorizedConfigurationProperties()
        {
            var scenario = new HashSet<string>(StringComparer.Ordinal);
            foreach (PropertyInfo property in ScenarioProperties)
            {
                scenario.Add(property.Name);
            }

            var excluded = new HashSet<string>(ExcludedConfigurationPropertyNames, StringComparer.Ordinal);
            var result = new List<string>();
            foreach (PropertyInfo property in ConfigurationProperties)
            {
                if (!scenario.Contains(property.Name) && !excluded.Contains(property.Name))
                {
                    result.Add(property.Name);
                }
            }

            return result;
        }

        public static void ApplyScenario(ExperimentScenarioSnapshot source, RealTimeConfig destination)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            if (destination == null)
            {
                throw new ArgumentNullException("destination");
            }

            if (source.SchemaVersion != ExperimentSchema.CurrentVersion)
            {
                throw new InvalidOperationException("The scenario settings schema is unsupported.");
            }

            foreach (PropertyInfo scenarioProperty in ScenarioProperties)
            {
                PropertyInfo destinationProperty = typeof(RealTimeConfig).GetProperty(scenarioProperty.Name);
                destinationProperty.SetValue(destination, scenarioProperty.GetValue(source, null), null);
            }
        }

        public static List<ExperimentSettingDifference> DiffScenario(
            ExperimentScenarioSnapshot left,
            ExperimentScenarioSnapshot right)
        {
            if (left == null)
            {
                throw new ArgumentNullException("left");
            }

            if (right == null)
            {
                throw new ArgumentNullException("right");
            }

            List<ExperimentSettingDifference> differences = new List<ExperimentSettingDifference>();
            AddDifference(differences, "InitialLockdownEnabled", left.InitialLockdownEnabled, right.InitialLockdownEnabled);
            foreach (PropertyInfo property in ScenarioProperties)
            {
                AddDifference(differences, property.Name, property.GetValue(left, null), property.GetValue(right, null));
            }

            return differences;
        }

        public static RealTimeConfig CloneConfiguration(RealTimeConfig source)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            RealTimeConfig result = new RealTimeConfig();
            CopyConfiguration(source, result);
            return result;
        }

        public static void CopyConfiguration(RealTimeConfig source, RealTimeConfig destination)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            if (destination == null)
            {
                throw new ArgumentNullException("destination");
            }

            foreach (PropertyInfo property in ConfigurationProperties)
            {
                property.SetValue(destination, property.GetValue(source, null), null);
            }
        }

        private static void CopyMatchingProperties(object source, object destination, IEnumerable<PropertyInfo> destinationProperties)
        {
            Type sourceType = source.GetType();
            foreach (PropertyInfo destinationProperty in destinationProperties)
            {
                PropertyInfo sourceProperty = sourceType.GetProperty(destinationProperty.Name);
                destinationProperty.SetValue(destination, sourceProperty.GetValue(source, null), null);
            }
        }

        private static PropertyInfo[] GetScenarioProperties()
        {
            List<PropertyInfo> properties = new List<PropertyInfo>();
            foreach (PropertyInfo property in typeof(ExperimentScenarioSnapshot).GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!property.CanRead || !property.CanWrite
                    || property.Name == "SchemaVersion"
                    || property.Name == "InitialLockdownEnabled")
                {
                    continue;
                }

                PropertyInfo configurationProperty = typeof(RealTimeConfig).GetProperty(property.Name);
                if (configurationProperty == null
                    || !configurationProperty.CanRead
                    || !configurationProperty.CanWrite
                    || configurationProperty.PropertyType != property.PropertyType)
                {
                    throw new InvalidOperationException("Scenario setting does not match RealTimeConfig: " + property.Name);
                }

                properties.Add(property);
            }

            properties.Sort(ComparePropertyNames);
            return properties.ToArray();
        }

        private static PropertyInfo[] GetConfigurationProperties()
        {
            List<PropertyInfo> properties = new List<PropertyInfo>();
            foreach (PropertyInfo property in typeof(RealTimeConfig).GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (property.CanRead && property.CanWrite && property.GetIndexParameters().Length == 0)
                {
                    properties.Add(property);
                }
            }

            properties.Sort(ComparePropertyNames);
            return properties.ToArray();
        }

        private static int ComparePropertyNames(PropertyInfo left, PropertyInfo right)
        {
            return string.Compare(left.Name, right.Name, StringComparison.Ordinal);
        }

        private static void AddDifference(
            ICollection<ExperimentSettingDifference> differences,
            string propertyName,
            object left,
            object right)
        {
            if (object.Equals(left, right))
            {
                return;
            }

            differences.Add(new ExperimentSettingDifference
            {
                PropertyName = propertyName,
                LeftValue = ToInvariantString(left),
                RightValue = ToInvariantString(right),
            });
        }

        private static string ToInvariantString(object value)
        {
            IFormattable formattable = value as IFormattable;
            return formattable == null
                ? (value == null ? null : value.ToString())
                : formattable.ToString(null, CultureInfo.InvariantCulture);
        }
    }
}
