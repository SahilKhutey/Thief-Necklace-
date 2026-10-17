using UnityEngine;
using ThiefAndTheNecklace.Visual;

namespace ThiefAndTheNecklace.Visual.UI
{
    [CreateAssetMenu(fileName = "TAN_ColorPalette", menuName = "Thief & the Necklace/Visual/Color Palette")]
    public sealed class TanColorPalette : ScriptableObject
    {
        [Header("Royal")]
        public Color Ink = VisualDesignTokens.DeepInk;
        public Color Ivory = VisualDesignTokens.WarmIvory;
        public Color Gold = VisualDesignTokens.AntiqueGold;
        public Color Burgundy = VisualDesignTokens.Burgundy;
        public Color Forest = VisualDesignTokens.Forest;
        public Color Midnight = VisualDesignTokens.Midnight;

        [Header("Text")]
        public Color TextPrimary = VisualDesignTokens.TextPrimary;
        public Color TextSecondary = VisualDesignTokens.TextSecondary;
        public Color TextMuted = VisualDesignTokens.Disabled;

        [Header("System")]
        public Color Success = VisualDesignTokens.Success;
        public Color Warning = VisualDesignTokens.Warning;
        public Color Error = VisualDesignTokens.Error;
        public Color Info = VisualDesignTokens.Information;
        public Color Disabled = VisualDesignTokens.Disabled;
    }
}
