using System;
using System.Collections.Generic;
using System.Linq;

namespace ThiefAndTheNecklace.PB1
{
    public sealed class MatchController
    {
        private readonly RuntimeInvariantValidator invariants = new RuntimeInvariantValidator();
        private readonly StateTransitionGuard transitions = new StateTransitionGuard();
        private readonly RuntimeVoteService votes = new RuntimeVoteService();
        private readonly RuntimeVoteResolver voteResolver = new RuntimeVoteResolver();
        private readonly RuntimeScoreService scores = new RuntimeScoreService();
        private readonly RoundResolver roundResolver;
        private readonly RoundFactory roundFactory;
        private readonly ScenarioBuildPipeline scenarioPipeline;
        private readonly RoleAssignmentService roles = new RoleAssignmentService();
        private readonly ReplayRecorder replay;
        private readonly ClaimService claims = new ClaimService();
        private readonly DiscussionTimer discussionTimer = new DiscussionTimer();
        private readonly List<CoreLogEvent> coreLog = new List<CoreLogEvent>();
        private bool revealCompleted;

        public GameRuntimeState State { get; }
        public event Action<GameSignal> Logged;
        public event Action<CoreLogEvent> CoreEventLogged;
        public event Action<RoundPhase> PhaseChanged;
        public event Action StateChanged;

        public MatchController(IScenarioGenerator scenarioGenerator, int seed, int totalRounds, DifficultyLevel difficulty, IReadOnlyList<string> players)
        {
            if (scenarioGenerator == null) throw new ArgumentNullException(nameof(scenarioGenerator));
            if (players == null) throw new ArgumentNullException(nameof(players));
            if (players.Count < 4 || players.Count > 6) throw new ArgumentException("Match requires 4–6 players.", nameof(players));
            if (players.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Player names must not be empty.", nameof(players));
            if (totalRounds < 1) throw new ArgumentOutOfRangeException(nameof(totalRounds));
            DifficultyConfig.Create(difficulty);
            roundFactory=new RoundFactory(scenarioGenerator);
            scenarioPipeline=new ScenarioBuildPipeline(new ScenarioValidator());
            roundResolver=new RoundResolver(votes, voteResolver, scores);
            State=CreateInitialState(seed, totalRounds, difficulty, players);
            replay=new ReplayRecorder(State.Match.MatchId, seed);
            Record("CORE_BOOT", "");
            claims.ClaimAdded += claim => Logged?.Invoke(GameSignal.ClaimCreated);
            claims.ClaimWasRetracted += claim => Logged?.Invoke(GameSignal.ClaimRetracted);
            discussionTimer.Ended += () => { if (State.Match.Status == MatchStatus.InProgress && State.Match.CurrentRoundState.Phase == RoundPhase.Discussion) BeginVoting(); };
        }

        public void Start()
        {
            if (State.Match.Status != MatchStatus.Created) throw new InvalidOperationException("Match has already started.");
            Logged?.Invoke(GameSignal.MatchCreated);
            Record("MATCH_CREATED", "");
            ChangePhase(RoundPhase.Starting);
            PrepareRound();
            State.Match.Status=MatchStatus.InProgress;
            Logged?.Invoke(GameSignal.MatchStarted);
            Record("MATCH_STARTED", "");
            ValidateAndNotify();
        }

        public void BeginDiscussion()
        {
            EnsureMatchActive(); EnsureRoundNotCompleted(); RequirePhase(RoundPhase.PrivateReveal);
            discussionTimer.Start(180f);
            ChangePhase(RoundPhase.Discussion); Record("DISCUSSION_STARTED", ""); ValidateAndNotify();
        }

        public void AdvanceDiscussionTimer(float deltaSeconds)
        {
            EnsureMatchActive();
            if (State.Match.CurrentRoundState.Phase == RoundPhase.Discussion) discussionTimer.Advance(deltaSeconds);
        }

        public void BeginVoting()
        {
            EnsureMatchActive(); EnsureRoundNotCompleted(); RequirePhase(RoundPhase.Discussion);
            discussionTimer.Pause();
            Record("DISCUSSION_COMPLETED", "");
            State.Votes.Reset();
            foreach (var player in State.Match.Players) player.Vote.Reset();
            ChangePhase(RoundPhase.Voting); Record("VOTING_STARTED", ""); ValidateAndNotify();
        }

        public bool SubmitVote(int voterId, int targetId)
        {
            EnsureMatchActive(); EnsureRoundNotCompleted(); RequirePhase(RoundPhase.Voting);
            var accepted=votes.SubmitVote(State, voterId, targetId);
            Logged?.Invoke(accepted ? GameSignal.VoteSubmitted : GameSignal.VoteRejected);
            if (accepted) Record("VOTE_SUBMITTED", voterId + ">" + targetId);
            else Record("VOTE_REJECTED", "");
            ValidateAndNotify(); return accepted;
        }

        public IReadOnlyList<PlayerClaim> Claims => claims.Claims;

        public PlayerClaim AddClaim(int playerId, string factId, string statement, float gameTime)
        {
            EnsureMatchActive(); EnsureRoundNotCompleted(); RequirePhase(RoundPhase.Discussion);
            if (!State.Match.Players.Any(p => p.PlayerId == playerId)) throw new ArgumentOutOfRangeException(nameof(playerId));
            if (!string.IsNullOrWhiteSpace(factId) && !State.Scenario.Truth.Facts.Any(f => f != null && f.FactId == factId)) throw new ArgumentException("Claim references an unknown fact.", nameof(factId));
            var claim=claims.AddClaim(playerId, factId, statement, gameTime);
            Record("CLAIM_CREATED", playerId.ToString());
            return claim;
        }

        public bool RetractClaim(string claimId)
        {
            EnsureMatchActive(); EnsureRoundNotCompleted(); RequirePhase(RoundPhase.Discussion);
            var retracted=claims.RetractClaim(claimId);
            if (retracted) Record("CLAIM_RETRACTED", "");
            return retracted;
        }

        public ContradictionResult EvaluateClaimContradictions()
        {
            EnsureMatchActive(); RequirePhase(RoundPhase.Discussion);
            var facts=State.Scenario.Truth.Facts;
            var assertions=claims.Claims.Where(c => !c.Retracted && !string.IsNullOrWhiteSpace(c.FactId))
                .Select(c => facts.FirstOrDefault(f => f != null && f.FactId == c.FactId))
                .Where(f => f != null)
                .Select(f => new FactDefinition { FactId=f.FactId, SubjectId=f.SubjectId, Predicate=f.Predicate, ObjectId=f.ObjectId, Truth=EvidenceTruth.True });
            var result=new ContradictionService().FindContradictions(assertions);
            if (result.ContradictionFound) Logged?.Invoke(GameSignal.ContradictionDetected);
            return result;
        }

        public VoteResolution ResolveVote()
        {
            EnsureMatchActive(); EnsureRoundNotCompleted(); RequirePhase(RoundPhase.Voting);
            if (!votes.EveryoneVoted(State)) throw new InvalidOperationException("Not all connected players have voted.");
            ChangePhase(RoundPhase.VoteResolution);
            var round=State.Match.CurrentRoundState;
            var finalVote=round.VoteRound > 0;
            State.Votes.FinalVote=finalVote;
            if (!voteResolver.GetWinner(State).HasValue) Logged?.Invoke(GameSignal.VoteTie);
            var result=roundResolver.ResolveVotes(State, finalVote);
            round.Resolution=result;
            Logged?.Invoke(GameSignal.VoteResolved);
            Logged?.Invoke(GameSignal.VotesComplete);
            Record("VOTE_RESOLVED", result.ToString());
            if (result == VoteResolution.RevoteRequired)
            {
                var highest=State.Votes.Votes.GroupBy(v => v.Value).Select(g => new { Target=g.Key, Count=g.Count() }).Max(g => g.Count);
                round.RunoffCandidates=State.Votes.Votes.GroupBy(v => v.Value).Where(g => g.Count() == highest).Select(g => g.Key).OrderBy(x => x).ToList();
                if (round.RunoffCandidates.Count < 2) throw new InvalidOperationException("A runoff requires at least two tied candidates.");
                round.VoteRound++;
                State.Votes.VoteRound=round.VoteRound;
                State.Votes.Reset();
                foreach (var player in State.Match.Players) player.Vote.Reset();
                Logged?.Invoke(GameSignal.RevoteStarted);
                Record("REVOTE_STARTED", string.Join(",", round.RunoffCandidates));
                ChangePhase(RoundPhase.Voting);
            }
            else
            {
                round.RunoffCandidates.Clear();
                revealCompleted=false;
                Logged?.Invoke(GameSignal.RevealStarted);
                ChangePhase(RoundPhase.Reveal);
            }
            ValidateAndNotify(); return result;
        }

        public void CompleteReveal()
        {
            EnsureMatchActive(); EnsureRoundNotCompleted(); RequirePhase(RoundPhase.Reveal);
            if (revealCompleted) throw new InvalidOperationException("Reveal has already completed.");
            revealCompleted=true;
            Logged?.Invoke(GameSignal.RevealCompleted);
            Record("REVEAL_COMPLETED", "");
        }

        public void ResolveScoring()
        {
            EnsureMatchActive(); EnsureRoundNotCompleted(); RequirePhase(RoundPhase.Reveal);
            if (!revealCompleted) throw new InvalidOperationException("Complete reveal before resolving scores.");
            roundResolver.ResolveScoring(State);
            Logged?.Invoke(GameSignal.RoundScoreResolved);
            Logged?.Invoke(GameSignal.MatchScoreUpdated);
            Record("ROUND_SCORE_CALCULATED", "");
            Record("ROUND_SCORE_COMMITTED", "");
            ChangePhase(RoundPhase.Scoring);
            ValidateAndNotify();
        }

        public void FinishRound()
        {
            EnsureMatchActive(); EnsureRoundNotCompleted(); RequirePhase(RoundPhase.Scoring);
            if (!State.Match.CurrentRoundState.ScoringResolved || !State.Scores.CommittedRounds.Contains(State.Match.CurrentRound)) throw new InvalidOperationException("Round score must be committed exactly once before finishing.");
            State.Match.CurrentRoundState.Completed=true;
            ChangePhase(RoundPhase.RoundEnd);
            Logged?.Invoke(GameSignal.RoundCompleted);
            Record("ROUND_COMPLETED", State.Match.CurrentRound.ToString());
            if (State.Match.CurrentRound == State.Match.TotalRounds)
            {
                State.Match.Status=MatchStatus.Completed;
                ChangePhase(RoundPhase.MatchEnd);
                Logged?.Invoke(GameSignal.MatchCompleted);
                Record("MATCH_COMPLETED", "");
                ValidateAndNotify(); return;
            }
            ChangePhase(RoundPhase.NextRound);
            State.Match.CurrentRound++;
            State.Match.CurrentRoundState=new RoundState { RoundNumber=State.Match.CurrentRound, Phase=RoundPhase.NextRound };
            ChangePhase(RoundPhase.Starting);
            PrepareRound();
            ValidateAndNotify();
        }

        public IReadOnlyList<PlayerViewData> GetStandings()
        {
            return State.Match.Players.OrderByDescending(p => p.MatchScore).ThenBy(p => p.PlayerId)
                .Select(p => new PlayerViewData { PlayerId=p.PlayerId, DisplayName=p.DisplayName, MatchScore=p.MatchScore, Connected=p.Connected }).ToList().AsReadOnly();
        }

        public IReadOnlyList<PlayerViewData> GetPublicPlayers()
        {
            return State.Match.Players.Select(p => new PlayerViewData { PlayerId=p.PlayerId, DisplayName=p.DisplayName, MatchScore=p.MatchScore, Connected=p.Connected }).ToList().AsReadOnly();
        }

        public PrivatePlayerViewData GetPrivateView(int playerId, int authenticatedRequesterId)
        {
            var phase=State.Match.CurrentRoundState.Phase;
            if (phase != RoundPhase.PrivateReveal && phase != RoundPhase.Discussion) throw new InvalidOperationException("Private information is unavailable in this phase.");
            if (playerId != authenticatedRequesterId) throw new InvalidOperationException("Private information is available only to its owning player.");
            var player=State.Match.Players.FirstOrDefault(p => p.PlayerId == playerId);
            if (player == null) throw new ArgumentOutOfRangeException(nameof(playerId));
            return new PrivatePlayerViewData
            {
                PlayerId=player.PlayerId, Role=player.Role, IsThief=player.IsThief, Situation=player.Knowledge.Situation,
                Clues=new List<string>().AsReadOnly(), EvidenceIds=new List<string>(player.Knowledge.EvidenceIds).AsReadOnly(),
                KnownFactIds=new List<string>(player.Knowledge.FactIds).AsReadOnly(), UnknownFactIds=new List<string>(player.Knowledge.UnknownFactIds).AsReadOnly(),
                FalseFactIds=new List<string>(player.Knowledge.FalseFactIds).AsReadOnly(), Objective=player.Knowledge.Objective,
                ThiefObjective=player.ThiefObjective, RoleScoreValue=RuntimeScoreService.GetRoleValue(player.Role)
            };
        }

        public IReadOnlyList<EvidenceDefinition> GetPrivateEvidence(int playerId, int authenticatedRequesterId)
        {
            var phase=State.Match.CurrentRoundState.Phase;
            if (phase != RoundPhase.PrivateReveal && phase != RoundPhase.Discussion) throw new InvalidOperationException("Private evidence is unavailable in this phase.");
            if (playerId != authenticatedRequesterId) throw new InvalidOperationException("Private evidence is available only to its owning player.");
            var player=State.Match.Players.FirstOrDefault(p => p.PlayerId == playerId);
            if (player == null) throw new ArgumentOutOfRangeException(nameof(playerId));
            var owned=new HashSet<string>(player.Knowledge.EvidenceIds, StringComparer.Ordinal);
            return State.Scenario.Truth.Evidence.Where(e => e != null && owned.Contains(e.EvidenceId))
                .Select(e => new EvidenceDefinition { EvidenceId=e.EvidenceId, Description=e.Description, SupportsFactIds=new List<string>(e.SupportsFactIds ?? new List<string>()), ContradictsFactIds=new List<string>(e.ContradictsFactIds ?? new List<string>()), RequiredEventIds=new List<string>(e.RequiredEventIds ?? new List<string>()), Independent=e.Independent, Critical=e.Critical, IsAmbiguous=e.IsAmbiguous, IsFalseLead=e.IsFalseLead, OwnerPlayerId=e.OwnerPlayerId })
                .ToList().AsReadOnly();
        }

        public GameSnapshot CaptureSnapshot()
        {
            var snapshot=new SnapshotService().Capture(State);
            Logged?.Invoke(GameSignal.SnapshotCreated);
            Record("SNAPSHOT_CREATED", snapshot.Round + ":" + snapshot.Phase);
            return snapshot;
        }

        public ReplayRecord GetReplayRecord() => replay.GetRecord();

        public IReadOnlyList<CoreLogEvent> GetCoreLog()
        {
            return coreLog.Select(item => item.DeepCopy()).ToList().AsReadOnly();
        }

        private void PrepareRound()
        {
            try { PrepareRoundCore(); }
            catch
            {
                var scenarioRejected=State.Scenario == null || !State.Scenario.Validated;
                State.Match.Status=MatchStatus.Aborted;
                if (scenarioRejected)
                {
                    Logged?.Invoke(GameSignal.ScenarioValidationFailed);
                    Record("SCENARIO_VALIDATION_FAILED", "");
                }
                throw;
            }
        }

        private void PrepareRoundCore()
        {
            var roundNumber=State.Match.CurrentRound;
            claims.Clear();
            var context=new RoundContext(State.Match.Seed, roundNumber, DifficultyConfig.Create(State.Match.Difficulty));
            Logged?.Invoke(GameSignal.RoundContextCreated);
            Logged?.Invoke(GameSignal.RoundSeedCreated);
            Record("ROUND_CONTEXT_CREATED", roundNumber + ":" + context.RoundSeed);
            State.Scenario=roundFactory.CreateScenario(context, State.Match.Players.Count);
            Record("SCENARIO_GENERATED", State.Scenario.ScenarioId);
            Logged?.Invoke(GameSignal.ScenarioBuildStarted);
            var truth=State.Scenario.Truth;
            if (string.IsNullOrWhiteSpace(truth.ScenarioId) || truth.Timeline == null || truth.Facts == null || truth.Evidence == null || truth.Opportunities == null || truth.PlayerInformation == null || truth.Evidence.Any(e => e == null) || truth.PlayerInformation.Any(p => p == null))
                throw new InvalidOperationException("Generated scenario is missing required truth collections or entries.");
            roles.AssignRoles(State.Match.Players, context.RoundSeed);
            Logged?.Invoke(GameSignal.RolesAssigned);
            var selected=new List<PlayerInformationBundle>();
            var config=context.Difficulty;
            var ambiguous=truth.Evidence.Where(e => e.IsAmbiguous).OrderBy(e => e.EvidenceId, StringComparer.Ordinal).ToList();
            var falseLeads=truth.Evidence.Where(e => e.IsFalseLead).OrderBy(e => e.EvidenceId, StringComparer.Ordinal).ToList();
            foreach (var player in State.Match.Players)
            {
                var key=player.IsThief ? RoleType.Thief : player.Role;
                var template=truth.PlayerInformation.FirstOrDefault(p => p != null && p.Role == key);
                if (template == null) throw new InvalidOperationException("Scenario is missing player information for " + key + ".");
                if (template.EvidenceIds == null || template.EvidenceIds.Count < config.EvidencePerPlayer) throw new InvalidOperationException("Scenario does not provide enough base evidence for " + key + ".");
                var evidenceIds=template.EvidenceIds.Take(config.EvidencePerPlayer).ToList();
                AddDifficultyEvidence(evidenceIds, ambiguous, config.AmbiguousEvidenceCount, player.PlayerId);
                AddDifficultyEvidence(evidenceIds, falseLeads, config.FalseLeadCount, player.PlayerId);
                var bundle=new PlayerInformationBundle { PlayerId=player.PlayerId.ToString(), Role=key, PrivateSituation=template.PrivateSituation, EvidenceIds=evidenceIds, KnownFactIds=new List<string>(template.KnownFactIds ?? new List<string>()), UnknownFactIds=new List<string>(template.UnknownFactIds ?? new List<string>()), FalseFactIds=new List<string>(template.FalseFactIds ?? new List<string>()), Objective=player.IsThief ? "Avoid detection and survive the vote." : "Determine who stole the Necklace." };
                selected.Add(bundle);
                player.ThiefObjective=player.IsThief ? ThiefObjective.MaintainAlibi : (ThiefObjective?)null;
                player.Knowledge=new PrivateKnowledgeState { Situation=bundle.PrivateSituation, FactIds=new List<string>(bundle.KnownFactIds), UnknownFactIds=new List<string>(bundle.UnknownFactIds), FalseFactIds=new List<string>(bundle.FalseFactIds), EvidenceIds=new List<string>(bundle.EvidenceIds), Objective=bundle.Objective };
            }
            truth.PlayerInformation=selected;
            foreach (var window in truth.Opportunities.Where(o => o != null && o.PlayerId == "Thief")) window.PlayerId=State.Match.Players.Single(p => p.IsThief).PlayerId.ToString();
            State.Scenario.SetTruth(truth);
            Logged?.Invoke(GameSignal.ScenarioValidationStarted);
            Record("SCENARIO_VALIDATION_STARTED", "");
            scenarioPipeline.ValidateOrThrow(State.Scenario, State.Match.Players.Count, config);
            Record("SCENARIO_VALIDATION_PASSED", State.Scenario.ScenarioId);
            State.Match.CurrentRoundState.RoundNumber=roundNumber;
            State.Match.CurrentRoundState.ScenarioId=State.Scenario.ScenarioId;
            State.Match.CurrentRoundState.VoteRound=0;
            State.Match.CurrentRoundState.Resolution=VoteResolution.Undecided;
            State.Match.CurrentRoundState.ThiefEscaped=false;
            State.Match.CurrentRoundState.AccusedPlayerId=null;
            State.Match.CurrentRoundState.Completed=false;
            State.Match.CurrentRoundState.ScoringResolved=false;
            State.Match.CurrentRoundState.RunoffCandidates.Clear();
            State.Votes.Reset(); State.Votes.VoteRound=0;
            State.Scores.RoundScores.Clear();
            foreach (var player in State.Match.Players) { player.Vote.Reset(); player.RoundScore=0; }
            if (State.Match.CurrentRoundState.Phase != RoundPhase.Starting) State.Match.CurrentRoundState.Phase=RoundPhase.Starting;
            ChangePhase(RoundPhase.PrivateReveal);
            Logged?.Invoke(GameSignal.ScenarioBuildCompleted);
            Logged?.Invoke(GameSignal.RoundValidated);
            Logged?.Invoke(GameSignal.RoundCreated);
            Logged?.Invoke(GameSignal.PrivateKnowledgeAssigned);
            Record("ROUND_CREATED", roundNumber + ":" + State.Scenario.ScenarioId);
            ValidateAndNotify();
        }

        private static void AddDifficultyEvidence(List<string> destination, List<EvidenceDefinition> pool, int count, int playerId)
        {
            if (count == 0) return;
            if (pool.Count == 0) throw new InvalidOperationException("Scenario lacks evidence required by its difficulty configuration.");
            for (var i=0; i<count; i++)
            {
                var evidence=pool[(playerId - 1 + i) % pool.Count];
                if (!destination.Contains(evidence.EvidenceId)) destination.Add(evidence.EvidenceId);
            }
        }

        private void ChangePhase(RoundPhase next)
        {
            var current=State.Match.CurrentRoundState.Phase;
            try { transitions.RequireTransition(current, next); }
            catch { Logged?.Invoke(GameSignal.IllegalStateTransition); Record("ILLEGAL_STATE_TRANSITION", current + ">" + next); throw; }
            State.Match.CurrentRoundState.Phase=next;
            Logged?.Invoke(GameSignal.PhaseChanged);
            PhaseChanged?.Invoke(next);
            Record("PHASE_CHANGED", next.ToString());
        }

        private void ValidateAndNotify()
        {
            try { invariants.Validate(State); invariants.ValidateScores(State); }
            catch { Logged?.Invoke(GameSignal.StateInvariantFailed); Record("STATE_VALIDATION_FAILED", ""); throw; }
            StateChanged?.Invoke();
        }

        private void EnsureMatchActive()
        {
            if (State.Match.Status == MatchStatus.Completed) throw new InvalidOperationException("Match has already completed.");
            if (State.Match.Status != MatchStatus.InProgress) throw new InvalidOperationException("Match is not active.");
        }
        private void EnsureRoundNotCompleted() { if (State.Match.CurrentRoundState.Completed) throw new InvalidOperationException("Round has already completed."); }
        private void RequirePhase(RoundPhase phase) { if (State.Match.CurrentRoundState.Phase != phase) throw new InvalidOperationException("Expected " + phase + ", current phase is " + State.Match.CurrentRoundState.Phase + "."); }
        private void Record(string eventType, string data)
        {
            replay.Record(State.Match.CurrentRound, eventType, data);
            var item = new CoreLogEvent
            {
                TimestampUtc = DateTime.UtcNow,
                Level = CoreLogLevel.Info,
                EventType = eventType,
                MatchId = State.Match.MatchId,
                Round = State.Match.CurrentRound,
                // Keep operational logs free of roles, private clues, and world truth.
                Message = eventType
            };
            coreLog.Add(item);
            CoreEventLogged?.Invoke(item.DeepCopy());
            Logged?.Invoke(GameSignal.ReplayEventRecorded);
        }

        private static GameRuntimeState CreateInitialState(int seed, int totalRounds, DifficultyLevel difficulty, IReadOnlyList<string> players)
        {
            var state=new GameRuntimeState();
            state.Match=new MatchState { MatchId=Guid.NewGuid().ToString("N"), Seed=seed, CurrentRound=1, TotalRounds=totalRounds, Difficulty=difficulty, Status=MatchStatus.Created, CurrentRoundState=new RoundState { RoundNumber=1, Phase=RoundPhase.Waiting } };
            for (var i=0; i<players.Count; i++) state.Match.Players.Add(new PlayerState { PlayerId=i+1, DisplayName=players[i] });
            return state;
        }
    }
}
