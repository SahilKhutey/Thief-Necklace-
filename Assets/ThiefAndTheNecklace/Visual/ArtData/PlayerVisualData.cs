using UnityEngine;

namespace ThiefAndTheNecklace.Visual
{
    [CreateAssetMenu(fileName = "TAN_PlayerVisual", menuName = "Thief & the Necklace/Visual/Player Art")]
    public sealed class PlayerVisualData : ScriptableObject
    {
        public Sprite defaultAvatar;
        public Sprite connectedIndicator;
        public Sprite disconnectedIndicator;
        public Sprite readyIndicator;
        public Sprite waitingIndicator;
    }
}
