using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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
        private int _lastVoteSeconds = -1;
        private readonly PresentationStateMachine _presentation = new PresentationStateMachine();

        private void Awake()
        {
            if (bootstrap == null) bootstrap = FindObjectOfType<LocalGameBootstrap>();
            if (screens == null) screens = FindObjectOfType<ScreenManager>();
            if (environment == null) environment = FindObjectOfType<EnvironmentPresentationController>();
        }

        private void Update()
        {
            if (_controller != null)
            {
                if (_controller.Phase == RoundPhase.Voting)
                {
                    var seconds=_controller.RemainingVotingSeconds;
                    if (seconds != _lastVoteSeconds) { screens?.BindTimer(seconds); _lastVoteSeconds=seconds; }
                }
                return;
            }
            if (bootstrap == null) bootstrap = FindObjectOfType<LocalGameBootstrap>();
            if (screens == null) screens = FindObjectOfType<ScreenManager>();
            if (bootstrap == null || bootstrap.Controller == null || screens == null) return;
            _controller = bootstrap.Controller;
            _controller.PhaseChanged += OnPhaseChanged;
            _controller.StateChanged += OnCoreStateChanged;
            _controller.DiscussionEventPublished += OnDiscussionEventPublished;
            _gameTimer = bootstrap.Timer;
            if (_gameTimer != null) _gameTimer.Tick += OnTimerTicked;
            else _controller.Discussion.Ticked += OnTimerTicked;
            if (screens != null)
            {
                screens.VoteSubmitted += OnVoteSubmitted;
                screens.ChatMessageSubmitted += OnChatMessageSubmitted;
                screens.CallVoteRequested += OnCallVoteRequested;
                screens.RoundResultsContinueRequested += OnRoundResultsContinue;
                screens.PrivateRoleContinueRequested += OnPrivateRoleContinue;
                screens.ScenarioIntroContinueRequested += OnScenarioIntroContinue;
            }
            Route(_controller.Phase);
            BindDiscussionMessages();
        }

        private void OnDestroy()
        {
            if (_controller != null)
            {
                _controller.PhaseChanged -= OnPhaseChanged;
                _controller.StateChanged -= OnCoreStateChanged;
                _controller.DiscussionEventPublished -= OnDiscussionEventPublished;
                if (_gameTimer != null) _gameTimer.Tick -= OnTimerTicked;
                else _controller.Discussion.Ticked -= OnTimerTicked;
            }
            if (screens != null)
            {
                screens.VoteSubmitted -= OnVoteSubmitted;
                screens.ChatMessageSubmitted -= OnChatMessageSubmitted;
                screens.CallVoteRequested -= OnCallVoteRequested;
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
                        break;
                    case RoundPhase.Results:
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
            if (_controller != null && _controller.Phase == RoundPhase.Results) Route(RoundPhase.Results);
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

        private void OnChatMessageSubmitted(string text)
        {
            if (_controller == null || _controller.Phase != RoundPhase.Discussion) return;
            try { _controller.SendChatMessage(localPlayerId, text); }
            catch (Exception exception)
            {
                screens?.ShowToast(exception.Message);
                VisualPresentationBus.Shared.Raise(VisualEventType.PresentationError, "chat message rejected");
            }
        }

        private void OnDiscussionEventPublished(DiscussionEvent item) => BindDiscussionMessages();

        private void OnCallVoteRequested()
        {
            if (_controller == null) return;
            if (_controller.Phase == RoundPhase.Discussion)
            {
                try
                {
                    if (!_controller.RequestEarlyVote(localPlayerId)) screens?.ShowToast("Waiting for the room to agree before voting.");
                }
                catch (Exception exception) { screens?.ShowToast(exception.Message); }
            }
            else if (_controller.Phase == RoundPhase.Voting) screens?.ShowOverlay(VisualOverlayId.Voting);
        }

        private void BindDiscussionMessages()
        {
            if (_controller == null || screens == null) return;
            var players=_controller.GetPublicPlayers().ToDictionary(player => player.PlayerId.ToString(), player => player.DisplayName);
            var messages=_controller.GetDiscussionSnapshot().Messages.Select(message => new ChatMessageViewModel
            {
                Sender=players.TryGetValue(message.PlayerId.ToString(), out var name) ? name : "Player " + message.PlayerId,
                Message=message.Text,
                Timestamp=DateTimeOffset.FromUnixTimeMilliseconds(message.Timestamp).ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                IsLocalPlayer=message.PlayerId == localPlayerId
            }).ToList();
            screens.BindChatMessages(messages);
        }

        private void BindVoteTargets()
        {
            var players = _controller.GetPublicPlayers();
            var eligibleTargets = new HashSet<int>(_controller.GetEligibleVoteTargets(localPlayerId));
            var tied = _controller.IsRunoff && _controller.LastOutcome != null
                ? new HashSet<int>(_controller.LastOutcome.Counts.Where(p => p.Value == _controller.LastOutcome.Counts.Values.Max()).Select(p => p.Key))
                : null;
            screens.BindVoteTargets(players.Select(p => new VoteTargetViewModel
            {
                PlayerId = p.PlayerId.ToString(), DisplayName = p.DisplayName,
                IsLocalPlayer = p.PlayerId == localPlayerId, IsConnected = p.Connected,
                IsEligible = eligibleTargets.Contains(p.PlayerId) && (tied == null || tied.Contains(p.PlayerId))
            }).ToList());
        }

        private void BindVoteProgress()
        {
            var progress = _controller.GetVoteProgress();
            screens.BindVoteProgress(progress.Submitted, progress.Eligible, _controller.IsRunoff, _controller.VotingStatus == VoteStatus.Open);
        }

        private void OnCoreStateChanged()
        {
            if (_controller == null || _controller.Phase != RoundPhase.Voting) return;
            var progress = _controller.GetVoteProgress();
            var isOpen=_controller.VotingStatus == VoteStatus.Open;
            screens.BindVoteProgress(progress.Submitted, progress.Eligible, _controller.IsRunoff, isOpen);
            if (!isOpen) { BindVoteTargets(); screens.HideOverlay(VisualOverlayId.Voting); }
            if (!isOpen && !_resolutionQueued)
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
                    if (_controller.VotingStatus != VoteStatus.Open)
                    {
                        var result = _controller.ResolveVotes();
                        var resolution = _controller.LastVoteResolutionResult;
                        if (resolution == null || resolution.Outcome == ResolutionOutcome.Pending)
                        {
                            screens.ShowVoteResolution("NO VALID VOTES", "No target is selected. The round remains pending.");
                            StartCoroutine(HideVoteResolutionAfter(1.25f));
                            return;
                        }
                        if (result == VoteResolution.RevoteRequired)
                        {
                            VisualEventBus.Raise(VisualEventType.VoteTie);
                            PlayApprovedEffect(VisualEffectId.VoteTie);
                            screens.ShowVoteResolution("TIE VOTE", "The top targets are tied. A revote is starting.");
                            StartCoroutine(HideVoteResolutionAfter(1.05f));
                        }
                        else if (resolution.Outcome == ResolutionOutcome.ThiefEscaped)
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
            var reveal = _controller.LastRevealResult;
            var roles = _controller.GetRevealedPlayers();
            var selectedId = reveal != null ? reveal.SelectedTargetId : null;
            var publicThiefId = reveal != null ? reveal.PublicThiefPlayerId : null;
            var displayId = selectedId.HasValue && roles.ContainsKey(selectedId.Value) ? selectedId : publicThiefId;
            var displayRole = displayId.HasValue && roles.TryGetValue(displayId.Value, out var foundRole) ? foundRole : null;
            var displayName = displayId.HasValue ? _controller.GetPublicPlayers().FirstOrDefault(player => player.PlayerId == displayId.Value)?.DisplayName : null;
            var thiefName = publicThiefId.HasValue ? _controller.GetPublicPlayers().FirstOrDefault(player => player.PlayerId == publicThiefId.Value)?.DisplayName : null;
            var outcomeText = reveal == null ? "REVEAL UNAVAILABLE"
                : reveal.Outcome == RevealOutcome.ThiefCaught ? "THE THIEF WAS CAUGHT · THE NECKLACE IS RECOVERED"
                : reveal.Outcome == RevealOutcome.WrongPlayerSelected ? (publicThiefId.HasValue ? "WRONG PLAYER SELECTED · THE THIEF IS REVEALED" : "WRONG PLAYER SELECTED")
                : "THE THIEF ESCAPED · THE NECKLACE REMAINS MISSING";
            var viewModel = new AuthorizedRevealViewModel
            {
                Title = "THE REVEAL", Outcome = outcomeText,
                SelectedTarget = selectedId.HasValue ? "SELECTED: " + (displayName ?? ("PLAYER " + selectedId.Value)) : "SELECTED: NONE · ESCAPE",
                ThiefPlayer = publicThiefId.HasValue ? "THIEF: " + (thiefName ?? ("PLAYER " + publicThiefId.Value)) : string.Empty,
                NecklaceRevealed = reveal != null && reveal.Necklace.IsRevealed,
                NecklaceOwner = reveal != null && reveal.Necklace.OwnerPlayerId.HasValue
                    ? (_controller.GetPublicPlayers().FirstOrDefault(player => player.PlayerId == reveal.Necklace.OwnerPlayerId.Value)?.DisplayName ?? ("PLAYER " + reveal.Necklace.OwnerPlayerId.Value)) : string.Empty
            };
            if (displayRole != null)
            {
                var roleName = displayRole.IsThief ? "THIEF" : displayRole.Role.ToString().ToUpperInvariant();
                viewModel.RevealedRole = roleName;
                viewModel.IsThiefObjectiveReveal = displayRole.IsThief;
                if (roleVisuals != null)
                {
                    var art = displayRole.IsThief ? roleVisuals.thiefObjectiveRevealData : roleVisuals.Find(displayRole.Role.ToString());
                    viewModel.RevealedArtwork = displayRole.IsThief
                        ? (art != null ? art.cardArt : roleVisuals.thiefObjectiveRevealArt)
                        : (art != null ? art.cardArt : null);
                }
            }
            else viewModel.RevealedRole = selectedId.HasValue ? "ROLE NOT REVEALED BY POLICY" : "NO ACCUSATION";
            screens.BindScreen(ScreenId.Reveal, viewModel);
            if (vfxPool != null && reveal != null)
                vfxPool.Play(reveal.Outcome == RevealOutcome.ThiefCaught ? VisualEffectId.NecklaceSpark : VisualEffectId.Escape, vfxPool.transform.position);
            if (reveal != null)
                VisualEventBus.Raise(reveal.Outcome == RevealOutcome.ThiefCaught ? VisualEventType.Victory : VisualEventType.Escape);
        }

        private void BindRoundResults()
        {
            var rows = _controller.GetRoundScoreboard();
            var viewModel = new ResultsScreenViewModel
            {
                Title = "ROUND " + _controller.CurrentRound + " RESULTS",
                Summary = _controller.LastRevealResult == null ? "Reveal information is unavailable."
                    : _controller.LastRevealResult.Outcome == RevealOutcome.ThiefCaught ? "The Thief was discovered. The Necklace is recovered."
                    : _controller.LastRevealResult.Outcome == RevealOutcome.WrongPlayerSelected ? "The wrong player was selected. The Thief escaped with the Necklace."
                    : "The Thief escaped after the tie. The Necklace remains missing.",
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
            if (_controller == null || _controller.Phase != RoundPhase.Results) return;
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
