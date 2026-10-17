using System;
using System.Collections.Generic;
using UnityEngine;

namespace ThiefAndTheNecklace.Visual
{
    [Serializable]
    public sealed class PropVisualEntry
    {
        public string propId;
        public PropVisualData visual;
    }

    [CreateAssetMenu(fileName = "TAN_PropVisualDatabase", menuName = "Thief & the Necklace/Visual/Prop Database")]
    public sealed class PropVisualDatabase : ScriptableObject
    {
        public List<PropVisualEntry> entries = new List<PropVisualEntry>();

        public PropVisualData Find(string propId)
        {
            if (string.IsNullOrWhiteSpace(propId)) return null;
            var entry = entries.Find(item => item != null && string.Equals(item.propId, propId, StringComparison.OrdinalIgnoreCase));
            return entry != null ? entry.visual : null;
        }
    }
}
