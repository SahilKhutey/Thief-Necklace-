# Thief & the Necklace

## P9 — Production Implementation & First Playable Vertical Slice

**Status:** Ready for implementation  
**Parent:** P0 → P1 → P2 → P3 → P4 → P5 → P6 → P7 → P8  
**Engine:** Unity 6 LTS  
**MVP:** 4–6 players  
**First milestone:** Fully playable four-player vertical slice  
**Primary goal:** Convert the approved architecture into a working Unity project without building the full game prematurely.

The supplied GDD targets Unity, multiplayer rooms, role assignment, vote/scoring systems, UI controllers, chat, and a staged MVP roadmap. P9 turns P0–P8 into an implementation sequence.

## 1. P9 objective and vertical slice

The first milestone is **VS-01 — The Royal Necklace**: one authored scenario, four real network participants, one Thief objective, private role/clue delivery, text discussion, one vote cycle (including the defined tie path), reveal, scoring, and round completion.

```text
CREATE ROOM → JOIN → READY → START → PRIVATE ROLE/CLUE
→ DISCUSS → VOTE → RESOLVE → REVEAL → SCORE → ROUND COMPLETE
```

The goal is to prove the integration of scenario data, authority, privacy, networking, UI, voting, reveal, and score before expanding content. VS-01 does not include profile economy, ranked mode, voice chat, cosmetics shop, or dozens of scenarios.

## 2. Implementation order

1. Project bootstrap and package/version pinning.
2. Core data and configuration assets.
3. Round state machine and authored Royal Necklace scenario.
4. Session creation/join, player registry, and readiness.
5. Authoritative role assignment and targeted private delivery.
6. Discussion timer and room chat.
7. Vote validation, tally, tie/runoff, reveal, and score.
8. UI, placeholder art, animation, and audio integration.
9. Four-client vertical-slice validation.
10. Build packaging, then expand to five/six players and full match.

## 3. Project foundation

Recommended project targets are desktop first, then WebGL, Android, and iOS. Use URP if compatible with the chosen visual scope and target hardware; use Unity Input System, Unity UI/TextMeshPro, NGO, Unity Transport, Unity Multiplayer Services, Relay, Authentication, and Unity Test Framework as appropriate.

Pin package versions supported by the exact Unity editor version. Do not copy unverified package versions from this document. Keep game assets under a project-owned folder rather than scattering files directly under `Assets`.

```text
Assets/ThiefAndTheNecklace/
├── Scenes/
├── Scripts/
├── Data/
├── Prefabs/
├── Art/
├── Audio/
└── Tests/
```

## 4. Bootstrap and service initialization

Create `Bootstrap.unity` with persistent bootstrap/service composition, network manager/transport, session service, scene router, audio service, and configuration registry. Initialize Unity Services, authentication, session/network layer, and game systems before loading Main Menu. Do not generate a scenario during bootstrap.

Session creation and joining remain behind an application boundary such as:

```csharp
public interface IGameSessionService
{
    Task<string> CreateSessionAsync(int maxPlayers);
    Task JoinSessionAsync(string joinCode);
    Task LeaveSessionAsync();
}
```

UI calls the application/session boundary and does not call Relay APIs directly.

## 5. Data and authoring layer

Begin with pure/serializable data and ScriptableObject authoring assets. Create role assets for King, Queen, Minister, Soldier, Servant, and Guard. Thief remains a hidden objective flag, not a separate villain role.

Suggested role authoring fields: `roleId`, display name, point value, description, role tags, and presentation asset references. Current proposed values are King 8, Queen 7, Minister 6, Soldier 5, Servant 4, Guard 5; keep them configurable in ScoreConfig.

Create `Scenario_RoyalNecklace` with scenario ID, title/description, locations, target object, event/timeline data, clue/situation templates, and validation metadata. Runtime scenario state is a snapshot separate from the authoring asset.

```csharp
public sealed class RuntimeScenario
{
    public string scenarioId;
    public int seed;
    public WorldState world;
    public List<ScenarioEvent> events;
    public List<PlayerSecret> playerSecrets;
}
```

Private player state is separated from public roster state and must only be sent to its owning client before reveal.

## 6. VS-01 authored scenario

Public scenario text:

> The King's Necklace has disappeared during a palace blackout. Someone among you is responsible. Find the Thief.

Initial locations: Banquet Hall, Royal Chamber, Treasury, East Corridor, West Gate, Garden. Use an authored deterministic timeline first:

| Time | Event |
|---|---|
| 21:00 | Banquet begins. |
| 21:07 | Queen leaves. |
| 21:09 | Minister heads toward the Treasury. |
| 21:11 | Servant passes through the East Corridor. |
| 21:12 | Blackout begins. |
| 21:13 | Thief enters the Royal Chamber. |
| 21:14 | Necklace is stolen; Thief exits. |
| 21:15 | Lights return. |
| 21:16 | Necklace is discovered missing. |

The exact authored situation/clue text must be consistency-reviewed against the final timeline and observation rules. The four-player role set for VS-01 is King, Minister, Soldier, and Servant; exactly one is independently assigned the Thief objective. A server-generated seed reproduces the role assignment and any variable selection.

Initially hand-author this content to validate lifecycle/network/UI. Later, implement the P2 generator behind the same runtime scenario interface so RoundManager need not know whether a round came from authored data or procedural generation.

## 7. Player, match, and round state

Public runtime player state includes stable player ID, display name, connected/ready status, and public score. Private runtime secret includes role ID, Thief flag/objective, private situation, and owned clues.

Suggested phase enum:

```csharp
public enum RoundPhase
{
    None,
    Intro,
    PrivateReveal,
    Discussion,
    Voting,
    VoteResolution,
    Reveal,
    Scoring,
    RoundEnd
}
```

`RoundManager` owns legal phase transitions, deadlines, and round completion. Other systems submit actions or listen to events; they do not freely mutate phase.

```csharp
public bool TryEnterPhase(RoundPhase expected, RoundPhase next)
{
    if (_currentPhase != expected || !IsLegalTransition(expected, next))
        return false;

    _currentPhase = next;
    PhaseChanged?.Invoke(next);
    return true;
}
```

Use authoritative network time for phase deadlines. Clients render countdown locally from phase end time; do not send a per-second timer packet.

## 8. Session and lobby slice

First working flow: Main Menu → Create Room → MPS Session/Relay → Host → Lobby; and Join Room → Session/Relay connection → Lobby. Lobby shows room code, roster, player count, host, readiness, and chat. Authority accepts ready requests only from registered session members and before match start.

Match start validation: `4 <= connectedPlayerCount <= 6`, required connected players ready, and host/start permission valid. Lock the roster, initialize match state, then start a round. Late join after match start is rejected for MVP.

## 9. Round creation, privacy, and commit

Authority flow:

```text
LOCK ROSTER → CREATE SEED → LOAD AUTHORED ROUND
→ ASSIGN UNIQUE ROLES → SELECT ONE THIEF
→ ASSIGN SITUATIONS/CLUES → VALIDATE → COMMIT
```

Only after commit does the authority send public scenario data to all clients and a distinct `PrivateRoundState` to each recipient. Clients store only their own role/situation/clues; no client needs all roles, all clues, full truth, or Thief ID. Host authority can inspect the complete state; this P4 trust limitation remains.

Private Role UI uses P5/P6 role/card presentation. The player can reopen **My Role / My Situation / My Clues** during discussion. Another client must not receive these payloads, even if the corresponding UI element is hidden.

## 10. Discussion and chat

Once each player confirms the private card, authority enters Discussion and publishes the phase start/end timestamps. Enable room chat, roster, timer, and private-info review. Chat remains behind `IChatService`; validate non-empty text, length, sender/session membership, discussion phase, and rate limit before broadcast.

**Call Vote is not in VS-01 MVP.** P1/P4/P5 specify timer-led discussion and no early-vote threshold. Discussion ends at the authoritative deadline, then voting begins. Add early voting only after a rule and authority-side threshold are specified.

## 11. Voting and resolution

Authority opens voting at deadline. Client sends a request ID and target player ID only. Authority rejects wrong phase, unknown/self target, duplicate/late request, or invalid sender. Ballots remain secret before resolution; clients receive only their own acceptance and public submitted count.

Close ballot when all eligible players have voted or the deadline expires. Apply P1 plurality: unique highest vote selects that player; top tie opens runoff only among tied candidates; second tie/no valid runoff result means Thief escapes. Authority resolves once and sends the result. No client sends a count or chooses the outcome.

## 12. Reveal and scoring

Only after vote closure does the authority send reveal data: accused player, Thief ID, revealed role mapping, outcome, and any approved timeline/clue explanation. Reveal UI presents accused role, innocent/Thief status, actual Thief if needed, and Necklace/outcome.

Use P7 scoring:

- Thief caught: each correct voter in the final deciding ballot receives their role value; other voters and Thief receive 0.
- Thief escapes: Thief receives sum of all innocent role values; others receive 0.
- No negative points and no separate Thief role-value bonus.

Score resolver should be deterministic and side-effect free where practical. Authority commits each round result once using `matchId`, `roundId`, `playerId`, `reason`, `delta`, and new total. Duplicate results do not duplicate points.

## 13. Round reset and system boundaries

After results, discard votes, private round secrets, timeline, phase deadline, and accused/result transient state. Preserve roster and match scores for the next round.

Suggested responsibilities:

- `GameBootstrap`: service initialization and persistent composition.
- `SessionService`: create/join/leave sessions.
- `PlayerRegistry`: public roster/readiness/connection state.
- `MatchManager`: match settings, round number, cumulative score.
- `RoundManager`: authoritative round phase/deadlines/transitions.
- `ScenarioManager`/`IRoundSource`: authored or generated round data and validation.
- `PrivateStateDispatcher`: recipient-targeted secrets.
- `VoteManager`: ballot validation and resolution.
- `ScoreManager`: P7 result calculation/commit.
- UI, chat, audio, animation: consume events and issue validated commands, never own game rules.

Avoid a monolithic GameManager. Keep each runtime system single-purpose, explicit, and testable.

## 14. Suggested folder and scene structure

```text
Assets/ThiefAndTheNecklace/
├── Scenes/ Bootstrap, MainMenu, Lobby, Game
├── Scripts/Runtime/
│   ├── Core/ Match/ Round/ Scenario/ Players/
│   ├── Voting/ Scoring/ Networking/ Chat/
├── Scripts/UI/
├── Data/ Roles/ Scenarios/ Config/
├── Prefabs/ Network/ UI/
├── Art/ Audio/ Tests/
```

Use a small number of scenes: Bootstrap, Main Menu, Lobby, Game. Scenario intro, private role, voting, reveal, and results are overlays/panels within the Game scene unless performance or platform needs dictate otherwise.

First reusable prefabs: NetworkPlayer (if needed), PlayerSlot, RoleCard, ClueCard, VoteTarget, ChatMessage, ScoreRow, TimerWidget, RevealCard. Do not network UI objects or decorative art.

## 15. Development and debug configuration

Development-only HUD may show phase, player count, seed, host, vote count, and (only in a protected local developer build) Thief ID. Never expose secrets in QA/release client builds. Add a DebugRoundSimulator later for player count, seed, forced role, timer skip, forced votes, reveal, and score; gate it from non-developer builds.

Use separate Development, QA, and Release build configurations. QA can retain diagnostics but not gameplay cheats; Release contains no debug secret display, force reveal, test seed override, or score manipulation path. Release logging should avoid private payloads.

## 16. First tests and four-client acceptance

Initial automated suites: role assignment, scenario validation, vote validation, vote resolution, score resolution, round state, and private state. Core cases:

- Four active players, one Thief, every player gets one unique role.
- Same seed reproduces assignment.
- Self-vote and duplicate vote rejected.
- Discussion transitions to voting only through legal phase flow.
- Catch/escape score results match P7.
- Duplicate score commit ignored.
- Each client can read only its own secret before reveal.

VS-01 acceptance sequence:

1. Launch four clients.
2. Client A creates a room; B/C/D join.
3. All four ready; host starts.
4. All see the same public scenario; each sees only their own role/clues.
5. Discussion timer and room chat work.
6. Discussion deadline starts voting.
7. Self-vote is impossible; accepted ballots are anonymous until resolution.
8. Tie/no-tie resolution follows P1.
9. Authority reveals the same result to all clients.
10. Score updates once and the round completes.

Run first with a host plus three clients, not six. After four-player stability, expand to five and six; then validate a full five-round match and state reset.

## 17. Production milestones

- **M1 — Offline round:** four virtual players, full rules flow.
- **M2 — Two clients:** host + client, session and state sync.
- **M3 — Four clients:** complete networked round, privacy verified.
- **M4 — Six clients:** full MVP capacity, UI and content distribution.
- **M5 — Five-round match:** scores persist/reset correctly.
- **M6 — Scenario generator:** replace authored source behind `IRoundSource`.
- **M7 — Art polish and platform validation.**

Do not start with six-player complexity or procedural generation. First prove the four-player round. Replace placeholder art with final art only after layout and core flow work.

## 18. P9 implementation Definition of Done

P9 VS-01 is complete when a clean development machine can open the Unity project, resolve pinned packages, initialize bootstrap/auth/session, create and join a room, ready four clients, start an authoritative authored round, deliver public and recipient-only private state, run synchronized discussion/chat/voting, prevent self/duplicate/late votes, resolve ties, reveal authoritatively, commit score once, reset round state, and rematch/continue. Core automated tests and multiplayer smoke tests pass, and debug tools are separated from release functionality.

The broader P9-to-MVP roadmap then adds five/six-player support, five-round matches, procedural scenarios, robust recovery, mobile/WebGL builds, and final art/performance validation. These are not prerequisites to prove the first four-player vertical slice.

## 19. Production lock

Build one complete, secure, playable round before scaling content. The implementation sequence is:

```text
FOUR-PLAYER ROUND → FULL NETWORK LOOP → PRIVATE DATA → VOTE/REVEAL
→ SCORE → REMATCH → VALIDATE → SCALE TO 4–6 PLAYERS
```

The P0–P8 design stack is the foundation; VS-01 is the first proof that the integrated game works.
