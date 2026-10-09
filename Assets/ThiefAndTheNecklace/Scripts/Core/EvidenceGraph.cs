using System;
using System.Collections.Generic;
using System.Linq;

namespace ThiefAndTheNecklace
{
    public sealed class EvidenceLink
    {
        public string EventId { get; }
        public string ClueId { get; }
        public string OwnerPlayerId { get; }
        public EvidenceLink(string eventId, string clueId, string ownerPlayerId)
        { EventId = eventId; ClueId = clueId; OwnerPlayerId = ownerPlayerId; }
    }

    public sealed class EvidenceGraph
    {
        public IReadOnlyList<EvidenceLink> Links { get; }
        private EvidenceGraph(IReadOnlyList<EvidenceLink> links) { Links = links; }

        public static EvidenceGraph Build(IEnumerable<RuntimeClue> clues)
        {
            if (clues == null) throw new ArgumentNullException(nameof(clues));
            return new EvidenceGraph(clues.Select(c => new EvidenceLink(c.sourceEventId, c.clueId, c.ownerPlayerId)).ToList().AsReadOnly());
        }

        public IReadOnlyList<EvidenceLink> EvidenceForEvent(string eventId)
            => Links.Where(link => link.EventId == eventId).ToList().AsReadOnly();
    }

    /// <summary>Structured statements can be compared for impossible overlapping locations. This reports conflicts only; it never assigns guilt or suspicion.</summary>
    public sealed class StructuredPlayerClaim
    {
        public string PlayerId;
        public string SubjectPlayerId;
        public string LocationId;
        public int StartMinute;
        public int EndMinute;
        public string SourceStatementId;
    }

    public sealed class PotentialContradiction
    {
        public StructuredPlayerClaim First { get; }
        public StructuredPlayerClaim Second { get; }
        public PotentialContradiction(StructuredPlayerClaim first, StructuredPlayerClaim second) { First = first; Second = second; }
    }

    public static class ContradictionGraph
    {
        public static IReadOnlyList<PotentialContradiction> FindOverlappingLocationConflicts(IEnumerable<StructuredPlayerClaim> claims)
        {
            if (claims == null) throw new ArgumentNullException(nameof(claims));
            var facts = claims.Where(c => c != null && !string.IsNullOrWhiteSpace(c.SubjectPlayerId) && !string.IsNullOrWhiteSpace(c.LocationId)).ToList();
            var conflicts = new List<PotentialContradiction>();
            for (var i = 0; i < facts.Count; i++)
            for (var j = i + 1; j < facts.Count; j++)
            {
                var a = facts[i];
                var b = facts[j];
                if (a.SubjectPlayerId != b.SubjectPlayerId || a.LocationId == b.LocationId) continue;
                if (a.EndMinute < b.StartMinute || b.EndMinute < a.StartMinute) continue;
                conflicts.Add(new PotentialContradiction(a, b));
            }
            return conflicts.AsReadOnly();
        }
    }
}
