# PB9 Go / No-Go Record

## Current decision

**NO-GO — project is not a release candidate.** This is a status record based on the current workspace, not a launch approval.

## Blocking gates

- [ ] Unity project imports, compiles, and builds on a fresh supported editor installation.
- [ ] Full regression passes on the exact candidate revision.
- [ ] A deterministic six-player, five-round golden match passes with recorded expected results.
- [ ] Real online authentication, matchmaking, authoritative match, profile, reward, and reconnect flows pass in staging.
- [ ] Private role/clue/world truth isolation is verified against modified clients and captured network/log/telemetry payloads.
- [ ] Production art, audio, VFX, localization, store metadata, and target platform artifacts are complete or explicitly scoped out of the release.
- [ ] Security, accessibility, compatibility, performance, device, load, and soak reviews have evidence and owners.
- [ ] Closed beta completes with privacy review, feedback analysis, and no unresolved launch-blocking issues.
- [ ] Monitoring, alerting, incident response, service outage, backup/restore, rollback, and config/content kill-switch drills pass.
- [ ] Known issues, player support, privacy/data retention, release notes, and platform submission records are approved.

## Decision rule

Any unresolved severity-0 or severity-1 issue, any missing critical evidence above, any unverified result/reward integrity path, or any unmitigated secret exposure is **NO-GO**. Accepted nonblocking risks must identify impact, mitigation, owner, and expiry/review date.

## Candidate record

| Field | Value |
|---|---|
| Candidate / version / build ID | Unassigned |
| Source revision / artifact checksum | Unassigned |
| Target platforms | Unassigned |
| QA lead and evidence bundle | Pending |
| Security reviewer and report | Pending |
| Product / game director decision | Pending |
| Decision date (UTC) | Pending |

Do not change this record to GO until every blocking gate is evidenced against the same immutable candidate artifact.
