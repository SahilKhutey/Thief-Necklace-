using System;
using System.Collections.Generic;

namespace ThiefAndTheNecklace.PB1
{
    [Serializable]
    public sealed class FactDefinition
    {
        public string FactId = string.Empty;
        public string SubjectId = string.Empty;
        public string Predicate = string.Empty;
        public string ObjectId = string.Empty;
        public EvidenceTruth Truth;
    }

    public readonly struct FactKey : IEquatable<FactKey>
    {
        public readonly string Subject;
        public readonly string Predicate;
        public readonly string Object;
        public FactKey(string subject, string predicate, string @object)
        { Subject = subject ?? string.Empty; Predicate = predicate ?? string.Empty; Object = @object ?? string.Empty; }
        public override string ToString() { return Subject + ":" + Predicate + ":" + Object; }
        public bool Equals(FactKey other) => string.Equals(Subject, other.Subject, StringComparison.Ordinal) && string.Equals(Predicate, other.Predicate, StringComparison.Ordinal) && string.Equals(Object, other.Object, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is FactKey other && Equals(other);
        public override int GetHashCode() { unchecked { return ((StringComparer.Ordinal.GetHashCode(Subject ?? string.Empty) * 397) ^ StringComparer.Ordinal.GetHashCode(Predicate ?? string.Empty)) * 397 ^ StringComparer.Ordinal.GetHashCode(Object ?? string.Empty); } }
    }

    [Serializable]
    public sealed class TimelineEvent
    {
        public string EventId = string.Empty;
        public int Sequence;
        public float StartTime;
        public float EndTime;
        public string LocationId = string.Empty;
        public string ActorId = string.Empty;
        public string Action = string.Empty;
        public bool Critical;
        public bool IsResolved;
    }

    [Serializable]
    public sealed class OpportunityWindow
    {
        public string PlayerId = string.Empty;
        public float StartTime, EndTime;
        public string LocationId = string.Empty;
        public bool HasAccess, PhysicallyPossible;
    }

    [Serializable]
    public sealed class EvidenceDefinition
    {
        public string EvidenceId = string.Empty;
        public string Description = string.Empty;
        public List<string> SupportsFactIds = new List<string>();
        public List<string> ContradictsFactIds = new List<string>();
        public List<string> RequiredEventIds = new List<string>();
        public bool Independent, Critical, IsAmbiguous, IsFalseLead;
        public string OwnerPlayerId = string.Empty;
    }

    [Serializable]
    public sealed class PlayerInformationBundle
    {
        public string PlayerId = string.Empty;
        public RoleType Role;
        public string PrivateSituation = string.Empty;
        public List<string> EvidenceIds = new List<string>();
        public List<string> KnownFactIds = new List<string>();
        public List<string> UnknownFactIds = new List<string>();
        public List<string> FalseFactIds = new List<string>();
        public string Objective = string.Empty;
    }

    [Serializable]
    public sealed class TemporalConstraint
    {
        public string PlayerId = string.Empty;
        public float StartTime, EndTime;
        public string LocationId = string.Empty;
    }

    [Serializable]
    public sealed class PlayerClaim
    {
        public string ClaimId = string.Empty;
        public int PlayerId;
        public string FactId = string.Empty;
        public string Statement = string.Empty;
        public float GameTime;
        public bool Retracted;
    }

    [Serializable]
    public sealed class ScenarioTruthModel
    {
        public string ScenarioId = string.Empty;
        public List<TimelineEvent> Timeline = new List<TimelineEvent>();
        public List<FactDefinition> Facts = new List<FactDefinition>();
        public List<OpportunityWindow> Opportunities = new List<OpportunityWindow>();
        public List<EvidenceDefinition> Evidence = new List<EvidenceDefinition>();
        public List<PlayerInformationBundle> PlayerInformation = new List<PlayerInformationBundle>();

        public ScenarioTruthModel DeepCopy()
        {
            var copy = new ScenarioTruthModel { ScenarioId = ScenarioId };
            foreach (var e in Timeline ?? new List<TimelineEvent>()) copy.Timeline.Add(e == null ? null : new TimelineEvent { EventId=e.EventId, Sequence=e.Sequence, StartTime=e.StartTime, EndTime=e.EndTime, LocationId=e.LocationId, ActorId=e.ActorId, Action=e.Action, Critical=e.Critical, IsResolved=e.IsResolved });
            foreach (var f in Facts ?? new List<FactDefinition>()) copy.Facts.Add(f == null ? null : new FactDefinition { FactId=f.FactId, SubjectId=f.SubjectId, Predicate=f.Predicate, ObjectId=f.ObjectId, Truth=f.Truth });
            foreach (var o in Opportunities ?? new List<OpportunityWindow>()) copy.Opportunities.Add(o == null ? null : new OpportunityWindow { PlayerId=o.PlayerId, StartTime=o.StartTime, EndTime=o.EndTime, LocationId=o.LocationId, HasAccess=o.HasAccess, PhysicallyPossible=o.PhysicallyPossible });
            foreach (var e in Evidence ?? new List<EvidenceDefinition>()) copy.Evidence.Add(e == null ? null : new EvidenceDefinition { EvidenceId=e.EvidenceId, Description=e.Description, SupportsFactIds=new List<string>(e.SupportsFactIds ?? new List<string>()), ContradictsFactIds=new List<string>(e.ContradictsFactIds ?? new List<string>()), RequiredEventIds=new List<string>(e.RequiredEventIds ?? new List<string>()), Independent=e.Independent, Critical=e.Critical, IsAmbiguous=e.IsAmbiguous, IsFalseLead=e.IsFalseLead, OwnerPlayerId=e.OwnerPlayerId });
            foreach (var p in PlayerInformation ?? new List<PlayerInformationBundle>()) copy.PlayerInformation.Add(p == null ? null : new PlayerInformationBundle { PlayerId=p.PlayerId, Role=p.Role, PrivateSituation=p.PrivateSituation, EvidenceIds=new List<string>(p.EvidenceIds ?? new List<string>()), KnownFactIds=new List<string>(p.KnownFactIds ?? new List<string>()), UnknownFactIds=new List<string>(p.UnknownFactIds ?? new List<string>()), FalseFactIds=new List<string>(p.FalseFactIds ?? new List<string>()), Objective=p.Objective });
            return copy;
        }
    }

    public sealed class RoundTruth
    {
        private readonly ScenarioTruthModel snapshot;
        public string ScenarioId { get; }
        public ScenarioTruthModel Scenario { get { return snapshot.DeepCopy(); } }
        public RoundTruth(string scenarioId, ScenarioTruthModel scenario) { ScenarioId = scenarioId; snapshot = (scenario ?? throw new ArgumentNullException(nameof(scenario))).DeepCopy(); }
    }

    public sealed class ContradictionResult
    {
        public bool ContradictionFound;
        public List<string> FactAIds = new List<string>(), FactBIds = new List<string>();
        public string Explanation = string.Empty;
    }

    public sealed class ScenarioValidationResult
    {
        public bool Valid;
        public string[] Errors = Array.Empty<string>();
    }
}
