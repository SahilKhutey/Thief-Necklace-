using System;
using System.Collections.Generic;
using System.Linq;

namespace ThiefAndTheNecklace.PB1
{
    public sealed class ScenarioService
    {
        public void ApplyPlayerInformation(ScenarioDefinitionData scenario, GameState state)
        {
            if (scenario == null) throw new ArgumentNullException(nameof(scenario));
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (string.IsNullOrWhiteSpace(scenario.ScenarioId) || string.IsNullOrWhiteSpace(scenario.Title) || string.IsNullOrWhiteSpace(scenario.Introduction))
                throw new InvalidOperationException("Scenario ID, title and introduction are required.");
            if (scenario.Events == null || scenario.Events.Count == 0 || scenario.Events.Any(e => e == null || string.IsNullOrWhiteSpace(e.EventId) || string.IsNullOrWhiteSpace(e.Location) || string.IsNullOrWhiteSpace(e.Description) || float.IsNaN(e.Time) || float.IsInfinity(e.Time)))
                throw new InvalidOperationException("Scenario truth needs a complete, ordered event timeline.");
            if (scenario.Events.Select(e => e.EventId).Distinct(StringComparer.Ordinal).Count() != scenario.Events.Count)
                throw new InvalidOperationException("Scenario event IDs must be unique.");
            for (var i = 1; i < scenario.Events.Count; i++)
                if (scenario.Events[i].Time < scenario.Events[i - 1].Time) throw new InvalidOperationException("Scenario events must be ordered by time.");
            if (scenario.PlayerInformation == null) throw new InvalidOperationException("Scenario player information is missing.");
            if (scenario.PlayerInformation.Where(x => x != null).Select(x => x.Role).Distinct().Count() != scenario.PlayerInformation.Count(x => x != null))
                throw new InvalidOperationException("Scenario role information cannot contain duplicate roles.");
            if (scenario.PlayerInformation.Any(x => x == null || !Enum.IsDefined(typeof(RoleType), x.Role) || x.Role == RoleType.Thief))
                throw new InvalidOperationException("Scenario private information must use valid court roles; the Thief is an objective overlay.");
            if (scenario.HasThiefOverride && (string.IsNullOrWhiteSpace(scenario.ThiefSituation) || scenario.ThiefClues == null || scenario.ThiefClues.Count == 0 || scenario.ThiefClues.Any(string.IsNullOrWhiteSpace)))
                throw new InvalidOperationException("Thief private situation and at least one Thief clue are required.");
            foreach (var player in state.Players)
            {
                if (!Enum.IsDefined(typeof(RoleType), player.Role) || player.Role == RoleType.Thief)
                    throw new InvalidOperationException("Player has an unknown or non-court role.");
                var info = scenario.PlayerInformation.FirstOrDefault(x => x != null && x.Role == player.Role);
                if (info == null) throw new InvalidOperationException("Missing scenario information for " + player.Role + ".");
                if (string.IsNullOrWhiteSpace(info.PrivateSituation) || info.Clues == null || info.Clues.Count == 0 || info.Clues.Any(string.IsNullOrWhiteSpace))
                    throw new InvalidOperationException("Scenario information must include a situation and at least one clue for " + player.Role + ".");
                player.PrivateSituation = player.IsThief && scenario.HasThiefOverride ? scenario.ThiefSituation : info.PrivateSituation;
                player.PrivateClues = new List<string>(player.IsThief && scenario.HasThiefOverride ? scenario.ThiefClues : info.Clues);
            }
        }
    }
}
