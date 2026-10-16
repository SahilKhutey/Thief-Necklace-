using System;
using System.Collections.Generic;
using System.Linq;

namespace ThiefAndTheNecklace
{
    public sealed class RoleManager
    {
        public void AssignRoles(IReadOnlyList<PlayerData> players, int seed)
        {
            if (players == null) throw new ArgumentNullException(nameof(players));
            if (players.Count < 4 || players.Count > 6) throw new ArgumentException("PB1 supports four to six players.", nameof(players));
            var playerIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var player in players)
                if (player == null || string.IsNullOrWhiteSpace(player.PlayerId) || !playerIds.Add(player.PlayerId))
                    throw new ArgumentException("Every player needs a unique, non-empty ID.", nameof(players));

            var rng = new SeededRandomService(seed);
            var roles = new List<RoleDefinition>(RoleCatalog.FourPlayerRoles);
            roles.Add(RoleCatalog.Find("queen"));
            roles.Add(RoleCatalog.Find("guard"));
            roles = roles.Take(players.Count).ToList();
            rng.Shuffle(roles);
            var thiefIndex = rng.Next(0, players.Count);

            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                player.RoleId = roles[i].Id;
                player.IsThief = i == thiefIndex;
                player.PrivateSituation = string.Empty;
                player.PrivateClue = string.Empty;
            }
        }
    }
}
