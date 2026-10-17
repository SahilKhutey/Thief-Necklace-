using System;
using UnityEngine.UIElements;

namespace ThiefAndTheNecklace.Visual.UI
{
    public sealed class LobbyScreenController
    {
        private readonly VisualElement _root;
        private readonly Button _ready;
        private readonly Button _start;
        private bool _localReady;
        public event Action<bool> ReadyChanged;
        public event Action StartRequested;

        public LobbyScreenController(VisualElement root)
        {
            _root = root;
            _ready = root?.Q<Button>("ready-toggle");
            _start = root?.Q<Button>("start-game");
            if (_ready != null) _ready.clicked += ToggleReady;
            if (_start != null) _start.clicked += () => StartRequested?.Invoke();
        }

        public void Bind(LobbyScreenViewModel data)
        {
            if (data == null) return;
            var status = _root?.Q<Label>("lobby-player-count");
            if (status != null) status.text = $"{data.PlayerCount} / {data.Capacity} PLAYERS  •  ROOM {data.RoomCode}";
            var grid = _root?.Q<VisualElement>("lobby-player-grid");
            if (grid != null && data.Players != null)
            {
                grid.Clear();
                foreach (var player in data.Players)
                {
                    if (player == null) continue;
                    var card = new VisualElement(); card.AddToClassList("tan-player-card");
                    var avatar = new VisualElement(); avatar.AddToClassList("tan-avatar");
                    var portrait = new VisualElement(); portrait.AddToClassList("tan-avatar__portrait");
                    if (player.Avatar != null) portrait.style.backgroundImage = new StyleBackground(player.Avatar);
                    avatar.Add(portrait);
                    var identity = new VisualElement(); identity.AddToClassList("tan-player-card__identity");
                    var name = new Label(player.DisplayName ?? string.Empty); name.AddToClassList("tan-player-card__name");
                    var playerStatus = new Label(!player.Connected ? "OFFLINE" : player.Ready ? "READY" : (player.Status ?? "WAITING")); playerStatus.AddToClassList("tan-player-card__status");
                    identity.Add(name); identity.Add(playerStatus);
                    card.Add(avatar); card.Add(identity); grid.Add(card);
                }
            }
            _localReady = data.LocalPlayerReady;
            if (_ready != null) _ready.text = _localReady ? "READY ✓" : "READY";
            if (_start != null) _start.SetEnabled(data.CanStart);
        }

        private void ToggleReady()
        {
            _localReady = !_localReady;
            if (_ready != null) _ready.text = _localReady ? "READY ✓" : "READY";
            ReadyChanged?.Invoke(_localReady);
        }
    }
}
