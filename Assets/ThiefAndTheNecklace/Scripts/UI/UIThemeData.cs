using UnityEngine;

namespace ThiefAndTheNecklace.UI
{
    /// <summary>Shared art-direction tokens. UI Toolkit consumes these at runtime.</summary>
    [CreateAssetMenu(fileName = "UIThemeData", menuName = "Thief & the Necklace/UI/Theme")]
    public sealed class UIThemeData : ScriptableObject
    {
        [Header("Royal mystery palette")]
        public Color ink = new Color(0.10f, 0.075f, 0.06f);
        public Color panel = new Color(0.18f, 0.13f, 0.10f);
        public Color parchment = new Color(0.88f, 0.79f, 0.61f);
        public Color gold = new Color(0.78f, 0.60f, 0.27f);
        public Color cream = new Color(0.96f, 0.91f, 0.78f);
        public Color warning = new Color(0.72f, 0.25f, 0.19f);

        [Header("Layout")]
        [Min(0)] public float smallSpacing = 8;
        [Min(0)] public float mediumSpacing = 16;
        [Min(0)] public float largeSpacing = 28;
        [Min(0)] public float cardRadius = 6;
        [Min(40)] public float buttonHeight = 48;

        [Header("Motion, seconds")]
        [Min(0)] public float fastAnimation = 0.12f;
        [Min(0)] public float normalAnimation = 0.22f;
        [Min(0)] public float dramaticAnimation = 0.55f;
    }
}
