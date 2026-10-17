using UnityEngine.UIElements;
using ThiefAndTheNecklace.Visual;

namespace ThiefAndTheNecklace.Visual.UI
{
    public sealed class TanPanel : VisualStateComponentBase
    {
        public VisualElement Content { get; }
        public VisualElement Footer { get; }
        private readonly Label _header;

        public TanPanel(VisualElement element) : base(element?.Q<VisualElement>("panel") ?? element)
        {
            _header = element?.Q<Label>("header");
            Content = element?.Q<VisualElement>("content");
            Footer = element?.Q<VisualElement>("footer");
        }

        public void SetHeader(string text)
        {
            if (_header == null) return;
            _header.text = text ?? string.Empty;
            _header.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        public void SetVariant(VisualPanelVariant variant)
        {
            if (Root == null) return;
            foreach (VisualPanelVariant value in System.Enum.GetValues(typeof(VisualPanelVariant)))
                Root.EnableInClassList("tan-panel--" + value.ToString().ToLowerInvariant(), value == variant);
        }
    }
}
