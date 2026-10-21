# GW0 — Gameplay Workflow Foundation

**Track:** Gameplay Workflow Production  
**Status:** Foundation contract  
**Scope:** Architecture and production rules; feature expansion starts in GW1  
**Runtime baseline:** PB1 deterministic match core

GW0 defines how gameplay decisions enter the runtime, are validated, change authoritative state, and become events for presentation. It is the contract for GW1–GW15. The UI, audio, animation, and VFX consume gameplay outcomes; they do not decide them.

## 1. Purpose and governing rule

The game is a multiplayer social-deduction card game built around partial information, discussion, claims, voting, reveal, scoring, and match progression. Each player has a court role, a private situation and knowledge set, and may also hold the hidden Thief objective. No player-facing projection may be treated as the complete authoritative truth.

> Gameplay determines what happened. Presentation communicates what happened.

Every action follows this path:

```text
Player input → command → authority check → phase/rule validation
             → state transition → event/result → presentation
```

Input handlers must not mutate match truth directly. Core gameplay must not invoke visual behavior directly.

## 2. Scope and relationship to existing production

The repository already contains a PB1 engine-independent match controller, seeded round/scenario construction, role and private-information assignment, discussion claims/timer, vote validation and runoff, reveal, scoring, snapshots/replay metadata, public/private view models, and invariant checks. GW0 documents their shared contract; it does not replace the PB1 compatibility paths or claim that a network server/transport is already implemented.

Visual production is a downstream consumer of gameplay state/events. Online authority, durable recovery, and transport enforcement remain implementation gates, not capabilities implied by this document.

## 3. Authority and ownership

There is one authoritative match state per match. In the local PB1 build, `MatchController` is the command boundary. In an online build, a trusted server must own the same decisions; clients submit requests and receive authorized public state plus recipient-specific private state.

| Responsibility | Owner | Boundary |
| --- | --- | --- |
| Match lifecycle and command orchestration | `MatchController` / future `MatchManager` | Starts, advances, and ends a match; routes commands to rule services |
| Round lifecycle | Round manager/factory | Prepares and completes a round; does not own UI |
| Role and private-data assignment | Role assignment service | Uses authoritative seed; validates exactly one hidden Thief objective |
| Scenario generation and truth | Scenario pipeline/service | Builds and validates immutable round truth before use |
| Discussion and claims | Discussion/claim service | Enforces phase and claim rules; ordinary chat is not proof |
| Ballot and resolution | Vote service / vote resolver | Validates ballots, calculates result and runoff |
| Reveal | Reveal workflow | Exposes already-assigned truth at the allowed phase |
| Score calculation/commit | Score service | Calculates once from resolved outcome; commits once |
| Presentation | UI, audio, VFX, visual bus | Renders projections and events; cannot mutate authoritative gameplay |

One authority owns each state transition. Services may calculate or validate a result, but must not independently advance the phase.

## 4. State model and information boundaries

The authoritative aggregate consists of match state, current round state, players, scenario truth, vote/resolution state, scores, and progression metadata. The runtime may use different concrete types as long as these responsibilities remain explicit.

| Information class | Examples | Recipient rule |
| --- | --- | --- |
| Public | Scenario brief, phase/deadline, visible player status, public claims/chat, ballot/result states when the rules expose them, scores after resolution | All match participants, subject to phase visibility rules |
| Private | Recipient’s own role/objective, situation, clues, knowledge, and private assignment data | That player only until a defined reveal |
| Authority-only | Actual Thief identity before reveal, complete truth/evidence graph, other players’ private data, assignment seed/resolution inputs | Trusted gameplay authority only; never in ordinary public snapshots or client logs |

Truth and player knowledge are separate models. The client receives a projection, not the whole aggregate. Reconnection rebuilds presentation from a fresh authoritative projection rather than trusting cached client state. Reveal changes visibility; it does not create or change role assignment.

## 5. Determinism and assignment

A match seed is authoritative input. Derive a round seed from the match seed, round index, and scenario identity/version using a stable, versioned derivation. The same derivation inputs, content version, and algorithm version must reproduce the same scenario and assignments. A seed alone is not permission to expose hidden assignment data.

Before committing a round, validate player-count policy, one hidden Thief objective, valid court-role assignment, complete recipient-specific private data, scenario references/evidence requirements, and reproducibility metadata. Assignment is committed atomically; a failed preparation must not expose a partial round.

## 6. Phase contract

The logical lifecycle is:

```text
Lobby → Prepare/assign → Scenario brief → Private information
      → Discussion → Voting → Resolution
          ├─ first tie → Revote → Resolution
          └─ unique result or final tie → Reveal → Scoring
             → Round results → Next round or Match results
```

A phase transition is accepted only by its designated authority and only when its preconditions hold. The local PB1 enum names may differ; phase meaning and guards are the compatibility contract.

| Phase/workflow | Authority | Exit condition |
| --- | --- | --- |
| Lobby / start | Match lifecycle | Match is valid and start is authorized |
| Preparation / assignment | Round + role/scenario services | Validated round is committed |
| Private information | Private-state dispatcher | Each recipient receives only its own payload |
| Discussion | Discussion controller | Authority deadline or explicit configured close condition |
| Voting | Vote service | All eligible active voters submit, or configured timeout policy applies |
| Resolution / revote | Resolution service | Unique eligible target, or final-tie escape rule |
| Reveal | Reveal controller | Authoritative outcome is exposed according to reveal policy |
| Scoring | Scoring service | Result validated and score transaction committed once |
| Round results / match results | Match/round controller | More rounds remain, or final round is complete |

Timers and deadlines are authoritative. UI countdowns are display projections. The existing local discussion timer is a prototype implementation, not a multiplayer clock guarantee.

## 7. Command and result contract

Gameplay actions enter through typed commands or equivalent validated controller methods. Each request identifies the authenticated actor; a client-provided actor ID is not proof of identity. A command result is accepted, rejected with a stable reason, or fails retryably/systemically. Repeated network requests must not apply irreversible effects twice.

For a vote request, validate in this order: authenticated voter exists and is eligible; phase is voting; target exists and is eligible; voter is not target; voter has not already submitted in this vote round; ballot is still open. Only then record the vote and emit the accepted event. The server-side check is required even if UI controls already disable invalid choices.

Other command families (match start, discussion/chat/claim, vote, resolution, reveal, score, round completion, next round) follow the same authority → phase → rule → mutation → event pattern. Chat content is not automatically interpreted as evidence or guilt.

## 8. Voting and tie policy

A voter may submit at most one ballot per vote round and may not vote for themselves. Eligible target and voter sets are computed by the authoritative rules. The default completion condition is all eligible active players have voted; timeout behavior is configuration, not UI logic.

Votes are counted over eligible targets. A unique leader proceeds to reveal. A first tie resets only vote-round state and begins a runoff among the tied candidates. It preserves round identity, role assignment, scenario truth, player knowledge, discussion history, and accumulated score. A second tie resolves as Thief escape. If the unique selected player is not the Thief, the Thief escapes under the current PB1 rules.

## 9. Reveal, scoring, and progression

Reveal reads the committed role/objective assignment. It must not run before vote resolution. Scoring must not run before reveal is complete and must not run for an unresolved/revote outcome.

Keep round score and match score separate. Current PB1 scoring awards each non-Thief player their court-role value when they correctly vote for the Thief. On escape, the Thief receives the sum of normal-role values. A caught Thief receives no catch reward in the current implementation. Score calculation and score commitment are guarded against duplicate application.

After scores commit, persist the round result, mark the round complete, and either increment the round index and prepare the next round or complete the match and rank by match score. Tie handling for match ranking and the escape payout are configurable gameplay policies and must be fixed before competitive release. No gameplay mutation is permitted after round completion except the authorized next-round/match lifecycle transition.

## 10. Events and presentation

Events describe committed outcomes and are emitted in deterministic order. At minimum, consumers need match/round lifecycle, assignment/private-delivery, phase, discussion/claim, vote accepted/rejected, vote resolved/tied/runoff, reveal, score, round-complete, and match-complete signals. Rejected commands return a reason; they must not masquerade as state-changing success events.

A presentation bus may translate an event into screen updates, sound, animation, camera work, or VFX. It cannot determine a vote, reveal a role early, award points, or move the phase. Event payloads must follow the same public/private/authority boundary as snapshots.

## 11. Failure, disconnect, and recovery contract

Command outcomes distinguish success, rule rejection, retryable failure, and system failure. Rejections do not mutate gameplay state. Retryable failures preserve enough identity/version information for safe retry. A system failure is logged with privacy-safe metadata and leaves the match in a valid recoverable state or explicitly terminates it.

A disconnect changes connection state, not scenario truth or role assignment. Reconnect authenticates the player, locates the active match, fetches a versioned authoritative snapshot and their private projection, then rebuilds local presentation. Player-loss behavior after the configured reconnect window is a dedicated policy and must not be scattered among phase services.

Snapshots carry schema/version, match/round identity, phase and version, permitted player projections, public scenario state, vote state appropriate to the current phase, scores, and resolution metadata allowed to the recipient. Hidden authoritative fields are excluded from client snapshots. Replay/debug records must preserve deterministic ordering without putting secrets in ordinary logs or telemetry.

## 12. Invariants

The runtime and future automated checks enforce:

- Exactly one authoritative match state and one active phase.
- Exactly one hidden Thief objective per valid round; every player has one valid court role.
- Private data is recipient-scoped; unrevealed hidden truth is absent from public projections.
- No command mutates state when authority, phase, or rule validation fails.
- At most one ballot per voter per vote round; no self-vote; no ballot outside voting.
- Revote resets ballot state and is limited to tied candidates; a final tie escapes.
- No reveal before resolution; no score before reveal; no duplicate score commit.
- No round completion before scoring; no next round before round completion; no match completion before the final round.
- Identical versioned inputs produce identical seeded preparation and deterministic event order.

## 13. Verification contract

The foundation suite should cover phase guards, command rejection/no-mutation, role assignment, deterministic seeds, privacy projections, ballot invariants, runoff/final tie, reveal ordering, score calculation/commit idempotence, round/match progression, and snapshot recovery. Integration and simulation suites should exercise full rounds/matches, disconnect/reconnect, and many seeds. Automated tests are a GW14 implementation gate; the pasted 1,000-round target is a future stress target, not evidence that it has already passed.

## 14. Repository baseline and open decisions

PB1 currently supplies a local deterministic 4–6 player controller, seeded scenario/role setup, claims and a 180-second local discussion timer, vote resolution/runoff, reveal/scoring, private/public view types, snapshots/replay metadata, and invariant validation. `Docs/ARCHITECTURE.md` describes the boundary and explicitly states that transport/server enforcement and distributed recovery are not yet implemented.

Resolve these before their dependent production gates:

1. **Player count:** project GDD says 4–8; PB1 constructor currently enforces 4–6. Confirm the supported production range and role/evidence coverage before expanding it.
2. **Thief model:** this repository treats Thief as an objective layered over a normal court role. Preserve that model unless a formal GDD revision changes scoring, assignment, cards, and privacy together.
3. **Scoring policy:** confirm whether catch rewards, escape payout, role values, and match-score tie handling are final. Current PB1 applies the policies in §9.
4. **Timeout/player loss:** confirm voting timeout and disconnect/reconnect expiry behavior before multiplayer implementation. Discussion is currently 180 seconds locally.
5. **Match length:** source design defaults to five rounds; PB1 accepts a configured positive round count. Confirm the production default.

These are tracked decisions, not silent changes to runtime behavior.

## 15. GW0 acceptance

GW0 is complete when authority, state/information boundaries, phase ownership, command/result flow, event/presentation boundary, invariants, recovery boundary, and verification contract are agreed and documented. This document establishes that foundation and records the remaining policy decisions. GW0 does not certify online deployment, resolve the listed design choices, or claim that future automation has passed.

## 16. Production sequence

```text
GW0 Foundation (this contract)
 → GW1 Match lifecycle and session workflow
 → GW2 Player/role/information
 → GW3 Scenario/evidence
 → GW4 Discussion
 → GW5 Voting
 → GW6 Resolution/tie
 → GW7 Reveal
 → GW8 Scoring/progression
 → GW9 Round/match completion
 → GW10 Networking/synchronization
 → GW11 Failure/recovery
 → GW12 Gameplay UX
 → GW13 Telemetry/replay/debug
 → GW14 QA/automated validation
 → GW15 Production integration
 → GW-FINAL Release gate
```
