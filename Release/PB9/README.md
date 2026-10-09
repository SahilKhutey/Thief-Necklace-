# PB9 Release Evidence Pack

This folder is the working release-control package for Thief & the Necklace. It turns the PB9 specification into records that can be completed during RC, QA, beta, platform validation, and launch review.

## Current disposition: NO-GO / not an RC

The checked-in project is an offline pass-and-play prototype with scenario and backend foundations. It is not the PB9 launch candidate described in the production brief. No release build has been produced, no Unity compile/regression run has been recorded, and no connected identity, matchmaking, authoritative backend, profile, moderation, analytics, monitoring, or release infrastructure exists here. Accordingly, this pack records blockers and unrun evidence rather than inferring passes from source code.

## Records

- `RC-Manifest.template.json` — identifiers and artifact hashes required for each immutable candidate.
- `Feature-Inventory.csv` — implementation state separated from validation/acceptance state.
- `Requirements-Traceability.csv` — requirement to source/test/evidence mapping.
- `Validation-Matrix.csv` — final test areas, evidence, and release blockers.
- `Beta-Plan.md` — invite cohorts, privacy-conscious feedback, metrics, exit review.
- `Operations-Drills.md` — failure, restore, rollback, and incident rehearsal records.
- `Go-No-Go.md` — blocking gate and required decision sign-offs.
- `Known-Issues.md` — current project limitations suitable for release tracking.

## How to use

1. Keep this evidence pack with the source tag and immutable build artifact for every candidate (RC1, RC2, LC1).
2. Copy the manifest template for each build. Never overwrite a candidate's versions or checksums after signing it.
3. Record evidence links, run IDs, tester/device details, dates, and owner for each result. Use `NOT RUN`, `BLOCKED`, or `FAIL` until evidence supports `PASS`.
4. A release lead updates the go/no-go record only after all blocking gates have evidence and named sign-off.

An empty template is not evidence. A source-level implementation is not a pass for a live service, platform, security, accessibility, or beta gate.
