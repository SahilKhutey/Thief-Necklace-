using UnityEngine;

namespace ThiefAndTheNecklace.UI
{
    public static class HapticsService
    {
        private const string EnabledKey = "tn.haptics.enabled";
        public static bool Enabled
        {
            get => PlayerPrefs.GetInt(EnabledKey, 1) != 0;
            set { PlayerPrefs.SetInt(EnabledKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static void Pulse()
        {
            if (Enabled && Application.isMobilePlatform) Handheld.Vibrate();
        }
    }
}
