namespace ThiefAndTheNecklace.PB1
{
    // Thief remains in the phase/data vocabulary for compatibility; no player is dealt a Thief role.
    // The hidden Thief objective is assigned independently over a normal court role.
    public enum RoleType { King, Queen, Minister, Soldier, Guard, Servant, Thief }
    public enum RoundPhase { Waiting, Starting, PrivateReveal, Discussion, Voting, VoteResolution, Reveal, Scoring, Results, RoundEnd, NextRound, MatchEnd }
    public enum VoteResolution { Undecided, ThiefCaught, ThiefEscaped, RevoteRequired }
    public enum EvidenceTruth { Unknown, True, False }
    public enum ThiefObjective { AvoidDetection, RedirectSuspicion, MaintainAlibi, CreateAmbiguity }
    public enum GameSignal { GameBoot, LocalMatchStarted, RoundStarted, RolesAssigned, PrivateInfoAssigned, PrivateInfoAcknowledged, DiscussionStarted, DiscussionEnded, VotingStarted, VoteSubmitted, VoteRejected, VoteResolved, RevoteStarted, RevealStarted, RoleRevealed, ThiefRevealed, NecklaceRevealed, RevealOutcomeResolved, RoundScoreCalculated, RoundResultsOpened, RoundCompleted, MatchCompleting, MatchCompleted, MatchAborted, ScenarioBuildStarted, ScenarioBuildCompleted, ScenarioGenerationFailed, ScenarioValidationStarted, ScenarioValidationFailed, ScenarioValidationPassed, TimelineCreated, FactsCreated, EvidenceCreated, EvidenceGraphBuilt, PlayerInformationAssigned, ClaimCreated, ClaimRetracted, ContradictionDetected, OpportunityChecked, MatchCreated, MatchStarted, RoundContextCreated, RoundSeedCreated, RoundCreated, RoundValidated, PhaseChanged, VotesComplete, VoteTie, RevealCompleted, RoundScoreResolved, MatchScoreUpdated, StateInvariantFailed, IllegalStateTransition, SnapshotCreated, ReplayEventRecorded }
}
