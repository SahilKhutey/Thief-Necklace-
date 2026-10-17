using UnityEngine;

namespace ThiefAndTheNecklace.Visual
{
    [CreateAssetMenu(fileName = "TAN_Icon", menuName = "Thief & the Necklace/Visual/Icon")]
    public sealed class IconVisualData : ScriptableObject
    {
        public string iconKey;
        public Sprite sprite;
        [Tooltip("Accessible name shown to assistive technology when no visible label is present.")]
        public string accessibleLabel;
    }
}
