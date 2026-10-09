using UnityEngine;

namespace ThiefAndTheNecklace.Content
{
    [CreateAssetMenu(fileName = "RoleData", menuName = "Thief & the Necklace/Role")]
    public sealed class RoleDataAsset : ScriptableObject
    {
        public string roleId;
        public string displayName;
        public int pointValue;
        [TextArea] public string description;
        public Sprite portrait;
        public Sprite cardArt;
        public Sprite emblem;
        public string[] tags = new string[0];
        public string[] preferredInformation = new string[0];
        public string[] preferredLocations = new string[0];

        public ThiefAndTheNecklace.ScenarioRoleData ToData() => new ThiefAndTheNecklace.ScenarioRoleData
        {
            id = roleId, displayName = displayName, pointValue = pointValue, description = description,
            tags = tags ?? new string[0], preferredInformation = preferredInformation ?? new string[0],
            preferredLocations = preferredLocations ?? new string[0]
        };
    }
}
