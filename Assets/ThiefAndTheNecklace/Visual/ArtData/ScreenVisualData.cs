using UnityEngine;

namespace ThiefAndTheNecklace.Visual
{
    [CreateAssetMenu(fileName = "TAN_ScreenVisual", menuName = "Thief & the Necklace/Visual/Screen Art")]
    public sealed class ScreenVisualData : ScriptableObject
    {
        public string screenId;
        public Sprite background;
        public Sprite headerDecoration;
        public Sprite cornerDecoration;
        public Sprite accentDecoration;
    }
}
