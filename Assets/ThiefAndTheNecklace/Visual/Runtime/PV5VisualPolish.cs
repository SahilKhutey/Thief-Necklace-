using System;
using UnityEngine;
using UnityEngine.UIElements;
using ThiefAndTheNecklace.UI;
using ThiefAndTheNecklace.Visual.UI;

namespace ThiefAndTheNecklace.Visual
{
    public enum VisualLightingState { Normal, Discussion, Voting, Reveal, Results }
    public enum VisualCharacterState { Idle, Listening, Speaking, Selected, Accused, Revealed, Results }
    public enum VisualLayoutMode { Desktop, Tablet, MobileLandscape }

    /// <summary>Shared event contract for visual presenters. Payloads are presentation-only.</summary>
    public interface IVisualPresenter
    {
        void Present(VisualPresentationEvent item);
    }

    /// <summary>UI Toolkit card feedback. It only changes presentation classes and publishes visual events.</summary>
    public sealed class CardPresenter : IVisualPresenter
    {
        private readonly VisualElement _root;
        private bool _selected;
        public CardPresenter(VisualElement root)
        {
            _root = root;
            if (_root == null) return;
            _root.RegisterCallback<PointerEnterEvent>(OnPointerEnter);
            _root.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
            _root.RegisterCallback<PointerDownEvent>(OnPointerDown);
            _root.RegisterCallback<PointerUpEvent>(OnPointerUp);
        }

        public void SetSelected(bool selected, bool publishEvent = true)
        {
            _selected = selected;
            _root?.EnableInClassList("pv5-card-selected", selected);
            if (publishEvent)
            {
                if (selected) VisualPresentationBus.Shared.Raise(VisualEventType.CardSelected, _root?.name);
                else VisualPresentationBus.Shared.Raise(VisualEventType.CardDeselected, _root?.name);
            }
        }

        public void PlayReveal(Action midpoint = null)
        {
            if (_root == null) { midpoint?.Invoke(); return; }
            _root.AddToClassList("pv5-card-revealing");
            VisualPresentationBus.Shared.Raise(VisualEventType.CardRevealStarted, _root.name);
            if (UIPresentationSettings.ReducedMotion)
            {
                midpoint?.Invoke();
                _root.RemoveFromClassList("pv5-card-revealing");
                _root.AddToClassList("pv5-card-revealed");
                VisualPresentationBus.Shared.Raise(VisualEventType.CardRevealCompleted, _root.name);
                return;
            }
            var started = Time.realtimeSinceStartup;
            IVisualElementScheduledItem item = null;
            item = _root.schedule.Execute(() =>
            {
                if (_root.panel == null) { item?.Pause(); return; }
                var t = Mathf.Clamp01((Time.realtimeSinceStartup - started) / 0.52f);
                if (t >= 0.5f && !_root.ClassListContains("pv5-card-midpoint"))
                {
                    _root.AddToClassList("pv5-card-midpoint");
                    midpoint?.Invoke();
                }
                if (t >= 1f)
                {
                    item?.Pause();
                    _root.RemoveFromClassList("pv5-card-revealing");
                    _root.RemoveFromClassList("pv5-card-midpoint");
                    _root.AddToClassList("pv5-card-revealed");
                    VisualPresentationBus.Shared.Raise(VisualEventType.CardRevealCompleted, _root.name);
                }
            }).Every(16);
        }

        public void Present(VisualPresentationEvent item)
        {
            switch (item.Type)
            {
                case VisualEventType.CardSelected:
                case VisualEventType.CardSelect: SetSelected(true); break;
                case VisualEventType.CardDeselected: SetSelected(false); break;
                case VisualEventType.CardRevealStarted:
                case VisualEventType.CardReveal: PlayReveal(); break;
                case VisualEventType.CardFlipped:
                case VisualEventType.CardFlip:
                    _root?.AddToClassList("pv5-card-flip-light");
                    if (_root != null) _root.schedule.Execute(() => _root.RemoveFromClassList("pv5-card-flip-light")).StartingIn(220);
                    break;
            }
        }

        private void OnPointerEnter(PointerEnterEvent evt)
        {
            if (_selected || UIPresentationSettings.ReducedMotion) return;
            _root.AddToClassList("pv5-card-hover");
            VisualPresentationBus.Shared.Raise(VisualEventType.CardHover, _root.name);
        }
        private void OnPointerLeave(PointerLeaveEvent evt) { _root.RemoveFromClassList("pv5-card-hover"); _root.RemoveFromClassList("pv5-card-pressed"); }
        private void OnPointerDown(PointerDownEvent evt) { if (!_root.ClassListContains("pv5-card-disabled")) _root.AddToClassList("pv5-card-pressed"); }
        private void OnPointerUp(PointerUpEvent evt) { _root.RemoveFromClassList("pv5-card-pressed"); }
    }

    /// <summary>Applies the restrained illustrated-character state vocabulary to an existing visual.</summary>
    public sealed class CharacterPresentationController
    {
        private readonly VisualElement _root;
        private VisualCharacterState _state;
        public CharacterPresentationController(VisualElement root) { _root = root; SetState(VisualCharacterState.Idle); }
        public void SetState(VisualCharacterState state)
        {
            if (_root == null) return;
            _root.RemoveFromClassList("pv5-character-" + _state.ToString().ToLowerInvariant());
            _state = state;
            _root.AddToClassList("pv5-character-" + state.ToString().ToLowerInvariant());
        }
    }

    /// <summary>Bounded pooled UI Toolkit toasts; toast creation never spawns a GameObject.</summary>
    public sealed class ToastController
    {
        private readonly VisualElement _host;
        private readonly VisualElementPool<VisualElement> _pool;
        private readonly System.Collections.Generic.Queue<VisualElement> _visible = new System.Collections.Generic.Queue<VisualElement>();
        private readonly System.Collections.Generic.Dictionary<VisualElement, int> _generation = new System.Collections.Generic.Dictionary<VisualElement, int>();
        private readonly int _visibleLimit;
        public ToastController(VisualElement host, int visibleLimit = 3)
        {
            _host = host;
            _visibleLimit = Mathf.Max(1, visibleLimit);
            _pool = new VisualElementPool<VisualElement>(() =>
            {
                var toast = new VisualElement(); toast.AddToClassList("tan-toast"); toast.AddToClassList("pv5-toast");
                var message = new Label { name = "toast-message" }; message.AddToClassList("tan-toast__message"); toast.Add(message);
                return toast;
            }, prewarm: _visibleLimit, capacity: 12, reset: toast =>
            {
                toast.RemoveFromClassList("pv5-toast-enter");
                toast.RemoveFromClassList("pv5-toast-exit");
                var label = toast.Q<Label>("toast-message"); if (label != null) label.text = string.Empty;
            });
        }

        public void Show(string message, int holdMilliseconds = 2200)
        {
            if (_host == null) return;
            while (_visible.Count >= _visibleLimit) Release(_visible.Dequeue());
            var toast = _pool.Acquire();
            _generation.TryGetValue(toast, out var generation);
            generation++;
            _generation[toast] = generation;
            var label = toast.Q<Label>("toast-message"); if (label != null) label.text = message ?? string.Empty;
            _host.Add(toast);
            _visible.Enqueue(toast);
            toast.AddToClassList("pv5-toast-enter");
            VisualPresentationBus.Shared.Raise(VisualEventType.ToastShown, message);
            toast.schedule.Execute(() =>
            {
                if (toast.panel == null || !_generation.TryGetValue(toast, out var current) || current != generation) return;
                toast.AddToClassList("pv5-toast-exit");
                toast.schedule.Execute(() =>
                {
                    if (_generation.TryGetValue(toast, out var latest) && latest == generation) Release(toast);
                }).StartingIn(UIPresentationSettings.ReducedMotion ? 0 : 180);
            }).StartingIn(Mathf.Max(250, holdMilliseconds));
        }

        private void Release(VisualElement toast)
        {
            if (toast == null) return;
            _generation.TryGetValue(toast, out var generation);
            _generation[toast] = generation + 1;
            _pool.Release(toast);
        }
    }

    /// <summary>Success/error feedback for any button. Must be invoked only after the owning action resolves.</summary>
    public static class ButtonPresentationFeedback
    {
        public static void Success(VisualElement button) => Pulse(button, "pv5-feedback-success", VisualEventType.ButtonPressed);
        public static void Error(VisualElement button) => Pulse(button, "pv5-feedback-error", VisualEventType.VisualError);

        private static void Pulse(VisualElement button, string className, VisualEventType eventType)
        {
            if (button == null) return;
            VisualPresentationBus.Shared.Raise(eventType, button.name);
            button.RemoveFromClassList("pv5-feedback-success");
            button.RemoveFromClassList("pv5-feedback-error");
            button.AddToClassList(className);
            if (className == "pv5-feedback-error" && !UIPresentationSettings.ReducedMotion)
            {
                button.schedule.Execute(() => { if (button.panel != null) button.style.translate = new Translate(-3f, 0f); }).StartingIn(35);
                button.schedule.Execute(() => { if (button.panel != null) button.style.translate = new Translate(3f, 0f); }).StartingIn(70);
                button.schedule.Execute(() => { if (button.panel != null) button.style.translate = new Translate(-2f, 0f); }).StartingIn(105);
                button.schedule.Execute(() => { if (button.panel != null) button.style.translate = new Translate(2f, 0f); }).StartingIn(140);
                button.schedule.Execute(() => { if (button.panel != null) button.style.translate = new Translate(0f, 0f); }).StartingIn(175);
            }
            if (UIPresentationSettings.ReducedMotion)
            {
                button.schedule.Execute(() => button.RemoveFromClassList(className)).StartingIn(220);
                return;
            }
            button.schedule.Execute(() => button.RemoveFromClassList(className)).StartingIn(360);
        }
    }
}
