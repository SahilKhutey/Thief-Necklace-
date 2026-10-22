using System;
using System.Collections.Generic;
using System.Linq;
using ThiefAndTheNecklace.PB1;

namespace ThiefAndTheNecklace.Networking
{
    public enum SessionState
    {
        Disconnected,
        Connecting,
        Connected,
        CreatingRoom,
        JoiningRoom,
        InLobby,
        StartingMatch,
        InMatch,
        MatchResults,
        ReturningToLobby,
        LeavingRoom,
        Error
    }

    public enum SessionMatchStatus { NotStarted, Preparing, Active, Completing, Completed, Aborted }
    public enum ActivePlayerLossPolicy { ContinueWithDisconnectedPlayer, AbortMatch }

    [Serializable]
    public sealed class RematchConfig
    {
        public bool KeepSamePlayers=true;
        public int MinimumPlayers=NetworkPlayerRegistry.MinimumPlayers;
        public int MaximumPlayers=NetworkPlayerRegistry.MaximumPlayers;
        internal RematchConfig Copy() => new RematchConfig { KeepSamePlayers=KeepSamePlayers, MinimumPlayers=MinimumPlayers, MaximumPlayers=MaximumPlayers };
        public void Validate()
        {
            if (MinimumPlayers < NetworkPlayerRegistry.MinimumPlayers || MaximumPlayers > NetworkPlayerRegistry.MaximumPlayers || MinimumPlayers > MaximumPlayers)
                throw new ArgumentOutOfRangeException(nameof(MinimumPlayers), "Rematch roster must fit the supported connected-player range.");
        }
    }

    public enum SessionLifecycleEventKind
    {
        RoomCreated,
        RoomJoined,
        RoomJoinRejected,
        PlayerJoinedRoom,
        PlayerLeftRoom,
        PlayerConnectionChanged,
        PlayerReadyChanged,
        PrivateInformationAcknowledged,
        HostAuthorityChanged,
        MatchStartRequested,
        MatchStartRejected,
        MatchStarted,
        RoundStarted,
        RoundResultsOpened,
        ContinueReceived,
        DiscussionStarted,
        RoundCompleted,
        MatchCompleting,
        MatchResultsPublished,
        MatchCompleted,
        MatchAborted,
        RematchRequested,
        RematchStarted,
        ReturnedToLobby,
        RoomLeft
    }

    public sealed class RoomConfiguration
    {
        public int MinimumPlayers { get; private set; }
        public int MaximumPlayers { get; private set; }
        public int TotalRounds { get; private set; }
        public DifficultyLevel Difficulty { get; private set; }
        public ActivePlayerLossPolicy PlayerLossPolicy { get; private set; }
        public bool RequirePrivateInformationAcknowledgement { get; private set; }

        public RoomConfiguration(int minimumPlayers = 4, int maximumPlayers = 6, int totalRounds = 5,
            DifficultyLevel difficulty = DifficultyLevel.Normal,
            ActivePlayerLossPolicy playerLossPolicy = ActivePlayerLossPolicy.ContinueWithDisconnectedPlayer,
            bool requirePrivateInformationAcknowledgement = true)
        {
            if (minimumPlayers < NetworkPlayerRegistry.MinimumPlayers || maximumPlayers > NetworkPlayerRegistry.MaximumPlayers || minimumPlayers > maximumPlayers)
                throw new ArgumentOutOfRangeException(nameof(minimumPlayers), "The current runtime supports a configured range within 4–6 players.");
            if (totalRounds < 1) throw new ArgumentOutOfRangeException(nameof(totalRounds));
            if (!Enum.IsDefined(typeof(DifficultyLevel), difficulty)) throw new ArgumentOutOfRangeException(nameof(difficulty));
            if (!Enum.IsDefined(typeof(ActivePlayerLossPolicy), playerLossPolicy)) throw new ArgumentOutOfRangeException(nameof(playerLossPolicy));
            MinimumPlayers = minimumPlayers;
            MaximumPlayers = maximumPlayers;
            TotalRounds = totalRounds;
            Difficulty = difficulty;
            PlayerLossPolicy = playerLossPolicy;
            RequirePrivateInformationAcknowledgement = requirePrivateInformationAcknowledgement;
        }
    }

    public sealed class SessionPlayerIdentity
    {
        public string PlayerId { get; private set; }
        public string DisplayName { get; private set; }
        public int ProtocolVersion { get; private set; }

        public SessionPlayerIdentity(string playerId, string displayName, int protocolVersion = 1)
        {
            PlayerId = playerId;
            DisplayName = displayName;
            ProtocolVersion = protocolVersion;
        }

        public bool IsValid => !string.IsNullOrWhiteSpace(PlayerId) && !string.IsNullOrWhiteSpace(DisplayName) && ProtocolVersion > 0;
    }

    public sealed class LobbyPlayerSnapshot
    {
        public string PlayerId { get; private set; }
        public string DisplayName { get; private set; }
        public bool IsConnected { get; private set; }
        public bool IsReady { get; private set; }
        public int SeatIndex { get; private set; }

        internal LobbyPlayerSnapshot(PublicPlayerState player, int seatIndex)
        {
            PlayerId = player.PlayerId;
            DisplayName = player.DisplayName;
            IsConnected = player.Connected;
            IsReady = player.Ready;
            SeatIndex = seatIndex;
        }
    }

    public sealed class LobbySnapshot
    {
        public long Version { get; private set; }
        public long EventSequence { get; private set; }
        public string RoomId { get; private set; }
        public string HostPlayerId { get; private set; }
        public bool RosterLocked { get; private set; }
        public int MinimumPlayers { get; private set; }
        public int MaximumPlayers { get; private set; }
        public int TotalRounds { get; private set; }
        public SessionMatchStatus MatchStatus { get; private set; }
        public bool CanStart { get; private set; }
        public IReadOnlyList<LobbyPlayerSnapshot> Players { get; private set; }

        internal LobbySnapshot(SessionRoom room)
        {
            Version = room.Version;
            EventSequence = room.EventSequence;
            RoomId = room.RoomId;
            HostPlayerId = room.HostPlayerId;
            RosterLocked = room.MatchStatus != SessionMatchStatus.NotStarted;
            MinimumPlayers = room.Configuration.MinimumPlayers;
            MaximumPlayers = room.Configuration.MaximumPlayers;
            TotalRounds = room.Configuration.TotalRounds;
            MatchStatus = room.MatchStatus;
            CanStart = room.MatchStatus == SessionMatchStatus.NotStarted && room.Players.CanStart;
            var members = room.Players.Players;
            Players = Array.AsReadOnly(members.Select((p, i) => new LobbyPlayerSnapshot(p, i)).ToArray());
        }
    }

    public sealed class MatchScoreRow
    {
        public string PlayerId { get; private set; }
        public int Score { get; private set; }
        public int Rank { get; private set; }
        public bool IsWinner { get; private set; }
        internal MatchScoreRow(string playerId, int score, int rank=0, bool isWinner=false) { PlayerId = playerId; Score = score; Rank=rank; IsWinner=isWinner; }
    }

    public sealed class MatchResultSnapshot
    {
        public string MatchId { get; private set; }
        public int CompletedRounds { get; private set; }
        public IReadOnlyList<MatchScoreRow> Players { get; private set; }
        public IReadOnlyList<string> WinnerIds { get; private set; }

        internal MatchResultSnapshot(string matchId, int completedRounds, IEnumerable<MatchScoreRow> players)
        {
            MatchId = matchId;
            CompletedRounds = completedRounds;
            var ordered = players.OrderByDescending(p => p.Score).ThenBy(p => p.PlayerId, StringComparer.Ordinal).ToArray();
            var highest=ordered.Length == 0 ? 0 : ordered[0].Score;
            var rows=new MatchScoreRow[ordered.Length]; int rank=0; int previous=int.MinValue;
            for (var index=0; index<ordered.Length; index++)
            {
                if (ordered[index].Score != previous) rank=index+1;
                rows[index]=new MatchScoreRow(ordered[index].PlayerId, ordered[index].Score, rank, ordered[index].Score == highest);
                previous=ordered[index].Score;
            }
            Players = Array.AsReadOnly(rows);
            WinnerIds = Array.AsReadOnly(rows.Where(p => p.Score == highest).Select(p => p.PlayerId).ToArray());
        }
    }

    public sealed class SessionRoundResultSnapshot
    {
        public string MatchId { get; private set; }
        public string RoundId { get; private set; }
        public int RoundIndex { get; private set; }
        public int TotalRounds { get; private set; }
        public RevealOutcome Outcome { get; private set; }
        public string SelectedTargetId { get; private set; }
        public string ThiefPlayerId { get; private set; }
        public IReadOnlyDictionary<string,int> RoundScores { get; private set; }
        public IReadOnlyDictionary<string,int> CumulativeScores { get; private set; }
        public IReadOnlyList<SessionRevealRoleRecord> RevealedRoles { get; private set; }
        public IReadOnlyList<SessionScoreTransaction> Transactions { get; private set; }
        public DateTimeOffset CompletedAt { get; private set; }
        public bool Finalized { get; private set; }
        internal SessionRoundResultSnapshot(CompletedRoundResult result, IReadOnlyList<string> playerIds)
        {
            MatchId=result.MatchId; RoundId=result.RoundId; RoundIndex=result.RoundIndex; TotalRounds=result.TotalRounds; Outcome=result.Outcome;
            SelectedTargetId=result.SelectedTargetId.HasValue ? playerIds[result.SelectedTargetId.Value-1] : null;
            ThiefPlayerId=result.ThiefPlayerId > 0 ? playerIds[result.ThiefPlayerId-1] : null;
            RoundScores=new System.Collections.ObjectModel.ReadOnlyDictionary<string,int>(result.RoundScores.ToDictionary(pair => playerIds[pair.Key-1], pair => pair.Value));
            CumulativeScores=new System.Collections.ObjectModel.ReadOnlyDictionary<string,int>(result.CumulativeScores.ToDictionary(pair => playerIds[pair.Key-1], pair => pair.Value));
            RevealedRoles=Array.AsReadOnly(result.RevealedRoles.Select(role => new SessionRevealRoleRecord(role, playerIds)).ToArray());
            Transactions=Array.AsReadOnly(result.Transactions.Select(transaction => new SessionScoreTransaction(transaction, playerIds)).ToArray());
            CompletedAt=result.CompletedAt; Finalized=result.Finalized;
        }
    }

    public sealed class SessionRoundFlowEvent
    {
        public string MatchId { get; private set; }
        public string RoundId { get; private set; }
        public long Sequence { get; private set; }
        public RoundFlowEventType Type { get; private set; }
        public string PlayerId { get; private set; }
        public SessionRoundResultSnapshot RoundResult { get; private set; }
        public MatchResultSnapshot MatchResult { get; private set; }
        internal SessionRoundFlowEvent(RoundFlowEvent item, IReadOnlyList<string> playerIds)
        {
            MatchId=item.MatchId; RoundId=item.RoundId; Sequence=item.Sequence; Type=item.Type;
            PlayerId=item.PlayerId.HasValue ? playerIds[item.PlayerId.Value-1] : null;
            RoundResult=item.RoundResult == null ? null : new SessionRoundResultSnapshot(item.RoundResult, playerIds);
            MatchResult=item.MatchResult == null ? null : new MatchResultSnapshot(item.MatchResult.MatchId,
                item.RoundResult?.TotalRounds ?? 0, item.MatchResult.Players.Select(row => new MatchScoreRow(playerIds[row.PlayerId-1], row.MatchScore)));
        }
    }

    public sealed class MatchSessionSnapshot
    {
        public long Version { get; private set; }
        public long EventSequence { get; private set; }
        public string RoomId { get; private set; }
        public string MatchId { get; private set; }
        public int RoundIndex { get; private set; }
        public int TotalRounds { get; private set; }
        public SessionMatchStatus Status { get; private set; }
        public RoundPhase Phase { get; private set; }
        public IReadOnlyList<MatchScoreRow> Scores { get; private set; }
        public MatchResultSnapshot Result { get; private set; }
        public IReadOnlyList<string> RematchRequestPlayerIds { get; private set; }
        public SessionRoundResultSnapshot RoundResult { get; private set; }
        public IReadOnlyList<SessionRoundResultSnapshot> RoundHistory { get; private set; }
        public IReadOnlyList<string> ReadyToContinuePlayerIds { get; private set; }
        public long ResultsDeadlineMilliseconds { get; private set; }
        public bool ResultsActive { get; private set; }
        public IReadOnlyList<string> PlayersAcknowledgedPrivateInformation { get; private set; }
        public bool AllConnectedPlayersAcknowledgedPrivateInformation { get; private set; }
        public bool RequiresPrivateInformationAcknowledgement { get; private set; }

        internal MatchSessionSnapshot(SessionRoom room)
        {
            Version = room.Version;
            EventSequence = room.EventSequence;
            RoomId = room.RoomId;
            MatchId = room.Match == null ? string.Empty : room.Match.MatchId;
            RoundIndex = room.Match == null ? 0 : room.Match.CurrentRound;
            TotalRounds = room.Configuration.TotalRounds;
            Status = room.MatchStatus;
            RequiresPrivateInformationAcknowledgement = room.Configuration.RequirePrivateInformationAcknowledgement;
            Phase = room.Match == null ? RoundPhase.Waiting : room.Match.Runtime.State.Match.CurrentRoundState.Phase;
            Scores = Array.AsReadOnly(CurrentScores(room).ToArray());
            Result = room.MatchStatus == SessionMatchStatus.Completed ? room.LastResult : null;
            RematchRequestPlayerIds=Array.AsReadOnly(room.RematchRequests.OrderBy(id => id, StringComparer.Ordinal).ToArray());
            var currentRoundResult=room.Match == null ? null : room.Match.Runtime.GetCurrentRoundResult();
            RoundResult=currentRoundResult == null ? null : new SessionRoundResultSnapshot(currentRoundResult, room.Match.PlayerIds.AsReadOnly());
            RoundHistory=room.Match == null ? Array.AsReadOnly(new SessionRoundResultSnapshot[0])
                : Array.AsReadOnly(room.Match.Runtime.GetRoundHistory().Select(result => new SessionRoundResultSnapshot(result, room.Match.PlayerIds.AsReadOnly())).ToArray());
            var advance=room.Match == null ? null : room.Match.Runtime.State.Match.CurrentRoundState.Phase == RoundPhase.Results
                ? room.Match.Runtime.GetCurrentRoundAdvanceState() : null;
            ReadyToContinuePlayerIds=Array.AsReadOnly(advance == null ? new string[0]
                : advance.ReadyPlayers.Select(id => room.Match.PlayerIds[id-1]).ToArray());
            ResultsDeadlineMilliseconds=advance?.DeadlineMilliseconds ?? 0;
            ResultsActive=advance?.ResultsActive ?? false;
            var playerStates = room.Match == null ? new List<PlayerState>() : room.Match.Runtime.State.Match.Players;
            PlayersAcknowledgedPrivateInformation = Array.AsReadOnly(playerStates
                .Where(p => p.PrivateInformationAcknowledged)
                .Select(p => room.Match.PlayerIds[p.PlayerId - 1]).ToArray());
            AllConnectedPlayersAcknowledgedPrivateInformation = !room.Configuration.RequirePrivateInformationAcknowledgement
                || playerStates.Where(p => p.Connected).All(p => p.PrivateInformationAcknowledged);
        }

        private static IEnumerable<MatchScoreRow> CurrentScores(SessionRoom room)
        {
            if (room.Match == null) return Enumerable.Empty<MatchScoreRow>();
            return room.Match.Runtime.State.Match.Players
                .Select(p => new MatchScoreRow(room.Match.PlayerIds[p.PlayerId - 1], p.MatchScore)).ToArray();
        }
    }

    public sealed class SessionMatchHistorySnapshot
    {
        public string MatchId { get; private set; }
        public int MatchSeed { get; private set; }
        public IReadOnlyList<SessionRoundResultSnapshot> Rounds { get; private set; }
        public MatchResultSnapshot Result { get; private set; }
        internal SessionMatchHistorySnapshot(CoreSessionMatch match, MatchResultSnapshot result)
        {
            MatchId=match.MatchId; MatchSeed=match.Seed;
            Rounds=Array.AsReadOnly(match.Runtime.GetRoundHistory().Select(round => new SessionRoundResultSnapshot(round, match.PlayerIds.AsReadOnly())).ToArray());
            Result=result;
        }
    }

    public sealed class SessionPrivatePlayerSnapshot
    {
        public string MatchId { get; private set; }
        public string PlayerId { get; private set; }
        public PrivatePlayerViewData Information { get; private set; }
        public IReadOnlyList<EvidenceDefinition> Clues { get; private set; }

        internal SessionPrivatePlayerSnapshot(string matchId, string playerId, PrivatePlayerViewData information,
            IReadOnlyList<EvidenceDefinition> clues)
        {
            MatchId = matchId;
            PlayerId = playerId;
            Information = information;
            Clues = clues;
        }
    }

    public sealed class SessionRevealedPlayerSnapshot
    {
        public string PlayerId { get; private set; }
        public RoleType Role { get; private set; }
        public bool IsThief { get; private set; }
        internal SessionRevealedPlayerSnapshot(string playerId, RoleType role, bool isThief)
        { PlayerId = playerId; Role = role; IsThief = isThief; }
    }

    public sealed class SessionRevealRoleRecord
    {
        public string PlayerId { get; private set; }
        public RoleType Role { get; private set; }
        public bool IsThief { get; private set; }
        public bool IsPublic { get; private set; }
        internal SessionRevealRoleRecord(RoleRevealRecord role, IReadOnlyList<string> playerIds)
        { PlayerId=playerIds[role.PlayerId - 1]; Role=role.Role; IsThief=role.IsThief; IsPublic=role.IsPublic; }
    }

    public sealed class SessionRevealSnapshot
    {
        public string MatchId { get; private set; }
        public string RoundId { get; private set; }
        public RevealStatus Status { get; private set; }
        public RevealOutcome Outcome { get; private set; }
        public string SelectedTargetId { get; private set; }
        public string ThiefPlayerId { get; private set; }
        public IReadOnlyList<SessionRevealRoleRecord> RevealedRoles { get; private set; }
        public string NecklaceOwnerId { get; private set; }
        public bool NecklaceRevealed { get; private set; }
        internal SessionRevealSnapshot(RevealSnapshot snapshot, IReadOnlyList<string> playerIds)
        {
            MatchId=snapshot.MatchId; RoundId=snapshot.RoundId; Status=snapshot.Status; Outcome=snapshot.Outcome;
            SelectedTargetId=snapshot.SelectedTargetId.HasValue ? playerIds[snapshot.SelectedTargetId.Value - 1] : null;
            ThiefPlayerId=snapshot.ThiefPlayerId.HasValue ? playerIds[snapshot.ThiefPlayerId.Value - 1] : null;
            RevealedRoles=Array.AsReadOnly(snapshot.RevealedRoles.Select(role => new SessionRevealRoleRecord(role, playerIds)).ToArray());
            NecklaceOwnerId=snapshot.Necklace.OwnerPlayerId.HasValue ? playerIds[snapshot.Necklace.OwnerPlayerId.Value - 1] : null;
            NecklaceRevealed=snapshot.Necklace.IsRevealed;
        }
    }

    public sealed class SessionRevealEvent
    {
        public string MatchId { get; private set; }
        public string RoundId { get; private set; }
        public long Sequence { get; private set; }
        public RevealEventType Type { get; private set; }
        public string PlayerId { get; private set; }
        public RoleType? Role { get; private set; }
        public RevealOutcome? Outcome { get; private set; }
        internal SessionRevealEvent(RevealEvent item, IReadOnlyList<string> playerIds)
        {
            MatchId=item.MatchId; RoundId=item.RoundId; Sequence=item.Sequence; Type=item.Type;
            PlayerId=item.PlayerId.HasValue ? playerIds[item.PlayerId.Value - 1] : null;
            Role=item.Role; Outcome=item.Outcome;
        }
    }

    public sealed class SessionScoreSnapshot
    {
        public string MatchId { get; private set; }
        public string RoundId { get; private set; }
        public IReadOnlyDictionary<string,int> RoundScores { get; private set; }
        public IReadOnlyDictionary<string,int> MatchScores { get; private set; }
        public IReadOnlyList<SessionScoreTransaction> Transactions { get; private set; }
        public bool Finalized { get; private set; }
        public long Version { get; private set; }
        internal SessionScoreSnapshot(ScoreSnapshot snapshot, IReadOnlyList<string> playerIds)
        {
            MatchId=snapshot.MatchId; RoundId=snapshot.RoundId;
            RoundScores=new System.Collections.ObjectModel.ReadOnlyDictionary<string,int>(snapshot.RoundScores.ToDictionary(pair => playerIds[pair.Key-1], pair => pair.Value));
            MatchScores=new System.Collections.ObjectModel.ReadOnlyDictionary<string,int>(snapshot.MatchScores.ToDictionary(pair => playerIds[pair.Key-1], pair => pair.Value));
            Transactions=Array.AsReadOnly(snapshot.Transactions.Select(item => new SessionScoreTransaction(item, playerIds)).ToArray());
            Finalized=snapshot.Finalized; Version=snapshot.Version;
        }
    }

    public sealed class SessionScoreTransaction
    {
        public string TransactionId { get; private set; }
        public string MatchId { get; private set; }
        public string RoundId { get; private set; }
        public string PlayerId { get; private set; }
        public int Amount { get; private set; }
        public ScoreReason Reason { get; private set; }
        internal SessionScoreTransaction(ScoreTransaction item, IReadOnlyList<string> playerIds)
        { TransactionId=item.TransactionId; MatchId=item.MatchId; RoundId=item.RoundId; PlayerId=playerIds[item.PlayerId-1]; Amount=item.Amount; Reason=item.Reason; }
    }

    public sealed class SessionScoreEvent
    {
        public string MatchId { get; private set; }
        public string RoundId { get; private set; }
        public long Sequence { get; private set; }
        public ScoreEventType Type { get; private set; }
        public SessionScoreTransaction Transaction { get; private set; }
        public SessionScoreSnapshot Snapshot { get; private set; }
        internal SessionScoreEvent(ScoreEvent item, IReadOnlyList<string> playerIds)
        { MatchId=item.MatchId; RoundId=item.RoundId; Sequence=item.Sequence; Type=item.Type;
          Transaction=item.Transaction == null ? null : new SessionScoreTransaction(item.Transaction, playerIds);
          Snapshot=item.Snapshot == null ? null : new SessionScoreSnapshot(item.Snapshot, playerIds); }
    }

    public sealed class SessionDiscussionEvent
    {
        public long Sequence { get; private set; }
        public DiscussionEventType Type { get; private set; }
        public string PlayerId { get; private set; }
        public string RelatedObjectId { get; private set; }
        public long Timestamp { get; private set; }
        internal SessionDiscussionEvent(DiscussionEvent item, string playerId)
        {
            Sequence=item.Sequence; Type=item.Type; PlayerId=playerId ?? string.Empty; RelatedObjectId=item.RelatedObjectId; Timestamp=item.Timestamp;
        }
    }

    public sealed class SessionVotingSnapshot
    {
        public int VoteSequence { get; private set; }
        public VoteStatus Status { get; private set; }
        public long StartTimestamp { get; private set; }
        public long EndTimestamp { get; private set; }
        public int RemainingSeconds { get; private set; }
        public int SubmittedVoters { get; private set; }
        public int EligibleVoters { get; private set; }
        public bool HasSubmitted { get; private set; }
        public IReadOnlyList<string> EligibleTargetPlayerIds { get; private set; }
        internal SessionVotingSnapshot(CoreGameRuntime runtime, int playerIndex, IReadOnlyList<string> playerIds)
        {
            VoteSequence=runtime.State.Votes.VoteRound + 1;
            Status=runtime.GetVotingStatus(); StartTimestamp=runtime.GetVotingStartTimestamp(); EndTimestamp=runtime.GetVotingEndTimestamp(); RemainingSeconds=runtime.RemainingVotingSeconds;
            var progress=runtime.GetVoteProgress(); SubmittedVoters=progress.Submitted; EligibleVoters=progress.Eligible;
            HasSubmitted=runtime.HasSubmittedVote(playerIndex);
            EligibleTargetPlayerIds=Array.AsReadOnly(runtime.GetEligibleVoteTargets(playerIndex).Select(id => playerIds[id - 1]).ToArray());
        }
    }

    public sealed class SessionVoteEvent
    {
        public VoteEventType Type { get; private set; }
        public VoteRejectionReason RejectionReason { get; private set; }
        public string PlayerId { get; private set; }
        public int VoteSequence { get; private set; }
        public int SubmittedVoters { get; private set; }
        public int EligibleVoters { get; private set; }
        internal SessionVoteEvent(VoteEventType type, VoteRejectionReason reason, string playerId, int sequence, VoteProgressViewData progress)
        { Type=type; RejectionReason=reason; PlayerId=playerId ?? string.Empty; VoteSequence=sequence; SubmittedVoters=progress.Submitted; EligibleVoters=progress.Eligible; }
    }

    public sealed class SessionVoteResolutionEvent
    {
        public string MatchId { get; private set; }
        public string RoundId { get; private set; }
        public int VoteSequence { get; private set; }
        public VoteResolutionType Type { get; private set; }
        public ResolutionOutcome Outcome { get; private set; }
        public IReadOnlyDictionary<string, int> VoteCounts { get; private set; }
        public IReadOnlyList<string> HighestTargets { get; private set; }
        public string SelectedTargetId { get; private set; }
        public int TieCount { get; private set; }
        internal SessionVoteResolutionEvent(VoteResolutionResult result, IReadOnlyList<string> playerIds)
        {
            MatchId=result.MatchId; RoundId=result.RoundId; VoteSequence=result.VoteSequence; Type=result.Type; Outcome=result.Outcome; TieCount=result.TieCount;
            VoteCounts=new ReadOnlyDictionary<string, int>(result.VoteCounts.ToDictionary(pair => playerIds[pair.Key - 1], pair => pair.Value, StringComparer.Ordinal));
            HighestTargets=Array.AsReadOnly(result.HighestTargets.Select(id => playerIds[id - 1]).ToArray());
            SelectedTargetId=result.SelectedTargetId.HasValue ? playerIds[result.SelectedTargetId.Value - 1] : null;
        }
    }

    public sealed class SessionLifecycleEvent
    {
        public long Sequence { get; private set; }
        public SessionLifecycleEventKind Kind { get; private set; }
        public string RoomId { get; private set; }
        public string MatchId { get; private set; }
        public string PlayerId { get; private set; }
        public string Reason { get; private set; }
        public LobbySnapshot Lobby { get; private set; }
        public MatchSessionSnapshot Match { get; private set; }

        internal SessionLifecycleEvent(long sequence, SessionLifecycleEventKind kind, SessionRoom room,
            string playerId, string reason)
        {
            Sequence = sequence;
            Kind = kind;
            RoomId = room == null ? string.Empty : room.RoomId;
            MatchId = room == null || room.Match == null ? string.Empty : room.Match.MatchId;
            PlayerId = playerId ?? string.Empty;
            Reason = reason ?? string.Empty;
            Lobby = room == null ? null : new LobbySnapshot(room);
            Match = room == null ? null : new MatchSessionSnapshot(room);
        }
    }

    public sealed class SessionReconnectSnapshot
    {
        public LobbySnapshot Lobby { get; private set; }
        public MatchSessionSnapshot Match { get; private set; }
        internal SessionReconnectSnapshot(LobbySnapshot lobby, MatchSessionSnapshot match) { Lobby = lobby; Match = match; }
    }

    public interface IMatchSeedSource { int NextSeed(); }
    public interface IMatchIdSource { string NextMatchId(); }
    public interface IRoomIdSource { string NextRoomId(); }

    public sealed class SystemMatchSeedSource : IMatchSeedSource
    {
        private readonly object _gate = new object();
        private readonly Random _random = new Random(Guid.NewGuid().GetHashCode());
        public int NextSeed()
        {
            var bytes = new byte[4];
            lock (_gate) _random.NextBytes(bytes);
            return BitConverter.ToInt32(bytes, 0);
        }
    }

    public sealed class GuidLifecycleIdSource : IMatchIdSource, IRoomIdSource
    {
        public string NextMatchId() => "TN-M-" + Guid.NewGuid().ToString("N");
        public string NextRoomId() => "TN-R-" + Guid.NewGuid().ToString("N");
    }

    internal sealed class SessionRoom
    {
        public readonly string RoomId;
        public readonly RoomConfiguration Configuration;
        public readonly NetworkPlayerRegistry Players;
        public string HostPlayerId;
        public SessionMatchStatus MatchStatus = SessionMatchStatus.NotStarted;
        public CoreSessionMatch Match;
        public MatchResultSnapshot LastResult;
        public readonly List<MatchResultSnapshot> Results = new List<MatchResultSnapshot>();
        public readonly List<CoreSessionMatch> CompletedMatches = new List<CoreSessionMatch>();
        public readonly HashSet<string> RematchRequests = new HashSet<string>(StringComparer.Ordinal);
        public long Version;
        public long EventSequence;

        public SessionRoom(string roomId, RoomConfiguration configuration, string hostPlayerId)
        {
            RoomId = roomId;
            Configuration = configuration;
            HostPlayerId = hostPlayerId;
            Players = new NetworkPlayerRegistry(configuration.MinimumPlayers, configuration.MaximumPlayers);
        }
    }

    internal sealed class CoreSessionMatch
    {
        private bool _listening;
        public readonly CoreGameRuntime Runtime;
        public readonly int Seed;
        public readonly string MatchId;
        public readonly List<string> PlayerIds;
        public event Action<CoreSessionMatch, int> RoundCompleted;
        public event Action<CoreSessionMatch, int> RoundStarted;
        public event Action<CoreSessionMatch> DiscussionStarted;
        public event Action<CoreSessionMatch, DiscussionEvent> DiscussionEventPublished;
        public event Action<CoreSessionMatch, VoteEventType, int, VoteRejectionReason> VoteEventPublished;
        public event Action<CoreSessionMatch, VoteResolutionResult> VoteResolutionPublished;
        public event Action<CoreSessionMatch, RevealEvent> RevealEventPublished;
        public event Action<CoreSessionMatch, ScoreEvent> ScoreEventPublished;
        public event Action<CoreSessionMatch, RoundFlowEvent> RoundFlowEventPublished;
        public event Action<CoreSessionMatch> MatchCompleted;
        public event Action<CoreSessionMatch> MatchAborted;

        public int CurrentRound => Runtime.State.Match.CurrentRound;

        public CoreSessionMatch(CoreGameRuntime runtime, int seed, string matchId, IEnumerable<string> playerIds)
        {
            Runtime = runtime;
            Seed = seed;
            MatchId = matchId;
            PlayerIds = playerIds.ToList();
            Runtime.State.Match.MatchId = matchId;
        }

        public void Start() => Runtime.StartMatch();

        public void Activate()
        {
            if (_listening) return;
            _listening = true;
            Runtime.Logged += OnLogged;
            Runtime.DiscussionEventPublished += OnDiscussionEvent;
            Runtime.VoteEventPublished += OnVoteEvent;
            Runtime.VoteResolutionPublished += OnResolution;
            Runtime.RevealEventPublished += OnRevealEvent;
            Runtime.ScoreEventPublished += OnScoreEvent;
            Runtime.RoundFlowEventPublished += OnRoundFlowEvent;
        }

        private void OnDiscussionEvent(DiscussionEvent item) => DiscussionEventPublished?.Invoke(this, item);
        private void OnVoteEvent(VoteEventType type, int playerId, VoteRejectionReason reason) => VoteEventPublished?.Invoke(this, type, playerId, reason);
        private void OnResolution(VoteResolutionResult result) => VoteResolutionPublished?.Invoke(this, result);
        private void OnRevealEvent(RevealEvent item) => RevealEventPublished?.Invoke(this, item);
        private void OnScoreEvent(ScoreEvent item) => ScoreEventPublished?.Invoke(this, item);
        private void OnRoundFlowEvent(RoundFlowEvent item) => RoundFlowEventPublished?.Invoke(this, item);

        private void OnLogged(GameSignal signal)
        {
            if (signal == GameSignal.RoundCompleted) RoundCompleted?.Invoke(this, CurrentRound);
            else if (signal == GameSignal.RoundCreated && CurrentRound > 1) RoundStarted?.Invoke(this, CurrentRound);
            else if (signal == GameSignal.DiscussionStarted) DiscussionStarted?.Invoke(this);
            else if (signal == GameSignal.MatchCompleted) MatchCompleted?.Invoke(this);
            else if (signal == GameSignal.MatchAborted) MatchAborted?.Invoke(this);
        }
    }

    /// <summary>
    /// Provider-neutral authoritative room/session lifecycle. A transport adapter must authenticate
    /// each player ID before calling mutating methods and must publish snapshots to authorized members.
    /// </summary>
    public sealed class SessionLifecycleRuntime
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, SessionRoom> _rooms = new Dictionary<string, SessionRoom>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _playerRooms = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, SessionState> _playerStates = new Dictionary<string, SessionState>(StringComparer.Ordinal);
        private readonly IScenarioGenerator _scenarioGenerator;
        private readonly DifficultyLevel _defaultDifficulty;
        private readonly IMatchSeedSource _seedSource;
        private readonly IMatchIdSource _matchIds;
        private readonly IRoomIdSource _roomIds;
        private readonly VoteResolutionConfig _voteResolutionConfig;
        private readonly RevealConfig _revealConfig;
        private readonly ResultsConfig _resultsConfig;
        private readonly RematchConfig _rematchConfig;
        private readonly int _protocolVersion;

        public event Action<SessionLifecycleEvent> LifecycleEvent;
        public event Action<string, SessionDiscussionEvent> DiscussionEventPublished;
        public event Action<string, SessionVoteEvent> VoteEventPublished;
        public event Action<string, SessionVoteResolutionEvent> VoteResolutionPublished;
        public event Action<string, SessionRevealEvent> RevealEventPublished;
        public event Action<string, SessionScoreEvent> ScoreEventPublished;
        public event Action<string, SessionRoundFlowEvent> RoundFlowEventPublished;

        public SessionLifecycleRuntime(IScenarioGenerator scenarioGenerator, DifficultyLevel defaultDifficulty = DifficultyLevel.Normal,
            IMatchSeedSource seedSource = null, IMatchIdSource matchIds = null, IRoomIdSource roomIds = null, VoteResolutionConfig voteResolutionConfig = null, RevealConfig revealConfig = null, ResultsConfig resultsConfig = null, RematchConfig rematchConfig = null, int protocolVersion = 1)
        {
            _scenarioGenerator = scenarioGenerator ?? throw new ArgumentNullException(nameof(scenarioGenerator));
            if (!Enum.IsDefined(typeof(DifficultyLevel), defaultDifficulty)) throw new ArgumentOutOfRangeException(nameof(defaultDifficulty));
            _defaultDifficulty = defaultDifficulty;
            _seedSource = seedSource ?? new SystemMatchSeedSource();
            var sharedIds = new GuidLifecycleIdSource();
            _matchIds = matchIds ?? sharedIds;
            _roomIds = roomIds ?? sharedIds;
            _voteResolutionConfig = voteResolutionConfig ?? new VoteResolutionConfig();
            _voteResolutionConfig.Validate();
            _revealConfig = revealConfig ?? new RevealConfig();
            _revealConfig.Validate();
            _resultsConfig=resultsConfig ?? new ResultsConfig(); _resultsConfig.Validate();
            _rematchConfig=(rematchConfig ?? new RematchConfig()).Copy(); _rematchConfig.Validate();
            if (protocolVersion < 1) throw new ArgumentOutOfRangeException(nameof(protocolVersion));
            _protocolVersion=protocolVersion;
        }

        public SessionState GetSessionState(string playerId)
        {
            lock (_gate)
            {
                SessionState state;
                return _playerStates.TryGetValue(playerId ?? string.Empty, out state) ? state : SessionState.Disconnected;
            }
        }

        public NetworkOperationResult<LobbySnapshot> CreateRoom(SessionPlayerIdentity creator, RoomConfiguration configuration = null)
        {
            if (creator == null || !creator.IsValid) return Failure<LobbySnapshot>(NetworkGameError.InvalidIdentity, "A valid player identity is required.");
            if (creator.ProtocolVersion != _protocolVersion) return Failure<LobbySnapshot>(NetworkGameError.ProtocolMismatch, "Client protocol is incompatible with this session runtime.");
            configuration = configuration ?? new RoomConfiguration(difficulty: _defaultDifficulty);
            var events = new List<SessionLifecycleEvent>();
            LobbySnapshot snapshot;
            lock (_gate)
            {
                if (_playerRooms.ContainsKey(creator.PlayerId)) return Failure<LobbySnapshot>(NetworkGameError.AlreadyInRoom, "Player already belongs to a room.");
                string roomId;
                try { roomId = _roomIds.NextRoomId(); }
                catch { return Failure<LobbySnapshot>(NetworkGameError.ServiceUnavailable, "Room identity could not be allocated."); }
                if (string.IsNullOrWhiteSpace(roomId) || _rooms.ContainsKey(roomId)) return Failure<LobbySnapshot>(NetworkGameError.ServiceUnavailable, "Room identity is invalid or already in use.");
                var room = new SessionRoom(roomId, configuration, creator.PlayerId);
                if (room.Players.Add(creator.PlayerId, creator.DisplayName) != NetworkGameError.None)
                    return Failure<LobbySnapshot>(NetworkGameError.InvalidIdentity, "Creator could not be added to the lobby.");
                _rooms.Add(roomId, room);
                _playerRooms.Add(creator.PlayerId, roomId);
                _playerStates[creator.PlayerId] = SessionState.InLobby;
                room.Version++;
                events.Add(Emit(room, SessionLifecycleEventKind.RoomCreated, creator.PlayerId, string.Empty));
                events.Add(Emit(room, SessionLifecycleEventKind.PlayerJoinedRoom, creator.PlayerId, string.Empty));
                snapshot = new LobbySnapshot(room);
            }
            Publish(events);
            return NetworkOperationResult<LobbySnapshot>.Success(snapshot);
        }

        public NetworkOperationResult<LobbySnapshot> JoinRoom(string roomId, SessionPlayerIdentity player)
        {
            if (player == null || !player.IsValid) return Failure<LobbySnapshot>(NetworkGameError.InvalidIdentity, "A valid player identity is required.");
            if (player.ProtocolVersion != _protocolVersion) return Failure<LobbySnapshot>(NetworkGameError.ProtocolMismatch, "Client protocol is incompatible with this room.");
            var events = new List<SessionLifecycleEvent>();
            LobbySnapshot snapshot;
            lock (_gate)
            {
                SessionRoom room;
                if (!_rooms.TryGetValue(roomId ?? string.Empty, out room)) return Failure<LobbySnapshot>(NetworkGameError.SessionNotFound, "Room was not found.");
                string currentRoom;
                if (_playerRooms.TryGetValue(player.PlayerId, out currentRoom) && currentRoom != room.RoomId)
                    return Failure<LobbySnapshot>(NetworkGameError.AlreadyInRoom, "Player already belongs to another room.");
                PublicPlayerState existing;
                var knownMember = room.Players.TryGet(player.PlayerId, out existing);
                if (knownMember && existing.Connected) return Failure<LobbySnapshot>(NetworkGameError.AlreadyInRoom, "Player is already connected to this room.");
                if (room.MatchStatus != SessionMatchStatus.NotStarted && !knownMember) return Failure<LobbySnapshot>(NetworkGameError.MatchStarted, "Room roster is locked for the active match or results.");
                if (room.MatchStatus == SessionMatchStatus.Active && room.Match != null && !room.Match.PlayerIds.Contains(player.PlayerId))
                    return Failure<LobbySnapshot>(NetworkGameError.MatchStarted, "Player was not part of the active match roster.");
                var addResult = knownMember
                    ? (room.Players.SetConnected(player.PlayerId, true) ? NetworkGameError.None : NetworkGameError.NotRoomMember)
                    : room.Players.Add(player.PlayerId, player.DisplayName);
                if (addResult != NetworkGameError.None) return Failure<LobbySnapshot>(addResult, "Room rejected the join request.");
                if (!knownMember && room.Players.TryGet(player.PlayerId, out existing))
                {
                    if (room.Players.OrderedPlayerIds.Count == 1) room.HostPlayerId = player.PlayerId;
                }
                _playerRooms[player.PlayerId] = room.RoomId;
                _playerStates[player.PlayerId] = room.MatchStatus == SessionMatchStatus.Active ? SessionState.InMatch
                    : room.MatchStatus == SessionMatchStatus.Completed ? SessionState.MatchResults : SessionState.InLobby;
                if (room.Match != null && room.MatchStatus == SessionMatchStatus.Active)
                    room.Match.Runtime.SetPlayerConnected(room.Match.PlayerIds.IndexOf(player.PlayerId) + 1, true);
                room.Version++;
                events.Add(Emit(room, knownMember ? SessionLifecycleEventKind.PlayerConnectionChanged : SessionLifecycleEventKind.PlayerJoinedRoom, player.PlayerId, string.Empty));
                events.Add(Emit(room, SessionLifecycleEventKind.RoomJoined, player.PlayerId, string.Empty));
                snapshot = new LobbySnapshot(room);
            }
            Publish(events);
            return NetworkOperationResult<LobbySnapshot>.Success(snapshot);
        }

        public NetworkOperationResult<SessionReconnectSnapshot> Reconnect(string roomId, SessionPlayerIdentity player)
        {
            var joined = JoinRoom(roomId, player);
            if (!joined.Succeeded) return Failure<SessionReconnectSnapshot>(joined.Error, joined.UserMessage);
            var match = GetMatchSnapshot(roomId, player.PlayerId);
            return NetworkOperationResult<SessionReconnectSnapshot>.Success(
                new SessionReconnectSnapshot(joined.Value, match.Succeeded ? match.Value : null));
        }

        public NetworkOperationResult<LobbySnapshot> SetReadyState(string playerId, bool ready)
        {
            var events = new List<SessionLifecycleEvent>();
            LobbySnapshot snapshot;
            lock (_gate)
            {
                SessionRoom room;
                PublicPlayerState player;
                if (!TryGetMemberRoom(playerId, out room, out player)) return Failure<LobbySnapshot>(NetworkGameError.NotRoomMember, "Player is not a room member.");
                if ((room.MatchStatus != SessionMatchStatus.NotStarted && room.MatchStatus != SessionMatchStatus.Completed) || room.Players.MatchStarted)
                    return Failure<LobbySnapshot>(NetworkGameError.RosterLocked, "Readiness is locked after match start.");
                if (!room.Players.SetReady(playerId, ready)) return Failure<LobbySnapshot>(NetworkGameError.NotRoomMember, "Player is not connected to the lobby.");
                room.Version++;
                events.Add(Emit(room, SessionLifecycleEventKind.PlayerReadyChanged, playerId, ready ? "ready" : "not-ready"));
                snapshot = new LobbySnapshot(room);
            }
            Publish(events);
            return NetworkOperationResult<LobbySnapshot>.Success(snapshot);
        }

        public NetworkOperationResult<MatchSessionSnapshot> StartMatch(string requestingPlayerId)
        {
            var events = new List<SessionLifecycleEvent>();
            MatchSessionSnapshot snapshot;
            NetworkGameError rejection = NetworkGameError.None;
            string rejectionMessage = string.Empty;
            lock (_gate)
            {
                SessionRoom room;
                PublicPlayerState requester;
                if (!TryGetMemberRoom(requestingPlayerId, out room, out requester))
                    return Failure<MatchSessionSnapshot>(NetworkGameError.NotRoomMember, "Requester is not a room member.");
                events.Add(Emit(room, SessionLifecycleEventKind.MatchStartRequested, requestingPlayerId, string.Empty));
                if (!requester.Connected) { rejection = NetworkGameError.NotConnected; rejectionMessage = "Requester is disconnected."; }
                else if (!string.Equals(room.HostPlayerId, requestingPlayerId, StringComparison.Ordinal)) { rejection = NetworkGameError.NotRoomAuthority; rejectionMessage = "Only the room authority can start the match."; }
                else if (room.MatchStatus != SessionMatchStatus.NotStarted || room.Players.MatchStarted) { rejection = NetworkGameError.InvalidState; rejectionMessage = "Match has already started or the room is locked."; }
                else if (!room.Players.CanStart) { rejection = NetworkGameError.MatchNotReady; rejectionMessage = "The connected roster must meet the player minimum and all connected players must be ready."; }

                if (rejection != NetworkGameError.None)
                {
                    events.Add(Emit(room, SessionLifecycleEventKind.MatchStartRejected, requestingPlayerId, rejection.ToString()));
                    snapshot = null;
                }
                else if (!room.Players.TryLockRoster())
                {
                    rejection = NetworkGameError.MatchNotReady;
                    rejectionMessage = "Lobby state changed before the roster could be locked.";
                    events.Add(Emit(room, SessionLifecycleEventKind.MatchStartRejected, requestingPlayerId, rejection.ToString()));
                    snapshot = null;
                }
                else
                {
                    room.MatchStatus = SessionMatchStatus.Preparing;
                    foreach (var id in room.Players.Players.Where(p => p.Connected).Select(p => p.PlayerId))
                        _playerStates[id] = SessionState.StartingMatch;
                    room.Version++;
                    var participants = room.Players.Players.Where(p => p.Connected).ToArray();
                    var playerIds = participants.Select(p => p.PlayerId).ToArray();
                    var names = participants.Select(p => p.DisplayName).ToArray();
                    try
                    {
                        var seed = _seedSource.NextSeed();
                        var matchId = _matchIds.NextMatchId();
                        if (string.IsNullOrWhiteSpace(matchId)) throw new InvalidOperationException("Match ID source returned an empty identity.");
                        while (room.CompletedMatches.Any(previous => previous.Seed == seed)) seed = unchecked(seed + 1);
                        if (room.CompletedMatches.Any(previous => string.Equals(previous.MatchId, matchId, StringComparison.Ordinal)))
                            matchId = matchId + "-" + Guid.NewGuid().ToString("N");
                        room.Match = new CoreSessionMatch(new CoreGameRuntime(names, seed, room.Configuration.TotalRounds,
                            room.Configuration.Difficulty, _scenarioGenerator,
                            room.Configuration.RequirePrivateInformationAcknowledgement, voteResolutionConfig: _voteResolutionConfig, revealConfig: _revealConfig, resultsConfig: _resultsConfig), seed, matchId, playerIds);
                        room.Match.Start();
                        room.Match.Activate();
                        room.Match.RoundCompleted += OnRoundCompleted;
                        room.Match.RoundStarted += OnRoundStarted;
                        room.Match.DiscussionStarted += OnDiscussionStarted;
                        room.Match.DiscussionEventPublished += OnDiscussionEventPublished;
                        room.Match.VoteEventPublished += OnVoteEventPublished;
                        room.Match.VoteResolutionPublished += OnVoteResolutionPublished;
                        room.Match.RevealEventPublished += OnRevealEventPublished;
                        room.Match.ScoreEventPublished += OnScoreEventPublished;
                        room.Match.RoundFlowEventPublished += OnRoundFlowEventPublished;
                        room.Match.MatchCompleted += OnMatchCompleted;
                        room.Match.MatchAborted += OnMatchAborted;
                        room.MatchStatus = SessionMatchStatus.Active;
                        foreach (var id in playerIds) _playerStates[id] = SessionState.InMatch;
                        room.Version++;
                        events.Add(Emit(room, SessionLifecycleEventKind.MatchStarted, requestingPlayerId, string.Empty));
                        events.Add(Emit(room, SessionLifecycleEventKind.RoundStarted, string.Empty, "1"));
                        snapshot = new MatchSessionSnapshot(room);
                    }
                    catch (Exception exception)
                    {
                        room.Match = null;
                        room.MatchStatus = SessionMatchStatus.NotStarted;
                        room.Players.ResetMatchLock();
                        foreach (var id in room.Players.Players.Where(p => p.Connected).Select(p => p.PlayerId))
                            _playerStates[id] = SessionState.InLobby;
                        room.Version++;
                        rejection = NetworkGameError.InitializationFailed;
                        rejectionMessage = "Match initialization failed; the lobby is available again. " + exception.GetType().Name;
                        events.Add(Emit(room, SessionLifecycleEventKind.MatchStartRejected, requestingPlayerId, rejection.ToString()));
                        snapshot = null;
                    }
                }
            }
            Publish(events);
            if (snapshot == null) return Failure<MatchSessionSnapshot>(rejection, rejectionMessage);
            return NetworkOperationResult<MatchSessionSnapshot>.Success(snapshot);
        }

        public NetworkOperationResult<MatchSessionSnapshot> RequestRematch(string requestingPlayerId)
        {
            var events = new List<SessionLifecycleEvent>();
            string roomId;
            CoreSessionMatch previousMatch;
            string authorityId;
            bool readyToStart;
            MatchSessionSnapshot pendingSnapshot;
            string previousHostId;
            lock (_gate)
            {
                SessionRoom room;
                PublicPlayerState requester;
                if (!TryGetMemberRoom(requestingPlayerId, out room, out requester)) return Failure<MatchSessionSnapshot>(NetworkGameError.NotRoomMember, "Requester is not a room member.");
                if (!requester.Connected) return Failure<MatchSessionSnapshot>(NetworkGameError.NotConnected, "Reconnect before requesting a rematch.");
                if (room.MatchStatus != SessionMatchStatus.Completed || room.LastResult == null) return Failure<MatchSessionSnapshot>(NetworkGameError.MatchNotCompleted, "A rematch is available only after match completion.");
                if (room.Match == null || !room.Match.PlayerIds.Contains(requestingPlayerId)) return Failure<MatchSessionSnapshot>(NetworkGameError.InvalidRematch, "Only a participant in the completed match can request its rematch.");
                roomId = room.RoomId;
                previousMatch = room.Match;
                previousHostId=room.HostPlayerId;
                var newlyRequested=room.RematchRequests.Add(requestingPlayerId);
                if (newlyRequested) room.Version++;
                if (newlyRequested) events.Add(Emit(room, SessionLifecycleEventKind.RematchRequested, requestingPlayerId, string.Empty));
                var eligible=previousMatch.PlayerIds.Where(id => room.Players.TryGet(id, out var player) && player.Connected).ToArray();
                var requested=eligible.Where(room.RematchRequests.Contains).ToArray();
                readyToStart=requested.Length >= _rematchConfig.MinimumPlayers && requested.Length <= _rematchConfig.MaximumPlayers
                    && (!_rematchConfig.KeepSamePlayers || requested.Length == eligible.Length);
                pendingSnapshot=new MatchSessionSnapshot(room);
                if (!readyToStart)
                {
                    pendingSnapshot=new MatchSessionSnapshot(room);
                    authorityId=string.Empty;
                }
                else
                {
                    var participants=_rematchConfig.KeepSamePlayers ? eligible : requested;
                    authorityId=participants.Contains(room.HostPlayerId) ? room.HostPlayerId : participants[0];
                    room.HostPlayerId=authorityId;
                    room.Players.ResetMatchLock();
                    foreach (var player in room.Players.Players.ToArray())
                    {
                        var active=participants.Contains(player.PlayerId);
                        room.Players.SetConnected(player.PlayerId, active);
                        if (active) room.Players.SetReady(player.PlayerId, true);
                        else _playerStates[player.PlayerId]=SessionState.Disconnected;
                    }
                    room.Match=null;
                    room.MatchStatus=SessionMatchStatus.NotStarted;
                    room.Version++;
                }
            }
            Publish(events);
            if (!readyToStart) return NetworkOperationResult<MatchSessionSnapshot>.Success(pendingSnapshot);
            var start = StartMatch(authorityId);
            if (!start.Succeeded)
            {
                lock (_gate)
                {
                    SessionRoom failedRoom;
                    if (_rooms.TryGetValue(roomId, out failedRoom) && failedRoom.MatchStatus == SessionMatchStatus.NotStarted)
                    {
                        failedRoom.Match = previousMatch;
                        failedRoom.MatchStatus = SessionMatchStatus.Completed;
                        failedRoom.HostPlayerId=previousHostId;
                        failedRoom.Players.ResetMatchLock();
                        foreach (var id in previousMatch.PlayerIds)
                        {
                            PublicPlayerState member;
                            if (failedRoom.Players.TryGet(id, out member))
                            {
                                var oldPlayer=previousMatch.Runtime.State.Match.Players.FirstOrDefault(player => previousMatch.PlayerIds[player.PlayerId-1] == id);
                                var connected=oldPlayer != null && oldPlayer.Connected;
                                failedRoom.Players.SetConnected(id, connected);
                                _playerStates[id] = connected ? SessionState.MatchResults : SessionState.Disconnected;
                            }
                        }
                        failedRoom.Version++;
                    }
                }
                return Failure<MatchSessionSnapshot>(start.Error, start.UserMessage);
            }
            lock (_gate)
            {
                SessionRoom restarted;
                if (!_rooms.TryGetValue(roomId, out restarted)) return Failure<MatchSessionSnapshot>(NetworkGameError.SessionNotFound, "Room disappeared during rematch.");
                events = new List<SessionLifecycleEvent> { Emit(restarted, SessionLifecycleEventKind.RematchStarted, requestingPlayerId, string.Empty) };
            }
            Publish(events);
            MatchSessionSnapshot rematchSnapshot;
            lock (_gate)
            {
                if (!_rooms.TryGetValue(roomId, out var completedRoom)) return Failure<MatchSessionSnapshot>(NetworkGameError.SessionNotFound, "Room disappeared after rematch start.");
                completedRoom.RematchRequests.Clear();
                rematchSnapshot=new MatchSessionSnapshot(completedRoom);
            }
            return NetworkOperationResult<MatchSessionSnapshot>.Success(rematchSnapshot);
        }

        public NetworkOperationResult<LobbySnapshot> ReturnToLobby(string requestingPlayerId)
        {
            var events = new List<SessionLifecycleEvent>();
            LobbySnapshot snapshot;
            lock (_gate)
            {
                SessionRoom room;
                PublicPlayerState requester;
                if (!TryGetMemberRoom(requestingPlayerId, out room, out requester)) return Failure<LobbySnapshot>(NetworkGameError.NotRoomMember, "Requester is not a room member.");
                if (!string.Equals(room.HostPlayerId, requestingPlayerId, StringComparison.Ordinal)) return Failure<LobbySnapshot>(NetworkGameError.NotRoomAuthority, "Only the room authority can return the room to its lobby.");
                if (room.MatchStatus != SessionMatchStatus.Completed && room.MatchStatus != SessionMatchStatus.Aborted)
                    return Failure<LobbySnapshot>(NetworkGameError.InvalidState, "The match must be complete or aborted before returning to the lobby.");
                room.Match = null;
                room.MatchStatus = SessionMatchStatus.NotStarted;
                room.Players.ResetMatchLock();
                foreach (var player in room.Players.Players)
                {
                    if (player.Connected) room.Players.SetReady(player.PlayerId, false);
                    if (player.Connected) _playerStates[player.PlayerId] = SessionState.InLobby;
                }
                room.Version++;
                events.Add(Emit(room, SessionLifecycleEventKind.ReturnedToLobby, requestingPlayerId, string.Empty));
                snapshot = new LobbySnapshot(room);
            }
            Publish(events);
            return NetworkOperationResult<LobbySnapshot>.Success(snapshot);
        }

        public NetworkOperationResult<bool> LeaveRoom(string playerId)
        {
            var events = new List<SessionLifecycleEvent>();
            lock (_gate)
            {
                SessionRoom room;
                PublicPlayerState player;
                if (!TryGetMemberRoom(playerId, out room, out player)) return Failure<bool>(NetworkGameError.NotRoomMember, "Player is not a room member.");
                if (room.MatchStatus == SessionMatchStatus.Active || room.MatchStatus == SessionMatchStatus.Preparing)
                {
                    room.Players.SetConnected(playerId, false);
                    var matchPlayerIndex = room.Match == null ? -1 : room.Match.PlayerIds.IndexOf(playerId);
                    if (matchPlayerIndex >= 0) room.Match.Runtime.SetPlayerConnected(matchPlayerIndex + 1, false);
                    _playerStates[playerId] = SessionState.Disconnected;
                    if (string.Equals(room.HostPlayerId, playerId, StringComparison.Ordinal)) TransferHost(room, events, playerId);
                    room.Version++;
                    events.Add(Emit(room, SessionLifecycleEventKind.PlayerConnectionChanged, playerId, "disconnected"));
                    if (room.Configuration.PlayerLossPolicy == ActivePlayerLossPolicy.AbortMatch)
                    {
                        room.MatchStatus = SessionMatchStatus.Aborted;
                        room.Match.Runtime.AbortMatch();
                        room.Version++;
                        events.Add(Emit(room, SessionLifecycleEventKind.MatchAborted, playerId, "player-loss-policy"));
                    }
                }
                else
                {
                    room.Players.RemoveMember(playerId);
                    _playerRooms.Remove(playerId);
                    _playerStates[playerId] = SessionState.Connected;
                    if (string.Equals(room.HostPlayerId, playerId, StringComparison.Ordinal)) TransferHost(room, events, playerId);
                    room.Version++;
                    events.Add(Emit(room, SessionLifecycleEventKind.PlayerLeftRoom, playerId, string.Empty));
                    events.Add(Emit(room, SessionLifecycleEventKind.RoomLeft, playerId, string.Empty));
                }
            }
            Publish(events);
            return NetworkOperationResult<bool>.Success(true);
        }

        /// <summary>Applies an authenticated transport connection update without treating it as an explicit room exit.</summary>
        public NetworkOperationResult<LobbySnapshot> SetPlayerConnection(string playerId, bool connected)
        {
            var events = new List<SessionLifecycleEvent>();
            LobbySnapshot snapshot;
            lock (_gate)
            {
                SessionRoom room;
                PublicPlayerState player;
                if (!TryGetMemberRoom(playerId, out room, out player)) return Failure<LobbySnapshot>(NetworkGameError.NotRoomMember, "Player is not a room member.");
                var matchRosterIndex = room.Match == null ? -1 : room.Match.PlayerIds.IndexOf(playerId);
                if (connected && room.MatchStatus != SessionMatchStatus.NotStarted && room.MatchStatus != SessionMatchStatus.Completed
                    && matchRosterIndex < 0)
                    return Failure<LobbySnapshot>(NetworkGameError.RosterLocked, "Only a frozen match participant may reconnect during a match.");
                if (!room.Players.SetConnected(playerId, connected)) return Failure<LobbySnapshot>(NetworkGameError.NotRoomMember, "Connection update was rejected.");
                var abortedByPolicy = false;
                if (room.MatchStatus == SessionMatchStatus.Active && room.Match != null)
                {
                    if (matchRosterIndex >= 0) room.Match.Runtime.SetPlayerConnected(matchRosterIndex + 1, connected);
                    _playerStates[playerId] = connected ? SessionState.InMatch : SessionState.Disconnected;
                    if (!connected && room.Configuration.PlayerLossPolicy == ActivePlayerLossPolicy.AbortMatch)
                    {
                        room.MatchStatus = SessionMatchStatus.Aborted;
                        room.Match.Runtime.AbortMatch();
                        abortedByPolicy = true;
                    }
                }
                else if (room.MatchStatus == SessionMatchStatus.Completed)
                    _playerStates[playerId] = connected ? SessionState.MatchResults : SessionState.Disconnected;
                else
                    _playerStates[playerId] = connected ? SessionState.InLobby : SessionState.Disconnected;

                if (!connected && string.Equals(room.HostPlayerId, playerId, StringComparison.Ordinal)) TransferHost(room, events, playerId);
                room.Version++;
                events.Add(Emit(room, SessionLifecycleEventKind.PlayerConnectionChanged, playerId, connected ? "connected" : "disconnected"));
                if (abortedByPolicy) events.Add(Emit(room, SessionLifecycleEventKind.MatchAborted, playerId, "player-loss-policy"));
                snapshot = new LobbySnapshot(room);
            }
            Publish(events);
            return NetworkOperationResult<LobbySnapshot>.Success(snapshot);
        }

        public NetworkOperationResult<LobbySnapshot> GetLobbySnapshot(string roomId, string authenticatedPlayerId)
        {
            lock (_gate)
            {
                SessionRoom room;
                PublicPlayerState member;
                if (!TryGetRoom(roomId, out room) || !room.Players.TryGet(authenticatedPlayerId ?? string.Empty, out member) || !member.Connected)
                    return Failure<LobbySnapshot>(NetworkGameError.NotRoomMember, "An authenticated room member is required.");
                return NetworkOperationResult<LobbySnapshot>.Success(new LobbySnapshot(room));
            }
        }

        public NetworkOperationResult<MatchSessionSnapshot> GetMatchSnapshot(string roomId, string authenticatedPlayerId)
        {
            lock (_gate)
            {
                SessionRoom room;
                PublicPlayerState member;
                if (!TryGetRoom(roomId, out room) || !room.Players.TryGet(authenticatedPlayerId ?? string.Empty, out member))
                    return Failure<MatchSessionSnapshot>(NetworkGameError.NotRoomMember, "An authenticated room member is required.");
                if (room.Match == null) return Failure<MatchSessionSnapshot>(NetworkGameError.InvalidState, "Room has no active or completed match snapshot.");
                if (room.MatchStatus == SessionMatchStatus.Active || room.MatchStatus == SessionMatchStatus.Completed
                    || room.MatchStatus == SessionMatchStatus.Aborted)
                {
                    if (!room.Match.PlayerIds.Contains(member.PlayerId))
                        return Failure<MatchSessionSnapshot>(NetworkGameError.MatchStarted, "Player was not part of this match roster.");
                }
                if (!member.Connected)
                    return Failure<MatchSessionSnapshot>(NetworkGameError.NotConnected, "Reconnect before requesting the current match snapshot.");
                return NetworkOperationResult<MatchSessionSnapshot>.Success(new MatchSessionSnapshot(room));
            }
        }

        public NetworkOperationResult<IReadOnlyList<SessionMatchHistorySnapshot>> GetMatchHistory(string roomId, string authenticatedPlayerId)
        {
            lock (_gate)
            {
                if (!TryGetRoom(roomId, out var room) || !room.Players.TryGet(authenticatedPlayerId ?? string.Empty, out var member))
                    return Failure<IReadOnlyList<SessionMatchHistorySnapshot>>(NetworkGameError.NotRoomMember, "An authenticated room member is required.");
                if (!member.Connected) return Failure<IReadOnlyList<SessionMatchHistorySnapshot>>(NetworkGameError.NotConnected, "Reconnect before requesting match history.");
                var history=room.CompletedMatches.Select(match => new SessionMatchHistorySnapshot(match,
                    room.Results.FirstOrDefault(result => result.MatchId == match.MatchId))).ToArray();
                return NetworkOperationResult<IReadOnlyList<SessionMatchHistorySnapshot>>.Success(Array.AsReadOnly(history));
            }
        }

        public NetworkOperationResult<MatchResultSnapshot> GetMatchResult(string roomId, string matchId, string authenticatedPlayerId)
        {
            lock (_gate)
            {
                SessionRoom room;
                PublicPlayerState member;
                if (!TryGetRoom(roomId, out room) || !room.Players.TryGet(authenticatedPlayerId ?? string.Empty, out member))
                    return Failure<MatchResultSnapshot>(NetworkGameError.NotRoomMember, "An authenticated room member is required.");
                var result = room.Results.FirstOrDefault(item => string.Equals(item.MatchId, matchId, StringComparison.Ordinal));
                return result == null
                    ? Failure<MatchResultSnapshot>(NetworkGameError.SessionNotFound, "Match result was not found.")
                    : NetworkOperationResult<MatchResultSnapshot>.Success(result);
            }
        }

        /// <summary>Builds a private projection for the authenticated participant only.</summary>
        public NetworkOperationResult<SessionPrivatePlayerSnapshot> GetPrivatePlayerInformation(string roomId, string authenticatedPlayerId)
        {
            lock (_gate)
            {
                SessionRoom room;
                PublicPlayerState member;
                if (!TryGetRoom(roomId, out room) || !room.Players.TryGet(authenticatedPlayerId ?? string.Empty, out member) || !member.Connected)
                    return Failure<SessionPrivatePlayerSnapshot>(NetworkGameError.NotRoomMember, "A connected authenticated room member is required.");
                if (room.Match == null || room.MatchStatus != SessionMatchStatus.Active)
                    return Failure<SessionPrivatePlayerSnapshot>(NetworkGameError.InvalidState, "Private round information is available only during an active match.");
                var playerIndex = room.Match.PlayerIds.IndexOf(member.PlayerId);
                if (playerIndex < 0) return Failure<SessionPrivatePlayerSnapshot>(NetworkGameError.MatchStarted, "Player was not part of the active match roster.");
                var runtimePlayerId = playerIndex + 1;
                var information = room.Match.Runtime.GetPrivateView(runtimePlayerId, runtimePlayerId);
                var clues = room.Match.Runtime.GetPrivateEvidence(runtimePlayerId, runtimePlayerId);
                return NetworkOperationResult<SessionPrivatePlayerSnapshot>.Success(
                    new SessionPrivatePlayerSnapshot(room.Match.MatchId, member.PlayerId, information, clues));
            }
        }

        public NetworkOperationResult<MatchSessionSnapshot> AcknowledgePrivateInformation(string roomId, string authenticatedPlayerId)
        {
            var events = new List<SessionLifecycleEvent>();
            MatchSessionSnapshot snapshot;
            lock (_gate)
            {
                SessionRoom room;
                PublicPlayerState member;
                if (!TryGetRoom(roomId, out room) || !room.Players.TryGet(authenticatedPlayerId ?? string.Empty, out member))
                    return Failure<MatchSessionSnapshot>(NetworkGameError.NotRoomMember, "An authenticated room member is required.");
                if (!member.Connected) return Failure<MatchSessionSnapshot>(NetworkGameError.NotConnected, "Reconnect before acknowledging private information.");
                if (room.Match == null || room.MatchStatus != SessionMatchStatus.Active)
                    return Failure<MatchSessionSnapshot>(NetworkGameError.InvalidState, "No active match is awaiting private information acknowledgement.");
                var index = room.Match.PlayerIds.IndexOf(member.PlayerId);
                if (index < 0) return Failure<MatchSessionSnapshot>(NetworkGameError.MatchStarted, "Player was not part of the active match roster.");
                try
                {
                    if (room.Match.Runtime.AcknowledgePrivateInformation(index + 1))
                    {
                        room.Version++;
                        events.Add(Emit(room, SessionLifecycleEventKind.PrivateInformationAcknowledged, member.PlayerId, string.Empty));
                    }
                }
                catch (InvalidOperationException exception)
                {
                    return Failure<MatchSessionSnapshot>(NetworkGameError.InvalidState, exception.Message);
                }
                snapshot = new MatchSessionSnapshot(room);
            }
            Publish(events);
            return NetworkOperationResult<MatchSessionSnapshot>.Success(snapshot);
        }

        public NetworkOperationResult<MatchSessionSnapshot> BeginDiscussion(string roomId, string authenticatedPlayerId)
        {
            CoreSessionMatch match;
            lock (_gate)
            {
                SessionRoom room;
                PublicPlayerState member;
                if (!TryGetRoom(roomId, out room) || !room.Players.TryGet(authenticatedPlayerId ?? string.Empty, out member))
                    return Failure<MatchSessionSnapshot>(NetworkGameError.NotRoomMember, "An authenticated room member is required.");
                if (!member.Connected) return Failure<MatchSessionSnapshot>(NetworkGameError.NotConnected, "Reconnect before starting discussion.");
                if (!string.Equals(room.HostPlayerId, member.PlayerId, StringComparison.Ordinal))
                    return Failure<MatchSessionSnapshot>(NetworkGameError.NotRoomAuthority, "Only the room authority can start discussion.");
                if (room.Match == null || room.MatchStatus != SessionMatchStatus.Active)
                    return Failure<MatchSessionSnapshot>(NetworkGameError.InvalidState, "There is no active match ready for discussion.");
                var runtimeMatch = room.Match.Runtime.State.Match;
                if (runtimeMatch.CurrentRoundState.Phase != RoundPhase.PrivateReveal)
                    return Failure<MatchSessionSnapshot>(NetworkGameError.InvalidState, "Discussion can only begin after private-role reveal.");
                if (room.Configuration.RequirePrivateInformationAcknowledgement
                    && (!runtimeMatch.Players.Any(player => player.Connected)
                        || runtimeMatch.Players.Any(player => player.Connected && !player.PrivateInformationAcknowledged)))
                    return Failure<MatchSessionSnapshot>(NetworkGameError.NotReady, "Every connected player must acknowledge their private information before discussion.");
                match = room.Match;
            }

            try { match.Runtime.BeginDiscussion(); }
            catch (InvalidOperationException exception)
            { return Failure<MatchSessionSnapshot>(NetworkGameError.InvalidState, exception.Message); }

            lock (_gate)
            {
                SessionRoom room;
                if (!TryGetRoom(roomId, out room) || !ReferenceEquals(room.Match, match))
                    return Failure<MatchSessionSnapshot>(NetworkGameError.InvalidState, "Match changed while discussion was starting.");
                return NetworkOperationResult<MatchSessionSnapshot>.Success(new MatchSessionSnapshot(room));
            }
        }

        /// <summary>Roles are returned publicly only after the core has committed the reveal.</summary>
        public NetworkOperationResult<IReadOnlyList<SessionRevealedPlayerSnapshot>> GetRevealedPlayerInformation(string roomId, string authenticatedPlayerId)
        {
            lock (_gate)
            {
                SessionRoom room;
                PublicPlayerState member;
                if (!TryGetRoom(roomId, out room) || !room.Players.TryGet(authenticatedPlayerId ?? string.Empty, out member) || !member.Connected)
                    return Failure<IReadOnlyList<SessionRevealedPlayerSnapshot>>(NetworkGameError.NotRoomMember, "A connected authenticated room member is required.");
                if (room.Match == null || (room.MatchStatus != SessionMatchStatus.Active && room.MatchStatus != SessionMatchStatus.Completed)
                    || !room.Match.PlayerIds.Contains(member.PlayerId))
                    return Failure<IReadOnlyList<SessionRevealedPlayerSnapshot>>(NetworkGameError.InvalidState, "The authenticated player has no active match reveal.");
                try
                {
                    var revealed = room.Match.Runtime.GetRevealedPlayers()
                        .Select(item => new SessionRevealedPlayerSnapshot(room.Match.PlayerIds[item.PlayerId - 1], item.Role, item.IsThief)).ToArray();
                    return NetworkOperationResult<IReadOnlyList<SessionRevealedPlayerSnapshot>>.Success(Array.AsReadOnly(revealed));
                }
                catch (InvalidOperationException exception)
                {
                    return Failure<IReadOnlyList<SessionRevealedPlayerSnapshot>>(NetworkGameError.InvalidState, exception.Message);
                }
            }
        }

        /// <summary>Reconnect projection for the current round, available only after authoritative reveal starts.</summary>
        public NetworkOperationResult<SessionRevealSnapshot> GetRevealSnapshot(string roomId, string authenticatedPlayerId)
        {
            lock (_gate)
            {
                if (!TryGetRoom(roomId, out var room) || !room.Players.TryGet(authenticatedPlayerId ?? string.Empty, out var member) || !member.Connected)
                    return Failure<SessionRevealSnapshot>(NetworkGameError.NotRoomMember, "A connected authenticated room member is required.");
                if (room.Match == null || (room.MatchStatus != SessionMatchStatus.Active && room.MatchStatus != SessionMatchStatus.Completed)
                    || !room.Match.PlayerIds.Contains(member.PlayerId))
                    return Failure<SessionRevealSnapshot>(NetworkGameError.InvalidState, "The authenticated player has no active match reveal.");
                try
                {
                    var snapshot=room.Match.Runtime.GetRevealSnapshot();
                    return NetworkOperationResult<SessionRevealSnapshot>.Success(new SessionRevealSnapshot(snapshot, room.Match.PlayerIds.AsReadOnly()));
                }
                catch (InvalidOperationException exception)
                { return Failure<SessionRevealSnapshot>(NetworkGameError.InvalidState, exception.Message); }
            }
        }

        /// <summary>Authenticated reconnect projection for current round and cumulative scores.</summary>
        public NetworkOperationResult<SessionScoreSnapshot> GetScoreSnapshot(string roomId, string authenticatedPlayerId)
        {
            lock (_gate)
            {
                if (!TryGetRoom(roomId, out var room) || !room.Players.TryGet(authenticatedPlayerId ?? string.Empty, out var member) || !member.Connected)
                    return Failure<SessionScoreSnapshot>(NetworkGameError.NotRoomMember, "A connected authenticated room member is required.");
                if (room.Match == null || (room.MatchStatus != SessionMatchStatus.Active && room.MatchStatus != SessionMatchStatus.Completed)
                    || !room.Match.PlayerIds.Contains(member.PlayerId))
                    return Failure<SessionScoreSnapshot>(NetworkGameError.InvalidState, "The authenticated player has no active match score snapshot.");
                var snapshot=room.Match.Runtime.GetScoreSnapshot();
                return NetworkOperationResult<SessionScoreSnapshot>.Success(new SessionScoreSnapshot(snapshot, room.Match.PlayerIds.AsReadOnly()));
            }
        }

        public NetworkOperationResult<MatchSessionSnapshot> ContinueFromResults(string roomId, string authenticatedPlayerId, string matchId, string roundId)
        {
            CoreSessionMatch activeMatch; int playerId;
            lock (_gate)
            {
                if (!TryGetRoom(roomId, out var room) || !room.Players.TryGet(authenticatedPlayerId ?? string.Empty, out var member))
                    return Failure<MatchSessionSnapshot>(NetworkGameError.NotRoomMember, "An authenticated room member is required.");
                if (!member.Connected) return Failure<MatchSessionSnapshot>(NetworkGameError.NotConnected, "Reconnect before continuing from results.");
                if (room.Match == null || room.MatchStatus != SessionMatchStatus.Active)
                    return Failure<MatchSessionSnapshot>(NetworkGameError.InvalidState, "There is no active match results phase.");
                var index=room.Match.PlayerIds.IndexOf(member.PlayerId);
                if (index < 0) return Failure<MatchSessionSnapshot>(NetworkGameError.InvalidState, "Player was not part of this match.");
                if (room.Match.Runtime.State.Match.CurrentRoundState.RoundId != roundId || room.Match.MatchId != matchId)
                    return Failure<MatchSessionSnapshot>(NetworkGameError.StaleRound, "Continue command belongs to an earlier or different match round.");
                activeMatch=room.Match; playerId=index+1;
            }
            bool newlyReady;
            try
            {
                newlyReady=activeMatch.Runtime.ContinueFromResults(playerId, matchId, roundId);
            }
            catch (InvalidOperationException exception)
            {
                var error=exception.Message.StartsWith("StaleRound:", StringComparison.Ordinal) ? NetworkGameError.StaleRound : NetworkGameError.InvalidPhase;
                return Failure<MatchSessionSnapshot>(error, exception.Message);
            }
            lock (_gate)
            {
                if (!TryGetRoom(roomId, out var room) || room.Match != activeMatch)
                    return Failure<MatchSessionSnapshot>(NetworkGameError.StaleRound, "The active match changed while processing continue.");
                if (newlyReady) room.Version++;
                return NetworkOperationResult<MatchSessionSnapshot>.Success(new MatchSessionSnapshot(room));
            }
        }

        public NetworkOperationResult<MatchSessionSnapshot> PollResultsTimeout(string roomId, string authenticatedPlayerId)
        {
            CoreSessionMatch activeMatch;
            lock (_gate)
            {
                if (!TryGetRoom(roomId, out var room) || !room.Players.TryGet(authenticatedPlayerId ?? string.Empty, out var member) || !member.Connected)
                    return Failure<MatchSessionSnapshot>(NetworkGameError.NotRoomMember, "A connected authenticated room member is required.");
                if (!string.Equals(room.HostPlayerId, member.PlayerId, StringComparison.Ordinal)) return Failure<MatchSessionSnapshot>(NetworkGameError.NotRoomAuthority, "Only the room authority may poll results timeout.");
                if (room.Match == null || room.MatchStatus != SessionMatchStatus.Active) return Failure<MatchSessionSnapshot>(NetworkGameError.InvalidState, "There is no active results phase.");
                activeMatch=room.Match;
            }
            var advanced=activeMatch.Runtime.PollResultsTimeout();
            lock (_gate)
            {
                if (!TryGetRoom(roomId, out var room) || room.Match != activeMatch) return Failure<MatchSessionSnapshot>(NetworkGameError.StaleRound, "The active match changed while polling results.");
                if (advanced) room.Version++;
                return NetworkOperationResult<MatchSessionSnapshot>.Success(new MatchSessionSnapshot(room));
            }
        }

        public NetworkOperationResult<MatchSessionSnapshot> AcknowledgeRevealPresentation(string roomId, string authenticatedPlayerId)
        {
            CoreSessionMatch match;
            lock (_gate)
            {
                if (!TryGetRoom(roomId, out var room) || !room.Players.TryGet(authenticatedPlayerId ?? string.Empty, out var member) || !member.Connected)
                    return Failure<MatchSessionSnapshot>(NetworkGameError.NotRoomMember, "A connected authenticated room member is required.");
                if (!string.Equals(room.HostPlayerId, member.PlayerId, StringComparison.Ordinal))
                    return Failure<MatchSessionSnapshot>(NetworkGameError.NotRoomAuthority, "Only the room authority can acknowledge reveal presentation.");
                if (room.Match == null || room.MatchStatus != SessionMatchStatus.Active
                    || room.Match.Runtime.State.Match.CurrentRoundState.Phase != RoundPhase.Reveal)
                    return Failure<MatchSessionSnapshot>(NetworkGameError.InvalidPhase, "Reveal presentation can be acknowledged only during the active reveal phase.");
                match=room.Match;
            }
            try { match.Runtime.CompleteReveal(); }
            catch (InvalidOperationException exception)
            { return Failure<MatchSessionSnapshot>(NetworkGameError.InvalidState, exception.Message); }
            lock (_gate)
            {
                if (!TryGetRoom(roomId, out var room) || !ReferenceEquals(room.Match, match))
                    return Failure<MatchSessionSnapshot>(NetworkGameError.InvalidState, "Match changed while reveal was being acknowledged.");
                return NetworkOperationResult<MatchSessionSnapshot>.Success(new MatchSessionSnapshot(room));
            }
        }

        public NetworkOperationResult<CoreGameRuntime> GetAuthorityGameplayRuntime(string roomId)
        {
            lock (_gate)
            {
                SessionRoom room;
                if (!TryGetRoom(roomId, out room) || room.Match == null || room.MatchStatus != SessionMatchStatus.Active)
                    return Failure<CoreGameRuntime>(NetworkGameError.InvalidState, "Room has no active gameplay runtime.");
                return NetworkOperationResult<CoreGameRuntime>.Success(room.Match.Runtime);
            }
        }

        public NetworkOperationResult<DiscussionSnapshot> GetDiscussionSnapshot(string roomId, string authenticatedPlayerId, long afterSequence = 0, int pageSize = 100)
        {
            CoreGameRuntime runtime;
            int playerIndex;
            string message;
            var error=ResolveDiscussionParticipant(roomId, authenticatedPlayerId, false, out runtime, out playerIndex, out message);
            if (error != NetworkGameError.None) return Failure<DiscussionSnapshot>(error, message);
            try { return NetworkOperationResult<DiscussionSnapshot>.Success(runtime.GetDiscussionSnapshot(afterSequence, pageSize)); }
            catch (ArgumentOutOfRangeException exception) { return Failure<DiscussionSnapshot>(NetworkGameError.InvalidState, exception.Message); }
            catch (InvalidOperationException exception) { return Failure<DiscussionSnapshot>(NetworkGameError.InvalidState, exception.Message); }
        }

        public NetworkOperationResult<DiscussionMessage> SendDiscussionMessage(string roomId, string authenticatedPlayerId, string text)
        {
            CoreGameRuntime runtime; int playerIndex; string message;
            var error=ResolveDiscussionParticipant(roomId, authenticatedPlayerId, true, out runtime, out playerIndex, out message);
            if (error != NetworkGameError.None) return Failure<DiscussionMessage>(error, message);
            try { return NetworkOperationResult<DiscussionMessage>.Success(runtime.SendChatMessage(playerIndex, text)); }
            catch (ArgumentException exception) { return Failure<DiscussionMessage>(NetworkGameError.InvalidChat, exception.Message); }
            catch (InvalidOperationException exception) { return Failure<DiscussionMessage>(exception.Message.Contains("rate limit") ? NetworkGameError.RateLimited : NetworkGameError.InvalidPhase, exception.Message); }
        }

        public NetworkOperationResult<PlayerQuestion> SubmitDiscussionQuestion(string roomId, string authenticatedPlayerId, string targetPlayerId, string text)
        {
            CoreGameRuntime runtime; int playerIndex; string message;
            var error=ResolveDiscussionParticipant(roomId, authenticatedPlayerId, true, out runtime, out playerIndex, out message);
            if (error != NetworkGameError.None) return Failure<PlayerQuestion>(error, message);
            CoreSessionMatch match;
            lock (_gate) { SessionRoom room; if (!TryGetRoom(roomId, out room) || room.Match == null) return Failure<PlayerQuestion>(NetworkGameError.InvalidState, "Match is no longer active."); match=room.Match; }
            var targetIndex=match.PlayerIds.IndexOf(targetPlayerId ?? string.Empty);
            if (targetIndex < 0) return Failure<PlayerQuestion>(NetworkGameError.PlayerNotFound, "Question target is not in this match.");
            try { return NetworkOperationResult<PlayerQuestion>.Success(runtime.SubmitQuestion(playerIndex, targetIndex + 1, text)); }
            catch (ArgumentException exception) { return Failure<PlayerQuestion>(NetworkGameError.InvalidChat, exception.Message); }
            catch (InvalidOperationException exception) { return Failure<PlayerQuestion>(exception.Message.Contains("rate limit") ? NetworkGameError.RateLimited : NetworkGameError.InvalidPhase, exception.Message); }
        }

        public NetworkOperationResult<DiscussionResponse> RespondToDiscussionQuestion(string roomId, string authenticatedPlayerId, string questionId, string text)
        {
            CoreGameRuntime runtime; int playerIndex; string message;
            var error=ResolveDiscussionParticipant(roomId, authenticatedPlayerId, true, out runtime, out playerIndex, out message);
            if (error != NetworkGameError.None) return Failure<DiscussionResponse>(error, message);
            try { return NetworkOperationResult<DiscussionResponse>.Success(runtime.RespondToQuestion(playerIndex, questionId, text)); }
            catch (ArgumentException exception) { return Failure<DiscussionResponse>(NetworkGameError.InvalidChat, exception.Message); }
            catch (InvalidOperationException exception) { return Failure<DiscussionResponse>(exception.Message.Contains("rate limit") ? NetworkGameError.RateLimited : NetworkGameError.InvalidPhase, exception.Message); }
        }

        public NetworkOperationResult<PlayerClaim> SubmitDiscussionClaim(string roomId, string authenticatedPlayerId, string factId, string statement, float gameTime, ClaimType type = ClaimType.General)
        {
            CoreGameRuntime runtime; int playerIndex; string message;
            var error=ResolveDiscussionParticipant(roomId, authenticatedPlayerId, true, out runtime, out playerIndex, out message);
            if (error != NetworkGameError.None) return Failure<PlayerClaim>(error, message);
            try { return NetworkOperationResult<PlayerClaim>.Success(runtime.AddClaim(playerIndex, factId, statement, gameTime, type)); }
            catch (ArgumentException exception) { return Failure<PlayerClaim>(NetworkGameError.InvalidChat, exception.Message); }
            catch (InvalidOperationException exception) { return Failure<PlayerClaim>(exception.Message.Contains("rate limit") ? NetworkGameError.RateLimited : NetworkGameError.InvalidPhase, exception.Message); }
        }

        public NetworkOperationResult<ClaimChallenge> ChallengeDiscussionClaim(string roomId, string authenticatedPlayerId, string claimId, string text)
        {
            CoreGameRuntime runtime; int playerIndex; string message;
            var error=ResolveDiscussionParticipant(roomId, authenticatedPlayerId, true, out runtime, out playerIndex, out message);
            if (error != NetworkGameError.None) return Failure<ClaimChallenge>(error, message);
            try { return NetworkOperationResult<ClaimChallenge>.Success(runtime.ChallengeClaim(playerIndex, claimId, text)); }
            catch (ArgumentException exception) { return Failure<ClaimChallenge>(NetworkGameError.InvalidChat, exception.Message); }
            catch (InvalidOperationException exception) { return Failure<ClaimChallenge>(exception.Message.Contains("rate limit") ? NetworkGameError.RateLimited : NetworkGameError.InvalidPhase, exception.Message); }
        }

        public NetworkOperationResult<bool> RequestDiscussionEarlyVote(string roomId, string authenticatedPlayerId)
        {
            CoreGameRuntime runtime; int playerIndex; string message;
            var error=ResolveDiscussionParticipant(roomId, authenticatedPlayerId, true, out runtime, out playerIndex, out message);
            if (error != NetworkGameError.None) return Failure<bool>(error, message);
            try { return NetworkOperationResult<bool>.Success(runtime.RequestEarlyVote(playerIndex)); }
            catch (InvalidOperationException exception) { return Failure<bool>(exception.Message.Contains("rate limit") ? NetworkGameError.RateLimited : NetworkGameError.InvalidPhase, exception.Message); }
        }

        public NetworkOperationResult<VoteSubmissionResult> SubmitVote(string roomId, string authenticatedPlayerId, string targetPlayerId, int voteSequence)
        {
            CoreGameRuntime runtime; int voterIndex; string message;
            var error=ResolveDiscussionParticipant(roomId, authenticatedPlayerId, false, out runtime, out voterIndex, out message);
            if (error != NetworkGameError.None) return Failure<VoteSubmissionResult>(error, message);
            CoreSessionMatch match;
            lock (_gate) { SessionRoom room; if (!TryGetRoom(roomId, out room) || room.Match == null) return Failure<VoteSubmissionResult>(NetworkGameError.InvalidState, "Match is no longer active."); match=room.Match; }
            var targetIndex=match.PlayerIds.IndexOf(targetPlayerId ?? string.Empty);
            if (targetIndex < 0) return Failure<VoteSubmissionResult>(NetworkGameError.PlayerNotFound, "Vote target is not in this match.");
            return NetworkOperationResult<VoteSubmissionResult>.Success(runtime.SubmitVote(voterIndex, targetIndex + 1, voteSequence));
        }

        public NetworkOperationResult<SessionVotingSnapshot> GetVotingSnapshot(string roomId, string authenticatedPlayerId)
        {
            CoreGameRuntime runtime; int playerIndex; string message;
            var error=ResolveDiscussionParticipant(roomId, authenticatedPlayerId, false, out runtime, out playerIndex, out message);
            if (error != NetworkGameError.None) return Failure<SessionVotingSnapshot>(error, message);
            if (runtime.State.Match.CurrentRoundState.Phase != RoundPhase.Voting) return Failure<SessionVotingSnapshot>(NetworkGameError.InvalidPhase, "Voting is not active.");
            CoreSessionMatch match;
            lock (_gate) { SessionRoom room; if (!TryGetRoom(roomId, out room) || room.Match == null) return Failure<SessionVotingSnapshot>(NetworkGameError.InvalidState, "Match is no longer active."); match=room.Match; }
            return NetworkOperationResult<SessionVotingSnapshot>.Success(new SessionVotingSnapshot(runtime, playerIndex, match.PlayerIds));
        }

        /// <summary>Called by the authoritative host scheduler; client clocks never close voting.</summary>
        public void PollActiveVotingTimers()
        {
            CoreGameRuntime[] runtimes;
            lock (_gate) runtimes=_rooms.Values.Where(room => room.MatchStatus == SessionMatchStatus.Active && room.Match != null).Select(room => room.Match.Runtime).ToArray();
            foreach (var runtime in runtimes)
            {
                runtime.AdvanceVotingTimer();
                var finalized=runtime.GetFinalizedVotes();
                if (finalized != null && runtime.State.Match.CurrentRoundState.Phase == RoundPhase.Voting)
                    runtime.ResolveVote(finalized.VoteSequence);
            }
        }

        private void OnDiscussionEventPublished(CoreSessionMatch match, DiscussionEvent item)
        {
            string roomId;
            string playerId=string.Empty;
            lock (_gate)
            {
                var room=FindRoom(match);
                if (room == null || (room.MatchStatus != SessionMatchStatus.Active && room.MatchStatus != SessionMatchStatus.Completing)) return;
                roomId=room.RoomId;
                if (item.PlayerId > 0 && item.PlayerId <= match.PlayerIds.Count) playerId=match.PlayerIds[item.PlayerId - 1];
            }
            DiscussionEventPublished?.Invoke(roomId, new SessionDiscussionEvent(item, playerId));
        }

        private void OnVoteEventPublished(CoreSessionMatch match, VoteEventType type, int playerIndex, VoteRejectionReason reason)
        {
            string roomId; string playerId=string.Empty; VoteProgressViewData progress; int sequence;
            lock (_gate)
            {
                var room=FindRoom(match);
                if (room == null || room.MatchStatus != SessionMatchStatus.Active) return;
                roomId=room.RoomId;
                if (playerIndex > 0 && playerIndex <= match.PlayerIds.Count) playerId=match.PlayerIds[playerIndex - 1];
                progress=match.Runtime.GetVoteProgress(); sequence=match.Runtime.State.Votes.VoteRound + 1;
            }
            VoteEventPublished?.Invoke(roomId, new SessionVoteEvent(type, reason, playerId, sequence, progress));
        }

        private void OnVoteResolutionPublished(CoreSessionMatch match, VoteResolutionResult result)
        {
            string roomId; IReadOnlyList<string> playerIds;
            lock (_gate)
            {
                var room=FindRoom(match);
                if (room == null || room.MatchStatus != SessionMatchStatus.Active) return;
                roomId=room.RoomId; playerIds=match.PlayerIds.AsReadOnly();
            }
            VoteResolutionPublished?.Invoke(roomId, new SessionVoteResolutionEvent(result, playerIds));
        }

        private void OnRevealEventPublished(CoreSessionMatch match, RevealEvent item)
        {
            string roomId; IReadOnlyList<string> playerIds;
            lock (_gate)
            {
                var room=FindRoom(match);
                if (room == null || room.MatchStatus != SessionMatchStatus.Active) return;
                roomId=room.RoomId; playerIds=match.PlayerIds.AsReadOnly();
            }
            RevealEventPublished?.Invoke(roomId, new SessionRevealEvent(item, playerIds));
        }

        private void OnScoreEventPublished(CoreSessionMatch match, ScoreEvent item)
        {
            string roomId; IReadOnlyList<string> playerIds;
            lock (_gate)
            {
                var room=FindRoom(match);
                if (room == null || room.MatchStatus != SessionMatchStatus.Active) return;
                roomId=room.RoomId; playerIds=match.PlayerIds.AsReadOnly();
            }
            ScoreEventPublished?.Invoke(roomId, new SessionScoreEvent(item, playerIds));
        }

        private void OnRoundFlowEventPublished(CoreSessionMatch match, RoundFlowEvent item)
        {
            string roomId; IReadOnlyList<string> playerIds; List<SessionLifecycleEvent> events=new List<SessionLifecycleEvent>();
            lock (_gate)
            {
                var room=FindRoom(match);
                if (room == null || (room.MatchStatus != SessionMatchStatus.Active && room.MatchStatus != SessionMatchStatus.Completing)) return;
                roomId=room.RoomId; playerIds=match.PlayerIds.AsReadOnly();
                if (item.Type == RoundFlowEventType.RoundResultsOpened)
                {
                    room.Version++;
                    events.Add(Emit(room, SessionLifecycleEventKind.RoundResultsOpened, string.Empty, item.RoundId));
                }
                else if (item.Type == RoundFlowEventType.PlayerReadyToContinue)
                {
                    room.Version++;
                    var playerId=item.PlayerId.HasValue ? match.PlayerIds[item.PlayerId.Value-1] : string.Empty;
                    events.Add(Emit(room, SessionLifecycleEventKind.ContinueReceived, playerId, item.RoundId));
                }
                else if (item.Type == RoundFlowEventType.MatchCompleting)
                {
                    room.MatchStatus=SessionMatchStatus.Completing;
                    room.Version++;
                    events.Add(Emit(room, SessionLifecycleEventKind.MatchCompleting, string.Empty, item.RoundId));
                }
                else if (item.Type == RoundFlowEventType.MatchResultsPublished)
                {
                    room.Version++;
                    events.Add(Emit(room, SessionLifecycleEventKind.MatchResultsPublished, string.Empty, item.RoundId));
                }
            }
            RoundFlowEventPublished?.Invoke(roomId, new SessionRoundFlowEvent(item, playerIds));
            Publish(events);
        }

        private NetworkGameError ResolveDiscussionParticipant(string roomId, string authenticatedPlayerId, bool requireDiscussion,
            out CoreGameRuntime runtime, out int playerIndex, out string message)
        {
            runtime=null; playerIndex=0; message=string.Empty;
            lock (_gate)
            {
                if (!TryGetRoom(roomId, out var room) || !room.Players.TryGet(authenticatedPlayerId ?? string.Empty, out var member))
                { message="An authenticated room member is required."; return NetworkGameError.NotRoomMember; }
                if (!member.Connected) { message="Reconnect before participating in discussion."; return NetworkGameError.NotConnected; }
                if (room.Match == null || room.MatchStatus != SessionMatchStatus.Active || !room.Match.PlayerIds.Contains(member.PlayerId))
                { message="There is no active match for this room member."; return NetworkGameError.InvalidState; }
                var phase=room.Match.Runtime.State.Match.CurrentRoundState.Phase;
                if (requireDiscussion && phase != RoundPhase.Discussion)
                { message="Discussion is not active."; return NetworkGameError.InvalidPhase; }
                if (!requireDiscussion && phase != RoundPhase.Discussion && phase != RoundPhase.Voting)
                { message="Discussion history is unavailable in this phase."; return NetworkGameError.InvalidPhase; }
                runtime=room.Match.Runtime;
                playerIndex=room.Match.PlayerIds.IndexOf(member.PlayerId) + 1;
                return NetworkGameError.None;
            }
        }

        private void OnRoundCompleted(CoreSessionMatch match, int round)
        {
            List<SessionLifecycleEvent> events;
            lock (_gate)
            {
                var room = FindRoom(match);
                if (room == null || room.MatchStatus != SessionMatchStatus.Active) return;
                room.Version++;
                events = new List<SessionLifecycleEvent> { Emit(room, SessionLifecycleEventKind.RoundCompleted, string.Empty, round.ToString()) };
            }
            Publish(events);
        }

        private void OnRoundStarted(CoreSessionMatch match, int round)
        {
            List<SessionLifecycleEvent> events;
            lock (_gate)
            {
                var room = FindRoom(match);
                if (room == null || room.MatchStatus != SessionMatchStatus.Active) return;
                room.Version++;
                events = new List<SessionLifecycleEvent> { Emit(room, SessionLifecycleEventKind.RoundStarted, string.Empty, round.ToString()) };
            }
            Publish(events);
        }

        private void OnDiscussionStarted(CoreSessionMatch match)
        {
            List<SessionLifecycleEvent> events;
            lock (_gate)
            {
                var room = FindRoom(match);
                if (room == null || room.MatchStatus != SessionMatchStatus.Active) return;
                room.Version++;
                events = new List<SessionLifecycleEvent> { Emit(room, SessionLifecycleEventKind.DiscussionStarted, string.Empty, string.Empty) };
            }
            Publish(events);
        }

        private void OnMatchCompleted(CoreSessionMatch match)
        {
            List<SessionLifecycleEvent> events;
            lock (_gate)
            {
                var room = FindRoom(match);
                if (room == null || (room.MatchStatus != SessionMatchStatus.Active && room.MatchStatus != SessionMatchStatus.Completing)) return;
                room.MatchStatus = SessionMatchStatus.Completed;
                room.LastResult = BuildResult(room);
                room.Results.Add(room.LastResult);
                if (!room.CompletedMatches.Contains(match)) room.CompletedMatches.Add(match);
                room.Players.ResetMatchLock();
                room.Version++;
                foreach (var id in match.PlayerIds) _playerStates[id] = SessionState.MatchResults;
                events = new List<SessionLifecycleEvent> { Emit(room, SessionLifecycleEventKind.MatchCompleted, string.Empty, string.Empty) };
            }
            Publish(events);
        }

        private void OnMatchAborted(CoreSessionMatch match)
        {
            List<SessionLifecycleEvent> events;
            lock (_gate)
            {
                var room = FindRoom(match);
                if (room == null || room.MatchStatus != SessionMatchStatus.Active) return;
                room.MatchStatus = SessionMatchStatus.Aborted;
                room.Version++;
                events = new List<SessionLifecycleEvent> { Emit(room, SessionLifecycleEventKind.MatchAborted, string.Empty, "gameplay-aborted") };
            }
            Publish(events);
        }

        private static MatchResultSnapshot BuildResult(SessionRoom room)
        {
            var rows = room.Match.Runtime.State.Match.Players
                .Select(p => new MatchScoreRow(room.Match.PlayerIds[p.PlayerId - 1], p.MatchScore));
            return new MatchResultSnapshot(room.Match.MatchId, room.Match.CurrentRound, rows);
        }

        private bool TryGetMemberRoom(string playerId, out SessionRoom room, out PublicPlayerState player)
        {
            room = null;
            player = null;
            string roomId;
            return !string.IsNullOrWhiteSpace(playerId) && _playerRooms.TryGetValue(playerId, out roomId)
                && _rooms.TryGetValue(roomId, out room) && room.Players.TryGet(playerId, out player);
        }

        private bool TryGetRoom(string roomId, out SessionRoom room) => _rooms.TryGetValue(roomId ?? string.Empty, out room);

        private SessionRoom FindRoom(CoreSessionMatch match) => _rooms.Values.FirstOrDefault(room => ReferenceEquals(room.Match, match));

        private void TransferHost(SessionRoom room, List<SessionLifecycleEvent> events, string previousHost)
        {
            var connectedPlayers = room.Players.Players.Where(p => p.Connected);
            if (room.MatchStatus == SessionMatchStatus.Active && room.Match != null)
                connectedPlayers = connectedPlayers.Where(p => room.Match.PlayerIds.Contains(p.PlayerId));
            var nextHost = connectedPlayers.FirstOrDefault();
            room.HostPlayerId = nextHost == null ? string.Empty : nextHost.PlayerId;
            events.Add(Emit(room, SessionLifecycleEventKind.HostAuthorityChanged, room.HostPlayerId,
                string.IsNullOrEmpty(room.HostPlayerId) ? "no-connected-host" : "previous:" + previousHost));
        }

        private static SessionLifecycleEvent Emit(SessionRoom room, SessionLifecycleEventKind kind, string playerId, string reason)
        {
            return new SessionLifecycleEvent(++room.EventSequence, kind, room, playerId, reason);
        }

        private void Publish(IEnumerable<SessionLifecycleEvent> events)
        {
            var handler = LifecycleEvent;
            if (handler == null) return;
            foreach (var item in events) handler(item);
        }

        private static NetworkOperationResult<T> Failure<T>(NetworkGameError error, string message)
            => NetworkOperationResult<T>.Failure(error, message);
    }

}
