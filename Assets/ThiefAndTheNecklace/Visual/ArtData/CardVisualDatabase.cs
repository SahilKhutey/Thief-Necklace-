using UnityEngine;

namespace ThiefAndTheNecklace.Visual
{
    [CreateAssetMenu(fileName = "TAN_CardVisualDatabase", menuName = "Thief & the Necklace/Visual/Card Database")]
    public sealed class CardVisualDatabase : ScriptableObject
    {
        public Sprite universalBack;
        public Sprite roleFrame;
        public Sprite clueFrame;
        public Sprite scenarioFrame;
        public Sprite revealFrame;
        public Sprite clueArt;
        public Sprite scenarioArt;
        public Sprite necklaceMaster;
        public Sprite necklaceIcon;
        public Sprite necklaceReveal;
    }
}
