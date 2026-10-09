using System;

namespace ThiefAndTheNecklace
{
    public sealed class RoundManager
    {
        public RoundPhase CurrentPhase { get; private set; }
        public event Action<RoundPhase> PhaseChanged;

        public bool TryEnterPhase(RoundPhase next)
        {
            if (!IsLegal(CurrentPhase, next)) return false;
            CurrentPhase = next;
            var handler = PhaseChanged;
            if (handler != null) handler(next);
            return true;
        }

        public void Reset() => CurrentPhase = RoundPhase.None;

        private static bool IsLegal(RoundPhase current, RoundPhase next)
        {
            switch (current)
            {
                case RoundPhase.None: return next == RoundPhase.Intro;
                case RoundPhase.Intro: return next == RoundPhase.PrivateReveal;
                case RoundPhase.PrivateReveal: return next == RoundPhase.Discussion;
                case RoundPhase.Discussion: return next == RoundPhase.Voting;
                case RoundPhase.Voting: return next == RoundPhase.VoteResolution;
                case RoundPhase.VoteResolution: return next == RoundPhase.Voting || next == RoundPhase.Reveal;
                case RoundPhase.Reveal: return next == RoundPhase.Scoring;
                case RoundPhase.Scoring: return next == RoundPhase.RoundEnd;
                case RoundPhase.RoundEnd: return next == RoundPhase.Intro || next == RoundPhase.MatchEnd;
                default: return false;
            }
        }
    }
}
