using System;
using System.Collections.Generic;
using ThiefAndTheNecklace;

namespace ThiefAndTheNecklace.Networking
{
    /// <summary>
    /// Authority-side round clock and phase owner. A transport adapter must publish SnapshotChanged
    /// and must invoke mutating methods only after validating an authenticated client command.
    /// </summary>
    public sealed class NetworkRoundController
    {
        public const int MatchRounds = 5;
        public const double DiscussionSeconds = 180;
        public const double VotingSeconds = 30;
        private readonly INetworkClock _clock;
        private readonly RoundManager _phaseMachine = new RoundManager();
        private int _roundNumber;
        private string _scenarioId;
        private double _phaseDeadline;
        private bool _matchStarted;

        public event Action<PublicRoundSnapshot> SnapshotChanged;
        public PublicRoundSnapshot Snapshot => new PublicRoundSnapshot(_roundNumber, _scenarioId, _phaseMachine.CurrentPhase, _phaseDeadline, _matchStarted);

        public NetworkRoundController(INetworkClock clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public bool TryStartMatch(NetworkPlayerRegistry players, string scenarioId)
        {
            if (players == null || !players.MatchStarted || _matchStarted || string.IsNullOrWhiteSpace(scenarioId)) return false;
            _matchStarted = true;
            _roundNumber = 1;
            _scenarioId = scenarioId;
            _phaseMachine.Reset();
            if (!_phaseMachine.TryEnterPhase(RoundPhase.Intro)) return false;
            Publish();
            return true;
        }

        public bool BeginPrivateReveal()
        {
            if (!_phaseMachine.TryEnterPhase(RoundPhase.PrivateReveal)) return false;
            _phaseDeadline = 0;
            Publish();
            return true;
        }

        public bool CompletePrivateReveal()
        {
            if (!_phaseMachine.TryEnterPhase(RoundPhase.Discussion)) return false;
            _phaseDeadline = _clock.Now + DiscussionSeconds;
            Publish();
            return true;
        }

        public bool BeginVoting()
        {
            if (_phaseMachine.CurrentPhase != RoundPhase.Discussion && _phaseMachine.CurrentPhase != RoundPhase.VoteResolution) return false;
            if (!_phaseMachine.TryEnterPhase(RoundPhase.Voting)) return false;
            _phaseDeadline = _clock.Now + VotingSeconds;
            Publish();
            return true;
        }

        public bool BeginVoteResolution()
        {
            if (!_phaseMachine.TryEnterPhase(RoundPhase.VoteResolution)) return false;
            _phaseDeadline = 0;
            Publish();
            return true;
        }

        public bool BeginReveal()
        {
            if (!_phaseMachine.TryEnterPhase(RoundPhase.Reveal)) return false;
            _phaseDeadline = 0;
            Publish();
            return true;
        }

        public bool BeginScoring()
        {
            if (!_phaseMachine.TryEnterPhase(RoundPhase.Scoring)) return false;
            Publish();
            return true;
        }

        public bool CompleteRound(bool matchComplete)
        {
            if (!_phaseMachine.TryEnterPhase(RoundPhase.RoundEnd)) return false;
            if (matchComplete || _roundNumber >= MatchRounds)
            {
                if (!_phaseMachine.TryEnterPhase(RoundPhase.MatchEnd)) return false;
                _matchStarted = false;
            }
            Publish();
            return true;
        }

        public bool StartNextRound(string scenarioId)
        {
            if (!_matchStarted || _phaseMachine.CurrentPhase != RoundPhase.RoundEnd || _roundNumber >= MatchRounds || string.IsNullOrWhiteSpace(scenarioId)) return false;
            _roundNumber++;
            _scenarioId = scenarioId;
            if (!_phaseMachine.TryEnterPhase(RoundPhase.Intro)) return false;
            Publish();
            return true;
        }

        public void Tick()
        {
            if (_phaseDeadline <= 0 || _clock.Now < _phaseDeadline) return;
            if (_phaseMachine.CurrentPhase == RoundPhase.Discussion) BeginVoting();
            else if (_phaseMachine.CurrentPhase == RoundPhase.Voting) BeginVoteResolution();
        }

        private void Publish()
        {
            var handler = SnapshotChanged;
            if (handler != null) handler(Snapshot);
        }
    }
}
