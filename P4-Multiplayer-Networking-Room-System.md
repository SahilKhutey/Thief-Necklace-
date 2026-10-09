# Thief & the Necklace

## P4 — Multiplayer Networking & Room Architecture

**Status:** Production architecture / implementation specification  
**Parent:** P0 → P1 → P2 → P3  
**Engine:** Unity 6 LTS  
**MVP players:** 4–6  
**Network model:** Host-authoritative client/server over Relay  
**Primary services:** Unity Multiplayer Services SDK, Netcode for GameObjects (NGO), Relay, Unity Authentication  
**Core requirement:** Private information must remain private.

The original GDD proposed Photon PUN 2 for the MVP and listed Unity Netcode/Relay as an alternative. P4 selects Unity Multiplayer Services (MPS) sessions with NGO and Relay for the production architecture. Unity positions NGO for GameObject/MonoBehaviour workflows and smaller-scale multiplayer; MPS sessions coordinate player membership and network connection setup while NGO synchronizes gameplay state. Verify package versions and APIs at implementation time. [Unity: Sessions overview](https://docs.unity.com/en-us/mps-sdk/sessions-overview) · [Unity: Relay with NGO](https://docs.unity.com/en-us/mps-sdk/tutorials/relay-and-ngo)

## 1. Objective and system scope

P4 connects real players through room creation, joining, lobby readiness, authoritative round state, private role/clue delivery, discussion, voting, reveal, scoring, disconnect handling, and rematch.

```text
CREATE / JOIN SESSION
        ↓
LOBBY + READY STATE
        ↓
AUTHORITATIVE ROUND GENERATION
        ↓
PRIVATE ROLE / CLUE DELIVERY
        ↓
DISCUSSION → VOTE → RESOLVE
        ↓
REVEAL → SCORE → REMATCH
```

The game has low-frequency state changes, no synchronized movement or physics, and a small player count. Network design should prioritize correctness, privacy, reliability, and simple synchronization.

## 2. Networking decision and topology

Use Unity 6 LTS, MPS SDK sessions, NGO, Unity Transport, Relay, and Unity Authentication. For Unity 6 projects, Unity recommends accessing Relay through the MPS SDK. Sessions can coordinate connection setup and session membership; NGO owns gameplay-state synchronization. [Unity: Relay servers](https://docs.unity.com/relay/relay-servers) · [Unity: Sessions overview](https://docs.unity.com/en-us/mps-sdk/sessions-overview)

```text
MPS SESSION (membership + join flow)
              │
              ├── Relay (connection transport)
              │
              └── NGO HOST / AUTHORITY (game rules)
                         │
                  3–5 clients
```

Relay transports packets and helps players connect through NAT/firewalls; it does not decide roles, votes, or scores. The host application is the gameplay authority for MVP.

## 3. Authority and trust boundaries

The host is authoritative for:

- Player roster, readiness, and match start.
- Round seed, scenario, world truth, timeline, role assignment, Thief identity, and clue ownership.
- Phase transitions and authoritative deadlines.
- Vote validation, tally, tie resolution, outcomes, and score calculations.
- Round and match results.

Clients own local input, UI, animation, audio, chat composition, vote selection, and private display. A client sends intent; the authority validates it and commits the state change.

```text
CLIENT INPUT → REQUEST → AUTHORITY VALIDATION → STATE CHANGE → UPDATE
```

Clients never submit an authoritative vote count, score delta, phase transition, Thief assignment, or reveal decision. The host-authoritative model protects against ordinary client tampering, but the host itself can inspect the full mystery state. It is suitable for a trusted friend-group MVP, not a competitive game requiring protection from a malicious host. Competitive/public release would need a dedicated authoritative server or another trusted authority model.

## 4. Session and room model

Each room/session has a stable session ID, join code, host identity, player roster, capacity (6), public game mode/rules, match status, and readiness. Minimum players to start: 4. Late join is lobby-only in MVP; after the match starts, reject new gameplay joins with a clear “Match already started” message.

Only public matchmaking/session metadata belongs in session properties: mode, current/max player count, match-started flag, round count, and public rule selections. Never store the Thief, private roles/clues, world truth, complete timeline, or unrevealed votes in public session metadata. Session data coordinates the group; it is not the gameplay-state replication mechanism. [Unity: Multiplayer Sessions building block](https://docs.unity.com/en-us/engine/6000.3/manual/building-blocks/multiplayer/sessions)

### Create flow

1. Initialize Unity Services and authenticate.
2. Create an MPS session configured for Relay and the selected NGO network type.
3. Start the NGO host/network.
4. Register host as the first player, publish safe public room metadata, and show the join code.
5. Enter the lobby.

### Join flow

1. Authenticate and validate the join code.
2. Find and join the session.
3. Let the session connection flow establish Relay/NGO connectivity.
4. Receive public lobby state and enter the lobby.

Prefer the unified MPS session flow over manually coordinating independent Lobby and Relay operations unless a specific product need requires lower-level control. MPS can manage session creation and connection setup; exact API calls must follow the package version selected for the project. [Unity: Session networking](https://docs.unity.com/en-us/mps-sdk/session-networking)

## 5. Identity and player state

Do not use display names as authoritative identifiers. Maintain a stable service/session player identity and the current NGO client ID separately; client IDs may change across reconnects or sessions.

Public roster state:

- Stable player ID and display name.
- Connected/reconnecting/removed state.
- Ready state.
- Public cumulative score.

Private player state:

- Role ID, `isThief`, private situation, and owned clue IDs/text.

Private player state must never live in a normal replicated NetworkVariable or any payload sent to all clients.

## 6. Private data and public round state

This is the most important network rule. The authority generates the complete mystery, validates it, commits it, and then produces distinct public and per-player private payloads.

Public payload sent to all players:

- Scenario ID, title, and public description.
- Round number, public phase, and phase deadline.
- Public roster and vote-submission count.
- Final result only after resolution.

Private payload sent only to its recipient:

- That player's role and objective.
- That player's private situation and clue(s).

Authority-only state before reveal:

- Full world truth and timeline.
- All roles and Thief identity.
- All clue ownership and private information.
- Individual ballots and tally inputs until resolution.

Use targeted NGO client RPCs or another recipient-targeted transport available in the chosen NGO version. The API shape is version-sensitive; check current package documentation during implementation. Do not send every secret to every client and rely on the UI to hide it.

## 7. Round generation and commit

At match start, authority locks the roster and calls the P2 generation system with a round seed. It generates scenario, roles, one Thief objective, timeline, situations, and clues; runs the validator; then commits the candidate round.

```text
GENERATING → VALIDATING → COMMITTED → PRIVATE/PUBLIC DELIVERY
```

Before commit, no gameplay phase begins. After commit, roles, clues, and Thief identity do not regenerate. If the candidate fails validation, retry within a bound or use a known-valid fallback; if generation cannot succeed, abort the round cleanly. The networking layer transports the validated result and should not know the clue-generation rules.

## 8. Networked game state and timers

The authority owns current phase, phase start, phase end, round number, and state version. Clients render remaining time from an injected synchronized network clock:

```text
remaining = phaseEndNetworkTime - networkClock.Now
```

Do not broadcast a decrementing timer value every second. Send phase changes and authoritative deadlines; clients render locally. Do not use local wall-clock time (`DateTime.Now`) for authoritative phase resolution.

Every significant state update should carry a monotonically increasing state version so clients can discard stale updates. Client commands should carry a unique request ID so retries can be handled idempotently.

## 9. Lobby readiness and start

The client submits `SetReady`. Authority checks that the player belongs to the session and updates the roster. The match can start only when 4–6 players are present and all required connected players are ready. The start command is host-only (or an explicitly defined party rule); clients cannot skip the lobby gate. When the match starts, close the room to additional players.

On returning to a lobby after the match, reopen it subject to capacity. A rematch can retain the same session and roster, but must create a new seed and freshly assign roles and Thief.

## 10. Discussion and chat

Chat is room-wide in MVP. No private whisper, spectator, or dead-player chat. Keep the discussion UI independent from chat transport through an interface such as `IChatService` so the backing implementation can change.

Each chat message contains message ID, sender ID, server/session sequence, timestamp, and text. Validate sender and session membership; reject empty or oversized messages; apply rate limits; preserve a consistent display order. Suggested starting limits are 180 characters, minimum 250 ms between messages, and a burst of five messages. Tune these values during playtesting.

Chat transport can use a dedicated chat service or the session's network connection. Select the actual transport before implementation and document its moderation, retention, profanity, and platform-policy behavior. Keep chat events separate from authoritative game-state events.

## 11. Voting and resolution

Client sends only a target player ID. Authority validates:

- Correct session and registered sender.
- Current phase accepts votes.
- Target exists, is eligible, and is not the voter.
- No already-accepted vote exists from this player (or a replacement is allowed only while the ballot remains open).
- The request ID has not already been processed.

Authority records the vote and returns `VoteAccepted` or a structured rejection. Clients see only “your vote accepted” and public submitted count, not who voted for whom. Do not broadcast individual ballots before resolution.

Authority closes voting when all eligible players submit or the deadline expires. It computes plurality and tie resolution from its own stored ballots, following P1: a unique highest vote selects that player; a tie at the top opens a runoff among tied candidates; a second tie or no valid runoff vote means the Thief escapes. The authority decides caught/escaped and broadcasts the result. Only the final deciding ballot is used for correct-voter scoring.

## 12. Reveal and score synchronization

Reveal is an authoritative network event, never a client-side decision. After vote resolution, authority sends reveal data containing accused player, Thief, roles, verified clue/timeline explanation as appropriate, outcome, and score deltas. This is the first point where full role information is broadcast.

Authority applies scores once per resolved round. A score update includes match ID, round number, player ID, delta, and new total. Track resolved round IDs to prevent duplicate scoring after retries or reconnects. Clients render the received totals; clients do not calculate authoritative scores.

## 13. Disconnects, reconnect, and host loss

Connection states are `Connected`, `Disconnected`, `Reconnecting`, and `Removed`.

- **Lobby disconnect:** remove the player from readiness; broadcast roster change.
- **Discussion disconnect:** for MVP, stop their chat/input and continue. Their missing vote abstains. They remain in the match result unless the round-abort condition below applies.
- **Vote disconnect:** missing ballot is an abstention; a previously accepted ballot remains recorded.
- **Thief disconnect before resolution:** abort the current round with no score and start a replacement round after reconnection/party confirmation. This fairness rule is a P4 decision, not specified in P0.
- **Non-Thief disconnect:** continue unless fewer than the minimum viable number remain; MVP should define this minimum as four connected players, otherwise abort the round with no score.
- **Reconnect:** allow a known session member to reconnect only if session and round policy permit. Before full active-round resumption is implemented, a reconnecting player may rejoin as observer until reveal; do not resend any other player's private data.
- **Late join after match start:** reject.

### Host loss policy

Unity MPS sessions distinguish host election from synchronized-data migration. Unity documents that session host election alone does not migrate network-synchronized state, and its default data migration path is for Netcode for Entities; NGO projects need a migration strategy such as Distributed Authority or a custom handler. Relay host loss also disconnects other clients from that Relay allocation. [Unity: Session host migration](https://docs.unity.com/en-us/mps-sdk/session-host-migration) · [Unity: Relay integration](https://docs.unity.com/en-us/mps-sdk/networking/relay-integration)

For this host-authoritative NGO MVP, use a safe restart policy: invalidate the active round, award no points for it, recover/recreate the session and Relay connection, elect or establish a new host, then start a fresh authoritative round with a new seed. Do not claim seamless migration. Do not restore secrets from client state unless a complete, validated authority-snapshot transfer is designed and tested.

Full state migration is a future feature requiring an explicit trusted-state transfer design covering world truth, private assignments, phase/deadline, votes, score ledger, and idempotency records.

## 14. Security and privacy controls

Host authority protects against untrusted client commands, not a malicious host. Required controls:

- No client API to set the Thief, reveal roles, resolve votes, change phase, or mutate authoritative score.
- All client requests pass identity, session, phase, payload, and game-rule validation.
- Private data goes only to its owner before reveal.
- Full truth and all votes remain authority-only until the designed reveal point.
- Public session metadata contains no secrets.
- Reconnect and retry handling cannot duplicate scores or votes.
- Avoid logging private payloads in production client logs.

## 15. Message contract

Client → authority commands:

`SetReady`, `StartMatchRequest`, `ChatSend`, `VoteSubmit`, `RematchRequest`, `LeaveSession`.

Authority → clients events:

`PlayerJoined`, `PlayerLeft`, `ReadyChanged`, `MatchStarted`, `RoundStarted`, `PublicScenario`, targeted `PrivateRoundState`, `PhaseChanged`, `ChatMessage`, `VoteAccepted`, `VotingClosed`, `RevealStarted`, `RevealResult`, `ScoreUpdated`, `RoundEnded`, `MatchEnded`, `SessionError`.

Every command has a request ID and authenticated sender context. Every state event has a state version or event sequence. Errors use a stable enum such as `NotConnected`, `SessionNotFound`, `SessionFull`, `MatchAlreadyStarted`, `NotReady`, `InvalidPhase`, `InvalidPlayer`, `InvalidVote`, `VoteClosed`, `DuplicateRequest`, `HostUnavailable`, and `ServiceUnavailable`; UI maps those to plain messages.

## 16. Network object and code boundaries

Potential network objects/controllers:

- Persistent: network game session, player registry.
- Round-scoped: round controller, vote controller.
- Service abstractions: session service, chat service, network clock.
- Recovery: disconnect handler and host-loss/session recovery handler.
- Local-only: card artwork, chat message UI, buttons, backgrounds, timers, particles, screen animation, audio.

The role card is local presentation driven by the local private payload. It is not a networked physical card. Other players' cards remain face-down locally until authoritative reveal data arrives.

Keep the following boundaries:

- Scenario generation returns pure data and does not depend on NetworkObjects.
- Networking transports validated round data and does not implement clue generation.
- UI sends game commands and renders state; it does not know Relay details or mutate authority state.
- Session service owns create/join/leave; round controllers own gameplay phases.

## 17. Suggested project structure

```text
Scripts/Networking/
├── Bootstrap/       MultiplayerBootstrap.cs, AuthenticationService.cs
├── Sessions/        SessionService.cs, SessionState.cs
├── Authority/       GameAuthority.cs, AuthorityState.cs, NetworkClock.cs
├── Players/         NetworkPlayerRegistry.cs, NetworkPlayerState.cs
├── Round/           NetworkRoundController.cs, PublicRoundState.cs,
│                    PrivateRoundState.cs, PrivateStateDispatcher.cs
├── Voting/          NetworkVoteController.cs, VoteMessages.cs
├── Chat/            IChatService.cs, ChatMessage.cs
└── Recovery/        DisconnectHandler.cs, HostLossRecoveryHandler.cs
```

## 18. Implementation sequence

1. Select and pin Unity, MPS SDK, NGO, Transport, Authentication, and platform package versions.
2. Bootstrap Unity Services, authentication, and NGO.
3. Create/join sessions and enforce room capacity and join-code errors.
4. Synchronize roster and ready state.
5. Implement authoritative phase machine and network clock.
6. Integrate P2 round generation, commit, public scenario delivery, and targeted private delivery.
7. Add room chat and message validation.
8. Add authoritative vote submission, anonymity, tie/runoff, reveal, and scoring.
9. Add disconnect, reconnect policy, host-loss abort/recovery, rematch.
10. Validate with Multiplayer Play Mode and real clients across 4-, 5-, and 6-player cases.

## 19. Required validation matrix

### Privacy

For a test round with distinct roles/clues, inspect payloads and client memory/logs: each client receives its own private state only; other secrets and full truth remain absent until reveal.

### Vote validation

Reject self-vote, nonexistent target, duplicate/late vote, vote in the wrong phase, forged voter ID, and forged vote count. Confirm the runoff allows only tied candidates and a second tie resolves as escape.

### Authority validation

Confirm there is no client command to set Thief, add score, force reveal, or advance phase. Verify duplicate event delivery cannot score a round twice.

### Disconnect validation

Test disconnect/reconnect in lobby, discussion, initial vote, runoff, reveal, and rematch; include Thief disconnect, host exit, Relay loss, and join-after-start. Each should have a deterministic expected result.

### Network conditions

Test chat ordering, vote acknowledgement, deadline display, reveal synchronization, and score synchronization under latency, packet loss, and service interruption. Confirm clients never silently construct different truth models.

Use Unity Multiplayer Play Mode for local multi-client simulation, then validate service/Relay behavior with real clients. [Unity: Casual co-op quickstart](https://docs.unity.com/en-us/multiplayer/quickstarts/casual-co-op-quickstart)

## 20. P4 definition of done

P4 is ready when 4–6 clients can authenticate, create/join a room, synchronize roster/readiness, start a match, receive a public scenario and only their own private role/clues, discuss, submit anonymous votes, resolve ties authoritatively, reveal roles, synchronize scores, play another round/rematch, handle documented disconnects and host loss, and pass privacy/anti-cheat validation.

## 21. Architecture lock and P5 handoff

The network privacy rule is:

> Synchronize only the information each recipient is allowed to know.

```text
PUBLIC       → scenario, roster, readiness, discussion, vote count, final result, scores
PRIVATE      → recipient's role, objective, situation, clues
AUTHORITY    → full truth, timeline, all assignments, unrevealed ballots, scoring decision
```

P5 — UI/UX + Micro-Animation System should build the interface around network-ready states: Main Menu, Create/Join, Lobby, Scenario Intro, Private Role Card, Investigation Table, Discussion, Voting, Resolution, Thief Reveal, Score, and Next Round. It should define responsive layouts, player slots, chat, vote interactions, loading/error/disconnect states, animation hooks, accessibility, and Unity UI implementation while preserving the established visual direction.
