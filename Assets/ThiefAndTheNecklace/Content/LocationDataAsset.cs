using System.Linq;
using UnityEngine;

namespace ThiefAndTheNecklace.Content
{
    [CreateAssetMenu(fileName = "LocationData", menuName = "Thief & the Necklace/Scenario/Location")]
    public sealed class LocationDataAsset : ScriptableObject
    {
        public string locationId;
        public string displayName;
        public LocationDataAsset[] connectedLocations = new LocationDataAsset[0];
        public bool publicArea;
        public bool restricted;
        public string[] accessTags = new string[0];

        public ThiefAndTheNecklace.ScenarioLocationData ToData() => new ThiefAndTheNecklace.ScenarioLocationData
        {
            id = locationId,
            displayName = displayName,
            connectedLocationIds = (connectedLocations ?? new LocationDataAsset[0]).Where(x => x != null).Select(x => x.locationId).ToArray(),
            publicArea = publicArea,
            restricted = restricted,
            accessTags = accessTags ?? new string[0]
        };
    }
}
