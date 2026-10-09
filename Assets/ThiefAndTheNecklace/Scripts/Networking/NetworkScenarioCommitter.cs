using System;
using System.Collections.Generic;
using System.Linq;
using ThiefAndTheNecklace;

namespace ThiefAndTheNecklace.Networking
{
    /// <summary>Authority-only adapter between generated truth and PB2's public/private delivery boundary.</summary>
    public sealed class NetworkScenarioCommitter
    {
        private readonly PrivateStateDispatcher _privateState = new PrivateStateDispatcher();
        private RuntimeScenario _authorityScenario;
        private PublicScenarioState _publicScenario;

        public PublicScenarioState PublicScenario => _publicScenario;
        public bool HasCommittedRound => _authorityScenario != null;

        /// <summary>Authority-only replay key. Never replicate this descriptor; its hash includes private round truth.</summary>
        public ScenarioReplayDescriptor GetAuthorityReplayDescriptor(int roundNumber)
        {
            if (_authorityScenario == null) throw new InvalidOperationException("No round has been committed.");
            return ScenarioReplayDescriptor.From(_authorityScenario, roundNumber);
        }

        public PublicScenarioState Commit(IRoundSource source, IReadOnlyList<PlayerData> players, int roundNumber,
            int seed, ScenarioDifficulty difficulty = ScenarioDifficulty.Normal)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (players == null) throw new ArgumentNullException(nameof(players));
            if (roundNumber <= 0) throw new ArgumentOutOfRangeException(nameof(roundNumber));
            _privateState.Clear();
            _authorityScenario = null;
            _publicScenario = null;
            var generated = source.Create(players, seed, difficulty);
            var secrets = generated.playerSecrets.Select(secret => new PrivateRoundState(
                secret.playerId,
                secret.roleId,
                secret.isThief,
                secret.privateSituation,
                generated.clues.Where(clue => clue.ownerPlayerId == secret.playerId).Select(clue => clue.text).ToList())).ToList();
            _privateState.ReplaceRound(secrets);
            _authorityScenario = generated;
            _publicScenario = new PublicScenarioState(roundNumber,
                generated.scenarioId, generated.scenarioVersion, generated.title, generated.publicDescription, generated.targetObjectId, generated.publicScenarioHash);
            return _publicScenario;
        }

        public bool TryGetPrivateState(string authenticatedPlayerId, out PrivateRoundState state)
        {
            if (_authorityScenario == null || _privateState.IsRevealed) { state = null; return false; }
            return _privateState.TryGetForRecipient(authenticatedPlayerId, out state);
        }

        /// <summary>Invoke only from the authoritative reveal transition, after voting is closed.</summary>
        public PublicRevealState Reveal(string accusedPlayerId, RoundPhase authorityPhase)
        {
            if (_authorityScenario == null) throw new InvalidOperationException("No round has been committed.");
            if (authorityPhase != RoundPhase.Reveal) throw new InvalidOperationException("Private roles stay hidden until the authority enters Reveal.");
            var roles = _privateState.RevealRoles();
            return new PublicRevealState(accusedPlayerId, _authorityScenario.thiefPlayerId, roles,
                !string.IsNullOrEmpty(accusedPlayerId) && accusedPlayerId == _authorityScenario.thiefPlayerId);
        }

        public void ClearRound()
        {
            _authorityScenario = null;
            _publicScenario = null;
            _privateState.Clear();
        }
    }
}
