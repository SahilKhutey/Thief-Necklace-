# GW2 — Player, Role & Information Workflow

**Track:** Gameplay Workflow Production  
**Status:** Local authoritative information workflow implemented; provider integration pending  
**Depends on:** [GW0 Gameplay Workflow Foundation](GW0-Gameplay-Workflow-Foundation.md), [GW1 Match Lifecycle](GW1-Match-Lifecycle.md)  
**Runtime baseline:** PB1 match core and GW1 `SessionLifecycleRuntime`

GW2 owns round-level role assignment, private situations/objectives/clues/knowledge, information validation, recipient-scoped projections, and the reveal visibility boundary. Player identity and cumulative match score persist across rounds; role and private information are regenerated per round.

## Authority and information model

```text
Scenario truth (authority only)
   ├─ public projection ───────────────> connected participants
   ├─ private package for player P ───> authenticated participant P
   └─ revealed roles/outcome ─────────> participants after reveal commit
```

The PB1 design represents the Thief as an objective layered over a normal court role. `RoleType.Thief` and the role catalog's Thief entry are markers for the hidden objective/private-information template; the role assignment service never deals Thief as a court role. Exactly one player receives `IsThief` in each valid round. `PlayerId` remains stable inside a match while role, situation, objective, clues, and round knowledge reset at preparation.

## Runtime implementation

- `RoleAssignmentService` derives assignable court roles from the role catalog, shuffles them with the round seed, and independently selects one Thief player. The Thief flag and court role remain separate.
- `MatchController.PrepareRoundCore` generates each player's scenario package, sets the objective, assigns evidence and fact knowledge, then runs `InformationIntegrityValidator` before committing the scenario and entering private reveal.
- `InformationIntegrityValidator` rejects duplicate/missing player packages, invalid ownership/role mapping, zero or multiple Thieves, duplicate court roles, missing private situation/objective/evidence, unknown fact/evidence references, and overlapping known/unknown/false fact sets.
- `InformationProjectionService` builds role-free public player views, recipient-owned private player/evidence views, and revealed role views. The private view includes its owner ID and clue descriptions. Public player views include seat, connection, readiness, and score, and never contain role fields.
- `SessionLifecycleRuntime.GetPrivatePlayerInformation` maps the authenticated stable room ID to its match-local player ID and returns only that player's private package. `GetRevealedPlayerInformation` returns all roles only after the core has committed reveal.
- `RoomConfiguration` requires each connected player to acknowledge their private information by default. `BeginDiscussion` is host-authorized and rejected until all connected participants acknowledge; match snapshots publish the acknowledgement roster. Disconnect clears that player's acknowledgement, and the next round resets acknowledgements.
- `PrivateKnowledgeState` distinguishes known, unknown, and known-false facts, beliefs, evidence, observed events, known locations, and known players. `PlayerKnowledgeService` validates authorized updates and emits typed knowledge events. A public claim can be recorded as a belief source but cannot be promoted directly into authoritative fact knowledge.
- Role point values now come from the shared data role catalog. The Thief entry has value zero and is a marker, not an assignable court card.
- Existing claim handling remains separate from truth and player knowledge. A claim records a player's statement; it does not change scenario facts.

## Visibility contract

| Projection | Available information |
| --- | --- |
| Public player view | Player ID, display name, seat, connection/readiness, match score |
| Private player view | Its owner's public projection, owner ID, own court role, own Thief objective status, private situation, objective, clue text/IDs, personal known/unknown/false fact IDs, observed events, locations, players, and beliefs |
| Revealed player view | All court roles and Thief markers after authoritative reveal completion |
| Authority state | Complete scenario truth, assignment and every player's private knowledge; remains local to the trusted runtime |

The session runtime and transport interface require authenticated player identities from their caller. The runtime does not authenticate a network connection, encrypt traffic, or provide a concrete provider adapter. Do not expose `GetAuthorityGameplayRuntime` to an untrusted client or serialize `GameRuntimeState` as a public payload.

## Round information lifecycle

```text
Round preparation
  → seeded court-role shuffle + exactly one hidden Thief selection
  → situation/objective/evidence/knowledge assignment
  → ownership/reference/integrity validation
  → scenario commit
  → private reveal and player acknowledgements
  → discussion after all connected acknowledgements
  → authoritative reveal commit
  → public revealed-role projection
  → next round clears reveal visibility and assigns fresh private information
```

The validation and projection services return/reject impossible state instead of repairing it silently. Assignment is seeded by the round context, and an invalid package aborts round preparation through the existing match initialization failure path.

## Remaining production gates

- Add a concrete authenticated transport adapter and recipient-only routing for `SessionPrivatePlayerSnapshot`.
- Add serialization-level leak checks and automate the GW2 validation, ownership, determinism, role immutability, reveal-boundary, and round-reset scenarios in GW14.
- Persist assignment/version metadata with authoritative match state before supporting reconnect after authority process loss.
- Scenario content still supplies private situations, objectives, clue references, and initial fact sets; GW3 must formalize truth/evidence support and authoring validation.
- `GameRuntimeState` remains an authority-side mutable aggregate exposed by the trusted core API. Keep it server/local-authority only until a read-only authority facade replaces direct state access.

No automated tests have been run for GW2. This implementation is a local authoritative prototype and does not certify online privacy or transport security.
