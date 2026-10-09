using System;
using System.Collections.Generic;
using ThiefAndTheNecklace;

namespace ThiefAndTheNecklace.Networking
{
    public sealed class IdempotentScoreBook
    {
        private readonly HashSet<string> _resolvedRoundIds = new HashSet<string>();

        public bool TryApplyRound(string roundId, IReadOnlyList<PlayerData> players, string thiefId, string accusedId,
            IReadOnlyDictionary<string, string> finalVotes, out IReadOnlyList<ScoreUpdate> updates)
        {
            if (string.IsNullOrWhiteSpace(roundId)) throw new ArgumentException("A unique round ID is required.", nameof(roundId));
            if (players == null) throw new ArgumentNullException(nameof(players));
            if (_resolvedRoundIds.Contains(roundId)) { updates = new ScoreUpdate[0]; return false; }
            var result = new ScoreResolver().Resolve(players, thiefId, accusedId, finalVotes);
            _resolvedRoundIds.Add(roundId);
            var applied = new List<ScoreUpdate>();
            foreach (var player in players)
            {
                int delta;
                if (!result.Deltas.TryGetValue(player.PlayerId, out delta)) delta = 0;
                player.MatchScore += delta;
                string reason;
                result.Reasons.TryGetValue(player.PlayerId, out reason);
                applied.Add(new ScoreUpdate(player.PlayerId, delta, player.MatchScore, reason ?? string.Empty));
            }
            updates = applied;
            return true;
        }

        public void ResetMatch() => _resolvedRoundIds.Clear();
    }
}
