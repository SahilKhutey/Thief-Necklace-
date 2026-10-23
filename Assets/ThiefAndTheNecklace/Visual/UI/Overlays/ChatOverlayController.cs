using System;
using System.Collections.Generic;
using ThiefAndTheNecklace.UI;
using UnityEngine;
using UnityEngine.UIElements;
using ThiefAndTheNecklace.Visual;

namespace ThiefAndTheNecklace.Visual.UI
{
    public sealed class ChatOverlayController
    {
        private readonly VisualElement _root;
        private readonly TextField _input;
        private readonly Label _validation;
        private readonly Button _send;
        private bool _sending;
        private Func<GameplayInputAction, bool> _actionAllowed = action => true;
        private readonly VisualElementPool<VisualElement> _messagePool = new VisualElementPool<VisualElement>(CreateMessageRow, 4, 48, ResetMessageRow);
        public event Action<string> MessageSubmitted;

        public ChatOverlayController(VisualElement root)
        {
            _root = root;
            _input = root?.Q<TextField>("chat-input");
            _validation = root?.Q<Label>("chat-validation");
            _send = root?.Q<Button>("send-message");
            if (_send != null) _send.clicked += Submit;
        }

        public void SetActionGuard(Func<GameplayInputAction, bool> actionAllowed)
        {
            _actionAllowed = actionAllowed ?? (action => true);
            if (!_sending && _send != null) _send.SetEnabled(_actionAllowed(GameplayInputAction.SendChat));
            if (!_sending && _input != null) _input.SetEnabled(_actionAllowed(GameplayInputAction.SendChat));
        }

        public void BindMessages(IReadOnlyList<ChatMessageViewModel> messages)
        {
            var container = _root?.Q<ScrollView>("chat-messages");
            if (container == null || messages == null) return;
            while (container.childCount > 0) _messagePool.Release(container[0]);
            foreach (var data in messages)
            {
                if (data == null) continue;
                var row = _messagePool.Acquire();
                row.EnableInClassList("tan-chat-message--local", data.IsLocalPlayer);
                row.EnableInClassList("tan-chat-message--system", data.IsSystem);
                row.Q<Label>("sender").text = data.IsSystem ? "PALACE NOTICE" : data.Sender ?? string.Empty;
                row.Q<Label>("timestamp").text = data.Timestamp ?? string.Empty;
                row.Q<Label>("content").text = data.Message ?? string.Empty;
                container.Add(row);
                AnimateEntry(row);
                VisualEventBus.Raise(VisualEventType.ChatMessageAdded);
            }
        }

        private static VisualElement CreateMessageRow()
        {
            var row = new VisualElement(); row.AddToClassList("tan-chat-message");
            var body = new VisualElement(); body.AddToClassList("tan-chat-message__body");
            var meta = new VisualElement(); meta.AddToClassList("tan-chat-message__meta");
            var sender = new Label { name = "sender" }; sender.AddToClassList("tan-chat-message__sender");
            var timestamp = new Label { name = "timestamp" }; timestamp.AddToClassList("tan-chat-message__timestamp");
            var content = new Label { name = "content" }; content.AddToClassList("tan-chat-message__content");
            meta.Add(sender); meta.Add(timestamp); body.Add(meta); body.Add(content); row.Add(body);
            return row;
        }

        private static void ResetMessageRow(VisualElement row)
        {
            row.EnableInClassList("tan-chat-message--local", false);
            row.EnableInClassList("tan-chat-message--system", false);
            row.Q<Label>("sender").text = string.Empty;
            row.Q<Label>("timestamp").text = string.Empty;
            row.Q<Label>("content").text = string.Empty;
        }

        private static void AnimateEntry(VisualElement row)
        {
            if (UIPresentationSettings.ReducedMotion) { row.style.opacity = 1f; row.style.translate = new Translate(0f, 0f); return; }
            var start = Time.realtimeSinceStartup;
            row.style.opacity = 0f;
            row.style.translate = new Translate(0f, 8f);
            IVisualElementScheduledItem item = null;
            item = row.schedule.Execute(() =>
            {
                if (row.panel == null) { item?.Pause(); return; }
                var t = Mathf.Clamp01((Time.realtimeSinceStartup - start) / 0.17f);
                row.style.opacity = t;
                row.style.translate = new Translate(0f, Mathf.Lerp(8f, 0f, t));
                if (t >= 1f) item?.Pause();
            }).Every(16);
        }

        private void Submit()
        {
            if (_sending || !_actionAllowed(GameplayInputAction.SendChat)) return;
            var text = _input?.value?.Trim() ?? string.Empty;
            if (text.Length == 0 || text.Length > 280)
            {
                if (_validation != null)
                {
                    _validation.text = text.Length == 0 ? "Write a message first." : "Message limit is 280 characters.";
                    _validation.style.display = DisplayStyle.Flex;
                }
                return;
            }
            _sending = true;
            if (_validation != null) { _validation.text = "Sending…"; _validation.style.display = DisplayStyle.Flex; }
            _input?.SetEnabled(false);
            _send?.SetEnabled(false);
            MessageSubmitted?.Invoke(text);
            if (MessageSubmitted == null) ConfirmMessageSent();
        }

        public void ConfirmMessageSent()
        {
            if (!_sending) return;
            _sending = false;
            if (_input != null) { _input.value = string.Empty; _input.SetEnabled(true); }
            if (_send != null) _send.SetEnabled(_actionAllowed(GameplayInputAction.SendChat));
            if (_validation != null) { _validation.text = "Sent"; _validation.style.display = DisplayStyle.Flex; }
        }

        public void ConfirmMessageFailed(string playerMessage = null)
        {
            if (!_sending) return;
            _sending = false;
            _input?.SetEnabled(true);
            if (_send != null) _send.SetEnabled(_actionAllowed(GameplayInputAction.SendChat));
            if (_validation != null)
            {
                _validation.text = string.IsNullOrWhiteSpace(playerMessage) ? "Message failed to send. Please retry." : playerMessage;
                _validation.style.display = DisplayStyle.Flex;
            }
        }
    }
}
