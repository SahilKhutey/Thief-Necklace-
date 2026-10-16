using System;
using System.Collections.Generic;
using System.Linq;

namespace ThiefAndTheNecklace.PB1
{
    public sealed class VoteService
    {
        public bool SubmitVote(GameState state, int voterId, int targetId, IReadOnlyCollection<int> eligibleTargets = null)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.Phase != RoundPhase.Voting) return false;
            var voter = state.Players.FirstOrDefault(p => p.PlayerId == voterId && p.Connected);
            var target = state.Players.FirstOrDefault(p => p.PlayerId == targetId && p.Connected);
            if (voter == null || target == null || voter.HasSubmittedVote || voterId == targetId) return false;
            if (eligibleTargets != null && !eligibleTargets.Contains(targetId)) return false;
            voter.VoteTargetId = targetId;
            voter.HasSubmittedVote = true;
            return true;
        }

        public bool EveryoneHasVoted(GameState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            return state.Players.Where(p => p.Connected).All(p => p.HasSubmittedVote);
        }
    }
}
