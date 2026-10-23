using System;
using ThiefAndTheNecklace.UI;
using UnityEngine.UIElements;

namespace ThiefAndTheNecklace.Visual.UI
{
    public sealed class LobbyScreenController
    {
        private readonly VisualElement _root;
        private readonly Button _ready;
        private readonly Button _start;
        private readonly Label _feedback;
        private bool _localReady;
        private bool _readyPending;
        private bool _startPending;
        private bool _canStart;
        private Func<GameplayInputAction, bool> _actionAllowed = action => true;
        public event Action<bool> ReadyChanged;
        public event Action StartRequested;

        public LobbyScreenController(VisualElement root)
        {
            _root = root;
            _ready = root?.Q<Button>("ready-toggle");
            _start = root?.Q<Button>("start-game");
            _feedback = new Label { name = "lobby-action-feedback" };
            _feedback.AddToClassList("tn-caption");
            root?.Add(_feedback);
            if (_ready != null) _ready.clicked += ToggleReady;
            if (_start != null) _start.clicked += RequestStart;
        }

        public void SetActionGuard(Func<GameplayInputAction, bool> actionAllowed)
        {
            _actionAllowed = actionAllowed ?? (action => true);
            if (_ready != null) _ready.SetEnabled(!_readyPending && _actionAllowed(GameplayInputAction.Ready));
            if (_start != null) _start.SetEnabled(!_startPending && _canStart && _actionAllowed(GameplayInputAction.StartMatch));
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
            _readyPending = false;
            _startPending = false;
            _canStart = data.CanStart;
            if (_feedback != null) _feedback.text = string.Empty;
            if (_ready != null) { _ready.text = _localReady ? "READY ✓" : "READY"; _ready.SetEnabled(_actionAllowed(GameplayInputAction.Ready)); }
            if (_start != null) { _start.text = "START"; _start.SetEnabled(_canStart && _actionAllowed(GameplayInputAction.StartMatch)); }
        }

        private void ToggleReady()
        {
            if (_readyPending || !_actionAllowed(GameplayInputAction.Ready)) return;
            _readyPending = true;
            if (_ready != null) { _ready.text = "UPDATING…"; _ready.SetEnabled(false); }
            if (_feedback != null) _feedback.text = "Updating ready status…";
            ReadyChanged?.Invoke(!_localReady);
        }

        private void RequestStart()
        {
            if (_startPending || !_actionAllowed(GameplayInputAction.StartMatch)) return;
            _startPending = true;
            if (_start != null) { _start.text = "STARTING…"; _start.SetEnabled(false); }
            StartRequested?.Invoke();
        }

        public void ConfirmReady(bool ready)
        {
            _localReady = ready;
            _readyPending = false;
            if (_ready != null) { _ready.text = ready ? "READY ✓" : "READY"; _ready.SetEnabled(_actionAllowed(GameplayInputAction.Ready)); }
            if (_feedback != null) _feedback.text = ready ? "You are ready." : "You are not ready.";
        }

        public void RejectReady(string message)
        {
            _readyPending = false;
            if (_ready != null) { _ready.text = _localReady ? "READY ✓" : "READY"; _ready.SetEnabled(_actionAllowed(GameplayInputAction.Ready)); }
            if (_feedback != null) _feedback.text = string.IsNullOrWhiteSpace(message) ? "Unable to update ready status." : message;
        }

        public void RejectStart(string message)
        {
            _startPending = false;
            if (_start != null) { _start.text = "START"; _start.SetEnabled(_canStart && _actionAllowed(GameplayInputAction.StartMatch)); }
            if (_feedback != null) _feedback.text = string.IsNullOrWhiteSpace(message) ? "Unable to start the match." : message;
        }
    }
}
