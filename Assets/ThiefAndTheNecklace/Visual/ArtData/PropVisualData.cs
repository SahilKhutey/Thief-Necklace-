using UnityEngine;

namespace ThiefAndTheNecklace.Visual
{
    [CreateAssetMenu(fileName = "TAN_PropVisual", menuName = "Thief & the Necklace/Visual/Prop Art")]
    public sealed class PropVisualData : ScriptableObject
    {
        public string propId;
        public Sprite worldSprite;
        public Sprite cardSprite;
        public Sprite icon;
    }
}
