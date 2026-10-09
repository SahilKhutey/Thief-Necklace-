using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace ThiefAndTheNecklace.Tests
{
    public sealed class CoreGameplayTests
    {
        private static List<PlayerData> CreatePlayers()
        {
            return new List<PlayerData>
            {
                new PlayerData("P1", "Player 1"),
                new PlayerData("P2", "Player 2"),
                new PlayerData("P3", "Player 3"),
                new PlayerData("P4", "Player 4")
            };
        }

        [Test]
        public void RoleAssignment_DealsFourUniqueRolesAndExactlyOneThief()
        {
            var players = CreatePlayers();
            new RoleManager().AssignRoles(players, 12345);

            Assert.That(players.Count(p => p.IsThief), Is.EqualTo(1));
            Assert.That(players.All(p => !string.IsNullOrEmpty(p.RoleId)), Is.True);
            Assert.That(players.Select(p => p.RoleId).Distinct().Count(), Is.EqualTo(4));
        }

        [Test]
        public void RoleAssignment_SameSeedReproducesAssignment()
        {
            var first = CreatePlayers();
            var second = CreatePlayers();
            var manager = new RoleManager();

            manager.AssignRoles(first, 73918244);
            manager.AssignRoles(second, 73918244);

            for (var i = 0; i < first.Count; i++)
            {
                Assert.That(second[i].RoleId, Is.EqualTo(first[i].RoleId));
                Assert.That(second[i].IsThief, Is.EqualTo(first[i].IsThief));
            }
        }

        [Test]
        public void VoteManager_RejectsSelfVoteAndDuplicateVote()
        {
            var manager = new VoteManager();
            IReadOnlyCollection<string> players = new[] { "P1", "P2", "P3", "P4" };

            Assert.That(manager.SubmitVote("P1", "P1", players, players), Is.False);
            Assert.That(manager.SubmitVote("P1", "P2", players, players), Is.True);
            Assert.That(manager.SubmitVote("P1", "P3", players, players), Is.False);
        }

        [Test]
        public void VoteManager_ResolvesUniquePlurality()
        {
            var manager = new VoteManager();
            IReadOnlyCollection<string> players = new[] { "P1", "P2", "P3", "P4" };
            manager.SubmitVote("P1", "P2", players, players);
            manager.SubmitVote("P2", "P1", players, players);
            manager.SubmitVote("P3", "P2", players, players);
            manager.SubmitVote("P4", "P2", players, players);

            var result = manager.Resolve(players);

            Assert.That(result.IsResolved, Is.True);
            Assert.That(result.WinnerId, Is.EqualTo("P2"));
            Assert.That(result.Counts["P2"], Is.EqualTo(3));
        }

        [Test]
        public void VoteManager_ReturnsAllTiedLeadersForRunoff()
        {
            var manager = new VoteManager();
            IReadOnlyCollection<string> players = new[] { "P1", "P2", "P3", "P4" };
            manager.SubmitVote("P1", "P2", players, players);
            manager.SubmitVote("P2", "P1", players, players);
            manager.SubmitVote("P3", "P4", players, players);
            manager.SubmitVote("P4", "P3", players, players);

            var result = manager.Resolve(players);

            Assert.That(result.IsResolved, Is.False);
            Assert.That(result.TopCandidates.Count, Is.EqualTo(4));
        }

        [Test]
        public void ScoreResolver_CatchRewardsOnlyCorrectVotersByRoleValue()
        {
            var players = CreatePlayers();
            players[0].RoleId = "king";
            players[1].RoleId = "minister";
            players[2].RoleId = "soldier";
            players[3].RoleId = "servant";
            var votes = new Dictionary<string, string>
            {
                ["P1"] = "P2",
                ["P2"] = "P1",
                ["P3"] = "P2",
                ["P4"] = "P2"
            };

            var result = new ScoreResolver().Resolve(players, "P2", "P2", votes);

            Assert.That(result.Deltas["P1"], Is.EqualTo(8));
            Assert.That(result.Deltas["P3"], Is.EqualTo(5));
            Assert.That(result.Deltas["P2"], Is.EqualTo(0));
            Assert.That(result.Deltas["P4"], Is.EqualTo(0));
        }

        [Test]
        public void ScoreResolver_EscapeRewardExcludesThiefRoleValue()
        {
            var players = CreatePlayers();
            players[0].RoleId = "king";
            players[1].RoleId = "minister";
            players[2].RoleId = "soldier";
            players[3].RoleId = "servant";

            var result = new ScoreResolver().Resolve(players, "P4", null, new Dictionary<string, string>());

            Assert.That(result.Deltas["P4"], Is.EqualTo(19));
            Assert.That(result.Deltas["P1"], Is.EqualTo(0));
            Assert.That(result.Deltas["P2"], Is.EqualTo(0));
            Assert.That(result.Deltas["P3"], Is.EqualTo(0));
        }

        [Test]
        public void RoundManager_RejectsIllegalTransitions()
        {
            var manager = new RoundManager();

            Assert.That(manager.TryEnterPhase(RoundPhase.Reveal), Is.False);
            Assert.That(manager.TryEnterPhase(RoundPhase.Intro), Is.True);
            Assert.That(manager.TryEnterPhase(RoundPhase.Discussion), Is.False);
            Assert.That(manager.TryEnterPhase(RoundPhase.PrivateReveal), Is.True);
        }
    }
}
