using System;
using UnityEngine.UIElements;

namespace ThiefAndTheNecklace.Visual.UI
{
    public enum TimerVisualState { Normal, Low, Critical, Complete }

    public sealed class TimerViewModel
    {
        public string Heading = "DISCUSSION";
        public float RemainingSeconds;
        public float TotalSeconds;
        public TimerVisualState State;
    }

    public sealed class TimerView : VisualStateComponentBase
    {
        private readonly Label _time;
        private readonly VisualElement _progress;
        private readonly Label _heading;

        public TimerView(VisualElement element) : base(element?.Q<VisualElement>("timer") ?? element)
        {
            _time = element?.Q<Label>("time");
            _progress = element?.Q<VisualElement>("progress");
            _heading = element?.Q<Label>("heading");
        }

        public void SetTime(float seconds, float totalSeconds = 0f)
        {
            seconds = Math.Max(0f, seconds);
            var wholeSeconds = (int)Math.Ceiling(seconds);
            if (_time != null) _time.text = $"{wholeSeconds / 60:00}:{wholeSeconds % 60:00}";
            if (_progress != null && totalSeconds > 0f)
                _progress.style.width = Length.Percent(Math.Max(0f, Math.Min(1f, seconds / totalSeconds)) * 100f);
        }

        public void SetHeading(string text)
        {
            if (_heading != null) _heading.text = text ?? string.Empty;
        }

        public void Bind(TimerViewModel data)
        {
            if (data == null) return;
            SetHeading(data.Heading);
            SetTime(data.RemainingSeconds, data.TotalSeconds);
            SetState(data.State);
        }

        public void SetState(TimerVisualState state)
        {
            if (Root == null) return;
            foreach (TimerVisualState value in Enum.GetValues(typeof(TimerVisualState)))
                Root.EnableInClassList("tan-timer--" + value.ToString().ToLowerInvariant(), value == state);
            if (Root.Q<Label>("state-label") is Label stateLabel)
                stateLabel.text = state == TimerVisualState.Critical ? "TIME CRITICAL" : state == TimerVisualState.Low ? "LOW TIME" : state == TimerVisualState.Complete ? "COMPLETE" : string.Empty;
        }
    }
}
