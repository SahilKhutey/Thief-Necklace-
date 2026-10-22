# GW8 — Scoring, Transactions, and Round Rewards

## Workflow boundary

GW8 runs after the GW7 reveal has completed. It consumes the authoritative `RevealResult` and the immutable finalized vote collection for the same match and round. It awards points only; it does not resolve who the Thief is or alter the vote result.

## Default scoring policy

| Role | Value |
| --- | ---: |
| King | 8 |
| Queen | 7 |
| Minister | 6 |
| Soldier | 5 |
| Guard | 5 |
| Servant | 4 |
| Thief | 0 when caught |

When caught, each player whose finalized vote targets the revealed Thief earns their own role value. Wrong accusations earn zero. The caught Thief earns zero. When the Thief escapes, the Thief earns the sum of all normal role values (35 with the defaults); other players earn zero even if some selected the Thief in a tied ballot. `ScoringConfig` supports fixed correct-vote and escape rewards for future balancing.

## Authoritative score ledger

`ScoringService` validates the reveal/vote match and round identities, roster membership, unique voters, and vote sequences before producing transactions. Every transaction has a deterministic unique ID, match/round IDs, player ID, amount, and reason. IDs are retained as an idempotency guard. Score arithmetic is checked before committing the round totals, cumulative totals, and ledger.

`ScoreSnapshot` separates round scores from cumulative match scores and includes the transaction history for the round, finalization state, and version. Scoring is permitted only after reveal completion and a finalized vote collection. A new round starts with zero round scores while cumulative player scores persist; a new match creates a fresh ledger.

The session layer projects score snapshots and events from internal numeric seats to authenticated session player IDs. `GetScoreSnapshot` provides a reconnect snapshot, and clients cannot submit score transactions or mutate scores.

## Events and handoff

Scoring publishes `ScoringStarted`, one `ScoreTransactionCreated` per award, `RoundScoreFinalized`, `CumulativeScoreUpdated`, and `ScoringResolved`. GW9 can consume the finalized round snapshot and cumulative standings to show round results or advance/end the match.
