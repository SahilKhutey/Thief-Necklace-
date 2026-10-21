using System;
using System.Linq;

namespace ThiefAndTheNecklace.PB1
{
    public sealed class RuntimeVoteService
    {
        public bool SubmitVote(GameRuntimeState state, int voterId, int targetId)
        {
            if (state == null || state.Match == null || state.Match.Players == null) return false;
            if (state.Match.CurrentRoundState.Phase != RoundPhase.Voting) return false;
            var voter=state.Match.Players.FirstOrDefault(p => p.PlayerId == voterId);
            var target=state.Match.Players.FirstOrDefault(p => p.PlayerId == targetId);
            if (voter == null || target == null || !voter.Connected || !target.Connected || voterId == targetId || voter.Vote.Submitted) return false;
            if (state.Match.CurrentRoundState.VoteRound > 0 && !state.Match.CurrentRoundState.RunoffCandidates.Contains(targetId)) return false;
            voter.Vote.TargetPlayerId=targetId;
            voter.Vote.Submitted=true;
            state.Votes.Votes[voterId]=targetId;
            return true;
        }
        public bool EveryoneVoted(GameRuntimeState state) => state != null && state.Match != null && state.Match.Players.Any(p => p.Connected) && state.Match.Players.Where(p => p.Connected).All(p => p.Vote.Submitted);
    }

    public sealed class RuntimeVoteResolver
    {
        public int? GetWinner(GameRuntimeState state)
        {
            var groups=state.Votes.Votes.GroupBy(x => x.Value).Select(g => new { TargetId=g.Key, Votes=g.Count() }).OrderByDescending(x => x.Votes).ThenBy(x => x.TargetId).ToList();
            if (groups.Count == 0 || groups.Count > 1 && groups[0].Votes == groups[1].Votes) return null;
            return groups[0].TargetId;
        }
        public VoteResolution Resolve(GameRuntimeState state, bool finalVote)
        {
            var targetId=GetWinner(state);
            if (!targetId.HasValue) return finalVote ? VoteResolution.ThiefEscaped : VoteResolution.RevoteRequired;
            var target=state.Match.Players.FirstOrDefault(p => p.PlayerId == targetId.Value);
            if (target == null) return VoteResolution.ThiefEscaped;
            return target.IsThief ? VoteResolution.ThiefCaught : VoteResolution.ThiefEscaped;
        }
    }

    public sealed class RuntimeScoreService
    {
        public void Resolve(GameRuntimeState state)
        {
            if (state == null || state.Match == null || state.Match.CurrentRoundState == null) throw new ArgumentNullException(nameof(state));
            var round=state.Match.CurrentRoundState;
            if (round.Phase != RoundPhase.Reveal || round.ScoringResolved) throw new InvalidOperationException("Scoring can resolve once, after reveal.");
            var thief=state.Match.Players.SingleOrDefault(p => p.IsThief);
            if (thief == null || state.Match.Players.Count(p => p.IsThief) != 1) throw new InvalidOperationException("Scoring requires exactly one Thief.");
            foreach (var player in state.Match.Players) player.RoundScore=0;
            if (round.ThiefEscaped) thief.RoundScore=state.Match.Players.Where(p => !p.IsThief).Sum(GetRoleValue);
            else
            {
                foreach (var voter in state.Match.Players.Where(p => !p.IsThief && p.Vote.TargetPlayerId == thief.PlayerId)) voter.RoundScore=GetRoleValue(voter.Role);
            }
            state.Scores.RoundScores.Clear();
            foreach (var player in state.Match.Players) state.Scores.RoundScores[player.PlayerId]=player.RoundScore;
            round.ScoringResolved=true;
        }

        public void Commit(GameRuntimeState state)
        {
            var round=state.Match.CurrentRoundState;
            if (!round.ScoringResolved) throw new InvalidOperationException("Resolve round scoring before commit.");
            if (state.Scores.CommittedRounds.Contains(round.RoundNumber)) throw new InvalidOperationException("Round scores have already been committed.");
            var totals=state.Match.Players.ToDictionary(p => p.PlayerId, p => checked(p.MatchScore + p.RoundScore));
            foreach (var player in state.Match.Players) player.MatchScore=totals[player.PlayerId];
            state.Scores.CommittedRounds.Add(round.RoundNumber);
        }

        public static int GetRoleValue(RoleType role)
        {
            var definition = ThiefAndTheNecklace.RoleCatalog.Find(role.ToString().ToLowerInvariant());
            return definition == null ? 0 : definition.PointValue;
        }
    }

    public sealed class RoundResolver
    {
        private readonly RuntimeVoteService votes;
        private readonly RuntimeVoteResolver voteResolver;
        private readonly RuntimeScoreService scores;
        public RoundResolver(RuntimeVoteService voteService, RuntimeVoteResolver resolver, RuntimeScoreService scoreService)
        { votes=voteService ?? throw new ArgumentNullException(nameof(voteService)); voteResolver=resolver ?? throw new ArgumentNullException(nameof(resolver)); scores=scoreService ?? throw new ArgumentNullException(nameof(scoreService)); }
        public VoteResolution ResolveVotes(GameRuntimeState state, bool finalVote, bool allowPartial = false)
        {
            if (!allowPartial && !votes.EveryoneVoted(state)) throw new InvalidOperationException("Not every connected player has voted.");
            var result=voteResolver.Resolve(state, finalVote);
            state.Match.CurrentRoundState.Resolution=result;
            state.Match.CurrentRoundState.AccusedPlayerId=voteResolver.GetWinner(state);
            state.Match.CurrentRoundState.ThiefEscaped=result == VoteResolution.ThiefEscaped;
            return result;
        }
        public void ResolveScoring(GameRuntimeState state) { scores.Resolve(state); scores.Commit(state); }
    }
}
