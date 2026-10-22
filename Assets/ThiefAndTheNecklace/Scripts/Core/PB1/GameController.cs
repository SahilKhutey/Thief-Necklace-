using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ThiefAndTheNecklace.PB1
{
    public sealed class GameController
    {
        private readonly ScenarioDefinitionData _scenario;
        private readonly RoleAssignmentService _roles = new RoleAssignmentService();
        private readonly ScenarioService _scenarioService = new ScenarioService();
        private readonly ScenarioValidator _scenarioValidator = new ScenarioValidator();
        private readonly ClaimService _claims = new ClaimService();
        private DiscussionService _discussionService;
        private readonly VotingService _votingService = new VotingService();
        private readonly FinalizedVoteResolver _finalizedVoteResolver = new FinalizedVoteResolver();
        private readonly VoteResolutionConfig _resolutionConfig;
        private readonly RevealService _revealService;
        private readonly Dictionary<string, VoteResolutionCommit> _resolutionHistory = new Dictionary<string, VoteResolutionCommit>(StringComparer.Ordinal);
        private readonly KnowledgeService _knowledge = new KnowledgeService();
        private readonly IScenarioGenerator _scenarioGenerator;
        private readonly VoteService _votes = new VoteService();
        private readonly VoteResolutionService _resolution = new VoteResolutionService();
        private readonly ScoringService _scoringService;
        private readonly RoundCompletionService _roundCompletionService;
        private readonly RoundManager _rounds = new RoundManager();
        private readonly int _matchSeed;
        private readonly int _totalRounds;
        private VoteOutcome _lastOutcome;
        private List<int> _runoffCandidates;
        private bool _roleRevealNotified;
        private GameState _state;
        private RoundTruth _roundTruth;
        private EvidenceGraph _evidenceGraph;

        public event Action<RoundPhase> PhaseChanged;
        public event Action StateChanged;
        public event Action<GameSignal> Logged;
        public event Action<VoteResolutionResult> VoteResolutionPublished;
        public event Action<RevealEvent> RevealEventPublished;
        public event Action<ScoreEvent> ScoreEventPublished;
        public event Action<DiscussionEvent> DiscussionEventPublished;
        public RoundPhase Phase { get { return _state == null ? RoundPhase.Waiting : _state.Phase; } }
        public int CurrentRound { get { return _state == null ? 0 : _state.CurrentRound; } }
        public int TotalRounds { get { return _totalRounds; } }
        public VoteOutcome LastOutcome { get { return _lastOutcome; } }
        public VoteResolutionResult LastVoteResolutionResult { get; private set; }
        public RevealResult LastRevealResult => _revealService.Current;
        public bool IsRunoff { get { return _state != null && _state.VoteRound > 0; } }
        public IReadOnlyList<PlayerClaim> Claims { get { return _claims.Claims; } }
        private string LocalMatchId => "local-" + _matchSeed.ToString(System.Globalization.CultureInfo.InvariantCulture);
        private string LocalRoundId => LocalMatchId + "/round/" + CurrentRound.ToString(System.Globalization.CultureInfo.InvariantCulture);

        public GameController(ScenarioDefinitionData scenario, int totalRounds = 5, int matchSeed = 10001, DiscussionRateConfig discussionConfig = null, VoteResolutionConfig resolutionConfig = null, RevealConfig revealConfig = null, ScoringConfig scoringConfig = null, ResultsConfig resultsConfig = null)
        {
            _scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
            if (totalRounds < 1) throw new ArgumentOutOfRangeException(nameof(totalRounds));
            _totalRounds = totalRounds;
            _matchSeed = matchSeed;
            _discussionService=new DiscussionService(discussionConfig);
            _resolutionConfig=resolutionConfig ?? new VoteResolutionConfig(); _resolutionConfig.Validate();
            _revealService=new RevealService(revealConfig);
            _scoringService=new ScoringService(scoringConfig);
            _roundCompletionService=new RoundCompletionService(resultsConfig);
            _scoringService.EventPublished += item => ScoreEventPublished?.Invoke(item);
            _revealService.EventPublished += item =>
            {
                RevealEventPublished?.Invoke(item);
                if (item.Type == RevealEventType.RoleRevealed) Logged?.Invoke(GameSignal.RoleRevealed);
                else if (item.Type == RevealEventType.ThiefRevealed) Logged?.Invoke(GameSignal.ThiefRevealed);
                else if (item.Type == RevealEventType.NecklaceRevealed) Logged?.Invoke(GameSignal.NecklaceRevealed);
                else if (item.Type == RevealEventType.RevealOutcomeResolved) Logged?.Invoke(GameSignal.RevealOutcomeResolved);
                else if (item.Type == RevealEventType.RevealCompleted) Logged?.Invoke(GameSignal.RevealCompleted);
            };
            if (_scenario.TruthModel != null) _scenarioGenerator = new StaticScenarioGenerator(_scenario.TruthModel);
            _claims.ClaimAdded += claim => Logged?.Invoke(GameSignal.ClaimCreated);
            _claims.ClaimWasRetracted += claim => Logged?.Invoke(GameSignal.ClaimRetracted);
            _discussionService.EventPublished += item =>
            {
                DiscussionEventPublished?.Invoke(item);
                if (item.Type == DiscussionEventType.ContradictionDetected) Logged?.Invoke(GameSignal.ContradictionDetected);
            };
            _votingService.EventPublished += (type, playerId, reason) => { if (type == VoteEventType.VotingClosed || type == VoteEventType.VoteCollectionFinalized || type == VoteEventType.VotingProgressChanged) RaiseChanged(); };
            _rounds.PhaseChanged += phase => { if (_state != null) _state.Phase = phase; PhaseChanged?.Invoke(phase); };
            _discussionTimer.Ended += () => { if (_state != null && _state.Phase == RoundPhase.Discussion) BeginVoting(); };
        }

        public void StartLocalMatch(IReadOnlyList<string> playerNames)
        {
            if (_state != null) throw new InvalidOperationException("A match has already started on this controller.");
            if (playerNames == null) throw new ArgumentNullException(nameof(playerNames));
            if (playerNames.Count < 4 || playerNames.Count > 6) throw new ArgumentException("Local matches require 4–6 players.", nameof(playerNames));
            if (_discussionService.AllowEarlyVote && _discussionService.EarlyVoteConfirmationsRequired > playerNames.Count) throw new ArgumentException("Early-vote confirmations cannot exceed the match size.", nameof(playerNames));
            if (playerNames.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Player names must be non-empty.", nameof(playerNames));
            var candidate = new GameState { MatchNumber = 1, CurrentRound = 1, TotalRounds = _totalRounds, Phase = RoundPhase.Waiting, ScenarioId = _scenario.ScenarioId };
            Logged?.Invoke(GameSignal.GameBoot);
            for (var i = 0; i < playerNames.Count; i++) candidate.Players.Add(new PlayerRuntimeData { PlayerId = i + 1, DisplayName = playerNames[i] });
            var seed = unchecked(_matchSeed + 7919);
            _roles.AssignRoles(candidate.Players, seed);
            ApplyScenarioPlayerInformation(candidate);
            var generated = GenerateAndValidateRoundTruth(candidate, seed);
            _roundTruth = new RoundTruth(_scenario.ScenarioId, generated);
            _state = candidate;
            Logged?.Invoke(GameSignal.LocalMatchStarted);
            _rounds.TransitionTo(RoundPhase.Starting);
            SetupRound();
        }

        public IReadOnlyList<PlayerViewData> GetPublicPlayers()
        {
            EnsureStarted();
            return _state.Players.Select(p => new PlayerViewData { PlayerId = p.PlayerId, DisplayName = p.DisplayName, MatchScore = p.MatchScore, Connected = p.Connected }).ToList();
        }

        public IReadOnlyList<PlayerViewData> GetMatchStandings()
        {
            EnsurePhase(RoundPhase.MatchEnd);
            return _state.Players
                .OrderByDescending(p => p.MatchScore)
                .ThenBy(p => p.PlayerId)
                .Select(p => new PlayerViewData { PlayerId = p.PlayerId, DisplayName = p.DisplayName, MatchScore = p.MatchScore, Connected = p.Connected })
                .ToList();
        }

        /// <summary>Public score rows are available only after the round's score has been committed.</summary>
        public IReadOnlyList<RoundScoreViewData> GetRoundScoreboard()
        {
            if (_state == null) throw new InvalidOperationException("No match has started.");
            if (_state.Phase != RoundPhase.Scoring && _state.Phase != RoundPhase.Results && _state.Phase != RoundPhase.RoundEnd && _state.Phase != RoundPhase.MatchEnd)
                throw new InvalidOperationException("Round scores are available only after scoring.");
            return _state.Players
                .OrderByDescending(p => p.MatchScore)
                .ThenBy(p => p.PlayerId)
                .Select(p => new RoundScoreViewData { PlayerId = p.PlayerId, DisplayName = p.DisplayName, RoundScore = p.RoundScore, MatchScore = p.MatchScore })
                .ToList().AsReadOnly();
        }

        /// <summary>Returns aggregate progress only; individual vote choices remain private until resolution.</summary>
        public VoteProgressViewData GetVoteProgress() { EnsurePhase(RoundPhase.Voting); return _votingService.GetProgress(); }
        public VoteStatus VotingStatus { get { _votingService.Poll(); return _votingService.Status; } }
        public int RemainingVotingSeconds => _votingService.RemainingSeconds;
        public FinalizedVoteCollection GetFinalizedVotes() => _votingService.GetFinalizedVotes();
        public IReadOnlyList<FinalizedVoteCollection> GetFinalizedVoteHistory() => _votingService.GetFinalizedVoteHistory();
        public IReadOnlyList<int> GetEligibleVoteTargets(int voterId)
        {
            if (_state == null || _state.Phase != RoundPhase.Voting || _votingService.Status != VoteStatus.Open || _votingService.HasSubmitted(voterId)) return new List<int>().AsReadOnly();
            var voter=_state.Players.FirstOrDefault(player => player.PlayerId == voterId);
            if (voter == null || !voter.Connected) return new List<int>().AsReadOnly();
            var targets=_state.Players.Where(player => player.Connected && player.PlayerId != voterId);
            if (_state.VoteRound > 0 && _resolutionConfig.RevotePolicy == RevoteTargetPolicy.TiedPlayersOnly && _runoffCandidates != null) targets=targets.Where(player => _runoffCandidates.Contains(player.PlayerId));
            return targets.Select(player => player.PlayerId).OrderBy(id => id).ToList().AsReadOnly();
        }

        public PrivatePlayerViewData GetPrivateView(int playerId, int authenticatedRequesterId)
        {
            EnsureStarted();
            if (_state.Phase != RoundPhase.PrivateReveal && _state.Phase != RoundPhase.Discussion) throw new InvalidOperationException("Private information is unavailable in this phase.");
            if (playerId != authenticatedRequesterId) throw new InvalidOperationException("Private information is available only to its owning player.");
            var player = _state.Players.FirstOrDefault(p => p.PlayerId == playerId);
            if (player == null) throw new ArgumentOutOfRangeException(nameof(playerId));
            return new PrivatePlayerViewData
            {
                PlayerId = player.PlayerId, Role = player.Role, IsThief = player.IsThief,
                Situation = player.PrivateSituation, Clues = Array.AsReadOnly(player.PrivateClues.ToArray()),
                EvidenceIds = Array.AsReadOnly(player.KnownEvidenceIds.ToArray()), KnownFactIds = Array.AsReadOnly(player.KnownFactIds.ToArray()),
                UnknownFactIds = Array.AsReadOnly(player.UnknownFactIds.ToArray()), FalseFactIds = Array.AsReadOnly(player.FalseFactIds.ToArray()),
                Objective = player.Objective, ThiefObjective = player.IsThief ? (ThiefObjective?)player.ThiefObjective : null, RoleScoreValue = ScoreService.RoleValue(player.Role)
            };
        }

        public IReadOnlyDictionary<int, RevealedPlayerData> GetRevealedPlayers()
        {
            EnsureStarted();
            if (_revealService.Current == null) throw new InvalidOperationException("Roles are public only after the authoritative reveal begins.");
            return new ReadOnlyDictionary<int, RevealedPlayerData>(_revealService.Current.RevealedRoles.ToDictionary(item => item.PlayerId,
                item => new RevealedPlayerData { PlayerId = item.PlayerId, Role = item.Role, IsThief = item.IsThief }));
        }
        public RevealSnapshot GetRevealSnapshot() => _revealService.CaptureSnapshot();

        public void BeginDiscussion() { EnsurePhase(RoundPhase.PrivateReveal); _rounds.TransitionTo(RoundPhase.Discussion); _discussionService.Start(_state.Players.Select(player => player.PlayerId)); _discussionTimer.Start(_discussionService.DurationSeconds); Logged?.Invoke(GameSignal.DiscussionStarted); RaiseChanged(); }
        public void BeginVoting()
        {
            EnsurePhase(RoundPhase.Discussion); _discussionTimer.Pause(); _discussionService.End(); Logged?.Invoke(GameSignal.DiscussionEnded); _state.ResetVotes(); _runoffCandidates = null;
            _votingService.Start(_state.Players.Select(player => player.PlayerId), _state.VoteRound + 1, _discussionService.VotingDurationSeconds, LocalMatchId, LocalRoundId); _rounds.TransitionTo(RoundPhase.Voting); _votingService.AnnounceStarted();
            Logged?.Invoke(GameSignal.VotingStarted); RaiseChanged();
        }

        public DiscussionSnapshot GetDiscussionSnapshot(long afterSequence = 0, int pageSize = 100) => _discussionService.CaptureSnapshot(afterSequence, pageSize);
        public DiscussionMessage SendChatMessage(int playerId, string text)
        {
            EnsurePhase(RoundPhase.Discussion);
            var player=_state.Players.FirstOrDefault(item => item.PlayerId == playerId) ?? throw new ArgumentOutOfRangeException(nameof(playerId));
            return _discussionService.SendMessage(playerId, text, player.Connected, _state.Phase);
        }
        public PlayerQuestion SubmitQuestion(int fromPlayerId, int toPlayerId, string text)
        {
            EnsurePhase(RoundPhase.Discussion);
            var player=_state.Players.FirstOrDefault(item => item.PlayerId == fromPlayerId) ?? throw new ArgumentOutOfRangeException(nameof(fromPlayerId));
            return _discussionService.Ask(fromPlayerId, toPlayerId, text, player.Connected, _state.Players.Any(item => item.PlayerId == toPlayerId), _state.Phase);
        }
        public DiscussionResponse RespondToQuestion(int playerId, string questionId, string text)
        {
            EnsurePhase(RoundPhase.Discussion);
            var player=_state.Players.FirstOrDefault(item => item.PlayerId == playerId) ?? throw new ArgumentOutOfRangeException(nameof(playerId));
            return _discussionService.Respond(questionId, playerId, text, player.Connected, _state.Phase);
        }
        public ClaimChallenge ChallengeClaim(int playerId, string claimId, string text)
        {
            EnsurePhase(RoundPhase.Discussion);
            var player=_state.Players.FirstOrDefault(item => item.PlayerId == playerId) ?? throw new ArgumentOutOfRangeException(nameof(playerId));
            return _discussionService.Challenge(claimId, playerId, text, player.Connected, _state.Phase);
        }
        public bool RequestEarlyVote(int playerId)
        {
            EnsurePhase(RoundPhase.Discussion);
            var player=_state.Players.FirstOrDefault(item => item.PlayerId == playerId) ?? throw new ArgumentOutOfRangeException(nameof(playerId));
            var reached=_discussionService.RequestEarlyVote(playerId, player.Connected, _state.Phase);
            if (reached) BeginVoting();
            return reached;
        }
        public void UpdatePlayerHypothesis(int playerId, IEnumerable<string> candidates, IEnumerable<string> supported, IEnumerable<string> rejected)
        {
            EnsurePhase(RoundPhase.Discussion);
            var playerIds=new HashSet<string>(_state.Players.Select(item => item.PlayerId.ToString()), StringComparer.Ordinal);
            var candidateList=(candidates ?? Enumerable.Empty<string>()).ToList();
            var player=_state.Players.FirstOrDefault(item => item.PlayerId == playerId);
            if (player == null || candidateList.Any(id => !playerIds.Contains(id))) throw new ArgumentException("Hypothesis references a player outside this match.");
            if (!player.Connected) throw new InvalidOperationException("Disconnected players cannot update discussion hypotheses.");
            _discussionService.UpdateHypothesis(playerId, candidateList, supported, rejected);
        }

        public bool SubmitVote(int voterId, int targetId)
        {
            EnsurePhase(RoundPhase.Voting);
            var voter=_state.Players.FirstOrDefault(item => item.PlayerId == voterId);
            var result=_votingService.Submit(voterId, targetId, _state.VoteRound + 1, voter != null && voter.Connected, GetEligibleVoteTargets(voterId));
            var accepted=result.Accepted;
            if (accepted) { voter.HasSubmittedVote=true; voter.VoteTargetId=targetId; }
            Logged?.Invoke(accepted ? GameSignal.VoteSubmitted : GameSignal.VoteRejected);
            if (accepted) RaiseChanged();
            return accepted;
        }

        public PlayerClaim AddClaim(int playerId, string factId, string statement, float gameTime)
            => AddClaim(playerId, factId, statement, gameTime, string.IsNullOrWhiteSpace(factId) ? ClaimType.General : ClaimType.Timeline);

        public PlayerClaim AddClaim(int playerId, string factId, string statement, float gameTime, ClaimType type)
        {
            EnsurePhase(RoundPhase.Discussion);
            var player = _state.Players.FirstOrDefault(x => x.PlayerId == playerId);
            if (player == null) throw new ArgumentOutOfRangeException(nameof(playerId));
            if (!string.IsNullOrWhiteSpace(factId) && !_roundTruth.Scenario.Facts.Any(x => x != null && x.FactId == factId)) throw new ArgumentException("Claim references an unknown fact.", nameof(factId));
            if (!Enum.IsDefined(typeof(ClaimType), type)) throw new ArgumentOutOfRangeException(nameof(type));
            var claim=_discussionService.PublishClaim(playerId, factId, statement, gameTime, type, player.Connected, _state.Phase);
            return _claims.RegisterClaim(claim);
        }

        public bool RetractClaim(string claimId)
        {
            EnsurePhase(RoundPhase.Discussion);
            var retracted=_claims.RetractClaim(claimId);
            if (retracted) _discussionService.RetractClaim(claimId);
            return retracted;
        }

        public IReadOnlyList<EvidenceDefinition> GetPrivateEvidence(int playerId, int authenticatedRequesterId)
        {
            EnsureStarted();
            if (_state.Phase != RoundPhase.PrivateReveal && _state.Phase != RoundPhase.Discussion) throw new InvalidOperationException("Private evidence is unavailable in this phase.");
            if (playerId != authenticatedRequesterId) throw new InvalidOperationException("Private evidence is available only to its owning player.");
            var player = _state.Players.FirstOrDefault(p => p.PlayerId == playerId);
            if (player == null) throw new ArgumentOutOfRangeException(nameof(playerId));
            return player.KnownEvidenceIds.Select(id => { _evidenceGraph.TryGetEvidence(id, out var item); return item; }).Where(item => item != null).ToList().AsReadOnly();
        }

        public ContradictionResult EvaluateClaimContradictions()
        {
            EnsurePhase(RoundPhase.Discussion);
            foreach (var claim in _discussionService.CaptureSnapshot().Claims.Where(item => !item.Retracted && !string.IsNullOrWhiteSpace(item.FactId)))
            {
                var player=_state.Players.FirstOrDefault(item => item.PlayerId == claim.PlayerId);
                if (_discussionService.EvaluateStructuredClaim(claim, _roundTruth.Scenario, player == null ? Enumerable.Empty<string>() : player.KnownEvidenceIds) != null)
                    Logged?.Invoke(GameSignal.ContradictionDetected);
            }
            var hasContradictions=_discussionService.HasEvidenceBackedContradictions;
            return new ContradictionResult { ContradictionFound=hasContradictions, Explanation=hasContradictions ? "Structured claims conflict with traceable evidence known to the claimant." : "No evidence-backed structured claim conflict was found." };
        }

        public bool HasPlayerOpportunity(int playerId, float eventTime, string eventLocation)
        {
            EnsureStarted();
            var service = new OpportunityService();
            var result = _roundTruth.Scenario.Opportunities.Any(window => window != null && (window.PlayerId == playerId.ToString() || window.PlayerId == "Thief" && _state.Players.Any(p => p.PlayerId == playerId && p.IsThief)) && service.HasOpportunity(window, eventTime, eventLocation));
            Logged?.Invoke(GameSignal.OpportunityChecked);
            return result;
        }

        public VoteResolution ResolveVotes() => ResolveVotes(_state.VoteRound + 1);

        public VoteResolution ResolveVotes(int voteSequence)
        {
            var key=CurrentRound + ":" + voteSequence;
            if (_resolutionHistory.TryGetValue(key, out var previous)) return previous.LegacyOutcome;
            EnsurePhase(RoundPhase.Voting); _votingService.Poll();
            var collection=_votingService.GetFinalizedVotes();
            if (collection == null) throw new VoteResolutionException(VoteResolutionError.NoCollection, "Voting must close before resolution.");
            var sequence=_state.VoteRound + 1;
            if (voteSequence != sequence) throw new VoteResolutionException(VoteResolutionError.InvalidSequence, "Vote resolution request is stale or references another vote instance.");
            var allPlayers=_state.Players.Select(player => player.PlayerId).ToArray();
            var result=_finalizedVoteResolver.Resolve(collection, new VoteResolutionContext(_state.VoteRound, _resolutionConfig),
                allPlayers, allPlayers, sequence, _runoffCandidates, LocalMatchId, LocalRoundId);
            LastVoteResolutionResult=result;
            if (result.Outcome == ResolutionOutcome.Pending)
            {
                _resolutionHistory[key]=new VoteResolutionCommit(result, VoteResolution.Undecided);
                VoteResolutionPublished?.Invoke(result);
                RaiseChanged(); return VoteResolution.Undecided;
            }
            if (result.Type == VoteResolutionType.Tie && result.HighestTargets.Count < 2)
                throw new VoteResolutionException(VoteResolutionError.InvalidState, "A tie resolution requires at least two highest targets.");
            _rounds.TransitionTo(RoundPhase.VoteResolution);
            _lastOutcome=_resolution.Resolve(_state, result.Outcome == ResolutionOutcome.ThiefEscaped, _runoffCandidates, true);
            _resolutionHistory[key]=new VoteResolutionCommit(result, _lastOutcome.Result);
            LastVoteResolutionResult=result;
            VoteResolutionPublished?.Invoke(result);
            Logged?.Invoke(GameSignal.VoteResolved);
            if (result.Type == VoteResolutionType.Tie) Logged?.Invoke(GameSignal.VoteTie);
            if (result.Outcome == ResolutionOutcome.RevoteRequired)
            {
                _state.VoteRound++;
                _runoffCandidates=result.HighestTargets.ToList();
                _state.ResetVotes();
                _votingService.Start(_state.Players.Select(player => player.PlayerId), _state.VoteRound + 1, _discussionService.VotingDurationSeconds, LocalMatchId, LocalRoundId);
                _rounds.TransitionTo(RoundPhase.Voting); _votingService.AnnounceStarted();
                Logged?.Invoke(GameSignal.RevoteStarted);
            }
            else
            {
                _roleRevealNotified=false;
                _revealService.Prepare(LocalMatchId, LocalRoundId, RoundPhase.VoteResolution, result,
                    _state.Players.Select(player => (player.PlayerId, player.Role, player.IsThief)).ToArray());
                Logged?.Invoke(GameSignal.RevealStarted);
                _rounds.TransitionTo(RoundPhase.Reveal);
                _revealService.Activate();
                _roleRevealNotified=_revealService.Current.Status == RevealStatus.Complete;
            }
            RaiseChanged(); return _lastOutcome.Result;
        }

        private sealed class VoteResolutionCommit
        {
            public VoteResolutionResult Result { get; }
            public VoteResolution LegacyOutcome { get; }
            public VoteResolutionCommit(VoteResolutionResult result, VoteResolution legacyOutcome) { Result=result; LegacyOutcome=legacyOutcome; }
        }

        public void CalculateScores()
        {
            EnsurePhase(RoundPhase.Reveal);
            if (!_roleRevealNotified) throw new InvalidOperationException("Role reveal must complete before scoring.");
            var scoringPlayers=_state.Players.Select(player => new PlayerState
            { PlayerId=player.PlayerId, DisplayName=player.DisplayName, Role=player.Role, IsThief=player.IsThief,
              MatchScore=player.MatchScore, RoundScore=player.RoundScore }).ToList();
            var matchScores=scoringPlayers.ToDictionary(player => player.PlayerId, player => player.MatchScore);
            var snapshot=_scoringService.Resolve(_revealService.Current, _votingService.GetFinalizedVotes(), scoringPlayers, matchScores);
            foreach (var player in _state.Players)
            { player.RoundScore=snapshot.RoundScores[player.PlayerId]; player.MatchScore=snapshot.MatchScores[player.PlayerId]; }
            var roundResult=_roundCompletionService.OpenResults(CreateRoundMatchState(), _revealService.CaptureSnapshot(), _revealService.Current.ActualThiefPlayerId,
                snapshot, _revealService.Current.RevealedRoles, false);
            _rounds.TransitionTo(RoundPhase.Scoring);
            _rounds.TransitionTo(RoundPhase.Results);
            _roundCompletionService.PublishResults(roundResult);
            Logged?.Invoke(GameSignal.RoundScoreCalculated);
            RaiseChanged();
        }

        public void NotifyRoleRevealed()
        {
            EnsurePhase(RoundPhase.Reveal);
            if (_roleRevealNotified) return;
            _roleRevealNotified=_revealService.Complete().Status == RevealStatus.Complete;
        }

        public RevealResult CompleteReveal()
        {
            EnsurePhase(RoundPhase.Reveal);
            if (_revealService.Current == null) throw new InvalidOperationException("Reveal has not started for this round.");
            var result=_revealService.Complete();
            _roleRevealNotified=result.Status == RevealStatus.Complete;
            return result;
        }

        public ScoreSnapshot GetScoreSnapshot() => _scoringService.Capture(LocalMatchId, LocalRoundId,
            _state.Players.Select(player => new PlayerState { PlayerId=player.PlayerId, RoundScore=player.RoundScore, MatchScore=player.MatchScore }).ToArray());
        public CompletedRoundResult GetCurrentRoundResult() => _roundCompletionService.AdvanceState.ResultsActive ? _roundCompletionService.CurrentRoundResult : null;
        public IReadOnlyList<CompletedRoundResult> GetRoundHistory() => _roundCompletionService.RoundHistory;
        public MatchResult GetMatchResult() => _roundCompletionService.FinalResult;
        public bool PollResultsTimeout()
        {
            if (_state == null || _state.Phase != RoundPhase.Results) return false;
            if (!_roundCompletionService.CanAdvance(CreateRoundMatchState())) return false;
            FinishRound(); return true;
        }

        public void FinishRound()
        {
            EnsurePhase(RoundPhase.Results);
            var matchState=CreateRoundMatchState();
            foreach (var player in matchState.Players.Where(player => player.Connected))
                _roundCompletionService.Continue(matchState, player.PlayerId, LocalMatchId, LocalRoundId);
            if (!_roundCompletionService.CanAdvance(matchState)) throw new InvalidOperationException("Minimum results display time has not elapsed.");
            _roundCompletionService.MarkResultsComplete();
            _rounds.TransitionTo(RoundPhase.RoundEnd);
            Logged?.Invoke(GameSignal.RoundCompleted);
            if (_state.CurrentRound >= _state.TotalRounds)
            {
                _roundCompletionService.BuildFinalResult(matchState);
                _rounds.TransitionTo(RoundPhase.MatchEnd); Logged?.Invoke(GameSignal.MatchCompleted);
            }
            else
            {
                _state.CurrentRound++;
                _rounds.TransitionTo(RoundPhase.NextRound);
                _rounds.TransitionTo(RoundPhase.Starting);
                SetupRound();
            }
            RaiseChanged();
        }

        public void AdvanceTimer(float deltaSeconds) { EnsureStarted(); if (_state.Phase == RoundPhase.Discussion) _discussionTimer.Advance(deltaSeconds); else if (_state.Phase == RoundPhase.Voting) _votingService.Poll(); else if (_state.Phase == RoundPhase.Results) PollResultsTimeout(); }

        private MatchState CreateRoundMatchState() => new MatchState
        {
            MatchId=LocalMatchId, CurrentRound=_state.CurrentRound, TotalRounds=_state.TotalRounds, Status=MatchStatus.InProgress,
            Players=_state.Players.Select(player => new PlayerState { PlayerId=player.PlayerId, DisplayName=player.DisplayName, Role=player.Role,
                IsThief=player.IsThief, Connected=player.Connected, RoundScore=player.RoundScore, MatchScore=player.MatchScore }).ToList(),
            CurrentRoundState=new RoundState { RoundNumber=_state.CurrentRound, RoundId=LocalRoundId, Phase=_state.Phase, ScoringResolved=true }
        };
        public DiscussionTimer Discussion { get { return _discussionTimer; } }

        private readonly DiscussionTimer _discussionTimer = new DiscussionTimer();
        private void SetupRound()
        {
            Logged?.Invoke(GameSignal.RoundStarted);
            _state.ScenarioId = _scenario.ScenarioId;
            _state.VoteRound = 0;
            LastVoteResolutionResult = null;
            _revealService.Reset();
            _state.RoundScoreCalculated = false;
            _state.RoundScoreCommitted = false;
            _lastOutcome = null;
            _runoffCandidates = null;
            _roleRevealNotified = false;
            _claims.Clear();
            var seed = unchecked(_matchSeed + _state.CurrentRound * 7919);
            _roles.AssignRoles(_state.Players, seed);
            Logged?.Invoke(GameSignal.RolesAssigned);
            ApplyScenarioPlayerInformation(_state);
            var truth = GenerateAndValidateRoundTruth(_state, seed);
            _roundTruth = new RoundTruth(_scenario.ScenarioId, truth);
            Logged?.Invoke(GameSignal.PrivateInfoAssigned);
            _state.ResetVotes();
            _rounds.TransitionTo(RoundPhase.PrivateReveal);
            RaiseChanged();
        }

        private ScenarioTruthModel GenerateAndValidateRoundTruth(GameState state, int seed)
        {
            Logged?.Invoke(GameSignal.ScenarioBuildStarted);
            if (_scenarioGenerator == null) throw new InvalidOperationException("Scenario truth data is required before match start.");
            var truth = _scenarioGenerator.Generate(seed, state.Players.Count);
            if (truth == null) throw new InvalidOperationException("Scenario generator returned no scenario.");
            Logged?.Invoke(GameSignal.ScenarioBuildCompleted);
            if (truth.ScenarioId != _scenario.ScenarioId || truth.Timeline == null || truth.Facts == null || truth.Evidence == null || truth.Opportunities == null || truth.PlayerInformation == null)
                throw ValidationFailure("Scenario identity or required truth collections are missing.");
            if (state.Players.Count(p => p.IsThief) != 1 || state.Players.Count(p => !p.IsThief) < 3)
                throw ValidationFailure("Exactly one Thief and at least three innocents are required.");
            var selectedBundles = new List<PlayerInformationBundle>();
            foreach (var player in state.Players)
            {
                var roleKey = player.IsThief ? RoleType.Thief : player.Role;
                var bundle = truth.PlayerInformation.FirstOrDefault(x => x != null && x.Role == roleKey);
                if (bundle == null) throw ValidationFailure("Missing structured player information for " + roleKey + ".");
                var assigned = truth.PlayerInformation.FirstOrDefault(x => x != null && x.PlayerId == player.PlayerId.ToString());
                var perPlayer = bundle;
                if (!string.IsNullOrWhiteSpace(player.IsThief ? _scenario.ThiefSituation : player.PrivateSituation))
                {
                    perPlayer = new PlayerInformationBundle { PlayerId=player.PlayerId.ToString(), Role=roleKey, PrivateSituation=player.PrivateSituation, EvidenceIds=new List<string>(bundle.EvidenceIds), KnownFactIds=new List<string>(bundle.KnownFactIds), UnknownFactIds=new List<string>(bundle.UnknownFactIds), FalseFactIds=new List<string>(bundle.FalseFactIds), Objective=player.IsThief ? "Avoid detection" : bundle.Objective };
                }
                else if (assigned != null) perPlayer = assigned;
                else perPlayer = new PlayerInformationBundle { PlayerId=player.PlayerId.ToString(), Role=roleKey, PrivateSituation=bundle.PrivateSituation, EvidenceIds=new List<string>(bundle.EvidenceIds), KnownFactIds=new List<string>(bundle.KnownFactIds), UnknownFactIds=new List<string>(bundle.UnknownFactIds), FalseFactIds=new List<string>(bundle.FalseFactIds), Objective=bundle.Objective };
                perPlayer.PlayerId = player.PlayerId.ToString();
                selectedBundles.Add(perPlayer);
                player.KnownEvidenceIds.Clear();
                player.KnownFactIds.Clear();
                player.UnknownFactIds = new List<string>(perPlayer.UnknownFactIds ?? new List<string>());
                player.FalseFactIds = new List<string>(perPlayer.FalseFactIds ?? new List<string>());
                foreach (var evidenceId in perPlayer.EvidenceIds ?? new List<string>()) _knowledge.GrantEvidence(player, evidenceId);
                foreach (var factId in perPlayer.KnownFactIds ?? new List<string>()) _knowledge.GrantFact(player, factId);
                player.Objective = perPlayer.Objective;
            }
            var solutionEvidence=truth.Evidence.Where(item => item != null && item.EliminatesPlayerIds != null && item.EliminatesPlayerIds.Count > 0).OrderBy(item => item.EvidenceId, StringComparer.Ordinal).FirstOrDefault();
            if (solutionEvidence != null)
            {
                var recipient=state.Players.Where(player => !player.IsThief).OrderBy(player => player.PlayerId).First();
                var package=selectedBundles.Single(bundle => bundle.PlayerId == recipient.PlayerId.ToString());
                if (!package.EvidenceIds.Contains(solutionEvidence.EvidenceId)) package.EvidenceIds.Add(solutionEvidence.EvidenceId);
                if (!recipient.KnownEvidenceIds.Contains(solutionEvidence.EvidenceId)) recipient.KnownEvidenceIds.Add(solutionEvidence.EvidenceId);
            }
            truth.PlayerInformation = selectedBundles;
            truth.Actors=truth.Actors ?? new List<ActorState>();
            var actorIds=new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var player in state.Players)
            {
                actorIds[player.Role.ToString()]=player.PlayerId.ToString();
                if (player.IsThief) { actorIds["Thief"]=player.PlayerId.ToString(); truth.ThiefPlayerId=player.PlayerId.ToString(); }
            }
            actorIds["Candidate"]=state.Players.Where(player => !player.IsThief).OrderBy(player => player.PlayerId).First().PlayerId.ToString();
            foreach (var actor in truth.Actors ?? new List<ActorState>()) if (actor != null && actorIds.TryGetValue(actor.ActorId, out var actorId)) actor.ActorId=actorId;
            foreach (var group in truth.Actors.Where(actor => actor != null).GroupBy(actor => actor.ActorId, StringComparer.Ordinal).Where(group => group.Count() > 1).ToList())
            {
                var primary=group.First();
                foreach (var duplicate in group.Skip(1))
                {
                    foreach (var location in duplicate.AccessibleLocationIds ?? new List<string>()) if (!primary.AccessibleLocationIds.Contains(location)) primary.AccessibleLocationIds.Add(location);
                    foreach (var action in duplicate.AvailableActions ?? new List<string>()) if (!primary.AvailableActions.Contains(action)) primary.AvailableActions.Add(action);
                    truth.Actors.Remove(duplicate);
                }
            }
            foreach (var timelineEvent in truth.Timeline ?? new List<TimelineEvent>()) if (timelineEvent != null && actorIds.TryGetValue(timelineEvent.ActorId, out var eventActorId)) timelineEvent.ActorId=eventActorId;
            foreach (var fact in truth.Facts ?? new List<FactDefinition>()) if (fact != null && actorIds.TryGetValue(fact.SubjectId, out var subjectId)) fact.SubjectId=subjectId;
            foreach (var window in truth.Opportunities ?? new List<OpportunityWindow>()) if (window != null && actorIds.TryGetValue(window.PlayerId, out var playerId)) window.PlayerId=playerId;
            foreach (var transition in truth.ObjectTransitions ?? new List<ObjectTransition>()) if (transition != null && actorIds.TryGetValue(transition.ToActorId, out var holderId)) transition.ToActorId=holderId;
            foreach (var evidence in truth.Evidence ?? new List<EvidenceDefinition>()) if (evidence != null)
            {
                evidence.EliminatesPlayerIds=(evidence.EliminatesPlayerIds ?? new List<string>()).Select(id => actorIds.TryGetValue(id, out var mappedId) ? mappedId : id).Distinct(StringComparer.Ordinal).ToList();
                if (actorIds.TryGetValue(evidence.ClueTargetPlayerId, out var targetId)) evidence.ClueTargetPlayerId=targetId;
            }
            var result = _scenarioValidator.Validate(truth, state.Players.Count);
            if (!result.Valid) throw ValidationFailure(string.Join(" ", result.Errors));
            var solvability=new ScenarioSolvabilityValidator().Validate(truth);
            if (solvability.Status != SolvabilityStatus.UniqueSolution) throw ValidationFailure("Scenario does not have one evidence-supported solution: " + solvability.Explanation);
            var thiefId = state.Players.Single(p => p.IsThief).PlayerId.ToString();
            var theftEvent = truth.Timeline.FirstOrDefault(e => e != null && e.EventId == "necklace-stolen") ?? truth.Timeline.FirstOrDefault(e => e != null && e.Critical);
            if (theftEvent == null || !truth.Opportunities.Any(w => w != null && w.PlayerId == thiefId && new OpportunityService().HasOpportunity(w, theftEvent.StartTime, theftEvent.LocationId)))
                throw ValidationFailure("Assigned Thief has no valid opportunity for the critical theft event.");
            _evidenceGraph = new EvidenceGraph();
            foreach (var fact in truth.Facts) _evidenceGraph.AddFact(fact);
            foreach (var item in truth.Evidence) _evidenceGraph.AddEvidence(item);
            Logged?.Invoke(GameSignal.ScenarioValidationPassed);
            Logged?.Invoke(GameSignal.TimelineCreated);
            Logged?.Invoke(GameSignal.FactsCreated);
            Logged?.Invoke(GameSignal.EvidenceCreated);
            Logged?.Invoke(GameSignal.EvidenceGraphBuilt);
            Logged?.Invoke(GameSignal.PlayerInformationAssigned);
            return truth;
        }

        private void ApplyScenarioPlayerInformation(GameState state)
        {
            Logged?.Invoke(GameSignal.ScenarioValidationStarted);
            try { _scenarioService.ApplyPlayerInformation(_scenario, state); }
            catch (Exception exception) { throw ValidationFailure(exception.Message); }
        }

        private InvalidOperationException ValidationFailure(string reason)
        {
            Logged?.Invoke(GameSignal.ScenarioValidationFailed);
            return new InvalidOperationException("Scenario rejected before play: " + reason);
        }

        private void EnsureStarted() { if (_state == null) throw new InvalidOperationException("No match has started."); }
        private void EnsurePhase(RoundPhase expected) { EnsureStarted(); if (_state.Phase != expected) throw new InvalidOperationException("Expected phase " + expected + ", current phase is " + _state.Phase + "."); }
        private void RaiseChanged() { StateChanged?.Invoke(); }
    }
}
