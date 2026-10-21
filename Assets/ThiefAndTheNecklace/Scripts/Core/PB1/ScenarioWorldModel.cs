using System;
using System.Collections.Generic;
using System.Linq;

namespace ThiefAndTheNecklace.PB1
{
    public enum ScenarioActorType { Player, Npc, Environment }
    public enum ScenarioObjectType { Necklace, Key, Door, Alarm, DisplayCase, Other }
    public enum EvidenceStrength { Weak, Moderate, Strong, Conclusive }
    public enum SolvabilityStatus { UniqueSolution, MultipleSolutions, NoSolution, InsufficientEvidence }
    public enum ContradictionType { LocationConflict, TimelineConflict, ActionConflict, AccessConflict, WitnessConflict, ObjectConflict }

    [Serializable]
    public sealed class LocationState
    {
        public string LocationId = string.Empty;
        public string DisplayName = string.Empty;
        public string LocationType = "Palace";
        public bool IsPublic;
        public List<string> ConnectedLocationIds = new List<string>();
        public LocationState Copy() => new LocationState { LocationId=LocationId, DisplayName=DisplayName, LocationType=LocationType, IsPublic=IsPublic, ConnectedLocationIds=new List<string>(ConnectedLocationIds ?? new List<string>()) };
    }

    [Serializable]
    public sealed class ActorState
    {
        public string ActorId = string.Empty;
        public ScenarioActorType Type;
        public string DisplayName = string.Empty;
        public List<string> AccessibleLocationIds = new List<string>();
        public List<string> AvailableActions = new List<string>();
        public ActorState Copy() => new ActorState { ActorId=ActorId, Type=Type, DisplayName=DisplayName, AccessibleLocationIds=new List<string>(AccessibleLocationIds ?? new List<string>()), AvailableActions=new List<string>(AvailableActions ?? new List<string>()) };
    }

    [Serializable]
    public sealed class ScenarioObjectState
    {
        public string ObjectId = string.Empty;
        public ScenarioObjectType Type;
        public string LocationId = string.Empty;
        public bool IsPublic;
        public List<string> StateTags = new List<string>();
        public ScenarioObjectState Copy() => new ScenarioObjectState { ObjectId=ObjectId, Type=Type, LocationId=LocationId, IsPublic=IsPublic, StateTags=new List<string>(StateTags ?? new List<string>()) };
    }

    [Serializable]
    public sealed class ObjectTransition
    {
        public string ObjectId = string.Empty;
        public string EventId = string.Empty;
        public string FromLocationId = string.Empty;
        public string ToLocationId = string.Empty;
        public string ToActorId = string.Empty;
        public ObjectTransition Copy() => new ObjectTransition { ObjectId=ObjectId, EventId=EventId, FromLocationId=FromLocationId, ToLocationId=ToLocationId, ToActorId=ToActorId };
    }

    [Serializable]
    public sealed class KnowledgeAssignment
    {
        public string FactId = string.Empty;
        public List<string> PlayerIds = new List<string>();
        public KnowledgeSource Source;
    }

    public enum AlibiStatus { Verified, PartiallyVerified, Unverified, Contradicted }

    [Serializable]
    public sealed class Alibi
    {
        public string PlayerId = string.Empty;
        public float StartTime, EndTime;
        public string ClaimedLocationId = string.Empty;
        public string SourceId = string.Empty;
        public AlibiStatus Status;
    }

    [Serializable]
    public sealed class Contradiction
    {
        public string ContradictionId = string.Empty;
        public string PlayerId = string.Empty;
        public string ClaimId = string.Empty;
        public string FactId = string.Empty;
        public ContradictionType Type;
    }

    public sealed class AlibiAssessment
    {
        public AlibiStatus Status { get; internal set; }
        public IReadOnlyList<string> SupportingEventIds { get; internal set; }
        public IReadOnlyList<string> ConflictingEventIds { get; internal set; }
    }

    /// <summary>Evaluates alibi and claim conflicts against authority truth without promoting claims into facts.</summary>
    public sealed class ScenarioDeductionService
    {
        public AlibiAssessment EvaluateAlibi(ScenarioTruthModel scenario, Alibi alibi)
        {
            if (scenario == null) throw new ArgumentNullException(nameof(scenario));
            if (alibi == null) throw new ArgumentNullException(nameof(alibi));
            if (string.IsNullOrWhiteSpace(alibi.PlayerId) || string.IsNullOrWhiteSpace(alibi.ClaimedLocationId)
                || float.IsNaN(alibi.StartTime) || float.IsInfinity(alibi.StartTime) || float.IsNaN(alibi.EndTime) || float.IsInfinity(alibi.EndTime) || alibi.StartTime < 0 || alibi.EndTime < alibi.StartTime)
                throw new ArgumentException("Alibi requires a player, location, and valid time interval.", nameof(alibi));

            var overlapping=(scenario.Timeline ?? new List<TimelineEvent>()).Where(e => e != null && e.ActorId == alibi.PlayerId && e.StartTime <= alibi.EndTime && alibi.StartTime <= e.EndTime).ToList();
            var supporting=overlapping.Where(e => e.LocationId == alibi.ClaimedLocationId).Select(e => e.EventId).Distinct(StringComparer.Ordinal).ToList();
            var conflicting=overlapping.Where(e => e.LocationId != alibi.ClaimedLocationId).Select(e => e.EventId).Distinct(StringComparer.Ordinal).ToList();
            var status=conflicting.Count > 0 ? AlibiStatus.Contradicted
                : supporting.Any(id => { var item=overlapping.First(e => e.EventId == id); return item.StartTime <= alibi.StartTime && item.EndTime >= alibi.EndTime; }) ? AlibiStatus.Verified
                : supporting.Count > 0 ? AlibiStatus.PartiallyVerified : AlibiStatus.Unverified;
            alibi.Status=status;
            return new AlibiAssessment { Status=status, SupportingEventIds=supporting.AsReadOnly(), ConflictingEventIds=conflicting.AsReadOnly() };
        }

        public Contradiction FindEvidenceBackedClaimContradiction(ScenarioTruthModel scenario, PlayerClaim claim, IEnumerable<string> knownEvidenceIds)
        {
            if (scenario == null) throw new ArgumentNullException(nameof(scenario));
            if (claim == null) throw new ArgumentNullException(nameof(claim));
            var known=new HashSet<string>(knownEvidenceIds ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            var claimFact=(scenario.Facts ?? new List<FactDefinition>()).FirstOrDefault(f => f != null && f.FactId == claim.FactId);
            if (claimFact == null) return null;
            var supported=(scenario.Evidence ?? new List<EvidenceDefinition>()).Where(e => e != null && known.Contains(e.EvidenceId)).ToList();
            var contradicts=supported.Any(e => (e.ContradictsFactIds ?? new List<string>()).Contains(claimFact.FactId))
                || (claimFact.Truth == EvidenceTruth.False && supported.Any(e => (e.SupportsFactIds ?? new List<string>()).Contains(claimFact.FactId)))
                || supported.SelectMany(e => e.SupportsFactIds ?? new List<string>()).Select(id => (scenario.Facts ?? new List<FactDefinition>()).FirstOrDefault(f => f != null && f.FactId == id))
                    .Any(f => f != null && f.Truth == EvidenceTruth.True && f.SubjectId == claimFact.SubjectId && f.Predicate == claimFact.Predicate && f.ObjectId != claimFact.ObjectId);
            if (!contradicts) return null;
            return new Contradiction { ContradictionId="CON-" + claim.ClaimId, ClaimId=claim.ClaimId, PlayerId=claim.PlayerId.ToString(), FactId=claimFact.FactId, Type=Classify(claimFact.Predicate) };
        }

        private static ContradictionType Classify(string predicate)
        {
            var value=predicate ?? string.Empty;
            if (value.IndexOf("location", StringComparison.OrdinalIgnoreCase) >= 0 || value.IndexOf("wasAt", StringComparison.OrdinalIgnoreCase) >= 0 || value.IndexOf("occup", StringComparison.OrdinalIgnoreCase) >= 0) return ContradictionType.LocationConflict;
            if (value.IndexOf("time", StringComparison.OrdinalIgnoreCase) >= 0 || value.IndexOf("during", StringComparison.OrdinalIgnoreCase) >= 0) return ContradictionType.TimelineConflict;
            if (value.IndexOf("access", StringComparison.OrdinalIgnoreCase) >= 0 || value.IndexOf("key", StringComparison.OrdinalIgnoreCase) >= 0) return ContradictionType.AccessConflict;
            if (value.IndexOf("object", StringComparison.OrdinalIgnoreCase) >= 0 || value.IndexOf("steal", StringComparison.OrdinalIgnoreCase) >= 0 || value.IndexOf("possess", StringComparison.OrdinalIgnoreCase) >= 0) return ContradictionType.ObjectConflict;
            return ContradictionType.ActionConflict;
        }
    }

    public sealed class ScenarioGenerationError
    {
        public string Code { get; internal set; }
        public string Message { get; internal set; }
        public string RelatedObjectId { get; internal set; }
    }

    public sealed class ScenarioGenerationResult
    {
        public bool Success { get; internal set; }
        public ScenarioTruthModel Scenario { get; internal set; }
        public IReadOnlyList<ScenarioGenerationError> Errors { get; internal set; }
    }

    public sealed class PublicScenarioView
    {
        public string ScenarioId { get; internal set; }
        public string Version { get; internal set; }
        public string Theme { get; internal set; }
        public string StoryContext { get; internal set; }
        public IReadOnlyList<LocationState> Locations { get; internal set; }
        public IReadOnlyList<ScenarioObjectState> Objects { get; internal set; }
    }

    public sealed class RevealedScenarioView
    {
        public PublicScenarioView Public { get; internal set; }
        public ScenarioTruthModel Truth { get; internal set; }
    }

    /// <summary>Builds safe public and post-reveal scenario projections from authority state.</summary>
    public sealed class ScenarioProjectionService
    {
        public PublicScenarioView BuildPublic(ScenarioTruthModel scenario)
        {
            if (scenario == null) throw new ArgumentNullException(nameof(scenario));
            var publicLocationIds=new HashSet<string>((scenario.Locations ?? new List<LocationState>()).Where(l => l != null && l.IsPublic).Select(l => l.LocationId), StringComparer.Ordinal);
            var locations=(scenario.Locations ?? new List<LocationState>()).Where(l => l != null && l.IsPublic).Select(l =>
            {
                var copy=l.Copy();
                copy.ConnectedLocationIds=(copy.ConnectedLocationIds ?? new List<string>()).Where(publicLocationIds.Contains).ToList();
                return copy;
            }).ToList();
            // Object location and state remain hidden unless the scenario author marks the object public.
            var objects=(scenario.Objects ?? new List<ScenarioObjectState>()).Where(o => o != null && o.IsPublic)
                .Select(o => new ScenarioObjectState { ObjectId=o.ObjectId, Type=o.Type, IsPublic=true }).ToList();
            return new PublicScenarioView { ScenarioId=scenario.ScenarioId, Version=scenario.ScenarioVersion, Theme=scenario.Theme, StoryContext=scenario.PublicStoryContext, Locations=locations.AsReadOnly(), Objects=objects.AsReadOnly() };
        }

        public RevealedScenarioView BuildReveal(ScenarioTruthModel scenario, RoundPhase phase, bool revealCommitted)
        {
            if (scenario == null) throw new ArgumentNullException(nameof(scenario));
            if (!revealCommitted || (phase != RoundPhase.Reveal && phase != RoundPhase.Scoring && phase != RoundPhase.RoundEnd && phase != RoundPhase.NextRound && phase != RoundPhase.MatchEnd))
                throw new InvalidOperationException("Scenario truth is available only after authoritative reveal commit.");
            return new RevealedScenarioView { Public=BuildPublic(scenario), Truth=scenario.DeepCopy() };
        }
    }

    [Serializable]
    public sealed class ScenarioGenerationConfig
    {
        public int MinimumEvidencePaths = 2;
        public int MaxGenerationAttempts = 20;
        public bool RequireUniqueSolution = true;
        public bool AllowFalseLeads = true;
        public DifficultyLevel Difficulty;
    }

    public sealed class SolvabilityResult
    {
        public SolvabilityStatus Status { get; internal set; }
        public string ThiefPlayerId { get; internal set; }
        public IReadOnlyList<string> RemainingCandidates { get; internal set; }
        public IReadOnlyList<string> MinimumEvidenceSet { get; internal set; }
        public string Explanation { get; internal set; }
    }

    /// <summary>Checks whether the declared evidence distribution narrows the suspect set to the known culprit.</summary>
    public interface IScenarioSolvabilityValidator { SolvabilityResult Validate(ScenarioTruthModel scenario); }

    public sealed class ScenarioSolvabilityValidator : IScenarioSolvabilityValidator
    {
        public SolvabilityResult Validate(ScenarioTruthModel scenario)
        {
            if (scenario == null) throw new ArgumentNullException(nameof(scenario));
            var theftEvent=(scenario.Timeline ?? new List<TimelineEvent>()).FirstOrDefault(e => e != null && e.IsTheftEvent);
            var candidates = (scenario.Opportunities ?? new List<OpportunityWindow>())
                .Where(o => o != null && o.HasAccess && o.PhysicallyPossible && o.HadTime && o.HasCapability && (theftEvent == null || (o.StartTime <= theftEvent.StartTime && o.EndTime >= theftEvent.EndTime && string.Equals(o.LocationId, theftEvent.LocationId, StringComparison.Ordinal))))
                .Select(o => o.PlayerId).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToList();
            if (candidates.Count == 0) return Result(scenario.ThiefPlayerId, SolvabilityStatus.NoSolution, candidates, new List<string>(), "No actor has a complete opportunity.");
            var thief = scenario.ThiefPlayerId;
            if (string.IsNullOrWhiteSpace(thief) || !candidates.Contains(thief, StringComparer.Ordinal))
                return Result(thief, SolvabilityStatus.NoSolution, candidates, new List<string>(), "The declared Thief has no complete opportunity.");

            var eliminationEvidence = new List<string>();
            var remaining = candidates;
            // Elimination assertions are explicit authored evidence metadata. Ordinary clue text is never treated as a solver rule.
            var assignedEvidence=(scenario.PlayerInformation ?? new List<PlayerInformationBundle>()).Where(bundle => bundle != null)
                .SelectMany(bundle => (bundle.EvidenceIds ?? new List<string>()).Select(id => new { bundle.PlayerId, EvidenceId=id })).ToList();
            foreach (var item in (scenario.Evidence ?? new List<EvidenceDefinition>()).Where(e => e != null && assignedEvidence.Any(a => a.EvidenceId == e.EvidenceId)))
            {
                var excludes = item.EliminatesPlayerIds ?? new List<string>();
                var next = remaining.Except(excludes, StringComparer.Ordinal).ToList();
                if (next.Count != remaining.Count) { remaining = next; eliminationEvidence.Add(item.EvidenceId); }
            }
            if (remaining.Count == 1 && string.Equals(remaining[0], thief, StringComparison.Ordinal) && eliminationEvidence.Count > 0)
                return Result(thief, SolvabilityStatus.UniqueSolution, remaining, eliminationEvidence, "Distributed, explicitly authored evidence leaves only the declared Thief.");
            if (!remaining.Contains(thief, StringComparer.Ordinal)) return Result(thief, SolvabilityStatus.NoSolution, remaining, eliminationEvidence, "Evidence eliminates the declared Thief.");
            if (eliminationEvidence.Count == 0) return Result(thief, SolvabilityStatus.InsufficientEvidence, remaining, eliminationEvidence, "Evidence does not encode enough explicit candidate constraints to prove a solution.");
            return Result(thief, SolvabilityStatus.MultipleSolutions, remaining, eliminationEvidence, "More than one suspect remains after authored candidate constraints.");
        }

        private static SolvabilityResult Result(string thief, SolvabilityStatus status, List<string> candidates, List<string> evidence, string explanation)
            => new SolvabilityResult { ThiefPlayerId=thief, Status=status, RemainingCandidates=candidates.AsReadOnly(), MinimumEvidenceSet=evidence.AsReadOnly(), Explanation=explanation };
    }

    public sealed class EvidenceGraphValidator
    {
        public int CountIndependentPaths(ScenarioTruthModel scenario, string factId)
        {
            if (scenario == null) throw new ArgumentNullException(nameof(scenario));
            return (scenario.Evidence ?? new List<EvidenceDefinition>())
                .Where(e => e != null && (e.SupportsFactIds ?? new List<string>()).Contains(factId))
                .Select(e => !string.IsNullOrWhiteSpace(e.CausalPathId) ? "path:" + e.CausalPathId : !string.IsNullOrWhiteSpace(e.SourceId) ? "source:" + e.SourceId : !string.IsNullOrWhiteSpace(e.SourceEventId) ? "event:" + e.SourceEventId : string.Empty)
                .Where(path => !string.IsNullOrEmpty(path)).Distinct(StringComparer.Ordinal).Count();
        }
    }
}
