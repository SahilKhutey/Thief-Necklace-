using System;
using System.Collections.Generic;
using UnityEngine;

namespace ThiefAndTheNecklace.Visual
{
    [Serializable]
    public sealed class SpaceVisualEntry
    {
        public string spaceId;
        public SpaceVisualData visual;
    }

    [CreateAssetMenu(fileName = "TAN_SpaceVisualDatabase", menuName = "Thief & the Necklace/Visual/Space Database")]
    public sealed class SpaceVisualDatabase : ScriptableObject
    {
        public List<SpaceVisualEntry> entries = new List<SpaceVisualEntry>();

        public SpaceVisualData Find(string spaceId)
        {
            if (string.IsNullOrEmpty(spaceId)) return null;
            var entry = entries.Find(item => item != null && string.Equals(item.spaceId, spaceId, StringComparison.OrdinalIgnoreCase));
            return entry != null ? entry.visual : null;
        }
    }
}
