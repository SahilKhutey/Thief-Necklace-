using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ThiefAndTheNecklace.PB1
{
    public enum ScoreReason { CorrectThiefVote, IncorrectThiefVote, ThiefEscapeReward, CaughtThiefReward, NoScore, ManualAdjustment }
    public enum EscapeRewardPolicy { SumNormalRoleValues, FixedValue }
    public enum CorrectVoteRewardPolicy { OwnRoleValue, FixedValue }
    public enum ScoreEventType { ScoringStarted, ScoreTransactionCreated, RoundScoreFinalized, CumulativeScoreUpdated, ScoringResolved }

    [Serializable]
    public sealed class ScoringConfig
    {
        public Dictionary<RoleType, int> RoleValues = new Dictionary<RoleType, int>
        {
            [RoleType.King]=8, [RoleType.Queen]=7, [RoleType.Minister]=6,
            [RoleType.Soldier]=5, [RoleType.Guard]=5, [RoleType.Servant]=4, [RoleType.Thief]=0
        };
        public bool AwardRoleValueToCorrectVoter = true;
        public CorrectVoteRewardPolicy CorrectVoteRewardPolicy = CorrectVoteRewardPolicy.OwnRoleValue;
        public int CorrectVoteFixedReward;
        public int IncorrectVoteReward;
        public int CaughtThiefReward;
        public EscapeRewardPolicy EscapeRewardPolicy = EscapeRewardPolicy.SumNormalRoleValues;
        public int EscapeFixedReward;

        public void Validate()
        {
            if (RoleValues == null || Enum.GetValues(typeof(RoleType)).Cast<RoleType>().Any(role => !RoleValues.ContainsKey(role) || RoleValues[role] < 0))
                throw new InvalidOperationException("Every role must have a non-negative configured score value.");
            if (IncorrectVoteReward < 0 || CaughtThiefReward < 0 || CorrectVoteFixedReward < 0 || EscapeFixedReward < 0)
                throw new InvalidOperationException("Score rewards cannot be negative.");
            if (!Enum.IsDefined(typeof(CorrectVoteRewardPolicy), CorrectVoteRewardPolicy) || !Enum.IsDefined(typeof(EscapeRewardPolicy), EscapeRewardPolicy))
                throw new InvalidOperationException("Unknown score reward policy.");
        }
        internal ScoringConfig Copy()
        {
            var copy=(ScoringConfig)MemberwiseClone(); copy.RoleValues=new Dictionary<RoleType,int>(RoleValues); return copy;
        }
    }

    public sealed class ScoreTransaction
    {
        public string TransactionId { get; }
        public string MatchId { get; }
        public string RoundId { get; }
        public int PlayerId { get; }
        public int Amount { get; }
        public ScoreReason Reason { get; }
        internal ScoreTransaction(string transactionId, string matchId, string roundId, int playerId, int amount, ScoreReason reason)
        { TransactionId=transactionId; MatchId=matchId; RoundId=roundId; PlayerId=playerId; Amount=amount; Reason=reason; }
    }

    public sealed class ScoreSnapshot
    {
        public string MatchId { get; }
        public string RoundId { get; }
        public IReadOnlyDictionary<int,int> RoundScores { get; }
        public IReadOnlyDictionary<int,int> MatchScores { get; }
        public IReadOnlyList<ScoreTransaction> Transactions { get; }
        public bool Finalized { get; }
        public long Version { get; }
        internal ScoreSnapshot(string matchId, string roundId, IDictionary<int,int> round, IDictionary<int,int> match,
            IEnumerable<ScoreTransaction> transactions, bool finalized, long version)
        { MatchId=matchId; RoundId=roundId; RoundScores=new ReadOnlyDictionary<int,int>(new Dictionary<int,int>(round));
          MatchScores=new ReadOnlyDictionary<int,int>(new Dictionary<int,int>(match)); Transactions=Array.AsReadOnly(transactions.ToArray()); Finalized=finalized; Version=version; }
    }

    public sealed class ScoreEvent
    {
        public string MatchId { get; }
        public string RoundId { get; }
        public long Sequence { get; }
        public ScoreEventType Type { get; }
        public ScoreTransaction Transaction { get; }
        public ScoreSnapshot Snapshot { get; }
        internal ScoreEvent(string matchId, string roundId, long sequence, ScoreEventType type, ScoreTransaction transaction=null, ScoreSnapshot snapshot=null)
        { MatchId=matchId; RoundId=roundId; Sequence=sequence; Type=type; Transaction=transaction; Snapshot=snapshot; }
    }

    /// <summary>Authoritative score ledger. It calculates only from GW7 truth and a finalized GW6 vote collection.</summary>
    public sealed class ScoringService
    {
        private readonly ScoringConfig config;
        private readonly HashSet<string> appliedIds=new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> resolvedRounds=new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string,ScoreSnapshot> snapshots=new Dictionary<string,ScoreSnapshot>(StringComparer.Ordinal);
        private readonly List<ScoreTransaction> history=new List<ScoreTransaction>();
        private readonly Dictionary<int,int> roundScores=new Dictionary<int,int>();
        private long sequence;
        private ScoreSnapshot current;
        private string activeMatchId=string.Empty;
        private string activeRoundId=string.Empty;
        public event Action<ScoreEvent> EventPublished;
        public ScoreSnapshot Current => current;

        public ScoringService(ScoringConfig scoringConfig=null) { config=(scoringConfig ?? new ScoringConfig()).Copy(); config.Validate(); }

        public ScoreSnapshot Resolve(RevealResult reveal, FinalizedVoteCollection votes, IReadOnlyList<PlayerState> players,
            IDictionary<int,int> cumulativeScores)
        {
            if (reveal == null || reveal.Status != RevealStatus.Complete) throw new InvalidOperationException("Scoring requires a completed GW7 reveal.");
            if (votes == null || players == null || cumulativeScores == null) throw new ArgumentNullException("Scoring inputs are required.");
            if (!StringComparer.Ordinal.Equals(reveal.MatchId, votes.MatchId) || !StringComparer.Ordinal.Equals(reveal.RoundId, votes.RoundId))
                throw new InvalidOperationException("Reveal and finalized vote collection must identify the same match and round.");
            var roundKey=reveal.MatchId + "|" + reveal.RoundId;
            if (resolvedRounds.Contains(roundKey)) return snapshots[roundKey];
            var byId=players.ToDictionary(player => player.PlayerId);
            if (!byId.ContainsKey(reveal.ActualThiefPlayerId)) throw new InvalidOperationException("Revealed Thief is absent from the authoritative roster.");
            if (votes.ValidVotes.Select(v => v.VoterId).Distinct().Count() != votes.ValidVotes.Count || votes.ValidVotes.Any(v => !byId.ContainsKey(v.VoterId) || !byId.ContainsKey(v.TargetId)))
                throw new InvalidOperationException("Finalized votes contain duplicate or unknown player identities.");
            if (votes.ValidVotes.Any(v => v.VoteSequence != votes.VoteSequence)) throw new InvalidOperationException("Finalized votes contain a mismatched vote sequence.");

            var pending=new List<ScoreTransaction>();
            foreach (var vote in votes.ValidVotes.OrderBy(v => v.VoterId))
            {
                var correct=reveal.Outcome == RevealOutcome.ThiefCaught && vote.TargetId == reveal.ActualThiefPlayerId;
                var amount=reveal.Outcome != RevealOutcome.ThiefCaught ? 0 :
                    correct ? (config.AwardRoleValueToCorrectVoter ? CorrectReward(byId[vote.VoterId].Role) : 0) : config.IncorrectVoteReward;
                var reason=correct ? ScoreReason.CorrectThiefVote : (amount == 0 ? ScoreReason.NoScore : ScoreReason.IncorrectThiefVote);
                pending.Add(Create(reveal, vote.VoterId, amount, reason, "vote-" + votes.VoteSequence + "-" + vote.VoterId));
            }
            if (reveal.Outcome == RevealOutcome.ThiefEscaped)
            {
                var reward=config.EscapeRewardPolicy == EscapeRewardPolicy.FixedValue ? config.EscapeFixedReward : config.RoleValues.Where(pair => pair.Key != RoleType.Thief).Sum(pair => pair.Value);
                pending.Add(Create(reveal, reveal.ActualThiefPlayerId, reward, ScoreReason.ThiefEscapeReward, "escape"));
            }
            else if (reveal.Outcome == RevealOutcome.ThiefCaught)
                pending.Add(Create(reveal, reveal.ActualThiefPlayerId, config.CaughtThiefReward, config.CaughtThiefReward == 0 ? ScoreReason.NoScore : ScoreReason.CaughtThiefReward, "caught-thief"));

            var nextRound=players.ToDictionary(p => p.PlayerId, _ => 0);
            var nextMatch=players.ToDictionary(p => p.PlayerId, p => cumulativeScores.TryGetValue(p.PlayerId, out var value) ? value : 0);
            activeMatchId=reveal.MatchId; activeRoundId=reveal.RoundId;
            foreach (var transaction in pending)
            {
                Validate(transaction, reveal, byId);
                if (appliedIds.Contains(transaction.TransactionId)) continue;
                nextRound[transaction.PlayerId]=checked(nextRound[transaction.PlayerId] + transaction.Amount);
                nextMatch[transaction.PlayerId]=checked(nextMatch[transaction.PlayerId] + transaction.Amount);
            }
            // Commit all totals and ledger records only after every transaction validates and arithmetic succeeds.
            foreach (var transaction in pending)
            {
                if (!appliedIds.Add(transaction.TransactionId)) continue;
                history.Add(transaction);
            }
            roundScores.Clear(); foreach (var pair in nextRound) roundScores[pair.Key]=pair.Value;
            foreach (var player in players) { player.RoundScore=nextRound[player.PlayerId]; player.MatchScore=nextMatch[player.PlayerId]; }
            var roundHistory=history.Where(t => t.MatchId == reveal.MatchId && t.RoundId == reveal.RoundId).ToArray();
            current=new ScoreSnapshot(reveal.MatchId, reveal.RoundId, roundScores, nextMatch, roundHistory, true, ++sequence);
            snapshots[roundKey]=current; resolvedRounds.Add(roundKey);
            Publish(ScoreEventType.ScoringStarted);
            foreach (var transaction in pending.Where(item => appliedIds.Contains(item.TransactionId)))
                Publish(ScoreEventType.ScoreTransactionCreated, transaction);
            Publish(ScoreEventType.RoundScoreFinalized, snapshot:current);
            Publish(ScoreEventType.CumulativeScoreUpdated, snapshot:current);
            Publish(ScoreEventType.ScoringResolved, snapshot:current);
            return current;
        }

        public ScoreSnapshot Capture(string matchId, string roundId, IReadOnlyList<PlayerState> players)
        {
            if (current != null && current.MatchId == matchId && current.RoundId == roundId) return current;
            var round=players.ToDictionary(p => p.PlayerId, p => p.RoundScore);
            var match=players.ToDictionary(p => p.PlayerId, p => p.MatchScore);
            return new ScoreSnapshot(matchId, roundId, round, match, history.Where(t => t.MatchId == matchId && t.RoundId == roundId), false, sequence);
        }

        private int CorrectReward(RoleType role) => config.CorrectVoteRewardPolicy == CorrectVoteRewardPolicy.FixedValue ? config.CorrectVoteFixedReward : config.RoleValues[role];
        private static ScoreTransaction Create(RevealResult reveal, int playerId, int amount, ScoreReason reason, string suffix) =>
            new ScoreTransaction(reveal.MatchId + "/" + reveal.RoundId + "/score/" + suffix, reveal.MatchId, reveal.RoundId, playerId, amount, reason);
        private static void Validate(ScoreTransaction transaction, RevealResult reveal, IDictionary<int,PlayerState> players)
        {
            if (string.IsNullOrWhiteSpace(transaction.MatchId) || string.IsNullOrWhiteSpace(transaction.RoundId) || transaction.Amount < 0 ||
                !Enum.IsDefined(typeof(ScoreReason), transaction.Reason) || !players.ContainsKey(transaction.PlayerId) ||
                transaction.MatchId != reveal.MatchId || transaction.RoundId != reveal.RoundId || string.IsNullOrWhiteSpace(transaction.TransactionId))
                throw new InvalidOperationException("Score transaction failed authoritative validation.");
        }
        private void Publish(ScoreEventType type, ScoreTransaction transaction=null, ScoreSnapshot snapshot=null) =>
            EventPublished?.Invoke(new ScoreEvent(snapshot?.MatchId ?? transaction?.MatchId ?? activeMatchId,
                snapshot?.RoundId ?? transaction?.RoundId ?? activeRoundId, ++sequence, type, transaction, snapshot));
    }
}
