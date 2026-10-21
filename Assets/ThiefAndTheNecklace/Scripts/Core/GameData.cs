using System;
using System.Collections.Generic;

namespace ThiefAndTheNecklace
{
    public enum RoundPhase
    {
        None,
        Intro,
        PrivateReveal,
        Discussion,
        Voting,
        VoteResolution,
        Reveal,
        Scoring,
        RoundEnd,
        MatchEnd
    }

    [Serializable]
    public sealed class RoleDefinition
    {
        public string Id;
        public string DisplayName;
        public int PointValue;
        public string Description;
        public bool IsThief;

        public RoleDefinition(string id, string displayName, int pointValue, string description, bool isThief = false)
        {
            Id = id;
            DisplayName = displayName;
            PointValue = pointValue;
            Description = description;
            IsThief = isThief;
        }
    }

    [Serializable]
    public sealed class PlayerData
    {
        public string PlayerId;
        public string DisplayName;
        public string RoleId;
        public bool IsThief;
        public string PrivateSituation;
        public string PrivateClue;
        public readonly List<string> PrivateClues = new List<string>();
        public int MatchScore;
        public int CorrectDetections;
        public int SuccessfulThiefEscapes;

        public PlayerData(string playerId, string displayName)
        {
            PlayerId = playerId;
            DisplayName = displayName;
        }
    }

    [Serializable]
    public sealed class DiscussionMessage
    {
        public string SenderId;
        public string Text;
        public int Sequence;
    }

    public sealed class VoteResolution
    {
        public readonly Dictionary<string, int> Counts;
        public readonly List<string> TopCandidates;

        public bool IsResolved => TopCandidates.Count == 1;
        public string WinnerId => IsResolved ? TopCandidates[0] : null;

        public VoteResolution(Dictionary<string, int> counts, List<string> topCandidates)
        {
            Counts = counts;
            TopCandidates = topCandidates;
        }
    }

    public sealed class RoundScoreResult
    {
        public readonly Dictionary<string, int> Deltas;
        public readonly Dictionary<string, string> Reasons;

        public RoundScoreResult(Dictionary<string, int> deltas, Dictionary<string, string> reasons)
        {
            Deltas = deltas;
            Reasons = reasons;
        }
    }

    public sealed class RevealResult
    {
        public string AccusedPlayerId;
        public string ThiefPlayerId;
        public bool ThiefCaught;
        public bool WasRunoff;
        public VoteResolution FinalVote;
    }

    public static class RoleCatalog
    {
        public static readonly RoleDefinition[] AllRoles =
        {
            new RoleDefinition("king", "King", 8, "The ruler of the palace."),
            new RoleDefinition("queen", "Queen", 7, "The Queen of the palace."),
            new RoleDefinition("minister", "Minister", 6, "A trusted court official."),
            new RoleDefinition("soldier", "Soldier", 5, "A disciplined palace defender."),
            new RoleDefinition("servant", "Servant", 4, "An observant member of the household."),
            new RoleDefinition("guard", "Guard", 5, "A palace security officer."),
            new RoleDefinition("thief", "Thief", 0, "The hidden Necklace bearer. This is an objective marker layered over a court role.", true)
        };

        public static readonly RoleDefinition[] FourPlayerRoles = { AllRoles[0], AllRoles[2], AllRoles[3], AllRoles[4] };

        public static RoleDefinition Find(string id)
        {
            foreach (var role in AllRoles)
                if (role.Id == id) return role;
            return null;
        }
    }
}
