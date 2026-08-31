// <copyright file="ExperimentBuildIdentity.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.Experiments
{
    using System;
    using System.Reflection;

    /// <summary>Build-time Git identity embedded into the production assembly.</summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    internal sealed class TenusBuildIdentityAttribute : Attribute
    {
        public TenusBuildIdentityAttribute(string commitSha, string branchOrTag)
        {
            CommitSha = commitSha;
            BranchOrTag = branchOrTag;
        }

        public string CommitSha { get; }

        public string BranchOrTag { get; }
    }

    internal sealed class ExperimentBuildIdentity
    {
        private ExperimentBuildIdentity(string commitSha, string branchOrTag)
        {
            CommitSha = commitSha;
            BranchOrTag = branchOrTag;
        }

        public string CommitSha { get; }

        public string BranchOrTag { get; }

        public static ExperimentBuildIdentity Read(Assembly assembly)
        {
            if (assembly == null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            object[] values = assembly.GetCustomAttributes(typeof(TenusBuildIdentityAttribute), false);
            if (values.Length == 1)
            {
                var identity = (TenusBuildIdentityAttribute)values[0];
                return new ExperimentBuildIdentity(
                    Normalize(identity.CommitSha),
                    Normalize(identity.BranchOrTag));
            }

            return new ExperimentBuildIdentity("unavailable", "unavailable");
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrEmpty(value) ? "unavailable" : value.Trim();
        }
    }
}
