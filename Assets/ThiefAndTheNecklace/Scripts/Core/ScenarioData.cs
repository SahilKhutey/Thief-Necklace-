using System;
using System.Collections.Generic;

namespace ThiefAndTheNecklace
{
    public enum InformationType { Location, Time, Witness, Action, Object, Relationship }
    public enum TruthType { HardFact, Observation, PartialTruth, Deception }
    public enum EventType { Gathering, Movement, Conversation, Observation, Access, Distraction, Theft, Escape, Discovery }
    public enum ScenarioDifficulty { Easy, Normal, Hard }

    [Serializable]
    public sealed class ScenarioLocationData
    {
        public string id;
        public string displayName;
        public string[] connectedLocationIds = new string[0];
        public bool publicArea;
        public bool restricted;
        public string[] accessTags = new string[0];
    }

    [Serializable]
    public sealed class ScenarioObjectData
    {
        public string id;
        public string displayName;
        public string ownerRoleId;
        public string locationId;
        public string importance;
    }

    [Serializable]
    public sealed class ScenarioRoleData
    {
        public string id;
        public string displayName;
        public int pointValue;
        public string description;
        public string[] tags = new string[0];
        public string[] preferredInformation = new string[0];
        public string[] preferredLocations = new string[0];
    }

    [Serializable]
    public sealed class EventTemplate
    {
        public string id;
        public EventType type;
        public string actorRoleId;
        public bool actorIsThief;
        public bool actorIsFalseLead;
        public bool optionalIfRoleUnavailable;
        public string locationId;
        public bool useTargetLocation;
        public bool useEscapeLocation;
        public string targetObjectId;
        public string[] requiresEventIds = new string[0];
        public int minuteOffset;
        public bool critical;
    }

    [Serializable]
    public sealed class ClueTemplate
    {
        public string id;
        public string eventId;
        public EventType sourceEventType;
        public string preferredRoleId;
        public InformationType informationType;
        public TruthType truthType;
        public int strength = 1;
        public bool criticalEvidence;
        public bool falseLead;
        public string text;
        public string ambiguousText;
    }

    [Serializable]
    public sealed class DifficultyProfile
    {
        public ScenarioDifficulty difficulty = ScenarioDifficulty.Normal;
        public int minCluesPerPlayer = 1;
        public int maxCluesPerPlayer = 2;
        public int minimumEvidencePaths = 2;
        public int falseLeadCount = 1;
        public int ambiguityCount = 1;
    }

    [Serializable]
    public sealed class ScenarioTemplate
    {
        public string scenarioId;
        public int version = 1;
        public string themeTag;
        public string title;
        public string publicDescription;
        public string distractionName;
        public string targetLocationId;
        public string escapeLocationId;
        public ScenarioLocationData[] locations = new ScenarioLocationData[0];
        public ScenarioObjectData[] objects = new ScenarioObjectData[0];
        public ScenarioRoleData[] roles = new ScenarioRoleData[0];
        public EventTemplate[] eventTemplates = new EventTemplate[0];
        public ClueTemplate[] clueTemplates = new ClueTemplate[0];
        public DifficultyProfile[] difficulties = new DifficultyProfile[0];
    }

    [Serializable]
    public sealed class ScenarioEvent
    {
        public string eventId;
        public int sequence;
        public int minute;
        public EventType eventType;
        public string actorPlayerId;
        public string actorRoleId;
        public string locationId;
        public string targetObjectId;
        public string description;
        public string[] requiresEventIds = new string[0];
        public bool critical;
    }

    [Serializable]
    public sealed class RuntimeClue
    {
        public string clueId;
        public string ownerPlayerId;
        public string sourceEventId;
        public InformationType informationType;
        public TruthType truthType;
        public int strength;
        public bool criticalEvidence;
        public bool falseLead;
        public string text;
    }

    [Serializable]
    public sealed class PlayerSecret
    {
        public string playerId;
        public string roleId;
        public bool isThief;
        public string privateSituation;
        public List<string> clueIds = new List<string>();
    }

    public sealed class WorldState
    {
        public IReadOnlyList<ScenarioLocationData> Locations { get; private set; }
        public IReadOnlyList<ScenarioObjectData> Objects { get; private set; }
        public List<ScenarioEvent> Events { get; private set; }

        public WorldState(IReadOnlyList<ScenarioLocationData> locations, IReadOnlyList<ScenarioObjectData> objects)
        {
            Locations = locations ?? throw new ArgumentNullException(nameof(locations));
            Objects = objects ?? throw new ArgumentNullException(nameof(objects));
            Events = new List<ScenarioEvent>();
        }
    }

    public sealed class RuntimeScenario
    {
        public string scenarioId;
        public int scenarioVersion;
        public int seed;
        public string scenarioHash;
        public int playerCount;
        public ScenarioDifficulty difficulty;
        public string title;
        public string publicDescription;
        public string targetObjectId;
        public string thiefPlayerId;
        public WorldState world;
        public List<ScenarioEvent> events = new List<ScenarioEvent>();
        public List<RuntimeClue> clues = new List<RuntimeClue>();
        public List<PlayerSecret> playerSecrets = new List<PlayerSecret>();
        public EvidenceGraph evidenceGraph;
        public string publicScenarioHash;
        public int generationAttempts;
        public bool usedFallback;
        public string fallbackReason;
    }

    public sealed class ScenarioReplayDescriptor
    {
        public string roundId;
        public string scenarioId;
        public int scenarioVersion;
        public int seed;
        public int playerCount;
        public ScenarioDifficulty difficulty;
        public string authorityScenarioHash;

        public static ScenarioReplayDescriptor From(RuntimeScenario scenario, int roundNumber)
        {
            if (scenario == null) throw new ArgumentNullException(nameof(scenario));
            return new ScenarioReplayDescriptor
            {
                roundId = "R" + roundNumber.ToString(System.Globalization.CultureInfo.InvariantCulture) + "-" + scenario.scenarioId + "-" + scenario.seed.ToString(System.Globalization.CultureInfo.InvariantCulture),
                scenarioId = scenario.scenarioId,
                scenarioVersion = scenario.scenarioVersion,
                seed = scenario.seed,
                playerCount = scenario.playerCount,
                difficulty = scenario.difficulty,
                authorityScenarioHash = scenario.scenarioHash
            };
        }
    }

    public sealed class ScenarioQualityReport
    {
        public string ScenarioId;
        public string ScenarioHash;
        public int ClueCount;
        public int DistinctEvidencePaths;
        public int FalseLeadCount;
        public int AmbiguousClueCount;
        public int ThiefClueCount;
        public int GenerationAttempts;
        public bool UsedFallback;

        public static ScenarioQualityReport From(RuntimeScenario scenario)
        {
            if (scenario == null) throw new ArgumentNullException(nameof(scenario));
            return new ScenarioQualityReport
            {
                ScenarioId = scenario.scenarioId,
                ScenarioHash = scenario.scenarioHash,
                ClueCount = scenario.clues.Count,
                DistinctEvidencePaths = scenario.clues.Where(c => c.criticalEvidence).Select(c => c.sourceEventId).Distinct(StringComparer.Ordinal).Count(),
                FalseLeadCount = scenario.clues.Count(c => c.falseLead),
                AmbiguousClueCount = scenario.clues.Count(c => c.truthType == TruthType.PartialTruth),
                ThiefClueCount = scenario.clues.Count(c => c.ownerPlayerId == scenario.thiefPlayerId),
                GenerationAttempts = scenario.generationAttempts,
                UsedFallback = scenario.usedFallback
            };
        }
    }

    public sealed class ValidationResult
    {
        public bool IsValid => Errors.Count == 0;
        public readonly List<string> Errors = new List<string>();
        public void AddError(string error) => Errors.Add(error);
    }

    public sealed class ScenarioGenerationException : Exception
    {
        public ScenarioGenerationException(string message) : base(message) { }
    }
}
