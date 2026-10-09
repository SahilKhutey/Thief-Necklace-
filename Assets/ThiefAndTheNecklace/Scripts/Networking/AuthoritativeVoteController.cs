using System;
using System.Collections.Generic;
using ThiefAndTheNecklace;

namespace ThiefAndTheNecklace.Networking
{
    public sealed class AuthoritativeVoteController
    {
        private readonly VoteManager _voteManager = new VoteManager();
        private readonly HashSet<string> _seenRequests = new HashSet<string>();
        private readonly HashSet<string> _eligibleVoters = new HashSet<string>();
        private readonly HashSet<string> _validTargets = new HashSet<string>();
        private bool _open;

        public int SubmittedCount => _voteManager.Count;
        public bool IsOpen => _open;

        public void Open(IEnumerable<string> eligibleVoters, IEnumerable<string> validTargets)
        {
            if (eligibleVoters == null) throw new ArgumentNullException(nameof(eligibleVoters));
            if (validTargets == null) throw new ArgumentNullException(nameof(validTargets));
            _voteManager.Clear();
            _seenRequests.Clear();
            _eligibleVoters.Clear();
            _validTargets.Clear();
            foreach (var id in eligibleVoters) _eligibleVoters.Add(id);
            foreach (var id in validTargets) _validTargets.Add(id);
            _open = true;
        }

        public VoteAcknowledgement Submit(string authenticatedVoterId, VoteRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.RequestId)) return new VoteAcknowledgement(request == null ? null : request.RequestId, false, NetworkGameError.InvalidVote, SubmittedCount);
            if (!_open) return new VoteAcknowledgement(request.RequestId, false, NetworkGameError.VoteClosed, SubmittedCount);
            if (!_eligibleVoters.Contains(authenticatedVoterId)) return new VoteAcknowledgement(request.RequestId, false, NetworkGameError.NotConnected, SubmittedCount);
            var requestKey = authenticatedVoterId + ":" + request.RequestId;
            if (_seenRequests.Contains(requestKey)) return new VoteAcknowledgement(request.RequestId, false, NetworkGameError.InvalidVote, SubmittedCount);
            _seenRequests.Add(requestKey);
            var accepted = _voteManager.SubmitVote(authenticatedVoterId, request.TargetPlayerId, _eligibleVoters, _validTargets);
            return new VoteAcknowledgement(request.RequestId, accepted, accepted ? NetworkGameError.None : NetworkGameError.InvalidVote, SubmittedCount);
        }

        public VoteResolution CloseAndResolve()
        {
            _open = false;
            return _voteManager.Resolve(_validTargets);
        }

        public IReadOnlyDictionary<string, string> GetVotesForAuthorityOnly() => _voteManager.Votes;
    }
}
