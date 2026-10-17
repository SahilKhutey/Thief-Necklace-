using UnityEngine;

namespace ThiefAndTheNecklace.Visual
{
    public enum ScreenTransitionType { None, StandardFade, DirectionalSlide, CardReveal, Curtain, Cinematic, Crossfade, Overlay, ScoreTransition }

    [CreateAssetMenu(fileName = "TAN_TransitionProfile", menuName = "Thief & the Necklace/Visual/Transition Profile")]
    public sealed class TransitionProfile : ScriptableObject
    {
        [Min(0f)] public float Duration = 0.2f;
        public AnimationCurve Curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        public bool UseUnscaledTime = true;
        [Min(0f)] public float CinematicDuration = 0.42f;
        [Min(0f)] public float CardDuration = 0.3f;
        [Min(0f)] public float ResultsDuration = 0.3f;

        public float DurationFor(ScreenTransitionType type)
        {
            switch (type)
            {
                case ScreenTransitionType.Cinematic:
                case ScreenTransitionType.Curtain: return Mathf.Max(0f, CinematicDuration);
                case ScreenTransitionType.CardReveal: return Mathf.Max(0f, CardDuration);
                case ScreenTransitionType.ScoreTransition: return Mathf.Max(0f, ResultsDuration);
                default: return Mathf.Max(0f, Duration);
            }
        }
    }
}
