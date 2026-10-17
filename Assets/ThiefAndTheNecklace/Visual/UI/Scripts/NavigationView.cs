using System;
using UnityEngine.UIElements;

namespace ThiefAndTheNecklace.Visual.UI
{
    public sealed class NavigationView : VisualStateComponentBase
    {
        private readonly Label _title;
        private readonly Button _back;
        public VisualElement Utility { get; }

        public NavigationView(VisualElement element) : base(element?.Q<VisualElement>("navigation") ?? element)
        {
            _title = element?.Q<Label>("title");
            _back = element?.Q<Button>("back");
            Utility = element?.Q<VisualElement>("utility");
            if (_back != null) _back.style.minWidth = _back.style.minHeight = 48;
        }

        public void Bind(string title, Action back)
        {
            if (_title != null) _title.text = title ?? string.Empty;
            VisualEventBus.Raise(VisualEventType.ScreenOpened);
            if (_back == null) return;
            _back.clicked -= back;
            _back.clicked += back;
        }
    }
}
