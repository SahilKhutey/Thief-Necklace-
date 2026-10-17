using UnityEngine;

namespace ThiefAndTheNecklace.Visual
{
    [CreateAssetMenu(fileName = "TAN_SpaceVisual", menuName = "Thief & the Necklace/Visual/Palace Space Art")]
    public sealed class SpaceVisualData : ScriptableObject
    {
        public string spaceId;
        public Sprite background;
        public Sprite midground;
        public Sprite thumbnail;
        public Sprite foreground;
        public Sprite[] props = new Sprite[0];
        public Sprite lightingOverlay;
        public Sprite atmosphere;
    }
}
