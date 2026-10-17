using System;
using System.Collections.Generic;
using UnityEngine;

namespace ThiefAndTheNecklace.Visual
{
    [Serializable]
    public sealed class RoleVisualEntry
    {
        public string roleId;
        public RoleVisualData visual;
    }

    [CreateAssetMenu(fileName = "TAN_RoleVisualDatabase", menuName = "Thief & the Necklace/Visual/Role Database")]
    public sealed class RoleVisualDatabase : ScriptableObject
    {
        public List<RoleVisualEntry> entries = new List<RoleVisualEntry>();
        [Tooltip("Objective/reveal art only; never used as pre-reveal player role art.")]
        public Sprite thiefObjectiveRevealArt;
        [Tooltip("Reveal-only visual variant. Find(Thief) intentionally remains null before the Core authorizes reveal.")]
        public RoleVisualData thiefObjectiveRevealData;

        public RoleVisualData Find(string roleId)
        {
            if (string.IsNullOrEmpty(roleId) || string.Equals(roleId, "Thief", StringComparison.OrdinalIgnoreCase)) return null;
            var entry = entries.Find(item => item != null && string.Equals(item.roleId, roleId, StringComparison.OrdinalIgnoreCase));
            return entry != null ? entry.visual : null;
        }
    }
}
