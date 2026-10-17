using UnityEngine;
using ThiefAndTheNecklace.Visual;

namespace ThiefAndTheNecklace.Visual.UI
{
    /// <summary>Editable theme values. Defaults mirror the canonical PV0 fallback tokens.</summary>
    [CreateAssetMenu(fileName = "TAN_ThemeTokens", menuName = "Thief & the Necklace/Visual/Theme Tokens")]
    public sealed class TanThemeTokens : ScriptableObject
    {
        [Header("Spacing")]
        public float Space1 = 4f;
        public float Space2 = 8f;
        public float Space3 = 12f;
        public float Space4 = 16f;
        public float Space5 = 24f;
        public float Space6 = 32f;
        public float Space7 = 48f;
        public float Space8 = 64f;
        public float Space9 = 96f;

        [Header("Radii")]
        public float RadiusSmall = 8f;
        public float RadiusMedium = 14f;
        public float RadiusLarge = 24f;

        [Header("Motion, seconds")]
        public float MotionFast = 0.10f;
        public float MotionNormal = 0.18f;
        public float MotionSlow = 0.30f;
        public float MotionCinematic = 0.65f;
    }
}
