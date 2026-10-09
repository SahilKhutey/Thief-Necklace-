# PB9 Operational Drill Record

Use staging and disposable test data. Do not perform destructive drills against production. Record candidate, environment, time (UTC), operator, observer, evidence, recovery time, data-integrity result, and follow-up issue IDs for every rehearsal.

| Drill | Procedure and expected recovery | Current result |
|---|---|---|
| Match completion failure | Alert → identify affected build → preserve authoritative match state → disable affected path or rollback → verify a fresh match | NOT RUN — no staging service |
| Reward service outage | Preserve match result; queue/retry reward idempotently; show clear pending state; verify one reward after recovery | NOT RUN — no reward service |
| Matchmaking outage | Return bounded error with retry/private-game path where available; no endless spinner or ghost ticket | NOT RUN — no matchmaking service |
| Profile outage | Keep match result safe; communicate unavailable profile; reconcile on recovery without overwriting newer version | NOT RUN — no cloud profile |
| Analytics outage | Continue gameplay; retain bounded queue or drop by policy; avoid blocking core loop | NOT RUN — no telemetry sink |
| Database failover | Recover transaction consistency for profiles, match results, and reward ledger | NOT RUN — no database |
| Backup and restore | Restore staging snapshot; verify profile, transaction, and match-record integrity | NOT RUN — no backend storage |
| Candidate rollback | Roll back staged RC2 to RC1; verify compatibility and match continuity policy | NOT RUN — no deploy pipeline |
| Config rollback | Revert a deliberately bad staging config to last-known-good; confirm version and client behavior | NOT RUN — no config provider |
| Content kill switch | Disable scenario for new rounds; preserve versioned content for already-started rounds | NOT RUN — no remote config service |
| Incident communications | Run tabletop: owner, severity, player notice, support response, status cadence, postmortem | NOT RUN |

## Drill evidence template

```text
Drill ID:
Candidate / source revision:
Environment:
Start / recovery time (UTC):
Operator / observer:
Expected behavior:
Observed behavior:
Match/profile/reward integrity verified by:
Player-facing impact:
Evidence location:
Follow-up issue IDs:
Result: NOT RUN / PASS / FAIL
```
