using System.Linq;
using System;
using UnityEngine;
using UnityEngine.UIElements;
using ThiefAndTheNecklace.Visual;
using ThiefAndTheNecklace.UI;

namespace ThiefAndTheNecklace.Visual.UI
{
    public sealed class PrivateRoleScreenController
    {
        private readonly VisualElement _root;
        private readonly RoleCardView _card;
        private readonly Button _reveal;
        private readonly Button _continue;
        private bool _revealing;
        public event Action ContinueRequested;

        public PrivateRoleScreenController(VisualElement root)
        {
            _root = root;
            _card = new RoleCardView(root?.Q<VisualElement>("role-card"));
            _reveal = root?.Q<Button>("show-role");
            _continue = root?.Q<Button>("continue-role");
            if (_reveal != null) _reveal.clicked += Reveal;
            if (_continue != null) _continue.clicked += () => ContinueRequested?.Invoke();
            Reset();
        }

        public void Reset()
        {
            _revealing = false;
            _card.ShowBack();
            if (_reveal != null) { _reveal.style.display = DisplayStyle.Flex; _reveal.SetEnabled(true); }
            if (_continue != null) _continue.SetEnabled(false);
            foreach (var detail in _root.Query<VisualElement>(className: "pv2-private-detail").ToList()) detail.style.display = DisplayStyle.None;
        }

        public void Bind(PrivateRoleScreenViewModel data)
        {
            if (data == null) return;
            _card.Bind(data.RoleName, data.RoleArtwork, data.RoleValue);
            SetText("private-situation", data.Situation);
            SetText("private-objective", data.Objective);
            var clueList = _root.Q<VisualElement>("private-clues");
            if (clueList != null)
            {
                clueList.Clear();
                if (data.Clues != null)
                    foreach (var clue in data.Clues)
                    {
                        var label = new Label("• " + clue);
                        label.AddToClassList("tn-body");
                        clueList.Add(label);
                    }
            }
        }

        private void Reveal()
        {
            if (_revealing) return;
            _revealing = true;
            if (_reveal != null) _reveal.style.display = DisplayStyle.None;
            var details = _root.Query<VisualElement>(className: "pv2-private-detail").ToList();
            if (UIPresentationSettings.ReducedMotion)
            {
                VisualEventBus.Raise(VisualEventType.CardRevealStarted);
                _card.ShowFront();
                RevealDetails(details);
                if (_continue != null) _continue.SetEnabled(true);
                _revealing = false;
                VisualEventBus.Raise(VisualEventType.CardRevealCompleted);
            }
            else PlayFlip(details);
            VisualEventBus.Raise(VisualEventType.RoleShown);
        }

        private void PlayFlip(System.Collections.Generic.List<VisualElement> details)
        {
            var element = _card.Element;
            if (element == null) { _card.ShowFront(); RevealDetails(details); if (_continue != null) _continue.SetEnabled(true); _revealing = false; return; }
            VisualEventBus.Raise(VisualEventType.CardRevealStarted);
            var start = Time.realtimeSinceStartup;
            var changedFace = false;
            IVisualElementScheduledItem item = null;
            item = element.schedule.Execute(() =>
            {
                if (element.panel == null) { _revealing = false; item?.Pause(); return; }
                var t = Mathf.Clamp01((Time.realtimeSinceStartup - start) / 0.54f);
                var x = t < 0.5f ? Mathf.Lerp(1f, 0.035f, t * 2f) : Mathf.Lerp(0.035f, 1f, (t - 0.5f) * 2f);
                element.style.scale = new StyleScale(new Scale(new Vector3(x, 1f, 1f)));
                if (!changedFace && t >= 0.5f) { _card.ShowFront(); changedFace = true; }
                if (t >= 1f)
                {
                    element.style.scale = new StyleScale(new Scale(Vector3.one));
                    RevealDetails(details);
                    if (_continue != null) _continue.SetEnabled(true);
                    _revealing = false;
                    VisualEventBus.Raise(VisualEventType.CardRevealCompleted);
                    item?.Pause();
                }
            }).Every(16);
        }

        private static void RevealDetails(System.Collections.Generic.List<VisualElement> details)
        {
            for (var i = 0; i < details.Count; i++)
            {
                var detail = details[i];
                detail.style.display = DisplayStyle.Flex;
                if (UIPresentationSettings.ReducedMotion) detail.style.opacity = 1f;
                else UIPresentationMotion.FadeIn(detail, 120 + i * 35);
            }
        }

        private void SetText(string name, string value)
        {
            var label = _root.Q<Label>(name);
            if (label != null) label.text = value ?? string.Empty;
        }
    }
}
