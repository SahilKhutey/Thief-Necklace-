# Architecture Overview

## PB1.2 deduction layer

`Scripts/Core/PB1` models scenario truth separately from evidence and player knowledge. `ScenarioTruthModel` owns authored events, facts, opportunities, evidence, and role-specific information bundles. `ScenarioValidator` checks event ordering and references, fact/evidence identity, player bundle coverage, access windows, and two independent evidence paths for each critical event. `GameController` generates and validates a round model before exposing private reveal. `RoundTruth` stores an isolated deep-copy snapshot; callers that request its data receive a separate copy. Discussion claims are recorded/retracted independently and never write into scenario truth.

## PB1.3 authoritative runtime

`MatchController` is the canonical PB1.3 command surface over `GameRuntimeState`. It owns phase transitions, round preparation, runoff constraints, score commitment, and exact match completion. `RuntimeInvariantValidator` checks match/round/player/vote/score consistency after commands. `ScenarioState` freezes a validated deep copy; `RoundContext` derives reproducible seeds; difficulty only changes the assigned evidence mix. `SnapshotService` and `ReplayRecorder` retain non-secret debugging metadata. The earlier `GameController` remains the PB1.1 compatibility path until PB1.4 presentation integration.

```text
UI / input
   ↓
Core rules + seeded scenario engine
   ↓
Round and vote state
   ↓
Reveal and local progression

Online lifecycle prototype (provider-neutral, in-memory)
Client → authenticated transport adapter (not connected) → SessionLifecycleRuntime
                                                        ├→ PB1 CoreGameRuntime
                                                        ├→ public room/match snapshots
                                                        └→ in-memory result history

Future online boundary (provider and durable services)
Client → authenticated transport → authoritative server
                                  ├→ private recipient state
                                  ├→ vote/result/score
                                  └→ profile/reward persistence

Operations foundations
Scenario templates → validation → versioned content package → match content pin
Typed events → privacy-safe telemetry/logging contracts → provider sinks (not connected)
```

## Unity assemblies

- `ThiefAndTheNecklace.Core`: engine-independent rules, scenario data, generation, validation, events, and local progression models.
- `ThiefAndTheNecklace.PB1.Unity`: Unity asset definitions and timer/reveal/bootstrap adapters for the PB1 match core.
- `ThiefAndTheNecklace.PB2.Networking`: engine-independent authority/network contracts; not a transport implementation.
- `ThiefAndTheNecklace.Backend`: environment/version/config validation, profile and matchmaking seams, result validation, privacy and security helpers.
- `ThiefAndTheNecklace.Live`: versioned content validation, staging/activation/rollback model, scheduled-content checks, and match content pins.
- `ThiefAndTheNecklace.Logging`: fixed-schema privacy-safe logger contract; no sink is connected.
- UI scripts live in `Scripts/UI` and currently support the offline prototype.

## Trust boundaries

An eventual production client must be presentation/input only. The server must own role assignment, private clue routing, phase deadlines, ballot validation, result/score, and reward application. PB2/PB8 interfaces express some of those boundaries but do not enforce them until a trusted server and transport adapter exist.

Never put complete world truth, private role/clue data, auth credentials, or raw chat in ordinary telemetry/logs. Historical match reconstruction requires immutable scenario/content versions, package hash, seed, config version, and authoritative result records under access control.

## State and failure policy

`Scripts/Networking/SessionLifecycleRuntime.cs` implements an in-memory room/session lifecycle around the PB1 match core, including lobby readiness, authoritative start validation, reconnect snapshots, configurable active-player-loss handling, rematch, return-to-lobby, and in-memory result history. `IRoomTransport` remains a contract only: no provider adapter or remote identity enforcement is connected. PB1 and this session runtime have no distributed failure recovery; host election does not restore state after authority process loss, and results are not durable/idempotent across restarts. PB10 content pins are only in-memory models until stored with authoritative match state.

## PB1 deterministic match core

`Scripts/Core/PB1` provides an engine-independent 4–6 player match controller, seeded normal-role assignment with an independent hidden Thief objective, scenario-private information assignment, guarded phases, vote validation and runoff resolution, scoring, public/private player view projections, privacy-safe event hooks, and a discussion clock. `Scripts/PB1Unity` provides the ScriptableObject assets and Unity timer, reveal, and local bootstrap components. The Thief does not consume a court-role slot, matching the established P2 scenario model. The existing pass-and-play app remains the authored prototype UI; the PB1 controller is exercised separately by `ProductionMatchCoreTests`.

GW2's `InformationIntegrityValidator` checks each round's one-Thief assignment, unique court roles, one correctly owned information package per player, and valid private evidence/fact references before scenario commit. `InformationProjectionService` is the public/private/revealed projection boundary. `SessionLifecycleRuntime` maps stable room player IDs to the owning match-local private projection; a transport adapter must still authenticate and privately route that response.

GW3 extends PB1 scenario truth with versioned world metadata, location/actor/object definitions, causal timeline links, source/path-tagged evidence, opportunity validation, alibi/claim contradiction assessment, bounded end-to-end deterministic retries in `MatchController`, and gated public/revealed scenario projections. Preparation requires the configured solvability result; the default sample is uniquely solvable using an authored private elimination clue. Private clue projections omit authority-only evidence-to-truth links. Broader authored candidate rules, retry parity for the legacy `GameController` entry point, and the full GW14 scenario QA suite remain production gates.

GW4 adds the authority-owned, turnless discussion history for chat, questions, responses, structured claims, challenges, contradiction records, hypotheses, timer deadlines, optional consensus early voting, and immutable paged snapshots. Structured contradiction records require evidence known to the claimant and retain their source path; ordinary chat is never interpreted as truth. The local visual adapter submits and displays only accepted messages. Provider transport must still authenticate and broadcast events and restore paged snapshots on reconnect; compaction remains open.

GW5 adds sequenced authoritative voting, target eligibility, one-vote locking, hidden ballot targets, deadline and all-voted closure, and immutable `FinalizedVoteCollection` handoff. The network session API exposes only caller-specific targets and aggregate progress; an authoritative host scheduler polls vote deadlines. Provider transport still requires authenticated adapters and broadcast/reconnect integration.

GW6 validates and deterministically counts only finalized GW5 collections, supports partial timeout ballots, caches resolution by round and vote sequence, and applies configurable tie/revote/second-tie escape policies. Privacy-safe result events expose counts and selected targets without revealing roles. Empty ballots remain pending without selecting a target; an explicit recovery policy is still needed before automatic round progression can be enabled for that case.
