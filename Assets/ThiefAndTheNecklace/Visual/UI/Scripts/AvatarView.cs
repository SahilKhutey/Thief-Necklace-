using UnityEngine;
using UnityEngine.UIElements;

namespace ThiefAndTheNecklace.Visual.UI
{
    public enum AvatarVisualState { Normal, Selected, Talking, Disconnected, Winner, Accused }

    public sealed class AvatarView : VisualStateComponentBase
    {
        private readonly VisualElement _container;
        private readonly Label _name;
        private readonly Label _status;

        public AvatarView(VisualElement element) : base(element?.Q<VisualElement>("portrait") ?? element)
        {
            _container = element;
            _name = element?.Q<Label>("avatar-name");
            _status = element?.Q<Label>("avatar-status");
        }

        public void Bind(Sprite portrait, string displayName, string status)
        {
            if (Root != null) Root.style.backgroundImage = portrait == null ? StyleKeyword.None : new StyleBackground(portrait);
            if (_name != null) _name.text = displayName ?? string.Empty;
            if (_status != null) _status.text = status ?? string.Empty;
        }

        public void SetVisible(bool visible)
        {
            if (_container != null) _container.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void SetState(AvatarVisualState state)
        {
            if (Root == null) return;
            foreach (AvatarVisualState value in System.Enum.GetValues(typeof(AvatarVisualState)))
                Root.EnableInClassList("tan-avatar--" + value.ToString().ToLowerInvariant(), value == state);
        }
    }
}
