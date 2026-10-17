using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ThiefAndTheNecklace.UI;

namespace ThiefAndTheNecklace.Visual
{
    public enum VisualQualityLevel { High, Medium, Low }
    public enum VisualEnvironmentState { Normal, Discussion, Voting, Reveal, Results }
    public enum VisualEffectId
    {
        CardSelect, CardReveal, VoteSelect, VoteResolve, ScoreGain, NecklaceSpark, Victory, Escape,
        CardHover, CardFlip, VoteLock, VoteTie, ScoreTotal, EnvironmentDust, EnvironmentMist,
        CandleFlicker, Success, Warning, Error
    }

    /// <summary>Presentation quality policy. It never changes game rules or game timing.</summary>
    public static class VisualQualitySettings
    {
        public static VisualQualityLevel Level { get; private set; } = VisualQualityLevel.High;
        public static event Action<VisualQualityLevel> Changed;
        public static void Set(VisualQualityLevel level)
        {
            if (Level == level) return;
            Level = level;
            Changed?.Invoke(level);
        }
        public static bool EnvironmentMotionEnabled => Level != VisualQualityLevel.Low && !UIPresentationSettings.ReducedMotion;
        public static bool ParticlesEnabled => Level != VisualQualityLevel.Low && !UIPresentationSettings.ReducedMotion;
        public static int ParticleLimit => Level == VisualQualityLevel.High ? 128 : Level == VisualQualityLevel.Medium ? 40 : 0;
        public static int MaxPooledEffects => Level == VisualQualityLevel.High ? 64 : Level == VisualQualityLevel.Medium ? 20 : 0;
        public static float ParallaxScale => Level == VisualQualityLevel.High ? 1f : Level == VisualQualityLevel.Medium ? 0.35f : 0f;
    }

    /// <summary>Optional visual-to-audio cue adapter; missing cue assets stay silent.</summary>
    public sealed class VisualAudioCueRouter : MonoBehaviour
    {
        [SerializeField] private AudioService audioService;
        private void Awake() { if (audioService == null) audioService = FindObjectOfType<AudioService>(); }
        private void OnEnable() { VisualPresentationBus.Shared.EventRaised += OnPresentationEvent; }
        private void OnDisable() { VisualPresentationBus.Shared.EventRaised -= OnPresentationEvent; }

        private void OnPresentationEvent(VisualPresentationEvent item)
        {
            if (audioService == null) return;
            string cue = null;
            switch (item.Type)
            {
                case VisualEventType.ButtonPressed: cue = "button_pressed"; break;
                case VisualEventType.CardFlipped: cue = "card_flip"; break;
                case VisualEventType.CardSelected: cue = "card_select"; break;
                case VisualEventType.VoteLocked: cue = "vote_locked"; break;
                case VisualEventType.VoteTie: cue = "vote_tie"; break;
                case VisualEventType.VoteResolve: cue = "vote_resolve"; break;
                case VisualEventType.TimerWarning: cue = "timer_warning"; break;
                case VisualEventType.TimerCritical: cue = "timer_critical"; break;
                case VisualEventType.RoleRevealed: cue = "truth_reveal"; break;
                case VisualEventType.ScoreTick: cue = "score_tick"; break;
                case VisualEventType.Victory: cue = "thief_caught"; break;
                case VisualEventType.Escape: cue = "thief_escaped"; break;
            }
            if (cue != null) audioService.PlayCue(cue);
        }
    }

    /// <summary>Reduced-motion-aware horizontal flip for world-space/canvas role cards.</summary>
    public sealed class CardFlipAnimation : MonoBehaviour
    {
        [SerializeField] private RectTransform target;
        [SerializeField, Min(0.1f)] private float duration = 0.52f;
        private Coroutine _active;

        public void Play(Action midpoint)
        {
            if (target == null) { midpoint?.Invoke(); return; }
            if (_active != null) StopCoroutine(_active);
            _active = StartCoroutine(Flip(midpoint));
        }

        private IEnumerator Flip(Action midpoint)
        {
            if (UIPresentationSettings.ReducedMotion)
            {
                yield return Fade(1f, 0.35f);
                midpoint?.Invoke();
                yield return Fade(0.35f, 1f);
                _active = null;
                yield break;
            }
            yield return ScaleX(1f, 0.04f, duration * 0.5f);
            midpoint?.Invoke();
            yield return ScaleX(0.04f, 1f, duration * 0.5f);
            _active = null;
        }

        private IEnumerator ScaleX(float from, float to, float seconds)
        {
            var elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / seconds);
                target.localScale = new Vector3(Mathf.Lerp(from, to, t), 1f, 1f);
                yield return null;
            }
            target.localScale = new Vector3(to, 1f, 1f);
        }

        private IEnumerator Fade(float from, float to)
        {
            var group = target.GetComponent<CanvasGroup>();
            if (group == null) { yield return null; yield break; }
            var elapsed = 0f;
            while (elapsed < 0.12f)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / 0.12f));
                yield return null;
            }
            group.alpha = to;
        }
    }

    /// <summary>Non-blocking count-up helper for results labels; supports reduced motion.</summary>
    public static class ScoreCountAnimation
    {
        public static IVisualElementScheduledItem Count(Label label, int from, int to, float duration = 0.55f, long delayMilliseconds = 0)
        {
            if (label == null) return null;
            if (UIPresentationSettings.ReducedMotion || duration <= 0f)
            {
                label.text = to.ToString();
                return null;
            }
            VisualEventBus.Raise(VisualEventType.ScoreAnimationStarted);
            label.text = from.ToString();
            var start = Time.realtimeSinceStartup;
            var previousValue = from;
            IVisualElementScheduledItem item = null;
            item = label.schedule.Execute(() =>
            {
                if (label.panel == null) { item?.Pause(); return; }
                var t = Mathf.Clamp01((Time.realtimeSinceStartup - start) / duration);
                var value = Mathf.RoundToInt(Mathf.Lerp(from, to, t));
                label.text = value.ToString();
                if (value != previousValue) { previousValue = value; VisualEventBus.Raise(VisualEventType.ScoreTick); }
                if (t >= 1f) { item?.Pause(); VisualEventBus.Raise(VisualEventType.ScoreAnimationCompleted); }
            }).StartingIn(delayMilliseconds).Every(16);
            return item;
        }
    }

    /// <summary>Low-cost environment mood tint and restrained camera parallax.</summary>
    public sealed class EnvironmentPresentationController : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        [SerializeField] private string atmosphereElementName = "environment-atmosphere";
        [SerializeField] private Transform parallaxTarget;
        [SerializeField, Range(0f, 0.04f)] private float parallaxAmount = 0.012f;
        [SerializeField, Range(0.1f, 4f)] private float settleSpeed = 0.65f;
        private VisualElement _atmosphere;
        private VisualEnvironmentState _state;
        private Vector3 _home;

        private void Awake()
        {
            if (document == null) document = GetComponent<UIDocument>();
            if (document != null) _atmosphere = document.rootVisualElement.Q<VisualElement>(atmosphereElementName);
            if (parallaxTarget != null) _home = parallaxTarget.localPosition;
            ApplyMood();
        }

        private void Update()
        {
            if (parallaxTarget == null) return;
            var motion = VisualQualitySettings.EnvironmentMotionEnabled;
            var mouse = motion ? Input.mousePosition : Vector3.zero;
            var normalized = motion ? new Vector2(mouse.x / Mathf.Max(1f, Screen.width) - 0.5f, mouse.y / Mathf.Max(1f, Screen.height) - 0.5f) : Vector2.zero;
            var target = _home + new Vector3(-normalized.x, -normalized.y, 0f) * (parallaxAmount * VisualQualitySettings.ParallaxScale);
            parallaxTarget.localPosition = Vector3.Lerp(parallaxTarget.localPosition, target, Time.unscaledDeltaTime * settleSpeed);
        }

        public void SetState(VisualEnvironmentState state)
        {
            _state = state;
            ApplyMood();
        }

        private void ApplyMood()
        {
            if (_atmosphere == null) return;
            var color = new Color(0.22f, 0.12f, 0.06f, 0.10f);
            switch (_state)
            {
                case VisualEnvironmentState.Discussion: color = new Color(0.63f, 0.35f, 0.12f, 0.10f); break;
                case VisualEnvironmentState.Voting: color = new Color(0.10f, 0.08f, 0.12f, 0.18f); break;
                case VisualEnvironmentState.Reveal: color = new Color(0.03f, 0.02f, 0.04f, 0.32f); break;
                case VisualEnvironmentState.Results: color = new Color(0.50f, 0.34f, 0.10f, 0.12f); break;
            }
            _atmosphere.style.backgroundColor = new StyleColor(color);
        }
    }

    [Serializable]
    public sealed class VfxPoolEntry
    {
        public VisualEffectId Id;
        public PooledVfx Prefab;
        [Min(0)] public int Prewarm = 2;
    }

    /// <summary>Reusable short-lived particle effects, capped by the active visual quality level.</summary>
    public sealed class VFXPool : MonoBehaviour
    {
        [SerializeField] private ReleasePresentationProfile releaseProfile;
        [SerializeField] private List<VfxPoolEntry> entries = new List<VfxPoolEntry>();
        private readonly Dictionary<VisualEffectId, Queue<PooledVfx>> _available = new Dictionary<VisualEffectId, Queue<PooledVfx>>();
        private int _instanceCount;

        private void Awake()
        {
            foreach (var entry in entries)
            {
                if (entry == null || entry.Prefab == null) continue;
                var queue = new Queue<PooledVfx>(); _available[entry.Id] = queue;
                var count = Mathf.Min(entry.Prewarm, VisualQualitySettings.MaxPooledEffects - _instanceCount);
                for (var i = 0; i < count; i++) queue.Enqueue(Create(entry.Prefab));
            }
        }

        public PooledVfx Play(VisualEffectId id, Vector3 position)
        {
            if (!VisualQualitySettings.ParticlesEnabled || VisualQualitySettings.ParticleLimit == 0) return null;
            if (releaseProfile != null && !releaseProfile.Allows(id)) return null;
            var entry = entries.Find(item => item != null && item.Id == id && item.Prefab != null);
            if (entry == null) return null;
            if (!_available.TryGetValue(id, out var queue)) _available[id] = queue = new Queue<PooledVfx>();
            // PV5 performance rule: prewarm is the hard per-effect supply. Never Instantiate during gameplay.
            if (queue.Count == 0) return null;
            var effect = queue.Dequeue();
            effect.transform.position = position;
            effect.gameObject.SetActive(true);
            effect.Play(() => { effect.gameObject.SetActive(false); queue.Enqueue(effect); });
            VisualPresentationBus.Shared.Raise(VisualEventType.VfxStarted, id);
            return effect;
        }

        private PooledVfx Create(PooledVfx prefab)
        {
            var item = Instantiate(prefab, transform);
            _instanceCount++;
            item.gameObject.SetActive(false);
            return item;
        }
    }

    public sealed class PooledVfx : MonoBehaviour
    {
        [SerializeField] private ParticleSystem particles;
        private Action _completed;
        private Coroutine _routine;
        public void Play(Action completed = null)
        {
            _completed = completed;
            if (particles == null) { _completed?.Invoke(); return; }
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.Play(true);
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(WaitForFinish());
        }
        public void Stop()
        {
            if (particles != null) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
            _completed = null;
        }
        private IEnumerator WaitForFinish()
        {
            while (particles != null && particles.IsAlive(true)) yield return null;
            _routine = null;
            VisualPresentationBus.Shared.Raise(VisualEventType.VfxCompleted);
            var callback = _completed; _completed = null; callback?.Invoke();
        }
    }
}
