using UnityEngine;
using UnityEngine.UIElements;
using ThiefAndTheNecklace.Visual;
using ThiefAndTheNecklace.UI;

namespace ThiefAndTheNecklace.Visual.UI
{
    public sealed class TanButton : VisualStateComponentBase
    {
        public Button RootButton { get; }
        private readonly VisualElement _icon;
        private readonly Label _label;
        private readonly VisualElement _loading;

        public TanButton(VisualElement element) : base(element?.Q<Button>("root") ?? element as Button)
        {
            RootButton = Root as Button;
            _icon = element?.Q<VisualElement>("icon");
            _label = element?.Q<Label>("label");
            _loading = element?.Q<VisualElement>("loading");
            if (_icon != null) _icon.style.display = DisplayStyle.None;
            if (RootButton != null)
            {
                RootButton.RegisterCallback<PointerEnterEvent>(_ =>
                {
                    if (!UIPresentationSettings.ReducedMotion && RootButton.enabledSelf) RootButton.style.translate = new Translate(0, -2);
                });
                RootButton.RegisterCallback<PointerLeaveEvent>(_ => RootButton.style.translate = new Translate(0, 0));
                RootButton.RegisterCallback<PointerDownEvent>(_ => RootButton.style.opacity = 0.84f);
                RootButton.RegisterCallback<PointerUpEvent>(_ => RootButton.style.opacity = 1f);
                RootButton.RegisterCallback<FocusInEvent>(_ => RootButton.AddToClassList("tan-is-focused"));
                RootButton.RegisterCallback<FocusOutEvent>(_ => RootButton.RemoveFromClassList("tan-is-focused"));
            }
        }

        public void SetText(string text)
        {
            if (_label != null) _label.text = text ?? string.Empty;
            else if (RootButton != null) RootButton.text = text ?? string.Empty;
        }

        public void SetVariant(VisualButtonVariant variant)
        {
            if (Root == null) return;
            foreach (VisualButtonVariant value in System.Enum.GetValues(typeof(VisualButtonVariant)))
                Root.EnableInClassList("tan-button--" + value.ToString().ToLowerInvariant(), value == variant);
        }

        public void SetFeedbackState(VisualState state)
        {
            if (Root == null) return;
            Root.EnableInClassList("tan-button--success", state == VisualState.Success);
            Root.EnableInClassList("tan-button--error", state == VisualState.Error);
        }

        public void SetIcon(Sprite sprite)
        {
            if (_icon == null) return;
            _icon.style.display = sprite == null ? DisplayStyle.None : DisplayStyle.Flex;
            _icon.style.backgroundImage = sprite == null ? StyleKeyword.None : new StyleBackground(sprite);
        }

        public override void SetLoadingState(bool loading)
        {
            if (_loading != null) _loading.style.display = loading ? DisplayStyle.Flex : DisplayStyle.None;
            base.SetLoadingState(loading);
        }
    }
}
