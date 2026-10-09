using UnityEngine;

namespace ThiefAndTheNecklace.Content
{
    /// <summary>Optional scenario-specific presentation; gameplay content stays in ScenarioTemplate.</summary>
    [CreateAssetMenu(fileName = "ScenarioArt", menuName = "Thief & the Necklace/Art/Scenario Art")]
    public sealed class ScenarioArtData : ScriptableObject
    {
        public string scenarioId;
        public Sprite keyIllustration;
        public Sprite locationIllustration;
        public Sprite objectIllustration;
        [TextArea] public string artDirection;
    }
}
