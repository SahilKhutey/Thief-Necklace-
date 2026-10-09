# Architecture Overview

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
