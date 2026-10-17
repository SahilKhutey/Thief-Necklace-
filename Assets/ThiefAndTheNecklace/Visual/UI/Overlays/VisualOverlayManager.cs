using System.Collections.Generic;
using System.Linq;
using ThiefAndTheNecklace.UI;
using ThiefAndTheNecklace.Visual;
using UnityEngine;
using UnityEngine;
using UnityEngine.UIElements;

namespace ThiefAndTheNecklace.Visual.UI
{
    /// <summary>Overlay layer is independent from primary screen visibility.</summary>
    public sealed class VisualOverlayManager
    {
        private readonly VisualElement _layer;
        private readonly Dictionary<VisualOverlayId, VisualElement> _overlays = new Dictionary<VisualOverlayId, VisualElement>();
        public VisualOverlayId? Topmost { get; private set; }
        public VisualElement GetView(VisualOverlayId id) => _overlays.TryGetValue(id, out var view) ? view : null;

        public VisualOverlayManager(VisualElement layer) { _layer = layer; }
        public void Register(VisualOverlayId id, VisualTreeAsset asset)
        {
            if (_layer == null || asset == null) return;
            if (_overlays.TryGetValue(id, out var old)) old.RemoveFromHierarchy();
            var element = asset.CloneTree();
            element.name = "overlay-" + id;
            element.AddToClassList("tn-production-overlay");
            element.style.display = DisplayStyle.None;
            _layer.Add(element);
            foreach (var button in element.Query<Button>().ToList())
                if (button.name == "overlay-close") button.clicked += () => Hide(id);
            _overlays[id] = element;
        }
        public void Show(VisualOverlayId id)
        {
            if (!_overlays.TryGetValue(id, out var view)) return;
            view.style.display = DisplayStyle.Flex;
            if (UIPresentationSettings.ReducedMotion)
            {
                view.style.opacity = 1f;
                view.style.translate = new Translate(0f, 0f);
            }
            else
            {
                view.style.opacity = 0f;
                view.style.translate = new Translate(0f, 10f);
                var start = Time.realtimeSinceStartup;
                IVisualElementScheduledItem transition = null;
                transition = view.schedule.Execute(() =>
                {
                    if (view.panel == null) { transition?.Pause(); return; }
                    var t = Mathf.Clamp01((Time.realtimeSinceStartup - start) / 0.18f);
                    view.style.opacity = t;
                    view.style.translate = new Translate(0f, Mathf.Lerp(10f, 0f, t));
                    if (t >= 1f) transition?.Pause();
                }).Every(16);
            }
            view.BringToFront();
            Topmost = id;
            VisualEventBus.Raise(VisualEventType.ModalOpened);
            if (id == VisualOverlayId.Chat) VisualEventBus.Raise(VisualEventType.ChatOpened);
            if (id == VisualOverlayId.Voting) VisualEventBus.Raise(VisualEventType.VoteOpened);
        }
        public void Hide(VisualOverlayId id)
        {
            if (!_overlays.TryGetValue(id, out var view)) return;
            view.style.display = DisplayStyle.None;
            VisualEventBus.Raise(VisualEventType.ModalClosed);
            if (id == VisualOverlayId.Chat) VisualEventBus.Raise(VisualEventType.ChatClosed);
            Topmost = null;
            foreach (var pair in _overlays) if (pair.Value.style.display == DisplayStyle.Flex) Topmost = pair.Key;
        }
        public void HideAll()
        {
            var visible = new List<VisualOverlayId>();
            foreach (var pair in _overlays) if (pair.Value.style.display == DisplayStyle.Flex) visible.Add(pair.Key);
            foreach (var id in visible) Hide(id);
        }
    }
}
