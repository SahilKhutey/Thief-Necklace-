# GW1 — Match Lifecycle & Session Workflow

**Track:** Gameplay Workflow Production  
**Status:** Provider-neutral lifecycle runtime implemented; provider integration pending  
**Depends on:** [GW0 Gameplay Workflow Foundation](GW0-Gameplay-Workflow-Foundation.md)  
**Runtime baseline:** PB1 local match core and PB2 networking contracts

GW1 defines the path from a connected player entering a room through lobby, match start, round progression, match results, rematch, return to lobby, or exit. It keeps session/room state separate from gameplay match state and defines the handoff into GW2.

```text
Connect → Create/Join Room → Lobby → Ready → Start Validation
        → Match Initialization → Round 1 → ... → Final Results
        → Rematch / Return to Lobby / Leave
```

## 1. Scope and ownership

GW1 owns connection and room membership lifecycle, lobby state and readiness, match-start authorization, match identity/configuration, round index progression, finalization boundary, and post-match session choices. GW1 does not own role/clue assignment, scenario truth, discussion, ballots, reveal, or score calculation; those remain gameplay workflow responsibilities.

`SessionState`, `RoomState`, `MatchStatus`, and `RoundPhase` are separate state dimensions. A session contains a room; a room may launch successive matches; a match contains rounds; a round has gameplay phases. Do not combine them into one large enum.

| State dimension | Examples | Authority |
| --- | --- | --- |
| Session | Disconnected, Connecting, Connected, CreatingRoom, JoiningRoom, InLobby, StartingMatch, InMatch, ReturningToLobby, LeavingRoom, Error | Session/transport adapter |
| Room/lobby | Room ID, roster, readiness, capacity, host/authority, locked state | Authoritative room service |
| Match | NotStarted, Preparing, Active, Completing, Completed, Aborted | Authoritative match lifecycle controller |
| Round/gameplay | Round index and GW0 gameplay phase | Authoritative gameplay runtime |

## 2. Architecture and provider boundary

```text
Session UI ── session commands ─┐
Gameplay UI ─ gameplay commands ├─> Session/Match Runtime ─> Authoritative state
                               │             │
                               └─────────────┴─> Room transport adapter
```

Session and gameplay code depend on provider-neutral contracts. A Photon, Unity Netcode, Relay, or other adapter translates provider callbacks into validated session events. A provider callback is input to the authority, not permission to bypass lifecycle validation. No transport provider is selected or connected by this specification.

## 3. Room configuration and lobby state

Room configuration is data-driven:

| Setting | Rule |
| --- | --- |
| Minimum players | Current project baseline is 4 |
| Maximum players | Current project baseline is 6; the source GDD describes up to 8 |
| Match rounds | Current MVP result contract expects 5; local PB1 controller accepts a configured positive count |
| Ready-to-start | Require all connected lobby members ready, in addition to valid player count |
| Join during match | Rejected; spectator/queue behavior is out of scope |

Resolve the maximum-player discrepancy before expanding beyond six: role, clue, scenario, score-result, and QA contracts currently assume 4–6 in multiple runtime locations.

Lobby state contains room ID, authoritative host/authority identity, configuration, match lock/status, and an ordered roster. Each lobby player contains stable player ID, display name, connection state, ready state, and seat index. Reconnecting a known member restores membership from authority; it does not create a second player or inherit stale readiness.

## 4. Session and room workflows

### Create room

1. Require a connected session and valid configuration.
2. Send a create request through the room adapter.
3. On authoritative success, assign a room ID, add the creator as a connected unready member, initialize the lobby, and publish the authoritative snapshot.
4. On failure, remain outside the room and return a typed failure reason.

### Join room

1. Require a connected session and resolve the requested room.
2. Validate room exists, is not full, has not locked its roster, and does not already contain a conflicting identity.
3. Add/reconnect the member atomically and return a current room snapshot.
4. On rejection, return a stable reason and retain the prior session state.

### Readiness

`SetReadyStateCommand` is valid only for a connected lobby member while the room is unlocked. Authority owns readiness; clients render the latest authoritative snapshot. Disconnect clears readiness before match start. Start eligibility is recomputed after every membership, connection, readiness, or configuration change.

### Leave room

Leave is confirmed by the authoritative room service before the client transitions to connected/outside-room. Before match start, remove the member and recompute start eligibility. During an active match, emit a disconnect/leave signal and delegate gameplay consequences to the configured player-loss policy; do not silently mutate roles or scores.

## 5. Start match contract

`StartMatchCommand` is accepted only when all conditions hold:

- Requesting identity is authenticated, is a connected room member, and has start authority.
- Room is not already locked or starting.
- Connected roster is within configured minimum and maximum.
- All connected roster members satisfy the readiness policy.
- Match configuration and scenario/content selection are valid.

The host clicking Start is not itself authorization. The authoritative handler evaluates current room state at command-processing time, so a simultaneous departure can invalidate the request. A repeated or concurrent start request is rejected after the first request atomically claims the start lock.

Start transition:

```text
InLobby → StartingMatch → MatchStatus.Preparing → Active
                   └─ initialization failure → Aborted
```

Before Round 1 begins, freeze roster membership and initialize a unique match ID, authoritative seed, total-round configuration, round index 1, zeroed per-player match totals, and initial round context. If initialization fails, publish an abort/failure result, do not emit a normal `MatchStarted` or `RoundStarted`, do not expose a partial match, and return the room to a safe state according to policy.

Emit lifecycle events in order: `MatchStartRequested`, then either `MatchStartRejected` or `MatchStarted`, then `RoundStarted(1)`. `RoundStarted` must not precede a committed active match. The GW2 handoff receives a valid match ID/seed, frozen player roster, round index 1, total rounds, zeroed match scores, and current round seed/context. Private role data remains GW2-owned.

## 6. Match and round progression

Round index is authoritative, begins at 1, and cannot be client-mutated. On `RoundCompleted`, verify scoring/result persistence completed exactly once. If more rounds remain, increment the index, derive the next round seed from versioned match seed + index + scenario identity/version, construct a clean round context, preserve player identities/room membership/match identity/seed/cumulative scores, and emit `RoundStarted`.

Reset round-local role/private data, scenario, discussion, votes, reveal, and round score only as the next-round workflow initializes them. Do not clear cumulative match scores. No transition to a next round is allowed before the prior round is complete.

After the final round, build an immutable match result from completed round results, validate final totals, rank by match score descending, record all co-winners for tied top scores unless a later approved tie-break policy says otherwise, set status to Completed, and emit `MatchCompleted` once. Completed matches reject ordinary gameplay commands and further round/score mutations.

## 7. Post-match actions

| Action | Result |
| --- | --- |
| Play again / rematch | Requires a completed match and valid room membership. Preserve session/room/roster, create a new match ID and seed, reset match scores and round index to 1, and clear readiness only if the configured lobby policy requires it. The previous result remains immutable. |
| Return to lobby | Finalize current match; retain room and members; clear match-specific state; set every member unready; unlock the lobby. |
| Leave room | Request provider departure and wait for confirmation before returning to the main menu/session-connected state. |

A rematch is a new match, never another round: match ID and seed change; the previous result and score history do not.

## 8. Disconnect, reconnect, and authority loss

Before match start, a disconnected member becomes unready and no longer counts toward connected minimum-player eligibility. The room can remain startable if the remaining connected, ready roster still satisfies policy.

During a match, membership/roster remains frozen. Emit `PlayerDisconnected` and invoke a dedicated player-loss policy. Supported policy choices include continue, pause, remove from participation, or abort; the production policy must be selected before online release. Do not encode a default consequence in UI or a transport adapter.

Reconnect authenticates the player, locates room/match membership, and returns a versioned authoritative room snapshot or match snapshot plus the player’s private projection. The client discards stale local state and rebuilds from that snapshot. Authority loss enters a distinct recovery state; continuation requires an explicit provider-supported authority transfer with a validated state snapshot. If recovery fails, enter an error/abort path without fabricating completion or scores.

## 9. Snapshot and result contracts

Lobby snapshot fields: schema version, room ID, host/authority, ordered public roster, connection/readiness, capacity/configuration, lock state, and match status.

Match snapshot fields: schema version, match/room IDs, match status, round index/count, current public phase, public roster/scores, permitted round/scenario summary, and versioned state sequence. Include private payload only for the authenticated recipient. Never include the complete truth model or other players’ private data.

The immutable match result contains match ID, completed round count, final score rows, winner IDs, and references to validated per-round outcomes. Result construction occurs once after the final round; retries return the same result identity/content. Result persistence failure leaves the authoritative completed state recoverable and does not generate a conflicting second result.

## 10. Commands and events

Session commands: `CreateRoomCommand`, `JoinRoomCommand`, `LeaveRoomCommand`, `SetReadyStateCommand`, `StartMatchCommand`, `RequestRematchCommand`, `ReturnToLobbyCommand`.

Lifecycle events: `SessionConnected`, `SessionDisconnected`, `RoomCreateRequested`, `RoomCreated`, `RoomJoinRequested`, `RoomJoined`, `RoomJoinRejected`, `PlayerJoinedRoom`, `PlayerLeftRoom`, `PlayerReadyChanged`, `MatchStartRequested`, `MatchStarted`, `MatchStartRejected`, `RoundStarted`, `RoundCompleted`, `MatchCompleted`, `MatchAborted`, `RematchRequested`, `RematchStarted`, `ReturnedToLobby`, `RoomLeft`, `PlayerDisconnected`, and authority-recovery outcomes.

Events represent committed state transitions. Rejected requests return stable reason codes and do not appear as successful state changes. All events include match/room correlation IDs when available, sequence/version, and privacy-safe metadata.

## 11. Failure and recovery matrix

| Failure | Required response |
| --- | --- |
| Room creation or join fails | Preserve prior session state; return typed reason |
| Room full or match already started | Reject join; publish current safe room status when authorized |
| Player count/readiness becomes invalid | Disable start eligibility in authoritative lobby state |
| Start race or duplicate start | Process against current state; reject after lock is claimed or requirements fail |
| Match initialization fails | Abort without partial normal match/result; return to safe session state |
| Disconnect before start | Clear readiness; recompute connected count and start eligibility |
| Disconnect during match | Emit lifecycle event and invoke explicit player-loss policy |
| Reconnect | Re-authenticate and replace local state from authoritative snapshot |
| Authority lost | Transfer only through supported recovery; otherwise enter explicit match error/abort |
| Match result persistence unavailable | Preserve completed authoritative result for idempotent retry |
| Invalid rematch/return/leave | Reject without modifying completed result or active membership |

## 12. Invariants

- A player belongs to at most one active room on the service.
- A room has one authoritative lobby state and one room authority at a time.
- Roster count never exceeds configured maximum; start requires configured minimum.
- Match start is authorized, atomic, and accepted at most once per room/match generation.
- Roster is frozen after match start; late joins cannot enter the active match.
- Match initialization precedes `RoundStarted(1)`.
- Round index is one-based, monotonic, and authority-owned.
- Scores persist between rounds and reset only for a new match.
- A completed match/result is immutable; rematch creates a new match ID and seed.
- Disconnected clients cannot mutate authoritative lifecycle state.
- Session/lobby state is not copied into gameplay truth; hidden gameplay data is not copied into public room snapshots.

## 13. Verification contract

Future automated coverage should include session state transitions, create/join/leave, capacity and player-count boundaries, readiness and disconnect-before-start, start authority, simultaneous leave/start and double-start races, initialization abort, round transition and final-round completion, immutable results, rematch/new IDs and score reset, return-to-lobby readiness reset, reconnect snapshots, active-match disconnect policy, and authority-loss recovery. Provider adapters require integration tests against their actual room lifecycle and identity guarantees.

These are GW1 QA requirements; their listing does not imply they have passed. No network provider or transport implementation is certified by this document.

## 14. Repository implementation map and gates

The repository now contains a provider-neutral local authority implementation:

- `SessionLifecycleRuntime` owns room creation/join, readiness, host-authorized atomic start, match startup/rollback, snapshots, reconnect, disconnect policy, results, rematch, return-to-lobby, and leave flows. It authenticates no identities itself: callers must supply authenticated player IDs.
- `NetworkPlayerRegistry` stores ordered lobby membership, readiness, connection state, configurable 4–6 capacity, and match roster locking.
- `NetworkRoundController` starts at round 1, exposes public round snapshots, drives phase deadlines, and advances a fixed five-round flow, but does not construct or finalize the complete `MatchController` result, rematch, return-to-lobby, or failure recovery.
- `MatchController` owns deterministic match/round progression and configured round count; `SessionLifecycleRuntime` adapts its lifecycle signals and builds immutable final score rows and co-winners.
- `NetworkContracts` provides provider-neutral operation and `IRoomTransport` seams. No provider adapter, remote server enforcement, or persistence implementation is connected.
- `MatchResultValidator` currently validates 4–6 players and exactly five rounds, so this is a project configuration constraint, not merely a lobby UI setting.

The runtime currently defaults to five rounds but accepts a positive configured round count; reconcile this with the fixed-five `NetworkRoundController` and backend validator before persistence/provider integration. The active-player-loss policy is configurable as continue-with-disconnected-player or abort. Authority transfer is currently an in-memory host election and does not recover authoritative gameplay state after process loss.

## 15. GW1 acceptance and GW2 handoff

GW1's local lifecycle acceptance path is implemented by `SessionLifecycleRuntime`, `NetworkPlayerRegistry`, and the PB1 `CoreGameRuntime` adapter. This does not certify an online deployment: an authenticated provider adapter, server-side authority enforcement, durable/idempotent result persistence, authority-loss recovery, configuration alignment, and the verification suite above remain open. No automated tests have been run for this implementation.

At the GW2 handoff, the session runtime supplies a validated active match with match ID, authoritative match and round seeds, one-based round index, configured total rounds, frozen player roster, zeroed match scores, and current round context. GW2 then owns role, private situation, objective, and player knowledge assignment.
