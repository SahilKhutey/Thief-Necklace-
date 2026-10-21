using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ThiefAndTheNecklace.PB1
{
    public enum VoteStatus { NotStarted, Open, Closing, Locked, Resolving, Complete }
    public enum VoteRejectionReason { None, WrongPhase, WrongVoteSequence, NotEligibleVoter, DisconnectedVoter, SelfVoteForbidden, InvalidTarget, AlreadySubmitted, VoteExpired, AlreadyClosed }
    public enum VoteEventType { VotingStarted, VoteSubmitted, VoteLocked, VotingProgressChanged, VotingClosing, VotingTimeout, VotingClosed, VoteCollectionFinalized, VoteRejected }

    [Serializable]
    public sealed class VoteRecord
    {
        public string VoteId { get; private set; }
        public int VoterId { get; private set; }
        public int TargetId { get; private set; }
        public int VoteSequence { get; private set; }
        public long SubmittedAt { get; private set; }
        internal VoteRecord(string id, int voterId, int targetId, int sequence, long submittedAt)
        { VoteId=id; VoterId=voterId; TargetId=targetId; VoteSequence=sequence; SubmittedAt=submittedAt; }
        internal VoteRecord Copy() => new VoteRecord(VoteId, VoterId, TargetId, VoteSequence, SubmittedAt);
    }

    public sealed class FinalizedVoteCollection
    {
        public int VoteSequence { get; }
        public string MatchId { get; }
        public string RoundId { get; }
        public IReadOnlyList<VoteRecord> ValidVotes { get; }
        public IReadOnlyList<int> EligibleVoterIds { get; }
        public int ExpectedVoterCount { get; }
        public int SubmittedVoteCount { get; }
        public bool TimedOut { get; }
        internal FinalizedVoteCollection(string matchId, string roundId, int sequence, IEnumerable<VoteRecord> records, IEnumerable<int> eligibleVoters, int expected, bool timedOut)
        {
            MatchId=matchId ?? string.Empty; RoundId=roundId ?? string.Empty; VoteSequence=sequence;
            ValidVotes=new ReadOnlyCollection<VoteRecord>((records ?? Enumerable.Empty<VoteRecord>()).Select(item => item.Copy()).ToList());
            EligibleVoterIds=new ReadOnlyCollection<int>((eligibleVoters ?? Enumerable.Empty<int>()).Distinct().OrderBy(id => id).ToList());
            ExpectedVoterCount=expected; SubmittedVoteCount=ValidVotes.Count; TimedOut=timedOut;
        }
    }

    public sealed class VoteSubmissionResult
    {
        public bool Accepted { get; }
        public VoteRejectionReason Reason { get; }
        public VoteRecord Record { get; }
        internal VoteSubmissionResult(bool accepted, VoteRejectionReason reason, VoteRecord record = null)
        { Accepted=accepted; Reason=reason; Record=record == null ? null : record.Copy(); }
    }

    /// <summary>Authoritative, turnless vote collection. It never resolves the Thief or exposes targets in public progress.</summary>
    public sealed class VotingService
    {
        private readonly Func<long> clock;
        private readonly HashSet<int> voters = new HashSet<int>();
        private readonly Dictionary<int, VoteRecord> votes = new Dictionary<int, VoteRecord>();
        private readonly List<FinalizedVoteCollection> history = new List<FinalizedVoteCollection>();
        private FinalizedVoteCollection finalized;
        public int VoteSequence { get; private set; }
        public string MatchId { get; private set; } = string.Empty;
        public string RoundId { get; private set; } = string.Empty;
        public long StartTimestamp { get; private set; }
        public long EndTimestamp { get; private set; }
        public VoteStatus Status { get; private set; } = VoteStatus.NotStarted;
        public bool TimedOut { get; private set; }
        public event Action<VoteEventType, int, VoteRejectionReason> EventPublished;

        public VotingService(Func<long> timestampProvider = null) => clock=timestampProvider ?? (() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        public void Start(IEnumerable<int> eligibleVoters, int voteSequence, float durationSeconds, string matchId = "", string roundId = "")
        {
            if (voteSequence < 1) throw new ArgumentOutOfRangeException(nameof(voteSequence));
            if (durationSeconds <= 0 || float.IsNaN(durationSeconds) || float.IsInfinity(durationSeconds)) throw new ArgumentOutOfRangeException(nameof(durationSeconds));
            var active=(eligibleVoters ?? Enumerable.Empty<int>()).Distinct().ToArray();
            if (active.Length == 0 || active.Any(id => id <= 0)) throw new ArgumentException("Voting requires at least one valid eligible voter.", nameof(eligibleVoters));
            if (Status == VoteStatus.Open || Status == VoteStatus.Closing) throw new InvalidOperationException("A voting instance is already open.");
            if (finalized != null && !history.Contains(finalized)) history.Add(finalized);
            voters.Clear(); foreach (var id in active) voters.Add(id);
            votes.Clear(); finalized=null; MatchId=matchId ?? string.Empty; RoundId=roundId ?? string.Empty; VoteSequence=voteSequence; TimedOut=false; StartTimestamp=clock(); EndTimestamp=StartTimestamp + (long)(durationSeconds * 1000f); Status=VoteStatus.Open;
        }

        public void AnnounceStarted() { if (Status != VoteStatus.Open) throw new InvalidOperationException("Voting is not open."); EventPublished?.Invoke(VoteEventType.VotingStarted, 0, VoteRejectionReason.None); }

        public VoteSubmissionResult Submit(int voterId, int targetId, int voteSequence, bool voterConnected, IEnumerable<int> currentlyEligibleTargets)
        {
            Poll();
            VoteRejectionReason reason=VoteRejectionReason.None;
            if (Status != VoteStatus.Open) reason=Status == VoteStatus.NotStarted ? VoteRejectionReason.WrongPhase : TimedOut || clock() >= EndTimestamp ? VoteRejectionReason.VoteExpired : VoteRejectionReason.AlreadyClosed;
            else if (voteSequence != VoteSequence) reason=VoteRejectionReason.WrongVoteSequence;
            else if (!voters.Contains(voterId)) reason=VoteRejectionReason.NotEligibleVoter;
            else if (!voterConnected) reason=VoteRejectionReason.DisconnectedVoter;
            else if (votes.ContainsKey(voterId)) reason=VoteRejectionReason.AlreadySubmitted;
            else if (voterId == targetId) reason=VoteRejectionReason.SelfVoteForbidden;
            else if (!(currentlyEligibleTargets ?? Enumerable.Empty<int>()).Contains(targetId)) reason=VoteRejectionReason.InvalidTarget;
            if (reason != VoteRejectionReason.None)
            {
                EventPublished?.Invoke(VoteEventType.VoteRejected, voterId, reason);
                return new VoteSubmissionResult(false, reason);
            }
            var record=new VoteRecord(Guid.NewGuid().ToString("N"), voterId, targetId, VoteSequence, clock());
            votes.Add(voterId, record);
            EventPublished?.Invoke(VoteEventType.VoteSubmitted, voterId, VoteRejectionReason.None);
            EventPublished?.Invoke(VoteEventType.VoteLocked, voterId, VoteRejectionReason.None);
            EventPublished?.Invoke(VoteEventType.VotingProgressChanged, voterId, VoteRejectionReason.None);
            if (votes.Count == voters.Count) Close(false);
            return new VoteSubmissionResult(true, VoteRejectionReason.None, record);
        }

        public VoteSubmissionResult Reject(int voterId, VoteRejectionReason reason)
        {
            EventPublished?.Invoke(VoteEventType.VoteRejected, voterId, reason);
            return new VoteSubmissionResult(false, reason);
        }

        public void Poll()
        {
            if (Status == VoteStatus.Open && clock() >= EndTimestamp) Close(true);
        }

        public FinalizedVoteCollection Close(bool timeout)
        {
            if (finalized != null) return finalized;
            if (Status != VoteStatus.Open) throw new InvalidOperationException("Voting is not open.");
            Status=VoteStatus.Closing; EventPublished?.Invoke(VoteEventType.VotingClosing, 0, VoteRejectionReason.None);
            TimedOut=timeout; if (timeout) EventPublished?.Invoke(VoteEventType.VotingTimeout, 0, VoteRejectionReason.None);
            Status=VoteStatus.Locked; EventPublished?.Invoke(VoteEventType.VotingClosed, 0, VoteRejectionReason.None);
            finalized=new FinalizedVoteCollection(MatchId, RoundId, VoteSequence, votes.Values.OrderBy(item => item.SubmittedAt).ThenBy(item => item.VoterId), voters, voters.Count, TimedOut);
            Status=VoteStatus.Complete;
            EventPublished?.Invoke(VoteEventType.VoteCollectionFinalized, 0, VoteRejectionReason.None);
            return finalized;
        }

        public FinalizedVoteCollection GetFinalizedVotes() => finalized;
        public IReadOnlyList<FinalizedVoteCollection> GetFinalizedVoteHistory() => new ReadOnlyCollection<FinalizedVoteCollection>(history.ToList());
        public IReadOnlyList<VoteRecord> GetAuthorityRecords() => new ReadOnlyCollection<VoteRecord>(votes.Values.Select(item => item.Copy()).OrderBy(item => item.SubmittedAt).ToList());
        public VoteProgressViewData GetProgress() { Poll(); return new VoteProgressViewData(votes.Count, voters.Count); }
        public bool HasSubmitted(int voterId) { Poll(); return votes.ContainsKey(voterId); }
        public int RemainingSeconds { get { Poll(); return (int)Math.Max(0, Math.Ceiling((EndTimestamp - clock()) / 1000.0)); } }
    }
}
