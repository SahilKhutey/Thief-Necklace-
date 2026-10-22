# GW9 — Round Completion, Results, Progression, and Rematch

## Scope

GW9 begins after GW8 has finalized the round score ledger. Scoring does not itself complete a round: the runtime stores a completed round result, presents the results phase, synchronizes continuation, and only then completes the round or match.

## Round results

`RoundCompletionService` requires a completed reveal and a finalized score snapshot for the same match and round. It stores an immutable `CompletedRoundResult` with the outcome, selected target, actual Thief, revealed roles, score transactions, round scores, cumulative scores, round index, and timestamp. It rejects out-of-order results and verifies each cumulative total equals the previous total plus this round's points.

The `Results` phase holds the committed result. The default display window is four seconds minimum and thirty seconds maximum. Players can send an idempotent continue command scoped to the current match ID and round ID. Once the minimum display time has elapsed, all connected players must be ready for early progression. At the maximum deadline, the authority can force progression. Local Unity play polls its timer automatically. Network hosts must call `PollResultsTimeout` from their authoritative heartbeat.

Reconnect snapshots expose the active result, round history, ready player IDs, and results deadline. Duplicate ready commands do not create duplicate readiness events. Stale match or round identities are rejected.

## Round and match progression

A non-final round enters `RoundEnd`, increments the round index, assigns a new round ID, regenerates the round state, and retains the cumulative score ledger. Total rounds remain room-configurable (default five).

Before starting another round, the core checks the configured minimum connected player count (default four). The disconnect policy can continue with remaining players, leave results paused until enough players return, or abort the match. Final results freeze the cumulative score ledger and assign shared ranks for equal scores. Player ID sorting stabilizes display order only and is not a tie-breaker.

## Rematch and history

The match completion screen exposes a rematch request. Eligible participants request once; the default requires every still-connected participant to request, while disconnected participants are excluded. The new roster must meet configured 4–6 player limits. Starting the rematch creates a fresh match runtime, seed, and match identity. A failed start restores the completed match and prior lobby connection state.

Completed match history remains available in memory with its round results, score transactions, seed, and final score snapshot. History is retained across a rematch within the room's lifetime.

## Integration boundary

The session layer is an in-memory authority prototype. Its transport contract is not connected to a provider, and the application host must schedule results timeout polling. Room limits currently support four to six players. The game design's broader four-to-eight target requires coordinated changes across core validation, networking, UI, and tests.
