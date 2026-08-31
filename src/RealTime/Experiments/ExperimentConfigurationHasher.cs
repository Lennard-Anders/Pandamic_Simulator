// <copyright file="ExperimentConfigurationHasher.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.Experiments
{
    using System;
    using System.Globalization;
    using System.Reflection;
    using System.Security.Cryptography;
    using System.Text;

    /// <summary>Produces an order-independent identity of all scenario simulation inputs.</summary>
    public static class ExperimentConfigurationHasher
    {
        public const string AlgorithmName = "TENUS-CONFIG-v1/SHA-256";

        public static string Compute(ExperimentScenario scenario)
        {
            if (scenario == null)
            {
                throw new ArgumentNullException(nameof(scenario));
            }

            if (scenario.Settings == null)
            {
                throw new ArgumentException("The scenario settings snapshot is required.", nameof(scenario));
            }

            var canonical = new StringBuilder();
            canonical.Append("TENUS-CONFIG-v1\n");
            Append(canonical, "DurationDays", scenario.DurationDays);
            Append(canonical, "EndMode", scenario.EndMode);

            PropertyInfo[] properties = typeof(ExperimentScenarioSnapshot).GetProperties(BindingFlags.Instance | BindingFlags.Public);
            Array.Sort(properties, (left, right) => string.CompareOrdinal(left.Name, right.Name));
            for (int i = 0; i < properties.Length; ++i)
            {
                PropertyInfo property = properties[i];
                if (!property.CanRead || property.GetIndexParameters().Length != 0)
                {
                    continue;
                }

                Append(canonical, "Settings." + property.Name, property.GetValue(scenario.Settings, null));
            }

            byte[] bytes = new UTF8Encoding(false).GetBytes(canonical.ToString());
            using (SHA256 algorithm = SHA256.Create())
            {
                return ToHex(algorithm.ComputeHash(bytes));
            }
        }

        internal static string BuildCanonicalRepresentation(ExperimentScenario scenario)
        {
            if (scenario == null || scenario.Settings == null)
            {
                throw new ArgumentNullException(nameof(scenario));
            }

            var canonical = new StringBuilder();
            canonical.Append("TENUS-CONFIG-v1\n");
            Append(canonical, "DurationDays", scenario.DurationDays);
            Append(canonical, "EndMode", scenario.EndMode);
            PropertyInfo[] properties = typeof(ExperimentScenarioSnapshot).GetProperties(BindingFlags.Instance | BindingFlags.Public);
            Array.Sort(properties, (left, right) => string.CompareOrdinal(left.Name, right.Name));
            for (int i = 0; i < properties.Length; ++i)
            {
                PropertyInfo property = properties[i];
                if (property.CanRead && property.GetIndexParameters().Length == 0)
                {
                    Append(canonical, "Settings." + property.Name, property.GetValue(scenario.Settings, null));
                }
            }

            return canonical.ToString();
        }

        private static void Append(StringBuilder canonical, string name, object value)
        {
            canonical.Append(name.Length.ToString(CultureInfo.InvariantCulture));
            canonical.Append(':').Append(name).Append('=');
            if (value == null)
            {
                canonical.Append("null");
            }
            else if (value is string text)
            {
                canonical.Append("string:").Append(text.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(text);
            }
            else if (value is bool boolean)
            {
                canonical.Append("bool:").Append(boolean ? '1' : '0');
            }
            else if (value is float single)
            {
                canonical.Append("single:").Append(single.ToString("R", CultureInfo.InvariantCulture));
            }
            else if (value is double number)
            {
                canonical.Append("double:").Append(number.ToString("R", CultureInfo.InvariantCulture));
            }
            else if (value.GetType().IsEnum)
            {
                canonical.Append("enum:")
                    .Append(value.GetType().FullName)
                    .Append(':')
                    .Append(Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture));
            }
            else if (value is IFormattable formattable)
            {
                canonical.Append(value.GetType().FullName)
                    .Append(':')
                    .Append(formattable.ToString(null, CultureInfo.InvariantCulture));
            }
            else
            {
                throw new InvalidOperationException("Unsupported scenario configuration value type: " + value.GetType().FullName);
            }

            canonical.Append('\n');
        }

        private static string ToHex(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
