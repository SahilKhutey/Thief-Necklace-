using System;
using UnityEngine.UIElements;

namespace ThiefAndTheNecklace.Visual.UI
{
    public sealed class RoomEntryController
    {
        private readonly VisualElement _root;
        private readonly bool _join;
        public event Action<string> RoomRequested;

        public RoomEntryController(VisualElement root, bool join)
        {
            _root = root;
            _join = join;
            var field = root?.Q<TextField>(join ? "room-code" : "room-name");
            var submit = root?.Q<Button>(join ? "join-room-submit" : "create-room-submit");
            if (submit != null) submit.clicked += () => Submit(field);
            if (field != null) field.RegisterValueChangedCallback(_ => SetError(string.Empty));
            SetError(string.Empty);
        }

        private void Submit(TextField field)
        {
            var value = field?.value?.Trim() ?? string.Empty;
            if (_join)
            {
                value = value.ToUpperInvariant();
                if (value.Length != 5 || !IsRoomCode(value)) { SetError("Enter a valid 5-character room code."); return; }
            }
            else if (value.Length < 3 || value.Length > 24)
            {
                SetError("Room name must be 3–24 characters.");
                return;
            }
            SetError(string.Empty);
            RoomRequested?.Invoke(value);
        }

        private static bool IsRoomCode(string value)
        {
            foreach (var character in value)
                if (!(character >= 'A' && character <= 'Z') && !(character >= '0' && character <= '9')) return false;
            return true;
        }

        private void SetError(string message)
        {
            var status = _root?.Q<Label>("validation-message");
            if (status == null) return;
            status.text = message;
            status.style.display = string.IsNullOrEmpty(message) ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
