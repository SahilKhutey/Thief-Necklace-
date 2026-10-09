using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ThiefAndTheNecklace.Networking;

namespace ThiefAndTheNecklace.Tests
{
    public sealed class ScenarioEngineTests
    {
        [Test]
        public void BuiltInTemplatesValidateForFourFiveAndSixPlayers()
        {
            var validator = new ScenarioValidator();
            foreach (var template in DefaultScenarioCatalog.CreateRegistry().All)
            for (var playerCount = 4; playerCount <= 6; playerCount++)
            {
                var result = ScenarioTemplateValidator.Validate(template, playerCount);
                Assert.That(result.IsValid, Is.True, template.scenarioId + " / " + playerCount + ": " + string.Join("; ", result.Errors));
            }
        }

        [Test]
        public void GeneratedScenariosHaveOneThiefAndValidateAcrossPlayerCountsAndDifficulties()
        {
            var registry = DefaultScenarioCatalog.CreateRegistry();
            var generator = new ScenarioGenerator(new ScenarioValidator());
            foreach (var template in registry.All)
            foreach (var difficulty in new[] { ScenarioDifficulty.Easy, ScenarioDifficulty.Normal, ScenarioDifficulty.Hard })
            for (var count = 4; count <= 6; count++)
            {
                var players = Enumerable.Range(1, count).Select(i => new PlayerData("p" + i, "Player " + i)).ToList();
                var round = generator.Generate(template, players, unchecked(4137 + count * 19 + (int)difficulty), difficulty);
                Assert.That(round.playerSecrets.Count(s => s.isThief), Is.EqualTo(1));
                Assert.That(round.events.Count(e => e.eventType == EventType.Theft), Is.EqualTo(1));
                Assert.That(round.events.Any(e => e.eventType == EventType.Escape), Is.True);
                Assert.That(round.events.Any(e => e.eventType == EventType.Discovery), Is.True);
                Assert.That(new ScenarioValidator().Validate(template, round, template.difficulties.First(p => p.difficulty == difficulty)).IsValid, Is.True);
                Assert.That(round.publicScenarioHash, Is.Not.EqualTo(round.scenarioHash));
            }
        }

        [Test]
        public void SameScenarioVersionPlayerCountAndSeedReproduceTheSameHash()
        {
            var template = DefaultScenarioCatalog.CreateRegistry().Get("ROYAL_NECKLACE_03");
            var generator = new ScenarioGenerator(new ScenarioValidator());
            var first = generator.Generate(template, 6, 73918244, ScenarioDifficulty.Normal);
            var second = generator.Generate(template, 6, 73918244, ScenarioDifficulty.Normal);
            Assert.That(second.scenarioHash, Is.EqualTo(first.scenarioHash));
            Assert.That(second.publicScenarioHash, Is.EqualTo(first.publicScenarioHash));
            Assert.That(second.thiefPlayerId, Is.EqualTo(first.thiefPlayerId));
            Assert.That(second.clues.Select(c => c.text), Is.EqualTo(first.clues.Select(c => c.text)));
        }

        [Test]
        public void TheftHasIndependentEvidenceAndNoClueNamesTheThiefAsTheThief()
        {
            var template = DefaultScenarioCatalog.CreateRegistry().Get("ROYAL_NECKLACE_01");
            var round = new ScenarioGenerator(new ScenarioValidator()).Generate(template, 6, 98765);
            var evidence = round.evidenceGraph.EvidenceForEvent("thief-entry")
                .Concat(round.evidenceGraph.EvidenceForEvent("thief-escape"))
                .Concat(round.evidenceGraph.EvidenceForEvent("discovery"));
            Assert.That(evidence.Select(link => link.EventId).Distinct().Count(), Is.GreaterThanOrEqualTo(2));
            Assert.That(round.clues.Where(c => c.ownerPlayerId != round.thiefPlayerId).Any(c => c.text.Contains(round.thiefPlayerId)), Is.False);
        }

        [Test]
        public void AuthoredSourceProvidesFallbackAndProceduralSourceRotatesScenarios()
        {
            var registry = DefaultScenarioCatalog.CreateRegistry();
            var fallback = new AuthoredRoundSource(registry, "ROYAL_NECKLACE_01").Create(4, 42);
            Assert.That(fallback.scenarioId, Is.EqualTo("ROYAL_NECKLACE_01"));
            var source = new ProceduralRoundSource(registry, "ROYAL_NECKLACE_01");
            var first = source.Create(4, 7);
            var second = source.Create(4, 8);
            Assert.That(second.scenarioId, Is.Not.EqualTo(first.scenarioId));
        }

        [Test]
        public void InvalidProceduralTemplateRetriesThenUsesValidatedAuthoredFallback()
        {
            var templates = DefaultScenarioCatalog.CreateRegistry().All.ToList();
            var invalid = DefaultScenarioCatalog.CreateRegistry().Get("ROYAL_NECKLACE_02");
            invalid.targetLocationId = "missing-location";
            templates.Add(invalid);
            var source = new ProceduralRoundSource(new ScenarioRegistry(templates), "ROYAL_NECKLACE_01");
            var players = Enumerable.Range(1, 4).Select(i => new PlayerData("p" + i, "Player " + i)).ToList();
            var result = source.CreateForScenario("ROYAL_NECKLACE_02", players, 91);
            Assert.That(result.usedFallback, Is.True);
            Assert.That(result.scenarioId, Is.EqualTo("ROYAL_NECKLACE_01"));
            Assert.That(result.generationAttempts, Is.EqualTo(ProceduralRoundSource.MaximumAttempts + 1));
        }

        [Test]
        public void NetworkCommitExposesPublicContentAndOnlyRoutesOwnPrivateStateUntilReveal()
        {
            var players = Enumerable.Range(1, 4).Select(i => new PlayerData("stable-" + i, "Player " + i)).ToList();
            var committer = new NetworkScenarioCommitter();
            var publicState = committer.Commit(new AuthoredRoundSource(DefaultScenarioCatalog.CreateRegistry(), "ROYAL_NECKLACE_01"), players, 1, 2255);
            Assert.That(publicState.ScenarioId, Is.EqualTo("ROYAL_NECKLACE_01"));
            PrivateRoundState state;
            Assert.That(committer.TryGetPrivateState("stable-1", out state), Is.True);
            Assert.That(state.OwnerPlayerId, Is.EqualTo("stable-1"));
            Assert.That(committer.TryGetPrivateState("other-user", out state), Is.False);
            Assert.Throws<System.InvalidOperationException>(() => committer.Reveal(null, RoundPhase.Discussion));
            var reveal = committer.Reveal(null, RoundPhase.Reveal);
            Assert.That(reveal.Roles.Count, Is.EqualTo(4));
            Assert.That(committer.TryGetPrivateState("stable-1", out state), Is.False);
        }

        [Test]
        public void ContradictionGraphFindsOnlyOverlappingDifferentLocations()
        {
            var claims = new[]
            {
                new StructuredPlayerClaim { PlayerId = "p1", SubjectPlayerId = "p3", LocationId = "hall", StartMinute = 10, EndMinute = 12 },
                new StructuredPlayerClaim { PlayerId = "p2", SubjectPlayerId = "p3", LocationId = "garden", StartMinute = 11, EndMinute = 13 },
                new StructuredPlayerClaim { PlayerId = "p4", SubjectPlayerId = "p3", LocationId = "office", StartMinute = 20, EndMinute = 21 }
            };
            var conflicts = ContradictionGraph.FindOverlappingLocationConflicts(claims);
            Assert.That(conflicts.Count, Is.EqualTo(1));
        }
    }
}
