using UnityEngine;
using UnityEngine.UIElements;
using ThiefAndTheNecklace.Visual;

namespace ThiefAndTheNecklace.Visual.UI
{
    public enum RoleCardVisualState { FaceDown, FaceUp, Selected, Disabled, Revealing, Revealed }

    public sealed class RoleCardViewModel
    {
        public string DisplayName = string.Empty;
        public Sprite Artwork;
        public int RoleValue;
        public bool FaceUp;
        public bool Selected;
        public bool Enabled = true;
    }

    public sealed class RoleCardView : VisualStateComponentBase
    {
        private readonly VisualElement _artwork;
        private readonly Label _roleName;
        private readonly Label _roleValue;
        private readonly VisualElement _backEmblem;
        private Label _missingArtLabel;
        private readonly CardPresenter _presenter;

        public RoleCardView(VisualElement element) : base(element?.Q<VisualElement>("role-card") ?? element)
        {
            _presenter = new CardPresenter(Root);
            _artwork = element?.Q<VisualElement>("artwork");
            _roleName = element?.Q<Label>("role-name");
            _roleValue = element?.Q<Label>("role-value");
            _backEmblem = element?.Q<VisualElement>("back-emblem");
        }

        public void Bind(string displayName, Sprite art, int value)
        {
            if (_roleName != null) _roleName.text = displayName ?? string.Empty;
            if (_roleValue != null) _roleValue.text = value.ToString();
            if (_artwork == null) return;

            if (art != null && art.texture != null)
            {
                _artwork.style.backgroundImage = new StyleBackground(art);
                _missingArtLabel?.RemoveFromHierarchy();
                _missingArtLabel = null;
                return;
            }

            _artwork.style.backgroundImage = StyleKeyword.None;
            if (_missingArtLabel == null)
            {
                _missingArtLabel = new Label();
                _missingArtLabel.AddToClassList("tan-visual-missing-art");
                _missingArtLabel.pickingMode = PickingMode.Ignore;
                _artwork.Add(_missingArtLabel);
            }
            _missingArtLabel.text = "MISSING ART\n" + (string.IsNullOrWhiteSpace(displayName) ? "Role card" : displayName);
        }

        public void Bind(string displayName, RoleVisualData artData, int value)
            => Bind(displayName, artData != null ? artData.cardArt : null, value);

        public void Bind(RoleCardViewModel data)
        {
            if (data == null) return;
            Bind(data.DisplayName, data.Artwork, data.RoleValue);
            SetState(data.FaceUp ? RoleCardVisualState.FaceUp : RoleCardVisualState.FaceDown);
            SetSelectedState(data.Selected);
            SetEnabledState(data.Enabled);
        }

        public void SetState(RoleCardVisualState state)
        {
            if (Root == null) return;
            foreach (RoleCardVisualState value in System.Enum.GetValues(typeof(RoleCardVisualState)))
                Root.EnableInClassList("tan-role-card--" + value.ToString().ToLowerInvariant(), value == state);
            if (state == RoleCardVisualState.FaceDown) ShowBack();
            else if (state != RoleCardVisualState.Disabled) ShowFront();
            Root.SetEnabled(state != RoleCardVisualState.Disabled);
        }

        public void ShowBack(Sprite universalBack = null)
        {
            if (Root == null) return;
            Root.AddToClassList("tan-role-card--back");
            if (_backEmblem != null && universalBack != null) _backEmblem.style.backgroundImage = new StyleBackground(universalBack);
            VisualEventBus.Raise(VisualEventType.CardHidden);
            VisualEventBus.Raise(VisualEventType.CardFlipped);
        }

        public void ShowFront()
        {
            if (Root == null) return;
            Root.RemoveFromClassList("tan-role-card--back");
            if (_backEmblem != null) _backEmblem.style.backgroundImage = StyleKeyword.None;
            VisualEventBus.Raise(VisualEventType.CardShown);
            VisualEventBus.Raise(VisualEventType.CardFlipped);
        }

        public override void SetSelectedState(bool selected)
        {
            base.SetSelectedState(selected);
            _presenter?.SetSelected(selected, false);
            VisualEventBus.Raise(selected ? VisualEventType.CardSelected : VisualEventType.CardDeselected);
        }
    }
}
