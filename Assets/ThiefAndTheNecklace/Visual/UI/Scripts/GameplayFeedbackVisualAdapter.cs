using System;
using ThiefAndTheNecklace.UI;

namespace ThiefAndTheNecklace.Visual
{
    /// <summary>Routes semantic gameplay feedback into the existing PV visual-event stream.</summary>
    public sealed class GameplayFeedbackVisualAdapter : IDisposable
    {
        private readonly GameplayFeedbackBus source;

        public GameplayFeedbackVisualAdapter(GameplayFeedbackBus source)
        {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            source.Published += OnFeedback;
        }

        public void Dispose()
        {
            if (source != null) source.Published -= OnFeedback;
        }

        private static void OnFeedback(GameplayFeedbackEvent feedback)
        {
            switch (feedback)
            {
                case GameplayFeedbackEvent.ButtonPressed: VisualEventBus.Raise(VisualEventType.ButtonPressed); break;
                case GameplayFeedbackEvent.ActionPending: VisualEventBus.Raise(VisualEventType.ActionPending); break;
                case GameplayFeedbackEvent.ActionAccepted: VisualEventBus.Raise(VisualEventType.ActionAccepted); break;
                case GameplayFeedbackEvent.ActionRejected: VisualEventBus.Raise(VisualEventType.ActionRejected); break;
                case GameplayFeedbackEvent.Warning: VisualEventBus.Raise(VisualEventType.GameplayWarning); break;
                case GameplayFeedbackEvent.Error: VisualEventBus.Raise(VisualEventType.GameplayError); break;
                case GameplayFeedbackEvent.PhaseChanged: VisualEventBus.Raise(VisualEventType.GameplayPhaseChanged); break;
                case GameplayFeedbackEvent.VoteSubmitted: VisualEventBus.Raise(VisualEventType.VoteSubmitted); break;
                case GameplayFeedbackEvent.VoteResolved: VisualEventBus.Raise(VisualEventType.VoteResolved); break;
                case GameplayFeedbackEvent.RevealStarted: VisualEventBus.Raise(VisualEventType.RevealStarted); break;
                case GameplayFeedbackEvent.ScoreUpdated: VisualEventBus.Raise(VisualEventType.ScoreUpdated); break;
                case GameplayFeedbackEvent.RoundCompleted: VisualEventBus.Raise(VisualEventType.RoundEnd); break;
                case GameplayFeedbackEvent.MatchCompleted: VisualEventBus.Raise(VisualEventType.MatchVictory); break;
                case GameplayFeedbackEvent.ConnectionLost: VisualEventBus.Raise(VisualEventType.ConnectionLost); break;
                case GameplayFeedbackEvent.ConnectionRestored: VisualEventBus.Raise(VisualEventType.ConnectionRestored); break;
            }
        }
    }
}
