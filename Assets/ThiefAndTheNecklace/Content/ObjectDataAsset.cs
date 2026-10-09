using UnityEngine;

namespace ThiefAndTheNecklace.Content
{
    [CreateAssetMenu(fileName = "ObjectData", menuName = "Thief & the Necklace/Scenario/Object")]
    public sealed class ObjectDataAsset : ScriptableObject
    {
        public string objectId;
        public string displayName;
        public string ownerRoleId;
        public string locationId;
        public Sprite icon;
        public Sprite artwork;
        public ThiefAndTheNecklace.ScenarioObjectData ToData() => new ThiefAndTheNecklace.ScenarioObjectData
        { id = objectId, displayName = displayName, ownerRoleId = ownerRoleId, locationId = locationId, importance = "critical" };
    }
}
