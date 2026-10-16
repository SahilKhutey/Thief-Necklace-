using System.Linq;
using NUnit.Framework;
using ThiefAndTheNecklace.PB1;

namespace ThiefAndTheNecklace.Tests
{
    public sealed class PB1FinalAcceptanceTests
    {
        private static CoreGameRuntime Create(int seed)
        {
            return new CoreGameRuntime(new[] { "A", "B", "C", "D" }, seed, 5,
                DifficultyLevel.Normal,
                new StaticScenarioGenerator(DefaultScenarioFactory.CreateRoyalNecklaceTruth()));
        }

        [Test]
        public void CanonicalApiCompletesFiveRoundsAndRejectsCommandsAfterMatchEnd()
        {
            var game = Create(20260914);
            game.StartMatch();

            for (var round = 1; round <= 5; round++)
            {
                game.BeginDiscussion();
                game.BeginVoting();
                for (var voter = 1; voter <= 4; voter++)
                    Assert.That(game.SubmitVote(voter, voter == 1 ? 2 : 1), Is.True);
                Assert.That(game.ResolveVote(), Is.Not.EqualTo(VoteResolution.RevoteRequired));
                game.CompleteReveal();
                game.ResolveScoring();
                game.FinishRound();
            }

            Assert.That(game.State.Match.Status, Is.EqualTo(MatchStatus.Completed));
            Assert.That(game.State.Match.CurrentRound, Is.EqualTo(5));
            Assert.That(game.State.Match.CurrentRoundState.Phase, Is.EqualTo(RoundPhase.MatchEnd));
            Assert.That(game.State.Scores.CommittedRounds, Is.EqualTo(new[] { 1, 2, 3, 4, 5 }));
            Assert.Throws<System.InvalidOperationException>(() => game.BeginDiscussion());
        }

        [Test]
        public void IdenticalSeedsReproduceRolesPrivateEvidenceTruthAndScenarioSeed()
        {
            var left = Create(987654);
            var right = Create(987654);
            left.StartMatch();
            right.StartMatch();

            Assert.That(left.State.Scenario.GenerationSeed, Is.EqualTo(right.State.Scenario.GenerationSeed));
            Assert.That(left.State.Scenario.ScenarioId, Is.EqualTo(right.State.Scenario.ScenarioId));
            Assert.That(left.State.Match.Players.Select(p => p.Role), Is.EqualTo(right.State.Match.Players.Select(p => p.Role)));
            Assert.That(left.State.Match.Players.Select(p => p.IsThief), Is.EqualTo(right.State.Match.Players.Select(p => p.IsThief)));
            Assert.That(left.State.Match.Players.Select(p => string.Join(",", p.Knowledge.EvidenceIds)),
                Is.EqualTo(right.State.Match.Players.Select(p => string.Join(",", p.Knowledge.EvidenceIds))));
            Assert.That(left.State.Scenario.Truth.Facts.Select(f => f.FactId + ":" + f.Truth),
                Is.EqualTo(right.State.Scenario.Truth.Facts.Select(f => f.FactId + ":" + f.Truth)));
        }

        [Test]
        public void StructuredLogAndReplayDoNotContainPrivateRoleOrClueData()
        {
            var game = Create(8765);
            game.StartMatch();

            var log = game.GetCoreLog();
            var replay = game.GetReplayRecord();
            var thief = game.State.Match.Players.Single(p => p.IsThief);
            Assert.That(log.Count, Is.GreaterThan(0));
            Assert.That(log.All(e => !e.Message.Contains("Thief") && !e.Message.Contains("PrivateSituation")), Is.True);
            Assert.That(replay.Events.All(e => !e.Data.Contains("Thief") && !e.Data.Contains(thief.PlayerId + ":" + thief.Role)), Is.True);

            var mutableCopy = log[0];
            mutableCopy.Message = "changed";
            Assert.That(game.GetCoreLog()[0].Message, Is.Not.EqualTo("changed"));
        }
    }
}
