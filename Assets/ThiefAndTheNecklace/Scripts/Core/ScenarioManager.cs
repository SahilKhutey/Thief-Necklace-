using System;
using System.Collections.Generic;

namespace ThiefAndTheNecklace
{
    public sealed class ScenarioManager
    {
        public const string ScenarioId = "royal_necklace";
        public const string Title = "The Royal Necklace";
        public const string Description = "The King's necklace has disappeared during a palace blackout. Someone among you is responsible. Find the Thief.";

        public static readonly string[] CanonicalTimeline =
        {
            "21:00 Banquet begins.",
            "21:07 Queen leaves the banquet.",
            "21:09 Minister heads toward the Treasury.",
            "21:11 Servant passes through the East Corridor.",
            "21:12 Blackout begins; the western gate remains closed.",
            "21:13 Thief enters the Royal Chamber; the chamber door opens.",
            "21:14 Thief takes the Necklace and exits through the East Corridor.",
            "21:15 Lights return.",
            "21:16 Necklace is discovered missing."
        };

        public void AssignPrivateInformation(IReadOnlyList<PlayerData> players, int seed)
        {
            if (players == null) throw new ArgumentNullException(nameof(players));
            foreach (var player in players)
            {
                if (player.IsThief)
                {
                    player.PrivateSituation = "During the blackout, you entered the Royal Chamber, took the Necklace, and escaped by the East Corridor. Keep your identity hidden.";
                    player.PrivateClue = "You know the blackout lasted long enough for someone to reach the display and leave.";
                    continue;
                }

                switch (player.RoleId)
                {
                    case "king":
                        player.PrivateSituation = "You spoke with the Queen shortly before she left the banquet.";
                        player.PrivateClue = "The Queen left the banquet at 21:07, before the blackout.";
                        break;
                    case "minister":
                        player.PrivateSituation = "You checked documents near the Treasury before the blackout.";
                        player.PrivateClue = "At 21:09, you headed toward the Treasury to check the documents.";
                        break;
                    case "soldier":
                        player.PrivateSituation = "You guarded the western gate when the blackout began.";
                        player.PrivateClue = "The western gate remained closed throughout the blackout.";
                        break;
                    case "servant":
                        player.PrivateSituation = "You passed through the East Corridor shortly before the blackout.";
                        player.PrivateClue = "You heard a door open near the Royal Chamber during the blackout.";
                        break;
                    default:
                        throw new InvalidOperationException("Unknown PB1 role: " + player.RoleId);
                }
            }
        }
    }
}
