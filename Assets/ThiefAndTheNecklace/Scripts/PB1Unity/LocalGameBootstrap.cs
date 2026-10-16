using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ThiefAndTheNecklace.PB1;

namespace ThiefAndTheNecklace.PB1.Unity
{
    public sealed class LocalGameBootstrap : MonoBehaviour
    {
        [SerializeField] private ScenarioDefinition scenario;
        [SerializeField] private GameTimer gameTimer;
        [SerializeField] private RevealController revealController;
        [SerializeField, Min(1)] private int totalRounds = 5;
        [SerializeField] private int matchSeed = 10001;
        [SerializeField] private List<string> playerNames = new List<string> { "Player 1", "Player 2", "Player 3", "Player 4" };

        public GameController Controller { get; private set; }

        private void Start()
        {
            if (Controller == null) StartLocalMatch(playerNames);
        }

        private void Update()
        {
            if (gameTimer == null && Controller != null && Controller.Phase == RoundPhase.Discussion)
                Controller.AdvanceTimer(Time.deltaTime);
        }

        private void OnDestroy()
        {
            if (Controller != null) Controller.PhaseChanged -= OnPhaseChanged;
            if (gameTimer != null) gameTimer.Completed -= OnDiscussionTimerCompleted;
            if (revealController != null)
            {
                revealController.RoleRevealed -= OnRoleRevealed;
                revealController.RevealFinished -= OnRevealFinished;
            }
        }

        private void OnPhaseChanged(RoundPhase phase)
        {
            if (gameTimer != null)
            {
                if (phase == RoundPhase.Discussion) gameTimer.StartTimer(180f);
                else gameTimer.StopTimer();
            }
            if (phase != RoundPhase.Reveal) return;
            if (revealController == null)
            {
                Controller.NotifyRoleRevealed();
                Controller.CalculateScores();
                return;
            }
            var roles = Controller.GetRevealedPlayers();
            var accusedId = Controller.LastOutcome.AccusedPlayerId ?? roles.First(pair => pair.Value.IsThief).Key;
            revealController.BeginReveal(accusedId, roles[accusedId].Role, Controller.LastOutcome.Result == VoteResolution.ThiefCaught);
        }

        private void OnDiscussionTimerCompleted()
        {
            if (Controller != null && Controller.Phase == RoundPhase.Discussion) Controller.BeginVoting();
        }

        private void OnRevealFinished()
        {
            if (Controller != null && Controller.Phase == RoundPhase.Reveal) Controller.CalculateScores();
        }

        private void OnRoleRevealed(RoleType role, bool thiefCaught)
        {
            if (Controller != null && Controller.Phase == RoundPhase.Reveal) Controller.NotifyRoleRevealed();
        }

        public void StartDevelopmentMatch(int playerCount)
        {
            if (playerCount < 4 || playerCount > 6) throw new System.ArgumentOutOfRangeException(nameof(playerCount));
            var names = new List<string>();
            for (var i = 1; i <= playerCount; i++) names.Add("Player " + i);
            StartLocalMatch(names);
        }

        private void StartLocalMatch(IReadOnlyList<string> names)
        {
            if (Controller != null) throw new System.InvalidOperationException("This bootstrap already owns a match controller.");
            var runtimeScenario = ResolveScenario();
            Controller = new GameController(runtimeScenario, totalRounds, matchSeed);
            Controller.PhaseChanged += OnPhaseChanged;
            if (gameTimer != null) gameTimer.Completed += OnDiscussionTimerCompleted;
            if (revealController != null)
            {
                revealController.RoleRevealed += OnRoleRevealed;
                revealController.RevealFinished += OnRevealFinished;
            }
            Controller.StartLocalMatch(names);
        }

        private ScenarioDefinitionData ResolveScenario()
        {
            var asset = scenario != null ? scenario : Resources.Load<ScenarioDefinition>("PB1/RoyalNecklace");
            return asset == null ? DefaultScenarioFactory.CreateRoyalNecklace() : asset.ToRuntime();
        }
    }
}
