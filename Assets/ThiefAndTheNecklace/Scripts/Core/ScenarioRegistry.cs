using System;
using System.Collections.Generic;
using System.Linq;

namespace ThiefAndTheNecklace
{
    public interface IRoundSource
    {
        RuntimeScenario Create(int playerCount, int seed, ScenarioDifficulty difficulty = ScenarioDifficulty.Normal);
        RuntimeScenario Create(IReadOnlyList<PlayerData> players, int seed, ScenarioDifficulty difficulty = ScenarioDifficulty.Normal);
        RuntimeScenario CreateForScenario(string scenarioId, int playerCount, int seed, ScenarioDifficulty difficulty = ScenarioDifficulty.Normal);
        RuntimeScenario CreateForScenario(string scenarioId, IReadOnlyList<PlayerData> players, int seed, ScenarioDifficulty difficulty = ScenarioDifficulty.Normal);
    }

    public sealed class ScenarioRegistry
    {
        private readonly Dictionary<string, ScenarioTemplate> _scenarios;

        public ScenarioRegistry(IEnumerable<ScenarioTemplate> scenarios)
        {
            if (scenarios == null) throw new ArgumentNullException(nameof(scenarios));
            _scenarios = new Dictionary<string, ScenarioTemplate>(StringComparer.Ordinal);
            foreach (var scenario in scenarios)
            {
                if (scenario == null || string.IsNullOrWhiteSpace(scenario.scenarioId)) throw new ArgumentException("Every scenario needs an ID.", nameof(scenarios));
                if (_scenarios.ContainsKey(scenario.scenarioId)) throw new ArgumentException("Duplicate scenario ID: " + scenario.scenarioId, nameof(scenarios));
                _scenarios.Add(scenario.scenarioId, scenario);
            }
            if (_scenarios.Count == 0) throw new ArgumentException("The registry needs at least one scenario.", nameof(scenarios));
        }

        public IReadOnlyCollection<ScenarioTemplate> All => _scenarios.Values.OrderBy(s => s.scenarioId, StringComparer.Ordinal).ToArray();
        public ScenarioTemplate Get(string id)
        {
            ScenarioTemplate scenario;
            if (!_scenarios.TryGetValue(id ?? string.Empty, out scenario)) throw new KeyNotFoundException("Scenario not found: " + id);
            return scenario;
        }
    }

    public sealed class AuthoredRoundSource : IRoundSource
    {
        private readonly ScenarioGenerator _generator;
        private readonly ScenarioRegistry _registry;
        private readonly string _fallbackScenarioId;

        public AuthoredRoundSource(ScenarioRegistry registry, string fallbackScenarioId)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _fallbackScenarioId = fallbackScenarioId;
            _generator = new ScenarioGenerator(new ScenarioValidator());
        }

        public RuntimeScenario Create(int playerCount, int seed, ScenarioDifficulty difficulty = ScenarioDifficulty.Normal)
            => CreateForScenario(_fallbackScenarioId, playerCount, seed, difficulty);

        public RuntimeScenario Create(IReadOnlyList<PlayerData> players, int seed, ScenarioDifficulty difficulty = ScenarioDifficulty.Normal)
            => CreateForScenario(_fallbackScenarioId, players, seed, difficulty);

        public RuntimeScenario CreateForScenario(string scenarioId, int playerCount, int seed, ScenarioDifficulty difficulty = ScenarioDifficulty.Normal)
        {
            var scenario = _generator.Generate(_registry.Get(scenarioId), playerCount, seed, difficulty);
            scenario.generationAttempts = 1;
            return scenario;
        }

        public RuntimeScenario CreateForScenario(string scenarioId, IReadOnlyList<PlayerData> players, int seed, ScenarioDifficulty difficulty = ScenarioDifficulty.Normal)
        {
            var scenario = _generator.Generate(_registry.Get(scenarioId), players, seed, difficulty);
            scenario.generationAttempts = 1;
            return scenario;
        }
    }

    public sealed class ProceduralRoundSource : IRoundSource
    {
        public const int MaximumAttempts = 20;
        private readonly ScenarioRegistry _registry;
        private readonly string _fallbackScenarioId;
        private readonly ScenarioGenerator _generator;
        private readonly List<string> _recentScenarioIds = new List<string>();
        private readonly int _rotationWindow;

        public ProceduralRoundSource(ScenarioRegistry registry, string fallbackScenarioId, int rotationWindow = 1)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _fallbackScenarioId = fallbackScenarioId;
            _rotationWindow = Math.Max(0, rotationWindow);
            _generator = new ScenarioGenerator(new ScenarioValidator());
        }

        public IReadOnlyList<string> RecentScenarioIds => _recentScenarioIds.AsReadOnly();

        public RuntimeScenario Create(int playerCount, int seed, ScenarioDifficulty difficulty = ScenarioDifficulty.Normal)
        {
            var candidates = _registry.All.Where(s => !_recentScenarioIds.Contains(s.scenarioId)).ToList();
            if (candidates.Count == 0) candidates = _registry.All.ToList();
            var random = new SeededRandomService(seed);
            var selected = candidates[random.Next(0, candidates.Count)];
            return CreateWithRetry(selected, playerCount, seed, difficulty);
        }

        public RuntimeScenario Create(IReadOnlyList<PlayerData> players, int seed, ScenarioDifficulty difficulty = ScenarioDifficulty.Normal)
        {
            if (players == null) throw new ArgumentNullException(nameof(players));
            var candidates = _registry.All.Where(s => !_recentScenarioIds.Contains(s.scenarioId)).ToList();
            if (candidates.Count == 0) candidates = _registry.All.ToList();
            var random = new SeededRandomService(seed);
            return CreateWithRetry(candidates[random.Next(0, candidates.Count)], players, seed, difficulty);
        }

        public RuntimeScenario CreateForScenario(string scenarioId, int playerCount, int seed, ScenarioDifficulty difficulty = ScenarioDifficulty.Normal)
            => CreateWithRetry(_registry.Get(scenarioId), playerCount, seed, difficulty);

        public RuntimeScenario CreateForScenario(string scenarioId, IReadOnlyList<PlayerData> players, int seed, ScenarioDifficulty difficulty = ScenarioDifficulty.Normal)
            => CreateWithRetry(_registry.Get(scenarioId), players, seed, difficulty);

        private RuntimeScenario CreateWithRetry(ScenarioTemplate selected, int playerCount, int seed, ScenarioDifficulty difficulty)
        {
            string lastFailure = null;
            for (var attempt = 0; attempt < MaximumAttempts; attempt++)
            {
                var attemptSeed = unchecked(seed + attempt * 7919);
                try
                {
                    var result = _generator.Generate(selected, playerCount, attemptSeed, difficulty);
                    result.generationAttempts = attempt + 1;
                    Remember(selected.scenarioId);
                    return result;
                }
                catch (ScenarioGenerationException exception)
                {
                    lastFailure = exception.Message;
                }
            }

            var fallback = _generator.Generate(_registry.Get(_fallbackScenarioId), playerCount, unchecked(seed ^ 0x5A17), difficulty);
            fallback.generationAttempts = MaximumAttempts + 1;
            fallback.usedFallback = true;
            fallback.fallbackReason = lastFailure;
            fallback.scenarioHash = ScenarioGenerator.ComputeScenarioHash(fallback);
            Remember(fallback.scenarioId);
            return fallback;
        }

        private RuntimeScenario CreateWithRetry(ScenarioTemplate selected, IReadOnlyList<PlayerData> players, int seed, ScenarioDifficulty difficulty)
        {
            string lastFailure = null;
            for (var attempt = 0; attempt < MaximumAttempts; attempt++)
            {
                var attemptSeed = unchecked(seed + attempt * 7919);
                try
                {
                    var result = _generator.Generate(selected, players, attemptSeed, difficulty);
                    result.generationAttempts = attempt + 1;
                    Remember(selected.scenarioId);
                    return result;
                }
                catch (ScenarioGenerationException exception) { lastFailure = exception.Message; }
            }
            var fallback = _generator.Generate(_registry.Get(_fallbackScenarioId), players, unchecked(seed ^ 0x5A17), difficulty);
            fallback.generationAttempts = MaximumAttempts + 1;
            fallback.usedFallback = true;
            fallback.fallbackReason = lastFailure;
            fallback.scenarioHash = ScenarioGenerator.ComputeScenarioHash(fallback);
            Remember(fallback.scenarioId);
            return fallback;
        }

        private void Remember(string scenarioId)
        {
            _recentScenarioIds.Remove(scenarioId);
            _recentScenarioIds.Add(scenarioId);
            while (_recentScenarioIds.Count > _rotationWindow) _recentScenarioIds.RemoveAt(0);
        }
    }
}
