using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ThiefAndTheNecklace.Networking
{
    public enum RecoveryState { None, Disconnected, Reconnecting, Synchronizing, Rehydrating, Recovered, Failed, Removed }
    public enum RecoveryInputPolicy { BlockGameplayOnly, BlockAllInput, AllowChatOnly }
    public enum RecoveryResult { Reconnected, Rehydrated, AlreadyCurrent, Removed, MatchCompleted, MatchAborted, Failed }
    public enum AuthorityRecoveryState { Stable, AuthorityLost, ElectionPending, NewAuthoritySelected, StateRecovery, AuthorityRestored, Failed }
    public enum RecoveryEventType { ConnectionLost, RecoveryStarted, ReconnectAttempted, ConnectionRestored, SnapshotRequested, SnapshotFailed, SnapshotApplied, PlayerRehydrated, RecoveryCompleted, RecoveryTimeout, PlayerRemoved, RecoveryFailed, AuthorityLost, AuthorityChanged, StateRecoveryStarted, CommittedStateLoaded, StateValidated, AuthorityStateRecovered }
    public enum RecoveryFailureReason { AuthorityUnavailable, InsufficientPlayers, SnapshotInvalid, ProtocolMismatch, StateCorruption, ReconnectTimeout, RoomClosed, TransportFailure, Unknown }
    public enum RecoveryDecision { MatchActive, MatchCompleted, MatchAborted, PlayerRemoved, RoomClosed, ProtocolMismatch, Unknown }
    public enum EventCatchUpMode { AlreadyCurrent, ReplayEvents, RequestSnapshot }

    public sealed class EventCatchUpPlan
    {
        public EventCatchUpMode Mode { get; }
        public long FromSequenceExclusive { get; }
        public long ToSequenceInclusive { get; }
        private EventCatchUpPlan(EventCatchUpMode mode, long from, long to) { Mode=mode; FromSequenceExclusive=from; ToSequenceInclusive=to; }
        public static EventCatchUpPlan Create(long localSequence, long authoritySequence, int maximumCatchUpEvents)
        {
            if (localSequence < 0 || authoritySequence < localSequence || maximumCatchUpEvents < 1) throw new ArgumentOutOfRangeException(nameof(localSequence));
            if (localSequence == authoritySequence) return new EventCatchUpPlan(EventCatchUpMode.AlreadyCurrent, localSequence, authoritySequence);
            return authoritySequence-localSequence <= maximumCatchUpEvents
                ? new EventCatchUpPlan(EventCatchUpMode.ReplayEvents, localSequence, authoritySequence)
                : new EventCatchUpPlan(EventCatchUpMode.RequestSnapshot, localSequence, authoritySequence);
        }
    }

    public sealed class AuthorityHeartbeat
    {
        public long AuthoritySequence { get; }
        public DateTimeOffset ServerTimestamp { get; }
        public string AuthorityActorId { get; }
        public AuthorityHeartbeat(long sequence, DateTimeOffset serverTimestamp, string authorityActorId)
        { if (sequence < 0 || string.IsNullOrWhiteSpace(authorityActorId)) throw new ArgumentException("Authority heartbeat identity is invalid."); AuthoritySequence=sequence; ServerTimestamp=serverTimestamp; AuthorityActorId=authorityActorId; }
    }

    public sealed class FailureResult
    {
        public string MatchId { get; }
        public string RoundId { get; }
        public RecoveryFailureReason Reason { get; }
        public bool MatchAborted { get; }
        public bool ScoresFinalized { get; }
        public FailureResult(string matchId, string roundId, RecoveryFailureReason reason, bool matchAborted, bool scoresFinalized)
        { MatchId=matchId ?? string.Empty; RoundId=roundId ?? string.Empty; Reason=reason; MatchAborted=matchAborted; ScoresFinalized=scoresFinalized; }
    }

    public sealed class RecoveryTelemetry
    {
        public int DisconnectCount { get; private set; }
        public int ReconnectCount { get; private set; }
        public int SnapshotFailureCount { get; private set; }
        public int RecoveryFailureCount { get; private set; }
        public int ProtocolMismatchCount { get; private set; }
        public int AuthorityChangeCount { get; private set; }
        public int PlayerRemovalCount { get; private set; }
        public int MatchAbortCount { get; private set; }
        public long TotalRecoveryDurationMilliseconds { get; private set; }
        public void Record(RecoveryEventType type, long recoveryDurationMilliseconds=0)
        {
            switch (type)
            {
                case RecoveryEventType.ConnectionLost: DisconnectCount++; break;
                case RecoveryEventType.ConnectionRestored: ReconnectCount++; break;
                case RecoveryEventType.RecoveryCompleted: TotalRecoveryDurationMilliseconds=Math.Max(0, checked(TotalRecoveryDurationMilliseconds + recoveryDurationMilliseconds)); break;
                case RecoveryEventType.SnapshotFailed: SnapshotFailureCount++; break;
                case RecoveryEventType.PlayerRemoved: PlayerRemovalCount++; break;
                case RecoveryEventType.AuthorityChanged: AuthorityChangeCount++; break;
                case RecoveryEventType.RecoveryFailed: RecoveryFailureCount++; break;
            }
        }
        public void RecordProtocolMismatch() => ProtocolMismatchCount++;
        public void RecordMatchAbort() => MatchAbortCount++;
    }

    [Serializable]
    public sealed class RecoveryConfig
    {
        public int MaxAttempts=5;
        public int BaseRetryDelayMilliseconds=1000;
        public int MaxRetryDelayMilliseconds=10000;
        public int RecoveryWindowSeconds=30;
        public bool ContinueMatchDuringWindow=true;
        public bool RemoveAfterTimeout=true;
        public RecoveryInputPolicy InputPolicy=RecoveryInputPolicy.AllowChatOnly;
        public int MaximumCatchUpEvents=100;
        public RecoveryConfig Copy() => (RecoveryConfig)MemberwiseClone();
        public void Validate()
        {
            if (MaxAttempts < 1 || BaseRetryDelayMilliseconds < 1 || MaxRetryDelayMilliseconds < BaseRetryDelayMilliseconds
                || RecoveryWindowSeconds < 1 || MaximumCatchUpEvents < 1 || !Enum.IsDefined(typeof(RecoveryInputPolicy), InputPolicy))
                throw new ArgumentOutOfRangeException(nameof(MaxAttempts), "Recovery retry, window, catch-up, and input settings are invalid.");
        }
    }

    public sealed class RecoveryContext
    {
        public string PlayerId { get; }
        public string MatchId { get; }
        public string LastKnownRoundId { get; internal set; }
        public long LastKnownSequence { get; internal set; }
        public RecoveryState State { get; internal set; }
        public DateTimeOffset DisconnectStartedAt { get; internal set; }
        public int ReconnectAttempts { get; internal set; }
        internal RecoveryContext(string playerId, string matchId, string roundId, long sequence, DateTimeOffset disconnectedAt)
        { PlayerId=playerId; MatchId=matchId; LastKnownRoundId=roundId; LastKnownSequence=sequence; DisconnectStartedAt=disconnectedAt; State=RecoveryState.Disconnected; }
    }

    public sealed class RecoveryWindowPolicy
    {
        public int RecoveryWindowSeconds { get; }
        public bool ContinueMatchDuringWindow { get; }
        public bool RemoveAfterTimeout { get; }
        public RecoveryWindowPolicy(int recoveryWindowSeconds=30, bool continueMatchDuringWindow=true, bool removeAfterTimeout=true)
        {
            if (recoveryWindowSeconds < 1) throw new ArgumentOutOfRangeException(nameof(recoveryWindowSeconds));
            RecoveryWindowSeconds=recoveryWindowSeconds; ContinueMatchDuringWindow=continueMatchDuringWindow; RemoveAfterTimeout=removeAfterTimeout;
        }
        public bool IsExpired(DateTimeOffset disconnectedAt, DateTimeOffset now)
            => now >= disconnectedAt.AddSeconds(RecoveryWindowSeconds);
    }

    public sealed class RecoverySnapshot
    {
        private readonly byte[] ownPrivateState;
        public string RecipientPlayerId { get; }
        public NetworkMatchSnapshot PublicState { get; }
        public byte[] OwnPrivateState => ownPrivateState == null ? null : (byte[])ownPrivateState.Clone();
        public RecoverySnapshot(string recipientPlayerId, NetworkMatchSnapshot publicState, byte[] ownPrivateState)
        {
            if (string.IsNullOrWhiteSpace(recipientPlayerId) || publicState == null) throw new ArgumentException("Recovery snapshot requires an authenticated recipient and public state.");
            RecipientPlayerId=recipientPlayerId; PublicState=publicState; this.ownPrivateState=ownPrivateState == null ? null : (byte[])ownPrivateState.Clone();
        }
    }

    public sealed class RecoveryEvent
    {
        public RecoveryEventType Type { get; }
        public string PlayerId { get; }
        public string MatchId { get; }
        public string RoundId { get; }
        public long Sequence { get; }
        public int Attempt { get; }
        public long DurationMilliseconds { get; }
        public string Reason { get; }
        internal RecoveryEvent(RecoveryEventType type, RecoveryContext context, long sequence=0, int attempt=0, string reason=null)
        { Type=type; PlayerId=context?.PlayerId ?? string.Empty; MatchId=context?.MatchId ?? string.Empty; RoundId=context?.LastKnownRoundId ?? string.Empty; Sequence=sequence; Attempt=attempt; DurationMilliseconds=type == RecoveryEventType.RecoveryCompleted || type == RecoveryEventType.RecoveryTimeout ? (long)Math.Max(0, (DateTimeOffset.UtcNow-context.DisconnectStartedAt).TotalMilliseconds) : 0; Reason=reason ?? string.Empty; }
    }

    /// <summary>Single-flight reconnect and snapshot-first rehydration. The supplied callbacks must use authenticated transport identity.</summary>
    public sealed class NetworkRecoveryCoordinator
    {
        private readonly object gate=new object();
        private readonly RecoveryConfig config;
        private readonly Func<Task<bool>> reconnect;
        private readonly Func<Task<RecoverySnapshot>> requestSnapshot;
        private readonly Func<RecoverySnapshot,Task<bool>> applyAtomically;
        private readonly Func<RecoverySnapshot,Task<bool>> restorePrivateState;
        private readonly Func<TimeSpan,CancellationToken,Task> delay;
        private Task<RecoveryResult> activeTask;
        public RecoveryContext Context { get; private set; }
        public RecoveryState State => Context?.State ?? RecoveryState.None;
        public bool GameplayInputBlocked => State != RecoveryState.None && State != RecoveryState.Recovered;
        public bool CanSendChat => !GameplayInputBlocked || config.InputPolicy != RecoveryInputPolicy.BlockAllInput;
        public RecoveryInputPolicy InputPolicy => config.InputPolicy;
        public event Action<RecoveryEvent> EventPublished;

        public NetworkRecoveryCoordinator(RecoveryConfig config, Func<Task<bool>> reconnect,
            Func<Task<RecoverySnapshot>> requestSnapshot, Func<RecoverySnapshot,Task<bool>> applyAtomically,
            Func<RecoverySnapshot,Task<bool>> restorePrivateState,
            Func<TimeSpan,CancellationToken,Task> delay=null)
        {
            this.config=(config ?? new RecoveryConfig()).Copy(); this.config.Validate();
            this.reconnect=reconnect ?? throw new ArgumentNullException(nameof(reconnect));
            this.requestSnapshot=requestSnapshot ?? throw new ArgumentNullException(nameof(requestSnapshot));
            this.applyAtomically=applyAtomically ?? throw new ArgumentNullException(nameof(applyAtomically));
            this.restorePrivateState=restorePrivateState ?? throw new ArgumentNullException(nameof(restorePrivateState));
            this.delay=delay ?? ((duration, token) => Task.Delay(duration, token));
        }

        public Task<RecoveryResult> RecoverAsync(string playerId, string matchId, string roundId, long lastSequence,
            DateTimeOffset disconnectedAt, CancellationToken cancellationToken=default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(matchId) || lastSequence < 0) throw new ArgumentException("Recovery identity and last sequence are required.");
            lock (gate)
            {
                if (activeTask != null && !activeTask.IsCompleted)
                {
                    if (Context == null || Context.PlayerId != playerId || Context.MatchId != matchId)
                        throw new InvalidOperationException("A recovery coordinator can run only one identity and match at a time.");
                    return activeTask;
                }
                Context=new RecoveryContext(playerId, matchId, roundId ?? string.Empty, lastSequence, disconnectedAt);
                Context.State=RecoveryState.Reconnecting;
                activeTask=RecoverCoreAsync(Context, cancellationToken);
                return activeTask;
            }
        }

        private async Task<RecoveryResult> RecoverCoreAsync(RecoveryContext context, CancellationToken token)
        {
            var previouslyKnownSequence=context.LastKnownSequence;
            Publish(RecoveryEventType.ConnectionLost, context); Publish(RecoveryEventType.RecoveryStarted, context);
            for (var attempt=1; attempt<=config.MaxAttempts; attempt++)
            {
                if (token.IsCancellationRequested)
                { context.State=RecoveryState.Failed; Publish(RecoveryEventType.RecoveryFailed, context, attempt:context.ReconnectAttempts, reason:"RecoveryCancelled"); return RecoveryResult.Failed; }
                if (DateTimeOffset.UtcNow - context.DisconnectStartedAt >= TimeSpan.FromSeconds(config.RecoveryWindowSeconds)) break;
                context.ReconnectAttempts=attempt; Publish(RecoveryEventType.ReconnectAttempted, context, attempt:attempt);
                bool connected=false;
                try { connected=await reconnect(); } catch { }
                if (!connected)
                {
                    if (attempt < config.MaxAttempts)
                    {
                        var delayMilliseconds=Math.Min(config.MaxRetryDelayMilliseconds, config.BaseRetryDelayMilliseconds * Math.Pow(2, attempt-1));
                        try { await delay(TimeSpan.FromMilliseconds(delayMilliseconds), token); }
                        catch (OperationCanceledException) { context.State=RecoveryState.Failed; Publish(RecoveryEventType.RecoveryFailed, context, attempt:attempt, reason:"RecoveryCancelled"); return RecoveryResult.Failed; }
                        catch { context.State=RecoveryState.Failed; Publish(RecoveryEventType.RecoveryFailed, context, attempt:attempt, reason:"RecoveryDelayFailed"); return RecoveryResult.Failed; }
                    }
                    continue;
                }
                Publish(RecoveryEventType.ConnectionRestored, context); context.State=RecoveryState.Synchronizing;
                Publish(RecoveryEventType.SnapshotRequested, context);
                RecoverySnapshot snapshot;
                try { snapshot=await requestSnapshot(); }
                catch { snapshot=null; }
                if (!ValidForRecipient(snapshot, context))
                {
                    Publish(RecoveryEventType.SnapshotFailed, context, attempt:attempt, reason:"InvalidSnapshotOrIdentity");
                    context.State=RecoveryState.Failed; Publish(RecoveryEventType.RecoveryFailed, context, attempt:attempt, reason:"InvalidSnapshotOrIdentity");
                    return RecoveryResult.Failed;
                }
                context.State=RecoveryState.Rehydrating;
                bool applied=false, privateRestored=false;
                var terminalMatch=snapshot.PublicState.Status == ThiefAndTheNecklace.PB1.MatchStatus.Completed
                    || snapshot.PublicState.Status == ThiefAndTheNecklace.PB1.MatchStatus.Aborted;
                try { applied=await applyAtomically(snapshot); if (applied) privateRestored=terminalMatch || await restorePrivateState(snapshot); } catch { }
                if (!applied || !privateRestored)
                {
                    context.State=RecoveryState.Failed; Publish(RecoveryEventType.RecoveryFailed, context, attempt:attempt, reason:!applied ? "StateRehydrationFailed" : "PrivateStateRestoreFailed");
                    return RecoveryResult.Failed;
                }
                context.LastKnownRoundId=snapshot.PublicState.RoundId; context.LastKnownSequence=snapshot.PublicState.Sequence;
                context.State=RecoveryState.Recovered;
                Publish(RecoveryEventType.SnapshotApplied, context, context.LastKnownSequence);
                Publish(RecoveryEventType.PlayerRehydrated, context, context.LastKnownSequence);
                Publish(RecoveryEventType.RecoveryCompleted, context, context.LastKnownSequence);
                if (snapshot.PublicState.Status == ThiefAndTheNecklace.PB1.MatchStatus.Aborted) return RecoveryResult.MatchAborted;
                if (snapshot.PublicState.Status == ThiefAndTheNecklace.PB1.MatchStatus.Completed) return RecoveryResult.MatchCompleted;
                return snapshot.PublicState.Sequence == previouslyKnownSequence ? RecoveryResult.AlreadyCurrent : RecoveryResult.Rehydrated;
            }
            context.State=RecoveryState.Failed; Publish(RecoveryEventType.RecoveryTimeout, context, attempt:context.ReconnectAttempts);
            Publish(RecoveryEventType.RecoveryFailed, context, attempt:context.ReconnectAttempts, reason:"ReconnectAttemptsExhausted");
            return RecoveryResult.Failed;
        }

        public void MarkRemoved(string reason)
        {
            if (Context == null) return; Context.State=RecoveryState.Removed;
            Publish(RecoveryEventType.PlayerRemoved, Context, reason:reason); Publish(RecoveryEventType.RecoveryFailed, Context, reason:reason);
        }

        public void ApplyAuthorityDecision(RecoveryDecision decision, string reason=null)
        {
            if (Context == null) return;
            switch (decision)
            {
                case RecoveryDecision.MatchActive:
                case RecoveryDecision.MatchCompleted:
                case RecoveryDecision.MatchAborted:
                    Context.State=RecoveryState.Synchronizing;
                    Publish(RecoveryEventType.SnapshotRequested, Context, reason:decision.ToString());
                    break;
                case RecoveryDecision.PlayerRemoved:
                case RecoveryDecision.RoomClosed: MarkRemoved(reason ?? decision.ToString()); break;
                case RecoveryDecision.ProtocolMismatch:
                case RecoveryDecision.Unknown:
                default:
                    Context.State=RecoveryState.Failed; Publish(RecoveryEventType.RecoveryFailed, Context, reason:reason ?? decision.ToString()); break;
            }
        }
        private static bool ValidForRecipient(RecoverySnapshot snapshot, RecoveryContext context)
        {
            if (snapshot == null || !string.Equals(snapshot.RecipientPlayerId, context.PlayerId, StringComparison.Ordinal)
                || !string.Equals(snapshot.PublicState.MatchId, context.MatchId, StringComparison.Ordinal)) return false;
            if (!NetworkSnapshotValidator.Validate(snapshot.PublicState).IsValid) return false;
            return snapshot.PublicState.Players.Any(player => player.PlayerId == context.PlayerId);
        }
        private void Publish(RecoveryEventType type, RecoveryContext context, long sequence=0, int attempt=0, string reason=null)
            => EventPublished?.Invoke(new RecoveryEvent(type, context, sequence, attempt, reason));
    }

    public sealed class AuthorityRecoveryEvent
    {
        public RecoveryEventType Type { get; }
        public string MatchId { get; }
        public string AuthorityPlayerId { get; }
        public long Sequence { get; }
        public string Reason { get; }
        internal AuthorityRecoveryEvent(RecoveryEventType type, string matchId, string authorityId, long sequence, string reason)
        { Type=type; MatchId=matchId ?? string.Empty; AuthorityPlayerId=authorityId ?? string.Empty; Sequence=sequence; Reason=reason ?? string.Empty; }
    }

    public sealed class AuthorityRecoveryCoordinator
    {
        private readonly IAuthoritativeStateStore store;
        private readonly Func<AuthoritativeStateCheckpoint,bool> validate;
        private readonly Func<AuthoritativeStateCheckpoint,bool> restore;
        private readonly object gate=new object();
        private readonly System.Collections.Generic.HashSet<string> recoveredMatches=new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        public AuthorityRecoveryState State { get; private set; } = AuthorityRecoveryState.Stable;
        public event Action<AuthorityRecoveryEvent> EventPublished;
        public AuthorityRecoveryCoordinator(IAuthoritativeStateStore store, Func<AuthoritativeStateCheckpoint,bool> validate, Func<AuthoritativeStateCheckpoint,bool> restore)
        { this.store=store ?? throw new ArgumentNullException(nameof(store)); this.validate=validate ?? throw new ArgumentNullException(nameof(validate)); this.restore=restore ?? throw new ArgumentNullException(nameof(restore)); }

        public bool Recover(string matchId, string newAuthorityPlayerId, long observedAuthoritySequence)
        {
            lock (gate)
            {
                if (string.IsNullOrWhiteSpace(matchId) || string.IsNullOrWhiteSpace(newAuthorityPlayerId) || observedAuthoritySequence < 0) return Fail(matchId, newAuthorityPlayerId, "InvalidHandoffRequest");
                if (recoveredMatches.Contains(matchId)) return true;
                State=AuthorityRecoveryState.AuthorityLost; Publish(RecoveryEventType.AuthorityLost, matchId, newAuthorityPlayerId, observedAuthoritySequence);
                State=AuthorityRecoveryState.NewAuthoritySelected; Publish(RecoveryEventType.AuthorityChanged, matchId, newAuthorityPlayerId, observedAuthoritySequence);
                State=AuthorityRecoveryState.StateRecovery; Publish(RecoveryEventType.StateRecoveryStarted, matchId, newAuthorityPlayerId, observedAuthoritySequence);
                AuthoritativeStateCheckpoint checkpoint;
                try { checkpoint=store.Read(matchId); } catch { return Fail(matchId,newAuthorityPlayerId,"CheckpointReadFailed"); }
                if (checkpoint == null || checkpoint.Sequence != observedAuthoritySequence) return Fail(matchId,newAuthorityPlayerId,"CommittedCheckpointUnavailableOrSequenceMismatch");
                Publish(RecoveryEventType.CommittedStateLoaded, matchId,newAuthorityPlayerId,checkpoint.Sequence);
                bool valid;
                try { valid=validate(checkpoint); } catch { valid=false; }
                if (!valid) return Fail(matchId,newAuthorityPlayerId,"CheckpointValidationFailed");
                Publish(RecoveryEventType.StateValidated,matchId,newAuthorityPlayerId,checkpoint.Sequence);
                bool restored;
                try { restored=restore(checkpoint); } catch { restored=false; }
                if (!restored) return Fail(matchId,newAuthorityPlayerId,"CheckpointRestoreFailed");
                recoveredMatches.Add(matchId); State=AuthorityRecoveryState.AuthorityRestored;
                Publish(RecoveryEventType.AuthorityStateRecovered,matchId,newAuthorityPlayerId,checkpoint.Sequence); return true;
            }
        }
        private bool Fail(string matchId,string playerId,string reason) { State=AuthorityRecoveryState.Failed; Publish(RecoveryEventType.RecoveryFailed,matchId,playerId,0,reason); return false; }
        private void Publish(RecoveryEventType type,string matchId,string playerId,long sequence,string reason=null)
            => EventPublished?.Invoke(new AuthorityRecoveryEvent(type,matchId,playerId,sequence,reason));
    }

    public sealed class StateIntegrityInfo
    {
        public long Sequence { get; }
        public string StateHash { get; }
        public StateIntegrityInfo(long sequence, string stateHash) { Sequence=sequence; StateHash=stateHash ?? string.Empty; }
    }

    public static class RecoveryDiagnostics
    {
        public static StateIntegrityInfo CreateIntegrityInfo(NetworkMatchSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var normalized=string.Join("|", new[] { snapshot.MatchId, snapshot.RoundId, snapshot.RoundIndex.ToString(), snapshot.TotalRounds.ToString(),
                snapshot.Phase.ToString(), snapshot.Status.ToString(), snapshot.Sequence.ToString() });
            using (var sha=SHA256.Create()) return new StateIntegrityInfo(snapshot.Sequence, BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(normalized))).Replace("-", string.Empty));
        }
    }
}
