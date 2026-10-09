using System.Collections.Generic;
using NUnit.Framework;
using ThiefAndTheNecklace.Networking;

namespace ThiefAndTheNecklace.Tests
{
    public sealed class PB2NetworkingTests
    {
        [Test]
        public void RegistryRequiresFourReadyConnectedPlayersAndLocksAtStart()
        {
            var registry = new NetworkPlayerRegistry();
            for (var i = 1; i <= 3; i++)
            {
                registry.Add("p" + i, "Player " + i);
                registry.SetReady("p" + i, true);
            }
            Assert.That(registry.TryLockRoster(), Is.False);
            registry.Add("p4", "Player 4");
            registry.SetReady("p4", true);
            Assert.That(registry.TryLockRoster(), Is.True);
            Assert.That(registry.Add("p5", "Late joiner"), Is.EqualTo(NetworkGameError.MatchStarted));
        }

        [Test]
        public void PrivateDispatcherOnlyReturnsStateToAuthenticatedOwner()
        {
            var dispatcher = new PrivateStateDispatcher();
            dispatcher.ReplaceRound(new[]
            {
                new PrivateRoundState("p1", "king", false, "In the hall", new[] { "Heard a bell" }),
                new PrivateRoundState("p2", "minister", true, "Near the chamber", new string[0])
            });
            PrivateRoundState state;
            Assert.That(dispatcher.TryGetForRecipient("p1", out state), Is.True);
            Assert.That(state.RoleId, Is.EqualTo("king"));
            Assert.That(dispatcher.TryGetForRecipient("p999", out state), Is.False);
            Assert.That(dispatcher.IsRevealed, Is.False);
            dispatcher.RevealRoles();
            Assert.That(dispatcher.IsRevealed, Is.True);
        }

        [Test]
        public void VoteControllerRejectsSelfDuplicateAndClosedRequests()
        {
            var controller = new AuthoritativeVoteController();
            controller.Open(new[] { "p1", "p2", "p3", "p4" }, new[] { "p1", "p2", "p3", "p4" });
            Assert.That(controller.Submit("p1", new VoteRequest("r1", "p1")).Accepted, Is.False);
            Assert.That(controller.Submit("p1", new VoteRequest("r2", "p2")).Accepted, Is.True);
            Assert.That(controller.Submit("p1", new VoteRequest("r2", "p3")).Accepted, Is.False);
            controller.CloseAndResolve();
            Assert.That(controller.Submit("p2", new VoteRequest("r3", "p1")).Error, Is.EqualTo(NetworkGameError.VoteClosed));
        }

        [Test]
        public void ChatPolicyValidatesPhaseLengthBurstAndRateAndOrdersMessages()
        {
            var policy = new NetworkChatPolicy();
            ChatMessage message;
            NetworkGameError error;
            Assert.That(policy.TryAccept("p1", "Hello", RoundPhase.Discussion, 1.0, out message, out error), Is.True);
            Assert.That(message.Sequence, Is.EqualTo(1));
            Assert.That(policy.TryAccept("p1", "Too soon", RoundPhase.Discussion, 1.1, out message, out error), Is.False);
            Assert.That(error, Is.EqualTo(NetworkGameError.RateLimited));
            Assert.That(policy.TryAccept("p1", "Hello", RoundPhase.Voting, 2.0, out message, out error), Is.False);
        }

        [Test]
        public void ScoreBookAppliesRoundAtMostOnce()
        {
            var players = new List<PlayerData>
            {
                new PlayerData("p1", "A") { RoleId = "king" },
                new PlayerData("p2", "B") { RoleId = "minister", IsThief = true },
                new PlayerData("p3", "C") { RoleId = "soldier" },
                new PlayerData("p4", "D") { RoleId = "servant" }
            };
            var book = new IdempotentScoreBook();
            var votes = new Dictionary<string, string> { { "p1", "p2" }, { "p3", "p2" } };
            IReadOnlyList<ScoreUpdate> updates;
            Assert.That(book.TryApplyRound("r1", players, "p2", "p2", votes, out updates), Is.True);
            Assert.That(players[0].MatchScore, Is.EqualTo(8));
            Assert.That(book.TryApplyRound("r1", players, "p2", "p2", votes, out updates), Is.False);
            Assert.That(players[0].MatchScore, Is.EqualTo(8));
        }
    }
}
