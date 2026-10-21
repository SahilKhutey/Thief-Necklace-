# GW6 — Vote Resolution, Tie, Revote, and Escape

**Status:** PB1 and local gameplay now consume immutable GW5 finalized collections through a deterministic resolver. Session results are projected to stable room player IDs without exposing roles.

## Resolution contract

- Resolution accepts a finalized collection only after the matching vote instance closes. It checks match, round, sequence, eligible voter set, submitted totals, duplicate voters, self-votes, and target eligibility before counting.
- Only submitted valid records are counted. Partial timeout collections are valid; an empty collection yields `NoValidVotes` / `Pending` with no selected target.
- Counts and highest targets are sorted deterministically. A unique highest target produces `TargetSelected`; tied highest targets produce a revote or the configured second-tie escape.
- `VoteResolutionConfig` defaults to two tie rounds, escape on the second tie, `TiedPlayersOnly` runoff targets, and `Pending` for no votes. Tie count resets when the next round starts. `AllEligiblePlayers` can be selected for revotes.
- Resolution commits are idempotent by round and vote sequence. A duplicate request returns the previously committed legacy outcome; a stale sequence is rejected.
- Result records expose counts, highest targets, selected target, tie count, and outcome. They do not expose roles or identify whether the selected player is the Thief. Role reveal remains a later phase.

## Integration

`FinalizedVoteResolver` is the pure GW6 validation/count/tie-policy component. `MatchController` and the local `GameController` publish immutable result objects, cache sequence results, and start a fresh GW5 voting instance when a revote is required. `SessionLifecycleRuntime.PollActiveVotingTimers()` closes expired voting and resolves its finalized collection, then publishes `SessionVoteResolutionEvent` using stable room player IDs. The visual adapter also resolves on timeout, including partial and empty ballots.

`IRoomTransport` defines the vote-result event contract but remains provider-neutral; a provider adapter must authenticate commands, broadcast results, and restore state after reconnect. No-vote `Pending` intentionally does not advance to reveal or scoring; a product recovery policy must decide whether to reopen, skip, or abort that round.

## Verification scope

No automated tests or build were run as part of this implementation. The implementation is covered by the existing result and transport seams, but production acceptance should add the GW6 resolver, tie, duplicate-resolution, and privacy cases to the automated suite.
