using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using ThiefAndTheNecklace;

namespace ThiefAndTheNecklace.Networking
{
    public enum NetworkGameError
    {
        None,
        SessionFull,
        SessionNotFound,
        MatchStarted,
        NotConnected,
        InvalidVote,
        InvalidChat,
        RateLimited,
        VoteClosed,
        InvalidPhase,
        HostDisconnected,
        ServiceUnavailable,
        InvalidConfiguration,
        InvalidIdentity,
        PlayerNotFound,
        AlreadyInRoom,
        NotRoomMember,
        NotRoomAuthority,
        NotReady,
        MatchNotReady,
        InvalidState,
        InitializationFailed,
        MatchNotCompleted,
        RosterLocked,
        InvalidRematch
    }

    public enum ConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Reconnecting,
        Unavailable
    }

    public sealed class NetworkOperationResult<T>
    {
        public bool Succeeded { get; }
        public T Value { get; }
        public NetworkGameError Error { get; }
        public string UserMessage { get; }

        private NetworkOperationResult(bool succeeded, T value, NetworkGameError error, string userMessage)
        {
            Succeeded = succeeded;
            Value = value;
            Error = error;
            UserMessage = userMessage;
        }

        public static NetworkOperationResult<T> Success(T value) => new NetworkOperationResult<T>(true, value, NetworkGameError.None, string.Empty);
        public static NetworkOperationResult<T> Failure(NetworkGameError error, string userMessage) => new NetworkOperationResult<T>(false, default(T), error, userMessage);
    }

    public interface IGameSessionService
    {
        Task<NetworkOperationResult<string>> CreateSessionAsync(int maxPlayers);
        Task<NetworkOperationResult<bool>> JoinSessionAsync(string joinCode);
        Task LeaveSessionAsync();
    }

    /// <summary>Provider adapter contract. All calls must be authenticated and validated by the room authority.</summary>
    public interface IRoomTransport
    {
        event Action<LobbySnapshot> LobbySnapshotReceived;
        event Action<MatchSessionSnapshot> MatchSnapshotReceived;
        event Action<string, bool> PlayerConnectionChanged;
        event Action<SessionLifecycleEvent> LifecycleEventReceived;
        event Action<string, SessionDiscussionEvent> DiscussionEventReceived;
        event Action<string, SessionVoteEvent> VoteEventReceived;
        event Action<string, SessionVoteResolutionEvent> VoteResolutionEventReceived;

        Task<NetworkOperationResult<LobbySnapshot>> CreateRoomAsync(SessionPlayerIdentity creator, RoomConfiguration configuration);
        Task<NetworkOperationResult<LobbySnapshot>> JoinRoomAsync(string roomId, SessionPlayerIdentity player);
        Task<NetworkOperationResult<LobbySnapshot>> SetReadyStateAsync(string authenticatedPlayerId, bool ready);
        Task<NetworkOperationResult<MatchSessionSnapshot>> StartMatchAsync(string authenticatedPlayerId);
        Task<NetworkOperationResult<MatchSessionSnapshot>> RequestRematchAsync(string authenticatedPlayerId);
        Task<NetworkOperationResult<LobbySnapshot>> ReturnToLobbyAsync(string authenticatedPlayerId);
        Task<NetworkOperationResult<bool>> LeaveRoomAsync(string authenticatedPlayerId);
        Task<NetworkOperationResult<SessionPrivatePlayerSnapshot>> GetPrivatePlayerInformationAsync(string authenticatedPlayerId);
        Task<NetworkOperationResult<MatchSessionSnapshot>> AcknowledgePrivateInformationAsync(string authenticatedPlayerId);
        Task<NetworkOperationResult<MatchSessionSnapshot>> BeginDiscussionAsync(string authenticatedPlayerId);
        Task<NetworkOperationResult<ThiefAndTheNecklace.PB1.DiscussionSnapshot>> GetDiscussionSnapshotAsync(string authenticatedPlayerId, long afterSequence = 0, int pageSize = 100);
        Task<NetworkOperationResult<ThiefAndTheNecklace.PB1.DiscussionMessage>> SendDiscussionMessageAsync(string authenticatedPlayerId, string text);
        Task<NetworkOperationResult<ThiefAndTheNecklace.PB1.PlayerQuestion>> SubmitDiscussionQuestionAsync(string authenticatedPlayerId, string targetPlayerId, string text);
        Task<NetworkOperationResult<ThiefAndTheNecklace.PB1.DiscussionResponse>> RespondToDiscussionQuestionAsync(string authenticatedPlayerId, string questionId, string text);
        Task<NetworkOperationResult<ThiefAndTheNecklace.PB1.PlayerClaim>> SubmitDiscussionClaimAsync(string authenticatedPlayerId, string factId, string statement, float gameTime, ThiefAndTheNecklace.PB1.ClaimType type);
        Task<NetworkOperationResult<ThiefAndTheNecklace.PB1.ClaimChallenge>> ChallengeDiscussionClaimAsync(string authenticatedPlayerId, string claimId, string text);
        Task<NetworkOperationResult<bool>> RequestDiscussionEarlyVoteAsync(string authenticatedPlayerId);
        Task<NetworkOperationResult<ThiefAndTheNecklace.PB1.VoteSubmissionResult>> SubmitVoteAsync(string authenticatedPlayerId, string targetPlayerId, int voteSequence);
        Task<NetworkOperationResult<SessionVotingSnapshot>> GetVotingSnapshotAsync(string authenticatedPlayerId);
        Task<NetworkOperationResult<IReadOnlyList<SessionRevealedPlayerSnapshot>>> GetRevealedPlayerInformationAsync(string authenticatedPlayerId);
    }

    public interface INetworkClock
    {
        double Now { get; }
    }

    public interface IGameTransport
    {
        Task SendAsync<T>(T message);
    }

    public sealed class PublicPlayerState
    {
        public string PlayerId { get; }
        public string DisplayName { get; }
        public bool Connected { get; internal set; }
        public bool Ready { get; internal set; }
        public int Score { get; internal set; }

        public PublicPlayerState(string playerId, string displayName)
        {
            if (string.IsNullOrWhiteSpace(playerId)) throw new ArgumentException("A stable player ID is required.", nameof(playerId));
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("A display name is required.", nameof(displayName));
            PlayerId = playerId;
            DisplayName = displayName;
            Connected = true;
        }
    }

    public sealed class PublicRoundSnapshot
    {
        public int RoundNumber { get; }
        public string ScenarioId { get; }
        public RoundPhase Phase { get; }
        public double PhaseDeadline { get; }
        public bool MatchStarted { get; }

        public PublicRoundSnapshot(int roundNumber, string scenarioId, RoundPhase phase, double phaseDeadline, bool matchStarted)
        {
            RoundNumber = roundNumber;
            ScenarioId = scenarioId;
            Phase = phase;
            PhaseDeadline = phaseDeadline;
            MatchStarted = matchStarted;
        }
    }

    public sealed class PublicScenarioState
    {
        public int RoundNumber { get; }
        public string ScenarioId { get; }
        public int ScenarioVersion { get; }
        public string Title { get; }
        public string Description { get; }
        public string TargetObjectId { get; }
        public string PublicHash { get; }
        public PublicScenarioState(int roundNumber, string scenarioId, int scenarioVersion, string title, string description, string targetObjectId, string publicHash)
        {
            RoundNumber = roundNumber; ScenarioId = scenarioId; ScenarioVersion = scenarioVersion;
            Title = title; Description = description; TargetObjectId = targetObjectId; PublicHash = publicHash;
        }
    }

    public sealed class PublicRevealState
    {
        public string AccusedPlayerId { get; }
        public string ThiefPlayerId { get; }
        public IReadOnlyDictionary<string, string> Roles { get; }
        public bool ThiefCaught { get; }
        public PublicRevealState(string accusedPlayerId, string thiefPlayerId, IReadOnlyDictionary<string, string> roles, bool thiefCaught)
        { AccusedPlayerId = accusedPlayerId; ThiefPlayerId = thiefPlayerId; Roles = roles; ThiefCaught = thiefCaught; }
    }

    public sealed class PrivateRoundState
    {
        public string OwnerPlayerId { get; }
        public string RoleId { get; }
        public bool IsThief { get; }
        public string PrivateSituation { get; }
        public IReadOnlyList<string> Clues { get; }

        public PrivateRoundState(string ownerPlayerId, string roleId, bool isThief, string privateSituation, IReadOnlyList<string> clues)
        {
            OwnerPlayerId = ownerPlayerId;
            RoleId = roleId;
            IsThief = isThief;
            PrivateSituation = privateSituation;
            var copy = clues == null ? new List<string>() : new List<string>(clues);
            Clues = new ReadOnlyCollection<string>(copy);
        }
    }

    public sealed class ChatMessage
    {
        public string MessageId { get; }
        public string SenderId { get; }
        public string Text { get; }
        public long Sequence { get; }

        public ChatMessage(string messageId, string senderId, string text, long sequence)
        {
            MessageId = messageId;
            SenderId = senderId;
            Text = text;
            Sequence = sequence;
        }
    }

    public sealed class VoteRequest
    {
        public string RequestId { get; }
        public string TargetPlayerId { get; }

        public VoteRequest(string requestId, string targetPlayerId)
        {
            RequestId = requestId;
            TargetPlayerId = targetPlayerId;
        }
    }

    public sealed class VoteAcknowledgement
    {
        public string RequestId { get; }
        public bool Accepted { get; }
        public NetworkGameError Error { get; }
        public int SubmittedCount { get; }

        public VoteAcknowledgement(string requestId, bool accepted, NetworkGameError error, int submittedCount)
        {
            RequestId = requestId;
            Accepted = accepted;
            Error = error;
            SubmittedCount = submittedCount;
        }
    }

    public sealed class ScoreUpdate
    {
        public string PlayerId { get; }
        public int Delta { get; }
        public int NewTotal { get; }
        public string Reason { get; }

        public ScoreUpdate(string playerId, int delta, int newTotal, string reason)
        {
            PlayerId = playerId;
            Delta = delta;
            NewTotal = newTotal;
            Reason = reason;
        }
    }
}
