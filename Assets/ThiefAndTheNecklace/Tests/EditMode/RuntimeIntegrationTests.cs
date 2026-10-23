using System.Linq;
using NUnit.Framework;
using ThiefAndTheNecklace.PB1;

namespace ThiefAndTheNecklace.Tests
{
    public sealed class RuntimeIntegrationTests
    {
        private static MatchController Create(int seed=10001, int rounds=5, DifficultyLevel difficulty=DifficultyLevel.Normal, int players=4)
        {
            var names=Enumerable.Range(1, players).Select(i => "Player " + i).ToArray();
            return new MatchController(new StaticScenarioGenerator(DefaultScenarioFactory.CreateRoyalNecklaceTruth()), seed, rounds, difficulty, names);
        }

        private static void PlayNoTieRound(MatchController match)
        {
            var targets=Enumerable.Range(1, match.State.Match.Players.Count).Select(id => id == 1 ? 2 : 1).ToArray();
            new LocalMatchTestDriver(match).PlayRound(targets);
        }

        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        public void RuntimeStartsValidatedRoundWithOneThiefAndPrivateKnowledge(int count)
        {
            var match=Create(players:count);
            match.Start();
            Assert.That(match.State.Match.CurrentRound, Is.EqualTo(1));
            Assert.That(match.State.Match.CurrentRoundState.Phase, Is.EqualTo(RoundPhase.PrivateReveal));
            Assert.That(match.State.Scenario.Validated, Is.True);
            Assert.That(match.State.Match.Players.Count(p => p.IsThief), Is.EqualTo(1));
            Assert.That(match.State.Match.Players.All(p => p.Knowledge.EvidenceIds.Count == 4), Is.True);
            var owner=match.State.Match.Players[0].PlayerId;
            Assert.That(match.GetPrivateView(owner, owner).EvidenceIds.Count, Is.EqualTo(4));
            Assert.That(match.GetPrivateEvidence(owner, owner).Count, Is.EqualTo(4));
            Assert.Throws<System.InvalidOperationException>(() => match.GetPrivateView(owner, owner == 1 ? 2 : 1));
            Assert.Throws<System.InvalidOperationException>(() => match.GetPrivateEvidence(owner, owner == 1 ? 2 : 1));
            Assert.That(match.GetPublicPlayers().First().GetType().GetField("Role"), Is.Null);
        }

        [Test]
        public void SameSeedAndDifficultyProduceSameRolesAndKnowledge()
        {
            var a=Create(555555); var b=Create(555555);
            a.Start(); b.Start();
            for (var i=0; i<a.State.Match.Players.Count; i++)
            {
                Assert.That(a.State.Match.Players[i].Role, Is.EqualTo(b.State.Match.Players[i].Role));
                Assert.That(a.State.Match.Players[i].IsThief, Is.EqualTo(b.State.Match.Players[i].IsThief));
                Assert.That(a.State.Match.Players[i].Knowledge.EvidenceIds, Is.EqualTo(b.State.Match.Players[i].Knowledge.EvidenceIds));
            }
            Assert.That(a.State.Scenario.Truth.Timeline.Select(e => e.EventId), Is.EqualTo(b.State.Scenario.Truth.Timeline.Select(e => e.EventId)));
        }

        [Test]
        public void DifficultyChangesEvidenceDensityWithoutChangingVoteRules()
        {
            var easy=Create(difficulty:DifficultyLevel.Easy); var hard=Create(difficulty:DifficultyLevel.Hard);
            easy.Start(); hard.Start();
            Assert.That(easy.State.Match.Players[0].Knowledge.EvidenceIds.Count, Is.EqualTo(3));
            Assert.That(hard.State.Match.Players[0].Knowledge.EvidenceIds.Count, Is.EqualTo(6));
            easy.BeginDiscussion(); easy.BeginVoting();
            Assert.That(easy.SubmitVote(1, 1), Is.False);
        }

        [Test]
        [Description("GW17-COMPLETE-005")]
        public void FullFiveRoundMatchCompletesThroughPublicCommands()
        {
            var match=Create();
            match.Start();
            for (var round=1; round<=5; round++)
            {
                PlayNoTieRound(match);
                Assert.That(match.State.Match.CurrentRound, Is.EqualTo(round == 5 ? 5 : round + 1));
                Assert.That(match.State.Match.CurrentRoundState.Phase, Is.EqualTo(round == 5 ? RoundPhase.MatchEnd : RoundPhase.PrivateReveal));
            }
            Assert.That(match.State.Match.Status, Is.EqualTo(MatchStatus.Completed));
            Assert.That(match.GetStandings().Count, Is.EqualTo(4));
            Assert.Throws<System.InvalidOperationException>(() => match.BeginDiscussion());
        }

        [Test]
        [Description("GW17-RESOLUTION-002")]
        public void FirstTieRunsOffAndSecondTieEscapes()
        {
            var match=Create(rounds:1); match.Start();
            match.BeginDiscussion(); match.BeginVoting();
            var first=new[] { 2, 1, 1, 2 };
            for (var i=0; i<first.Length; i++) Assert.That(match.SubmitVote(i+1, first[i]), Is.True);
            Assert.That(match.ResolveVote(), Is.EqualTo(VoteResolution.RevoteRequired));
            Assert.That(match.State.Match.CurrentRoundState.VoteRound, Is.EqualTo(1));
            Assert.That(match.State.Match.CurrentRoundState.Phase, Is.EqualTo(RoundPhase.Voting));
            var runoff=new[] { 2, 1, 1, 2 };
            for (var i=0; i<runoff.Length; i++) Assert.That(match.SubmitVote(i+1, runoff[i]), Is.True);
            Assert.That(match.ResolveVote(), Is.EqualTo(VoteResolution.ThiefEscaped));
            match.CompleteReveal(); match.ResolveScoring(); match.FinishRound();
            Assert.That(match.State.Match.Status, Is.EqualTo(MatchStatus.Completed));
        }

        [Test]
        public void SnapshotAndReplayMetadataAreDetachedFromRuntimeState()
        {
            var match=Create(); match.Start();
            var snapshot=match.CaptureSnapshot();
            Assert.That(snapshot.Round, Is.EqualTo(1));
            Assert.That(snapshot.Phase, Is.EqualTo(RoundPhase.PrivateReveal));
            var replay=match.GetReplayRecord();
            replay.Events.Clear();
            Assert.That(match.GetReplayRecord().Events.Count, Is.GreaterThan(0));
            var truth=match.State.Scenario.Truth;
            truth.Timeline[0].Action="mutated copy";
            Assert.That(match.State.Scenario.Truth.Timeline[0].Action, Is.Not.EqualTo("mutated copy"));
        }

        [Test]
        [Description("GW17-REPLAY-001")]
        public void PublicReplayRedactsAuthorityPayloadAndDetectsTampering()
        {
            var match=Create(); match.Start(); match.BeginDiscussion(); match.BeginVoting();
            Assert.That(match.SubmitVote(1, 2), Is.True);
            var replay=match.GetReplayRecord();
            Assert.That(replay.ContainsAuthorityPrivateData, Is.False);
            Assert.That(replay.SeedRedacted, Is.True);
            Assert.That(replay.Seed, Is.Zero);
            Assert.That(replay.Events.All(item => item.Data == string.Empty), Is.True);
            Assert.That(ReplayIntegrity.Validate(replay).IsValid, Is.True);

            replay.Events[0].EventType="ALTERED_EVENT";
            Assert.That(ReplayIntegrity.Validate(replay).IsValid, Is.False);
            var truncated=match.GetReplayRecord();
            truncated.Events.RemoveAt(truncated.Events.Count - 1);
            Assert.That(ReplayIntegrity.Validate(truncated).IsValid, Is.False);
        }

        [Test]
        [Description("GW17-HASH-001")]
        public void MatchTraceHasMonotonicSequenceAndSeedStablePublicStateHashes()
        {
            var first=Create(34567); var second=Create(34567);
            first.Start(); second.Start();
            for (var round=0; round<5; round++) { PlayNoTieRound(first); PlayNoTieRound(second); }
            var trace=first.GetMatchTrace();
            Assert.That(trace.Count, Is.GreaterThan(0));
            Assert.That(trace.Select(item => item.Sequence), Is.EqualTo(Enumerable.Range(1, trace.Count).Select(i => (long)i)));
            Assert.That(trace.All(item => item.PublicStateHash.Length == 64), Is.True);
            Assert.That(trace.Select(item => item.PublicStateHash), Is.EqualTo(second.GetMatchTrace().Select(item => item.PublicStateHash)));
        }

        [Test]
        public void InvalidScenarioAbortsBeforePrivateReveal()
        {
            var truth=DefaultScenarioFactory.CreateRoyalNecklaceTruth();
            truth.Evidence.Clear();
            var match=new MatchController(new StaticScenarioGenerator(truth), 123, 5, DifficultyLevel.Normal, new[] { "A", "B", "C", "D" });
            Assert.Throws<System.InvalidOperationException>(() => match.Start());
            Assert.That(match.State.Match.Status, Is.EqualTo(MatchStatus.Aborted));
            Assert.That(match.State.Match.CurrentRoundState.Phase, Is.EqualTo(RoundPhase.Starting));
        }

        [Test]
        public void RuntimeVoteCommandsRejectSelfDuplicateAndMissingTargets()
        {
            var match=Create(); match.Start(); match.BeginDiscussion(); match.BeginVoting();
            Assert.That(match.SubmitVote(1, 1), Is.False);
            Assert.That(match.SubmitVote(1, 99), Is.False);
            Assert.That(match.SubmitVote(1, 2), Is.True);
            Assert.That(match.SubmitVote(1, 3), Is.False);
        }

        [Test]
        public void ConstructorRejectsInvalidPlayerCountAndRoundConfiguration()
        {
            var generator=new StaticScenarioGenerator(DefaultScenarioFactory.CreateRoyalNecklaceTruth());
            Assert.Throws<System.ArgumentException>(() => new MatchController(generator, 1, 5, DifficultyLevel.Normal, new[] { "A", "B", "C" }));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new MatchController(generator, 1, 0, DifficultyLevel.Normal, new[] { "A", "B", "C", "D" }));
        }

        [Test]
        [Description("GW17-SCORE-009")]
        public void RoundCannotScoreOrFinishTwice()
        {
            var match=Create(rounds:1); match.Start(); PlayNoTieRound(match);
            Assert.Throws<System.InvalidOperationException>(() => match.FinishRound());
            Assert.Throws<System.InvalidOperationException>(() => match.ResolveScoring());
        }
    }
}
