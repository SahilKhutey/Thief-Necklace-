# Thief & the Necklace

## P2 — Scenario, Role & Clue Generation Specification

**Status:** Design specification  
**Parent:** P0 Game Vision & Core Rules; P1 Core Deduction Mechanics  
**Target engine:** Unity  
**MVP:** 4–6 players  
**Primary output:** A logically valid, replayable mystery round

P0 defines secret roles, one hidden Thief, discussion, voting, reveal, and scoring. P1 defines facts, observations, partial truths, contradictions, ambiguity, and deception. P2 defines how the game creates a mystery from structured content.

## 1. Objective and design philosophy

The system combines authored content, rules, and controlled variables to create rounds without manually scripting every possible story:

```text
STATIC CONTENT + RULES + SEEDED VARIABLES
                   ↓
             GENERATED ROUND
```

Each round must give innocents enough evidence to reason toward the Thief while giving the Thief enough ambiguity to construct a believable defense. The generator produces a scenario, world truth, characters, roles, culprit, timeline, events, private information, and ambiguity. It does not generate arbitrary prose first and try to invent a consistent story afterward.

## 2. Generation pipeline

```text
SCENARIO TEMPLATE
       ↓
BUILD WORLD
       ↓
SELECT VALID ROLE SET
       ↓
ASSIGN ROLES + ONE THIEF OBJECTIVE
       ↓
GENERATE TIMELINE + EVENTS
       ↓
GENERATE SITUATIONS + CLUES
       ↓
DISTRIBUTE PRIVATE INFORMATION
       ↓
ADD CONTROLLED AMBIGUITY / FALSE LEAD
       ↓
VALIDATE ROUND
       ↓
LOCK + SEND PUBLIC / PRIVATE DATA
```

The authority generates one round. Clients never independently generate roles, clues, or the timeline.

## 3. Scenario template

A scenario template is a reusable blueprint. The initial template is **The Royal Necklace**:

- **Setting:** Royal Palace
- **Target:** Royal Necklace
- **Critical location:** Royal Chamber
- **Incident:** The Necklace disappears
- **Primary distraction:** Temporary blackout
- **Discovery:** The open display case is noticed after the lights return

Each scenario definition contains:

- **Identity:** ID, title, theme, public description, supported player counts.
- **World:** locations, connections, characters, objects, access rules, visibility.
- **Incident:** target object, incident type, opportunity requirements, discovery condition.
- **Timeline:** setup, critical events, escape, discovery, event dependencies.
- **Roles:** compatible role set, role tags, point values, minimum/maximum players.
- **Content rules:** situation and clue templates, evidence coverage, ambiguity constraints.
- **Difficulty:** evidence-density and false-lead profile.

## 4. World model

The world is a logical graph, not a simulated explorable environment. For example:

```text
Banquet Hall
  ├── West Corridor ── West Gate
  ├── East Corridor ── Royal Chamber
  ├── Treasury
  └── Garden
```

Each location has an ID, display name, access category, visibility, importance, tags, and connected locations. Example access values are public, semi-public, restricted, and guarded. These properties constrain which movements and observations are plausible.

Important objects also have IDs and properties: the Necklace, Treasury Key, Candle, Letter, Small Box. The target object must exist in the scenario world and be reachable by the theft event.

## 5. Roles and Thief assignment

Role identity and guilt remain separate. A role defines identity, social position, and point value; it does not define an alibi, truthfulness, or suspect status. Store `roleId` independently from `isThief`.

Suggested MVP roles and point values, carried forward from P1:

| Role | Points | Example tags |
|---|---:|---|
| King | 8 | royal, host |
| Queen | 7 | royal, guest |
| Minister | 6 | royal, official |
| Soldier | 5 | guard, palace |
| Servant | 4 | staff, palace |
| Guard | 5 | guard, palace (updated by the later P7 scoring configuration) |

Use a validated, configurable role pool. For the first authored packs, curated sets may be used: four players (King, Minister, Soldier, Servant); five players (King, Queen, Minister, Soldier, Servant); six players (those five plus Guard or Guest). Exactly one player is independently assigned the Thief objective. Do not add a second “Thief role” that consumes a player slot.

The selection process must verify that all selected roles are unique and compatible with the scenario. The authoritative host/server randomly selects exactly one of the dealt players as Thief. Role point values affect scoring, never vote power.

## 6. Timeline, events, and dependencies

The scenario engine stores the canonical truth before generating any player-facing clue. A timeline is an ordered list of events with actor, time, location, object, event type, and dependencies. Suggested event types:

```csharp
public enum EventType
{
    Movement,
    Conversation,
    Observation,
    Access,
    Distraction,
    Theft,
    Escape,
    Discovery
}
```

Events form a causal sequence. For example:

```text
Blackout → visibility reduced → Thief enters chamber
         → Necklace taken → Thief exits → lights return
         → missing Necklace discovered
```

The validator checks event ordering and dependencies. A theft requires an existing target, access, and opportunity. The thief must reach the target location, take the object, and have an allowed escape path before discovery. A scenario may change the event names, but must preserve a complete causal chain: start, buildup, opportunity/distraction, theft, escape, discovery.

## 7. Situations and clue generation

Every player receives a short private situation, such as “You were stationed at the western gate” or “You were speaking with the Queen before the blackout.” Situation sentences should be rendered from structured templates, not stored as every possible unique sentence:

```text
"You were {action} at {location} {timeOrEvent}."
```

The generator resolves template variables from the canonical world model and validates that the rendered sentence remains consistent with it.

A clue is derived from an event, observation opportunity, or relationship in the world model. One event may produce different perspectives:

- **Direct observation:** “You saw the Soldier leave the gate.”
- **Indirect observation:** “You heard movement near the gate.”
- **Relationship:** “The Queen appeared to know where the Minister was.”
- **Timeline:** “The Minister left shortly before the blackout.”
- **Object:** “Someone was carrying a small box.”

Each clue has a clue ID, owner player ID, source event ID(s), information type, fact/observation type, strength, text/template ID, known variables, and optional relations to other clues. The world engine may store these as ScriptableObjects or serialized data; the runtime should not rely on sentence text as truth.

### Truth semantics

P1's distinction remains authoritative:

- Innocent private clues are grounded in hard facts or genuine observations; the generator must not randomly lie to innocents.
- Partial truth describes incomplete but accurate information.
- Deception is a player statement that conflicts with the world truth, not a false private clue assigned by the generator.
- The Thief can receive a real private situation, including their own action, and manually choose what to say publicly.

For a clue like “You saw someone enter the Royal Chamber during the blackout,” the event and observation are true while the actor identity remains unknown. The game must not expose internal truth labels during discussion.

## 8. Clue perspectives, strength, and ambiguity

Clue strength is an internal authoring/balance field:

| Strength | Example |
|---:|---|
| 1 — Weak | “You heard something nearby.” |
| 2 — Moderate | “You heard footsteps near the chamber.” |
| 3 — Strong | “You saw someone enter the chamber.” |
| 4 — Very strong | “You saw the Minister enter the chamber.” |

Strength 4 should still not automatically prove guilt. Avoid direct-reveal text such as “You saw the Thief steal the Necklace” in normal MVP scenarios. Prefer useful observations with at least one uncertain variable: “You saw someone near the Necklace display during the blackout.”

Ambiguity must be intentional. It should leave space for competing interpretations, not produce empty vagueness (“something felt wrong”). The Thief mainly exploits unknown actor, timing, or object details. Players can reduce unknowns through questions and corroboration.

## 9. Evidence distribution and relationships

Generate and validate clues before assigning owners. Then distribute information so no player receives all critical evidence and no player is left without a useful contribution.

For every critical event, target at least two independent evidence paths. Independence means different observations or event links, not repeated wording. For example:

- Evidence A: the Servant heard footsteps near the chamber.
- Evidence B: the Queen noticed the chamber door open.
- Evidence C: the Soldier saw someone leave the east corridor.

Clue relationships can include `SUPPORTS`, `CONTRADICTS`, `EXTENDS`, `QUALIFIES`, `LOCATES`, `TIMES`, and `IDENTIFIES`. Relationships are derived from referenced world events and structured facts. The generator must not invent a contradiction arbitrarily; two clues conflict only if their underlying claims cannot both be true under the timeline and world rules. A contradiction is not equivalent to Thief guilt.

Starting Normal six-player targets for playtesting:

- Six private situations.
- One or two supporting clues per player (six to ten total supporting clues).
- Two or more independent paths to critical events.
- Two or three cross-check relationships.
- One or two controlled ambiguities.
- One plausible false lead and a secondary suspicious player.

These are tuning targets, not immutable generation quotas.

## 10. False leads, alibi support, and suspect distribution

Each Normal scenario should have at least one plausible innocent suspect. For example, the Minister may genuinely move toward the treasury for an innocent reason. That gives the Thief a fact-based opportunity to reinforce suspicion. The reveal should explain why the false lead looked plausible.

Some players may have stronger corroboration than others: the Queen can confirm the King's earlier location; gate evidence can support the Soldier; another role may have no direct witness; the Thief's truth is hidden. This produces differentiated suspicion without hard-coding guilt to a role.

An initial six-player balance target is one Thief, one primary false lead, one secondary suspect, and three relatively supported players. This is not a required fixed pattern and should not make the Thief mechanically obvious.

## 11. Difficulty profiles and information budget

```csharp
public enum Difficulty
{
    Easy,
    Normal,
    Hard
}
```

| Difficulty | Evidence | Ambiguity | False leads | Intended feel |
|---|---|---|---|---|
| Easy | High | Low | Few | Evidence reasonably converges. |
| Normal | Medium | Medium | One strong lead | Multiple explanations remain discussable. |
| Hard | More indirect | High | Several plausible suspects | More reasoning is needed, not random guessing. |

Difficulty adjusts clue strength, corroboration, and unknown information. It must not simply remove evidence until a round becomes guesswork. A later internal score may estimate difficulty as evidence ambiguity + contradiction complexity + plausible suspects − independent confirmation. It must never be shown as a player-facing suspicion score.

## 12. Complete example: The Royal Necklace

### Canonical world truth

| Time | Event |
|---|---|
| 21:00 | Banquet begins. |
| 21:07 | Queen leaves the banquet temporarily. |
| 21:09 | Minister heads toward the Treasury. |
| 21:11 | Servant passes through the East Corridor. |
| 21:12 | Blackout begins. |
| 21:13 | Minister enters the Royal Chamber. |
| 21:14 | Minister takes the Necklace and exits via the East Corridor. |
| 21:15 | Lights return. |
| 21:16 | The missing Necklace is discovered. |

The Minister is secretly assigned the Thief objective. The Minister role itself does not imply guilt.

### Private situations and supporting clues

| Player | Situation | Private clue |
|---|---|---|
| P1 — King | You were speaking with the Queen shortly before the blackout. | The Queen was with you before she left. |
| P2 — Queen | You left the banquet shortly before the blackout. | You saw the Minister heading toward the Treasury. |
| P3 — Minister / Thief | You entered the Royal Chamber during the blackout. | You noticed the chamber was unusually quiet. |
| P4 — Soldier | You guarded the Western Gate during the blackout. | No one passed through the Western Gate during the blackout. |
| P5 — Servant | You walked through the East Corridor shortly before the blackout. | You heard a door open near the Royal Chamber. |
| P6 — Guard | You watched the eastern side of the palace. | You saw someone moving toward the East Corridor after the blackout. |

The clue text above is schematic and should be reviewed against exact event times before being packaged. Ensure every innocent statement remains possible under the final timeline and observation rules. Players should have multiple candidates: the Queen left early, the Minister moved toward a restricted area, the Servant was near the corridor, and the Guard saw an unidentified person. The King can partially corroborate the Queen; the eastern clues support an escape route without naming the Thief. The Minister can truthfully mention checking documents and redirect attention to the Servant.

## 13. Round validator

Generation produces a candidate round. It must pass validation before any client receives private information.

### Required checks

1. **Single Thief:** `thiefCount == 1`.
2. **Role validity:** role set meets player-count rules, contains no invalid duplicates, and every role is compatible with the scenario.
3. **Complete timeline:** events are ordered and the start, buildup, opportunity, theft, escape, and discovery exist.
4. **Dependencies:** all event dependencies are satisfied.
5. **Theft causality:** the Thief can access the location and target during the opportunity and has a valid escape route.
6. **Reference integrity:** clue owners, actors, locations, objects, and source events exist.
7. **Information consistency:** innocent situations and clues can coexist with world truth.
8. **Critical coverage:** every critical event has at least two independent evidence paths for MVP.
9. **Distribution:** no player receives another player's secret; evidence is spread and every player has a useful contribution.
10. **Direct-reveal protection:** no normal clue explicitly identifies the Thief as thief or directly states that they stole the target.
11. **False lead and ambiguity:** at least one plausible innocent suspect and an explainable ambiguity exist.
12. **Thief playability:** the Thief has a plausible true statement, an ambiguous element, and an alternative explanation.
13. **Innocent playability:** the group has cross-checks and a reasonable path to a suspect.

Invalid candidates are rejected and regenerated with a bounded retry count. If generation repeatedly fails, load a known-valid authored fallback for that player count and report a content-generation diagnostic to the host logs. Never send a partially validated mystery to clients.

## 14. Seeded generation and reproducibility

Every round uses an authority-created seed. The seed and generator/content version allow deterministic reproduction:

```text
Seed → scenario choice → role set/order → Thief assignment
     → timeline variables → clue selection → clue distribution
```

Store the seed with the match record and diagnostics. A report such as “Round 839 had impossible clues” should be reproducible from its seed plus content and generator version. Use a deterministic random service rather than unrelated calls to global random state.

## 15. Data-driven Unity content

Separate content, logic, and presentation:

- **Content:** scenario text, roles, locations, event and clue templates.
- **Logic:** world construction, seeded generation, distribution, validation.
- **Presentation:** card frames, UI, animation, and rendered text.

Scenario content can use ScriptableObject assets for authoring, with serializable runtime snapshots for network transmission. Role tags and scenario tags support compatibility. For example, a `Soldier` tagged `royal`, `guard`, `human` can match a `Royal Palace` scenario tagged `royal`, `palace`, `theft`.

Suggested asset structure:

```text
ScenarioData
├── scenarioId, title, description, tags
├── locations[]
├── objects[]
├── roleRules[]
├── eventTemplates[]
├── clueTemplates[]
├── situationTemplates[]
├── difficultyRules
└── validationRules
```

Illustrative Unity definition:

```csharp
[CreateAssetMenu(fileName = "Scenario_RoyalNecklace",
    menuName = "ThiefAndNecklace/Scenario")]
public class ScenarioData : ScriptableObject
{
    public string scenarioId;
    public string title;
    [TextArea] public string description;
    public LocationData[] locations;
    public ObjectData[] objects;
    public EventTemplate[] eventTemplates;
    public ClueTemplate[] clueTemplates;
    public SituationTemplate[] situationTemplates;
}
```

Template assets should refer to structured IDs and constraints. Do not treat rendered text as the authoritative world state.

## 16. Suggested runtime data model

This sketch extends P1 and is design guidance, not finished production code:

```csharp
[Serializable]
public class ScenarioEvent
{
    public string eventId;
    public int sequence;
    public int timeMinutes;
    public string actorId;
    public string locationId;
    public string targetObjectId;
    public EventType eventType;
    public List<string> dependsOnEventIds;
    public string description;
}

[Serializable]
public class ClueData
{
    public string clueId;
    public string ownerPlayerId;
    public List<string> sourceEventIds;
    public InformationType informationType;
    public FactType factType;
    public int strength;
    public string templateId;
    public string renderedText;
}

[Serializable]
public class PlayerSecret
{
    public string playerId;
    public string roleId;
    public List<string> clueIds;
    public string privateSituation;
    public bool isThief;
}
```

Keep world truth, all clues, and all secrets in the authoritative round model. Create separate public and per-player private payloads for networking. A future implementation should use enums and schema validation rather than free-form truth labels.

## 17. Generation algorithm

```csharp
GenerateRound(playerCount, difficulty, seed)
{
    var rng = CreateDeterministicRandom(seed);
    var scenario = SelectCompatibleScenario(playerCount, rng);
    var world = BuildWorld(scenario);
    var roles = SelectValidRoles(scenario, playerCount, rng);
    var thief = SelectThief(roles, rng);
    var timeline = GenerateTimeline(world, roles, thief, difficulty, rng);
    var events = BuildEvents(timeline);
    var situations = GenerateSituations(events, roles, thief, rng);
    var clues = GenerateClues(events, situations, difficulty, rng);
    var distribution = DistributeClues(clues, roles, rng);
    var candidate = BuildCandidate(scenario, world, roles, thief,
                                   timeline, events, situations, distribution, seed);

    return Validate(candidate) ? candidate : RetryOrUseFallback(...);
}
```

Generation order matters: truth first, evidence second, language last. Text variation may later use an AI layer, but only after structured truth and clue semantics are validated. AI must never author authoritative truth.

## 18. Security and data ownership

The authority owns the full mystery. Never broadcast globally:

- True Thief identity.
- Full timeline or hidden event data.
- All clue assignments.
- Other players' role/objective or private situation.

Each client receives the public scenario plus only that player's role and private information. At reveal, the authority sends the authorized reveal data. Critical assignment, validation, and scoring remain authoritative, following the networking requirement in the supplied GDD.

## 19. Architecture responsibilities

```text
ScenarioManager
├── ScenarioSelector
├── WorldBuilder
├── RoleGenerator
├── ThiefSelector
├── TimelineGenerator
├── EventGenerator
├── SituationGenerator
├── ClueGenerator
├── ClueDistributor
└── ScenarioValidator

Supporting:
SeedManager · RandomService · TemplateResolver · DifficultyProfile
```

At match start, the manager knows player count, selects compatible content, generates and validates the round, locks the result, and distributes public/private payloads. UI and chat consume these payloads; they do not regenerate or infer hidden truth.

## 20. Validation and balance tests

Future automated tests should cover:

- Valid scenario load and invalid scenario rejection.
- Four-, five-, and six-player role configurations; exactly one Thief.
- Location graph references and reachability.
- Timeline ordering and event dependencies.
- Theft access/opportunity and discovery after theft.
- Clue references, owner privacy, and consistency with world truth.
- Critical-event evidence coverage and no accidental direct reveal.
- Distribution fairness and no secret leakage.
- Seed reproducibility for the same content/generator version.
- Fallback behavior after bounded generation retries.

For offline balance evaluation, generate large batches (eventually thousands) and measure invalid-round rate, direct-reveal rate, low-evidence rate, over-obvious rate, no-plausible-suspect rate, and excessive-ambiguity rate. These are future test/analytics tasks, not required product UI.

## 21. P2 definition of done

P2 is ready for implementation when the design and content format can reliably produce:

- A valid scenario and compatible role configuration for 4–6 players.
- Exactly one hidden Thief objective, independent of role identity.
- A coherent world, ordered timeline, reachable theft, and escape path.
- A private situation and useful clue for each player.
- Overlapping evidence, controlled ambiguity, and a plausible false lead.
- A playable Thief defense and a reasonable innocent deduction path.
- No accidental direct reveal or impossible innocent clue.
- Deterministic seeded generation and validation before distribution.
- Secure separation between world truth and each client's private payload.

## 22. P2 design lock and P3 handoff

The core architecture rule is:

> The game generates a structured truth model first, then derives events, evidence, private information, and player-facing text from it.

```text
TRUTH → EVENTS → EVIDENCE → PRIVATE INFORMATION
      → PLAYER STATEMENTS → HUMAN DEDUCTION
```

P3 — Round/Game-State Architecture should turn this content system into Unity runtime behavior: lobby, match start, authoritative generation, private distribution, discussion, chat, voting, resolution, reveal, score, and round reset. P3 should define manager ownership, state transitions, events, networking contracts, private-data synchronization, and production C# implementation.
