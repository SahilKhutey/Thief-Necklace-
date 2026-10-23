# GW17 — Unit Test Architecture and Verification

GW17 makes the existing EditMode suite traceable to the workflow's stable test IDs and defines the rules for extending and running it. Test definitions and code are not evidence of a passing run: execution results belong in CI artifacts and the release record.

## Current test layout

The Unity Test Framework NUnit assembly is `Assets/ThiefAndTheNecklace/Tests/EditMode/PB1.EditModeTests.asmdef`. EditMode tests exercise domain and runtime contracts without launching scenes or real networking. PlayMode/UI/scene tests should live in a separate assembly and be added when those runtime surfaces are present; do not pretend EditMode coverage proves them.

Existing suites include core gameplay, match lifecycle, scenario generation, networking, runtime integration, QA/release gates, runtime composition, production integration, and live operations. They are the starting implementation, not a claim that every GW17 matrix row is covered. The machine-readable catalog at `Assets/ThiefAndTheNecklace/Tests/GW17-Test-Catalog.json` records each source test ID and its current status. Rows marked `specified` need a concrete automated mapping; rows marked `mapped` need an audit against their named test. `blocked` means the current product contract does not support that case. In particular, the current match runtime supports 4–6 players; 7–8 player cases must remain blocked until implementation supports those counts.

## Conventions

- Test names use `Method_Condition_ExpectedOutcome` where practical. Keep stable GW17 IDs in NUnit descriptions or the catalog mapping; do not renumber IDs to match file layout.
- Put deterministic domain/service tests in EditMode. Use PlayMode only for scene, UI Toolkit, frame/timer, and actual Unity lifecycle behavior. Network contract tests use fakes unless testing the transport itself.
- Give every case a priority (`P0` release-blocking integrity/security, `P1` required gameplay/runtime contract, `P2` important supporting behavior, `P3` non-blocking diagnostics), expected outcome, automation status, and test reference or explicit blocker.
- Keep test builders, fixtures, fakes, golden data, and assertions under `Tests/Shared`. Golden records include rules/scenario versions and expected hashes. Update them intentionally alongside the behavior change.
- Capture test ID, class/method, build and source revision, expected/actual values, and stack trace on failure. Preserve relevant state snapshot/replay artifacts for deterministic debugging; redact private player data from shared artifacts.

## Coverage and release policy

Track code coverage together with rule, state, and transition coverage. Prioritize role uniqueness, private-data projection, vote validation/tally/tie behavior, authoritative reveal, score ledger integrity, match completion, command idempotency, recovery, deterministic replay, serialization, and configuration validation. Mutation checks should target the thief count, winner/tie comparisons, scoring/rewards, completion, and private-data filtering.

The release gate is evidence-driven: P0 and P1 tests, critical security, determinism, serialization, replay, configuration, and regression review must all have passing results from the target revision. Any failed or unexecuted required case blocks sign-off. This document and the catalog define coverage; neither substitutes for a Unity Test Runner/CI report. No test execution is recorded by this implementation task.

## CI integration

Run the EditMode suite through Unity Test Framework in CI with the repository's pinned Unity editor version. Preserve NUnit XML and Unity logs as build artifacts, fail the job on failed or missing required P0/P1 tests, and keep PlayMode results as a separate gate. Do not introduce a workflow that silently uses an unpinned editor or converts missing license/editor configuration into a passing result.

## Extension workflow

For every production defect, add or identify a regression case with a stable ID, reproduce it deterministically, fix it, and rerun the relevant suite. Keep test changes alongside the behavior change. A unit-test pass is necessary but does not replace integration, multiplayer, platform, or release verification.
