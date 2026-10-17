using UnityEngine;
using ThiefAndTheNecklace.Visual;

namespace ThiefAndTheNecklace.UI
{
    /// <summary>Shared art-direction tokens. UI Toolkit consumes these at runtime.</summary>
    [CreateAssetMenu(fileName = "UIThemeData", menuName = "Thief & the Necklace/UI/Theme")]
    public sealed class UIThemeData : ScriptableObject
    {
        [Header("Royal mystery palette")]
        public Color ink = VisualDesignTokens.DeepInk;
        public Color panel = VisualDesignTokens.RoyalSurface;
        public Color parchment = VisualDesignTokens.Parchment;
        public Color gold = VisualDesignTokens.AntiqueGold;
        public Color cream = VisualDesignTokens.WarmIvory;
        public Color warning = VisualDesignTokens.Warning;

        [Header("Layout")]
        [Min(0)] public float smallSpacing = 8;
        [Min(0)] public float mediumSpacing = 16;
        [Min(0)] public float largeSpacing = 32;
        [Min(0)] public float cardRadius = VisualDesignTokens.RadiusMedium;
        [Min(40)] public float buttonHeight = 50;

        [Header("Motion, seconds")]
        [Min(0)] public float fastAnimation = 0.10f;
        [Min(0)] public float normalAnimation = 0.18f;
        [Min(0)] public float dramaticAnimation = 0.65f;
    }
}
