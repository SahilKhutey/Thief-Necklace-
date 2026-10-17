using UnityEngine;
using UnityEngine.UIElements;

namespace ThiefAndTheNecklace.Visual.UI
{
    public sealed class ChatMessageViewModel
    {
        public string Sender = string.Empty;
        public string Message = string.Empty;
        public string Timestamp = string.Empty;
        public Sprite Avatar;
        public bool IsLocalPlayer;
        public bool IsSystem;
    }

    public sealed class ChatMessageView : VisualStateComponentBase
    {
        private readonly AvatarView _avatar;
        private readonly Label _sender;
        private readonly Label _content;
        private readonly Label _timestamp;

        public ChatMessageView(VisualElement element) : base(element?.Q<VisualElement>("message") ?? element)
        {
            _avatar = new AvatarView(element?.Q<VisualElement>("avatar"));
            _sender = element?.Q<Label>("sender");
            _content = element?.Q<Label>("content");
            _timestamp = element?.Q<Label>("timestamp");
        }

        public void Bind(ChatMessageViewModel data)
        {
            if (data == null) return;
            if (_sender != null) _sender.text = data.IsSystem ? "PALACE NOTICE" : data.Sender ?? string.Empty;
            if (_content != null) _content.text = data.Message ?? string.Empty;
            if (_timestamp != null) _timestamp.text = data.Timestamp ?? string.Empty;
            _avatar.Bind(data.Avatar, data.Sender, null);
            Root?.EnableInClassList("tan-chat-message--local", data.IsLocalPlayer);
            Root?.EnableInClassList("tan-chat-message--system", data.IsSystem);
            _avatar.SetVisible(!data.IsSystem);
            VisualEventBus.Raise(VisualEventType.MessageAdded);
        }
    }
}
