using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ThiefAndTheNecklace
{
    public sealed class ScenarioGenerator
    {
        private readonly ScenarioValidator _validator;
        public ScenarioGenerator(ScenarioValidator validator) { _validator = validator ?? throw new ArgumentNullException(nameof(validator)); }

        public RuntimeScenario Generate(ScenarioTemplate template, int playerCount, int seed, ScenarioDifficulty difficulty = ScenarioDifficulty.Normal)
        {
            if (playerCount < 4 || playerCount > 6) throw new ScenarioGenerationException("Player count must be between 4 and 6.");
            var players = Enumerable.Range(1, playerCount).Select(i => new PlayerData("P" + i, "Player " + i)).ToList();
            return Generate(template, players, seed, difficulty);
        }

        public RuntimeScenario Generate(ScenarioTemplate template, IReadOnlyList<PlayerData> players, int seed, ScenarioDifficulty difficulty = ScenarioDifficulty.Normal)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            if (players == null) throw new ArgumentNullException(nameof(players));
            if (players.Count < 4 || players.Count > 6) throw new ScenarioGenerationException("Player count must be between 4 and 6.");
            var preflight = ScenarioTemplateValidator.Validate(template, players.Count);
            if (!preflight.IsValid) throw new ScenarioGenerationException(string.Join("; ", preflight.Errors));

            var random = new SeededRandomService(seed);
            var selectedRoles = SelectRoles(template, players.Count);
            random.Shuffle(selectedRoles);
            var thiefIndex = random.Next(0, players.Count);
            var secrets = new List<PlayerSecret>(players.Count);
            var roleToPlayer = new Dictionary<string, string>();
            for (var i = 0; i < players.Count; i++)
            {
                if (players[i] == null || string.IsNullOrWhiteSpace(players[i].PlayerId)) throw new ScenarioGenerationException("Every participant needs a stable player ID.");
                players[i].RoleId = selectedRoles[i].id;
                players[i].IsThief = i == thiefIndex;
                var secret = new PlayerSecret { playerId = players[i].PlayerId, roleId = selectedRoles[i].id, isThief = players[i].IsThief };
                secrets.Add(secret);
                roleToPlayer.Add(secret.roleId, secret.playerId);
            }
            if (secrets.Select(s => s.playerId).Distinct(StringComparer.Ordinal).Count() != players.Count)
                throw new ScenarioGenerationException("Player IDs must be unique.");

            var thief = secrets[thiefIndex];
            var target = template.objects[random.Next(0, template.objects.Length)];
            var profile = GetProfile(template, difficulty);
            var world = new WorldState(template.locations, template.objects);
            var events = new TimelineGenerator().Generate(template, world, secrets, roleToPlayer, target, random);
            var clues = new ClueGenerator().Generate(template, events, secrets, roleToPlayer, target, profile);
            DistributeClues(clues, secrets, profile);
            SetSituations(template, events, secrets, target);

            var result = new RuntimeScenario
            {
                scenarioId = template.scenarioId,
                scenarioVersion = template.version,
                seed = seed,
                playerCount = players.Count,
                difficulty = difficulty,
                title = template.title,
                publicDescription = template.publicDescription,
                targetObjectId = target.id,
                thiefPlayerId = thief.playerId,
                world = world,
                events = events,
                clues = clues,
                playerSecrets = secrets
            };
            result.evidenceGraph = EvidenceGraph.Build(clues);
            var validation = _validator.Validate(template, result, profile);
            if (!validation.IsValid) throw new ScenarioGenerationException(string.Join("; ", validation.Errors));
            result.scenarioHash = ComputeScenarioHash(result);
            result.publicScenarioHash = ComputePublicHash(result);
            return result;
        }

        private static List<ScenarioRoleData> SelectRoles(ScenarioTemplate template, int count)
        {
            var ids = new[] { "king", "minister", "soldier", "servant", "queen", "guard" }.Take(count).ToArray();
            var roles = ids.Select(id => template.roles.FirstOrDefault(role => role.id == id && role.tags.Contains(template.themeTag))).ToList();
            if (roles.Count != count) throw new ScenarioGenerationException("The template does not have a compatible role set for " + count + " players.");
            return roles;
        }

        private static DifficultyProfile GetProfile(ScenarioTemplate template, ScenarioDifficulty difficulty)
        {
            var profile = template.difficulties.FirstOrDefault(p => p.difficulty == difficulty);
            if (profile == null) throw new ScenarioGenerationException("Missing difficulty profile: " + difficulty);
            return profile;
        }

        private static void DistributeClues(List<RuntimeClue> clues, List<PlayerSecret> secrets, DifficultyProfile profile)
        {
            foreach (var secret in secrets) secret.clueIds.Clear();
            var criticalOwners = new HashSet<string>(StringComparer.Ordinal);
            foreach (var clue in clues)
            {
                PlayerSecret owner = null;
                // Critical evidence is separated across innocent players where possible.
                if (clue.criticalEvidence)
                {
                    owner = secrets.FirstOrDefault(s => !s.isThief && s.playerId == clue.ownerPlayerId && !criticalOwners.Contains(s.playerId));
                    if (owner == null) owner = secrets.Where(s => !s.isThief && !criticalOwners.Contains(s.playerId))
                        .OrderBy(s => s.clueIds.Count).ThenBy(s => s.playerId, StringComparer.Ordinal).FirstOrDefault();
                }
                if (owner == null && !clue.criticalEvidence)
                    owner = secrets.FirstOrDefault(s => s.playerId == clue.ownerPlayerId && s.clueIds.Count < profile.maxCluesPerPlayer);
                if (owner == null) owner = secrets.Where(s => !s.isThief || clue.ownerPlayerId == s.playerId)
                    .OrderBy(s => s.clueIds.Count).ThenBy(s => s.playerId, StringComparer.Ordinal).First();
                clue.ownerPlayerId = owner.playerId;
                owner.clueIds.Add(clue.clueId);
                if (clue.criticalEvidence) criticalOwners.Add(owner.playerId);
            }

            // Protect the one-clue-per-player floor even when a template has few clue templates.
            var unowned = secrets.Where(s => s.clueIds.Count == 0).ToList();
            foreach (var secret in unowned)
            {
                var source = clues.Where(c => c.ownerPlayerId != secret.playerId && !c.criticalEvidence).OrderBy(c => c.strength).FirstOrDefault();
                if (source == null) continue;
                var copy = new RuntimeClue
                {
                    clueId = source.clueId + "-shared-" + secret.playerId,
                    ownerPlayerId = secret.playerId,
                    sourceEventId = source.sourceEventId,
                    informationType = source.informationType,
                    truthType = TruthType.PartialTruth,
                    strength = Math.Max(1, source.strength - 1),
                    falseLead = source.falseLead,
                    text = source.text
                };
                clues.Add(copy);
                secret.clueIds.Add(copy.clueId);
            }
        }

        private static void SetSituations(ScenarioTemplate template, List<ScenarioEvent> events, List<PlayerSecret> secrets, ScenarioObjectData target)
        {
            foreach (var secret in secrets)
            {
                if (secret.isThief)
                {
                    secret.privateSituation = "During " + template.distractionName + ", you entered the " + FindLocation(template, target.locationId).displayName
                        + ", took the " + target.displayName + " and escaped through the " + FindLocation(template, template.escapeLocationId).displayName
                        + ". Your objective is to survive the vote; invent a believable explanation.";
                    continue;
                }
                var ownEvent = events.FirstOrDefault(e => e.actorPlayerId == secret.playerId && e.eventType != EventType.Discovery);
                if (ownEvent == null) ownEvent = events.FirstOrDefault(e => e.eventType == EventType.Gathering);
                var location = FindLocation(template, ownEvent.locationId).displayName;
                secret.privateSituation = "You were near the " + location + " shortly before or during " + template.distractionName + ". Explain what you remember and compare it with the others.";
            }
        }

        internal static ScenarioLocationData FindLocation(ScenarioTemplate template, string id)
            => template.locations.FirstOrDefault(l => l.id == id);

        internal static string ComputeScenarioHash(RuntimeScenario scenario)
        {
            var text = new StringBuilder();
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            text.Append(scenario.scenarioId).Append('|').Append(scenario.scenarioVersion.ToString(culture)).Append('|').Append(scenario.seed.ToString(culture))
                .Append('|').Append(scenario.playerCount.ToString(culture)).Append('|').Append(((int)scenario.difficulty).ToString(culture)).Append('|').Append(scenario.targetObjectId)
                .Append('|').Append(scenario.title).Append('|').Append(scenario.publicDescription);
            foreach (var location in scenario.world.Locations.OrderBy(location => location.id, StringComparer.Ordinal))
                text.Append('|').Append(location.id).Append(':').Append(location.displayName).Append(':').Append(location.publicArea).Append(':').Append(location.restricted)
                    .Append(':').Append(string.Join(",", (location.connectedLocationIds ?? new string[0]).OrderBy(x => x, StringComparer.Ordinal)))
                    .Append(':').Append(string.Join(",", (location.accessTags ?? new string[0]).OrderBy(x => x, StringComparer.Ordinal)));
            foreach (var item in scenario.world.Objects.OrderBy(item => item.id, StringComparer.Ordinal))
                text.Append('|').Append(item.id).Append(':').Append(item.displayName).Append(':').Append(item.ownerRoleId).Append(':').Append(item.locationId);
            foreach (var e in scenario.events.OrderBy(e => e.sequence))
                text.Append('|').Append(e.eventId).Append(':').Append(e.sequence.ToString(culture)).Append(':').Append(e.minute.ToString(culture)).Append(':').Append(((int)e.eventType).ToString(culture)).Append(':')
                    .Append(e.actorPlayerId).Append(':').Append(e.actorRoleId).Append(':').Append(e.locationId).Append(':').Append(e.targetObjectId)
                    .Append(':').Append(e.description).Append(':').Append(string.Join(",", e.requiresEventIds ?? new string[0]));
            foreach (var c in scenario.clues.OrderBy(c => c.clueId, StringComparer.Ordinal))
                text.Append('|').Append(c.clueId).Append(':').Append(c.ownerPlayerId).Append(':').Append(c.sourceEventId).Append(':')
                    .Append(((int)c.informationType).ToString(culture)).Append(':').Append(((int)c.truthType).ToString(culture)).Append(':').Append(c.strength.ToString(culture)).Append(':')
                    .Append(c.criticalEvidence).Append(':').Append(c.falseLead).Append(':').Append(c.text);
            foreach (var secret in scenario.playerSecrets.OrderBy(s => s.playerId, StringComparer.Ordinal))
                text.Append('|').Append(secret.playerId).Append(':').Append(secret.roleId).Append(':').Append(secret.isThief).Append(':').Append(secret.privateSituation)
                    .Append(':').Append(string.Join(",", secret.clueIds.OrderBy(x => x, StringComparer.Ordinal)));
            unchecked
            {
                uint hash = 2166136261;
                var bytes = Encoding.UTF8.GetBytes(text.ToString());
                for (var i = 0; i < bytes.Length; i++) { hash ^= bytes[i]; hash *= 16777619; }
                return hash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        public static string ComputePublicHash(RuntimeScenario scenario)
        {
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            var value = scenario.scenarioId + "|" + scenario.scenarioVersion.ToString(culture) + "|" + scenario.playerCount.ToString(culture) + "|" + scenario.title + "|" + scenario.publicDescription + "|" + scenario.targetObjectId;
            unchecked
            {
                uint hash = 2166136261;
                foreach (var b in Encoding.UTF8.GetBytes(value)) { hash ^= b; hash *= 16777619; }
                return hash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture);
            }
        }
    }

    public sealed class TimelineGenerator
    {
        public List<ScenarioEvent> Generate(ScenarioTemplate template, WorldState world, IReadOnlyList<PlayerSecret> secrets,
            IReadOnlyDictionary<string, string> roleToPlayer, ScenarioObjectData target, SeededRandomService random)
        {
            var events = new List<ScenarioEvent>();
            var thief = secrets.First(s => s.isThief);
            var innocentRoles = secrets.Where(s => !s.isThief).Select(s => s.roleId).ToList();
            var falseLeadRole = innocentRoles[random.Next(0, innocentRoles.Count)];
            var baseMinute = 0;
            foreach (var rule in template.eventTemplates)
            {
                string actorRole = rule.actorIsThief ? thief.roleId : rule.actorIsFalseLead ? falseLeadRole : rule.actorRoleId;
                string actorPlayer = null;
                if (!string.IsNullOrEmpty(actorRole) && !roleToPlayer.TryGetValue(actorRole, out actorPlayer))
                {
                    if (rule.optionalIfRoleUnavailable) continue;
                    throw new ScenarioGenerationException("Event '" + rule.id + "' needs unavailable role '" + actorRole + "'.");
                }
                var locationId = rule.useTargetLocation ? target.locationId : rule.useEscapeLocation ? template.escapeLocationId : rule.locationId;
                var eventMinute = Math.Max(baseMinute, rule.minuteOffset + random.Next(0, 2));
                baseMinute = eventMinute;
                var description = Describe(rule.type, template, target, actorRole, locationId);
                var scenarioEvent = new ScenarioEvent
                {
                    eventId = rule.id,
                    sequence = events.Count,
                    minute = eventMinute,
                    eventType = rule.type,
                    actorPlayerId = actorPlayer,
                    actorRoleId = actorRole,
                    locationId = locationId,
                    targetObjectId = (rule.type == EventType.Theft || rule.type == EventType.Escape) ? target.id : rule.targetObjectId,
                    description = description,
                    requiresEventIds = rule.requiresEventIds ?? new string[0],
                    critical = rule.critical
                };
                events.Add(scenarioEvent);
                world.Events.Add(scenarioEvent);
            }
            return events;
        }

        private static string Describe(EventType type, ScenarioTemplate template, ScenarioObjectData target, string actorRole, string locationId)
        {
            var location = ScenarioGenerator.FindLocation(template, locationId);
            switch (type)
            {
                case EventType.Gathering: return "The palace gathering begins in the " + location.displayName + ".";
                case EventType.Movement: return actorRole + " moves toward the " + location.displayName + ".";
                case EventType.Access: return actorRole == null ? "The " + location.displayName + " is accessible during " + template.distractionName + "." : actorRole + " remains at the " + location.displayName + ".";
                case EventType.Distraction: return template.distractionName + " draws attention away from the " + location.displayName + ".";
                case EventType.Theft: return actorRole + " takes the " + target.displayName + " from the " + location.displayName + ".";
                case EventType.Escape: return actorRole + " leaves by the " + location.displayName + " after the theft.";
                case EventType.Discovery: return "The missing " + target.displayName + " is discovered after the gathering.";
                default: return actorRole + " is observed near the " + location.displayName + ".";
            }
        }
    }

    public sealed class ClueGenerator
    {
        public List<RuntimeClue> Generate(ScenarioTemplate template, IReadOnlyList<ScenarioEvent> events,
            IReadOnlyList<PlayerSecret> secrets, IReadOnlyDictionary<string, string> roleToPlayer,
            ScenarioObjectData target, DifficultyProfile profile)
        {
            var result = new List<RuntimeClue>();
            var ambiguousRemaining = profile.ambiguityCount;
            var falseLeadsRemaining = profile.falseLeadCount;
            foreach (var clueTemplate in template.clueTemplates)
            {
                if (clueTemplate.falseLead && falseLeadsRemaining <= 0) continue;
                var source = !string.IsNullOrEmpty(clueTemplate.eventId)
                    ? events.FirstOrDefault(e => e.eventId == clueTemplate.eventId)
                    : events.FirstOrDefault(e => e.eventType == clueTemplate.sourceEventType);
                if (source == null) continue;
                string ownerId = null;
                if (clueTemplate.preferredRoleId == "thief") ownerId = secrets.First(s => s.isThief).playerId;
                else if (!string.IsNullOrEmpty(clueTemplate.preferredRoleId)) roleToPlayer.TryGetValue(clueTemplate.preferredRoleId, out ownerId);
                var owner = secrets.FirstOrDefault(s => s.playerId == ownerId && (clueTemplate.preferredRoleId == "thief" || !s.isThief));
                if (owner == null) owner = secrets.Where(s => !s.isThief).OrderBy(s => s.clueIds.Count).ThenBy(s => s.playerId, StringComparer.Ordinal).First();

                var useAmbiguous = ambiguousRemaining > 0 && !string.IsNullOrEmpty(clueTemplate.ambiguousText)
                                   && (!clueTemplate.criticalEvidence || profile.difficulty == ScenarioDifficulty.Hard);
                var text = useAmbiguous ? clueTemplate.ambiguousText : clueTemplate.text;
                if (useAmbiguous) ambiguousRemaining--;
                var location = ScenarioGenerator.FindLocation(template, source.locationId);
                var actor = template.roles.FirstOrDefault(r => r.id == source.actorRoleId);
                text = Expand(text, template, target, location, source, actor);
                var clue = new RuntimeClue
                {
                    clueId = clueTemplate.id,
                    ownerPlayerId = owner.playerId,
                    sourceEventId = source.eventId,
                    informationType = clueTemplate.informationType,
                    truthType = useAmbiguous ? TruthType.PartialTruth : clueTemplate.truthType,
                    strength = useAmbiguous ? Math.Max(1, clueTemplate.strength - 1) : clueTemplate.strength,
                    criticalEvidence = clueTemplate.criticalEvidence,
                    falseLead = clueTemplate.falseLead,
                    text = text
                };
                result.Add(clue);
                if (clueTemplate.falseLead) falseLeadsRemaining--;
            }
            while (falseLeadsRemaining > 0)
            {
                var lead = result.FirstOrDefault(c => c.falseLead);
                if (lead == null) break;
                result.Add(new RuntimeClue
                {
                    clueId = lead.clueId + "-alt-" + falseLeadsRemaining,
                    ownerPlayerId = lead.ownerPlayerId,
                    sourceEventId = lead.sourceEventId,
                    informationType = lead.informationType,
                    truthType = TruthType.PartialTruth,
                    strength = Math.Max(1, lead.strength - 1),
                    falseLead = true,
                    text = "You remember seeing the " + template.roles.First(r => r.id == events.First(e => e.eventId == lead.sourceEventId).actorRoleId).displayName + " near the Treasury before the commotion; you could not tell what brought them there."
                });
                falseLeadsRemaining--;
            }
            return result;
        }

        private static string Expand(string text, ScenarioTemplate template, ScenarioObjectData target,
            ScenarioLocationData sourceLocation, ScenarioEvent source, ScenarioRoleData actor)
        {
            return (text ?? string.Empty)
                .Replace("{target}", target.displayName)
                .Replace("{targetLocation}", ScenarioGenerator.FindLocation(template, target.locationId).displayName)
                .Replace("{escapeLocation}", ScenarioGenerator.FindLocation(template, template.escapeLocationId).displayName)
                .Replace("{location}", sourceLocation.displayName)
                .Replace("{actorRole}", actor == null ? "someone" : actor.displayName)
                .Replace("{distraction}", template.distractionName)
                .Replace("{time}", "21:" + source.minute.ToString("00", System.Globalization.CultureInfo.InvariantCulture));
        }
    }
}
