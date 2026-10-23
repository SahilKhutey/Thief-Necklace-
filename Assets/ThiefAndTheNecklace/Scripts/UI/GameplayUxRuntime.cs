using System;
using System.Collections.Generic;
using System.Linq;
using ThiefAndTheNecklace.PB1;

namespace ThiefAndTheNecklace.UI
{
    public enum GameplayUxState
    {
        None, Loading, Waiting, PrivateInformation, Discussion, Voting, VoteSubmitted,
        Resolving, Reveal, RoundResults, MatchResults, Reconnecting, Error, Disabled
    }

    public enum GameplayInputAction
    {
        Ready, Unready, StartMatch, SendChat, SelectPlayer, SubmitVote, CancelVoteSelection,
        Continue, Rematch, ExitMatch, OpenSettings, OpenHowToPlay, CloseOverlay,
        AcknowledgePrivateInformation, CallVote, RevealPrivateInformation
    }

    public enum InputLockScope { Gameplay, Chat, All }
    public enum GameplayTimerState { Normal, Warning, Critical, Expired }
    public enum GameplayFeedbackEvent
    {
        ButtonPressed, ActionPending, ActionAccepted, ActionRejected, Warning, Error,
        PhaseChanged, VoteSubmitted, VoteResolved, RevealStarted, ScoreUpdated,
        RoundCompleted, MatchCompleted, ConnectionLost, ConnectionRestored
    }

    public interface IInputLockService
    {
        bool IsLocked { get; }
        void Lock(string reason, InputLockScope scope = InputLockScope.All);
        void Unlock(string reason);
        bool CanDispatch(GameplayInputAction action);
    }

    /// <summary>Named, stacked input locks. Releasing one system's lock cannot release another's.</summary>
    public sealed class InputLockService : IInputLockService
    {
        private readonly object gate = new object();
        private readonly Dictionary<string, InputLockScope> locks = new Dictionary<string, InputLockScope>(StringComparer.Ordinal);
        public bool IsLocked { get { lock (gate) return locks.Count != 0; } }
        public IReadOnlyDictionary<string, InputLockScope> ActiveLocks
        {
            get { lock (gate) return new Dictionary<string, InputLockScope>(locks); }
        }

        public void Lock(string reason, InputLockScope scope = InputLockScope.All)
        {
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("An input lock needs a stable reason key.", nameof(reason));
            lock (gate) locks[reason] = scope;
        }

        public void Unlock(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason)) return;
            lock (gate) locks.Remove(reason);
        }

        public bool CanDispatch(GameplayInputAction action)
        {
            lock (gate)
            {
                foreach (var scope in locks.Values)
                {
                    if (scope == InputLockScope.All) return false;
                    if (scope == InputLockScope.Gameplay && IsGameplayAction(action)) return false;
                    if (scope == InputLockScope.Chat && action == GameplayInputAction.SendChat) return false;
                }
                return true;
            }
        }

        private static bool IsGameplayAction(GameplayInputAction action)
            => action != GameplayInputAction.SendChat && action != GameplayInputAction.OpenSettings
                && action != GameplayInputAction.OpenHowToPlay && action != GameplayInputAction.CloseOverlay;
    }

    public interface IGameplayFeedbackBus
    {
        void Publish(GameplayFeedbackEvent evt);
    }

    public sealed class GameplayFeedbackBus : IGameplayFeedbackBus
    {
        public event Action<GameplayFeedbackEvent> Published;
        public void Publish(GameplayFeedbackEvent evt) => Published?.Invoke(evt);
    }

    public sealed class PlayerUxModel
    {
        public string PlayerId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsConnected { get; set; }
        public bool IsReady { get; set; }
        public bool IsSelectableVoteTarget { get; set; }
        public bool IsVoteSelected { get; set; }
        public bool IsVoteSubmitted { get; set; }
    }

    public sealed class GameplayUxInput
    {
        public RoundPhase Phase { get; set; }
        public string LocalPlayerId { get; set; } = string.Empty;
        public IReadOnlyList<PlayerUxModel> Players { get; set; } = Array.Empty<PlayerUxModel>();
        public string SelectedVoteTargetId { get; set; }
        public bool VoteSubmitted { get; set; }
        public bool CanChat { get; set; }
        public bool CanReady { get; set; }
        public bool CanStartMatch { get; set; }
        public bool CanCallVote { get; set; }
        public bool CanContinue { get; set; }
        public bool CanRematch { get; set; }
        public bool IsRecovering { get; set; }
        public bool AllowChatDuringRecovery { get; set; }
        public bool IsLoading { get; set; }
        public float RemainingSeconds { get; set; }
        public float WarningSeconds { get; set; } = 20f;
        public float CriticalSeconds { get; set; } = 5f;
    }

    public sealed class GameplayUxModel
    {
        public RoundPhase Phase { get; internal set; }
        public GameplayUxState State { get; internal set; }
        public bool CanChat { get; internal set; }
        public bool CanReady { get; internal set; }
        public bool CanStartMatch { get; internal set; }
        public bool CanCallVote { get; internal set; }
        public bool CanVote { get; internal set; }
        public bool VoteSubmitted { get; internal set; }
        public bool CanSubmitVote { get; internal set; }
        public bool CanContinue { get; internal set; }
        public bool CanRematch { get; internal set; }
        public bool IsGameplayInputLocked { get; internal set; }
        public float RemainingSeconds { get; internal set; }
        public string TimerText { get; internal set; } = "00:00";
        public string TimerAnnouncement { get; internal set; } = "0 seconds remaining";
        public GameplayTimerState TimerState { get; internal set; }
        public IReadOnlyList<PlayerUxModel> Players { get; internal set; } = Array.Empty<PlayerUxModel>();
    }

    /// <summary>Maps an authoritative client projection into UI capabilities without changing gameplay state.</summary>
    public sealed class GameplayUxMapper
    {
        public GameplayUxModel Map(GameplayUxInput input, IInputLockService inputLocks = null)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            var seconds = Math.Max(0, input.RemainingSeconds);
            var wholeSeconds = (int)Math.Ceiling(seconds);
            var recovering = input.IsRecovering;
            var submitted = input.VoteSubmitted;
            var mappedPlayers = (input.Players ?? Array.Empty<PlayerUxModel>()).Where(player => player != null).Select(player => new PlayerUxModel
            {
                PlayerId = player.PlayerId,
                DisplayName = player.DisplayName,
                IsConnected = player.IsConnected,
                IsReady = player.IsReady,
                IsSelectableVoteTarget = player.IsSelectableVoteTarget && player.IsConnected
                    && !string.Equals(player.PlayerId, input.LocalPlayerId, StringComparison.Ordinal),
                IsVoteSelected = !string.IsNullOrEmpty(input.SelectedVoteTargetId)
                    && string.Equals(player.PlayerId, input.SelectedVoteTargetId, StringComparison.Ordinal),
                IsVoteSubmitted = player.IsVoteSubmitted
                    || (submitted && string.Equals(player.PlayerId, input.LocalPlayerId, StringComparison.Ordinal))
            }).ToArray();
            var canVote = input.Phase == RoundPhase.Voting && !recovering;
            var selected = mappedPlayers.FirstOrDefault(player =>
                string.Equals(player.PlayerId, input.SelectedVoteTargetId, StringComparison.Ordinal));
            var hasEligibleTarget = !string.IsNullOrWhiteSpace(input.SelectedVoteTargetId) && selected != null && selected.IsConnected
                && selected.IsSelectableVoteTarget
                && !string.Equals(selected.PlayerId, input.LocalPlayerId, StringComparison.Ordinal);
            var canSubmit = canVote && hasEligibleTarget && !submitted;
            if (inputLocks != null)
            {
                canVote &= inputLocks.CanDispatch(GameplayInputAction.SelectPlayer);
                canSubmit &= inputLocks.CanDispatch(GameplayInputAction.SubmitVote);
            }
            var phaseState = MapPhase(input.Phase);
            var state = recovering ? GameplayUxState.Reconnecting
                : input.IsLoading ? GameplayUxState.Loading
                : submitted && input.Phase == RoundPhase.Voting ? GameplayUxState.VoteSubmitted
                : phaseState;
            var timerState = seconds <= 0 ? GameplayTimerState.Expired
                : seconds <= Math.Max(0, input.CriticalSeconds) ? GameplayTimerState.Critical
                : seconds <= Math.Max(input.CriticalSeconds, input.WarningSeconds) ? GameplayTimerState.Warning
                : GameplayTimerState.Normal;
            return new GameplayUxModel
            {
                Phase = input.Phase, State = state,
                CanChat = input.CanChat && (!recovering || input.AllowChatDuringRecovery)
                    && (inputLocks == null || inputLocks.CanDispatch(GameplayInputAction.SendChat)),
                CanReady = input.CanReady && !recovering
                    && (inputLocks == null || inputLocks.CanDispatch(GameplayInputAction.Ready)),
                CanStartMatch = input.CanStartMatch && !recovering
                    && (inputLocks == null || inputLocks.CanDispatch(GameplayInputAction.StartMatch)),
                CanCallVote = input.CanCallVote && input.Phase == RoundPhase.Discussion && !recovering
                    && (inputLocks == null || inputLocks.CanDispatch(GameplayInputAction.CallVote)),
                CanVote = canVote, VoteSubmitted = submitted, CanSubmitVote = canSubmit,
                CanContinue = input.CanContinue && input.Phase == RoundPhase.Results && !recovering
                    && (inputLocks == null || inputLocks.CanDispatch(GameplayInputAction.Continue)),
                CanRematch = input.CanRematch && input.Phase == RoundPhase.MatchEnd && !recovering
                    && (inputLocks == null || inputLocks.CanDispatch(GameplayInputAction.Rematch)),
                IsGameplayInputLocked = recovering || (inputLocks != null && !inputLocks.CanDispatch(GameplayInputAction.SubmitVote)),
                RemainingSeconds = seconds,
                TimerText = string.Format("{0:00}:{1:00}", wholeSeconds / 60, wholeSeconds % 60),
                TimerAnnouncement = wholeSeconds == 1 ? "1 second remaining" : wholeSeconds + " seconds remaining",
                TimerState = timerState,
                Players = mappedPlayers
            };
        }

        private static GameplayUxState MapPhase(RoundPhase phase)
        {
            switch (phase)
            {
                case RoundPhase.Waiting: return GameplayUxState.Waiting;
                case RoundPhase.PrivateReveal: return GameplayUxState.PrivateInformation;
                case RoundPhase.Discussion: return GameplayUxState.Discussion;
                case RoundPhase.Voting: return GameplayUxState.Voting;
                case RoundPhase.VoteResolution: return GameplayUxState.Resolving;
                case RoundPhase.Reveal: return GameplayUxState.Reveal;
                case RoundPhase.Results: case RoundPhase.RoundEnd: case RoundPhase.NextRound: return GameplayUxState.RoundResults;
                case RoundPhase.MatchEnd: return GameplayUxState.MatchResults;
                default: return GameplayUxState.Disabled;
            }
        }
    }

    /// <summary>Two-layer local guard; authority still validates every dispatched command.</summary>
    public sealed class GameplayActionGuard
    {
        public bool CanDispatch(GameplayInputAction action, GameplayUxInput state, IInputLockService locks = null)
        {
            if (state == null || (state.IsRecovering && (action != GameplayInputAction.SendChat || !state.AllowChatDuringRecovery))
                || (locks != null && !locks.CanDispatch(action))) return false;
            switch (action)
            {
                case GameplayInputAction.SelectPlayer:
                    return state.Phase == RoundPhase.Voting && !state.VoteSubmitted;
                case GameplayInputAction.SubmitVote:
                    if (state.Phase != RoundPhase.Voting || state.VoteSubmitted) return false;
                    return state.Players != null && state.Players.Any(player => player != null && player.IsConnected
                        && player.IsSelectableVoteTarget && player.PlayerId != state.LocalPlayerId
                        && player.PlayerId == state.SelectedVoteTargetId);
                case GameplayInputAction.SendChat: return state.CanChat;
                case GameplayInputAction.Ready: return state.CanReady;
                case GameplayInputAction.StartMatch: return state.CanStartMatch;
                case GameplayInputAction.CallVote: return state.CanCallVote && state.Phase == RoundPhase.Discussion;
                case GameplayInputAction.Continue: return state.CanContinue && state.Phase == RoundPhase.Results;
                case GameplayInputAction.Rematch: return state.CanRematch && state.Phase == RoundPhase.MatchEnd;
                case GameplayInputAction.AcknowledgePrivateInformation:
                case GameplayInputAction.RevealPrivateInformation: return state.Phase == RoundPhase.PrivateReveal;
                default: return true;
            }
        }
    }

    public enum ActionFeedbackState { Idle, Pending, Accepted, Rejected }

    public sealed class ActionFeedback
    {
        public ActionFeedbackState State { get; }
        public string PlayerMessage { get; }
        public string TechnicalCode { get; }
        public ActionFeedback(ActionFeedbackState state, string playerMessage, string technicalCode = null)
        { State = state; PlayerMessage = playerMessage ?? string.Empty; TechnicalCode = technicalCode ?? string.Empty; }
    }

    public interface IScreenErrorHandler
    {
        void ShowRecoverableError(string message);
        void ShowBlockingError(string message);
    }

    public interface IGameplayCommandSender
    {
        /// <summary>The GW10 adapter sends a command and returns its authoritative acknowledgement.</summary>
        void Send(GameplayInputAction action, string value, Action<ActionFeedback> completed);
    }

    /// <summary>Validates local intent, blocks duplicate pending mutations, and reports the authority's result.</summary>
    public sealed class GameplayActionDispatcher
    {
        private readonly object gate = new object();
        private readonly HashSet<GameplayInputAction> pending = new HashSet<GameplayInputAction>();
        private readonly GameplayActionGuard guard;
        private readonly IGameplayCommandSender sender;
        private readonly IGameplayFeedbackBus feedback;

        public GameplayActionDispatcher(IGameplayCommandSender sender, IGameplayFeedbackBus feedback = null, GameplayActionGuard guard = null)
        {
            this.sender = sender ?? throw new ArgumentNullException(nameof(sender));
            this.feedback = feedback;
            this.guard = guard ?? new GameplayActionGuard();
        }

        public bool IsPending(GameplayInputAction action) { lock (gate) return pending.Contains(action); }

        public bool Dispatch(GameplayInputAction action, string value, GameplayUxInput state,
            IInputLockService locks, Action<ActionFeedback> completed = null)
        {
            if (!guard.CanDispatch(action, state, locks))
            {
                var rejected = new ActionFeedback(ActionFeedbackState.Rejected, "This action is no longer available.", "NotReady");
                Publish(GameplayFeedbackEvent.ActionRejected);
                Complete(completed, rejected);
                return false;
            }
            bool added;
            lock (gate)
                added = pending.Add(action);
            if (!added)
            {
                var duplicate = new ActionFeedback(ActionFeedbackState.Rejected, "Your request is already being processed.", "ActionPending");
                Publish(GameplayFeedbackEvent.ActionRejected);
                Complete(completed, duplicate);
                return false;
            }
            Publish(GameplayFeedbackEvent.ActionPending);
            try
            {
                sender.Send(action, value, result =>
                {
                    lock (gate) if (!pending.Remove(action)) return;
                    var acknowledged = result ?? new ActionFeedback(ActionFeedbackState.Rejected,
                        "Unable to complete that action. Please try again.", "Unknown");
                    Publish(acknowledged.State == ActionFeedbackState.Accepted
                        ? GameplayFeedbackEvent.ActionAccepted : GameplayFeedbackEvent.ActionRejected);
                    Complete(completed, acknowledged);
                });
            }
            catch
            {
                lock (gate) if (!pending.Remove(action)) return false;
                var failed = new ActionFeedback(ActionFeedbackState.Rejected, "Connection lost. Your action was not confirmed.", "ConnectionLost");
                Publish(GameplayFeedbackEvent.ActionRejected);
                Complete(completed, failed);
                return false;
            }
            return true;
        }

        private void Publish(GameplayFeedbackEvent evt)
        {
            try { feedback?.Publish(evt); } catch { }
        }

        private static void Complete(Action<ActionFeedback> callback, ActionFeedback result)
        {
            try { callback?.Invoke(result); } catch { }
        }
    }

    public static class GameplayErrorCopy
    {
        public static string ForCode(string code)
        {
            switch (code ?? string.Empty)
            {
                case "VoteExpired": return "Voting has ended.";
                case "AlreadyVoted": return "Your vote was already submitted.";
                case "InvalidTarget": return "Choose an available player.";
                case "ConnectionLost": return "Connection lost. Your action was not confirmed.";
                case "MatchChanged": return "This match is no longer available.";
                case "NotReady": return "This action is no longer available.";
                default: return "Unable to complete that action. Please try again.";
            }
        }
    }
}
