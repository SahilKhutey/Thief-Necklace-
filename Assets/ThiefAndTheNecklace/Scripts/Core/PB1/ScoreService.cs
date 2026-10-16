using System;
using System.Collections.Generic;
using System.Linq;

namespace ThiefAndTheNecklace.PB1
{
    public sealed class ScoreService
    {
        public void CalculateRoundScore(GameState state, VoteOutcome outcome)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (outcome == null) throw new ArgumentNullException(nameof(outcome));
            if (state.Phase != RoundPhase.Reveal) throw new InvalidOperationException("Scores can be calculated only after reveal.");
            if (state.RoundScoreCalculated || state.RoundScoreCommitted) throw new InvalidOperationException("Round score has already been calculated.");
            if (state.Players.Count(p => p.IsThief) != 1) throw new InvalidOperationException("Exactly one Thief is required for scoring.");
            foreach (var player in state.Players) player.RoundScore = 0;
            var thief = state.Players.Single(p => p.IsThief);
            if (outcome.Result == VoteResolution.ThiefCaught && outcome.AccusedPlayerId != thief.PlayerId)
                throw new InvalidOperationException("Caught result must accuse the Thief.");
            if (outcome.Result == VoteResolution.ThiefEscaped && outcome.AccusedPlayerId == thief.PlayerId)
                throw new InvalidOperationException("An escaped Thief cannot be the accused winner.");
            if (outcome.Result != VoteResolution.ThiefCaught && outcome.Result != VoteResolution.ThiefEscaped)
                throw new InvalidOperationException("A revote or undecided outcome cannot be scored.");
            if (outcome.Result == VoteResolution.ThiefEscaped)
            {
                thief.RoundScore = state.Players.Where(p => !p.IsThief).Sum(p => RoleValue(p.Role));
            }
            else if (outcome.Result == VoteResolution.ThiefCaught)
            {
                foreach (var player in state.Players.Where(p => !p.IsThief && p.VoteTargetId == thief.PlayerId))
                    player.RoundScore = RoleValue(player.Role);
            }
            if (state.Players.Any(p => p.RoundScore < 0)) throw new InvalidOperationException("Scores cannot be negative.");
            state.RoundScoreCalculated = true;
        }

        public void CommitRoundScores(GameState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.RoundScoreCommitted) throw new InvalidOperationException("Round score has already been committed.");
            if (!state.RoundScoreCalculated) throw new InvalidOperationException("Round score must be calculated before it can be committed.");
            var updatedScores = state.Players.ToDictionary(player => player, player => checked(player.MatchScore + player.RoundScore));
            foreach (var pair in updatedScores) pair.Key.MatchScore = pair.Value;
            state.RoundScoreCommitted = true;
        }

        public static int RoleValue(RoleType role)
        {
            if (!Enum.IsDefined(typeof(RoleType), role)) throw new ArgumentOutOfRangeException(nameof(role), "Unknown role.");
            return RuntimeScoreService.GetRoleValue(role);
        }
    }
}
