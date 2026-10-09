using System;
using System.Collections.Generic;

namespace ThiefAndTheNecklace
{
    public sealed class ScoreResolver
    {
        public RoundScoreResult Resolve(
            IReadOnlyList<PlayerData> players,
            string thiefId,
            string accusedId,
            IReadOnlyDictionary<string, string> finalVotes)
        {
            if (players == null) throw new ArgumentNullException(nameof(players));
            if (finalVotes == null) throw new ArgumentNullException(nameof(finalVotes));
            if (string.IsNullOrEmpty(thiefId)) throw new ArgumentException("A Thief must be assigned.", nameof(thiefId));
            if (FindPlayer(players, thiefId) == null) throw new InvalidOperationException("Thief is not in the player list.");

            var deltas = new Dictionary<string, int>();
            var reasons = new Dictionary<string, string>();
            foreach (var player in players) deltas[player.PlayerId] = 0;

            if (!string.IsNullOrEmpty(accusedId) && accusedId == thiefId)
            {
                foreach (var vote in finalVotes)
                {
                    if (vote.Value != thiefId) continue;
                    var voter = FindPlayer(players, vote.Key);
                    if (voter == null || voter.IsThief) continue;
                    var role = RoleCatalog.Find(voter.RoleId);
                    if (role == null) continue;
                    deltas[voter.PlayerId] = role.PointValue;
                    reasons[voter.PlayerId] = "Correctly identified the Thief";
                }
            }
            else
            {
                var reward = 0;
                foreach (var player in players)
                {
                    if (player.PlayerId == thiefId) continue;
                    var role = RoleCatalog.Find(player.RoleId);
                    if (role != null) reward += role.PointValue;
                }
                deltas[thiefId] = reward;
                reasons[thiefId] = "The Thief escaped; reward equals innocent role values";
            }

            return new RoundScoreResult(deltas, reasons);
        }

        private static PlayerData FindPlayer(IReadOnlyList<PlayerData> players, string playerId)
        {
            foreach (var player in players)
                if (player.PlayerId == playerId) return player;
            return null;
        }
    }
}
