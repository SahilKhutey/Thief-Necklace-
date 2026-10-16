using System;
using System.Collections.Generic;
using System.Linq;

namespace ThiefAndTheNecklace.PB1
{
    public sealed class PlayerRuntimeData
    {
        public int PlayerId;
        public string DisplayName;
        public RoleType Role;
        public bool IsThief;
        public ThiefObjective ThiefObjective;
        public string Objective;
        public string PrivateSituation = string.Empty;
        public List<string> PrivateClues = new List<string>();
        public List<string> KnownEvidenceIds = new List<string>();
        public List<string> KnownFactIds = new List<string>();
        public List<string> UnknownFactIds = new List<string>();
        public List<string> FalseFactIds = new List<string>();
        public int MatchScore;
        public int RoundScore;
        public bool Connected = true;
        public bool Ready;
        public bool HasSubmittedVote;
        public int? VoteTargetId;
    }
}
