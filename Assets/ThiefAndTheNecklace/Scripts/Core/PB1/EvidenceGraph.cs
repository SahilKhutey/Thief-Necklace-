using System;
using System.Collections.Generic;
using System.Linq;

namespace ThiefAndTheNecklace.PB1
{
    public sealed class EvidenceGraph
    {
        private readonly Dictionary<string, EvidenceDefinition> evidence = new Dictionary<string, EvidenceDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, FactDefinition> facts = new Dictionary<string, FactDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<FactKey, FactDefinition> factsByKey = new Dictionary<FactKey, FactDefinition>();
        public void AddFact(FactDefinition fact) { if (fact == null || string.IsNullOrWhiteSpace(fact.FactId)) throw new ArgumentException("A fact with an ID is required.", nameof(fact)); if (facts.ContainsKey(fact.FactId)) throw new InvalidOperationException("Duplicate fact ID: " + fact.FactId); var key = new FactKey(fact.SubjectId, fact.Predicate, fact.ObjectId); if (factsByKey.ContainsKey(key)) throw new InvalidOperationException("Duplicate logical fact: " + key); var copy = new FactDefinition { FactId=fact.FactId, SubjectId=fact.SubjectId, Predicate=fact.Predicate, ObjectId=fact.ObjectId, Truth=fact.Truth }; facts.Add(copy.FactId, copy); factsByKey.Add(key, copy); }
        public bool TryGetFact(FactKey key, out FactDefinition fact) => factsByKey.TryGetValue(key, out fact);
        public void AddEvidence(EvidenceDefinition item) { if (item == null || string.IsNullOrWhiteSpace(item.EvidenceId)) throw new ArgumentException("Evidence with an ID is required.", nameof(item)); if (evidence.ContainsKey(item.EvidenceId)) throw new InvalidOperationException("Duplicate evidence ID: " + item.EvidenceId); evidence.Add(item.EvidenceId, Copy(item)); }
        public bool TryGetEvidence(string evidenceId, out EvidenceDefinition item) { if (!evidence.TryGetValue(evidenceId, out var found)) { item = null; return false; } item = Copy(found); return true; }
        public IReadOnlyList<EvidenceDefinition> GetEvidenceSupporting(string factId) => evidence.Values.Where(e => e.SupportsFactIds != null && e.SupportsFactIds.Contains(factId)).Select(Copy).ToList().AsReadOnly();
        public IReadOnlyList<EvidenceDefinition> GetEvidenceContradicting(string factId) => evidence.Values.Where(e => e.ContradictsFactIds != null && e.ContradictsFactIds.Contains(factId)).Select(Copy).ToList().AsReadOnly();
        public bool HasIndependentSupport(string factId, int minimum = 2) => minimum >= 0 && GetEvidenceSupporting(factId).Count(e => e.Independent) >= minimum;
        private static EvidenceDefinition Copy(EvidenceDefinition e) => new EvidenceDefinition { EvidenceId=e.EvidenceId, Description=e.Description, SupportsFactIds=new List<string>(e.SupportsFactIds ?? new List<string>()), ContradictsFactIds=new List<string>(e.ContradictsFactIds ?? new List<string>()), RequiredEventIds=new List<string>(e.RequiredEventIds ?? new List<string>()), Independent=e.Independent, Critical=e.Critical, IsAmbiguous=e.IsAmbiguous, IsFalseLead=e.IsFalseLead, OwnerPlayerId=e.OwnerPlayerId };
    }

    public sealed class KnowledgeService
    {
        public bool KnowsFact(PlayerRuntimeData player, string factId) => player != null && player.KnownFactIds.Contains(factId);
        public bool KnowsEvidence(PlayerRuntimeData player, string evidenceId) => player != null && player.KnownEvidenceIds.Contains(evidenceId);
        public void GrantFact(PlayerRuntimeData player, string factId) { if (player == null) throw new ArgumentNullException(nameof(player)); if (string.IsNullOrWhiteSpace(factId)) throw new ArgumentException("Fact ID is required.", nameof(factId)); if (!player.KnownFactIds.Contains(factId)) player.KnownFactIds.Add(factId); }
        public void GrantEvidence(PlayerRuntimeData player, string evidenceId) { if (player == null) throw new ArgumentNullException(nameof(player)); if (string.IsNullOrWhiteSpace(evidenceId)) throw new ArgumentException("Evidence ID is required.", nameof(evidenceId)); if (!player.KnownEvidenceIds.Contains(evidenceId)) player.KnownEvidenceIds.Add(evidenceId); }
    }

    public sealed class ContradictionService
    {
        public bool Contradicts(TemporalConstraint claim, TemporalConstraint observed)
        {
            if (claim == null) throw new ArgumentNullException(nameof(claim));
            if (observed == null) throw new ArgumentNullException(nameof(observed));
            if (claim.EndTime < claim.StartTime || observed.EndTime < observed.StartTime) return false;
            var overlaps = claim.StartTime <= observed.EndTime && observed.StartTime <= claim.EndTime;
            return overlaps && string.Equals(claim.PlayerId, observed.PlayerId, StringComparison.Ordinal) && !string.Equals(claim.LocationId, observed.LocationId, StringComparison.Ordinal);
        }

        public ContradictionResult FindContradictions(IEnumerable<FactDefinition> input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            var facts = input.Where(x => x != null && x.Truth == EvidenceTruth.True).ToList();
            for (var i = 0; i < facts.Count; i++) for (var j = i + 1; j < facts.Count; j++)
            {
                var a = facts[i]; var b = facts[j];
                if (!string.Equals(a.SubjectId, b.SubjectId, StringComparison.Ordinal) || !string.Equals(a.Predicate, b.Predicate, StringComparison.Ordinal)) continue;
                if (string.Equals(a.ObjectId, b.ObjectId, StringComparison.Ordinal)) continue;
                return new ContradictionResult { ContradictionFound = true, FactAIds = new List<string> { a.FactId }, FactBIds = new List<string> { b.FactId }, Explanation = a.SubjectId + " cannot simultaneously have '" + a.Predicate + "' = '" + a.ObjectId + "' and '" + b.ObjectId + "'." };
            }
            return new ContradictionResult();
        }
    }

    public sealed class OpportunityService
    {
        public bool HasOpportunity(OpportunityWindow window, float eventTime, string eventLocation)
        {
            if (window == null) throw new ArgumentNullException(nameof(window));
            return eventTime >= window.StartTime && eventTime <= window.EndTime && string.Equals(window.LocationId, eventLocation, StringComparison.Ordinal) && window.HasAccess && window.PhysicallyPossible;
        }
    }

    public sealed class ClaimService
    {
        private readonly List<PlayerClaim> claims = new List<PlayerClaim>();
        public IReadOnlyList<PlayerClaim> Claims => claims.Select(Clone).ToList().AsReadOnly();
        public event Action<PlayerClaim> ClaimAdded;
        public event Action<PlayerClaim> ClaimWasRetracted;
        public void Clear() { claims.Clear(); }
        public PlayerClaim AddClaim(int playerId, string factId, string statement, float gameTime)
        {
            if (playerId <= 0) throw new ArgumentOutOfRangeException(nameof(playerId));
            if (string.IsNullOrWhiteSpace(statement)) throw new ArgumentException("Statement cannot be empty.", nameof(statement));
            if (float.IsNaN(gameTime) || float.IsInfinity(gameTime) || gameTime < 0) throw new ArgumentOutOfRangeException(nameof(gameTime));
            var claim = new PlayerClaim { ClaimId = Guid.NewGuid().ToString("N"), PlayerId = playerId, FactId = factId ?? string.Empty, Statement = statement.Trim(), GameTime = gameTime };
            claims.Add(claim); ClaimAdded?.Invoke(Clone(claim)); return Clone(claim);
        }
        public bool RetractClaim(string claimId)
        {
            var claim = claims.Find(x => string.Equals(x.ClaimId, claimId, StringComparison.Ordinal));
            if (claim == null || claim.Retracted) return false;
            claim.Retracted = true; ClaimWasRetracted?.Invoke(Clone(claim)); return true;
        }
        private static PlayerClaim Clone(PlayerClaim p) => new PlayerClaim { ClaimId=p.ClaimId, PlayerId=p.PlayerId, FactId=p.FactId, Statement=p.Statement, GameTime=p.GameTime, Retracted=p.Retracted };
    }
}
