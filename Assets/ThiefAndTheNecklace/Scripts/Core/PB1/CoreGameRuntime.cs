using System;
using System.Collections.Generic;

namespace ThiefAndTheNecklace.PB1
{
    /// <summary>
    /// Stable PB1-FINAL gameplay API. Presentation and transport layers should
    /// issue commands here rather than mutate runtime state directly.
    /// </summary>
    public sealed class CoreGameRuntime
    {
        private readonly MatchController controller;

        public GameRuntimeState State => controller.State;
        public event Action<GameSignal> Logged
        {
            add { controller.Logged += value; }
            remove { controller.Logged -= value; }
        }
        public event Action<CoreLogEvent> CoreEventLogged
        {
            add { controller.CoreEventLogged += value; }
            remove { controller.CoreEventLogged -= value; }
        }
        public event Action<RoundPhase> PhaseChanged
        {
            add { controller.PhaseChanged += value; }
            remove { controller.PhaseChanged -= value; }
        }
        public event Action StateChanged
        {
            add { controller.StateChanged += value; }
            remove { controller.StateChanged -= value; }
        }
        public event Action<KnowledgeAdded> KnowledgeChanged
        {
            add { controller.KnowledgeChanged += value; }
            remove { controller.KnowledgeChanged -= value; }
        }
        public event Action<DiscussionEvent> DiscussionEventPublished
        {
            add { controller.DiscussionEventPublished += value; }
            remove { controller.DiscussionEventPublished -= value; }
        }
        public event Action<VoteEventType, int, VoteRejectionReason> VoteEventPublished
        {
            add { controller.VoteEventPublished += value; }
            remove { controller.VoteEventPublished -= value; }
        }
        public event Action<VoteResolutionResult> VoteResolutionPublished
        {
            add { controller.VoteResolutionPublished += value; }
            remove { controller.VoteResolutionPublished -= value; }
        }
        public event Action<RevealEvent> RevealEventPublished
        {
            add { controller.RevealEventPublished += value; }
            remove { controller.RevealEventPublished -= value; }
        }
        public event Action<ScoreEvent> ScoreEventPublished
        {
            add { controller.ScoreEventPublished += value; }
            remove { controller.ScoreEventPublished -= value; }
        }
        public event Action<RoundFlowEvent> RoundFlowEventPublished
        {
            add { controller.RoundFlowEventPublished += value; }
            remove { controller.RoundFlowEventPublished -= value; }
        }

        public CoreGameRuntime(IReadOnlyList<string> playerNames, int seed, int totalRounds,
            DifficultyLevel difficulty, IScenarioGenerator scenarioGenerator, bool requirePrivateInformationAcknowledgement = false, DiscussionRateConfig discussionConfig = null, VoteResolutionConfig voteResolutionConfig = null, RevealConfig revealConfig = null, ScoringConfig scoringConfig = null, ResultsConfig resultsConfig = null)
        {
            controller = new MatchController(scenarioGenerator, seed, totalRounds, difficulty, playerNames, requirePrivateInformationAcknowledgement, discussionConfig, voteResolutionConfig, revealConfig, scoringConfig, resultsConfig);
        }

        public void StartMatch() => controller.Start();
        public void SetPlayerConnected(int playerId, bool connected) => controller.SetPlayerConnected(playerId, connected);
        public void AbortMatch() => controller.AbortMatch();
        public void BeginDiscussion() => controller.BeginDiscussion();
        public DiscussionSnapshot GetDiscussionSnapshot(long afterSequence = 0, int pageSize = 100) => controller.GetDiscussionSnapshot(afterSequence, pageSize);
        public DiscussionMessage SendChatMessage(int playerId, string text) => controller.SendChatMessage(playerId, text);
        public PlayerQuestion SubmitQuestion(int fromPlayerId, int toPlayerId, string text) => controller.SubmitQuestion(fromPlayerId, toPlayerId, text);
        public DiscussionResponse RespondToQuestion(int playerId, string questionId, string text) => controller.RespondToQuestion(playerId, questionId, text);
        public ClaimChallenge ChallengeClaim(int playerId, string claimId, string text) => controller.ChallengeClaim(playerId, claimId, text);
        public bool RequestEarlyVote(int playerId) => controller.RequestEarlyVote(playerId);
        public void UpdatePlayerHypothesis(int playerId, IEnumerable<string> candidates, IEnumerable<string> supported, IEnumerable<string> rejected) => controller.UpdatePlayerHypothesis(playerId, candidates, supported, rejected);
        public bool AcknowledgePrivateInformation(int playerId) => controller.AcknowledgePrivateInformation(playerId);
        public bool GrantKnownFact(int playerId, string factId, KnowledgeSource source) => controller.GrantKnownFact(playerId, factId, source);
        public bool GrantKnownFalseFact(int playerId, string factId, KnowledgeSource source) => controller.GrantKnownFalseFact(playerId, factId, source);
        public bool GrantEvidence(int playerId, string evidenceId, KnowledgeSource source) => controller.GrantEvidence(playerId, evidenceId, source);
        public bool GrantObservedEvent(int playerId, string eventId, KnowledgeSource source) => controller.GrantObservedEvent(playerId, eventId, source);
        public bool GrantKnownLocation(int playerId, string locationId, KnowledgeSource source) => controller.GrantKnownLocation(playerId, locationId, source);
        public bool GrantKnownPlayer(int playerId, string knownPlayerId, KnowledgeSource source) => controller.GrantKnownPlayer(playerId, knownPlayerId, source);
        public bool RecordBelief(int playerId, string factId, string sourceClaimId, float confidence) => controller.RecordBelief(playerId, factId, sourceClaimId, confidence);
        public void BeginVoting() => controller.BeginVoting();
        public bool SubmitVote(int voterId, int targetId) => controller.SubmitVote(voterId, targetId);
        public VoteSubmissionResult SubmitVote(int voterId, int targetId, int voteSequence) => controller.SubmitVote(voterId, targetId, voteSequence);
        public IReadOnlyList<int> GetEligibleVoteTargets(int voterId) => controller.GetEligibleVoteTargets(voterId);
        public VoteProgressViewData GetVoteProgress() => controller.GetVoteProgress();
        public bool HasSubmittedVote(int voterId) => controller.HasSubmittedVote(voterId);
        public int RemainingVotingSeconds => controller.RemainingVotingSeconds;
        public VoteStatus GetVotingStatus() => controller.VotingStatus;
        public long GetVotingStartTimestamp() => controller.VotingStartTimestamp;
        public long GetVotingEndTimestamp() => controller.VotingEndTimestamp;
        public FinalizedVoteCollection GetFinalizedVotes() => controller.GetFinalizedVotes();
        public IReadOnlyList<FinalizedVoteCollection> GetFinalizedVoteHistory() => controller.GetFinalizedVoteHistory();
        public VoteResolution ResolveVote() => controller.ResolveVote();
        public VoteResolution ResolveVote(int voteSequence) => controller.ResolveVote(voteSequence);
        public VoteResolutionResult LastVoteResolutionResult => controller.LastVoteResolutionResult;
        public IReadOnlyList<VoteResolutionResult> GetVoteResolutionHistory() => controller.VoteResolutionHistory;
        public void CompleteReveal() => controller.CompleteReveal();
        public RevealResult LastRevealResult => controller.LastRevealResult;
        public RevealSnapshot GetRevealSnapshot() => controller.GetRevealSnapshot();
        public ScoreSnapshot GetScoreSnapshot() => controller.GetScoreSnapshot();
        public CompletedRoundResult GetCurrentRoundResult() => controller.GetCurrentRoundResult();
        public IReadOnlyList<CompletedRoundResult> GetRoundHistory() => controller.GetRoundHistory();
        public MatchResult GetMatchResult() => controller.GetMatchResult();
        public RoundAdvanceState GetCurrentRoundAdvanceState() => controller.GetCurrentRoundAdvanceState();
        public bool ContinueFromResults(int playerId, string matchId, string roundId) => controller.ContinueFromResults(playerId, matchId, roundId);
        public bool PollResultsTimeout() => controller.PollResultsTimeout();
        public void ResolveScoring() => controller.ResolveScoring();
        public void FinishRound() => controller.FinishRound();
        public void AdvanceDiscussionTimer(float deltaSeconds) => controller.AdvanceDiscussionTimer(deltaSeconds);
        public void AdvanceVotingTimer() => controller.AdvanceVotingTimer();
        public PlayerClaim AddClaim(int playerId, string factId, string statement, float gameTime) => controller.AddClaim(playerId, factId, statement, gameTime);
        public PlayerClaim AddClaim(int playerId, string factId, string statement, float gameTime, ClaimType type) => controller.AddClaim(playerId, factId, statement, gameTime, type);
        public bool RetractClaim(string claimId) => controller.RetractClaim(claimId);
        public ContradictionResult EvaluateClaimContradictions() => controller.EvaluateClaimContradictions();
        public IReadOnlyList<PlayerViewData> GetPublicPlayers() => controller.GetPublicPlayers();
        public PrivatePlayerViewData GetPrivateView(int playerId, int authenticatedRequesterId) => controller.GetPrivateView(playerId, authenticatedRequesterId);
        public IReadOnlyList<RevealedPlayerData> GetRevealedPlayers() => controller.GetRevealedPlayers();
        public IReadOnlyList<EvidenceDefinition> GetPrivateEvidence(int playerId, int authenticatedRequesterId) => controller.GetPrivateEvidence(playerId, authenticatedRequesterId);
        public IReadOnlyList<PlayerViewData> GetStandings() => controller.GetStandings();
        public GameSnapshot CaptureSnapshot() => controller.CaptureSnapshot();
        public ReplayRecord GetReplayRecord() => controller.GetReplayRecord();
        public IReadOnlyList<CoreLogEvent> GetCoreLog() => controller.GetCoreLog();
    }
}
