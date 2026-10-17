using UnityEngine.UIElements;

namespace ThiefAndTheNecklace.Visual.UI
{
    public sealed class BadgeView : VisualStateComponentBase
    {
        private readonly Label _label;
        public BadgeView(VisualElement element) : base(element?.Q<VisualElement>("badge") ?? element)
        {
            _label = element?.Q<Label>("label");
        }

        public void Bind(string text, string tone)
        {
            if (_label != null) _label.text = text ?? string.Empty;
            if (Root == null) return;
            tone = (tone ?? "neutral").ToLowerInvariant();
            foreach (var value in new[] { "neutral", "success", "warning", "error", "info" })
                Root.EnableInClassList("tan-badge--" + value, value == tone);
        }
    }
}
