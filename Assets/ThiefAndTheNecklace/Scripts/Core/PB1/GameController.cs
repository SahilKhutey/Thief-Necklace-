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
        private readonly KnowledgeService _knowledge = new KnowledgeService();
        private readonly IScenarioGenerator _scenarioGenerator;
        private readonly VoteService _votes = new VoteService();
        private readonly VoteResolutionService _resolution = new VoteResolutionService();
        private readonly ScoreService _scores = new ScoreService();
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
        public RoundPhase Phase { get { return _state == null ? RoundPhase.Waiting : _state.Phase; } }
        public int CurrentRound { get { return _state == null ? 0 : _state.CurrentRound; } }
        public int TotalRounds { get { return _totalRounds; } }
        public VoteOutcome LastOutcome { get { return _lastOutcome; } }
        public bool IsRunoff { get { return _state != null && _state.VoteRound > 0; } }
        public IReadOnlyList<PlayerClaim> Claims { get { return _claims.Claims; } }

        public GameController(ScenarioDefinitionData scenario, int totalRounds = 5, int matchSeed = 10001)
        {
            _scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
            if (totalRounds < 1) throw new ArgumentOutOfRangeException(nameof(totalRounds));
            _totalRounds = totalRounds;
            _matchSeed = matchSeed;
            if (_scenario.TruthModel != null) _scenarioGenerator = new StaticScenarioGenerator(_scenario.TruthModel);
            _claims.ClaimAdded += claim => Logged?.Invoke(GameSignal.ClaimCreated);
            _claims.ClaimWasRetracted += claim => Logged?.Invoke(GameSignal.ClaimRetracted);
            _rounds.PhaseChanged += phase => { if (_state != null) _state.Phase = phase; PhaseChanged?.Invoke(phase); };
            _discussionTimer.Ended += () => { if (_state != null && _state.Phase == RoundPhase.Discussion) BeginVoting(); };
        }

        public void StartLocalMatch(IReadOnlyList<string> playerNames)
        {
            if (_state != null) throw new InvalidOperationException("A match has already started on this controller.");
            if (playerNames == null) throw new ArgumentNullException(nameof(playerNames));
            if (playerNames.Count < 4 || playerNames.Count > 6) throw new ArgumentException("Local matches require 4–6 players.", nameof(playerNames));
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
            if (_state.Phase != RoundPhase.Scoring && _state.Phase != RoundPhase.RoundEnd && _state.Phase != RoundPhase.MatchEnd)
                throw new InvalidOperationException("Round scores are available only after scoring.");
            return _state.Players
                .OrderByDescending(p => p.MatchScore)
                .ThenBy(p => p.PlayerId)
                .Select(p => new RoundScoreViewData { PlayerId = p.PlayerId, DisplayName = p.DisplayName, RoundScore = p.RoundScore, MatchScore = p.MatchScore })
                .ToList().AsReadOnly();
        }

        /// <summary>Returns aggregate progress only; individual vote choices remain private until resolution.</summary>
        public VoteProgressViewData GetVoteProgress()
        {
            EnsurePhase(RoundPhase.Voting);
            var eligible = _state.Players.Where(p => p.Connected).ToList();
            return new VoteProgressViewData(eligible.Count(p => p.HasSubmittedVote), eligible.Count);
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
            if (_state.Phase != RoundPhase.Reveal && _state.Phase != RoundPhase.Scoring && _state.Phase != RoundPhase.RoundEnd && _state.Phase != RoundPhase.MatchEnd)
                throw new InvalidOperationException("Roles are revealed only after vote resolution.");
            return new ReadOnlyDictionary<int, RevealedPlayerData>(_state.Players.ToDictionary(p => p.PlayerId,
                p => new RevealedPlayerData { PlayerId = p.PlayerId, Role = p.Role, IsThief = p.IsThief }));
        }

        public void BeginDiscussion() { EnsurePhase(RoundPhase.PrivateReveal); _rounds.TransitionTo(RoundPhase.Discussion); _discussionTimer.Start(180f); Logged?.Invoke(GameSignal.DiscussionStarted); RaiseChanged(); }
        public void BeginVoting() { EnsurePhase(RoundPhase.Discussion); _discussionTimer.Pause(); Logged?.Invoke(GameSignal.DiscussionEnded); _state.ResetVotes(); _runoffCandidates = null; _rounds.TransitionTo(RoundPhase.Voting); Logged?.Invoke(GameSignal.VotingStarted); RaiseChanged(); }

        public bool SubmitVote(int voterId, int targetId)
        {
            EnsurePhase(RoundPhase.Voting);
            var accepted = _votes.SubmitVote(_state, voterId, targetId, _runoffCandidates);
            Logged?.Invoke(accepted ? GameSignal.VoteSubmitted : GameSignal.VoteRejected);
            if (accepted) RaiseChanged();
            return accepted;
        }

        public PlayerClaim AddClaim(int playerId, string factId, string statement, float gameTime)
        {
            EnsurePhase(RoundPhase.Discussion);
            var player = _state.Players.FirstOrDefault(x => x.PlayerId == playerId);
            if (player == null) throw new ArgumentOutOfRangeException(nameof(playerId));
            if (!string.IsNullOrWhiteSpace(factId) && !_roundTruth.Scenario.Facts.Any(x => x != null && x.FactId == factId)) throw new ArgumentException("Claim references an unknown fact.", nameof(factId));
            return _claims.AddClaim(playerId, factId, statement, gameTime);
        }

        public bool RetractClaim(string claimId)
        {
            EnsurePhase(RoundPhase.Discussion);
            return _claims.RetractClaim(claimId);
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
            var claimedFacts = _claims.Claims.Where(c => !c.Retracted && !string.IsNullOrWhiteSpace(c.FactId))
                .Select(c => _roundTruth.Scenario.Facts.FirstOrDefault(f => f != null && f.FactId == c.FactId))
                .Where(f => f != null)
                .Select(f => new FactDefinition { FactId=f.FactId, SubjectId=f.SubjectId, Predicate=f.Predicate, ObjectId=f.ObjectId, Truth=EvidenceTruth.True });
            var result = new ContradictionService().FindContradictions(claimedFacts);
            if (result.ContradictionFound) Logged?.Invoke(GameSignal.ContradictionDetected);
            return result;
        }

        public bool HasPlayerOpportunity(int playerId, float eventTime, string eventLocation)
        {
            EnsureStarted();
            var service = new OpportunityService();
            var result = _roundTruth.Scenario.Opportunities.Any(window => window != null && (window.PlayerId == playerId.ToString() || window.PlayerId == "Thief" && _state.Players.Any(p => p.PlayerId == playerId && p.IsThief)) && service.HasOpportunity(window, eventTime, eventLocation));
            Logged?.Invoke(GameSignal.OpportunityChecked);
            return result;
        }

        public VoteResolution ResolveVotes()
        {
            EnsurePhase(RoundPhase.Voting);
            if (!_votes.EveryoneHasVoted(_state)) throw new InvalidOperationException("Cannot resolve votes before everyone votes.");
            _rounds.TransitionTo(RoundPhase.VoteResolution);
            _lastOutcome = _resolution.Resolve(_state, _state.VoteRound > 0, _runoffCandidates);
            Logged?.Invoke(GameSignal.VoteResolved);
            if (_lastOutcome.Result == VoteResolution.RevoteRequired)
            {
                _state.VoteRound++;
                _runoffCandidates = _lastOutcome.Counts.Where(x => x.Value == _lastOutcome.Counts.Values.Max()).Select(x => x.Key).ToList();
                _state.ResetVotes();
                _rounds.TransitionTo(RoundPhase.Voting);
                Logged?.Invoke(GameSignal.RevoteStarted);
            }
            else { _roleRevealNotified = false; _rounds.TransitionTo(RoundPhase.Reveal); Logged?.Invoke(GameSignal.RevealStarted); }
            RaiseChanged();
            return _lastOutcome.Result;
        }

        public void CalculateScores()
        {
            EnsurePhase(RoundPhase.Reveal);
            if (!_roleRevealNotified) throw new InvalidOperationException("Role reveal must complete before scoring.");
            _scores.CalculateRoundScore(_state, _lastOutcome);
            _scores.CommitRoundScores(_state);
            _rounds.TransitionTo(RoundPhase.Scoring);
            Logged?.Invoke(GameSignal.RoundScoreCalculated);
            RaiseChanged();
        }

        public void NotifyRoleRevealed()
        {
            EnsurePhase(RoundPhase.Reveal);
            if (_roleRevealNotified) throw new InvalidOperationException("Role reveal has already been reported for this round.");
            _roleRevealNotified = true;
            Logged?.Invoke(GameSignal.RoleRevealed);
        }

        public void FinishRound()
        {
            EnsurePhase(RoundPhase.Scoring);
            _rounds.TransitionTo(RoundPhase.RoundEnd);
            Logged?.Invoke(GameSignal.RoundCompleted);
            if (_state.CurrentRound >= _state.TotalRounds) { _rounds.TransitionTo(RoundPhase.MatchEnd); Logged?.Invoke(GameSignal.MatchCompleted); }
            else
            {
                _state.CurrentRound++;
                _rounds.TransitionTo(RoundPhase.NextRound);
                _rounds.TransitionTo(RoundPhase.Starting);
                SetupRound();
            }
            RaiseChanged();
        }

        public void AdvanceTimer(float deltaSeconds) { EnsureStarted(); if (_state.Phase != RoundPhase.Discussion) return; _discussionTimer.Advance(deltaSeconds); }
        public DiscussionTimer Discussion { get { return _discussionTimer; } }

        private readonly DiscussionTimer _discussionTimer = new DiscussionTimer();
        private void SetupRound()
        {
            Logged?.Invoke(GameSignal.RoundStarted);
            _state.ScenarioId = _scenario.ScenarioId;
            _state.VoteRound = 0;
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
            truth.PlayerInformation = selectedBundles;
            foreach (var window in truth.Opportunities.Where(x => x != null && x.PlayerId == "Thief"))
                window.PlayerId = state.Players.Single(p => p.IsThief).PlayerId.ToString();
            var result = _scenarioValidator.Validate(truth, state.Players.Count);
            if (!result.Valid) throw ValidationFailure(string.Join(" ", result.Errors));
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
