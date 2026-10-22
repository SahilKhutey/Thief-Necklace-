using System.Collections;
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
        [SerializeField, Min(30)] private int discussionDurationSeconds = 180;
        [SerializeField, Min(1)] private int votingDurationSeconds = 60;
        [SerializeField] private bool allowEarlyVote = true;
        [SerializeField, Min(1)] private int earlyVoteConfirmationsRequired = 4;
        [SerializeField] private RoleRevealPolicy roleRevealPolicy = RoleRevealPolicy.AllPlayers;
        [SerializeField] private bool revealNecklace = true;
        [SerializeField] private bool revealActualThiefOnWrongVote = true;
        [SerializeField] private bool revealActualThiefOnEscape = true;
        [SerializeField] private bool requireRevealPresentationAck;
        [SerializeField, Min(1f)] private float revealPresentationFallbackSeconds = 4f;
        [SerializeField, Min(0f)] private float resultsMinimumDisplaySeconds = 4f;
        [SerializeField, Min(1f)] private float resultsMaximumDisplaySeconds = 30f;
        [SerializeField] private bool allowEarlyResultsContinue = true;
        [SerializeField] private List<string> playerNames = new List<string> { "Player 1", "Player 2", "Player 3", "Player 4" };

        public GameController Controller { get; private set; }
        public GameTimer Timer { get { return gameTimer; } }
        public bool HasRevealController { get { return revealController != null; } }

        private void Start()
        {
            if (Controller == null) StartLocalMatch(playerNames);
        }

        private void Update()
        {
            if (Controller == null) return;
            if (Controller.Phase == RoundPhase.Voting || Controller.Phase == RoundPhase.Results) Controller.AdvanceTimer(Time.deltaTime);
            else if (gameTimer == null && Controller.Phase == RoundPhase.Discussion) Controller.AdvanceTimer(Time.deltaTime);
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
                if (phase == RoundPhase.Discussion) gameTimer.StartTimer(Mathf.Max(30, discussionDurationSeconds));
                else gameTimer.StopTimer();
            }
            if (phase != RoundPhase.Reveal) return;
            StartCoroutine(RevealPresentationFallback(Controller.CurrentRound));
            if (revealController == null)
            {
                Controller.NotifyRoleRevealed();
                StartCoroutine(CompleteFallbackReveal());
                return;
            }
            var reveal=Controller.LastRevealResult;
            if (reveal == null) { revealController.BeginOutcomeOnly(); return; }
            var roles = Controller.GetRevealedPlayers();
            if (reveal.Reason == RevealReason.ThiefEscaped)
            {
                var thiefId=reveal.PublicThiefPlayerId ?? reveal.ActualThiefPlayerId;
                if (roles.TryGetValue(thiefId, out var thiefRole)) revealController.BeginEscapeReveal(thiefRole.Role);
                else revealController.BeginOutcomeOnly();
                return;
            }
            if (reveal.SelectedTargetId.HasValue && roles.TryGetValue(reveal.SelectedTargetId.Value, out var selectedRole))
                revealController.BeginReveal(reveal.SelectedTargetId.Value, selectedRole.Role, reveal.Outcome == RevealOutcome.ThiefCaught);
            else revealController.BeginOutcomeOnly();
        }

        private IEnumerator CompleteFallbackReveal()
        {
            yield return null;
            if (Controller != null && Controller.Phase == RoundPhase.Reveal)
            {
                Controller.CompleteReveal();
                Controller.CalculateScores();
            }
        }

        private IEnumerator RevealPresentationFallback(int roundNumber)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(1f, revealPresentationFallbackSeconds));
            if (Controller != null && Controller.CurrentRound == roundNumber && Controller.Phase == RoundPhase.Reveal)
            {
                Controller.CompleteReveal();
                Controller.CalculateScores();
            }
        }

        private void OnDiscussionTimerCompleted()
        {
            if (Controller != null && Controller.Phase == RoundPhase.Discussion) Controller.BeginVoting();
        }

        private void OnRevealFinished()
        {
            if (Controller != null && Controller.Phase == RoundPhase.Reveal)
            {
                Controller.CompleteReveal();
                Controller.CalculateScores();
            }
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
            Controller = new GameController(runtimeScenario, totalRounds, matchSeed, new DiscussionRateConfig
            {
                DurationSeconds=Mathf.Max(30, discussionDurationSeconds),
                VotingDurationSeconds=Mathf.Max(1, votingDurationSeconds),
                AllowEarlyVote=allowEarlyVote,
                EarlyVoteConfirmationsRequired=allowEarlyVote ? Mathf.Max(1, earlyVoteConfirmationsRequired) : 0
            }, revealConfig: new RevealConfig
            {
                RoleRevealPolicy=roleRevealPolicy,
                RevealNecklace=revealNecklace,
                RevealActualThiefOnWrongVote=revealActualThiefOnWrongVote,
                RevealActualThiefOnEscape=revealActualThiefOnEscape,
                RequirePresentationAck=requireRevealPresentationAck
            }, resultsConfig: new ResultsConfig
            {
                MinimumDisplaySeconds=Mathf.Max(0f, resultsMinimumDisplaySeconds),
                MaximumDisplaySeconds=Mathf.Max(Mathf.Max(1f, resultsMaximumDisplaySeconds), resultsMinimumDisplaySeconds),
                AllowEarlyContinue=allowEarlyResultsContinue
            });
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
