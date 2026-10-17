using UnityEngine;

namespace ThiefAndTheNecklace.Visual
{
    public enum VisualCardFamily { Role, Clue, Scenario, UniversalBack, Reveal }

    [CreateAssetMenu(fileName = "TAN_CardVisual", menuName = "Thief & the Necklace/Visual/Card Art")]
    public sealed class CardVisualData : ScriptableObject
    {
        public VisualCardFamily family;
        public Sprite frame;
        public Sprite artWindow;
        public Sprite emblem;
        public Sprite universalBack;
    }
}
