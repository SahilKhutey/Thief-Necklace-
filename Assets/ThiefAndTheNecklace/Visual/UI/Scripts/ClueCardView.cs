using UnityEngine;
using UnityEngine.UIElements;

namespace ThiefAndTheNecklace.Visual.UI
{
    public sealed class ClueCardViewModel
    {
        public string Heading = "CLUE";
        public string Content = string.Empty;
        public string Source = string.Empty;
        public Sprite Icon;
    }

    public sealed class ClueCardView : VisualStateComponentBase
    {
        private readonly VisualElement _icon;
        private readonly Label _title;
        private readonly Label _content;
        private readonly Label _source;

        public ClueCardView(VisualElement element) : base(element?.Q<VisualElement>("clue-card") ?? element)
        {
            _icon = element?.Q<VisualElement>("icon");
            _title = element?.Q<Label>("title");
            _content = element?.Q<Label>("content");
            _source = element?.Q<Label>("source");
        }

        public void Bind(string heading, string clue, string source, Sprite icon)
        {
            if (_title != null) _title.text = heading ?? "CLUE";
            if (_content != null) _content.text = clue ?? string.Empty;
            if (_source != null) _source.text = source ?? string.Empty;
            if (_icon != null)
            {
                _icon.style.backgroundImage = icon == null ? StyleKeyword.None : new StyleBackground(icon);
                _icon.style.display = icon == null ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }

        public void Bind(ClueCardViewModel data)
        {
            if (data != null) Bind(data.Heading, data.Content, data.Source, data.Icon);
        }
    }
}
