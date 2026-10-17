using UnityEngine;

namespace ThiefAndTheNecklace.Visual
{
    /// <summary>Replaceable role artwork. Hidden Thief status is deliberately not represented here.</summary>
    [CreateAssetMenu(fileName = "TAN_RoleVisual", menuName = "Thief & the Necklace/Visual/Role Art")]
    public sealed class RoleVisualData : ScriptableObject
    {
        public string roleId;
        public Sprite portrait;
        public Sprite fullBody;
        public Sprite cardArt;
        public Sprite avatar;
        public Sprite silhouette;
        public Sprite roleIcon;
        [TextArea] public string artDirection;
    }
}
