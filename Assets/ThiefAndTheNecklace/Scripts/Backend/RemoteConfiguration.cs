using System;
using System.Collections.Generic;
using System.Linq;

namespace ThiefAndTheNecklace.Backend
{
    [Serializable]
    public sealed class RemoteConfigSnapshot
    {
        public int configVersion;
        public long effectiveFromUtcTicks;
        public int discussionSeconds = 180;
        public int votingSeconds = 30;
        public int roundXp = 10;
        public int correctVoteXp = 15;
        public int matchXp = 50;
        public string[] enabledFeatures = new string[0];
        public ScenarioWeight[] scenarioWeights = new ScenarioWeight[0];
    }

    [Serializable]
    public sealed class ScenarioWeight
    {
        public string scenarioId;
        public int weight = 1;
    }

    public sealed class ConfigValidationResult
    {
        public readonly List<string> Errors = new List<string>();
        public bool IsValid => Errors.Count == 0;
    }

    public static class RemoteConfigValidator
    {
        public static ConfigValidationResult Validate(RemoteConfigSnapshot config)
        {
            var result = new ConfigValidationResult();
            if (config == null) { result.Errors.Add("Configuration is missing."); return result; }
            if (config.configVersion <= 0) result.Errors.Add("configVersion must be positive.");
            if (config.discussionSeconds < 60 || config.discussionSeconds > 600) result.Errors.Add("Discussion duration must be between 60 and 600 seconds.");
            if (config.votingSeconds < 10 || config.votingSeconds > 120) result.Errors.Add("Voting duration must be between 10 and 120 seconds.");
            if (config.roundXp < 0 || config.roundXp > 1000 || config.correctVoteXp < 0 || config.correctVoteXp > 1000 || config.matchXp < 0 || config.matchXp > 5000)
                result.Errors.Add("XP values are outside their safe ranges.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in config.scenarioWeights ?? new ScenarioWeight[0])
                if (entry == null || string.IsNullOrWhiteSpace(entry.scenarioId) || entry.weight < 0 || entry.weight > 10000 || !ids.Add(entry.scenarioId))
                    result.Errors.Add("Scenario weights must have unique IDs and nonnegative bounded values.");
            var flags = new HashSet<string>(StringComparer.Ordinal);
            foreach (var flag in config.enabledFeatures ?? new string[0])
                if (string.IsNullOrWhiteSpace(flag) || !flags.Add(flag)) result.Errors.Add("Feature flag IDs must be nonempty and unique.");
            return result;
        }
    }

    /// <summary>Last-known-good snapshot remains active if a downloaded config fails validation.</summary>
    public sealed class RemoteConfigService
    {
        private RemoteConfigSnapshot _active;
        public RemoteConfigSnapshot Active => Clone(_active);
        public RemoteConfigService(RemoteConfigSnapshot safeDefaults)
        {
            var validation = RemoteConfigValidator.Validate(safeDefaults);
            if (!validation.IsValid) throw new ArgumentException("Safe defaults are invalid: " + string.Join("; ", validation.Errors));
            _active = Clone(safeDefaults);
        }

        public bool TryActivate(RemoteConfigSnapshot candidate, out IReadOnlyList<string> errors)
        {
            var validation = RemoteConfigValidator.Validate(candidate);
            if (validation.IsValid && candidate.configVersion <= _active.configVersion)
                validation.Errors.Add("Configuration version must be newer than the active version.");
            errors = validation.Errors.AsReadOnly();
            if (!validation.IsValid) return false;
            _active = Clone(candidate);
            return true;
        }

        public bool IsEnabled(string featureId) => _active.enabledFeatures != null && _active.enabledFeatures.Contains(featureId);

        private static RemoteConfigSnapshot Clone(RemoteConfigSnapshot source) => new RemoteConfigSnapshot
        {
            configVersion = source.configVersion,
            effectiveFromUtcTicks = source.effectiveFromUtcTicks,
            discussionSeconds = source.discussionSeconds,
            votingSeconds = source.votingSeconds,
            roundXp = source.roundXp,
            correctVoteXp = source.correctVoteXp,
            matchXp = source.matchXp,
            enabledFeatures = (source.enabledFeatures ?? new string[0]).ToArray(),
            scenarioWeights = (source.scenarioWeights ?? new ScenarioWeight[0]).Where(x => x != null).Select(x => new ScenarioWeight { scenarioId = x.scenarioId, weight = x.weight }).ToArray()
        };
    }
}
