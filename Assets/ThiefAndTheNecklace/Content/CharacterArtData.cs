using UnityEngine;

namespace ThiefAndTheNecklace.Content
{
    /// <summary>Art handoff reference for the role portrait, card illustration and emblem.</summary>
    [CreateAssetMenu(fileName = "CharacterArt", menuName = "Thief & the Necklace/Art/Character Art")]
    public sealed class CharacterArtData : ScriptableObject
    {
        public string roleId;
        public Sprite portrait;
        public Sprite cardIllustration;
        public Sprite emblem;
        [TextArea] public string artDirection;
    }
}
