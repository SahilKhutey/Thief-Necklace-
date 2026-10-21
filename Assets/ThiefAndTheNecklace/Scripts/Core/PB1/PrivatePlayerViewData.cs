using System;
using System.Collections.Generic;
using System.Linq;

namespace ThiefAndTheNecklace.PB1
{
    public sealed class PrivatePlayerViewData
    {
        public int PlayerId { get; internal set; }
        public int OwnerPlayerId { get; internal set; }
        public PlayerViewData Public { get; internal set; }
        public RoleType Role { get; internal set; }
        public bool IsThief { get; internal set; }
        public string Situation { get; internal set; }
        public IReadOnlyList<string> Clues { get; internal set; }
        public IReadOnlyList<string> EvidenceIds { get; internal set; }
        public IReadOnlyList<string> KnownFactIds { get; internal set; }
        public IReadOnlyList<string> UnknownFactIds { get; internal set; }
        public IReadOnlyList<string> FalseFactIds { get; internal set; }
        public IReadOnlyList<string> ObservedEventIds { get; internal set; }
        public IReadOnlyList<string> KnownLocationIds { get; internal set; }
        public IReadOnlyList<string> KnownPlayerIds { get; internal set; }
        public IReadOnlyList<PlayerBeliefView> Beliefs { get; internal set; }
        public string Objective { get; internal set; }
        public ThiefObjective? ThiefObjective { get; internal set; }
        public int RoleScoreValue { get; internal set; }
    }
}
