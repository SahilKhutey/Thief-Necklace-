# GW5 — Voting, Submission, Lock, and Finalization

**Status:** Authoritative vote sessions, eligibility, privacy projections, timeouts, and finalized vote collections are implemented in the PB1 runtime and local gameplay path.

## Runtime contract

- Discussion completion opens a voting instance with a monotonically increasing vote sequence, an authoritative UTC start/end time, and a fixed eligible voter set from the active match roster.
- A vote target is selected locally and submitted separately. The authority validates the active phase, sequence, voter membership and connection, self-vote rule, one-vote rule, and the target's current eligibility. Runoff targets follow the resolution-provided tied candidate set.
- Accepted votes are immediately locked. Rejections carry a `VoteRejectionReason`; rejected submissions do not alter the authoritative ballot.
- Live voting snapshots expose the sequence, status, timer, aggregate submitted/eligible counts, whether the requester submitted, and that requester's eligible target IDs. They never include anyone's target choice.
- Voting closes when every eligible voter has submitted or the authority clock reaches the end time. Both paths produce an immutable `FinalizedVoteCollection`; timeout collections preserve valid submitted ballots and mark `TimedOut`, without inventing missing votes.
- Finalized records include vote ID, voter, target, sequence, and authority timestamp. The full record list is available to resolution code; public snapshots omit targets and totals.
- Revotes create a new sequence and a fresh service state. Old vote records remain available only through the completed collection; they cannot be accepted into the new instance.

## Integration boundary

`CoreGameRuntime.GetFinalizedVotes()` is the GW6 handoff. The immutable collection now carries match/round identity and the fixed eligible voter IDs alongside the vote sequence and records. `SessionLifecycleRuntime` provides authenticated vote submission, requester-specific target lists, aggregate progress snapshots, privacy-safe vote events, and GW6 result events. An authoritative host scheduler must call `PollActiveVotingTimers()` so timeout events are emitted even when no player sends another command. Clients must never close a vote based on their local countdown.

The GW6 resolver accepts complete or partial finalized collections once the vote session closes. Empty finalized collections remain empty for the configured pending policy; the runtime does not assign a replacement target.
