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

        public CoreGameRuntime(IReadOnlyList<string> playerNames, int seed, int totalRounds,
            DifficultyLevel difficulty, IScenarioGenerator scenarioGenerator)
        {
            controller = new MatchController(scenarioGenerator, seed, totalRounds, difficulty, playerNames);
        }

        public void StartMatch() => controller.Start();
        public void BeginDiscussion() => controller.BeginDiscussion();
        public void BeginVoting() => controller.BeginVoting();
        public bool SubmitVote(int voterId, int targetId) => controller.SubmitVote(voterId, targetId);
        public VoteResolution ResolveVote() => controller.ResolveVote();
        public void CompleteReveal() => controller.CompleteReveal();
        public void ResolveScoring() => controller.ResolveScoring();
        public void FinishRound() => controller.FinishRound();
        public void AdvanceDiscussionTimer(float deltaSeconds) => controller.AdvanceDiscussionTimer(deltaSeconds);
        public PlayerClaim AddClaim(int playerId, string factId, string statement, float gameTime) => controller.AddClaim(playerId, factId, statement, gameTime);
        public bool RetractClaim(string claimId) => controller.RetractClaim(claimId);
        public ContradictionResult EvaluateClaimContradictions() => controller.EvaluateClaimContradictions();
        public IReadOnlyList<PlayerViewData> GetPublicPlayers() => controller.GetPublicPlayers();
        public PrivatePlayerViewData GetPrivateView(int playerId, int authenticatedRequesterId) => controller.GetPrivateView(playerId, authenticatedRequesterId);
        public IReadOnlyList<EvidenceDefinition> GetPrivateEvidence(int playerId, int authenticatedRequesterId) => controller.GetPrivateEvidence(playerId, authenticatedRequesterId);
        public IReadOnlyList<PlayerViewData> GetStandings() => controller.GetStandings();
        public GameSnapshot CaptureSnapshot() => controller.CaptureSnapshot();
        public ReplayRecord GetReplayRecord() => controller.GetReplayRecord();
        public IReadOnlyList<CoreLogEvent> GetCoreLog() => controller.GetCoreLog();
    }
}
