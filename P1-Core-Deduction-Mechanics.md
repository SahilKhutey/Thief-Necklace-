# Thief & the Necklace

## P1 — Core Deduction Mechanics Specification

**Document:** P1 Core Deduction Mechanics Specification  
**Status:** Design / system foundation  
**Parent:** P0 Game Vision & Core Rules  
**Engine target:** Unity  
**MVP players:** 4–6  
**Primary loop:** Observe → Question → Explain → Contradict → Suspect → Vote → Reveal

P0 established secret role assignment, one hidden Thief, free-form discussion, voting, reveal, and role-based scoring. P1 defines how the deduction works.

## 1. Objective

Everyone receives a piece of the story, but nobody receives the whole story. Innocent players must be able to reason toward the Thief; the Thief needs enough ambiguity to survive by bluffing and redirecting suspicion.

Avoid both extremes:

- **Too easy:** the information identifies the Thief immediately.
- **Too random:** players lack enough evidence to make a meaningful deduction.

The target is a mix of known facts, partial observations, and unknown information. Conversation turns those fragments into corroboration, questions, contradictions, and ultimately a vote.

## 2. Core deduction model

Each round has five information layers:

1. **Scenario:** the public incident and context.
2. **World truth:** the canonical sequence of events known to the game system.
3. **Player roles:** each player's public-after-reveal identity and score value.
4. **Private information:** each player's situation and clue.
5. **Player statements:** what players choose to report, infer, withhold, or fabricate.

Deduction occurs between private information and public statements. The game system stores the truth but does not label statements as true or suspicious during play.

### Example scenario truth

| Field | Canonical value |
|---|---|
| Location | Royal Chamber |
| Theft time | 21:13 |
| Distraction | Lights go out |
| Culprit | Secretly assigned player |
| Escape route | East Corridor |

The public scenario may say only that the Royal Necklace disappeared from the palace. The timeline and culprit remain hidden until reveal.

## 3. Information categories and truth model

Every private clue has a typed internal representation. Suggested categories:

| Type | Meaning | Example |
|---|---|---|
| Location | Where a person or event was | “You were near the treasury when the lights went out.” |
| Time | When something occurred | “The east clock chimed shortly before the blackout.” |
| Witness | What or whom the player observed | “You saw someone carrying a small box.” |
| Action | Something a character did | “The Minister left the banquet before the lights failed.” |
| Object | Information about the Necklace or another object | “The display case latch was open.” |
| Relationship | A connection between two characters | “The King and Queen spoke privately before dinner.” |

Separate the truth of a clue from the accuracy of an interpretation and from the truth of a later statement:

- **Hard fact:** objectively true in the world model. Example: “The Soldier was assigned to the west gate.”
- **Observed fact:** the player genuinely perceived something. Example: “You saw a person carrying a small box.” The observation is true; the identity of the person or contents of the box may be unknown.
- **Partial truth:** a statement that is accurate but incomplete. Example: “I was outside” when the speaker was in the garden, but omits that they later entered the palace.
- **Deceptive statement:** a player knowingly claims something false. Example: the Thief entered the Royal Chamber, then says, “I never went near the chamber.”

The game must not automatically expose these labels during discussion. The UI displays player-entered text as written. The labels are for content validation and post-round explanation.

**Data modeling rule:** deception belongs to a player's statement, not to an authored private clue. Innocent clues should be consistent with the world truth. The Thief may use a true cover clue and choose to lie in conversation.

## 4. Player information and Thief role

The Thief should still participate in the same mystery. Each player receives:

- A court role and role point value.
- A private situation sentence.
- Limited scenario information in the form of one or more clues.
- A secret objective: identify the Thief if innocent, survive the vote if Thief.

Role and guilt are separate concepts. A King, Queen, Minister, Soldier, or Servant may be innocent or may be the Thief. In data, store the role identity independently from the `isThief` objective flag.

The Thief's packet must provide enough true context to answer questions and construct a believable alternative account, without exposing the complete timeline. A scenario may tell the Thief the action they performed, such as entering the Royal Chamber during the blackout. They can hide, blend in, redirect suspicion, or counter-accuse using genuine inconsistencies in other players' stories.

Innocent players collect, compare, question, validate, and eliminate. They are trying to answer, “Whose story cannot be true given what I know?” rather than simply “Who seems suspicious?”

## 5. Statements, partial truths, and contradiction

Players may report facts, offer interpretations, or make claims. The system does not auto-judge statements in MVP. A player's claim can be true, false, or partially true; the game stores canonical events and private clues so a later reveal can explain the evidence.

### Contradiction levels

1. **Direct contradiction:** two claims cannot both be true. Example: “I was with the Queen” / “The Queen was with me” may be consistent; “I was alone with the Queen” / “I was alone with the Queen” from a different player creates a conflict about who was present.
2. **Timeline contradiction:** claims place a person in incompatible events or times. Example: one player says they reached the treasury after the lights returned, while another claims to have seen them leave it during the blackout.
3. **Location contradiction:** one claim places a player somewhere their timeline or another observation makes implausible. Example: “I was in the garden” versus a witness seeing someone matching their description inside the palace.

A conflict is a reason to question, not an automatic guilt verdict. An observation can be incomplete or mistaken; a contradiction can have an innocent explanation. Do not implement automatic accusation, lie detection, or guilt assignment from conflicts in MVP.

### Contradiction graph (internal model)

Represent authored events and clue references as a graph or equivalent relational data: events connect to actors, locations, times, objects, and observations. A consistency checker can identify whether two authored facts can coexist. It should not score player guilt or expose a suspicion meter.

## 6. Clue quality and information overlap

Clues should vary in strength:

- **Weak:** “You heard something nearby.”
- **Medium:** “You heard footsteps from the east corridor.”
- **Strong:** “You saw the Minister enter the east corridor during the blackout.”

Strong clues should still stop short of directly identifying the Thief. Avoid “You saw the Thief steal the Necklace.” Prefer observations that establish a person, place, or object while leaving interpretation open.

For each critical event, target at least two independent information fragments that help discover or corroborate it. Overlap should create a chain of reasoning, not repeat the same sentence:

- King: “The Minister left the banquet early.”
- Queen: “I saw the Minister heading toward the treasury.”
- Soldier: “The treasury corridor was accessible.”
- Servant: “I heard the treasury door open.”

Together, these details form a timeline. None alone proves guilt.

### Round information targets

Initial authoring targets for a six-player Normal scenario:

- Six private situations.
- Four to six supporting observations.
- Two or three discoverable timeline facts.
- One or two ambiguous or misleading observations.
- One critical event: the theft.

These are playtest starting points, not fixed content limits. The key rule is that every player has something useful to contribute and no one clue solves the round outright.

## 7. False leads and ambiguity

Each scenario should include at least one believable false lead: suspicious evidence that has an innocent explanation when the full timeline is revealed. The Thief can reinforce or redirect attention toward that lead.

The Thief primarily exploits unknown information. If every event is already accounted for, there is no room to bluff; if almost nothing is known, the vote is random. Balance facts, unknowns, and ambiguity so questions can turn some unknowns into shared knowledge without eliminating all uncertainty.

## 8. Discussion and player memory

Discussion is free-form with no speaking turns. Useful prompts include **Where? When? Why? With whom? What did you see? Who can confirm it?** Players may ask, answer, explain, challenge, accuse, defend, withhold a clue, or bluff. Role claims are permitted but not mechanically verified until reveal.

MVP does not automatically track every claim as a game mechanic. A future private Investigation Notes tool may let players record claims, for example:

- **Minister:** says library; time unclear.
- **Soldier:** says west gate; claims to have seen someone.
- **Servant:** says garden.

Notes should remain player-controlled. Do not add an AI suspicion score or public suspicion meter in MVP.

## 9. Voting and resolution

Each player gets one vote. Role value does not affect voting power. Self-voting is disabled. Ballots remain secret until the result reveal. Confidence may be collected privately for future balancing analytics, but it must not change MVP scoring.

### Plurality rule

The supplied GDD describes majority detection. For P1 MVP, resolve by **plurality** (the highest vote count), because it gives a result when the field is split and matches the intended one-suspect outcome:

- A unique highest vote total selects that player. If that player is the Thief, the Thief is caught; otherwise the Thief escapes.
- A tie for highest votes triggers a revote among only the tied candidates.
- If the revote is still tied, or there is no valid runoff vote, the Thief escapes.
- Abstentions count as no vote. There is no minimum turnout threshold.
- In a runoff, voters choose among the tied candidates except themselves. If a voter is a tied candidate and therefore has no valid target, they abstain.

This explicit plurality decision supersedes the earlier majority wording for the P1 prototype. Do not require more than 50% of all players to catch the Thief.

### Discussion timer

Use configurable Match Rules values. Initial defaults:

| Phase | Default |
|---|---:|
| Private read | 20–30 sec |
| Discussion | 180 sec |
| Initial vote | 30 sec |
| Runoff discussion | 30 sec |
| Runoff vote | 20 sec |

P1 uses the timer to begin voting. A call-to-vote button and democratic early-end threshold are future options, not MVP requirements.

## 10. Outcomes and scoring

Keep team outcome separate from individual score:

- **Innocent team outcome:** final deciding ballot selects the Thief.
- **Thief team outcome:** final deciding ballot selects someone else, or voting resolves as a tie.
- **Individual score on a catch:** every player who voted for the Thief in the final deciding ballot earns their own role's point value. Other players, including the Thief, earn zero for the round.
- **Individual score on an escape:** the Thief earns the sum of all innocent players' role values. Innocents earn zero for the round.

Role points influence personal rewards, never vote weight. Only the final deciding ballot scores if a runoff occurs; initial votes are discarded for scoring.

For a five-player example with values King 8, Queen 7, Minister 6, Soldier 5, Servant 4, if the Soldier is caught, each correct voter earns their own value. If the Soldier escapes, the Soldier earns 8 + 7 + 6 + 4 = 25.

Run a five-round match with cumulative scores as the initial format. Highest total wins; a match-level tie is a shared win. A target-score mode can be considered later.

## 11. Difficulty and balancing model

Support three internal scenario difficulty profiles. Higher difficulty should not mean arbitrary or random clues.

| Profile | Evidence density | False leads | Unknowns |
|---|---|---|---|
| Easy | High | Low | Low |
| Normal | Medium | Medium | Medium |
| Hard | Lower | High | High |

An internal balancing model may estimate:

`Difficulty = Evidence Ambiguity + Contradiction Complexity + Plausible Suspects - Independent Confirmation`

This value is for designers and scenario validation, never shown as a player suspicion meter. Future playtest analytics may track average accusations before vote, vote concentration, Thief detection rate, time to first credible suspect, and number of contradictory claims. These are balancing metrics, not MVP gameplay mechanics.

## 12. Gameplay state machine

```text
SCENARIO
   ↓
PRIVATE INFORMATION
   ↓
DISCUSSION (ask / answer / explain / challenge / accuse / defend)
   ↓
VOTING
   ↓
VOTE VALIDATION
   ↓
TIE? ── yes → RUNOFF → RESOLVE
   │ no                    │
   └──────────────┬────────┘
                  ↓
                REVEAL
                  ↓
                SCORING
                  ↓
             ROUND COMPLETE
```

Only the round/state owner advances the authoritative phase. UI and chat systems submit player actions; they do not independently jump the game between phases.

## 13. Data model foundation

The following C# sketch separates authored facts, player secrets, and public statements. It is a design model, not yet production code.

```csharp
public enum InformationType
{
    Location,
    Time,
    Witness,
    Action,
    Object,
    Relationship
}

public enum FactType
{
    HardFact,
    Observation,
    PartialTruth
}

[Serializable]
public class ClueData
{
    public string clueId;
    public InformationType informationType;
    public FactType factType;
    public string subjectId;
    public string targetId;
    public string locationId;
    public string eventId;
    public string text;
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

[Serializable]
public class ScenarioEvent
{
    public string eventId;
    public int sequence;
    public int timeMinutes;
    public string locationId;
    public string actorId;
    public string objectId;
    public string eventDescription;
}

[Serializable]
public class PlayerStatement
{
    public string statementId;
    public string speakerId;
    public string text;
    public List<string> referencedEventIds;
    // Truth assessment is internal/post-round only; never shown as a live verdict.
    public string internalAssessment;
}
```

Deception is represented by a player's statement relative to canonical scenario events, not by assigning false authored clues to innocents. A production implementation should use enums and validation rules rather than free-form assessment strings.

## 14. Scenario quality rules

Every authored or generated round must pass these checks:

- **Truth:** exactly one intended Thief exists.
- **Consistency:** all innocent information can coexist with the canonical timeline.
- **Coverage:** critical events can be reconstructed from at least two independent fragments.
- **Deduction:** the information can establish a reasonable suspect without making the answer certain immediately.
- **Deception:** the Thief can give a plausible alternative account.
- **Ambiguity:** at least one innocent can look suspicious for a reason the reveal explains.
- **Replayability:** changing role and clue assignments changes the discussion experience.
- **Reveal:** every clue and apparent contradiction can be explained from world truth.

## 15. P1 definition of done

P1 is ready for implementation when the design can answer:

- What information does each player receive?
- How is truth represented separately from a claim?
- How can information corroborate or contradict another statement?
- How can innocents narrow the suspect set?
- How can the Thief bluff without relying on silence?
- How does ambiguity create plausible alternative stories?
- How do plurality, runoff ties, and abstentions resolve?
- How do role values affect score without affecting vote weight?
- How can scenario content be validated deterministically?

The golden rule is: **reward players for constructing the story, not for guessing the most suspicious-looking person.** A strong deduction combines one's own situation, other players' observations, corroboration, and timeline conflicts before selecting a suspect.

## 16. P2 handoff

P2 — Scenario, Role & Clue Generation System should define:

- Scenario templates and canonical timeline authoring.
- Role definitions and role-value data.
- Private situation and clue generation rules.
- Truthful, partial, and deceptive statement handling.
- Clue distribution and information-overlap algorithms.
- Consistency and difficulty validation.
- A complete example scenario pack playable with 4–6 players.

The intended pipeline is:

```text
SCENARIO TEMPLATE
       ↓
WORLD EVENTS
       ↓
THIEF EVENT
       ↓
ROLE ASSIGNMENT
       ↓
CLUE GENERATION
       ↓
INFORMATION DISTRIBUTION
       ↓
CONSISTENCY + DIFFICULTY VALIDATION
       ↓
PLAYABLE ROUND
```
