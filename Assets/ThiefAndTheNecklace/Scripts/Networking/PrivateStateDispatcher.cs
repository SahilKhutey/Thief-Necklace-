using System;
using System.Collections.Generic;
using System.Linq;

namespace ThiefAndTheNecklace.Networking
{
    public sealed class PrivateStateDispatcher
    {
        private readonly Dictionary<string, PrivateRoundState> _byOwner = new Dictionary<string, PrivateRoundState>();
        private bool _revealed;

        public void ReplaceRound(IEnumerable<PrivateRoundState> states)
        {
            if (states == null) throw new ArgumentNullException(nameof(states));
            _byOwner.Clear();
            _revealed = false;
            foreach (var state in states)
            {
                if (state == null || string.IsNullOrWhiteSpace(state.OwnerPlayerId)) throw new ArgumentException("Every private state needs an owner.", nameof(states));
                if (_byOwner.ContainsKey(state.OwnerPlayerId)) throw new ArgumentException("Each player may receive only one private state.", nameof(states));
                _byOwner.Add(state.OwnerPlayerId, state);
            }
            if (_byOwner.Values.Count(s => s.IsThief) != 1) throw new ArgumentException("A round must contain exactly one Thief.", nameof(states));
        }

        /// <summary>Call only with the identity derived by the transport from the authenticated connection.</summary>
        public bool TryGetForRecipient(string authenticatedRecipientId, out PrivateRoundState state)
        {
            state = null;
            if (string.IsNullOrWhiteSpace(authenticatedRecipientId)) return false;
            return _byOwner.TryGetValue(authenticatedRecipientId, out state);
        }

        public IReadOnlyDictionary<string, string> RevealRoles()
        {
            _revealed = true;
            return _byOwner.ToDictionary(pair => pair.Key, pair => pair.Value.RoleId);
        }

        public void Clear()
        {
            _byOwner.Clear();
            _revealed = false;
        }

        public bool IsRevealed => _revealed;
    }
}
