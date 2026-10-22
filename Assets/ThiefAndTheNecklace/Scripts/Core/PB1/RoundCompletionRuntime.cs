using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ThiefAndTheNecklace.PB1
{
    public enum RoundStatus { NotStarted, Preparing, Active, Resolving, Results, Complete, Aborted }
    public enum ProgressionDecision { StartNextRound, CompleteMatch, AbortMatch }
    public enum FinalTiePolicy { SharedRank, SecondaryMetric, SuddenDeath }
    public enum DisconnectMatchPolicy { ContinueWithRemainingPlayers, PauseAndWait, AbortIfBelowMinimum }
    public enum RoundFlowEventType { RoundResultsOpened, OutcomePresented, RoleRevealPresented, RoundScorePresented, CumulativeScorePresented, ContinueEnabled, PlayerReadyToContinue, NextRoundLoading, FinalResultsOpened, RematchAvailable, RoundCompleted, MatchCompleting, MatchResultsPublished, MatchCompleted }

    [Serializable]
    public sealed class ResultsConfig
    {
        public float MinimumDisplaySeconds=4f;
        public float MaximumDisplaySeconds=30f;
        public bool AllowEarlyContinue=true;
        public int MinimumPlayers=4;
        public DisconnectMatchPolicy DisconnectPolicy=DisconnectMatchPolicy.AbortIfBelowMinimum;
        public FinalTiePolicy TiePolicy=FinalTiePolicy.SharedRank;
        internal ResultsConfig Copy() => new ResultsConfig { MinimumDisplaySeconds=MinimumDisplaySeconds, MaximumDisplaySeconds=MaximumDisplaySeconds,
            AllowEarlyContinue=AllowEarlyContinue, MinimumPlayers=MinimumPlayers, DisconnectPolicy=DisconnectPolicy, TiePolicy=TiePolicy };
        public void Validate()
        {
            if (MinimumDisplaySeconds < 0 || float.IsNaN(MinimumDisplaySeconds) || float.IsInfinity(MinimumDisplaySeconds)) throw new ArgumentOutOfRangeException(nameof(MinimumDisplaySeconds));
            if (MaximumDisplaySeconds <= 0 || MaximumDisplaySeconds < MinimumDisplaySeconds || float.IsNaN(MaximumDisplaySeconds) || float.IsInfinity(MaximumDisplaySeconds)) throw new ArgumentOutOfRangeException(nameof(MaximumDisplaySeconds));
            if (MinimumPlayers < 1 || !Enum.IsDefined(typeof(DisconnectMatchPolicy), DisconnectPolicy) || !Enum.IsDefined(typeof(FinalTiePolicy), TiePolicy)) throw new ArgumentOutOfRangeException(nameof(MinimumPlayers));
            if (TiePolicy != FinalTiePolicy.SharedRank) throw new NotSupportedException("Only the explicitly defined SharedRank policy is implemented.");
        }
    }

    public sealed class CompletedRoundResult
    {
        public string MatchId { get; }
        public string RoundId { get; }
        public int RoundIndex { get; }
        public int TotalRounds { get; }
        public RevealOutcome Outcome { get; }
        public int? SelectedTargetId { get; }
        public int ThiefPlayerId { get; }
        public IReadOnlyDictionary<int,int> RoundScores { get; }
        public IReadOnlyDictionary<int,int> CumulativeScores { get; }
        public IReadOnlyList<RoleRevealRecord> RevealedRoles { get; }
        public IReadOnlyList<ScoreTransaction> Transactions { get; }
        public DateTimeOffset CompletedAt { get; }
        public bool Finalized { get; }
        internal CompletedRoundResult(string matchId, string roundId, int roundIndex, int totalRounds, RevealSnapshot reveal,
            int actualThiefPlayerId, ScoreSnapshot score, IEnumerable<RoleRevealRecord> roles, DateTimeOffset completedAt)
        {
            MatchId=matchId; RoundId=roundId; RoundIndex=roundIndex; TotalRounds=totalRounds; Outcome=reveal.Outcome;
            SelectedTargetId=reveal.SelectedTargetId; ThiefPlayerId=actualThiefPlayerId;
            RoundScores=new ReadOnlyDictionary<int,int>(new Dictionary<int,int>(score.RoundScores));
            CumulativeScores=new ReadOnlyDictionary<int,int>(new Dictionary<int,int>(score.MatchScores));
            RevealedRoles=Array.AsReadOnly(roles.ToArray()); Transactions=Array.AsReadOnly(score.Transactions.ToArray()); CompletedAt=completedAt; Finalized=score.Finalized;
        }
    }

    public sealed class PlayerFinalScore
    {
        public int PlayerId { get; }
        public int MatchScore { get; }
        public int Rank { get; }
        public bool IsWinner { get; }
        internal PlayerFinalScore(int playerId, int matchScore, int rank, bool isWinner)
        { PlayerId=playerId; MatchScore=matchScore; Rank=rank; IsWinner=isWinner; }
    }

    public sealed class MatchResult
    {
        public string MatchId { get; }
        public IReadOnlyList<PlayerFinalScore> Players { get; }
        public IReadOnlyList<int> WinnerPlayerIds { get; }
        public DateTimeOffset CompletedAt { get; }
        internal MatchResult(string matchId, IEnumerable<PlayerFinalScore> players, DateTimeOffset completedAt)
        { MatchId=matchId; var rows=players.ToArray(); Players=Array.AsReadOnly(rows); WinnerPlayerIds=Array.AsReadOnly(rows.Where(row => row.IsWinner).Select(row => row.PlayerId).ToArray()); CompletedAt=completedAt; }
    }

    public sealed class RoundAdvanceState
    {
        private readonly HashSet<int> ready=new HashSet<int>();
        public bool ResultsActive { get; internal set; }
        public IReadOnlyCollection<int> ReadyPlayers => Array.AsReadOnly(ready.OrderBy(id => id).ToArray());
        public long StartedAtMilliseconds { get; internal set; }
        public long DeadlineMilliseconds { get; internal set; }
        public bool ForcedAdvance { get; internal set; }
        internal bool AddReady(int playerId) => ready.Add(playerId);
        internal bool IsReady(int playerId) => ready.Contains(playerId);
        internal void Reset() { ready.Clear(); ResultsActive=false; ForcedAdvance=false; StartedAtMilliseconds=0; DeadlineMilliseconds=0; }
    }

    public sealed class RoundCompletionResult
    {
        public string MatchId { get; }
        public string RoundId { get; }
        public int RoundIndex { get; }
        public bool MatchComplete { get; }
        internal RoundCompletionResult(string matchId, string roundId, int roundIndex, bool matchComplete)
        { MatchId=matchId; RoundId=roundId; RoundIndex=roundIndex; MatchComplete=matchComplete; }
    }

    public sealed class RoundFlowEvent
    {
        public string MatchId { get; }
        public string RoundId { get; }
        public long Sequence { get; }
        public RoundFlowEventType Type { get; }
        public int? PlayerId { get; }
        public CompletedRoundResult RoundResult { get; }
        public MatchResult MatchResult { get; }
        internal RoundFlowEvent(string matchId, string roundId, long sequence, RoundFlowEventType type, CompletedRoundResult roundResult=null, MatchResult matchResult=null, int? playerId=null)
        { MatchId=matchId; RoundId=roundId; Sequence=sequence; Type=type; RoundResult=roundResult; MatchResult=matchResult; PlayerId=playerId; }
    }

    /// <summary>Owns the durable results boundary and continuation handshake after GW8.</summary>
    public sealed class RoundCompletionService
    {
        private readonly ResultsConfig config;
        private readonly Func<long> clock;
        private readonly List<CompletedRoundResult> rounds=new List<CompletedRoundResult>();
        private readonly Dictionary<string,CompletedRoundResult> byRound=new Dictionary<string,CompletedRoundResult>(StringComparer.Ordinal);
        private long sequence;
        private MatchResult finalResult;
        public RoundAdvanceState AdvanceState { get; } = new RoundAdvanceState();
        public IReadOnlyList<CompletedRoundResult> RoundHistory => Array.AsReadOnly(rounds.ToArray());
        public MatchResult FinalResult => finalResult;
        public CompletedRoundResult CurrentRoundResult { get; private set; }
        public ResultsConfig Config => config.Copy();
        public event Action<RoundFlowEvent> EventPublished;

        public RoundCompletionService(ResultsConfig resultsConfig=null, Func<long> timestampProvider=null)
        { config=(resultsConfig ?? new ResultsConfig()).Copy(); config.Validate(); clock=timestampProvider ?? (() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()); }

        public CompletedRoundResult OpenResults(MatchState match, RevealSnapshot reveal, int actualThiefPlayerId, ScoreSnapshot score, IEnumerable<RoleRevealRecord> roles, bool publishEvents=true)
        {
            if (match == null || reveal == null || score == null || reveal.Status != RevealStatus.Complete || !score.Finalized) throw new InvalidOperationException("Round results require completed reveal and finalized scoring.");
            if (match.MatchId != reveal.MatchId || match.MatchId != score.MatchId || reveal.RoundId != score.RoundId || match.CurrentRound < 1 || match.TotalRounds < match.CurrentRound)
                throw new InvalidOperationException("Round result identities or indices are invalid.");
            if (byRound.TryGetValue(reveal.RoundId, out var existing)) { CurrentRoundResult=existing; return existing; }
            var result=new CompletedRoundResult(match.MatchId, reveal.RoundId, match.CurrentRound, match.TotalRounds, reveal,
                actualThiefPlayerId, score, roles, DateTimeOffset.UtcNow);
            if (result.RoundScores.Keys.Except(result.CumulativeScores.Keys).Any() || result.RoundScores.Values.Any(value => value < 0) || result.CumulativeScores.Values.Any(value => value < 0))
                throw new InvalidOperationException("Round result contains invalid score entries.");
            var previous=rounds.LastOrDefault();
            if ((previous == null && result.RoundIndex != 1) || (previous != null && (result.RoundIndex != previous.RoundIndex + 1 || previous.MatchId != result.MatchId)))
                throw new InvalidOperationException("Round results must be appended in sequence for one match.");
            foreach (var playerId in result.RoundScores.Keys)
            {
                var prior=previous != null && previous.CumulativeScores.TryGetValue(playerId, out var oldTotal) ? oldTotal : 0;
                if (!result.CumulativeScores.TryGetValue(playerId, out var total) || checked(prior + result.RoundScores[playerId]) != total)
                    throw new InvalidOperationException("Cumulative score does not equal prior total plus this round score.");
            }
            rounds.Add(result); byRound.Add(result.RoundId, result); CurrentRoundResult=result;
            var now=clock(); AdvanceState.Reset(); AdvanceState.ResultsActive=true; AdvanceState.StartedAtMilliseconds=now;
            AdvanceState.DeadlineMilliseconds=checked(now + (long)(config.MaximumDisplaySeconds * 1000f));
            if (publishEvents) PublishResults(result);
            return result;
        }

        public void PublishResults(CompletedRoundResult result)
        {
            if (result == null || !ReferenceEquals(result, CurrentRoundResult) || !AdvanceState.ResultsActive) throw new InvalidOperationException("Only the active persisted round result can be published.");
            Publish(result.MatchId, result.RoundId, RoundFlowEventType.RoundResultsOpened, result);
            Publish(result.MatchId, result.RoundId, RoundFlowEventType.OutcomePresented, result);
            Publish(result.MatchId, result.RoundId, RoundFlowEventType.RoleRevealPresented, result);
            Publish(result.MatchId, result.RoundId, RoundFlowEventType.RoundScorePresented, result);
            Publish(result.MatchId, result.RoundId, RoundFlowEventType.CumulativeScorePresented, result);
            Publish(result.MatchId, result.RoundId, RoundFlowEventType.ContinueEnabled, result);
        }

        public bool Continue(MatchState match, int playerId, string matchId, string roundId)
        {
            if (CurrentRoundResult == null || !AdvanceState.ResultsActive) throw new InvalidOperationException("Round results are not active.");
            if (!StringComparer.Ordinal.Equals(matchId, CurrentRoundResult.MatchId) || !StringComparer.Ordinal.Equals(roundId, CurrentRoundResult.RoundId)) throw new InvalidOperationException("StaleRound: continue command does not match current results.");
            var player=match.Players.FirstOrDefault(item => item.PlayerId == playerId);
            if (player == null || !player.Connected) throw new InvalidOperationException("Only a connected match participant can continue.");
            var added=AdvanceState.AddReady(playerId);
            if (added) Publish(CurrentRoundResult.MatchId, CurrentRoundResult.RoundId, RoundFlowEventType.PlayerReadyToContinue, CurrentRoundResult, playerId:playerId);
            return added;
        }

        public bool CanAdvance(MatchState match)
        {
            if (CurrentRoundResult == null || !AdvanceState.ResultsActive) return false;
            var now=clock();
            if (now >= AdvanceState.DeadlineMilliseconds) { AdvanceState.ForcedAdvance=true; return true; }
            if (!config.AllowEarlyContinue || now < AdvanceState.StartedAtMilliseconds + (long)(config.MinimumDisplaySeconds * 1000f)) return false;
            var eligible=match.Players.Where(player => player.Connected).Select(player => player.PlayerId).ToArray();
            return eligible.Length > 0 && eligible.All(AdvanceState.IsReady);
        }

        public ProgressionDecision DecideProgression(int completedRound, int totalRounds)
        { if (completedRound < 1 || totalRounds < completedRound) throw new ArgumentOutOfRangeException(nameof(completedRound)); return completedRound < totalRounds ? ProgressionDecision.StartNextRound : ProgressionDecision.CompleteMatch; }

        public bool HasMinimumPlayers(MatchState match) => match.Players.Count(player => player.Connected) >= config.MinimumPlayers;

        public MatchResult BuildFinalResult(MatchState match)
        {
            if (match == null) throw new ArgumentNullException(nameof(match));
            if (finalResult != null) return finalResult;
            if (rounds.Count != match.TotalRounds || rounds.Any(result => !result.Finalized) || rounds.Select(result => result.RoundIndex).Distinct().Count() != match.TotalRounds)
                throw new InvalidOperationException("Final scores cannot be ranked until every round is finalized.");
            var ordered=match.Players.Select(player => new { player.PlayerId, player.MatchScore }).OrderByDescending(player => player.MatchScore).ThenBy(player => player.PlayerId).ToArray();
            if (ordered.Any(player => player.MatchScore < 0)) throw new InvalidOperationException("Final scores must be non-negative.");
            var highest=ordered.Length == 0 ? 0 : ordered[0].MatchScore;
            var rows=new List<PlayerFinalScore>(); int rank=0; int priorScore=int.MinValue;
            for (var index=0; index<ordered.Length; index++)
            {
                if (ordered[index].MatchScore != priorScore) rank=index+1;
                rows.Add(new PlayerFinalScore(ordered[index].PlayerId, ordered[index].MatchScore, rank, ordered[index].MatchScore == highest));
                priorScore=ordered[index].MatchScore;
            }
            finalResult=new MatchResult(match.MatchId, rows, DateTimeOffset.UtcNow);
            Publish(match.MatchId, CurrentRoundResult.RoundId, RoundFlowEventType.MatchResultsPublished, CurrentRoundResult, finalResult);
            return finalResult;
        }

        public void MarkResultsComplete()
        { if (!AdvanceState.ResultsActive || CurrentRoundResult == null) throw new InvalidOperationException("Results are not active."); AdvanceState.ResultsActive=false; }

        public void PublishProgression(RoundFlowEventType type, MatchResult matchResult=null)
            => Publish(CurrentRoundResult?.MatchId ?? finalResult?.MatchId ?? string.Empty,
                CurrentRoundResult?.RoundId ?? string.Empty, type, CurrentRoundResult, matchResult);

        private void Publish(string matchId, string roundId, RoundFlowEventType type, CompletedRoundResult result=null, MatchResult final=null, int? playerId=null)
            => EventPublished?.Invoke(new RoundFlowEvent(matchId, roundId, ++sequence, type, result, final, playerId));
    }
}
