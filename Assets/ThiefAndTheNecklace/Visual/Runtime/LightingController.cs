using UnityEngine;
using UnityEngine.UIElements;
using ThiefAndTheNecklace.UI;

namespace ThiefAndTheNecklace.Visual
{
    /// <summary>Applies gradual phase lighting through an optional atmosphere UI layer.</summary>
    public sealed class LightingController : MonoBehaviour, IVisualPresenter
    {
        [SerializeField] private UIDocument document;
        [SerializeField] private string atmosphereElementName = "environment-atmosphere";
        [SerializeField, Range(0.1f, 1f)] private float revealDim = 0.74f;
        [SerializeField, Range(0.1f, 1f)] private float transitionSeconds = 0.35f;
        private VisualElement _atmosphere;
        private IVisualElementScheduledItem _transition;
        private readonly Color _baseColor = new Color(0.22f, 0.12f, 0.06f, 0.10f);

        private void OnEnable() { VisualPresentationBus.Shared.EventRaised += Present; }
        private void OnDisable() { VisualPresentationBus.Shared.EventRaised -= Present; }

        private void Awake()
        {
            EnsureTarget();
            SetState(VisualLightingState.Normal, false);
        }

        private void EnsureTarget()
        {
            if (document == null) document = GetComponent<UIDocument>();
            if (_atmosphere == null && document != null)
                _atmosphere = document.rootVisualElement.Q<VisualElement>(atmosphereElementName);
        }

        public void SetState(VisualLightingState state, bool animate = true)
        {
            EnsureTarget();
            if (_atmosphere == null) return;
            var target = MoodColor(state);
            if (state == VisualLightingState.Reveal)
                target = new Color(target.r * revealDim, target.g * revealDim, target.b * revealDim, Mathf.Max(target.a, 0.28f));
            if (!animate || UIPresentationSettings.ReducedMotion || transitionSeconds <= 0f)
            {
                _transition?.Pause();
                _atmosphere.style.backgroundColor = target;
                return;
            }

            var from = _atmosphere.resolvedStyle.backgroundColor;
            var start = Time.realtimeSinceStartup;
            _transition?.Pause();
            _transition = _atmosphere.schedule.Execute(() =>
            {
                if (_atmosphere.panel == null) { _transition?.Pause(); return; }
                var t = Mathf.Clamp01((Time.realtimeSinceStartup - start) / transitionSeconds);
                _atmosphere.style.backgroundColor = Color.Lerp(from, target, t);
                if (t >= 1f) _transition?.Pause();
            }).Every(16);
        }

        public void Present(VisualPresentationEvent item)
        {
            switch (item.Type)
            {
                case VisualEventType.DiscussionStarted: SetState(VisualLightingState.Discussion); break;
                case VisualEventType.VoteOpened: SetState(VisualLightingState.Voting); break;
                case VisualEventType.RevealStarted:
                case VisualEventType.RoleRevealed: SetState(VisualLightingState.Reveal); break;
                case VisualEventType.RoundResultShown:
                case VisualEventType.MatchResultShown: SetState(VisualLightingState.Results); break;
                case VisualEventType.RoundStarted:
                case VisualEventType.MatchStarted: SetState(VisualLightingState.Normal); break;
            }
        }

        private Color MoodColor(VisualLightingState state)
        {
            switch (state)
            {
                case VisualLightingState.Discussion: return new Color(0.52f, 0.29f, 0.12f, 0.11f);
                case VisualLightingState.Voting: return new Color(0.09f, 0.07f, 0.11f, 0.20f);
                case VisualLightingState.Reveal: return new Color(0.03f, 0.02f, 0.04f, 0.34f);
                case VisualLightingState.Results: return new Color(0.48f, 0.34f, 0.12f, 0.14f);
                default: return _baseColor;
            }
        }
    }
}
