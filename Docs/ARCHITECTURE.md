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

Future online boundary (contracts only)
Client → authenticated transport → authoritative match service
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

PB1 is local and has no distributed failure recovery. The online state machine, reconnect/host-loss policy, idempotent server persistence, and dependency recovery remain implementation gates. PB10 content pins are only in-memory models until stored with authoritative match state.

## PB1 deterministic match core

`Scripts/Core/PB1` provides an engine-independent 4–6 player match controller, seeded normal-role assignment with an independent hidden Thief objective, scenario-private information assignment, guarded phases, vote validation and runoff resolution, scoring, public/private player view projections, privacy-safe event hooks, and a discussion clock. `Scripts/PB1Unity` provides the ScriptableObject assets and Unity timer, reveal, and local bootstrap components. The Thief does not consume a court-role slot, matching the established P2 scenario model. The existing pass-and-play app remains the authored prototype UI; the PB1 controller is exercised separately by `ProductionMatchCoreTests`.
