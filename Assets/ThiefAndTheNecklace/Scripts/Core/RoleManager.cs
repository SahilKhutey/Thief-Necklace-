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
            if (players.Count != 4) throw new ArgumentException("PB1 requires exactly four players.", nameof(players));

            var rng = new SeededRandomService(seed);
            var roles = new List<RoleDefinition>(RoleCatalog.FourPlayerRoles);
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
