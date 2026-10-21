using System;
using System.Collections.Generic;
using System.Linq;

namespace ThiefAndTheNecklace.PB1
{
    public sealed class RoleAssignmentService
    {
        private static readonly RoleType[] InnocentRoles = ThiefAndTheNecklace.RoleCatalog.AllRoles
            .Where(role => role != null && !role.IsThief)
            .Select(role => (RoleType)Enum.Parse(typeof(RoleType), role.Id, true))
            .ToArray();

        public void AssignRoles(IList<PlayerRuntimeData> players, int seed)
        {
            if (players == null) throw new ArgumentNullException(nameof(players));
            if (players.Count < 4 || players.Count > 6) throw new InvalidOperationException("Supported player count is 4–6.");
            if (players.Any(p => p == null || string.IsNullOrWhiteSpace(p.DisplayName))) throw new InvalidOperationException("Every player needs a display name.");
            if (players.Any(p => p.PlayerId <= 0)) throw new InvalidOperationException("Player IDs must be positive.");
            if (players.Select(p => p.PlayerId).Distinct().Count() != players.Count) throw new InvalidOperationException("Player IDs must be unique.");

            var roles = InnocentRoles.Take(players.Count).ToList();
            var random = new SeededRandomService(seed);
            random.Shuffle(roles);
            var thiefIndex = random.Next(0, players.Count);
            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                player.Role = roles[i];
                player.IsThief = i == thiefIndex;
                player.Objective = player.IsThief ? "Avoid detection and survive the vote." : "Determine who stole the Necklace.";
                player.ThiefObjective = player.IsThief ? ThiefObjective.MaintainAlibi : ThiefObjective.AvoidDetection;
                player.PrivateSituation = string.Empty;
                player.PrivateClues.Clear();
            }
            if (players.Count(p => p.IsThief) != 1) throw new InvalidOperationException("Exactly one Thief must be assigned.");
        }

        public void AssignRoles(IList<PlayerState> players, int seed)
        {
            if (players == null) throw new ArgumentNullException(nameof(players));
            if (players.Count < 4 || players.Count > 6) throw new InvalidOperationException("Supported player count is 4–6.");
            if (players.Any(p => p == null || string.IsNullOrWhiteSpace(p.DisplayName) || p.PlayerId <= 0)) throw new InvalidOperationException("Every runtime player needs a positive ID and display name.");
            if (players.Select(p => p.PlayerId).Distinct().Count() != players.Count) throw new InvalidOperationException("Player IDs must be unique.");
            var roles=InnocentRoles.Take(players.Count).ToList();
            var random=new SeededRandomService(seed);
            random.Shuffle(roles);
            var thiefIndex=random.Next(0, players.Count);
            for (var i=0; i<players.Count; i++)
            {
                var player=players[i];
                player.Role=roles[i];
                player.IsThief=i == thiefIndex;
                player.Knowledge=new PrivateKnowledgeState();
                player.Vote.Reset();
            }
            if (players.Count(p => p.IsThief) != 1) throw new InvalidOperationException("Exactly one Thief must be assigned.");
        }
    }
}
