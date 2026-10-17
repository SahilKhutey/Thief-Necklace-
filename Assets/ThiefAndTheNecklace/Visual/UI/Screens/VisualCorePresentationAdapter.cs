using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ThiefAndTheNecklace.PB1;
using ThiefAndTheNecklace.PB1.Unity;
using ThiefAndTheNecklace.Visual;
using UnityEngine;

namespace ThiefAndTheNecklace.Visual.UI
{
    /// <summary>Routes authoritative local-core phases into visual screens. Public events never carry private role data.</summary>
    public sealed class VisualCorePresentationAdapter : MonoBehaviour
    {
        [SerializeField] private LocalGameBootstrap bootstrap;
        [SerializeField] private ScreenManager screens;
        [SerializeField] private RoleVisualDatabase roleVisuals;
        [SerializeField] private EnvironmentPresentationController environment;
        [SerializeField] private VFXPool vfxPool;
        [SerializeField, Min(1)] private int localPlayerId = 1;

        private GameController _controller;
        private GameTimer _gameTimer;
        private bool _voteLocked;
        private bool _timerAttentionSent;
        private bool _timerCriticalSent;
        private bool _resolutionQueued;
        private bool _revealWasPresented;
        private bool _scenarioIntroShown;
        private readonly PresentationStateMachine _presentation = new PresentationStateMachine();

        private void Awake()
        {
            if (bootstrap == null) bootstrap = FindObjectOfType<LocalGameBootstrap>();
            if (screens == null) screens = FindObjectOfType<ScreenManager>();
            if (environment == null) environment = FindObjectOfType<EnvironmentPresentationController>();
        }

        private void Update()
        {
            if (_controller != null) return;
            if (bootstrap == null) bootstrap = FindObjectOfType<LocalGameBootstrap>();
            if (screens == null) screens = FindObjectOfType<ScreenManager>();
            if (bootstrap == null || bootstrap.Controller == null || screens == null) return;
            _controller = bootstrap.Controller;
            _controller.PhaseChanged += OnPhaseChanged;
            _controller.StateChanged += OnCoreStateChanged;
            _gameTimer = bootstrap.Timer;
            if (_gameTimer != null) _gameTimer.Tick += OnTimerTicked;
            else _controller.Discussion.Ticked += OnTimerTicked;
            if (screens != null)
            {
                screens.VoteSubmitted += OnVoteSubmitted;
                screens.RoundResultsContinueRequested += OnRoundResultsContinue;
                screens.PrivateRoleContinueRequested += OnPrivateRoleContinue;
                screens.ScenarioIntroContinueRequested += OnScenarioIntroContinue;
            }
            Route(_controller.Phase);
        }

        private void OnDestroy()
        {
            if (_controller != null)
            {
                _controller.PhaseChanged -= OnPhaseChanged;
                _controller.StateChanged -= OnCoreStateChanged;
                if (_gameTimer != null) _gameTimer.Tick -= OnTimerTicked;
                else _controller.Discussion.Ticked -= OnTimerTicked;
            }
            if (screens != null)
            {
                screens.VoteSubmitted -= OnVoteSubmitted;
                screens.RoundResultsContinueRequested -= OnRoundResultsContinue;
                screens.PrivateRoleContinueRequested -= OnPrivateRoleContinue;
                screens.ScenarioIntroContinueRequested -= OnScenarioIntroContinue;
            }
        }

        private void OnPhaseChanged(RoundPhase phase)
        {
            // Phase events can nest when another core subscriber resolves a reveal synchronously.
            if (_controller != null && _controller.Phase == phase) Route(phase);
        }

        private void Route(RoundPhase phase)
        {
            if (_controller == null || screens == null) return;
            try
            {
                switch (phase)
                {
                    case RoundPhase.PrivateReveal:
                        VisualEventBus.Raise(VisualEventType.RoundStarted);
                        _voteLocked = false;
                        _revealWasPresented = false;
                        if (environment != null) environment.SetState(VisualEnvironmentState.Normal);
                        if (!_scenarioIntroShown)
                        {
                            _scenarioIntroShown = true;
                            screens.SetGameplayBackEnabled(false);
                            screens.Show(ScreenId.ScenarioIntro, ScreenTransitionType.Cinematic);
                            break;
                        }
                        BindPrivateRole();
                        _presentation.Complete(PresentationState.Revealing);
                        screens.SetCinematicInputLocked(false);
                        screens.SetGameplayBackEnabled(false);
                        screens.Show(ScreenId.PrivateRole, ScreenTransitionType.CardReveal);
                        break;
                    case RoundPhase.Discussion:
                        _timerAttentionSent = false;
                        _timerCriticalSent = false;
                        BindGame();
                        if (environment != null) environment.SetState(VisualEnvironmentState.Discussion);
                        _presentation.Complete(PresentationState.Discussing);
                        screens.SetCinematicInputLocked(false);
                        screens.SetGameplayBackEnabled(false);
                        screens.HideOverlay(VisualOverlayId.Voting);
                        screens.HideOverlay(VisualOverlayId.VoteLocked);
                        screens.Show(ScreenId.Game, ScreenTransitionType.Crossfade);
                        VisualEventBus.Raise(VisualEventType.DiscussionStarted);
                        break;
                    case RoundPhase.Voting:
                        _voteLocked = false;
                        if (!_controller.IsRunoff) VisualEventBus.Raise(VisualEventType.DiscussionEnded);
                        BindGame();
                        if (environment != null) environment.SetState(VisualEnvironmentState.Voting);
                        BindVoteTargets();
                        BindVoteProgress();
                        _presentation.Complete(PresentationState.Voting);
                        screens.SetCinematicInputLocked(false);
                        screens.SetGameplayBackEnabled(false);
                        screens.Show(ScreenId.Game, ScreenTransitionType.None);
                        screens.HideOverlay(VisualOverlayId.VoteLocked);
                        screens.ShowOverlay(VisualOverlayId.Voting);
                        if (_controller.IsRunoff)
                        {
                            VisualEventBus.Raise(VisualEventType.VoteTie);
                            VisualEventBus.Raise(VisualEventType.RevoteStarted);
                        }
                        // VisualOverlayManager raises VoteOpened when the panel actually enters.
                        break;
                    case RoundPhase.VoteResolution:
                        _presentation.Complete(PresentationState.Resolving);
                        screens.HideOverlay(VisualOverlayId.Voting);
                        screens.HideOverlay(VisualOverlayId.VoteLocked);
                        VisualEventBus.Raise(VisualEventType.VoteResolved);
                        break;
                    case RoundPhase.Reveal:
                        _revealWasPresented = true;
                        _presentation.Complete(PresentationState.Revealing);
                        screens.SetCinematicInputLocked(true);
                        screens.SetGameplayBackEnabled(false);
                        screens.SetRevealContinueEnabled(false);
                        if (environment != null) environment.SetState(VisualEnvironmentState.Reveal);
                        BindAuthorizedReveal();
                        screens.Show(ScreenId.Reveal, ScreenTransitionType.Cinematic);
                        break;
                    case RoundPhase.Scoring:
                        if (!_revealWasPresented && bootstrap != null && !bootstrap.HasRevealController)
                        {
                            // Keep the authorized reveal visible briefly when the optional core reveal runner is absent.
                            _revealWasPresented = true;
                            screens.SetCinematicInputLocked(true);
                            screens.SetGameplayBackEnabled(false);
                            if (environment != null) environment.SetState(VisualEnvironmentState.Reveal);
                            BindAuthorizedReveal();
                            screens.Show(ScreenId.Reveal, ScreenTransitionType.Cinematic);
                            StartCoroutine(ShowScoreResultsAfterFallbackReveal());
                            break;
                        }
                        _presentation.Complete(PresentationState.ShowingResults);
                        VisualEventBus.Raise(VisualEventType.RevealCompleted);
                        screens.SetCinematicInputLocked(true);
                        screens.SetGameplayBackEnabled(false);
                        if (environment != null) environment.SetState(VisualEnvironmentState.Results);
                        BindRoundResults();
                        VisualEventBus.Raise(VisualEventType.RoundResultShown);
                        screens.Show(ScreenId.RoundResults, ScreenTransitionType.ScoreTransition);
                        break;
                    case RoundPhase.MatchEnd:
                        BindMatchResults();
                        VisualEventBus.Raise(VisualEventType.MatchResultShown);
                        screens.SetCinematicInputLocked(false);
                        screens.SetGameplayBackEnabled(false);
                        screens.Show(ScreenId.MatchResults, ScreenTransitionType.ScoreTransition);
                        break;
                }
            }
            catch (Exception exception)
            {
                _presentation.Recover("phase=" + phase + "; " + exception.GetType().Name);
                screens.SetCinematicInputLocked(false);
                Debug.LogException(exception, this);
                VisualPresentationBus.Shared.Raise(VisualEventType.PresentationFallback, phase.ToString());
            }
        }

        private IEnumerator ShowScoreResultsAfterFallbackReveal()
        {
            yield return new WaitForSecondsRealtime(0.9f);
            if (_controller != null && _controller.Phase == RoundPhase.Scoring) Route(RoundPhase.Scoring);
        }

        private void BindPrivateRole()
        {
            var privateData = _controller.GetPrivateView(localPlayerId, localPlayerId);
            var visuals = roleVisuals != null ? roleVisuals.Find(privateData.Role.ToString()) : null;
            screens.BindScreen(ScreenId.PrivateRole, new PrivateRoleScreenViewModel
            {
                Title = privateData.IsThief ? "YOUR ROLE · THIEF" : "YOUR ROLE",
                RoleName = privateData.IsThief ? "THIEF" : privateData.Role.ToString().ToUpperInvariant(),
                RoleArtwork = visuals != null ? visuals.cardArt : null,
                RoleValue = privateData.RoleScoreValue,
                Situation = privateData.Situation,
                Clues = privateData.Clues,
                Objective = privateData.Objective
            });
        }

        private void BindGame()
        {
            var players = _controller.GetPublicPlayers();
            var cards = players.Select(p => new PlayerCardViewModel
            {
                PlayerId = p.PlayerId.ToString(), DisplayName = p.DisplayName, Score = p.MatchScore,
                Connected = p.Connected, Status = p.PlayerId == localPlayerId ? "YOU" : string.Empty
            }).ToList();
            screens.BindScreen(ScreenId.Game, new GameScreenViewModel
            {
                Title = "ROUND " + _controller.CurrentRound + " / " + _controller.TotalRounds,
                CurrentRound = _controller.CurrentRound, TotalRounds = _controller.TotalRounds,
                RemainingTime = _controller.Discussion.RemainingSeconds,
                CanVote = _controller.Phase == RoundPhase.Voting,
                LocalPlayerId = localPlayerId.ToString(), PublicPlayers = cards
            });
        }

        private void BindVoteTargets()
        {
            var players = _controller.GetPublicPlayers();
            var tied = _controller.IsRunoff && _controller.LastOutcome != null
                ? new HashSet<int>(_controller.LastOutcome.Counts.Where(p => p.Value == _controller.LastOutcome.Counts.Values.Max()).Select(p => p.Key))
                : null;
            screens.BindVoteTargets(players.Select(p => new VoteTargetViewModel
            {
                PlayerId = p.PlayerId.ToString(), DisplayName = p.DisplayName,
                IsLocalPlayer = p.PlayerId == localPlayerId, IsConnected = p.Connected,
                IsEligible = tied == null || tied.Contains(p.PlayerId)
            }).ToList());
        }

        private void BindVoteProgress()
        {
            var progress = _controller.GetVoteProgress();
            screens.BindVoteProgress(progress.Submitted, progress.Eligible, _controller.IsRunoff);
        }

        private void OnCoreStateChanged()
        {
            if (_controller == null || _controller.Phase != RoundPhase.Voting) return;
            var progress = _controller.GetVoteProgress();
            screens.BindVoteProgress(progress.Submitted, progress.Eligible, _controller.IsRunoff);
            if (progress.Eligible > 0 && progress.Submitted >= progress.Eligible && !_resolutionQueued)
                StartCoroutine(ResolveVotesAfterVoteLock());
        }

        private IEnumerator ResolveVotesAfterVoteLock()
        {
            _resolutionQueued = true;
            yield return null;
            try
            {
                if (_controller != null && _controller.Phase == RoundPhase.Voting)
                {
                    var progress = _controller.GetVoteProgress();
                    if (progress.Eligible > 0 && progress.Submitted >= progress.Eligible)
                    {
                        var result = _controller.ResolveVotes();
                        var counts = _controller.LastOutcome != null ? _controller.LastOutcome.Counts : null;
                        var highest = counts != null && counts.Count > 0 ? counts.Values.Max() : 0;
                        var tiedAgain = _controller.IsRunoff && counts != null && counts.Count(pair => pair.Value == highest) > 1;
                        if (result == VoteResolution.RevoteRequired)
                        {
                            VisualEventBus.Raise(VisualEventType.VoteTie);
                            PlayApprovedEffect(VisualEffectId.VoteTie);
                            screens.ShowVoteResolution("TIE VOTE", "The top targets are tied. A revote is starting.");
                            StartCoroutine(HideVoteResolutionAfter(1.05f));
                        }
                        else if (tiedAgain)
                        {
                            VisualEventBus.Raise(VisualEventType.VoteTie);
                            PlayApprovedEffect(VisualEffectId.VoteTie);
                            screens.ShowVoteResolution("TIE AGAIN", "The Thief escapes after the second tie. Reveal follows.");
                            StartCoroutine(HideVoteResolutionAfter(1.25f));
                        }
                        else
                        {
                            VisualEventBus.Raise(VisualEventType.VoteResolve);
                            PlayApprovedEffect(VisualEffectId.VoteResolve);
                            screens.ShowVoteResolution("VOTE RESOLVED", "The decision is sealed. The reveal is beginning.");
                            StartCoroutine(HideVoteResolutionAfter(0.72f));
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                _presentation.Recover("resolve votes");
                Debug.LogException(exception, this);
            }
            finally { _resolutionQueued = false; }
        }

        private IEnumerator HideVoteResolutionAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            if (screens != null) screens.HideOverlay(VisualOverlayId.VoteResolution);
        }

        private void PlayApprovedEffect(VisualEffectId effect)
        {
            if (vfxPool != null) vfxPool.Play(effect, vfxPool.transform.position);
        }

        private void OnVoteSubmitted(string target)
        {
            if (_voteLocked || _controller == null || _controller.Phase != RoundPhase.Voting) return;
            if (!int.TryParse(target, out var targetId)) return;
            try
            {
                if (_controller.SubmitVote(localPlayerId, targetId))
                {
                    _voteLocked = true;
                    screens.ConfirmVoteLocked();
                    PlayApprovedEffect(VisualEffectId.VoteLock);
                }
            }
            catch (Exception exception)
            {
                VisualPresentationBus.Shared.Raise(VisualEventType.PresentationError, "vote submission rejected");
                Debug.LogException(exception, this);
            }
        }

        private void BindAuthorizedReveal()
        {
            var outcome = _controller.LastOutcome;
            string roleName = "NO ACCUSATION";
            var viewModel = new AuthorizedRevealViewModel
            {
                Title = "THE REVEAL", Outcome = outcome != null && outcome.Result == VoteResolution.ThiefEscaped
                    ? "THE THIEF ESCAPED" : "THE NECKLACE IS RECOVERED"
            };
            if (outcome != null && outcome.AccusedPlayerId.HasValue)
            {
                var roles = _controller.GetRevealedPlayers();
                var accused = roles[outcome.AccusedPlayerId.Value];
                roleName = accused.IsThief ? "THIEF" : accused.Role.ToString().ToUpperInvariant();
                viewModel.RevealedRole = roleName;
                viewModel.IsThiefObjectiveReveal = accused.IsThief;
                if (roleVisuals != null)
                {
                    var art = accused.IsThief ? roleVisuals.thiefObjectiveRevealData : roleVisuals.Find(accused.Role.ToString());
                    viewModel.RevealedArtwork = accused.IsThief
                        ? (art != null ? art.cardArt : roleVisuals.thiefObjectiveRevealArt)
                        : (art != null ? art.cardArt : null);
                }
            }
            else viewModel.RevealedRole = roleName;
            screens.BindScreen(ScreenId.Reveal, viewModel);
            if (vfxPool != null && outcome != null)
                vfxPool.Play(outcome.Result == VoteResolution.ThiefCaught ? VisualEffectId.NecklaceSpark : VisualEffectId.Escape, vfxPool.transform.position);
            if (outcome != null)
                VisualEventBus.Raise(outcome.Result == VoteResolution.ThiefCaught ? VisualEventType.Victory : VisualEventType.Escape);
        }

        private void BindRoundResults()
        {
            var rows = _controller.GetRoundScoreboard();
            var viewModel = new ResultsScreenViewModel
            {
                Title = "ROUND " + _controller.CurrentRound + " RESULTS",
                Summary = _controller.LastOutcome != null && _controller.LastOutcome.Result == VoteResolution.ThiefCaught
                    ? "The Thief was discovered. The Necklace is safe."
                    : "The Necklace remains missing. The Thief escaped.",
                Rankings = rows.Select(p => new PlayerCardViewModel
                {
                    PlayerId = p.PlayerId.ToString(), DisplayName = p.DisplayName,
                    Score = p.MatchScore, Status = p.RoundScore >= 0 ? "+" + p.RoundScore : p.RoundScore.ToString()
                }).ToList()
            };
            var local = rows.FirstOrDefault(p => p.PlayerId == localPlayerId);
            if (local != null) { viewModel.ScoreDelta = local.RoundScore; viewModel.TotalScore = local.MatchScore; }
            screens.BindScreen(ScreenId.RoundResults, viewModel);
        }

        private void BindMatchResults()
        {
            var standings = _controller.GetMatchStandings();
            screens.BindScreen(ScreenId.MatchResults, new ResultsScreenViewModel
            {
                Title = "MATCH RESULTS",
                Summary = _controller.LastOutcome != null && _controller.LastOutcome.Result == VoteResolution.ThiefEscaped
                    ? "THE THIEF ESCAPED" : "THE NECKLACE IS SAFE",
                Rankings = standings.Select(p => new PlayerCardViewModel
                { PlayerId = p.PlayerId.ToString(), DisplayName = p.DisplayName, Score = p.MatchScore }).ToList()
            });
        }

        private void OnRoundResultsContinue()
        {
            if (_controller == null || _controller.Phase != RoundPhase.Scoring) return;
            try { _controller.FinishRound(); }
            catch (Exception exception) { _presentation.Recover("finish round"); Debug.LogException(exception, this); }
        }

        private void OnPrivateRoleContinue()
        {
            if (_controller == null || _controller.Phase != RoundPhase.PrivateReveal) return;
            try { _controller.BeginDiscussion(); }
            catch (Exception exception) { _presentation.Recover("begin discussion"); Debug.LogException(exception, this); }
        }

        private void OnScenarioIntroContinue()
        {
            if (_controller != null && _controller.Phase == RoundPhase.PrivateReveal) Route(RoundPhase.PrivateReveal);
        }

        private void OnTimerTicked(float remaining)
        {
            if (_controller == null || screens == null) return;
            if (_controller.Phase == RoundPhase.Discussion) screens.BindTimer(remaining);
            if (remaining <= 30f && !_timerCriticalSent) { _timerCriticalSent = true; VisualEventBus.Raise(VisualEventType.TimerCritical); }
            else if (remaining <= 60f && !_timerAttentionSent) { _timerAttentionSent = true; VisualEventBus.Raise(VisualEventType.TimerWarning); }
        }
    }
}
