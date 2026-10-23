using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ThiefAndTheNecklace.PB1;

namespace ThiefAndTheNecklace.Tests
{
    public sealed class ProductionMatchCoreTests
    {
        private static ScenarioDefinitionData Scenario()
        {
            var scenario = new ScenarioDefinitionData
            {
                ScenarioId = "royal-necklace", Title = "The Royal Necklace", Introduction = "A necklace has vanished.",
                TruthModel = DefaultScenarioFactory.CreateRoyalNecklaceTruth(),
                Events = new List<ScenarioEvent> { new ScenarioEvent { EventId = "theft", Time = 14f, Location = "Royal Chamber", Description = "21:14 — the Necklace is taken." } }
            };
            foreach (RoleType role in System.Enum.GetValues(typeof(RoleType)).Cast<RoleType>().Where(role => role != RoleType.Thief))
                scenario.PlayerInformation.Add(new PlayerScenarioInfo
                {
                    Role = role,
                    PrivateSituation = "You were in the palace during the incident.",
                    Clues = new List<string> { "You heard a door open." }
                });
            return scenario;
        }

        private static GameController Start(int count = 4, int rounds = 5, int seed = 10001)
        {
            var game = new GameController(Scenario(), rounds, seed);
            game.StartLocalMatch(Enumerable.Range(1, count).Select(i => "Player " + i).ToArray());
            return game;
        }

        private static int FindThief(GameController game, int count)
        {
            return Enumerable.Range(1, count).Select(id => game.GetPrivateView(id, id)).Single(view => view.IsThief).PlayerId;
        }

        private static void BeginVoting(GameController game)
        {
            game.BeginDiscussion();
            game.BeginVoting();
        }

        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [Description("GW17-ROLE-002")]
        public void StartMatch_AssignsUniqueRolesAndExactlyOneThief(int count)
        {
            var game = Start(count);
            var players = Enumerable.Range(1, count).Select(id => game.GetPrivateView(id, id)).ToList();
            var roles = players.Select(player => player.Role).ToList();
            Assert.That(players.Count(player => player.IsThief), Is.EqualTo(1));
            Assert.That(roles.Distinct().Count(), Is.EqualTo(count));
            Assert.That(roles.Contains(RoleType.Thief), Is.False);
        }

        [Test]
        [Description("GW17-ROLE-006")]
        public void RoleAndScenarioAssignment_AreDeterministicForTheSameSeed()
        {
            var first = Start(6, 5, 12345);
            var second = Start(6, 5, 12345);
            for (var id = 1; id <= 6; id++)
            {
                var a = first.GetPrivateView(id, id);
                var b = second.GetPrivateView(id, id);
                Assert.That(b.Role, Is.EqualTo(a.Role));
                Assert.That(b.IsThief, Is.EqualTo(a.IsThief));
                Assert.That(b.Clues, Is.EqualTo(a.Clues));
                Assert.That(b.EvidenceIds, Is.EqualTo(a.EvidenceIds));
                Assert.That(b.KnownFactIds, Is.EqualTo(a.KnownFactIds));
                Assert.That(b.UnknownFactIds, Is.EqualTo(a.UnknownFactIds));
                Assert.That(b.FalseFactIds, Is.EqualTo(a.FalseFactIds));
            }
        }

        [Test]
        [Description("GW17-INFO-002")]
        public void PublicViewDoesNotContainPrivateRoleOrClueFields()
        {
            var view = Start().GetPublicPlayers().Single();
            Assert.That(view.GetType().GetField("Role"), Is.Null);
            Assert.That(view.GetType().GetField("Clues"), Is.Null);
            Assert.That(view.GetType().GetProperty("DisplayName"), Is.Not.Null);
        }

        [Test]
        public void PrivateViewContainsSituationCluesAndObjective()
        {
            var game = Start();
            var view = game.GetPrivateView(FindThief(game, 4), FindThief(game, 4));
            Assert.That(view.Situation, Is.Not.Empty);
            Assert.That(view.Clues, Is.Not.Empty);
            Assert.That(view.Objective, Does.Contain("Avoid detection"));
        }

        [Test]
        [Description("GW17-INFO-003")]
        public void PrivateViewRejectsAnotherPlayersIdentity()
        {
            var game = Start();
            Assert.Throws<System.InvalidOperationException>(() => game.GetPrivateView(1, 2));
        }

        [Test]
        public void RoleScoreValuesMatchTheLockedProductionTable()
        {
            Assert.That(ScoreService.RoleValue(RoleType.King), Is.EqualTo(8));
            Assert.That(ScoreService.RoleValue(RoleType.Queen), Is.EqualTo(7));
            Assert.That(ScoreService.RoleValue(RoleType.Minister), Is.EqualTo(6));
            Assert.That(ScoreService.RoleValue(RoleType.Soldier), Is.EqualTo(5));
            Assert.That(ScoreService.RoleValue(RoleType.Guard), Is.EqualTo(5));
            Assert.That(ScoreService.RoleValue(RoleType.Servant), Is.EqualTo(4));
            Assert.That(ScoreService.RoleValue(RoleType.Thief), Is.EqualTo(0));
        }

        [Test]
        public void DiscussionTimerAdvancesAndAutomaticallyStartsVoting()
        {
            var game = Start();
            game.BeginDiscussion();
            game.AdvanceTimer(179f);
            Assert.That(game.Phase, Is.EqualTo(RoundPhase.Discussion));
            game.AdvanceTimer(1f);
            Assert.That(game.Phase, Is.EqualTo(RoundPhase.Voting));
        }

        [Test]
        [Description("GW17-VOTE-003")]
        public void VoteServiceRejectsSelfVoteDuplicateVoteAndInvalidTarget()
        {
            var game = Start();
            var thief = FindThief(game, 4);
            BeginVoting(game);
            var other = Enumerable.Range(1, 4).First(id => id != thief);
            Assert.That(game.SubmitVote(other, other), Is.False);
            Assert.That(game.SubmitVote(other, 999), Is.False);
            Assert.That(game.SubmitVote(other, thief), Is.True);
            Assert.That(game.SubmitVote(other, thief), Is.False);
        }

        [Test]
        [Description("GW17-VOTE-TALLY-001")]
        public void VoteResolutionRequiresAllEligiblePlayers()
        {
            var game = Start();
            var thief = FindThief(game, 4);
            BeginVoting(game);
            var voter = Enumerable.Range(1, 4).First(id => id != thief);
            game.SubmitVote(voter, thief);
            Assert.Throws<System.InvalidOperationException>(() => game.ResolveVotes());
        }

        [Test]
        [Description("GW17-SCORE-001")]
        public void CorrectInnocentVoterReceivesTheirRoleValueAndWrongVoterGetsZero()
        {
            var game = Start();
            var thief = FindThief(game, 4);
            var roles = Enumerable.Range(1, 4).ToDictionary(id => id, id => game.GetPrivateView(id, id).Role);
            BeginVoting(game);
            var wrongVoter = Enumerable.Range(1, 4).First(id => id != thief);
            var safeTarget = Enumerable.Range(1, 4).First(id => id != thief && id != wrongVoter);
            var thiefTarget = Enumerable.Range(1, 4).First(id => id != thief && id != wrongVoter && id != safeTarget);
            foreach (var id in Enumerable.Range(1, 4))
                game.SubmitVote(id, id == thief ? thiefTarget : id == wrongVoter ? safeTarget : thief);
            Assert.That(game.ResolveVotes(), Is.EqualTo(VoteResolution.ThiefCaught));
            Assert.That(game.GetRevealedPlayers()[thief].IsThief, Is.True);
            game.NotifyRoleRevealed();
            game.CalculateScores();
            Assert.Throws<System.InvalidOperationException>(() => game.CalculateScores());
            var scores = game.GetPublicPlayers().ToDictionary(p => p.PlayerId, p => p.MatchScore);
            Assert.That(scores[thief], Is.EqualTo(0));
            Assert.That(scores[wrongVoter], Is.EqualTo(0));
            foreach (var voter in Enumerable.Range(1, 4).Where(id => id != thief && id != wrongVoter))
                Assert.That(scores[voter], Is.EqualTo(ScoreService.RoleValue(roles[voter])));
            game.FinishRound();
            Assert.That(game.Phase, Is.EqualTo(RoundPhase.PrivateReveal));
            Assert.That(scores.Count, Is.EqualTo(4));
        }

        [Test]
        public void RoleRevealTelemetryWaitsForPresentationSignal()
        {
            var game = Start();
            var thief = FindThief(game, 4);
            var revealLogged = false;
            game.Logged += signal => { if (signal == GameSignal.RoleRevealed) revealLogged = true; };
            BeginVoting(game);
            foreach (var id in Enumerable.Range(1, 4))
                game.SubmitVote(id, id == thief ? Enumerable.Range(1, 4).First(other => other != thief) : thief);
            game.ResolveVotes();
            Assert.That(revealLogged, Is.False);
            game.NotifyRoleRevealed();
            Assert.That(revealLogged, Is.True);
        }

        [Test]
        [Description("GW17-REVOTE-002")]
        public void FirstTieRequiresRunoffAndSecondTieMakesThiefEscape()
        {
            var game = Start();
            var thief = FindThief(game, 4);
            var ids = Enumerable.Range(1, 4).ToArray();
            var a = ids.First(id => id != thief);
            var b = ids.First(id => id != thief && id != a);
            var c = ids.First(id => id != thief && id != a && id != b);
            var d = thief;
            BeginVoting(game);
            game.SubmitVote(a, b); game.SubmitVote(b, a); game.SubmitVote(c, b); game.SubmitVote(d, a);
            Assert.That(game.ResolveVotes(), Is.EqualTo(VoteResolution.RevoteRequired));
            Assert.That(game.IsRunoff, Is.True);
            game.SubmitVote(a, b); game.SubmitVote(b, a); game.SubmitVote(c, b); game.SubmitVote(d, a);
            Assert.That(game.ResolveVotes(), Is.EqualTo(VoteResolution.ThiefEscaped));
            game.NotifyRoleRevealed();
            game.CalculateScores();
            Assert.That(game.GetPublicPlayers().Single(p => p.PlayerId == thief).MatchScore, Is.GreaterThan(0));
        }

        [Test]
        public void ThiefEscapeScoreEqualsInnocentRoleValueSum()
        {
            var game = Start();
            var thief = FindThief(game, 4);
            var innocentRoleSum = Enumerable.Range(1, 4).Where(id => id != thief)
                .Select(id => ScoreService.RoleValue(game.GetPrivateView(id, id).Role)).Sum();
            BeginVoting(game);
            var target = Enumerable.Range(1, 4).First(id => id != thief);
            var alternate = Enumerable.Range(1, 4).First(id => id != thief && id != target);
            foreach (var id in Enumerable.Range(1, 4))
                game.SubmitVote(id, id == target ? alternate : target);
            Assert.That(game.ResolveVotes(), Is.EqualTo(VoteResolution.ThiefEscaped));
            game.NotifyRoleRevealed();
            game.CalculateScores();
            var scores = game.GetPublicPlayers().ToDictionary(p => p.PlayerId, p => p.MatchScore);
            Assert.That(scores[thief], Is.EqualTo(innocentRoleSum));
            Assert.That(scores.Where(x => x.Key != thief).All(x => x.Value == 0), Is.True);
        }

        [Test]
        public void FiveRoundMatchAccumulatesScoresAndEndsOnlyAfterRoundFive()
        {
            var game = Start(4, 5, 10001);
            var expected = Enumerable.Range(1, 4).ToDictionary(id => id, _ => 0);
            for (var round = 1; round <= 5; round++)
            {
                var thief = FindThief(game, 4);
                var roles = Enumerable.Range(1, 4).ToDictionary(id => id, id => game.GetPrivateView(id, id).Role);
                BeginVoting(game);
                foreach (var id in Enumerable.Range(1, 4))
                    game.SubmitVote(id, id == thief ? Enumerable.Range(1, 4).First(other => other != thief) : thief);
                game.ResolveVotes();
                game.NotifyRoleRevealed();
                game.CalculateScores();
                foreach (var id in Enumerable.Range(1, 4).Where(id => id != thief)) expected[id] += ScoreService.RoleValue(roles[id]);
                var currentScores = game.GetPublicPlayers().ToDictionary(p => p.PlayerId, p => p.MatchScore);
                foreach (var id in Enumerable.Range(1, 4)) Assert.That(currentScores[id], Is.EqualTo(expected[id]));
                game.FinishRound();
                Assert.That(game.CurrentRound, Is.EqualTo(round == 5 ? 5 : round + 1));
                Assert.That(game.Phase, Is.EqualTo(round == 5 ? RoundPhase.MatchEnd : RoundPhase.PrivateReveal));
            }
            Assert.That(game.GetPublicPlayers().All(p => p.MatchScore >= 0), Is.True);
            Assert.That(game.GetMatchStandings().Count, Is.EqualTo(4));
            Assert.Throws<System.InvalidOperationException>(() => game.FinishRound());
        }

        [Test]
        public void IllegalPhaseTransitionsAndDuplicateScoreApplicationAreRejected()
        {
            var game = Start();
            Assert.Throws<System.InvalidOperationException>(() => game.BeginVoting());
            Assert.Throws<System.InvalidOperationException>(() => game.CalculateScores());
        }

        [Test]
        public void MissingScenarioRoleInformationFailsRoundSetup()
        {
            var scenario = Scenario();
            scenario.PlayerInformation.RemoveAll(info => info.Role == RoleType.King);
            var game = new GameController(scenario);
            Assert.Throws<System.InvalidOperationException>(() => game.StartLocalMatch(new[] { "A", "B", "C", "D" }));
        }

        [Test]
        public void FactKey_IsStableAndComparable()
        {
            var a = new FactKey("Queen", "left", "BanquetHall");
            var b = new FactKey("Queen", "left", "BanquetHall");
            Assert.That(a.ToString(), Is.EqualTo("Queen:left:BanquetHall"));
            Assert.That(a, Is.EqualTo(b));
        }

        [Test]
        [Description("GW17-EVIDENCE-003")]
        public void EvidenceGraph_FindsSupportAndRequiresIndependentPaths()
        {
            var graph = new EvidenceGraph();
            graph.AddEvidence(new EvidenceDefinition { EvidenceId="E1", Independent=true, SupportsFactIds=new List<string> { "F1" } });
            Assert.That(graph.GetEvidenceSupporting("F1").Count, Is.EqualTo(1));
            Assert.That(graph.HasIndependentSupport("F1", 2), Is.False);
        }

        [Test]
        public void EvidenceGraph_RejectsDuplicateLogicalFacts()
        {
            var graph = new EvidenceGraph();
            graph.AddFact(new FactDefinition { FactId="F1", SubjectId="Queen", Predicate="left", ObjectId="Hall" });
            Assert.Throws<System.InvalidOperationException>(() => graph.AddFact(new FactDefinition { FactId="F2", SubjectId="Queen", Predicate="left", ObjectId="Hall" }));
        }

        [Test]
        public void PrivateEvidence_IsOwnerRestricted()
        {
            var game = Start();
            var owner = FindThief(game, 4);
            Assert.That(game.GetPrivateEvidence(owner, owner).Count, Is.GreaterThan(0));
            var other = owner == 1 ? 2 : 1;
            Assert.Throws<System.InvalidOperationException>(() => game.GetPrivateEvidence(owner, other));
        }

        [Test]
        public void ContradictionService_DetectsConflictingLocations()
        {
            var result = new ContradictionService().FindContradictions(new[]
            {
                new FactDefinition { FactId="F1", SubjectId="Player1", Predicate="At", ObjectId="Hall", Truth=EvidenceTruth.True },
                new FactDefinition { FactId="F2", SubjectId="Player1", Predicate="At", ObjectId="Treasury", Truth=EvidenceTruth.True }
            });
            Assert.That(result.ContradictionFound, Is.True);
        }

        [Test]
        public void OpportunityService_RejectsTimesOutsideWindow()
        {
            var window = new OpportunityWindow { StartTime=10, EndTime=20, LocationId="Chamber", HasAccess=true, PhysicallyPossible=true };
            Assert.That(new OpportunityService().HasOpportunity(window, 25, "Chamber"), Is.False);
        }

        [Test]
        public void TemporalContradiction_DetectsOverlappingDifferentLocations()
        {
            var claim = new TemporalConstraint { PlayerId="P1", StartTime=14, EndTime=14, LocationId="Treasury" };
            var evidence = new TemporalConstraint { PlayerId="P1", StartTime=13, EndTime=16, LocationId="Garden" };
            Assert.That(new ContradictionService().Contradicts(claim, evidence), Is.True);
        }

        [Test]
        public void StaticScenarioGenerator_ReturnsDeterministicIndependentCopies()
        {
            var generator = new StaticScenarioGenerator(DefaultScenarioFactory.CreateRoyalNecklaceTruth());
            var a = generator.Generate(10001, 4);
            var b = generator.Generate(10001, 4);
            Assert.That(a.Timeline.Select(x => x.EventId), Is.EqualTo(b.Timeline.Select(x => x.EventId)));
            a.Timeline[0].Action = "mutated";
            Assert.That(b.Timeline[0].Action, Is.Not.EqualTo("mutated"));
        }

        [Test]
        public void RoundTruth_ReturnsCopiesAndCannotMutateItsSnapshot()
        {
            var model = DefaultScenarioFactory.CreateRoyalNecklaceTruth();
            var roundTruth = new RoundTruth(model.ScenarioId, model);
            var returned = roundTruth.Scenario;
            returned.Timeline[0].Action = "changed";
            Assert.That(roundTruth.Scenario.Timeline[0].Action, Is.Not.EqualTo("changed"));
        }

        [Test]
        [Description("GW17-SCENARIO-011")]
        public void ScenarioValidator_RejectsMissingEvidenceAndCriticalCoverage()
        {
            var model = DefaultScenarioFactory.CreateRoyalNecklaceTruth();
            model.Evidence.Clear();
            var result = new ScenarioValidator().Validate(model, 4);
            Assert.That(result.Valid, Is.False);
            Assert.That(result.Errors.Any(x => x.Contains("evidence")), Is.True);
        }

        [Test]
        public void InvalidTruthModel_PreventsMatchFromStarting()
        {
            var scenario = Scenario();
            scenario.TruthModel.Evidence.Clear();
            var game = new GameController(scenario);
            Assert.Throws<System.InvalidOperationException>(() => game.StartLocalMatch(new[] { "A", "B", "C", "D" }));
            Assert.That(game.Phase, Is.EqualTo(RoundPhase.Waiting));
        }

        [Test]
        public void ClaimsCanBeRetractedWithoutChangingTruth()
        {
            var game = Start();
            var id = FindThief(game, 4);
            game.BeginDiscussion();
            var claim = game.AddClaim(id, "F_QUEEN_LEFT", "I was near the hall.", 1f);
            Assert.That(game.RetractClaim(claim.ClaimId), Is.True);
            Assert.That(game.Claims.Single().Retracted, Is.True);
            Assert.That(game.RetractClaim(claim.ClaimId), Is.False);
        }

        [TestCase(3)]
        [TestCase(7)]
        public void StartMatchRejectsPlayerCountsOutsideFourThroughSix(int count)
        {
            var game = new GameController(Scenario());
            Assert.Throws<System.ArgumentException>(() => game.StartLocalMatch(Enumerable.Range(1, count).Select(i => "P" + i).ToArray()));
        }
    }
}
