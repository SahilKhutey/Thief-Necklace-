using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;

namespace ThiefAndTheNecklace
{
    public sealed class VoteManager
    {
        private readonly Dictionary<string, string> _votes = new Dictionary<string, string>();
        private readonly ReadOnlyDictionary<string, string> _readOnlyVotes;
        public VoteManager() { _readOnlyVotes = new ReadOnlyDictionary<string, string>(_votes); }
        public IReadOnlyDictionary<string, string> Votes => _readOnlyVotes;
        public int Count => _votes.Count;

        public bool SubmitVote(string voterId, string targetId, IReadOnlyCollection<string> validVoters, IReadOnlyCollection<string> validTargets)
        {
            if (string.IsNullOrEmpty(voterId) || string.IsNullOrEmpty(targetId) || validVoters == null || validTargets == null)
                return false;
            if (!validVoters.Contains(voterId) || !validTargets.Contains(targetId)) return false;
            if (string.Equals(voterId, targetId, StringComparison.Ordinal)) return false;
            if (_votes.ContainsKey(voterId)) return false;
            _votes.Add(voterId, targetId);
            return true;
        }

        public VoteResolution Resolve(IReadOnlyCollection<string> validPlayers)
        {
            return Resolve(validPlayers, validPlayers);
        }

        public VoteResolution Resolve(IReadOnlyCollection<string> validVoters, IReadOnlyCollection<string> validTargets)
        {
            if (validVoters == null) throw new ArgumentNullException(nameof(validVoters));
            if (validTargets == null) throw new ArgumentNullException(nameof(validTargets));
            if (_votes.Count != validVoters.Count || validVoters.Any(id => !_votes.ContainsKey(id)))
                throw new InvalidOperationException("Every eligible player must submit exactly one vote before resolution.");
            var counts = new Dictionary<string, int>();
            foreach (var playerId in validTargets) counts[playerId] = 0;
            foreach (var pair in _votes)
            {
                if (!counts.ContainsKey(pair.Value)) continue;
                counts[pair.Value]++;
            }

            var highest = -1;
            var leaders = new List<string>();
            foreach (var pair in counts)
            {
                if (pair.Value > highest)
                {
                    highest = pair.Value;
                    leaders.Clear();
                    leaders.Add(pair.Key);
                }
                else if (pair.Value == highest)
                {
                    leaders.Add(pair.Key);
                }
            }
            return new VoteResolution(counts, leaders);
        }

        public void Clear() => _votes.Clear();
    }
}
