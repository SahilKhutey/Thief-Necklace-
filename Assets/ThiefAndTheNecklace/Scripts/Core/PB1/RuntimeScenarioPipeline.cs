using System;
using System.Collections.Generic;
using System.Linq;

namespace ThiefAndTheNecklace.PB1
{
    public sealed class RoundFactory
    {
        private readonly IScenarioGenerator generator;
        public RoundFactory(IScenarioGenerator scenarioGenerator) { generator=scenarioGenerator ?? throw new ArgumentNullException(nameof(scenarioGenerator)); }
        public ScenarioState CreateScenario(RoundContext context, int playerCount)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            var truth=generator.Generate(context.RoundSeed, playerCount);
            if (truth == null) throw new InvalidOperationException("Scenario generator returned null.");
            var scenario=new ScenarioState { ScenarioId=truth.ScenarioId, GenerationSeed=context.RoundSeed };
            scenario.SetTruth(truth);
            return scenario;
        }
    }

    public sealed class ScenarioBuildPipeline
    {
        private readonly ScenarioValidator validator;
        public ScenarioBuildPipeline(ScenarioValidator validator) { this.validator=validator ?? throw new ArgumentNullException(nameof(validator)); }
        public void ValidateOrThrow(ScenarioState scenario, int playerCount, DifficultyConfig difficulty)
        {
            if (scenario == null) throw new ArgumentNullException(nameof(scenario));
            if (difficulty == null) throw new ArgumentNullException(nameof(difficulty));
            if (scenario.Validated) throw new InvalidOperationException("Scenario is already validated.");
            var result=validator.Validate(scenario.Truth, playerCount);
            if (!result.Valid) throw new InvalidOperationException("Scenario validation failed: " + string.Join(" | ", result.Errors));
            if (scenario.Truth.PlayerInformation.Any(p => p.EvidenceIds.Count < difficulty.EvidencePerPlayer)) throw new InvalidOperationException("Scenario does not meet the evidence density for " + difficulty.Level + ".");
            scenario.MarkValidated();
        }
    }
}
