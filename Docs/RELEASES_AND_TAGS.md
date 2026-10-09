# Releases and Git Tags

## Tag convention

- Development snapshots: `dev-YYYYMMDD-N` or a descriptive `pbN-foundation` tag. They are not release approvals.
- Release candidates: `vX.Y.Z-rc.N`, only after the candidate manifest identifies immutable source/artifact hashes.
- Launch candidates: `vX.Y.Z-rc.N` promoted by evidence/sign-off; do not rewrite existing tags.
- Public releases: `vX.Y.Z`, only after the PB9 Go/No-Go record is GO for the exact artifact.

Use annotated tags with a message that records source revision, build ID, platform(s), content/config/protocol/profile versions, and evidence-pack location. Never tag an unverified build as `v1.0.0` or `latest`.

## Candidate evidence

Copy `Release/PB9/RC-Manifest.template.json`; replace all `UNASSIGNED` values, archive artifact checksums/symbols, complete `Production/Finalization/Final-Checklist.csv`, and collect the required sign-offs. Tags identify source; they do not replace artifact signatures, checksums, or platform submission records.

Current workspace status: no release tag is authorized; PB9 remains NO-GO.
