# GW11 — Network Recovery, Reconnection, and Authority Recovery

## Recovery and reconnect

`NetworkRecoveryRuntime.cs` adds a configurable recovery state machine, single-flight reconnect coordinator, capped exponential retry policy, input gating, recovery decisions, event catch-up planning, heartbeat and integrity diagnostics, typed failure records, and authority handoff coordination. The default policy allows five attempts, with exponential backoff starting at one second and capped at ten seconds, inside a 30-second recovery window. The gameplay input lock remains active until a validated authoritative snapshot has been applied and the owner's private state has been restored or revalidated.

Recovery starts from the authority snapshot. The coordinator checks the authenticated player and match identity, protocol, round bounds, sequence, roster, and snapshot validity before handing the full recovery snapshot to an atomic client-state replacement callback. Construction requires an explicit owner-private-state restoration callback, so an active client cannot report recovery without restoring or validating its own private state. Completed and aborted snapshots are applied too, so final results and abort state can be restored without reopening gameplay. A local client does not replay old animations or decide that a deadline expired.

The session reconnect path preserves the stable player ID, reconnects only an existing roster member after match start, and returns the current match snapshot plus that player's private information and own voting state when those projections are available. The snapshot carries the room event sequence, active GW9 result/readiness state, and recovery-window deadlines. The owner-scoped private information API remains the source for private data.

## Disconnect window and removal

A disconnection records an authority-side timestamp while retaining the match seat, score, role, and vote state. The default recovery window is 30 seconds, and the match continues during that window. The current room authority polls `ExpireDisconnectedPlayers`; an expired player is removed from room membership without rewriting the frozen match roster or prior results. No replacement is made in MVP. Existing room player-loss policy determines whether the match aborts after permanent removal. The recovery configuration can disable automatic removal.

Lobby departure, in-match departure, voting, reveal, scoring, results, and completed-match recovery all resolve to the latest authoritative match snapshot. An already-submitted vote remains in authority state; only the reconnecting player's vote projection is returned during voting. Disconnected players do not contribute to the connected-player continue handshake. Completed scores and match history remain unchanged.

## Authority handoff

`AuthorityRecoveryCoordinator` consumes a provider-selected new authority and accepts only a committed checkpoint at the observed authority sequence. It fails closed on a missing, stale, ahead-of-heartbeat, or invalid checkpoint rather than restoring stale state, then invokes a caller-supplied full-state validator and restore callback exactly once per match in the coordinator lifetime. The checkpoint carries match/round IDs, sequence, protected serialized state, and committed transition IDs. `InMemoryAuthoritativeStateStore` is only a development seam; production handoff requires a durable shared store with authenticated authority fencing and protected serialization. Photon MasterClient election and server/NGO authority selection remain provider responsibilities.

## Replication and diagnostics

The GW10 client state store ignores duplicate events, detects sequence gaps and requests a snapshot, rejects mismatched protocols/rounds, and atomically notifies presentation after snapshot replacement. `EventCatchUpPlan` uses bounded event replay or requests a fresh snapshot when the gap exceeds configuration. Equality with the authority sequence is an already-current recovery path, but the owner-private state still passes through restoration. `RecoveryTelemetry` records reconnect, snapshot, authority, removal, and abort counters; public state hashes are diagnostics only.

## Production boundaries

No Photon, Unity Netcode, secure chat channel, or durable checkpoint adapter is connected. The host integration must authenticate connection-to-player identity, send private state only on an isolated recipient channel, poll reconnect expirations, persist checkpoints before publishing commits, perform authority fencing, and map a recovery snapshot into an atomic client presentation projection. The gameplay model remains 4–6 players, matching the current project limit.
