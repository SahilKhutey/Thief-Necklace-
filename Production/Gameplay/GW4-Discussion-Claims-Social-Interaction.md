# GW4 — Discussion, Claims & Social Interaction

**Status:** Turnless authoritative discussion runtime, structured social actions, evidence-backed contradiction records, local chat presentation, and cursor-based history paging implemented. Provider-specific broadcast integration remains external.

## Implemented

- `DiscussionService` owns each round's ordered discussion history and lifecycle: chat messages, directed questions, target-only responses, structured claims, challenges, contradiction records, early-vote confirmations, and internal hypotheses.
- The authority assigns UTC timestamps and one monotonic event sequence. Text limits, player eligibility, connection state, phase, end timestamp, per-player rate limits, and the configured history cap are enforced before publication.
- Discussion starts after private reveal. The authoritative timer duration is configurable; early voting is optional and requires the configured number of distinct player confirmations. Ending freezes the state before the transition to voting.
- Structured claims remain separate from truth. Unstructured text stays ordinary chat. Contradictions require an explicit structured fact claim and matching evidence known to the claimant, and authority records the evidence ID and causal/source path. Those records stay out of public discussion snapshots and published room events. No result changes the Thief identity or marks a player guilty.
- `MatchController`/`CoreGameRuntime` and the local `GameController` expose the same chat, question, response, claim, challenge, hypothesis, snapshot, and early-vote operations.
- The local visual adapter publishes chat only after core acceptance, renders authority order and timestamps, and reports rejected messages locally. The discussion screen's call-vote action uses configured consensus during discussion and opens the ballot after voting begins.
- Snapshots copy history values so consumers cannot mutate the authoritative record through returned message objects. Reconnect clients request pages with `afterSequence` and `pageSize` (1–200), then continue from `NextSequence` while `HasMore` is true. `Sequence` reports the authority's latest sequence.

## Runtime rules

- The service has no speaking-turn state. Multiple connected players may submit actions while discussion is active.
- Repeated identical messages are separate messages unless configured rate limits reject them.
- Question responses must reference a current-round question and come from its target.
- Rejected commands do not enter the published history. Authority time and sequence, rather than client timestamps or arrival order, determine accepted ordering.
- Hypothesis sets are per-player reasoning aids and do not update world truth or public suspicion scores.

## Remaining integration work

- A provider adapter must authenticate room identities and route accepted discussion events to all room members. The session API provides authenticated command and paged snapshot endpoints; this project contains no provider-specific room transport implementation.
- History is capped by configuration and rejects new actions at capacity. Pagination is implemented; compaction is not.
- Automated contradiction checks intentionally handle only explicitly structured fact IDs. Natural-language interpretation, moderation, voice, and player-facing contradiction assistance remain future work.
- No tests were run for this pass.
