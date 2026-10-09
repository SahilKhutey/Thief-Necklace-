using UnityEngine;
using System.Linq;

namespace ThiefAndTheNecklace.Content
{
    [CreateAssetMenu(fileName = "ScenarioData", menuName = "Thief & the Necklace/Scenario")]
    public sealed class ScenarioDataAsset : ScriptableObject
    {
        public ThiefAndTheNecklace.ScenarioTemplate template = new ThiefAndTheNecklace.ScenarioTemplate();
        public LocationDataAsset[] locationAssets = new LocationDataAsset[0];
        public ObjectDataAsset[] objectAssets = new ObjectDataAsset[0];
        public RoleDataAsset[] roleAssets = new RoleDataAsset[0];

        public ThiefAndTheNecklace.ScenarioTemplate ToTemplate()
        {
            if (template == null) return null;
            return new ThiefAndTheNecklace.ScenarioTemplate
            {
                scenarioId = template.scenarioId,
                version = template.version,
                themeTag = template.themeTag,
                title = template.title,
                publicDescription = template.publicDescription,
                distractionName = template.distractionName,
                targetLocationId = template.targetLocationId,
                escapeLocationId = template.escapeLocationId,
                locations = locationAssets != null && locationAssets.Any(x => x != null) ? locationAssets.Where(x => x != null).Select(x => x.ToData()).ToArray() : template.locations,
                objects = objectAssets != null && objectAssets.Any(x => x != null) ? objectAssets.Where(x => x != null).Select(x => x.ToData()).ToArray() : template.objects,
                roles = roleAssets != null && roleAssets.Any(x => x != null) ? roleAssets.Where(x => x != null).Select(x => x.ToData()).ToArray() : template.roles,
                eventTemplates = template.eventTemplates,
                clueTemplates = template.clueTemplates,
                difficulties = template.difficulties
            };
        }
    }
}
