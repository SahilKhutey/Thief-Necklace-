# Current Known Issues / Release Blockers

This list describes the current workspace state. Recheck against each candidate and replace with player-facing release wording before any beta or store submission.

| ID | Issue | Impact | Current disposition |
|---|---|---|---|
| PB9-001 | Game is offline-only; no real authentication, matchmaking, network session, or reconnect flow | Fresh players cannot play online together | Release blocker |
| PB9-002 | PB8 backend types are contracts/guardrails without provider adapters or production authority | Cloud profiles, authoritative results/rewards, moderation, and telemetry are not available | Release blocker |
| PB9-003 | Unity Editor build/regression has not been recorded in this environment | Compile and runtime status are unknown | Release blocker |
| PB9-004 | No immutable release candidate, signed artifacts, CI release workflow, or artifact archive | Candidate cannot be reproduced or certified | Release blocker |
| PB9-005 | Production illustration/audio/VFX and platform certification evidence are not established | Presentation and store readiness remain incomplete | Release blocker |
| PB9-006 | No external beta cohort, service dashboard, alerting, backup/restore, or rollback evidence | Live reliability cannot be evaluated | Release blocker |
| PB9-007 | Existing source-level tests exist but no PB9 test run evidence is recorded | Historical behavior remains unverified in a Unity editor | Open; run on candidate |
| PB10-001 | Live content operations are in-memory only; no signed package store, staging/canary publisher, remote clock, or server-authoritative pin persistence | Cannot safely publish or roll back content for real online matches | Release blocker |
| PB10-002 | Seasons/events have data contracts but no backend scheduler or reward delivery | No live event/season can be activated for players | Not implemented |
| PB10-003 | Friends, presence, competitive rating, and ranked mode are not implemented or enabled | No live social/competitive service | Deferred until online launch gates pass |
