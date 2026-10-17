using UnityEngine;

namespace ThiefAndTheNecklace.Visual
{
    public enum VisualTextRole { DisplayXxl, DisplayXl, Display, Headline, Subheadline, BodyLarge, Body, BodySmall, Caption, Label }

    /// <summary>Canonical runtime design tokens shared by screens and reusable UI components.</summary>
    public static class VisualDesignTokens
    {
        public static readonly Color32 DeepInk = new Color32(33, 24, 19, 255);
        public static readonly Color32 InkSoft = new Color32(48, 35, 28, 255);
        public static readonly Color32 RoyalSurface = new Color32(56, 41, 32, 255);
        public static readonly Color32 SurfaceRaised = new Color32(73, 55, 42, 255);
        public static readonly Color32 WarmIvory = new Color32(245, 236, 208, 255);
        public static readonly Color32 Parchment = new Color32(216, 196, 155, 255);
        public static readonly Color32 AntiqueGold = new Color32(198, 154, 69, 255);
        public static readonly Color32 Burgundy = new Color32(105, 45, 60, 255);
        public static readonly Color32 Forest = new Color32(49, 87, 68, 255);
        public static readonly Color32 Midnight = new Color32(37, 56, 77, 255);
        public static readonly Color32 Success = new Color32(87, 144, 106, 255);
        public static readonly Color32 Warning = new Color32(213, 163, 75, 255);
        public static readonly Color32 Error = new Color32(182, 76, 71, 255);
        public static readonly Color32 Info = new Color32(107, 156, 170, 255);
        public static readonly Color32 Disabled = new Color32(117, 108, 97, 255);
        public static readonly Color32 Surface = RoyalSurface;
        public static readonly Color32 TextPrimary = WarmIvory;
        public static readonly Color32 TextSecondary = Parchment;
        public static readonly Color32 Accent = AntiqueGold;
        public static readonly Color32 Information = Info;

        public static readonly float[] Spacing = { 4, 8, 12, 16, 24, 32, 48, 64, 96 };
        public static readonly int[] TypeScale = { 48, 40, 32, 24, 20, 18, 16, 14, 12, 12 };
        public static int FontSize(VisualTextRole role) => TypeScale[(int)role];
        public const float RadiusSmall = 8;
        public const float RadiusMedium = 14;
        public const float RadiusLarge = 24;
        public const float BorderSubtle = 1;
        public const float BorderStrong = 2;
        public const int MotionFastMilliseconds = 100;
        public const int MotionNormalMilliseconds = 180;
        public const int MotionSlowMilliseconds = 300;
        public const int MotionDramaticMilliseconds = 650;
        public static readonly Color32 ShadowSoft = new Color32(0, 0, 0, 48);
        public static readonly Color32 ShadowElevated = new Color32(0, 0, 0, 88);
    }

    /// <summary>Visual state has text/icon shape companions; color alone must not carry meaning.</summary>
    public enum VisualState { Default, Selected, Success, Warning, Error, Disabled, Focused, Disconnected }
}
