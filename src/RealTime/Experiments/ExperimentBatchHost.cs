// <copyright file="ExperimentBatchHost.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.Experiments
{
    using UnityEngine;

    /// <summary>
    /// Application-lifetime main-thread pump for the experiment controller. It deliberately owns no
    /// level objects; those are attached and detached by <see cref="Core.RealTimeMod"/>.
    /// </summary>
    internal sealed class ExperimentBatchHost : MonoBehaviour
    {
        internal ExperimentBatchService Service { get; private set; }

        internal void Initialize(ExperimentBatchService service)
        {
            Service = service;
        }

        private void Update()
        {
            Service?.Tick();
        }

        private void OnDestroy()
        {
            Service?.NotifyHostDestroyed();
            Service = null;
        }
    }
}
