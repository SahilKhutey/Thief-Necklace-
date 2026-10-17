using UnityEngine;

namespace ThiefAndTheNecklace.Visual
{
    /// <summary>Frozen visual-run configuration for the final vertical slice. User accessibility preferences remain user-owned.</summary>
    [CreateAssetMenu(fileName = "TAN_ReleasePresentationProfile", menuName = "Thief & the Necklace/Visual/Release Presentation Profile")]
    public sealed class ReleasePresentationProfile : ScriptableObject
    {
        [Header("Production data")]
        public TransitionProfile transitions;
        public RoleVisualDatabase roles;
        public CardVisualDatabase cards;
        public SpaceVisualDatabase spaces;
        public PropVisualDatabase props;

        [Header("Presentation defaults")]
        public VisualQualityLevel quality = VisualQualityLevel.High;
        public VisualLightingState openingLighting = VisualLightingState.Normal;
        [Header("Release approval")]
        [Tooltip("Set only after the PV-FINAL automated report and every manual visual/release gate have passed.")]
        public bool approvedForRelease;
        [Tooltip("Effects not listed here are suppressed, including experimental scene effects.")]
        public VisualEffectId[] approvedEffects =
        {
            VisualEffectId.CardSelect, VisualEffectId.CardReveal, VisualEffectId.VoteSelect,
            VisualEffectId.VoteResolve, VisualEffectId.ScoreGain, VisualEffectId.NecklaceSpark,
            VisualEffectId.Victory, VisualEffectId.Escape, VisualEffectId.CardHover,
            VisualEffectId.CardFlip, VisualEffectId.VoteLock, VisualEffectId.VoteTie,
            VisualEffectId.ScoreTotal, VisualEffectId.EnvironmentDust, VisualEffectId.EnvironmentMist,
            VisualEffectId.CandleFlicker, VisualEffectId.Success, VisualEffectId.Warning, VisualEffectId.Error
        };

        public bool Allows(VisualEffectId id)
        {
            if (approvedEffects == null) return false;
            foreach (var approved in approvedEffects) if (approved == id) return true;
            return false;
        }

        public void ApplyRuntimeDefaults()
        {
            VisualQualitySettings.Set(quality);
            // Do not overwrite Reduced Motion, Large Text, or High Contrast user preferences.
        }
    }

}
