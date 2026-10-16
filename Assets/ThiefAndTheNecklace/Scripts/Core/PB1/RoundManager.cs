using System;
using System.Collections.Generic;
using System.Linq;

namespace ThiefAndTheNecklace.PB1
{
    public sealed class RoundManager
    {
        public RoundPhase Phase { get; private set; } = RoundPhase.Waiting;
        public event Action<RoundPhase> PhaseChanged;

        public void TransitionTo(RoundPhase next)
        {
            if (!CanTransition(Phase, next)) throw new InvalidOperationException("Invalid transition: " + Phase + " -> " + next);
            Phase = next;
            PhaseChanged?.Invoke(next);
        }

        private static bool CanTransition(RoundPhase current, RoundPhase next)
        {
            switch (current)
            {
                case RoundPhase.Waiting: return next == RoundPhase.Starting;
                case RoundPhase.Starting: return next == RoundPhase.PrivateReveal;
                case RoundPhase.PrivateReveal: return next == RoundPhase.Discussion;
                case RoundPhase.Discussion: return next == RoundPhase.Voting;
                case RoundPhase.Voting: return next == RoundPhase.VoteResolution;
                case RoundPhase.VoteResolution: return next == RoundPhase.Reveal || next == RoundPhase.Voting;
                case RoundPhase.Reveal: return next == RoundPhase.Scoring;
                case RoundPhase.Scoring: return next == RoundPhase.RoundEnd;
                case RoundPhase.RoundEnd: return next == RoundPhase.NextRound || next == RoundPhase.MatchEnd;
                case RoundPhase.NextRound: return next == RoundPhase.Starting;
                default: return false;
            }
        }
    }
}
