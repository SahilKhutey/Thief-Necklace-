using System.Linq;
using UnityEngine;

namespace ThiefAndTheNecklace.Content
{
    [CreateAssetMenu(fileName = "ScenarioRegistry", menuName = "Thief & the Necklace/Scenario Registry")]
    public sealed class ScenarioRegistryAsset : ScriptableObject
    {
        public ScenarioDataAsset[] scenarios = new ScenarioDataAsset[0];

        public ThiefAndTheNecklace.ScenarioRegistry ToRegistry()
        {
            return new ThiefAndTheNecklace.ScenarioRegistry((scenarios ?? new ScenarioDataAsset[0])
                .Where(asset => asset != null && asset.template != null)
                .Select(asset => asset.ToTemplate()));
        }
    }
}
