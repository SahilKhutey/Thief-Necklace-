# GW18 — Final Production Release Gate

GW18 closes the production workflow by evaluating evidence from GW0–GW17. It adds no gameplay rules. A feature being defined or implemented does not count as tested, validated, or release-ready.

## Decision and lifecycle

`FinalReleaseGate.Evaluate` returns `Approved` only when all required evidence is present, passing, tied to the candidate source revision, and signed off. Any absent or failed requirement returns `Blocked` with explicit blockers. `FinalReleaseLifecycle` enforces development → integration → validation → candidate → approval/freeze → release, with a blocked/revalidation path and rollback terminal state. A `ProductionBaseline` can only be created from an approved report. `FinalReleaseHandoff` requires the baseline, configuration digest, QA and test reports, release notes, known issues, rollback reference, and build artifacts.

The gate's quality domains are Gameplay, Networking, Recovery, Privacy/Security, UX, Accessibility, Performance, Replay, Diagnostics, Platform, Build, Operations, and Documentation. Every domain must have exactly one evidence item for the same source revision and a report/artifact reference. `Fail` and `Blocked` always block; `NotApplicable` requires a documented rationale. The release profile should use `NotApplicable` only when that domain truly falls outside the candidate's scope.

## Required evidence

- Zero P0 and critical P1 defects; healthy gameplay integrity.
- Passing GW17 P0/P1, security, determinism, serialization, replay, and configuration checks.
- Passing offline, online, full five-round, recovery, rematch, and score-ledger reconciliation checks.
- Production mutation/debug tools disabled; production configuration frozen; telemetry privacy reviewed.
- Verified candidate artifact, verified rollback reference, complete handoff documentation.
- Validated 4-, 5-, 6-, 7-, and 8-player runs, plus Android, iOS, and WebGL evidence.
- One approved sign-off, on the candidate revision, from each of Game Director, Gameplay Lead, Lead Programmer, Networking Lead, UI/UX Lead, QA Lead, and Technical/Build Lead.

These are reported inputs to the release gate, not claims that this repository has produced those artifacts. The gate does not infer successful platform builds from source configuration or successful multiplayer from unit tests.

## Current release blockers

The runtime readiness policy currently supports 4–6 players. GW18 requires verified 7- and 8-player runs, so it cannot approve the present product. Platform builds and real-device/browser validation also require target tooling and artifacts. No such validation results or approval signatures are supplied by this change. The final release status therefore remains **Blocked pending evidence and product support**, not Approved.

## Score integrity and production invariants

Displayed score, authoritative score, and score-ledger result must agree. Release is blocked by an integrity mismatch, an unrecoverable critical state, private-information leak, duplicate recovery transition, incompatible configuration, missing platform artifact, or any P0/critical P1 issue. Production clients must not control roles, votes, outcomes, scores, reveals, progression, or another player's private data.

## Freeze and handoff

Approval freezes gameplay rules, network and recovery contracts, score rules, protocol, configuration, and release artifact to a source revision. Later changes require a patch, hotfix, or next-version baseline. The handoff record binds game version/build number, source revision, protocol/rules/scenario versions, configuration digest, target artifacts, QA/test results, release notes, known issues, and rollback reference. Post-release health review follows the GW16 monitoring/incident process; production defects return through GW13 diagnosis, GW17 regression, GW14 QA, and GW15 integration before reevaluation.

## Final decision rule

All critical gates pass → approved baseline and handoff. Any failed, missing, stale, or unsigned critical evidence → blocked; diagnose, fix, retest, reintegrate, and reevaluate. The checklist is an operational record and must be completed from actual run artifacts; source code alone cannot check it off.
