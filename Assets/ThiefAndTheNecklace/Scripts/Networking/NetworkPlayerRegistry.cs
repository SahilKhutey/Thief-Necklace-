using System;
using System.Collections.Generic;
using System.Linq;

namespace ThiefAndTheNecklace.Networking
{
    public sealed class NetworkPlayerRegistry
    {
        public const int MinimumPlayers = 4;
        public const int MaximumPlayers = 6;
        private readonly Dictionary<string, PublicPlayerState> _players = new Dictionary<string, PublicPlayerState>();
        private bool _matchStarted;

        public IReadOnlyCollection<PublicPlayerState> Players => _players.Values.ToArray();
        public int ConnectedCount => _players.Values.Count(p => p.Connected);
        public bool MatchStarted => _matchStarted;

        public NetworkGameError Add(string playerId, string displayName)
        {
            if (_matchStarted) return NetworkGameError.MatchStarted;
            if (string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(displayName)) return NetworkGameError.NotConnected;
            if (string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(displayName)) return NetworkGameError.NotConnected;
            PublicPlayerState existing;
            if (_players.TryGetValue(playerId, out existing))
            {
                existing.Connected = true;
                existing.Ready = false;
                return NetworkGameError.None;
            }
            if (_players.Count >= MaximumPlayers) return NetworkGameError.SessionFull;
            _players.Add(playerId, new PublicPlayerState(playerId, displayName));
            return NetworkGameError.None;
        }

        public bool SetReady(string playerId, bool ready)
        {
            PublicPlayerState player;
            if (_matchStarted || !_players.TryGetValue(playerId, out player) || !player.Connected) return false;
            player.Ready = ready;
            return true;
        }

        public bool SetConnected(string playerId, bool connected)
        {
            PublicPlayerState player;
            if (!_players.TryGetValue(playerId, out player)) return false;
            player.Connected = connected;
            if (!connected) player.Ready = false;
            return true;
        }

        public bool RemoveFromLobby(string playerId)
        {
            if (_matchStarted) return false;
            return _players.Remove(playerId);
        }

        public bool CanStart => !_matchStarted && ConnectedCount >= MinimumPlayers && ConnectedCount <= MaximumPlayers
                                && _players.Values.Where(p => p.Connected).All(p => p.Ready);

        public bool TryLockRoster()
        {
            if (!CanStart) return false;
            _matchStarted = true;
            return true;
        }

        public bool TryGet(string playerId, out PublicPlayerState player) => _players.TryGetValue(playerId, out player);

        public void ResetMatchLock() => _matchStarted = false;
    }
}
