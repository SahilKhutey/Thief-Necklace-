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
        private readonly InformationProjectionService informationProjections = new InformationProjectionService();
        private readonly InformationIntegrityValidator informationValidator = new InformationIntegrityValidator();
        private readonly PrivateDataService privateData = new PrivateDataService();
        private readonly PlayerKnowledgeService knowledgeService = new PlayerKnowledgeService();
        private readonly RoundResolver roundResolver;
        private readonly FinalizedVoteResolver finalizedVoteResolver = new FinalizedVoteResolver();
        private readonly VoteResolutionConfig voteResolutionConfig;
        private readonly Dictionary<string, VoteResolutionCommit> voteResolutionHistory = new Dictionary<string, VoteResolutionCommit>(StringComparer.Ordinal);
        private readonly RoundFactory roundFactory;
        private readonly ScenarioBuildPipeline scenarioPipeline;
        private readonly RoleAssignmentService roles = new RoleAssignmentService();
        private readonly ReplayRecorder replay;
        private readonly ClaimService claims = new ClaimService();
        private readonly DiscussionTimer discussionTimer = new DiscussionTimer();
        private readonly DiscussionService discussion;
        private readonly VotingService voting;
        private readonly List<CoreLogEvent> coreLog = new List<CoreLogEvent>();
        private readonly bool requirePrivateInformationAcknowledgement;
        private bool revealCompleted;

        public GameRuntimeState State { get; }
        public event Action<GameSignal> Logged;
        public event Action<CoreLogEvent> CoreEventLogged;
        public event Action<RoundPhase> PhaseChanged;
        public event Action StateChanged;
        public event Action<KnowledgeAdded> KnowledgeChanged;
        public event Action<DiscussionEvent> DiscussionEventPublished;
        public event Action<VoteEventType, int, VoteRejectionReason> VoteEventPublished;
        public event Action<VoteResolutionResult> VoteResolutionPublished;
        public VoteResolutionResult LastVoteResolutionResult { get; private set; }
        public IReadOnlyList<VoteResolutionResult> VoteResolutionHistory => voteResolutionHistory.Values.Select(item => item.Result).ToList().AsReadOnly();

        public MatchController(IScenarioGenerator scenarioGenerator, int seed, int totalRounds, DifficultyLevel difficulty,
            IReadOnlyList<string> players, bool requirePrivateInformationAcknowledgement = false, DiscussionRateConfig discussionConfig = null, VoteResolutionConfig voteConfig = null)
        {
            if (scenarioGenerator == null) throw new ArgumentNullException(nameof(scenarioGenerator));
            if (players == null) throw new ArgumentNullException(nameof(players));
            if (players.Count < 4 || players.Count > 6) throw new ArgumentException("Match requires 4–6 players.", nameof(players));
            if (players.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Player names must not be empty.", nameof(players));
            if (totalRounds < 1) throw new ArgumentOutOfRangeException(nameof(totalRounds));
            DifficultyConfig.Create(difficulty);
            this.requirePrivateInformationAcknowledgement=requirePrivateInformationAcknowledgement;
            voteResolutionConfig=voteConfig ?? new VoteResolutionConfig(); voteResolutionConfig.Validate();
            discussion=new DiscussionService(discussionConfig);
            voting=new VotingService();
            voting.EventPublished += (type, playerId, reason) => { VoteEventPublished?.Invoke(type, playerId, reason); Record("VOTE_" + type.ToString().ToUpperInvariant(), playerId + ":" + reason); };
            if (discussion.AllowEarlyVote && discussion.EarlyVoteConfirmationsRequired > players.Count)
                throw new ArgumentOutOfRangeException(nameof(discussionConfig), "Early-vote confirmations cannot exceed the match size.");
            var scenarioConfig=new ScenarioGenerationConfig();
            roundFactory=new RoundFactory(scenarioGenerator, scenarioConfig);
            scenarioPipeline=new ScenarioBuildPipeline(new ScenarioValidator(), scenarioConfig);
            roundResolver=new RoundResolver(votes, voteResolver, scores);
            State=CreateInitialState(seed, totalRounds, difficulty, players);
            replay=new ReplayRecorder(State.Match.MatchId, seed);
            Record("CORE_BOOT", "");
            claims.ClaimAdded += claim => Logged?.Invoke(GameSignal.ClaimCreated);
            claims.ClaimWasRetracted += claim => Logged?.Invoke(GameSignal.ClaimRetracted);
            discussion.EventPublished += item => { DiscussionEventPublished?.Invoke(item); Record("DISCUSSION_" + item.Type.ToString().ToUpperInvariant(), item.Sequence + ":" + item.RelatedObjectId); };
            knowledgeService.KnowledgeAddedEvent += item => KnowledgeChanged?.Invoke(item);
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

        public void SetPlayerConnected(int playerId, bool connected)
        {
            EnsureMatchActive();
            var player=State.Match.Players.FirstOrDefault(p => p.PlayerId == playerId);
            if (player == null) throw new ArgumentOutOfRangeException(nameof(playerId));
            if (player.Connected == connected) return;
            player.Connected=connected;
            if (!connected) player.PrivateInformationAcknowledged=false;
            Record("PLAYER_CONNECTION_CHANGED", playerId + ":" + (connected ? "connected" : "disconnected"));
            ValidateAndNotify();
        }

        public void AbortMatch()
        {
            if (State.Match.Status == MatchStatus.Completed || State.Match.Status == MatchStatus.Aborted)
                throw new InvalidOperationException("A completed or aborted match cannot be aborted again.");
            State.Match.Status=MatchStatus.Aborted;
            Logged?.Invoke(GameSignal.MatchAborted);
            Record("MATCH_ABORTED", "");
            StateChanged?.Invoke();
        }

        public void BeginDiscussion()
        {
            EnsureMatchActive(); EnsureRoundNotCompleted(); RequirePhase(RoundPhase.PrivateReveal);
            if (requirePrivateInformationAcknowledgement)
            {
                var connectedPlayers = State.Match.Players.Where(player => player.Connected).ToArray();
                if (connectedPlayers.Length == 0 || connectedPlayers.Any(player => !player.PrivateInformationAcknowledged))
                    throw new InvalidOperationException("At least one connected player must be present and every connected player must acknowledge private information before discussion.");
            }
            ChangePhase(RoundPhase.Discussion);
            discussion.Start(State.Match.Players.Select(player => player.PlayerId));
            discussionTimer.Start(discussion.DurationSeconds);
            Logged?.Invoke(GameSignal.DiscussionStarted);
            Record("DISCUSSION_STARTED", ""); ValidateAndNotify();
        }

        public bool AcknowledgePrivateInformation(int playerId)
        {
            EnsureMatchActive(); EnsureRoundNotCompleted(); RequirePhase(RoundPhase.PrivateReveal);
            var player=State.Match.Players.FirstOrDefault(p => p.PlayerId == playerId);
            if (player == null) throw new ArgumentOutOfRangeException(nameof(playerId));
            if (!player.Connected) throw new InvalidOperationException("A disconnected player cannot acknowledge private information.");
            if (player.PrivateInformationAcknowledged) return false;
            player.PrivateInformationAcknowledged=true;
            Record("PRIVATE_INFORMATION_ACKNOWLEDGED", playerId.ToString());
            StateChanged?.Invoke();
            Logged?.Invoke(GameSignal.PrivateInfoAcknowledged);
            return true;
        }

        public void AdvanceDiscussionTimer(float deltaSeconds)
        {
            EnsureMatchActive();
            if (State.Match.CurrentRoundState.Phase == RoundPhase.Discussion) discussionTimer.Advance(deltaSeconds);
        }

        public void AdvanceVotingTimer() { if (State.Match.Status == MatchStatus.InProgress && State.Match.CurrentRoundState.Phase == RoundPhase.Voting) voting.Poll(); }
        public int RemainingVotingSeconds => voting.RemainingSeconds;
        public VoteStatus VotingStatus { get { voting.Poll(); return voting.Status; } }
        public long VotingStartTimestamp => voting.StartTimestamp;
        public long VotingEndTimestamp => voting.EndTimestamp;
        public FinalizedVoteCollection GetFinalizedVotes() => voting.GetFinalizedVotes();
        public IReadOnlyList<FinalizedVoteCollection> GetFinalizedVoteHistory() => voting.GetFinalizedVoteHistory();
        public IReadOnlyList<VoteRecord> GetVotesForAuthorityOnly() => voting.GetAuthorityRecords();
        public VoteProgressViewData GetVoteProgress() => voting.GetProgress();
        public bool HasSubmittedVote(int voterId) => voting.HasSubmitted(voterId);

        public IReadOnlyList<int> GetEligibleVoteTargets(int voterId)
        {
            var voter=State.Match.Players.FirstOrDefault(item => item.PlayerId == voterId);
            if (voter == null || !voter.Connected || State.Match.CurrentRoundState.Phase != RoundPhase.Voting || voting.Status != VoteStatus.Open || voting.HasSubmitted(voterId))
                return new List<int>().AsReadOnly();
            var targets=State.Match.Players.Where(item => item.Connected && item.PlayerId != voterId);
            if (State.Match.CurrentRoundState.VoteRound > 0 && voteResolutionConfig.RevotePolicy == RevoteTargetPolicy.TiedPlayersOnly) targets=targets.Where(item => State.Match.CurrentRoundState.RunoffCandidates.Contains(item.PlayerId));
            return targets.Select(item => item.PlayerId).OrderBy(id => id).ToList().AsReadOnly();
        }

        public void BeginVoting()
        {
            EnsureMatchActive(); EnsureRoundNotCompleted(); RequirePhase(RoundPhase.Discussion);
            discussionTimer.Pause();
            discussion.End();
            Logged?.Invoke(GameSignal.DiscussionEnded);
            Record("DISCUSSION_COMPLETED", "");
            State.Votes.Reset();
            foreach (var player in State.Match.Players) player.Vote.Reset();
            voting.Start(State.Match.Players.Select(player => player.PlayerId), State.Match.CurrentRoundState.VoteRound + 1, discussion.VotingDurationSeconds,
                State.Match.MatchId, State.Match.MatchId + "/round/" + State.Match.CurrentRound);
            State.Votes.VoteRound=State.Match.CurrentRoundState.VoteRound;
            ChangePhase(RoundPhase.Voting);
            voting.AnnounceStarted();
            Record("VOTING_STARTED", ""); ValidateAndNotify();
        }

        public bool SubmitVote(int voterId, int targetId)
            => SubmitVote(voterId, targetId, State.Match.CurrentRoundState.VoteRound + 1).Accepted;

        public VoteSubmissionResult SubmitVote(int voterId, int targetId, int voteSequence)
        {
            EnsureMatchActive(); EnsureRoundNotCompleted();
            if (State.Match.CurrentRoundState.Phase != RoundPhase.Voting)
            {
                var rejected=voting.Reject(voterId, VoteRejectionReason.WrongPhase);
                Logged?.Invoke(GameSignal.VoteRejected); ValidateAndNotify(); return rejected;
            }
            var voter=State.Match.Players.FirstOrDefault(player => player.PlayerId == voterId);
            var result=voting.Submit(voterId, targetId, voteSequence, voter != null && voter.Connected, GetEligibleVoteTargets(voterId));
            var accepted=result.Accepted;
            if (accepted) { State.Match.Players.First(player => player.PlayerId == voterId).Vote.Submitted=true; State.Match.Players.First(player => player.PlayerId == voterId).Vote.TargetPlayerId=targetId; State.Votes.Votes[voterId]=targetId; }
            Logged?.Invoke(accepted ? GameSignal.VoteSubmitted : GameSignal.VoteRejected);
            if (accepted) Record("VOTE_SUBMITTED", voterId + ">" + targetId);
            else Record("VOTE_REJECTED", result.Reason.ToString());
            ValidateAndNotify(); return result;
        }

        public IReadOnlyList<PlayerClaim> Claims => discussion.CaptureSnapshot().Claims;

        public DiscussionSnapshot GetDiscussionSnapshot(long afterSequence = 0, int pageSize = 100) => discussion.CaptureSnapshot(afterSequence, pageSize);

        public DiscussionMessage SendChatMessage(int playerId, string text)
        {
            EnsureMatchActive(); EnsureRoundNotCompleted();
            var player=RequireDiscussionPlayer(playerId);
            return discussion.SendMessage(playerId, text, player.Connected, State.Match.CurrentRoundState.Phase);
        }

        public PlayerQuestion SubmitQuestion(int fromPlayerId, int toPlayerId, string text)
        {
            EnsureMatchActive(); EnsureRoundNotCompleted();
            var player=RequireDiscussionPlayer(fromPlayerId);
            return discussion.Ask(fromPlayerId, toPlayerId, text, player.Connected, State.Match.Players.Any(p => p.PlayerId == toPlayerId), State.Match.CurrentRoundState.Phase);
        }

        public DiscussionResponse RespondToQuestion(int playerId, string questionId, string text)
        {
            EnsureMatchActive(); EnsureRoundNotCompleted();
            var player=RequireDiscussionPlayer(playerId);
            return discussion.Respond(questionId, playerId, text, player.Connected, State.Match.CurrentRoundState.Phase);
        }

        public ClaimChallenge ChallengeClaim(int playerId, string claimId, string text)
        {
            EnsureMatchActive(); EnsureRoundNotCompleted();
            var player=RequireDiscussionPlayer(playerId);
            return discussion.Challenge(claimId, playerId, text, player.Connected, State.Match.CurrentRoundState.Phase);
        }

        public void UpdatePlayerHypothesis(int playerId, IEnumerable<string> candidates, IEnumerable<string> supported, IEnumerable<string> rejected)
        {
            EnsureMatchActive(); EnsureRoundNotCompleted(); RequirePhase(RoundPhase.Discussion);
            if (!RequireDiscussionPlayer(playerId).Connected) throw new InvalidOperationException("Disconnected players cannot update discussion hypotheses.");
            var validIds=new HashSet<string>(State.Match.Players.Select(player => player.PlayerId.ToString()), StringComparer.Ordinal);
            var candidateList=(candidates ?? Enumerable.Empty<string>()).ToList();
            if (candidateList.Any(id => !validIds.Contains(id))) throw new ArgumentException("Hypothesis candidate references a player outside the match.", nameof(candidates));
            discussion.UpdateHypothesis(playerId, candidateList, supported, rejected);
        }

        public bool RequestEarlyVote(int playerId)
        {
            EnsureMatchActive(); EnsureRoundNotCompleted();
            var player=RequireDiscussionPlayer(playerId);
            var shouldEnd=discussion.RequestEarlyVote(playerId, player.Connected, State.Match.CurrentRoundState.Phase);
            if (shouldEnd) BeginVoting();
            return shouldEnd;
        }

        public PlayerClaim AddClaim(int playerId, string factId, string statement, float gameTime)
            => AddClaim(playerId, factId, statement, gameTime, string.IsNullOrWhiteSpace(factId) ? ClaimType.General : ClaimType.Timeline);

        public PlayerClaim AddClaim(int playerId, string factId, string statement, float gameTime, ClaimType type)
        {
            EnsureMatchActive(); EnsureRoundNotCompleted(); RequirePhase(RoundPhase.Discussion);
            if (!State.Match.Players.Any(p => p.PlayerId == playerId)) throw new ArgumentOutOfRangeException(nameof(playerId));
            if (!Enum.IsDefined(typeof(ClaimType), type)) throw new ArgumentOutOfRangeException(nameof(type));
            if (!string.IsNullOrWhiteSpace(factId) && !State.Scenario.Truth.Facts.Any(f => f != null && f.FactId == factId)) throw new ArgumentException("Claim references an unknown fact.", nameof(factId));
            var player=RequireDiscussionPlayer(playerId);
            var claim=discussion.PublishClaim(playerId, factId, statement, gameTime, type,
                player.Connected, State.Match.CurrentRoundState.Phase);
            claims.RegisterClaim(claim);
            Record("CLAIM_CREATED", playerId.ToString());
            return claim;
        }

        public bool RetractClaim(string claimId)
        {
            EnsureMatchActive(); EnsureRoundNotCompleted(); RequirePhase(RoundPhase.Discussion);
            var retracted=claims.RetractClaim(claimId);
            if (retracted) discussion.RetractClaim(claimId);
            if (retracted) Record("CLAIM_RETRACTED", "");
            return retracted;
        }

        public bool GrantKnownFact(int playerId, string factId, KnowledgeSource source)
        {
            EnsureMatchActive();
            var changed=knowledgeService.GrantKnownFact(State, playerId, factId, source);
            if (changed) { Record("KNOWLEDGE_FACT_ADDED", playerId.ToString()); StateChanged?.Invoke(); }
            return changed;
        }

        public bool GrantKnownFalseFact(int playerId, string factId, KnowledgeSource source)
        {
            EnsureMatchActive();
            var changed=knowledgeService.GrantKnownFalseFact(State, playerId, factId, source);
            if (changed) { Record("KNOWLEDGE_FALSE_FACT_ADDED", playerId.ToString()); StateChanged?.Invoke(); }
            return changed;
        }

        public bool GrantEvidence(int playerId, string evidenceId, KnowledgeSource source)
        {
            EnsureMatchActive();
            var changed=knowledgeService.GrantEvidence(State, playerId, evidenceId, source);
            if (changed) { Record("KNOWLEDGE_EVIDENCE_ADDED", playerId.ToString()); StateChanged?.Invoke(); }
            return changed;
        }

        public bool GrantObservedEvent(int playerId, string eventId, KnowledgeSource source)
        {
            EnsureMatchActive();
            var changed=knowledgeService.GrantObservedEvent(State, playerId, eventId, source);
            if (changed) { Record("KNOWLEDGE_EVENT_ADDED", playerId.ToString()); StateChanged?.Invoke(); }
            return changed;
        }

        public bool GrantKnownLocation(int playerId, string locationId, KnowledgeSource source)
        {
            EnsureMatchActive();
            var changed=knowledgeService.GrantKnownLocation(State, playerId, locationId, source);
            if (changed) { Record("KNOWLEDGE_LOCATION_ADDED", playerId.ToString()); StateChanged?.Invoke(); }
            return changed;
        }

        public bool GrantKnownPlayer(int playerId, string knownPlayerId, KnowledgeSource source)
        {
            EnsureMatchActive();
            var changed=knowledgeService.GrantKnownPlayer(State, playerId, knownPlayerId, source);
            if (changed) { Record("KNOWLEDGE_PLAYER_ADDED", playerId.ToString()); StateChanged?.Invoke(); }
            return changed;
        }

        public bool RecordBelief(int playerId, string factId, string sourceClaimId, float confidence)
        {
            EnsureMatchActive(); EnsureRoundNotCompleted(); RequirePhase(RoundPhase.Discussion);
            if (!claims.Claims.Any(claim => claim.ClaimId == sourceClaimId && claim.FactId == factId))
                throw new InvalidOperationException("A belief must reference a public claim about that fact.");
            var changed=knowledgeService.RecordBelief(State, playerId, factId, sourceClaimId, confidence);
            if (changed) { Record("PLAYER_BELIEF_RECORDED", playerId.ToString()); StateChanged?.Invoke(); }
            return changed;
        }

        public ContradictionResult EvaluateClaimContradictions()
        {
            EnsureMatchActive(); RequirePhase(RoundPhase.Discussion);
            var truth=State.Scenario.Truth;
            foreach (var claim in discussion.CaptureSnapshot().Claims.Where(c => !c.Retracted && !string.IsNullOrWhiteSpace(c.FactId)))
            {
                var player=State.Match.Players.FirstOrDefault(p => p.PlayerId == claim.PlayerId);
                var contradiction=discussion.EvaluateStructuredClaim(claim, truth, player == null ? Enumerable.Empty<string>() : player.Knowledge.EvidenceIds);
                if (contradiction != null) Logged?.Invoke(GameSignal.ContradictionDetected);
            }
            var result=new ContradictionResult { ContradictionFound=discussion.HasEvidenceBackedContradictions,
                Explanation=discussion.HasEvidenceBackedContradictions ? "Structured claims conflict with traceable evidence known to the claimant." : "No evidence-backed structured claim conflict was found." };
            return result;
        }

        private PlayerState RequireDiscussionPlayer(int playerId)
        {
            var player=State.Match.Players.FirstOrDefault(p => p.PlayerId == playerId);
            if (player == null) throw new ArgumentOutOfRangeException(nameof(playerId), "Player does not belong to this match.");
            return player;
        }

        public VoteResolution ResolveVote() => ResolveVote(State.Match.CurrentRoundState.VoteRound + 1);

        public VoteResolution ResolveVote(int voteSequence)
        {
            EnsureMatchActive(); EnsureRoundNotCompleted();
            var resolutionKey=State.Match.CurrentRound + ":" + voteSequence;
            if (voteResolutionHistory.TryGetValue(resolutionKey, out var prior)) return prior.LegacyOutcome;
            var round=State.Match.CurrentRoundState;
            var currentSequence=round.VoteRound + 1;
            if (voteSequence != currentSequence) throw new VoteResolutionException(VoteResolutionError.InvalidSequence, "Vote resolution request is stale or references another vote instance.");
            if (round.Phase != RoundPhase.Voting) throw new VoteResolutionException(VoteResolutionError.WrongPhase, "Vote resolution requires the voting phase.");
            voting.Poll();
            var collection=voting.GetFinalizedVotes();
            if (collection == null) throw new VoteResolutionException(VoteResolutionError.NoCollection, "Voting must close before resolution.");
            var result=finalizedVoteResolver.Resolve(collection, new VoteResolutionContext(round.TieCount, voteResolutionConfig),
                State.Match.Players.Select(player => player.PlayerId), State.Match.Players.Select(player => player.PlayerId), currentSequence, round.RunoffCandidates,
                State.Match.MatchId, State.Match.MatchId + "/round/" + State.Match.CurrentRound);
            LastVoteResolutionResult=result;

            if (result.Outcome == ResolutionOutcome.Pending)
            {
                voteResolutionHistory[resolutionKey]=new VoteResolutionCommit(result, VoteResolution.Undecided);
                VoteResolutionPublished?.Invoke(result); Record("VOTE_RESOLUTION_PENDING", result.Type.ToString()); ValidateAndNotify();
                return VoteResolution.Undecided;
            }

            if (result.Type == VoteResolutionType.Tie && result.HighestTargets.Count < 2)
                throw new VoteResolutionException(VoteResolutionError.InvalidState, "A tie resolution requires at least two highest targets.");

            var legacyFinalVote=result.Outcome == ResolutionOutcome.ThiefEscaped;
            State.Votes.FinalVote=legacyFinalVote;
            ChangePhase(RoundPhase.VoteResolution);
            var legacyOutcome=roundResolver.ResolveVotes(State, legacyFinalVote, true);
            round.Resolution=legacyOutcome;
            round.TieCount=result.TieCount;
            round.AccusedPlayerId=result.SelectedTargetId;
            if (result.Outcome == ResolutionOutcome.ThiefEscaped) { round.ThiefEscaped=true; round.AccusedPlayerId=null; }
            VoteResolutionPublished?.Invoke(result);
            voteResolutionHistory[resolutionKey]=new VoteResolutionCommit(result, legacyOutcome);
            Logged?.Invoke(GameSignal.VoteResolved); Logged?.Invoke(GameSignal.VotesComplete);
            if (result.Type == VoteResolutionType.Tie) Logged?.Invoke(GameSignal.VoteTie);
            Record("VOTE_RESOLVED", result.Outcome + ":" + result.Type);

            if (result.Outcome == ResolutionOutcome.RevoteRequired)
            {
                round.RunoffCandidates=result.HighestTargets.ToList();
                round.VoteRound++;
                State.Votes.VoteRound=round.VoteRound;
                State.Votes.Reset();
                foreach (var player in State.Match.Players) player.Vote.Reset();
                Logged?.Invoke(GameSignal.RevoteStarted);
                Record("REVOTE_STARTED", string.Join(",", round.RunoffCandidates));
                voting.Start(State.Match.Players.Select(player => player.PlayerId), round.VoteRound + 1, discussion.VotingDurationSeconds,
                    State.Match.MatchId, State.Match.MatchId + "/round/" + State.Match.CurrentRound);
                ChangePhase(RoundPhase.Voting); voting.AnnounceStarted();
            }
            else
            {
                round.RunoffCandidates.Clear(); revealCompleted=false;
                Logged?.Invoke(GameSignal.RevealStarted); ChangePhase(RoundPhase.Reveal);
            }
            ValidateAndNotify(); return legacyOutcome;
        }

        private sealed class VoteResolutionCommit
        {
            public VoteResolutionResult Result { get; }
            public VoteResolution LegacyOutcome { get; }
            public VoteResolutionCommit(VoteResolutionResult result, VoteResolution legacyOutcome) { Result=result; LegacyOutcome=legacyOutcome; }
        }

        public void CompleteReveal()
        {
            EnsureMatchActive(); EnsureRoundNotCompleted(); RequirePhase(RoundPhase.Reveal);
            if (revealCompleted) throw new InvalidOperationException("Reveal has already completed.");
            revealCompleted=true;
            Logged?.Invoke(GameSignal.RevealCompleted);
            Logged?.Invoke(GameSignal.RoleRevealed);
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
            return informationProjections.BuildPublicPlayers(State.Match).OrderByDescending(p => p.MatchScore).ThenBy(p => p.PlayerId).ToList().AsReadOnly();
        }

        public IReadOnlyList<PlayerViewData> GetPublicPlayers()
        {
            return informationProjections.BuildPublicPlayers(State.Match);
        }

        public PrivatePlayerViewData GetPrivateView(int playerId, int authenticatedRequesterId)
        {
            return informationProjections.BuildPrivatePlayer(State, playerId, authenticatedRequesterId);
        }

        public IReadOnlyList<RevealedPlayerData> GetRevealedPlayers()
            => informationProjections.BuildRevealedPlayers(State, revealCompleted);

        public IReadOnlyList<EvidenceDefinition> GetPrivateEvidence(int playerId, int authenticatedRequesterId)
        {
            return informationProjections.BuildPrivateEvidence(State, playerId, authenticatedRequesterId);
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
            catch (Exception exception)
            {
                var scenarioRejected=State.Scenario == null || !State.Scenario.Validated;
                State.Match.Status=MatchStatus.Aborted;
                if (scenarioRejected)
                {
                    Logged?.Invoke(GameSignal.ScenarioGenerationFailed);
                    Logged?.Invoke(GameSignal.ScenarioValidationFailed);
                    Record("SCENARIO_VALIDATION_FAILED", exception.Message);
                }
                throw;
            }
        }

        private void PrepareRoundCore()
        {
            revealCompleted=false;
            var roundNumber=State.Match.CurrentRound;
            claims.Clear();
            var context=new RoundContext(State.Match.Seed, roundNumber, DifficultyConfig.Create(State.Match.Difficulty));
            Logged?.Invoke(GameSignal.RoundContextCreated);
            Logged?.Invoke(GameSignal.RoundSeedCreated);
            Record("ROUND_CONTEXT_CREATED", roundNumber + ":" + context.RoundSeed);
            Logged?.Invoke(GameSignal.ScenarioBuildStarted);
            State.Scenario=roundFactory.CreateScenario(context, State.Match.Players.Count, candidate => FinalizeScenarioCandidate(candidate, context));
            Record("SCENARIO_GENERATED", State.Scenario.ScenarioId + ":attempt=" + State.Scenario.GenerationAttempt + ":seed=" + State.Scenario.Truth.Seed);
            Logged?.Invoke(GameSignal.RolesAssigned);
            State.Match.CurrentRoundState.RoundNumber=roundNumber;
            State.Match.CurrentRoundState.ScenarioId=State.Scenario.ScenarioId;
            State.Match.CurrentRoundState.VoteRound=0;
            State.Match.CurrentRoundState.TieCount=0;
            LastVoteResolutionResult=null;
            State.Match.CurrentRoundState.Resolution=VoteResolution.Undecided;
            State.Match.CurrentRoundState.ThiefEscaped=false;
            State.Match.CurrentRoundState.AccusedPlayerId=null;
            State.Match.CurrentRoundState.Completed=false;
            State.Match.CurrentRoundState.ScoringResolved=false;
            State.Match.CurrentRoundState.RunoffCandidates.Clear();
            State.Votes.Reset(); State.Votes.VoteRound=0;
            // The next voting instance is created only after discussion ends.
            State.Scores.RoundScores.Clear();
            foreach (var player in State.Match.Players) { player.Vote.Reset(); player.RoundScore=0; player.PrivateInformationAcknowledged=false; }
            if (State.Match.CurrentRoundState.Phase != RoundPhase.Starting) State.Match.CurrentRoundState.Phase=RoundPhase.Starting;
            ChangePhase(RoundPhase.PrivateReveal);
            Logged?.Invoke(GameSignal.ScenarioBuildCompleted);
            Logged?.Invoke(GameSignal.RoundValidated);
            Logged?.Invoke(GameSignal.RoundCreated);
            Logged?.Invoke(GameSignal.PrivateInfoAssigned);
            Record("ROUND_CREATED", roundNumber + ":" + State.Scenario.ScenarioId);
            ValidateAndNotify();
        }

        private void BindScenarioActorIdentities(ScenarioTruthModel truth)
        {
            truth.Actors=truth.Actors ?? new List<ActorState>();
            var identities=new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var player in State.Match.Players)
            {
                identities[player.Role.ToString()]=player.PlayerId.ToString();
                if (player.IsThief) { identities["Thief"]=player.PlayerId.ToString(); truth.ThiefPlayerId=player.PlayerId.ToString(); }
            }
            identities["Candidate"]=State.Match.Players.Where(player => !player.IsThief).OrderBy(player => player.PlayerId).First().PlayerId.ToString();
            foreach (var actor in truth.Actors ?? new List<ActorState>()) if (actor != null && identities.TryGetValue(actor.ActorId, out var actorId)) actor.ActorId=actorId;
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
            foreach (var ev in truth.Timeline ?? new List<TimelineEvent>()) if (ev != null && identities.TryGetValue(ev.ActorId, out var eventActorId)) ev.ActorId=eventActorId;
            foreach (var fact in truth.Facts ?? new List<FactDefinition>()) if (fact != null && identities.TryGetValue(fact.SubjectId, out var subjectId)) fact.SubjectId=subjectId;
            foreach (var opportunity in truth.Opportunities ?? new List<OpportunityWindow>()) if (opportunity != null && identities.TryGetValue(opportunity.PlayerId, out var opportunityPlayerId)) opportunity.PlayerId=opportunityPlayerId;
            foreach (var transition in truth.ObjectTransitions ?? new List<ObjectTransition>()) if (transition != null && identities.TryGetValue(transition.ToActorId, out var holderId)) transition.ToActorId=holderId;
            foreach (var evidence in truth.Evidence ?? new List<EvidenceDefinition>()) if (evidence != null)
            {
                evidence.EliminatesPlayerIds=(evidence.EliminatesPlayerIds ?? new List<string>()).Select(id => identities.TryGetValue(id, out var mappedId) ? mappedId : id).Distinct(StringComparer.Ordinal).ToList();
                if (identities.TryGetValue(evidence.ClueTargetPlayerId, out var targetId)) evidence.ClueTargetPlayerId=targetId;
            }
        }

        private ScenarioState FinalizeScenarioCandidate(ScenarioState candidate, RoundContext context)
        {
            Logged?.Invoke(GameSignal.ScenarioValidationStarted);
            Record("SCENARIO_VALIDATION_STARTED", candidate.GenerationAttempt.ToString());
            try
            {
            var truth=candidate.Truth;
            if (string.IsNullOrWhiteSpace(truth.ScenarioId) || truth.Timeline == null || truth.Facts == null || truth.Evidence == null || truth.Opportunities == null || truth.PlayerInformation == null || truth.Evidence.Any(e => e == null) || truth.PlayerInformation.Any(p => p == null))
                throw new InvalidOperationException("Generated scenario is missing required truth collections or entries.");
            roles.AssignRoles(State.Match.Players, context.RoundSeed);
            BindScenarioActorIdentities(truth);
            var packages=new List<PlayerInformationBundle>();
            foreach (var player in State.Match.Players) packages.Add(privateData.Generate(player, truth, context.Difficulty));
            DistributeAuthoredSolutionEvidence(truth, packages);
            truth.PlayerInformation=packages;
            informationValidator.ValidateRound(State.Match.Players, truth);
            candidate.SetTruth(truth);
            scenarioPipeline.ValidateOrThrow(candidate, State.Match.Players.Count, context.Difficulty);
            Record("SCENARIO_VALIDATION_PASSED", candidate.ScenarioId + ":attempt=" + candidate.GenerationAttempt);
            Logged?.Invoke(GameSignal.ScenarioValidationPassed);
            return candidate;
            }
            catch (Exception exception)
            {
                Record("SCENARIO_VALIDATION_FAILED", candidate.ScenarioId + ":attempt=" + candidate.GenerationAttempt + ":" + exception.Message);
                Logged?.Invoke(GameSignal.ScenarioValidationFailed);
                throw;
            }
        }

        private void DistributeAuthoredSolutionEvidence(ScenarioTruthModel truth, List<PlayerInformationBundle> packages)
        {
            var required=(truth.Evidence ?? new List<EvidenceDefinition>()).Where(e => e != null && e.EliminatesPlayerIds != null && e.EliminatesPlayerIds.Count > 0)
                .OrderBy(e => e.EvidenceId, StringComparer.Ordinal).FirstOrDefault();
            if (required == null) return;
            var recipient=State.Match.Players.Where(player => !player.IsThief).OrderBy(player => player.PlayerId).First();
            var package=packages.Single(item => item.PlayerId == recipient.PlayerId.ToString());
            if (!package.EvidenceIds.Contains(required.EvidenceId)) package.EvidenceIds.Add(required.EvidenceId);
            if (!recipient.Knowledge.EvidenceIds.Contains(required.EvidenceId)) recipient.Knowledge.EvidenceIds.Add(required.EvidenceId);
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
