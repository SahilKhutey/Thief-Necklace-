using UnityEngine.UIElements;
using ThiefAndTheNecklace.Visual;

namespace ThiefAndTheNecklace.Visual.UI
{
    /// <summary>Only public/player-visible fields belong here; never bind private role truth.</summary>
    [System.Serializable]
    public sealed class PlayerCardViewModel
    {
        public string PlayerId = string.Empty;
        public string DisplayName = string.Empty;
        public string Status = string.Empty;
        public UnityEngine.Sprite Avatar;
        public int Score;
        public bool Connected = true;
        public bool Ready;
        public bool Selected;
        public bool Accused;
        public bool Revealed;
        public bool Speaking;
        public bool Voted;
    }

    public sealed class PlayerCardView : VisualStateComponentBase
    {
        private readonly AvatarView _avatar;
        private readonly Label _name;
        private readonly Label _status;
        private readonly Label _score;
        private readonly Label _state;
        private bool _hasBound;
        private bool _wasSelected;
        private readonly CardPresenter _presenter;
        private readonly CharacterPresentationController _character;

        public PlayerCardView(VisualElement element) : base(element?.Q<VisualElement>("player-card") ?? element)
        {
            _presenter = new CardPresenter(Root);
            var avatarElement = element?.Q<VisualElement>("avatar");
            _avatar = new AvatarView(avatarElement);
            _character = new CharacterPresentationController(avatarElement);
            _name = element?.Q<Label>("name");
            _status = element?.Q<Label>("status");
            _score = element?.Q<Label>("score");
            _state = element?.Q<Label>("state");
        }

        public void Bind(PlayerCardViewModel data)
        {
            if (data == null) return;
            _avatar.Bind(data.Avatar, data.DisplayName, data.Connected ? data.Status : "OFFLINE");
            _character?.SetState(data.Accused ? VisualCharacterState.Accused : data.Speaking ? VisualCharacterState.Speaking : data.Selected ? VisualCharacterState.Selected : data.Revealed ? VisualCharacterState.Revealed : VisualCharacterState.Idle);
            if (_name != null) _name.text = data.DisplayName ?? string.Empty;
            if (_status != null) _status.text = data.Status ?? string.Empty;
            if (_score != null) _score.text = data.Score.ToString();
            if (_state != null) _state.text = data.Accused ? "ACCUSED" : data.Selected ? "SELECTED" : data.Voted ? "VOTED" : data.Speaking ? "SPEAKING" : data.Ready ? "READY" : data.Connected ? string.Empty : "OFFLINE";
            Root?.EnableInClassList("tan-player-card--disconnected", !data.Connected);
            Root?.EnableInClassList("tan-player-card--selected", data.Selected);
            Root?.EnableInClassList("tan-player-card--accused", data.Accused);
            Root?.EnableInClassList("tan-player-card--revealed", data.Revealed);
            Root?.EnableInClassList("tan-player-card--speaking", data.Speaking);
            Root?.EnableInClassList("tan-player-card--voted", data.Voted);
            _presenter?.SetSelected(data.Selected, false);
            if (_hasBound && _wasSelected != data.Selected)
                VisualEventBus.Raise(data.Selected ? VisualEventType.PlayerSelected : VisualEventType.PlayerDeselected);
            _wasSelected = data.Selected;
            _hasBound = true;
        }
    }
}
