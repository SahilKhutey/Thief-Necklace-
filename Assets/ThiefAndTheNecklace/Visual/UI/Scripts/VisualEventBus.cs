using System;

namespace ThiefAndTheNecklace.Visual
{
    public enum VisualEventType
    {
        ScreenOpened, ScreenClosed, ScenarioShown, RoleShown, ChatOpened, ChatClosed, ChatMessageAdded,
        VoteOpened, PlayerSelected, VoteSubmitted, VoteLocked, RevealStarted, RoleRevealed, RevealCompleted,
        RoundResultShown, MatchResultShown, CardShown, CardHidden, CardSelected, CardDeselected, CardFlipped,
        PlayerDeselected, VoteSelected, MessageAdded, ScoreStarted, ScoreUpdated, ScoreCompleted,
        ToastShown, ModalOpened, ModalClosed, MatchStarted, RoundStarted, DiscussionStarted, DiscussionEnded,
        VoteTie, RevoteStarted, VoteResolved, TransitionStarted, TransitionCompleted, CardRevealStarted,
        CardRevealCompleted, VfxStarted, VfxCompleted, ScoreAnimationStarted, ScoreAnimationCompleted,
        PresentationError, PresentationFallback, TimerWarning, TimerCritical, PlayerSpeaking,
        ButtonPressed, ScoreTick, Victory, Escape,
        // PV5 canonical presentation contract. Existing PV0–PV4 event names remain valid.
        CardHover, CardSelect, CardFlip, CardReveal, PlayerFocus, PlayerAccused,
        VoteSelect, VoteLock, VoteResolve, NecklaceReveal, ScoreGain, ScoreComplete,
        RoundEnd, MatchVictory, ThiefEscape, VisualError,
        ActionPending, ActionAccepted, ActionRejected, ConnectionLost, ConnectionRestored,
        GameplayPhaseChanged, GameplayWarning, GameplayError
    }

    /// <summary>Presentation notifications only; gameplay events remain owned by the core runtime.</summary>
    public static class VisualEventBus
    {
        public static event Action<VisualEventType> EventRaised;

        public static void Raise(VisualEventType type)
        {
            EventRaised?.Invoke(type);
            VisualPresentationBus.Shared.Raise(type);
        }
    }
}
