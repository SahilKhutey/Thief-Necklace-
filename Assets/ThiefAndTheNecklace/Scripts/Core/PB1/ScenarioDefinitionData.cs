using System;
using System.Collections.Generic;
using System.Linq;

namespace ThiefAndTheNecklace.PB1
{
    [Serializable]
    public sealed class ScenarioEvent
    {
        public string EventId;
        public float Time;
        public string Location;
        public string Description;
    }

    [Serializable]
    public sealed class ScenarioDefinitionData
    {
        public string ScenarioId;
        public string Title;
        public string Introduction;
        public List<ScenarioEvent> Events = new List<ScenarioEvent>();
        public List<PlayerScenarioInfo> PlayerInformation = new List<PlayerScenarioInfo>();
        public string ThiefSituation;
        public List<string> ThiefClues = new List<string>();
        public bool HasThiefOverride;
        public ScenarioTruthModel TruthModel;
    }
}
