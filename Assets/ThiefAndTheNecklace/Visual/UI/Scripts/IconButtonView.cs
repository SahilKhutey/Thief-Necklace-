using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ThiefAndTheNecklace.Visual.UI
{
    public sealed class IconButtonView : VisualStateComponentBase
    {
        public Button Button { get; }
        private readonly VisualElement _icon;

        public IconButtonView(VisualElement element) : base(element?.Q<Button>("button") ?? element as Button)
        {
            Button = Root as Button;
            _icon = element?.Q<VisualElement>("icon");
            if (Button != null) Button.style.minWidth = Button.style.minHeight = 48;
        }

        public void Bind(Sprite icon, string accessibleLabel, Action clicked)
        {
            if (_icon != null) _icon.style.backgroundImage = icon == null ? StyleKeyword.None : new StyleBackground(icon);
            if (Button == null) return;
            Button.tooltip = accessibleLabel ?? string.Empty;
            Button.clicked -= clicked;
            Button.clicked += clicked;
        }
    }
}
