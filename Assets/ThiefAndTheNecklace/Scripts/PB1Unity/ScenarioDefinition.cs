using System.Collections.Generic;
using UnityEngine;
using ThiefAndTheNecklace.PB1;

namespace ThiefAndTheNecklace.PB1
{
    [CreateAssetMenu(fileName = "ScenarioDefinition", menuName = "Thief & the Necklace/PB1 Scenario Definition")]
    public sealed class ScenarioDefinition : ScriptableObject
    {
        [SerializeField] private string scenarioId;
        [SerializeField] private string title;
        [SerializeField, TextArea(2, 5)] private string introduction;
        [SerializeField] private List<ScenarioEvent> events = new List<ScenarioEvent>();
        [SerializeField] private List<PlayerScenarioInfo> playerInformation = new List<PlayerScenarioInfo>();
        [SerializeField, TextArea(2, 5)] private string thiefSituation;
        [SerializeField] private List<string> thiefClues = new List<string>();
        [SerializeField] private bool hasThiefOverride = true;
        [SerializeField] private ScenarioTruthModel truthModel;

        public string ScenarioId { get { return scenarioId; } }
        public string Title { get { return title; } }

        public static ScenarioDefinition CreateDefault()
        {
            var data = DefaultScenarioFactory.CreateRoyalNecklace();
            var asset = CreateInstance<ScenarioDefinition>();
            asset.scenarioId = data.ScenarioId;
            asset.title = data.Title;
            asset.introduction = data.Introduction;
            asset.events = new List<ScenarioEvent>();
            foreach (var item in data.Events)
                asset.events.Add(new ScenarioEvent { EventId = item.EventId, Time = item.Time, Location = item.Location, Description = item.Description });
            asset.playerInformation = new List<PlayerScenarioInfo>();
            foreach (var item in data.PlayerInformation)
                asset.playerInformation.Add(new PlayerScenarioInfo { Role = item.Role, PrivateSituation = item.PrivateSituation, Clues = new List<string>(item.Clues) });
            asset.thiefSituation = data.ThiefSituation;
            asset.thiefClues = new List<string>(data.ThiefClues);
            asset.hasThiefOverride = data.HasThiefOverride;
            asset.truthModel = data.TruthModel == null ? null : data.TruthModel.DeepCopy();
            return asset;
        }

        public ScenarioDefinitionData ToRuntime()
        {
            var runtime = new ScenarioDefinitionData
            {
                ScenarioId = scenarioId,
                Title = title,
                Introduction = introduction,
                Events = new List<ScenarioEvent>(),
                PlayerInformation = new List<PlayerScenarioInfo>(),
                ThiefSituation = thiefSituation,
                ThiefClues = new List<string>(thiefClues ?? new List<string>()),
                HasThiefOverride = hasThiefOverride,
                TruthModel = truthModel == null ? null : truthModel.DeepCopy()
            };
            if (runtime.TruthModel == null && scenarioId == "royal-necklace") runtime.TruthModel = DefaultScenarioFactory.CreateRoyalNecklaceTruth();
            foreach (var scenarioEvent in events ?? new List<ScenarioEvent>())
            {
                if (scenarioEvent == null) continue;
                runtime.Events.Add(new ScenarioEvent
                {
                    EventId = scenarioEvent.EventId,
                    Time = scenarioEvent.Time,
                    Location = scenarioEvent.Location,
                    Description = scenarioEvent.Description
                });
            }
            foreach (var info in playerInformation ?? new List<PlayerScenarioInfo>())
            {
                if (info == null) continue;
                runtime.PlayerInformation.Add(new PlayerScenarioInfo
                {
                    Role = info.Role,
                    PrivateSituation = info.PrivateSituation,
                    Clues = new List<string>(info.Clues ?? new List<string>())
                });
            }
            return runtime;
        }
    }
}
