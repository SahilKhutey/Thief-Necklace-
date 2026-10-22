using System;
using System.Collections.Generic;

namespace ThiefAndTheNecklace.PB1
{
    public enum MatchStatus { Created, InProgress, Completing, Completed, Aborted }
    public enum DifficultyLevel { Easy, Normal, Hard }

    [Serializable]
    public sealed class MatchState
    {
        public string MatchId = string.Empty;
        public int Seed;
        public int CurrentRound;
        public int TotalRounds;
        public DifficultyLevel Difficulty;
        public MatchStatus Status;
        public List<PlayerState> Players = new List<PlayerState>();
        public RoundState CurrentRoundState = new RoundState();
    }

    [Serializable]
    public sealed class PlayerState
    {
        public int PlayerId;
        public string DisplayName = string.Empty;
        public RoleType Role;
        public bool IsThief;
        public ThiefObjective? ThiefObjective;
        public bool Connected = true;
        public bool Ready;
        public bool PrivateInformationAcknowledged;
        public int MatchScore;
        public int RoundScore;
        public PrivateKnowledgeState Knowledge = new PrivateKnowledgeState();
        public VoteState Vote = new VoteState();
    }

    [Serializable]
    public sealed class PrivateKnowledgeState
    {
        public string Situation = string.Empty;
        public List<string> FactIds = new List<string>();
        public List<string> UnknownFactIds = new List<string>();
        public List<string> FalseFactIds = new List<string>();
        public List<string> EvidenceIds = new List<string>();
        public List<string> ObservedEventIds = new List<string>();
        public List<string> KnownLocationIds = new List<string>();
        public List<string> KnownPlayerIds = new List<string>();
        public List<PlayerBelief> Beliefs = new List<PlayerBelief>();
        public string Objective = string.Empty;
    }

    [Serializable]
    public sealed class PlayerBelief
    {
        public string FactId = string.Empty;
        public string SourceClaimId = string.Empty;
        public float Confidence;
    }

    [Serializable]
    public sealed class VoteState
    {
        public bool Submitted;
        public int? TargetPlayerId;
        public void Reset() { Submitted = false; TargetPlayerId = null; }
    }

    [Serializable]
    public sealed class RoundState
    {
        public int RoundNumber;
        public string ScenarioId = string.Empty;
        public RoundPhase Phase = RoundPhase.Waiting;
        public int VoteRound;
        public int TieCount;
        public VoteResolution Resolution = VoteResolution.Undecided;
        public bool ThiefEscaped;
        public int? AccusedPlayerId;
        public bool Completed;
        public bool ScoringResolved;
        public RoundStatus Status = RoundStatus.NotStarted;
        public string RoundId = string.Empty;
        public List<int> RunoffCandidates = new List<int>();
    }

    [Serializable]
    public sealed class ScenarioState
    {
        private string scenarioId = string.Empty;
        private int generationSeed;
        private int generationAttempt;
        private bool validated;
        private ScenarioTruthModel truth = new ScenarioTruthModel();
        private SolvabilityResult solvability;
        public string ScenarioId { get { return scenarioId; } internal set { if (validated) throw new InvalidOperationException("Validated scenario state is immutable."); scenarioId = value; } }
        public int GenerationSeed { get { return generationSeed; } internal set { if (validated) throw new InvalidOperationException("Validated scenario state is immutable."); generationSeed = value; } }
        public int GenerationAttempt { get { return generationAttempt; } internal set { if (validated) throw new InvalidOperationException("Validated scenario state is immutable."); generationAttempt = value; } }
        public bool Validated { get { return validated; } }
        public SolvabilityResult Solvability { get { return solvability; } }
        public ScenarioTruthModel Truth { get { return truth.DeepCopy(); } }
        internal void SetTruth(ScenarioTruthModel model) { if (validated) throw new InvalidOperationException("Validated scenario state is immutable."); truth = (model ?? throw new ArgumentNullException(nameof(model))).DeepCopy(); }
        internal void MarkValidated(SolvabilityResult result) { if (validated) throw new InvalidOperationException("Scenario is already validated."); solvability=result; validated=true; }
    }

    [Serializable]
    public sealed class RoundVoteState
    {
        public int VoteRound;
        public Dictionary<int, int> Votes = new Dictionary<int, int>();
        public bool FinalVote;
        public void Reset() { Votes.Clear(); FinalVote = false; }
    }

    [Serializable]
    public sealed class MatchScoreState
    {
        public Dictionary<int, int> RoundScores = new Dictionary<int, int>();
        public List<int> CommittedRounds = new List<int>();
        public List<ScoreTransaction> Transactions = new List<ScoreTransaction>();
        public void Reset() { RoundScores.Clear(); CommittedRounds.Clear(); Transactions.Clear(); }
    }

    [Serializable]
    public sealed class GameRuntimeState
    {
        public MatchState Match = new MatchState();
        public ScenarioState Scenario = new ScenarioState();
        public RoundVoteState Votes = new RoundVoteState();
        public MatchScoreState Scores = new MatchScoreState();
    }

    [Serializable]
    public sealed class DifficultyConfig
    {
        public DifficultyLevel Level;
        public int EvidencePerPlayer;
        public int AmbiguousEvidenceCount;
        public int FalseLeadCount;
        public bool RequireMultipleEvidencePaths;

        public static DifficultyConfig Create(DifficultyLevel level)
        {
            switch (level)
            {
                case DifficultyLevel.Easy: return new DifficultyConfig { Level=level, EvidencePerPlayer=3, AmbiguousEvidenceCount=0, FalseLeadCount=0, RequireMultipleEvidencePaths=false };
                case DifficultyLevel.Normal: return new DifficultyConfig { Level=level, EvidencePerPlayer=2, AmbiguousEvidenceCount=1, FalseLeadCount=1, RequireMultipleEvidencePaths=true };
                case DifficultyLevel.Hard: return new DifficultyConfig { Level=level, EvidencePerPlayer=2, AmbiguousEvidenceCount=2, FalseLeadCount=2, RequireMultipleEvidencePaths=true };
                default: throw new ArgumentOutOfRangeException(nameof(level));
            }
        }
    }

    public sealed class RoundContext
    {
        public int MatchSeed { get; }
        public int RoundNumber { get; }
        public int RoundSeed { get; }
        public DifficultyConfig Difficulty { get; }
        public RoundContext(int matchSeed, int roundNumber, DifficultyConfig difficulty)
        {
            if (roundNumber < 1) throw new ArgumentOutOfRangeException(nameof(roundNumber));
            MatchSeed=matchSeed; RoundNumber=roundNumber; RoundSeed=SeedUtility.GetRoundSeed(matchSeed, roundNumber); Difficulty=difficulty ?? throw new ArgumentNullException(nameof(difficulty));
        }
    }

    public static class SeedUtility
    {
        public static int GetRoundSeed(int matchSeed, int roundNumber)
        {
            unchecked { var hash=17; hash=hash*31+matchSeed; hash=hash*31+roundNumber; return hash; }
        }
    }
}
