using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ThiefAndTheNecklace.PB1;

namespace ThiefAndTheNecklace.Networking
{
    public enum EventVisibility { Public, PrivateRecipient, AuthorityOnly }
    public enum NetworkErrorType { Transport, Authentication, Room, Authority, Timeout, Serialization, ProtocolMismatch, SnapshotMismatch, Unknown }
    public enum NetworkConnectionState { Disconnected, Connecting, Connected, JoiningRoom, InRoom, Synchronizing, Ready, Reconnecting, InMatch, Failed }
    public enum CommandRejectReason { None, NotAuthenticated, NotInMatch, WrongMatch, WrongRound, WrongPhase, NotEligible, AlreadySubmitted, DuplicateCommand, InvalidTarget, SelfVote, NotAuthorized, MatchCompleted, RoundCompleted, StaleSequence, ProtocolMismatch, InvalidState }

    public sealed class NetworkActorIdentity
    {
        public int Value { get; }
        public NetworkActorIdentity(int value) { if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value)); Value=value; }
    }

    public sealed class NetworkConnectionIdentity
    {
        public string Value { get; }
        public NetworkConnectionIdentity(string value) { if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Connection identity is required.", nameof(value)); Value=value; }
    }

    public sealed class ProtocolHeader
    {
        public int ProtocolVersion { get; }
        public int MessageVersion { get; }
        public ProtocolHeader(int protocolVersion, int messageVersion)
        {
            if (protocolVersion < 1 || messageVersion < 1) throw new ArgumentOutOfRangeException(nameof(protocolVersion));
            ProtocolVersion=protocolVersion; MessageVersion=messageVersion;
        }
    }

    public sealed class NetworkEnvelope
    {
        private readonly byte[] payload;
        public string MessageId { get; }
        public string MatchId { get; }
        public string RoundId { get; }
        public long Sequence { get; }
        public string MessageType { get; }
        public EventVisibility Visibility { get; }
        public string RecipientPlayerId { get; }
        public ProtocolHeader Header { get; }
        public byte[] Payload => (byte[])payload.Clone();

        public NetworkEnvelope(string messageId, string matchId, string roundId, long sequence, string messageType,
            EventVisibility visibility, string recipientPlayerId, ProtocolHeader header, byte[] payload)
        {
            if (string.IsNullOrWhiteSpace(messageId) || string.IsNullOrWhiteSpace(messageType)) throw new ArgumentException("A message identity and type are required.");
            if (sequence < 0 || header == null || !Enum.IsDefined(typeof(EventVisibility), visibility)) throw new ArgumentOutOfRangeException(nameof(sequence));
            if (visibility == EventVisibility.PrivateRecipient && string.IsNullOrWhiteSpace(recipientPlayerId)) throw new ArgumentException("Private events require a recipient.", nameof(recipientPlayerId));
            MessageId=messageId; MatchId=matchId ?? string.Empty; RoundId=roundId ?? string.Empty; Sequence=sequence; MessageType=messageType;
            Visibility=visibility; RecipientPlayerId=recipientPlayerId ?? string.Empty; Header=header; this.payload=payload == null ? new byte[0] : (byte[])payload.Clone();
        }

        public bool CanDeliverTo(string playerId)
        {
            if (Visibility == EventVisibility.AuthorityOnly) return false;
            if (Visibility == EventVisibility.PrivateRecipient) return string.Equals(RecipientPlayerId, playerId, StringComparison.Ordinal);
            return true;
        }
    }

    public interface IGameCommand
    {
        string CommandId { get; }
        string MatchId { get; }
        string RoundId { get; }
        string PlayerId { get; }
        string CommandType { get; }
        string Fingerprint { get; }
    }

    public sealed class GameCommand : IGameCommand
    {
        public string CommandId { get; }
        public string MatchId { get; }
        public string RoundId { get; }
        public string PlayerId { get; }
        public string CommandType { get; }
        public string Fingerprint { get; }
        public GameCommand(string commandId, string matchId, string roundId, string playerId, string commandType, string fingerprint)
        {
            CommandId=commandId; MatchId=matchId; RoundId=roundId; PlayerId=playerId; CommandType=commandType; Fingerprint=fingerprint;
        }
    }

    public sealed class NetworkCommandResult
    {
        public string CommandId { get; }
        public bool Accepted { get; }
        public CommandRejectReason RejectReason { get; }
        public long ResultingSequence { get; }
        public bool IsReplay { get; }
        public NetworkCommandResult(string commandId, bool accepted, CommandRejectReason reason, long sequence, bool replay=false)
        { CommandId=commandId ?? string.Empty; Accepted=accepted; RejectReason=reason; ResultingSequence=sequence; IsReplay=replay; }
        internal NetworkCommandResult AsReplay() => new NetworkCommandResult(CommandId, Accepted, RejectReason, ResultingSequence, true);
    }

    public interface IGameAuthority
    {
        bool IsAuthority { get; }
        string CurrentMatchId { get; }
        string CurrentRoundId { get; }
        bool IsParticipant(string playerId);
        NetworkCommandResult HandleCommand(IGameCommand command);
    }

    /// <summary>Authenticated authority boundary with replay-safe command IDs; domain handlers still validate phase and rules.</summary>
    public sealed class NetworkCommandRouter
    {
        private sealed class CachedCommand { public string Fingerprint; public NetworkCommandResult Result; }
        private readonly IGameAuthority authority;
        private readonly int cacheCapacity;
        private readonly Dictionary<string,CachedCommand> cache=new Dictionary<string,CachedCommand>(StringComparer.Ordinal);
        private readonly Queue<string> insertionOrder=new Queue<string>();
        private readonly object gate=new object();
        private readonly int protocolVersion;
        public NetworkCommandRouter(IGameAuthority authority, int protocolVersion=1, int cacheCapacity=2048)
        { this.authority=authority ?? throw new ArgumentNullException(nameof(authority)); if (protocolVersion < 1 || cacheCapacity < 1) throw new ArgumentOutOfRangeException(nameof(protocolVersion)); this.protocolVersion=protocolVersion; this.cacheCapacity=cacheCapacity; }

        public NetworkCommandResult Route(string authenticatedPlayerId, IGameCommand command, int receivedProtocolVersion)
        {
            if (command == null) return Reject(string.Empty, CommandRejectReason.InvalidState);
            if (receivedProtocolVersion != protocolVersion) return Reject(command.CommandId, CommandRejectReason.ProtocolMismatch);
            if (string.IsNullOrWhiteSpace(authenticatedPlayerId) || !string.Equals(authenticatedPlayerId, command.PlayerId, StringComparison.Ordinal))
                return Reject(command.CommandId, CommandRejectReason.NotAuthenticated);
            if (string.IsNullOrWhiteSpace(command.CommandId) || string.IsNullOrWhiteSpace(command.CommandType) || string.IsNullOrWhiteSpace(command.Fingerprint))
                return Reject(command.CommandId, CommandRejectReason.InvalidState);
            if (!authority.IsAuthority) return Reject(command.CommandId, CommandRejectReason.NotAuthorized);
            if (!authority.IsParticipant(authenticatedPlayerId)) return Reject(command.CommandId, CommandRejectReason.NotInMatch);

            var key=authenticatedPlayerId + "\n" + command.CommandId;
            var fingerprint=command.CommandType + "\n" + command.MatchId + "\n" + command.RoundId + "\n" + command.Fingerprint;
            lock (gate)
            {
                if (cache.TryGetValue(key, out var prior))
                    return string.Equals(prior.Fingerprint, fingerprint, StringComparison.Ordinal)
                        ? prior.Result.AsReplay() : Reject(command.CommandId, CommandRejectReason.DuplicateCommand);
                if (!string.Equals(command.MatchId, authority.CurrentMatchId, StringComparison.Ordinal)) return Reject(command.CommandId, CommandRejectReason.WrongMatch);
                if (!string.Equals(command.RoundId, authority.CurrentRoundId, StringComparison.Ordinal)) return Reject(command.CommandId, CommandRejectReason.WrongRound);
                NetworkCommandResult result;
                try { result=authority.HandleCommand(command) ?? Reject(command.CommandId, CommandRejectReason.InvalidState); }
                catch { result=Reject(command.CommandId, CommandRejectReason.InvalidState); }
                if (!string.Equals(result.CommandId, command.CommandId, StringComparison.Ordinal)) result=Reject(command.CommandId, CommandRejectReason.InvalidState);
                cache.Add(key, new CachedCommand { Fingerprint=fingerprint, Result=result }); insertionOrder.Enqueue(key);
                while (insertionOrder.Count > cacheCapacity) cache.Remove(insertionOrder.Dequeue());
                return result;
            }
        }
        private static NetworkCommandResult Reject(string commandId, CommandRejectReason reason) => new NetworkCommandResult(commandId, false, reason, 0);
    }

    public sealed class NetworkEventSequencer
    {
        private long current;
        public long Current { get { return current; } }
        public long Next() { if (current == long.MaxValue) throw new InvalidOperationException("Network event sequence exhausted."); return ++current; }
        public void Restore(long committedSequence) { if (committedSequence < current) throw new InvalidOperationException("Cannot roll back a committed event sequence."); current=committedSequence; }
        public NetworkEnvelope Create(string matchId, string roundId, string type, EventVisibility visibility, string recipient, ProtocolHeader header, byte[] payload)
            => new NetworkEnvelope(Guid.NewGuid().ToString("N"), matchId, roundId, Next(), type, visibility, recipient, header, payload);
    }

    public interface INetworkEventRouter { void Dispatch(NetworkEnvelope message); }

    public sealed class NetworkEventRouter : INetworkEventRouter
    {
        private readonly Dictionary<string,List<Action<NetworkEnvelope>>> handlers=new Dictionary<string,List<Action<NetworkEnvelope>>>(StringComparer.Ordinal);
        private readonly string authenticatedPlayerId;
        public NetworkEventRouter(string authenticatedPlayerId)
        { this.authenticatedPlayerId=authenticatedPlayerId ?? string.Empty; }
        public void Subscribe(string messageType, Action<NetworkEnvelope> handler)
        {
            if (string.IsNullOrWhiteSpace(messageType) || handler == null) throw new ArgumentException("Message type and handler are required.");
            if (!handlers.TryGetValue(messageType, out var list)) handlers.Add(messageType, list=new List<Action<NetworkEnvelope>>());
            list.Add(handler);
        }
        public void Unsubscribe(string messageType, Action<NetworkEnvelope> handler)
        { if (handlers.TryGetValue(messageType ?? string.Empty, out var list)) { list.Remove(handler); if (list.Count == 0) handlers.Remove(messageType); } }
        public void Dispatch(NetworkEnvelope message)
        {
            if (message == null || !message.CanDeliverTo(authenticatedPlayerId)) return;
            if (!handlers.TryGetValue(message.MessageType, out var list)) return;
            foreach (var handler in list.ToArray()) handler(message);
        }
    }

    public interface INetworkBackend
    {
        event Action<NetworkEnvelope> MessageReceived;
        Task ConnectAsync();
        Task JoinRoomAsync(string roomId);
        Task LeaveRoomAsync();
        Task SendAsync(NetworkEnvelope message);
    }

    public sealed class NetworkPlayerSnapshot
    {
        public string PlayerId { get; }
        public string DisplayName { get; }
        public bool Connected { get; }
        public bool Ready { get; }
        public int Score { get; }
        public NetworkPlayerSnapshot(string playerId, string displayName, bool connected, bool ready, int score)
        {
            PlayerId=playerId; DisplayName=displayName; Connected=connected; Ready=ready; Score=score;
        }
    }

    public sealed class NetworkPhaseSnapshot
    {
        public string MatchId { get; }
        public string RoundId { get; }
        public RoundPhase Phase { get; }
        public long StartUnixMilliseconds { get; }
        public long EndUnixMilliseconds { get; }
        public long Sequence { get; }
        public NetworkPhaseSnapshot(string matchId, string roundId, RoundPhase phase, long startUnixMilliseconds, long endUnixMilliseconds, long sequence)
        {
            if (string.IsNullOrWhiteSpace(matchId) || string.IsNullOrWhiteSpace(roundId)) throw new ArgumentException("Phase identity is required.");
            if (!Enum.IsDefined(typeof(RoundPhase), phase) || startUnixMilliseconds < 0 || endUnixMilliseconds < startUnixMilliseconds || sequence < 0)
                throw new ArgumentOutOfRangeException(nameof(startUnixMilliseconds));
            MatchId=matchId; RoundId=roundId; Phase=phase; StartUnixMilliseconds=startUnixMilliseconds; EndUnixMilliseconds=endUnixMilliseconds; Sequence=sequence;
        }
        public long RemainingMilliseconds(long estimatedServerNow) => Math.Max(0, EndUnixMilliseconds - estimatedServerNow);
    }

    /// <summary>Public client projection. It deliberately has no role, clue, objective, or truth fields.</summary>
    public sealed class NetworkMatchSnapshot
    {
        public string MatchId { get; }
        public string RoundId { get; }
        public int RoundIndex { get; }
        public int TotalRounds { get; }
        public RoundPhase Phase { get; }
        public MatchStatus Status { get; }
        public long Sequence { get; }
        public long PhaseStartUnixMilliseconds { get; }
        public long PhaseEndUnixMilliseconds { get; }
        public ProtocolHeader Header { get; }
        public IReadOnlyList<NetworkPlayerSnapshot> Players { get; }
        public NetworkMatchSnapshot(string matchId, string roundId, int roundIndex, int totalRounds, RoundPhase phase,
            MatchStatus status, long sequence, long phaseStartUnixMilliseconds, long phaseEndUnixMilliseconds,
            IEnumerable<NetworkPlayerSnapshot> players, ProtocolHeader header=null)
        {
            MatchId=matchId; RoundId=roundId; RoundIndex=roundIndex; TotalRounds=totalRounds; Phase=phase; Status=status; Sequence=sequence;
            PhaseStartUnixMilliseconds=phaseStartUnixMilliseconds; PhaseEndUnixMilliseconds=phaseEndUnixMilliseconds;
            Header=header ?? new ProtocolHeader(1, 1);
            Players=Array.AsReadOnly((players ?? Enumerable.Empty<NetworkPlayerSnapshot>()).ToArray());
        }
    }

    public sealed class SnapshotValidationResult
    {
        public bool IsValid { get; }
        public string Reason { get; }
        private SnapshotValidationResult(bool valid, string reason) { IsValid=valid; Reason=reason; }
        public static SnapshotValidationResult Valid() => new SnapshotValidationResult(true, string.Empty);
        public static SnapshotValidationResult Invalid(string reason) => new SnapshotValidationResult(false, reason);
    }

    public static class NetworkSnapshotValidator
    {
        public static SnapshotValidationResult Validate(NetworkMatchSnapshot snapshot)
        {
            if (snapshot == null) return SnapshotValidationResult.Invalid("MissingSnapshot");
            if (string.IsNullOrWhiteSpace(snapshot.MatchId) || string.IsNullOrWhiteSpace(snapshot.RoundId)) return SnapshotValidationResult.Invalid("MissingIdentity");
            if (snapshot.TotalRounds < 1 || snapshot.RoundIndex < 1 || snapshot.RoundIndex > snapshot.TotalRounds) return SnapshotValidationResult.Invalid("InvalidRound");
            if (!Enum.IsDefined(typeof(RoundPhase), snapshot.Phase) || !Enum.IsDefined(typeof(MatchStatus), snapshot.Status)) return SnapshotValidationResult.Invalid("InvalidPhaseOrStatus");
            if (snapshot.Sequence < 0 || snapshot.PhaseStartUnixMilliseconds < 0 || snapshot.PhaseEndUnixMilliseconds < snapshot.PhaseStartUnixMilliseconds) return SnapshotValidationResult.Invalid("InvalidSequenceOrDeadline");
            if (snapshot.Players.Count < 4 || snapshot.Players.Count > 8 || snapshot.Players.Any(player => player == null || string.IsNullOrWhiteSpace(player.PlayerId) || string.IsNullOrWhiteSpace(player.DisplayName) || player.Score < 0)
                || snapshot.Players.Select(player => player.PlayerId).Distinct(StringComparer.Ordinal).Count() != snapshot.Players.Count) return SnapshotValidationResult.Invalid("InvalidPlayers");
            return SnapshotValidationResult.Valid();
        }
    }

    /// <summary>Host-owned committed projection for authority handoff; a provider adapter must persist it outside this process.</summary>
    public sealed class AuthoritativeSnapshotStore
    {
        private readonly object gate=new object();
        private NetworkMatchSnapshot committed;
        public bool Commit(NetworkMatchSnapshot snapshot, bool isAuthority)
        {
            if (!isAuthority || !NetworkSnapshotValidator.Validate(snapshot).IsValid) return false;
            lock (gate)
            {
                if (committed != null && committed.MatchId != snapshot.MatchId
                    && committed.Status != MatchStatus.Completed && committed.Status != MatchStatus.Aborted) return false;
                if (committed != null && committed.MatchId == snapshot.MatchId && snapshot.Sequence <= committed.Sequence) return false;
                committed=snapshot; return true;
            }
        }
        public NetworkMatchSnapshot Read(string matchId)
        {
            lock (gate) return committed != null && string.Equals(committed.MatchId, matchId, StringComparison.Ordinal) ? committed : null;
        }
    }

    public sealed class AuthoritativeStateCheckpoint
    {
        private readonly byte[] state;
        public string MatchId { get; }
        public string RoundId { get; }
        public long Sequence { get; }
        public IReadOnlyList<string> CommittedTransitionIds { get; }
        public byte[] ProtectedState => (byte[])state.Clone();
        public AuthoritativeStateCheckpoint(string matchId, string roundId, long sequence, byte[] protectedState, IEnumerable<string> committedTransitionIds)
        {
            if (string.IsNullOrWhiteSpace(matchId) || string.IsNullOrWhiteSpace(roundId) || sequence < 0 || protectedState == null)
                throw new ArgumentException("Checkpoint identity, sequence, and protected state are required.");
            MatchId=matchId; RoundId=roundId; Sequence=sequence; state=(byte[])protectedState.Clone();
            var ids=(committedTransitionIds ?? Enumerable.Empty<string>()).ToArray();
            if (ids.Any(string.IsNullOrWhiteSpace) || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length) throw new ArgumentException("Committed transition IDs must be unique.");
            CommittedTransitionIds=Array.AsReadOnly(ids);
        }
    }

    public interface IAuthoritativeStateStore
    {
        bool Commit(AuthoritativeStateCheckpoint checkpoint, bool isAuthority);
        AuthoritativeStateCheckpoint Read(string matchId);
    }

    /// <summary>Process-local implementation for development; production authority handoff requires a shared protected provider store.</summary>
    public sealed class InMemoryAuthoritativeStateStore : IAuthoritativeStateStore
    {
        private readonly object gate=new object();
        private readonly Dictionary<string,AuthoritativeStateCheckpoint> checkpoints=new Dictionary<string,AuthoritativeStateCheckpoint>(StringComparer.Ordinal);
        public bool Commit(AuthoritativeStateCheckpoint checkpoint, bool isAuthority)
        {
            if (!isAuthority || checkpoint == null) return false;
            lock (gate)
            {
                if (checkpoints.TryGetValue(checkpoint.MatchId, out var prior) && checkpoint.Sequence <= prior.Sequence) return false;
                checkpoints[checkpoint.MatchId]=checkpoint; return true;
            }
        }
        public AuthoritativeStateCheckpoint Read(string matchId)
        { lock (gate) return checkpoints.TryGetValue(matchId ?? string.Empty, out var checkpoint) ? checkpoint : null; }
    }

    public sealed class NetworkClientStateStore
    {
        public NetworkMatchSnapshot Snapshot { get; private set; }
        public long LastSequence { get; private set; }
        public bool ResynchronizationRequired { get; private set; }
        public event Action<NetworkEnvelope> EventApplied;
        public event Action SnapshotRequested;

        private readonly int protocolVersion;
        public NetworkClientStateStore(int protocolVersion=1) { if (protocolVersion < 1) throw new ArgumentOutOfRangeException(nameof(protocolVersion)); this.protocolVersion=protocolVersion; }

        public bool ApplySnapshot(NetworkMatchSnapshot snapshot)
        {
            var validation=NetworkSnapshotValidator.Validate(snapshot);
            if (validation.IsValid && snapshot.Header.ProtocolVersion != protocolVersion) validation=SnapshotValidationResult.Invalid("ProtocolMismatch");
            if (!validation.IsValid) { RequireSnapshot(); return false; }
            if (Snapshot != null && !string.Equals(Snapshot.MatchId, snapshot.MatchId, StringComparison.Ordinal)
                && Snapshot.Status != MatchStatus.Completed && Snapshot.Status != MatchStatus.Aborted) { RequireSnapshot(); return false; }
            if (Snapshot != null && string.Equals(Snapshot.MatchId, snapshot.MatchId, StringComparison.Ordinal) && snapshot.Sequence <= LastSequence) return false;
            Snapshot=snapshot; LastSequence=snapshot.Sequence; ResynchronizationRequired=false; return true;
        }

        public bool ApplyEvent(NetworkEnvelope message, string authenticatedPlayerId)
        {
            if (message == null || message.Header.ProtocolVersion != protocolVersion || Snapshot == null
                || message.Visibility == EventVisibility.AuthorityOnly
                || !string.Equals(message.MatchId, Snapshot.MatchId, StringComparison.Ordinal)) { RequireSnapshot(); return false; }
            if (message.Sequence <= LastSequence) return false;
            if (message.Sequence != LastSequence + 1) { RequireSnapshot(); return false; }
            if (!string.IsNullOrEmpty(message.RoundId) && !string.Equals(message.RoundId, Snapshot.RoundId, StringComparison.Ordinal)) { RequireSnapshot(); return false; }
            if (message.Visibility == EventVisibility.PrivateRecipient && !message.CanDeliverTo(authenticatedPlayerId))
            {
                if (message.Payload.Length != 0) { RequireSnapshot(); return false; }
                LastSequence=message.Sequence; return true;
            }
            LastSequence=message.Sequence; EventApplied?.Invoke(message); return true;
        }

        public void Reset() { Snapshot=null; LastSequence=0; ResynchronizationRequired=false; }

        private void RequireSnapshot()
        {
            if (ResynchronizationRequired) return;
            ResynchronizationRequired=true; SnapshotRequested?.Invoke();
        }
    }

    public sealed class GameNetworkManager
    {
        private readonly INetworkBackend backend;
        public NetworkConnectionState State { get; private set; } = NetworkConnectionState.Disconnected;
        public event Action<NetworkConnectionState> StateChanged;
        public GameNetworkManager(INetworkBackend backend) { this.backend=backend ?? throw new ArgumentNullException(nameof(backend)); }
        public async Task ConnectAsync()
        {
            if (State != NetworkConnectionState.Disconnected && State != NetworkConnectionState.Failed) throw new InvalidOperationException("Network connection is already active.");
            SetState(NetworkConnectionState.Connecting);
            try { await backend.ConnectAsync(); SetState(NetworkConnectionState.Connected); }
            catch { SetState(NetworkConnectionState.Failed); throw; }
        }
        public async Task JoinRoomAsync(string roomId)
        {
            if (State != NetworkConnectionState.Connected || string.IsNullOrWhiteSpace(roomId)) throw new InvalidOperationException("Connect before joining a valid room.");
            SetState(NetworkConnectionState.JoiningRoom);
            try { await backend.JoinRoomAsync(roomId); SetState(NetworkConnectionState.Synchronizing); }
            catch { SetState(NetworkConnectionState.Failed); throw; }
        }
        public void MarkSynchronized() { if (State != NetworkConnectionState.Synchronizing) throw new InvalidOperationException("A room snapshot is not pending."); SetState(NetworkConnectionState.Ready); }
        public void MarkMatchStarted() { if (State != NetworkConnectionState.Ready) throw new InvalidOperationException("Network client is not synchronized and ready."); SetState(NetworkConnectionState.InMatch); }
        public async Task LeaveRoomAsync()
        {
            if (State == NetworkConnectionState.Disconnected) return;
            await backend.LeaveRoomAsync(); SetState(NetworkConnectionState.Connected);
        }
        public void MarkReconnecting() { if (State == NetworkConnectionState.InMatch || State == NetworkConnectionState.Ready || State == NetworkConnectionState.Synchronizing) SetState(NetworkConnectionState.Reconnecting); }
        public async Task ReconnectedAsync(string roomId)
        {
            if (State != NetworkConnectionState.Reconnecting) throw new InvalidOperationException("Client is not reconnecting.");
            SetState(NetworkConnectionState.Connecting);
            await backend.ConnectAsync(); SetState(NetworkConnectionState.Connected); await JoinRoomAsync(roomId);
        }
        public void Disconnect() => SetState(NetworkConnectionState.Disconnected);
        private void SetState(NetworkConnectionState state) { State=state; StateChanged?.Invoke(state); }
    }
}
