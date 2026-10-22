# GW7 — Reveal, Thief Identification, Necklace, and Outcome

**Status:** Authoritative reveal is implemented in the PB1 and local gameplay runtimes, with policy-filtered role exposure, ordered events, presentation acknowledgment support, and a room reconnect snapshot.

## Reveal boundary

- Reveal preparation is accepted only after GW6 commits a selected target or Thief escape. The service reads the active round's authoritative role assignment and validates exactly one Thief marker; it does not accept role or Thief IDs from a client request.
- A selected target produces `ThiefCaught` or `WrongPlayerSelected` by comparing the target with the authoritative Thief. An escape has no selected target and produces `ThiefEscaped`.
- The Necklace owner is the authoritative Thief. Necklace exposure is controlled by `RevealConfig`; recovery is not modeled as an action.
- `RoleRevealPolicy` supports `ThiefOnly`, `SelectedAndThief`, and `AllPlayers` (default). Wrong-vote and escape Thief exposure, Necklace exposure, and presentation acknowledgment are configurable.
- Results and snapshots include only roles allowed by policy. Public Thief and Necklace identities are omitted when their respective configuration does not reveal them. The trusted result retains the authoritative identity for scoring.

## Events and progression

The reveal event stream is ordered: `RevealStarted`, each authorized `RoleRevealed`, optional `ThiefRevealed`, optional `NecklaceRevealed`, `RevealOutcomeResolved`, then `RevealCompleted`. Duplicate completion returns the existing result and does not publish a second completion event. A new round resets the per-round reveal sequence.

By default, reveal completes without waiting on presentation. Set `RequirePresentationAck` to require `CompleteReveal()` before scoring; the room transport contract has a host-authorized acknowledgment, and the local visual runner has a four-second fallback if animation completion is lost. Reveal does not calculate scores or advance the match. The local reveal screen displays the selected target, public Thief/Necklace identity, and policy-authorized role card. Escape displays no selected target.

`SessionLifecycleRuntime.GetRevealSnapshot()` returns an authenticated, stable-room-ID projection after reveal preparation. `IRoomTransport` now defines the reveal event and reconnect snapshot seams; provider delivery remains the transport adapter's responsibility. Role projections are unavailable before the reveal transition.

## GW8 handoff

`RevealResult` provides the outcome, selected target, authoritative Thief identity, configured public role records, and Necklace state. GW8 scoring remains a separate phase and must use the authority-owned result and finalized GW5 vote collection; presentation animation does not change reveal or score truth.

## Verification scope

No automated tests or build were run as part of this implementation.
