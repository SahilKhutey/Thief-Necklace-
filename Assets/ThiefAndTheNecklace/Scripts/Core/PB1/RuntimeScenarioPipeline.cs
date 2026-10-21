using System;
using System.Collections.Generic;
using System.Linq;

namespace ThiefAndTheNecklace.PB1
{
    public sealed class RoundFactory
    {
        private readonly IScenarioGenerator generator;
        private readonly ScenarioValidator validator;
        private readonly ScenarioGenerationConfig config;
        public RoundFactory(IScenarioGenerator scenarioGenerator, ScenarioGenerationConfig generationConfig = null)
        {
            generator=scenarioGenerator ?? throw new ArgumentNullException(nameof(scenarioGenerator));
            validator=new ScenarioValidator();
            config=generationConfig ?? new ScenarioGenerationConfig();
            if (config.MaxGenerationAttempts < 1) throw new ArgumentOutOfRangeException(nameof(generationConfig), "At least one scenario generation attempt is required.");
        }
        public ScenarioState CreateScenario(RoundContext context, int playerCount)
            => CreateScenario(context, playerCount, null);

        public ScenarioState CreateScenario(RoundContext context, int playerCount, Func<ScenarioState, ScenarioState> finalizeCandidate)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            var failures=new List<ScenarioGenerationError>();
            for (var attempt=0; attempt < config.MaxGenerationAttempts; attempt++)
            {
                var seed=GetAttemptSeed(context.RoundSeed, attempt);
                ScenarioTruthModel truth;
                try { truth=generator.Generate(seed, playerCount); }
                catch (Exception exception)
                {
                    failures.Add(new ScenarioGenerationError { Code="GENERATOR_EXCEPTION", Message=exception.Message, RelatedObjectId=string.Empty });
                    continue;
                }
                if (truth == null)
                {
                    failures.Add(new ScenarioGenerationError { Code="NULL_SCENARIO", Message="Generator returned no scenario.", RelatedObjectId=string.Empty });
                    continue;
                }
                truth.Seed=seed;
                var validation=validator.Validate(truth, playerCount, false);
                if (!validation.Valid)
                {
                    foreach (var error in validation.Errors) failures.Add(new ScenarioGenerationError { Code="INVALID_SCENARIO", Message=error, RelatedObjectId=truth.ScenarioId });
                    continue;
                }
                var scenario=new ScenarioState { ScenarioId=truth.ScenarioId, GenerationSeed=context.RoundSeed, GenerationAttempt=attempt+1 };
                scenario.SetTruth(truth);
                if (finalizeCandidate != null)
                {
                    try
                    {
                        scenario=finalizeCandidate(scenario);
                        if (scenario == null) throw new InvalidOperationException("Candidate finalization returned no scenario.");
                    }
                    catch (Exception exception)
                    {
                        failures.Add(new ScenarioGenerationError { Code="FINALIZATION_REJECTED", Message=exception.Message, RelatedObjectId=truth.ScenarioId });
                        continue;
                    }
                }
                return scenario;
            }
            var result=new ScenarioGenerationResult { Success=false, Errors=failures.AsReadOnly() };
            throw new ScenarioGenerationException("Scenario generation failed after " + config.MaxGenerationAttempts + " deterministic attempts.", result);
        }

        private static int GetAttemptSeed(int roundSeed, int attempt)
        {
            unchecked { return attempt == 0 ? roundSeed : (roundSeed * 397) ^ (attempt * 7919); }
        }
    }

    public sealed class ScenarioGenerationException : InvalidOperationException
    {
        public ScenarioGenerationResult Result { get; }
        public ScenarioGenerationException(string message, ScenarioGenerationResult result) : base(message) { Result=result; }
    }

    public sealed class ScenarioBuildPipeline
    {
        private readonly ScenarioValidator validator;
        private readonly ScenarioGenerationConfig config;
        public ScenarioBuildPipeline(ScenarioValidator validator, ScenarioGenerationConfig generationConfig = null)
        {
            this.validator=validator ?? throw new ArgumentNullException(nameof(validator));
            config=generationConfig ?? new ScenarioGenerationConfig();
            if (config.MinimumEvidencePaths < 1) throw new ArgumentOutOfRangeException(nameof(generationConfig), "At least one evidence path is required.");
        }
        public void ValidateOrThrow(ScenarioState scenario, int playerCount, DifficultyConfig difficulty)
        {
            if (scenario == null) throw new ArgumentNullException(nameof(scenario));
            if (difficulty == null) throw new ArgumentNullException(nameof(difficulty));
            if (scenario.Validated) throw new InvalidOperationException("Scenario is already validated.");
            var result=validator.Validate(scenario.Truth, playerCount);
            if (!result.Valid) throw new InvalidOperationException("Scenario validation failed: " + string.Join(" | ", result.Errors));
            if (!config.AllowFalseLeads && scenario.Truth.Evidence.Any(e => e != null && e.IsFalseLead)) throw new InvalidOperationException("False leads are disabled by the scenario generation configuration.");
            if (scenario.Truth.PlayerInformation.Any(p => p.EvidenceIds.Count < difficulty.EvidencePerPlayer)) throw new InvalidOperationException("Scenario does not meet the evidence density for " + difficulty.Level + ".");
            foreach (var factId in scenario.Truth.CriticalFactIds ?? new List<string>())
                if (new EvidenceGraphValidator().CountIndependentPaths(scenario.Truth, factId) < config.MinimumEvidencePaths) throw new InvalidOperationException("Critical fact " + factId + " does not meet the configured independent evidence path threshold.");
            var solvability=new ScenarioSolvabilityValidator().Validate(scenario.Truth);
            if (solvability.Status == SolvabilityStatus.NoSolution) throw new InvalidOperationException("Scenario has no valid solution: " + solvability.Explanation);
            if (config.RequireUniqueSolution && solvability.Status != SolvabilityStatus.UniqueSolution) throw new InvalidOperationException("Scenario is not uniquely solvable at " + difficulty.Level + ": " + solvability.Explanation);
            scenario.MarkValidated(solvability);
        }
    }
}
