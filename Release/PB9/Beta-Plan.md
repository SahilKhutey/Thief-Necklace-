# Closed Beta Plan

## Readiness

**Not ready to recruit.** The current project has no online service or installable candidate. Start the cohort only after the release gate's prerequisite build, auth, matchmaking, privacy, support, and incident-response checks pass in staging.

## Controlled cohorts

1. Internal team: validate instructions, instrumentation, and support flow.
2. Small invite-only external cohort: observe first-session clarity, fairness, and match reliability.
3. Expanded invited cohort: proceed only after cohort 2 has no unresolved blocker and service capacity is approved.

Keep the cohort reversible. Use expiring invitations, a named owner, a fixed test window, a support contact, and a clear stop procedure. Never use synthetic test accounts in production.

## Consent and data minimization

- Explain the beta purpose, collected event categories, retention period, and feedback contact before opt-in.
- Do not collect raw chat, private clues, secret roles before reveal, authentication tokens, or full device identifiers in analytics.
- Collect an optional report category and short survey ratings; use the separate support channel for volunteered diagnostics.
- Limit access to identifiable reports and define deletion/retention before invites are sent.

## Selected-match survey

Ask at most once after a selected match:

- Mystery clarity: 1–5
- Fairness: 1–5
- Suspense: 1–5
- Enjoyment: 1–5
- After the reveal, did the truth make sense from the clues you had? Yes / Partly / No
- As Thief, did you have a believable chance to escape? Yes / No / Not applicable
- As an innocent, did you have enough evidence to decide? Yes / No / Unsure
- Would you play again? Yes / No

Provide an optional skip and a separate feedback/report action. Do not interrupt every round.

## Analysis slices

Review technical reliability, match completion, queue outcomes, and reconnects; gameplay outcomes by 4/5/6 players and scenario; reveal coherence and fairness ratings; tutorial/lobby/vote confusion; match length, rematch intent, and return only for participants with appropriate consent. Report denominators and cohort sizes; do not treat small samples as stable balance targets.

## Exit review

For every scenario, classify feedback as too easy, too hard, confusing, under/over-evidenced, weak false lead, weak Thief agency, uninteresting, or pacing issue. Disable or revise scenarios with repeatable logic/fairness failures. Exit only when critical reliability/security failures are closed, findings have owners, and the release lead records a cohort decision.
