# GW10 — Multiplayer Networking and Authority Contract

## Implemented provider-neutral layer

`NetworkReplicationRuntime.cs` defines stable player/actor/connection identities; protocol and message headers; sequenced event envelopes with public, owner-only, and authority-only visibility; authenticated command contracts; an authority command router; public match and phase snapshots; validation; an ordered client state store; reconnect state transitions; and checkpoint persistence seams.

Commands carry a unique command ID, player ID, match ID, round ID, type, and payload fingerprint. The router verifies protocol compatibility, authenticated identity, authority ownership, roster membership, and current match/round identity. Replayed command IDs return the cached result, while reusing an ID with different command data is rejected. Domain handlers remain responsible for phase-specific rules such as vote eligibility and self-vote prevention.

Authority events receive monotonic sequence numbers. Clients ignore already-applied events and request a fresh snapshot when they detect a sequence gap, protocol mismatch, invalid snapshot, or unexpected match/round identity. Timers replicate phase start and end server timestamps; a client derives its display countdown from estimated server time, while only the authority advances gameplay.

Public snapshots intentionally omit roles, clues, objectives, and truth state. `NetworkEnvelope` limits private events to their intended player, and authority-only payloads are not accepted by the client state store. Existing `SessionLifecycleRuntime` private-information APIs remain authenticated and owner-scoped. The authority checkpoint contract can retain protected state and committed transition IDs for handoff; the included in-memory implementation is development-only.

## Session and provider boundary

Session identity now carries a protocol version, and room creation/join rejects incompatible clients. Session match snapshots expose the room event sequence. `INetworkBackend` and `IRoomTransport` remain provider-neutral contracts; no Photon PUN/Chat or Unity Netcode adapter is connected. A provider adapter must authenticate the connection-to-player mapping, serialize and route private envelopes only to their recipient, persist protected authority checkpoints, publish ordered events, synchronize server time, and schedule GW9 result timeout polling.

## Scope limitations

The existing gameplay model, scenario data, room registry, and result contracts support **4–6 players**. GW10's requested **4–8 player** room target and eight-player integration acceptance require adding two distinct roles and scenario information packages, then expanding the gameplay, backend, content validation, UI, and test limits together. That gameplay/content expansion is not implemented here.

Authority checkpoints and match history are still in-memory until a provider-backed durable store is supplied. Authority transfer therefore has a recovery contract and serialization seam, but is not automatic across process loss. Network security also depends on the transport adapter enforcing authenticated routing; client-side filtering is not a security boundary.
