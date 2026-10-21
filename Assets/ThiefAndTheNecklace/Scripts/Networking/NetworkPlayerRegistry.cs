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
        private readonly List<string> _seatOrder = new List<string>();
        private readonly int _minimumPlayers;
        private readonly int _maximumPlayers;
        private bool _matchStarted;

        public IReadOnlyCollection<PublicPlayerState> Players => _seatOrder.Where(_players.ContainsKey).Select(id => _players[id]).ToArray();
        public IReadOnlyList<string> OrderedPlayerIds => _seatOrder.ToArray();
        public int ConnectedCount => _players.Values.Count(p => p.Connected);
        public bool MatchStarted => _matchStarted;
        public int MinimumPlayers => _minimumPlayers;
        public int MaximumPlayers => _maximumPlayers;

        public NetworkPlayerRegistry() : this(MinimumPlayers, MaximumPlayers) { }

        public NetworkPlayerRegistry(int minimumPlayers, int maximumPlayers)
        {
            if (minimumPlayers < MinimumPlayers || maximumPlayers > MaximumPlayers || minimumPlayers > maximumPlayers)
                throw new ArgumentOutOfRangeException(nameof(minimumPlayers), "The supported player range is 4–6 until gameplay and result contracts are expanded.");
            _minimumPlayers = minimumPlayers;
            _maximumPlayers = maximumPlayers;
        }

        public NetworkGameError Add(string playerId, string displayName)
        {
            if (_matchStarted) return NetworkGameError.MatchStarted;
            if (string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(displayName)) return NetworkGameError.NotConnected;
            PublicPlayerState existing;
            if (_players.TryGetValue(playerId, out existing))
            {
                existing.Connected = true;
                existing.Ready = false;
                return NetworkGameError.None;
            }
            if (_players.Count >= _maximumPlayers) return NetworkGameError.SessionFull;
            _players.Add(playerId, new PublicPlayerState(playerId, displayName));
            _seatOrder.Add(playerId);
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
            return RemoveMember(playerId);
        }

        public bool RemoveMember(string playerId)
        {
            if (!_players.Remove(playerId)) return false;
            _seatOrder.Remove(playerId);
            return true;
        }

        public bool CanStart => !_matchStarted && CanStartWithCurrentRoster;
        public bool CanStartWithCurrentRoster => ConnectedCount >= _minimumPlayers && ConnectedCount <= _maximumPlayers
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
