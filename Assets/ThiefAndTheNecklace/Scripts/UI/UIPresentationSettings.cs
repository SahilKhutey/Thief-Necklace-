using UnityEngine;
using UnityEngine.UIElements;

namespace ThiefAndTheNecklace.UI
{
    /// <summary>Player-controlled presentation preferences; never changes gameplay rules.</summary>
    public static class UIPresentationSettings
    {
        private const string ReducedMotionKey = "tn.ui.reducedMotion";
        private const string HighContrastKey = "tn.ui.highContrast";
        private const string LargeTextKey = "tn.ui.largeText";
        private const string TextScaleKey = "tn.ui.textScale";

        public static bool ReducedMotion
        {
            get => PlayerPrefs.GetInt(ReducedMotionKey, 0) != 0;
            set => Set(ReducedMotionKey, value);
        }

        public static bool HighContrast
        {
            get => PlayerPrefs.GetInt(HighContrastKey, 0) != 0;
            set => Set(HighContrastKey, value);
        }

        public static bool LargeText
        {
            get => TextScale > 1f;
            set
            {
                Set(LargeTextKey, value);
                PlayerPrefs.SetFloat(TextScaleKey, value ? 1.3f : 1f);
                PlayerPrefs.Save();
            }
        }

        public static float TextScale
        {
            get => Mathf.Clamp(PlayerPrefs.GetFloat(TextScaleKey, PlayerPrefs.GetInt(LargeTextKey, 0) != 0 ? 1.3f : 1f), 1f, 1.5f);
            set { PlayerPrefs.SetFloat(TextScaleKey, Mathf.Clamp(value, 1f, 1.5f)); PlayerPrefs.Save(); }
        }

        public static void Apply(VisualElement root)
        {
            if (root == null) return;
            root.EnableInClassList("tn-high-contrast", HighContrast);
            root.EnableInClassList("tn-large-text", LargeText);
        }

        private static void Set(string key, bool value)
        {
            PlayerPrefs.SetInt(key, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    /// <summary>Small UI-only fades. It reads no game state and never invokes gameplay actions.</summary>
    public static class UIPresentationMotion
    {
        public static void FadeIn(VisualElement element, int durationMilliseconds = 140)
        {
            if (element == null || UIPresentationSettings.ReducedMotion)
            {
                if (element != null) element.style.opacity = 1f;
                return;
            }

            element.style.opacity = 0f;
            var steps = Mathf.Max(1, durationMilliseconds / 16);
            var current = 0;
            void Advance()
            {
                if (element.panel == null) return;
                current++;
                element.style.opacity = Mathf.Clamp01((float)current / steps);
                if (current < steps) element.schedule.Execute(Advance).StartingIn(16);
            }
            element.schedule.Execute(Advance).StartingIn(16);
        }
    }
}
