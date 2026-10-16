using System;
using UnityEngine;

namespace ThiefAndTheNecklace.PB1.Unity
{
    public sealed class GameTimer : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float durationSeconds = 180f;
        public float Remaining { get; private set; }
        public bool IsRunning { get; private set; }
        public event Action Completed;
        public event Action<float> Tick;

        public void StartTimer(float seconds)
        {
            Remaining = Mathf.Max(0f, seconds);
            IsRunning = Remaining > 0f;
            Tick?.Invoke(Remaining);
            if (!IsRunning) Completed?.Invoke();
        }

        public void StartConfiguredTimer() { StartTimer(durationSeconds); }
        public void StopTimer() { IsRunning = false; }

        private void Update()
        {
            if (!IsRunning) return;
            Remaining = Mathf.Max(0f, Remaining - Time.deltaTime);
            Tick?.Invoke(Remaining);
            if (Remaining > 0f) return;
            IsRunning = false;
            Completed?.Invoke();
        }
    }
}
