using System;
using System.Collections.Generic;
using System.Linq;

namespace ThiefAndTheNecklace.PB1
{
    public sealed class DiscussionTimer
    {
        public float RemainingTime { get; private set; }
        public bool IsRunning { get; private set; }
        public event Action<float> Ticked;
        public event Action Ended;
        public void Start(float seconds) { RemainingTime = Math.Max(0f, seconds); IsRunning = RemainingTime > 0f; Ticked?.Invoke(RemainingTime); if (!IsRunning) Ended?.Invoke(); }
        public void Pause() { IsRunning = false; }
        public void Resume() { IsRunning = RemainingTime > 0f; }
        public void End() { IsRunning = false; RemainingTime = 0f; Ticked?.Invoke(0f); Ended?.Invoke(); }
        public void Advance(float deltaSeconds)
        {
            if (!IsRunning) return;
            if (deltaSeconds < 0f) throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            RemainingTime = Math.Max(0f, RemainingTime - deltaSeconds);
            Ticked?.Invoke(RemainingTime);
            if (RemainingTime <= 0f) { IsRunning = false; Ended?.Invoke(); }
        }
    }
}
