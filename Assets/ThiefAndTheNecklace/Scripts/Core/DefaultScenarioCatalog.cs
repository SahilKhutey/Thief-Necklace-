using System.Collections.Generic;

namespace ThiefAndTheNecklace
{
    public static class DefaultScenarioCatalog
    {
        public static ScenarioRegistry CreateRegistry()
        {
            return new ScenarioRegistry(new[]
            {
                Create("ROYAL_NECKLACE_01", "The Royal Necklace", "The King's necklace vanished during an unexpected blackout.", "a sudden blackout", "necklace", "The King's Necklace", "king", "chamber", "east_corridor"),
                Create("ROYAL_NECKLACE_02", "The Missing Royal Seal", "The royal seal disappeared during a surprise court announcement.", "a surprise announcement", "royal_seal", "The Royal Seal", "king", "treasury", "west_gate"),
                Create("ROYAL_NECKLACE_03", "The Secret Letter", "A sealed letter vanished during the palace festival preparations.", "festival preparations", "secret_letter", "The Secret Letter", "minister", "minister_office", "east_corridor"),
                Create("ROYAL_MYSTERY_04", "The Vanished Gate Key", "The western gate key disappeared during a shift change.", "the guard shift change", "gate_key", "The Western Gate Key", "soldier", "west_gate", "east_corridor"),
                Create("ROYAL_MYSTERY_05", "The Hidden Heirloom", "A royal heirloom went missing while guests gathered in the palace garden.", "the garden reception", "royal_heirloom", "The Royal Heirloom", "queen", "garden", "east_corridor"),
                Create("ROYAL_MYSTERY_06", "The Disappearing Treaty", "A signed treaty vanished during preparations for a royal audience.", "the audience preparations", "signed_treaty", "The Signed Treaty", "minister", "minister_office", "west_gate")
            });
        }

        private static ScenarioTemplate Create(string id, string title, string description, string distraction, string objectId,
            string objectName, string owner, string targetLocation, string escapeLocation)
        {
            return new ScenarioTemplate
            {
                scenarioId = id,
                version = 1,
                themeTag = "royal",
                title = title,
                publicDescription = description,
                distractionName = distraction,
                targetLocationId = targetLocation,
                escapeLocationId = escapeLocation,
                locations = CreateLocations(),
                objects = new[] { new ScenarioObjectData { id = objectId, displayName = objectName, ownerRoleId = owner, locationId = targetLocation, importance = "critical" } },
                roles = CreateRoles(),
                eventTemplates = CreateEvents(objectId),
                clueTemplates = CreateClues(),
                difficulties = new[]
                {
                    new DifficultyProfile { difficulty = ScenarioDifficulty.Easy, minCluesPerPlayer = 1, maxCluesPerPlayer = 3, minimumEvidencePaths = 3, falseLeadCount = 0, ambiguityCount = 0 },
                    new DifficultyProfile { difficulty = ScenarioDifficulty.Normal, minCluesPerPlayer = 1, maxCluesPerPlayer = 2, minimumEvidencePaths = 2, falseLeadCount = 1, ambiguityCount = 1 },
                    new DifficultyProfile { difficulty = ScenarioDifficulty.Hard, minCluesPerPlayer = 1, maxCluesPerPlayer = 3, minimumEvidencePaths = 2, falseLeadCount = 2, ambiguityCount = 2 }
                }
            };
        }

        private static ScenarioLocationData[] CreateLocations()
        {
            return new[]
            {
                Location("hall", "Grand Hall", true, false, new[] { "east_corridor", "garden", "west_gate" }, "royal"),
                Location("chamber", "Royal Chamber", false, true, new[] { "east_corridor" }, "royal"),
                Location("treasury", "Treasury", false, true, new[] { "east_corridor" }, "royal", "administrative"),
                Location("minister_office", "Minister's Office", false, true, new[] { "east_corridor" }, "royal", "administrative"),
                Location("east_corridor", "East Corridor", true, false, new[] { "hall", "chamber", "treasury", "minister_office" }, "royal", "service"),
                Location("garden", "Palace Garden", true, false, new[] { "hall" }, "royal"),
                Location("west_gate", "Western Gate", false, true, new[] { "hall" }, "royal", "security")
            };
        }

        private static ScenarioLocationData Location(string id, string name, bool isPublic, bool restricted, string[] connected, params string[] tags)
            => new ScenarioLocationData { id = id, displayName = name, connectedLocationIds = connected, publicArea = isPublic, restricted = restricted, accessTags = tags };

        private static ScenarioRoleData[] CreateRoles()
        {
            return new[]
            {
                Role("king", "King", 8, "The ruler of the palace.", "royal", "relationship"),
                Role("queen", "Queen", 7, "The Queen of the palace.", "royal", "relationship"),
                Role("minister", "Minister", 6, "A trusted court official.", "royal", "administrative"),
                Role("soldier", "Soldier", 5, "A disciplined palace defender.", "royal", "security"),
                Role("servant", "Servant", 4, "An observant member of the household.", "royal", "service"),
                Role("guard", "Guard", 5, "A palace security officer.", "royal", "security")
            };
        }

        private static ScenarioRoleData Role(string id, string name, int value, string description, string tag, string preferred)
            => new ScenarioRoleData { id = id, displayName = name, pointValue = value, description = description, tags = new[] { tag }, preferredInformation = new[] { preferred } };

        private static EventTemplate[] CreateEvents(string objectId)
        {
            return new[]
            {
                Event("gathering", EventType.Gathering, null, "hall", 0, false),
                Event("departure", EventType.Movement, "queen", "garden", 7, false, optional: true),
                Event("false-lead", EventType.Movement, null, "treasury", 9, false, falseLead: true),
                Event("guard-post", EventType.Access, "soldier", "west_gate", 10, false),
                Event("distraction", EventType.Distraction, null, "hall", 12, false),
                Event("thief-entry", EventType.Access, null, null, 13, true, thief: true, targetLocation: true, requires: new[] { "distraction" }),
                Event("theft", EventType.Theft, null, null, 14, true, thief: true, targetLocation: true, targetId: objectId, requires: new[] { "thief-entry" }),
                Event("thief-escape", EventType.Escape, null, null, 15, true, thief: true, escapeLocation: true, targetId: objectId, requires: new[] { "theft" }),
                Event("discovery", EventType.Discovery, null, "hall", 16, true, requires: new[] { "thief-escape" })
            };
        }

        private static EventTemplate Event(string id, EventType type, string actor, string location, int minute, bool critical,
            bool thief = false, bool falseLead = false, bool optional = false, bool targetLocation = false,
            bool escapeLocation = false, string targetId = null, string[] requires = null)
        {
            return new EventTemplate
            {
                id = id, type = type, actorRoleId = actor, actorIsThief = thief, actorIsFalseLead = falseLead,
                optionalIfRoleUnavailable = optional, locationId = location, useTargetLocation = targetLocation,
                useEscapeLocation = escapeLocation, targetObjectId = targetId, minuteOffset = minute,
                critical = critical, requiresEventIds = requires ?? new string[0]
            };
        }

        private static ClueTemplate[] CreateClues()
        {
            return new[]
            {
                Clue("target", "gathering", EventType.Gathering, "king", InformationType.Object, TruthType.HardFact, 2, false, false,
                    "The {target} was on display in the {targetLocation} before the gathering.", null),
                Clue("departure", "departure", EventType.Movement, "minister", InformationType.Time, TruthType.Observation, 2, false, false,
                    "You saw the Queen leave for the garden before the {distraction}.", "You remember the Queen left before the commotion, but not exactly when."),
                Clue("guard", "guard-post", EventType.Access, "soldier", InformationType.Location, TruthType.HardFact, 2, false, false,
                    "The western gate remained closed during the {distraction}.", null),
                Clue("entry-sound", "thief-entry", EventType.Access, "servant", InformationType.Witness, TruthType.Observation, 3, true, false,
                    "You heard a door near the {targetLocation} open during the {distraction}.", "You heard a door somewhere nearby during the {distraction}."),
                Clue("escape-movement", "thief-escape", EventType.Escape, "queen", InformationType.Witness, TruthType.Observation, 3, true, false,
                    "When the {distraction} ended, someone hurried through the {escapeLocation}.", "After the commotion, you noticed movement near a corridor."),
                Clue("thief-context", "distraction", EventType.Distraction, "thief", InformationType.Action, TruthType.PartialTruth, 1, false, false,
                    "You know the {distraction} gave people a reason to leave their places briefly.", null),
                Clue("false-lead", "false-lead", EventType.Movement, "guard", InformationType.Witness, TruthType.Observation, 2, false, true,
                    "You noticed the {actorRole} heading toward the Treasury shortly before the {distraction}.", "You noticed someone heading toward an administrative room before the commotion."),
                Clue("discovery", "discovery", EventType.Discovery, "king", InformationType.Object, TruthType.HardFact, 1, true, false,
                    "The {target} was discovered missing after everyone returned to the Grand Hall.", null)
            };
        }

        private static ClueTemplate Clue(string id, string eventId, EventType type, string ownerRole, InformationType information,
            TruthType truth, int strength, bool critical, bool falseLead, string text, string ambiguous)
        {
            return new ClueTemplate
            {
                id = id, eventId = eventId, sourceEventType = type, preferredRoleId = ownerRole,
                informationType = information, truthType = truth, strength = strength,
                criticalEvidence = critical, falseLead = falseLead, text = text, ambiguousText = ambiguous
            };
        }
    }
}
