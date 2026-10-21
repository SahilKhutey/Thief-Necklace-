using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ThiefAndTheNecklace.PB1
{
    public enum VoteResolutionType { NoValidVotes, UniqueTarget, Tie }
    public enum ResolutionOutcome { Pending, TargetSelected, RevoteRequired, ThiefEscaped }
    public enum RevoteTargetPolicy { AllEligiblePlayers, TiedPlayersOnly }
    public enum NoVotePolicy { Pending }
    public enum VoteResolutionError { NoCollection, InvalidSequence, DuplicateVoter, InvalidTarget, SelfVote, AlreadyResolved, WrongPhase, InvalidState }

    [Serializable]
    public sealed class VoteResolutionConfig
    {
        public int MaxTieRounds = 2;
        public bool SecondTieMeansEscape = true;
        public RevoteTargetPolicy RevotePolicy = RevoteTargetPolicy.TiedPlayersOnly;
        public NoVotePolicy NoVotePolicy = NoVotePolicy.Pending;
        public void Validate()
        {
            if (MaxTieRounds < 1 || !Enum.IsDefined(typeof(RevoteTargetPolicy), RevotePolicy) || !Enum.IsDefined(typeof(NoVotePolicy), NoVotePolicy))
                throw new ArgumentOutOfRangeException(nameof(VoteResolutionConfig), "Vote resolution policy is invalid.");
        }
    }

    [Serializable]
    public sealed class VoteResolutionContext
    {
        public int TieCount { get; private set; }
        public VoteResolutionConfig Config { get; private set; }
        public VoteResolutionContext(int tieCount, VoteResolutionConfig config)
        {
            if (tieCount < 0) throw new ArgumentOutOfRangeException(nameof(tieCount));
            TieCount=tieCount; Config=config ?? new VoteResolutionConfig(); Config.Validate();
        }
    }

    public sealed class VoteResolutionResult
    {
        public string MatchId { get; }
        public string RoundId { get; }
        public int VoteSequence { get; }
        public IReadOnlyDictionary<int, int> VoteCounts { get; }
        public IReadOnlyList<int> HighestTargets { get; }
        public VoteResolutionType Type { get; }
        public int? SelectedTargetId { get; }
        public ResolutionOutcome Outcome { get; }
        public int TieCount { get; }
        public VoteResolutionError? Error { get; }
        internal VoteResolutionResult(FinalizedVoteCollection collection, IDictionary<int, int> counts, IEnumerable<int> highest,
            VoteResolutionType type, int? selectedTargetId, ResolutionOutcome outcome, int tieCount, VoteResolutionError? error = null)
        {
            MatchId=collection == null ? string.Empty : collection.MatchId; RoundId=collection == null ? string.Empty : collection.RoundId;
            VoteSequence=collection == null ? 0 : collection.VoteSequence;
            VoteCounts=new ReadOnlyDictionary<int, int>(new Dictionary<int, int>(counts ?? new Dictionary<int, int>()));
            HighestTargets=Array.AsReadOnly((highest ?? Enumerable.Empty<int>()).Distinct().OrderBy(id => id).ToArray());
            Type=type; SelectedTargetId=selectedTargetId; Outcome=outcome; TieCount=tieCount; Error=error;
        }
    }

    public sealed class VoteResolutionException : InvalidOperationException
    {
        public VoteResolutionError Error { get; }
        public VoteResolutionException(VoteResolutionError error, string message) : base(message) { Error=error; }
    }

    /// <summary>Pure GW6 resolver: validates only the finalized GW5 ballot and never reads hidden roles.</summary>
    public sealed class FinalizedVoteResolver
    {
        public VoteResolutionResult Resolve(FinalizedVoteCollection collection, VoteResolutionContext context,
            IEnumerable<int> eligibleTargets, IEnumerable<int> eligibleVoters, int expectedSequence,
            IReadOnlyCollection<int> tiedTargets = null, string expectedMatchId = null, string expectedRoundId = null)
        {
            if (collection == null) throw new VoteResolutionException(VoteResolutionError.NoCollection, "A finalized vote collection is required.");
            if (context == null) throw new VoteResolutionException(VoteResolutionError.InvalidState, "A vote resolution context is required.");
            context.Config.Validate();
            if (collection.VoteSequence != expectedSequence) throw new VoteResolutionException(VoteResolutionError.InvalidSequence, "Finalized vote sequence does not match the active resolution request.");
            if ((!string.IsNullOrEmpty(expectedMatchId) && !string.Equals(collection.MatchId, expectedMatchId, StringComparison.Ordinal))
                || (!string.IsNullOrEmpty(expectedRoundId) && !string.Equals(collection.RoundId, expectedRoundId, StringComparison.Ordinal)))
                throw new VoteResolutionException(VoteResolutionError.InvalidState, "Finalized vote collection belongs to another match or round.");
            if (collection.ExpectedVoterCount < 0 || collection.SubmittedVoteCount != collection.ValidVotes.Count
                || collection.SubmittedVoteCount > collection.ExpectedVoterCount
                || collection.EligibleVoterIds.Count != collection.ExpectedVoterCount)
                throw new VoteResolutionException(VoteResolutionError.InvalidState, "Finalized vote collection totals are inconsistent.");

            var voters=new HashSet<int>(eligibleVoters ?? Enumerable.Empty<int>());
            if (!voters.SetEquals(collection.EligibleVoterIds)) throw new VoteResolutionException(VoteResolutionError.InvalidState, "Finalized voters do not match the active round eligibility set.");
            var targets=new HashSet<int>(eligibleTargets ?? Enumerable.Empty<int>());
            if (context.Config.RevotePolicy == RevoteTargetPolicy.TiedPlayersOnly && expectedSequence > 1)
            {
                var tied=new HashSet<int>(tiedTargets ?? Array.Empty<int>());
                targets.IntersectWith(tied);
            }

            var counts=new Dictionary<int, int>(); var seenVoters=new HashSet<int>();
            foreach (var vote in collection.ValidVotes)
            {
                if (vote == null || vote.VoteSequence != collection.VoteSequence) throw new VoteResolutionException(VoteResolutionError.InvalidSequence, "A vote record belongs to another vote sequence.");
                if (!voters.Contains(vote.VoterId)) throw new VoteResolutionException(VoteResolutionError.InvalidState, "A vote record references an ineligible voter.");
                if (!seenVoters.Add(vote.VoterId)) throw new VoteResolutionException(VoteResolutionError.DuplicateVoter, "A voter appears more than once in the finalized collection.");
                if (vote.TargetId == vote.VoterId) throw new VoteResolutionException(VoteResolutionError.SelfVote, "A finalized vote contains a self-vote.");
                if (!targets.Contains(vote.TargetId)) throw new VoteResolutionException(VoteResolutionError.InvalidTarget, "A finalized vote references an ineligible target.");
                counts[vote.TargetId]=counts.TryGetValue(vote.TargetId, out var count) ? count + 1 : 1;
            }

            if (counts.Count == 0)
                return new VoteResolutionResult(collection, counts, Array.Empty<int>(), VoteResolutionType.NoValidVotes, null, ResolutionOutcome.Pending, context.TieCount);
            var max=counts.Values.Max(); var highest=counts.Where(pair => pair.Value == max).Select(pair => pair.Key).OrderBy(id => id).ToArray();
            if (highest.Length == 1)
                return new VoteResolutionResult(collection, counts, highest, VoteResolutionType.UniqueTarget, highest[0], ResolutionOutcome.TargetSelected, context.TieCount);
            var nextTie=context.TieCount + 1;
            var outcome=nextTie < context.Config.MaxTieRounds ? ResolutionOutcome.RevoteRequired
                : context.Config.SecondTieMeansEscape ? ResolutionOutcome.ThiefEscaped : ResolutionOutcome.Pending;
            return new VoteResolutionResult(collection, counts, highest, VoteResolutionType.Tie, null, outcome, nextTie);
        }
    }
}
