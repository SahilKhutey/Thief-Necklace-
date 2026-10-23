# GW14 — QA, Multiplayer Validation, and Release Gates

## Purpose

GW14 defines reusable test-case/run/result contracts, defect severity and lifecycle, regression coverage, and an explicit release decision. It verifies gameplay, networking, recovery, replay, UX, security, performance, and platform work owned by earlier workflows; it does not replace those systems or claim their tests have passed.

## Implemented contracts

`QaTestCase` carries a stable ID, name, preconditions, ordered steps, expected result, severity, and one or more categories. `QaTestResult` captures status, elapsed milliseconds, failure text, bug ID, replay ID, and state hash. `QaTestRun` records build, platform, start time, and a detached result list. A run passes only when it has at least one result and every result is `Passed`; skipped, blocked, not-run, and failed cases do not pass.

`ReleaseGateEvaluator` blocks on a missing/mismatched run or sign-off, any non-passing result, open P0/P1 defects, an unvalidated build, or incomplete required player-count coverage. `ReleaseSignoff` requires approval from Gameplay, Networking, QA, UX, Performance, Security, and Platform for the same build version.

`DefectRecord` enforces the lifecycle `New → Triaged → Assigned → Fixed → Retest → Verified → Closed`. Reopening requires the defect to be verified and attaches both a replay ID and trace ID, preserving GW13 evidence for regression.

## Required regression matrix

| Area | Required coverage |
| --- | --- |
| Gameplay | Role uniqueness/privacy, scenario validity, discussion, chat, vote guards, tie/revote, reveal consistency, scoring ledger, round and match completion, rematch isolation |
| Network and recovery | Command idempotency, event ordering/duplication, reconnect in each major phase, vote/scoring recovery, authority change, snapshot and state-hash consistency |
| Security | Private-role projection, phase/identity/match guards, stale-match commands, score and replay tampering, release configuration |
| Determinism | Fixed seeds and rules/scenario versions, replay checkpoints, final authoritative state hash |
| UX/platform | Vote race and rehydration, accessibility, responsive layouts, WebGL/mobile suspension, error/recovery/result navigation |
| Performance/build | Platform-specific baselines, UI/network/memory measurements, scene/assets/configuration checks, target build validation |

Every test case uses a stable ID, preconditions, steps, expected outcome, and severity. Automated adapters should tag cases with `QaTestCategory` so CI can run the changed-system subset and the P0 regression set. The repository already has edit-mode coverage for core match progression, roles/private information, scenario determinism, voting, scoring, snapshots, and replay privacy. `QaReleaseGateTests` adds executable coverage for report contracts, release decisions, evidence-backed defect reopening, and detached input collections.

## Player-count and release status

The attached target matrix calls for 4, 5, 6, 7, and 8 players. The current canonical runtime enforces a 4–6 player limit and the role catalog/assignment path is sized for that supported range. Therefore, 7–8 player coverage is an explicit incomplete release requirement; the release gate must receive `RequiredPlayerCountMatrixComplete = false` until the gameplay configuration and runtime support are deliberately extended and validated. Do not report the full matrix as passing today.

This implementation adds the QA contracts and focused gate tests. It does not execute Unity tests, produce platform builds, run multiplayer/network fault simulations, establish performance baselines, or provide domain sign-offs. Those artifacts must come from the corresponding CI/device/release runs; a specification or dashboard mockup is not evidence of a pass.

## Test and release loops

- **Daily:** build, smoke test, P0 regression, changed-system cases, multiplayer smoke, capture and triage failures.
- **Nightly:** full edit/play-mode suites, 4–8-player integration where supported, network/recovery/replay suites, performance smoke, archive artifacts.
- **Release candidate:** clean build, P0 regression, multiplayer and recovery matrices, security/platform/performance validation, then matching domain sign-offs.

The release candidate is rejected for any P0 defect, failing required test, build failure, incomplete player-count matrix, missing required sign-off, or mismatched build evidence. Open P1 defects also block the evaluator and must be resolved before approval.

## Evidence captured for critical failures

At minimum, file a bug ID and retain build/platform, match and round IDs, phase, expected and actual outcomes, replay ID, event trace, state hash, and network profile where applicable. Avoid putting hidden role, truth, or private clue data into public logs and telemetry.
