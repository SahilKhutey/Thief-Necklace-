using System;
using System.Collections.Generic;
using System.Linq;

namespace ThiefAndTheNecklace.PB1
{
    public sealed class VoteResolutionService
    {
        public VoteOutcome Resolve(GameState state, bool isFinalVote, IReadOnlyCollection<int> eligibleTargets = null, bool allowPartial = false)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.Phase != RoundPhase.VoteResolution) throw new InvalidOperationException("Votes can be resolved only in VoteResolution phase.");
            var voters = allowPartial ? state.Players.Where(player => player.HasSubmittedVote).ToList() : state.Players.Where(p => p.Connected).ToList();
            if (voters.Count == 0 || !allowPartial && voters.Any(p => !p.HasSubmittedVote)) throw new InvalidOperationException("Cannot resolve before every connected player votes.");
            var allowed = eligibleTargets == null ? state.Players.Select(p => p.PlayerId).ToList() : eligibleTargets.ToList();
            var counts = allowed.Distinct().ToDictionary(id => id, _ => 0);
            foreach (var voter in voters.Where(player => player.HasSubmittedVote))
            {
                if (!voter.VoteTargetId.HasValue || !counts.ContainsKey(voter.VoteTargetId.Value))
                    throw new InvalidOperationException("A vote targets a player outside the eligible ballot.");
                counts[voter.VoteTargetId.Value]++;
            }
            var highest = counts.Values.DefaultIfEmpty(0).Max();
            var leaders = counts.Where(pair => pair.Value == highest).Select(pair => pair.Key).ToList();
            if (leaders.Count != 1)
                return new VoteOutcome(isFinalVote ? VoteResolution.ThiefEscaped : VoteResolution.RevoteRequired, null, counts);
            var accused = state.Players.FirstOrDefault(p => p.PlayerId == leaders[0]);
            if (accused == null) throw new InvalidOperationException("Vote winner is not a player.");
            return new VoteOutcome(accused.IsThief ? VoteResolution.ThiefCaught : VoteResolution.ThiefEscaped, accused.PlayerId, counts);
        }
    }
}
