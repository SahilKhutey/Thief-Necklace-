using System;
using System.Linq;

namespace ThiefAndTheNecklace.PB1
{
    public sealed class RuntimeInvariantValidator
    {
        public void Validate(GameRuntimeState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.Match == null || state.Match.CurrentRoundState == null || state.Scenario == null || state.Votes == null || state.Scores == null) throw new InvalidOperationException("Runtime state is incomplete.");
            var match = state.Match;
            if (string.IsNullOrWhiteSpace(match.MatchId)) throw new InvalidOperationException("Match ID is required.");
            if (match.TotalRounds < 1) throw new InvalidOperationException("Total rounds must be greater than zero.");
            if (match.CurrentRound < 1 || match.CurrentRound > match.TotalRounds) throw new InvalidOperationException("Invalid current round.");
            if (!Enum.IsDefined(typeof(MatchStatus), match.Status) || !Enum.IsDefined(typeof(DifficultyLevel), match.Difficulty)) throw new InvalidOperationException("Unknown match status or difficulty.");
            if (match.Players == null || match.Players.Count < 4 || match.Players.Count > 6) throw new InvalidOperationException("Player count must be between 4 and 6.");
            if (match.Players.Any(p => p == null || p.PlayerId <= 0 || string.IsNullOrWhiteSpace(p.DisplayName) || p.Knowledge == null || p.Vote == null)) throw new InvalidOperationException("Player state is incomplete.");
            if (match.Players.Select(p => p.PlayerId).Distinct().Count() != match.Players.Count) throw new InvalidOperationException("Duplicate player IDs.");
            if (match.Status == MatchStatus.InProgress || match.Status == MatchStatus.Completed)
            {
                if (match.Players.Count(p => p.IsThief) != 1) throw new InvalidOperationException("Exactly one Thief is required.");
                if (match.Players.Select(p => p.Role).Distinct().Count() != match.Players.Count || match.Players.Any(p => p.Role == RoleType.Thief)) throw new InvalidOperationException("Each player must have one unique court role.");
                if (!state.Scenario.Validated || string.IsNullOrWhiteSpace(match.CurrentRoundState.ScenarioId)) throw new InvalidOperationException("The active round needs a validated scenario.");
                if (state.Scenario.ScenarioId != match.CurrentRoundState.ScenarioId || state.Scenario.GenerationSeed != SeedUtility.GetRoundSeed(match.Seed, match.CurrentRound)) throw new InvalidOperationException("Round scenario identity or seed does not match the match context.");
                if (match.Players.Any(p => string.IsNullOrWhiteSpace(p.Knowledge.Situation) || string.IsNullOrWhiteSpace(p.Knowledge.Objective) || p.Knowledge.EvidenceIds == null || p.Knowledge.EvidenceIds.Count == 0)) throw new InvalidOperationException("Every player needs private round information.");
            }
            if (match.Players.Any(p => p.MatchScore < 0 || p.RoundScore < 0)) throw new InvalidOperationException("Scores cannot be negative.");
            var round = match.CurrentRoundState;
            if (round.RoundNumber != match.CurrentRound) throw new InvalidOperationException("Match and round counters differ.");
            if (!Enum.IsDefined(typeof(RoundPhase), round.Phase) || !Enum.IsDefined(typeof(VoteResolution), round.Resolution)) throw new InvalidOperationException("Unknown round phase or resolution.");
            if (round.Completed && round.Phase != RoundPhase.RoundEnd && round.Phase != RoundPhase.MatchEnd) throw new InvalidOperationException("Completed round is outside an end phase.");
            if (round.Completed && (!round.ScoringResolved || !state.Scores.CommittedRounds.Contains(round.RoundNumber))) throw new InvalidOperationException("A completed round must have exactly one committed score result.");
            if (round.Phase == RoundPhase.Results && (round.Status != RoundStatus.Results || !round.ScoringResolved || !state.Scores.CommittedRounds.Contains(round.RoundNumber))) throw new InvalidOperationException("Results require finalized scoring and a persisted score commit.");
            if (round.Status == RoundStatus.Complete && !round.Completed) throw new InvalidOperationException("A complete round must be marked completed.");
            if (round.Status == RoundStatus.Aborted && match.Status != MatchStatus.Aborted) throw new InvalidOperationException("An aborted round requires an aborted match.");
            if (match.Status == MatchStatus.Completed && (round.Phase != RoundPhase.MatchEnd || match.CurrentRound != match.TotalRounds)) throw new InvalidOperationException("Completed match is not at the final round end.");
            if (match.Status == MatchStatus.InProgress && round.Phase == RoundPhase.MatchEnd) throw new InvalidOperationException("Active match cannot be in MatchEnd.");
            if (state.Scores.CommittedRounds.Distinct().Count() != state.Scores.CommittedRounds.Count) throw new InvalidOperationException("A round score was committed more than once.");
            if (round.ScoringResolved && match.Players.Any(p => !state.Scores.RoundScores.ContainsKey(p.PlayerId) || state.Scores.RoundScores[p.PlayerId] != p.RoundScore)) throw new InvalidOperationException("Score state differs from player round scores.");
            if (state.Votes.Votes.Any(v => !match.Players.Any(p => p.PlayerId == v.Key && p.Connected) || !match.Players.Any(p => p.PlayerId == v.Value))) throw new InvalidOperationException("Vote state references an invalid player.");
            if (state.Votes.Votes.Any(v => !match.Players.Any(p => p.PlayerId == v.Key && p.Vote.Submitted && p.Vote.TargetPlayerId == v.Value)) || match.Players.Any(p => p.Vote.Submitted && (!p.Vote.TargetPlayerId.HasValue || !state.Votes.Votes.ContainsKey(p.PlayerId)))) throw new InvalidOperationException("Player and round vote states differ.");
            if (state.Votes.VoteRound != round.VoteRound) throw new InvalidOperationException("Round vote counters differ.");
        }

        public void ValidateScores(GameRuntimeState state)
        {
            if (state == null || state.Match == null || state.Match.Players == null) throw new ArgumentNullException(nameof(state));
            foreach (var player in state.Match.Players)
                if (player.RoundScore < 0 || player.MatchScore < 0) throw new InvalidOperationException("Negative score.");
        }
    }

    public sealed class StateTransitionGuard
    {
        public bool CanTransition(RoundPhase current, RoundPhase next)
        {
            switch (current)
            {
                case RoundPhase.Waiting: return next == RoundPhase.Starting;
                case RoundPhase.Starting: return next == RoundPhase.PrivateReveal;
                case RoundPhase.PrivateReveal: return next == RoundPhase.Discussion;
                case RoundPhase.Discussion: return next == RoundPhase.Voting;
                case RoundPhase.Voting: return next == RoundPhase.VoteResolution;
                case RoundPhase.VoteResolution: return next == RoundPhase.Reveal || next == RoundPhase.Voting;
                case RoundPhase.Reveal: return next == RoundPhase.Scoring;
                case RoundPhase.Scoring: return next == RoundPhase.Results;
                case RoundPhase.Results: return next == RoundPhase.RoundEnd;
                case RoundPhase.RoundEnd: return next == RoundPhase.NextRound || next == RoundPhase.MatchEnd;
                case RoundPhase.NextRound: return next == RoundPhase.Starting;
                default: return false;
            }
        }
        public void RequireTransition(RoundPhase current, RoundPhase next)
        {
            if (!CanTransition(current, next)) throw new InvalidOperationException("Illegal transition " + current + " -> " + next + ".");
        }
    }
}
