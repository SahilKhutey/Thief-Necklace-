using System;
using System.Collections.Generic;
using System.Linq;

namespace ThiefAndTheNecklace
{
    public static class ScenarioTemplateValidator
    {
        public static ValidationResult Validate(ScenarioTemplate template, int playerCount)
        {
            var result = new ValidationResult();
            if (template == null) { result.AddError("Template is null."); return result; }
            if (string.IsNullOrWhiteSpace(template.scenarioId) || template.version <= 0) result.AddError("Scenario ID and positive content version are required.");
            if (string.IsNullOrWhiteSpace(template.title) || string.IsNullOrWhiteSpace(template.publicDescription)) result.AddError("Public title and description are required.");
            if (playerCount < 4 || playerCount > 6) result.AddError("Supported player count is 4–6.");
            if (template.locations == null || template.locations.Length == 0) result.AddError("At least one location is required.");
            if (template.objects == null || template.objects.Length == 0) result.AddError("At least one target object is required.");
            if (template.roles == null || template.eventTemplates == null || template.clueTemplates == null) result.AddError("Roles, events and clue templates must be defined.");
            if (template.locations == null || template.objects == null || template.roles == null || template.eventTemplates == null || template.clueTemplates == null) return result;

            if (template.locations.Any(x => x == null) || template.objects.Any(x => x == null) || template.roles.Any(x => x == null)
                || template.eventTemplates.Any(x => x == null) || template.clueTemplates.Any(x => x == null)) result.AddError("Template collections cannot contain null entries.");
            CheckUnique(template.locations.Where(x => x != null).Select(x => x.id), "location", result);
            CheckUnique(template.objects.Where(x => x != null).Select(x => x.id), "object", result);
            CheckUnique(template.roles.Where(x => x != null).Select(x => x.id), "role", result);
            CheckUnique(template.eventTemplates.Where(x => x != null).Select(x => x.id), "event", result);
            CheckUnique(template.clueTemplates.Where(x => x != null).Select(x => x.id), "clue", result);

            var locations = new HashSet<string>(template.locations.Where(l => l != null).Select(l => l.id), StringComparer.Ordinal);
            var roles = new HashSet<string>(template.roles.Where(r => r != null).Select(r => r.id), StringComparer.Ordinal);
            var objects = new HashSet<string>(template.objects.Where(o => o != null).Select(o => o.id), StringComparer.Ordinal);
            var events = new Dictionary<string, EventTemplate>(StringComparer.Ordinal);
            foreach (var e in template.eventTemplates.Where(e => e != null && !string.IsNullOrWhiteSpace(e.id))) events[e.id] = e;
            if (!locations.Contains(template.targetLocationId)) result.AddError("Target location is missing.");
            if (!locations.Contains(template.escapeLocationId)) result.AddError("Escape location is missing.");

            foreach (var location in template.locations.Where(l => l != null))
            {
                if (string.IsNullOrWhiteSpace(location.id) || string.IsNullOrWhiteSpace(location.displayName)) result.AddError("A location is missing its ID or display name.");
                foreach (var adjacent in location.connectedLocationIds ?? new string[0])
                    if (!locations.Contains(adjacent)) result.AddError("Location " + location.id + " links to missing location " + adjacent + ".");
            }
            foreach (var item in template.objects.Where(o => o != null))
            {
                if (string.IsNullOrWhiteSpace(item.id) || string.IsNullOrWhiteSpace(item.displayName)) result.AddError("A target object is missing its ID or name.");
                if (!locations.Contains(item.locationId)) result.AddError("Object " + item.id + " uses a missing location.");
                if (!roles.Contains(item.ownerRoleId)) result.AddError("Object " + item.id + " uses a missing owner role.");
            }

            var expected = new[] { "king", "minister", "soldier", "servant", "queen", "guard" }.Take(Math.Max(0, playerCount));
            foreach (var roleId in expected)
            {
                var role = template.roles.FirstOrDefault(r => r != null && r.id == roleId);
                if (role == null || role.tags == null || !role.tags.Contains(template.themeTag)) result.AddError("Missing role or scenario-compatible tag: " + roleId);
            }

            var thefts = template.eventTemplates.Where(e => e != null && e.type == EventType.Theft).ToList();
            var escapes = template.eventTemplates.Where(e => e != null && e.type == EventType.Escape).ToList();
            var discoveries = template.eventTemplates.Where(e => e != null && e.type == EventType.Discovery).ToList();
            if (thefts.Count != 1) result.AddError("Exactly one theft event is required.");
            if (escapes.Count == 0 || discoveries.Count == 0) result.AddError("An escape and a later discovery event are required.");
            foreach (var e in template.eventTemplates.Where(e => e != null))
            {
                if (!e.useTargetLocation && !e.useEscapeLocation && !locations.Contains(e.locationId)) result.AddError("Event " + e.id + " uses a missing location.");
                if (e.actorIsThief && !e.actorIsFalseLead && e.type != EventType.Theft && e.type != EventType.Escape && e.type != EventType.Access)
                    result.AddError("Thief actor is used on unsupported event " + e.id + ".");
                if (!e.actorIsThief && !e.actorIsFalseLead && !e.optionalIfRoleUnavailable && !string.IsNullOrEmpty(e.actorRoleId) && !roles.Contains(e.actorRoleId)) result.AddError("Event " + e.id + " uses missing role " + e.actorRoleId + ".");
                foreach (var required in e.requiresEventIds ?? new string[0])
                    if (!events.ContainsKey(required)) result.AddError("Event " + e.id + " requires missing event " + required + ".");
                    else if (events[required].minuteOffset >= e.minuteOffset) result.AddError("Event dependency must precede " + e.id + ".");
            }
            foreach (var clue in template.clueTemplates.Where(c => c != null))
            {
                if (string.IsNullOrWhiteSpace(clue.text)) result.AddError("Clue " + clue.id + " has no text.");
                if (!string.IsNullOrEmpty(clue.eventId) && !events.ContainsKey(clue.eventId)) result.AddError("Clue " + clue.id + " references missing event " + clue.eventId + ".");
                if (!string.IsNullOrEmpty(clue.preferredRoleId) && clue.preferredRoleId != "thief" && !roles.Contains(clue.preferredRoleId)) result.AddError("Clue " + clue.id + " prefers a missing role.");
            }
            var profileInvalid = template.difficulties == null || template.difficulties.Any(p => p == null ||
                p.minCluesPerPlayer < 0 || p.maxCluesPerPlayer < p.minCluesPerPlayer || p.minimumEvidencePaths < 2 || p.falseLeadCount < 0 || p.ambiguityCount < 0);
            if (profileInvalid) result.AddError("Difficulty profiles have invalid clue, evidence, false-lead, or ambiguity budgets.");
            if (template.difficulties == null || !template.difficulties.Any(p => p != null && p.difficulty == ScenarioDifficulty.Normal)) result.AddError("A Normal difficulty profile is required.");
            return result;
        }

        private static void CheckUnique(IEnumerable<string> ids, string kind, ValidationResult result)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in ids)
                if (string.IsNullOrWhiteSpace(id) || !seen.Add(id)) result.AddError("Missing or duplicate " + kind + " ID: " + id);
        }
    }

    public sealed class ScenarioValidator
    {
        public ValidationResult Validate(RuntimeScenario scenario) => Validate(null, scenario, null);

        public ValidationResult Validate(ScenarioTemplate template, RuntimeScenario scenario, DifficultyProfile profile)
        {
            var result = new ValidationResult();
            if (scenario == null) { result.AddError("Runtime scenario is null."); return result; }
            if (scenario.playerCount < 4 || scenario.playerCount > 6) result.AddError("Runtime player count is outside 4–6.");
            if (scenario.world == null || scenario.world.Locations == null || scenario.world.Objects == null || scenario.events == null || scenario.clues == null || scenario.playerSecrets == null)
            { result.AddError("Runtime scenario is missing a required data set."); return result; }
            if (scenario.playerSecrets.Any(s => s == null) || scenario.events.Any(e => e == null) || scenario.clues.Any(c => c == null))
            { result.AddError("Runtime scenario collections cannot contain null entries."); return result; }
            if (scenario.playerSecrets.Count != scenario.playerCount) result.AddError("Every player must have one private state.");
            if (scenario.playerSecrets.Count(s => s.isThief) != 1) result.AddError("Exactly one Thief is required.");
            var secretIds = new HashSet<string>(scenario.playerSecrets.Select(s => s.playerId), StringComparer.Ordinal);
            if (secretIds.Count != scenario.playerSecrets.Count) result.AddError("Player IDs must be unique.");
            if (!secretIds.Contains(scenario.thiefPlayerId)) result.AddError("The selected Thief is not a participant.");
            var locations = new HashSet<string>(scenario.world.Locations.Where(l => l != null).Select(l => l.id), StringComparer.Ordinal);
            var objects = new HashSet<string>(scenario.world.Objects.Where(o => o != null).Select(o => o.id), StringComparer.Ordinal);
            var events = new Dictionary<string, ScenarioEvent>(StringComparer.Ordinal);
            foreach (var e in scenario.events)
            {
                if (string.IsNullOrWhiteSpace(e.eventId) || events.ContainsKey(e.eventId)) result.AddError("Runtime event IDs must be unique and present.");
                else events.Add(e.eventId, e);
            }
            var eventOrder = scenario.events.OrderBy(e => e.sequence).ToList();
            if (eventOrder.Count == 0) result.AddError("The timeline cannot be empty.");
            var previousMinute = -1;
            for (var i = 0; i < eventOrder.Count; i++)
            {
                var e = eventOrder[i];
                if (e.sequence != i) result.AddError("Timeline sequence numbers must be contiguous.");
                if (e.minute < previousMinute) result.AddError("Timeline time moves backwards.");
                previousMinute = e.minute;
                if (!locations.Contains(e.locationId)) result.AddError("Event " + e.eventId + " uses a missing location.");
                if (!string.IsNullOrEmpty(e.actorPlayerId) && !secretIds.Contains(e.actorPlayerId)) result.AddError("Event " + e.eventId + " has a nonparticipant actor.");
                foreach (var required in e.requiresEventIds ?? new string[0])
                    if (!events.ContainsKey(required) || events[required].sequence >= e.sequence) result.AddError("Event dependency is missing or out of order: " + e.eventId + " requires " + required + ".");
            }
            var theft = scenario.events.Where(e => e.eventType == EventType.Theft).ToList();
            var escape = scenario.events.FirstOrDefault(e => e.eventType == EventType.Escape);
            var discovery = scenario.events.FirstOrDefault(e => e.eventType == EventType.Discovery);
            if (theft.Count != 1) result.AddError("Exactly one theft must occur.");
            if (theft.Count == 1)
            {
                if (theft[0].actorPlayerId != scenario.thiefPlayerId) result.AddError("The Thief must be the actor of the theft.");
                if (!objects.Contains(theft[0].targetObjectId)) result.AddError("The theft target does not exist.");
            }
            if (escape == null || (theft.Count == 1 && escape.sequence <= theft[0].sequence)) result.AddError("A valid escape must follow the theft.");
            if (discovery == null || (escape != null && discovery.sequence <= escape.sequence)) result.AddError("Discovery must follow escape.");
            if (template != null && theft.Count == 1)
            {
                var target = (template.objects ?? new ScenarioObjectData[0]).FirstOrDefault(o => o != null && o.id == theft[0].targetObjectId);
                var thiefSecret = scenario.playerSecrets.FirstOrDefault(s => s.playerId == scenario.thiefPlayerId);
                var roleData = (template.roles ?? new ScenarioRoleData[0]).FirstOrDefault(r => r != null && thiefSecret != null && r.id == thiefSecret.roleId);
                var roles = new HashSet<string>((template.roles ?? new ScenarioRoleData[0]).Where(r => r != null).Select(r => r.id), StringComparer.Ordinal);
                var uniqueRoles = new HashSet<string>(StringComparer.Ordinal);
                foreach (var secret in scenario.playerSecrets)
                {
                    if (!roles.Contains(secret.roleId) || !uniqueRoles.Add(secret.roleId)) result.AddError("Each player must receive one unique scenario-compatible role.");
                }
                if (target == null || roleData == null || template.locations == null || !CanReach(template, "hall", target.locationId, roleData.tags)) result.AddError("The Thief cannot reach the target from the gathering location.");
                if (target != null && (escape == null || roleData == null || template.locations == null || !CanReach(template, target.locationId, escape.locationId, roleData.tags))) result.AddError("The Thief has no valid escape route.");
                if (thiefSecret == null || string.IsNullOrWhiteSpace(thiefSecret.privateSituation) || thiefSecret.clueIds == null || thiefSecret.clueIds.Count == 0)
                    result.AddError("The Thief needs a private situation and useful private information.");
            }

            var clueIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var clue in scenario.clues)
            {
                if (string.IsNullOrWhiteSpace(clue.clueId) || !clueIds.Add(clue.clueId)) result.AddError("Clue IDs must be present and unique.");
                if (!secretIds.Contains(clue.ownerPlayerId)) result.AddError("Clue " + clue.clueId + " has an unknown owner.");
                if (!events.ContainsKey(clue.sourceEventId)) result.AddError("Clue " + clue.clueId + " cites an unknown event.");
                if (string.IsNullOrWhiteSpace(clue.text)) result.AddError("Clue " + clue.clueId + " has no text.");
                var owner = scenario.playerSecrets.FirstOrDefault(s => s.playerId == clue.ownerPlayerId);
                var truthThief = scenario.playerSecrets.FirstOrDefault(s => s.isThief);
                if (owner != null && !owner.isThief && template != null && truthThief != null)
                {
                    var thiefRole = scenario.playerSecrets.First(s => s.isThief).roleId;
                    var roleName = (template.roles ?? new ScenarioRoleData[0]).FirstOrDefault(r => r != null && r.id == thiefRole)?.displayName ?? string.Empty;
                    var target = (template.objects ?? new ScenarioObjectData[0]).FirstOrDefault(o => o != null && o.id == scenario.targetObjectId);
                    if (target != null && clue.text.IndexOf(target.displayName, StringComparison.OrdinalIgnoreCase) >= 0
                        && clue.text.IndexOf(roleName, StringComparison.OrdinalIgnoreCase) >= 0
                        && (clue.text.IndexOf("stole", StringComparison.OrdinalIgnoreCase) >= 0 || clue.text.IndexOf("took", StringComparison.OrdinalIgnoreCase) >= 0))
                        result.AddError("Clue " + clue.clueId + " directly reveals the Thief and theft.");
                }
            }
            foreach (var secret in scenario.playerSecrets)
            {
                if (string.IsNullOrWhiteSpace(secret.roleId) || string.IsNullOrWhiteSpace(secret.privateSituation)) result.AddError("Every player needs a role and private situation.");
                if (secret.clueIds == null || secret.clueIds.Count == 0) result.AddError("Every player needs at least one clue.");
                foreach (var clueId in secret.clueIds ?? new List<string>())
                    if (!scenario.clues.Any(c => c.clueId == clueId && c.ownerPlayerId == secret.playerId)) result.AddError("Player secret references a clue it does not own.");
            }
            if (profile != null)
            {
                var evidence = scenario.clues.Where(c => c.criticalEvidence).Select(c => c.sourceEventId).Distinct(StringComparer.Ordinal).Count();
                if (evidence < profile.minimumEvidencePaths) result.AddError("Critical event evidence paths are below the difficulty minimum.");
                if (scenario.clues.Count(c => c.falseLead) < profile.falseLeadCount) result.AddError("Not enough false-lead observations were generated.");
                if (scenario.clues.Count(c => c.truthType == TruthType.PartialTruth) < profile.ambiguityCount) result.AddError("Not enough ambiguous clues were generated.");
                foreach (var secret in scenario.playerSecrets)
                    if (secret.clueIds == null || secret.clueIds.Count < profile.minCluesPerPlayer || secret.clueIds.Count > profile.maxCluesPerPlayer)
                        result.AddError("Player " + secret.playerId + " is outside the clue information budget.");
            }
            return result;
        }

        private static bool CanReach(ScenarioTemplate template, string from, string to, IEnumerable<string> roleTags)
        {
            if (from == to) return true;
            var tags = new HashSet<string>(roleTags ?? new string[0], StringComparer.Ordinal);
            var visited = new HashSet<string>(StringComparer.Ordinal) { from };
            var queue = new Queue<string>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var location = template.locations.FirstOrDefault(l => l.id == current);
                if (location == null) continue;
                foreach (var next in location.connectedLocationIds ?? new string[0])
                {
                    var destination = template.locations.FirstOrDefault(l => l.id == next);
                    if (destination == null) continue;
                    if (destination.restricted && !destination.publicArea && !(destination.accessTags ?? new string[0]).Any(tags.Contains)) continue;
                    if (next == to) return true;
                    if (visited.Add(next)) queue.Enqueue(next);
                }
            }
            return false;
        }
    }
}
