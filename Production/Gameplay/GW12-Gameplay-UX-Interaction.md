# GW12 — Gameplay UX and Interaction

## Scope

GW12 turns the authoritative client projection into UI capabilities and routes player intent through local UX checks before command submission. It does not assign roles, resolve votes, calculate scores, decide phases, or decide whether a command is valid at the authority.

`Scripts/UI/GameplayUxRuntime.cs` provides the shared presentation contract:

- `GameplayUxMapper` derives the current UX state, timer text and accessibility announcement, player presentation state, and action availability from a caller supplied client projection.
- `GameplayActionGuard` rejects stale or invalid local actions before dispatch. The vote guard requires the Voting phase, an eligible connected non-self target, and no already-submitted vote. `GameplayActionDispatcher` reports pending state, suppresses duplicate in-flight mutation actions, and returns the command sender's acknowledgement. The remote authority remains the final validator.
- `InputLockService` stores named locks with scopes, so clearing a modal or transition lock cannot clear a recovery lock. Gameplay-only, chat-only, and all-input locks can coexist.
- `GameplayFeedbackBus`, `GameplayFeedbackVisualAdapter`, `ActionFeedback`, `IScreenErrorHandler`, and `GameplayErrorCopy` provide semantic feedback and player-facing error language without exposing transport codes. The adapter forwards semantic cues to the existing PV visual-event stream.

The mapper accepts a projection instead of a concrete transport state type. This keeps presentation independent of a backend and requires adapters to rebuild the UX model whenever GW10/11 provides a new authoritative projection. Private role and clue information must continue to flow only into owner-scoped private-role views.

## Existing visual layer integration

The UI Toolkit `ScreenManager` can bind an `IInputLockService`; it passes that guard to chat, vote, lobby, and private-role controls and keeps the global blocker for all-input locks. After acquiring or releasing a scoped lock, call `RefreshGameplayInput` so enabled states reflect the new lock set. Other gameplay controls can query `CanDispatchGameplayAction` before raising their command events. Recovery and modal owners must unlock only their own stable reason key.

The voting overlay now requires an explicit confirmation, prevents double submission, displays a pending state, and offers accepted/rejected callbacks. It excludes self, disallows unavailable targets, and closes the voting target controls when the authority reports voting closed. Call `ConfirmVoteLocked` only after authoritative acceptance and `RejectVoteSubmission` on rejection. The overlay does not decide or reveal the vote target to other players.

The chat overlay now displays sending/sent/failure feedback and preserves the message on send failure. Call `ConfirmChatMessageSent` or `RejectChatMessage` from the transport result. If no message handler is connected, the screen manager reports chat unavailable instead of showing a false success.

The game timer displays `mm:ss`, urgency styling, and a readable seconds-remaining label with rate-limited announcements. Reaching zero is presentation only; the timer cannot advance the gameplay phase.

## Integration contract

1. Build `GameplayUxInput` from the latest authoritative projection, including phase, own vote submission state, eligible public targets, timer, chat policy, recovery state, and GW9 progression eligibility.
2. Bind the mapped result to the screen and controls. Preserve selected vote target and submitted state when rebuilding after GW11 recovery.
3. On a player action, check `GameplayActionGuard` and the bound input locks, show pending feedback immediately, and dispatch through the GW10 command sender.
4. On authoritative acceptance, show success feedback and update from the accepted projection. On rejection, preserve recoverable input and show player-facing copy. Do not infer success from an animation or button press.
5. Keep result rankings, score totals, reveal facts, and phase transitions sourced from GW7–GW9 authoritative results.

The existing UXML/CSS foundations already supply responsive compact layouts, large-text/high-contrast modes, reduced motion, focus styling, and accessible labels. Continue using text and shape alongside color for states; do not add a hidden-role suspicion meter or communicate hidden state through color.

## Verification status

This pass adds the reusable UX runtime and integrates vote confirmation, pending/rejected vote feedback, chat send feedback, timer urgency, and stacked lock hooks into the existing UI Toolkit screens. Backend-specific wiring from real GW10 command responses, concrete recovery lock lifecycle, and screen-reader announcements remain the responsibility of the gameplay host adapter. No tests or Unity build were run for this change.
