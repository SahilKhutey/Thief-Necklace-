using System;
using UnityEngine.UIElements;

namespace ThiefAndTheNecklace.Visual.UI
{
    public sealed class ModalView : VisualStateComponentBase
    {
        public VisualElement Body { get; }
        public VisualElement Actions { get; }
        private readonly Label _header;
        private readonly Button _close;
        private bool _isOpen;

        public ModalView(VisualElement element) : base(element?.Q<VisualElement>("modal") ?? element)
        {
            _header = element?.Q<Label>("header");
            Body = element?.Q<VisualElement>("body");
            Actions = element?.Q<VisualElement>("actions");
            _close = element?.Q<Button>("close");
            if (_close != null) _close.clicked += Close;
            Close();
        }

        public void Open(string title)
        {
            if (_header != null) _header.text = title ?? string.Empty;
            if (Root != null) Root.style.display = DisplayStyle.Flex;
            if (!_isOpen) VisualEventBus.Raise(VisualEventType.ModalOpened);
            _isOpen = true;
        }

        public void Close()
        {
            if (Root == null) return;
            var wasOpen = _isOpen;
            _isOpen = false;
            Root.style.display = DisplayStyle.None;
            if (wasOpen) VisualEventBus.Raise(VisualEventType.ModalClosed);
        }
    }
}
