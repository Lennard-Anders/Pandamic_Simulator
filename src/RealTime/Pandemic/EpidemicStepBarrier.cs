namespace RealTime.Pandemic
{
    using System;

    /// <summary>Publishes a due, completed simulation tick while holding the clock for the main thread.</summary>
    internal sealed class EpidemicStepBarrier
    {
        private readonly object gate = new object();
        private bool enabled, paused;
        private DateTime lastStep;
        private DateTime pendingTime;
        private bool acquired;
        private uint minutes = 5;

        public void Synchronize(bool running, DateTime completedStep, uint stepMinutes, Action releasePause)
        {
            lock (gate)
            {
                enabled = running;
                lastStep = completedStep;
                minutes = stepMinutes;
                if (paused)
                {
                    releasePause();
                    paused = false;
                    acquired = false;
                }
            }
        }

        public bool TryAcquire(out DateTime observedTime)
        {
            lock (gate)
            {
                observedTime = pendingTime;
                if (!paused || acquired) return false;
                acquired = true;
                return true;
            }
        }

        public bool CheckTick(DateTime observedTime, Action pauseClock)
        {
            lock (gate)
            {
                if (!enabled || paused) return false;
                var decision = EpidemicStepScheduler.Evaluate(lastStep, observedTime, minutes, true);
                if (decision.Kind == EpidemicStepDecisionKind.NotDue) return false;
                // Set the pause under the same lock as release, so an old callback
                // cannot re-pause after the main thread has consumed the request.
                pauseClock();
                pendingTime = observedTime;
                paused = true;
                return true;
            }
        }
    }
}
