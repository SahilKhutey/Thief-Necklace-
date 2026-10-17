using System;
using UnityEngine;

namespace ThiefAndTheNecklace.Visual
{
    /// <summary>Visual-only bridge for core phase signals, animation, and audio cues.</summary>
    public sealed class VisualPresentationBus
    {
        public static VisualPresentationBus Shared { get; } = new VisualPresentationBus();
        public event Action<VisualPresentationEvent> EventRaised;

        private VisualPresentationBus() { }

        public void Raise(VisualEventType type, object payload = null)
        {
            var item = new VisualPresentationEvent(type, payload);
            EventRaised?.Invoke(item);
            if (IsMilestone(type)) Debug.Log("[Visual] " + type);
        }

        private static bool IsMilestone(VisualEventType type)
        {
            switch (type)
            {
                case VisualEventType.ScreenOpened:
                case VisualEventType.ScreenClosed:
                case VisualEventType.TransitionStarted:
                case VisualEventType.TransitionCompleted:
                case VisualEventType.VoteTie:
                case VisualEventType.RevoteStarted:
                case VisualEventType.Victory:
                case VisualEventType.Escape:
                case VisualEventType.CardRevealStarted:
                case VisualEventType.CardRevealCompleted:
                case VisualEventType.VfxStarted:
                case VisualEventType.VfxCompleted:
                case VisualEventType.ScoreAnimationStarted:
                case VisualEventType.ScoreAnimationCompleted:
                case VisualEventType.PresentationError:
                case VisualEventType.PresentationFallback:
                    return true;
                default:
                    return false;
            }
        }
    }

    public readonly struct VisualPresentationEvent
    {
        public VisualEventType Type { get; }
        public object Payload { get; }
        public float Time { get; }

        public VisualPresentationEvent(VisualEventType type, object payload = null)
        {
            Type = type;
            Payload = payload;
            Time = UnityEngine.Time.realtimeSinceStartup;
        }
    }

    public enum PresentationState
    {
        Idle, Transitioning, Loading, Revealing, Discussing, Voting, Resolving, ShowingResults, Disabled
    }

    /// <summary>Guards cosmetic timelines so overlapping events cannot start duplicate cinematics.</summary>
    public sealed class PresentationStateMachine
    {
        public PresentationState State { get; private set; } = PresentationState.Idle;
        public bool IsTransitioning { get; private set; }
        public event Action<PresentationState, PresentationState> StateChanged;

        public bool TryBegin(PresentationState state)
        {
            if (IsTransitioning || State == PresentationState.Disabled) return false;
            IsTransitioning = true;
            SetState(state);
            return true;
        }

        public void Complete(PresentationState steadyState = PresentationState.Idle)
        {
            IsTransitioning = false;
            SetState(steadyState);
        }

        public void Recover(string context, PresentationState safeState = PresentationState.Idle)
        {
            VisualPresentationBus.Shared.Raise(VisualEventType.PresentationError, context);
            VisualPresentationBus.Shared.Raise(VisualEventType.PresentationFallback, context);
            IsTransitioning = false;
            SetState(safeState);
        }

        public void Disable() { IsTransitioning = false; SetState(PresentationState.Disabled); }

        private void SetState(PresentationState state)
        {
            if (State == state) return;
            var previous = State;
            State = state;
            StateChanged?.Invoke(previous, state);
        }
    }
}
