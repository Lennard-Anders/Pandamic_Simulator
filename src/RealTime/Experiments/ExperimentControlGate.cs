// <copyright file="ExperimentControlGate.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.Experiments
{
    using System;

    /// <summary>
    /// Process-local ownership gate used by manual UI and pandemic commands while a batch controls the simulation.
    /// </summary>
    public static class ExperimentControlGate
    {
        private static readonly object Sync = new object();
        private static string currentOwner;
        private static int leaseCount;

        public static bool IsControlLocked
        {
            get
            {
                lock (Sync)
                {
                    return leaseCount > 0;
                }
            }
        }

        public static string CurrentOwner
        {
            get
            {
                lock (Sync)
                {
                    return currentOwner;
                }
            }
        }

        public static IDisposable Acquire(string owner)
        {
            if (string.IsNullOrEmpty(owner))
            {
                throw new ArgumentException("A control owner is required.", "owner");
            }

            lock (Sync)
            {
                if (leaseCount > 0 && !string.Equals(currentOwner, owner, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Experiment control is already owned by " + currentOwner + ".");
                }

                currentOwner = owner;
                ++leaseCount;
                return new Lease(owner);
            }
        }

        private static void Release(string owner)
        {
            lock (Sync)
            {
                if (leaseCount == 0 || !string.Equals(currentOwner, owner, StringComparison.Ordinal))
                {
                    return;
                }

                --leaseCount;
                if (leaseCount == 0)
                {
                    currentOwner = null;
                }
            }
        }

        private sealed class Lease : IDisposable
        {
            private readonly string owner;
            private bool disposed;

            public Lease(string owner)
            {
                this.owner = owner;
            }

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                Release(owner);
            }
        }
    }
}
