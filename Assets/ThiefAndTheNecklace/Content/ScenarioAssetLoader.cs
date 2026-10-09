using UnityEngine;

namespace ThiefAndTheNecklace.Content
{
    public static class ScenarioAssetLoader
    {
        public static ThiefAndTheNecklace.ScenarioRegistry LoadResourcesOrDefault(string resourcePath = "ScenarioRegistry")
        {
            var asset = Resources.Load<ScenarioRegistryAsset>(resourcePath);
            if (asset == null) return ThiefAndTheNecklace.DefaultScenarioCatalog.CreateRegistry();
            try
            {
                var registry = asset.ToRegistry();
                foreach (var scenario in registry.All)
                for (var count = 4; count <= 6; count++)
                {
                    var validation = ThiefAndTheNecklace.ScenarioTemplateValidator.Validate(scenario, count);
                    if (!validation.IsValid) throw new System.InvalidOperationException(string.Join("; ", validation.Errors));
                }
                return registry;
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("Scenario registry is invalid; using built-in fallback content. " + exception.Message);
                return ThiefAndTheNecklace.DefaultScenarioCatalog.CreateRegistry();
            }
        }
    }
}
