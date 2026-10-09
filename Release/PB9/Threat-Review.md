# PB9 Release Threat Review

This is a review checklist, not a penetration-test result. A qualified reviewer must evaluate the deployed client and authoritative services before closed beta or launch.

| Protected asset | Threat scenario | Required control / evidence | Current state |
|---|---|---|---|
| Player identity/session | Token theft, spoofed identity, session replay | Provider auth review, short-lived credential handling, revocation/session policy, redacted logs | NOT IMPLEMENTED / NOT TESTED |
| Role and private clues | Modified client inspects payload/cache/logs before reveal | Server-side recipient authorization; capture and inspect network/cache/crash telemetry | No online client; NOT TESTED |
| World truth and scenario seed | Client infers hidden truth from deterministic seed or content bundles | Keep secret generation inputs server-side or prove safe recipient payloads; secrecy review | NOT TESTED |
| Vote | Duplicate, self, changed, or late vote | Authoritative phase/roster validation and idempotency; adversarial integration cases | Contracts only; NOT TESTED |
| Match result and score | Client forges thief, accused player, or score | Server computes result and role score; atomic match ID persistence; replay/conflict tests | Provider absent |
| Rewards and profile | Replay, cross-account access, concurrent writes, cosmetic unlock forgery | Auth-bound ownership, server reward ledger, expected-version writes, concurrency tests | Provider absent |
| Moderation/reporting | Spam, false-report abuse, harassment, mute/block bypass | Server report throttles, audit trail, response process, privacy controls | Contract only |
| Admin/config/content | Unauthorized production changes or leaked secrets | RBAC, MFA, audited change approvals, environment separation, secret manager, kill switch | Not configured |
| Analytics/crash data | Hidden role/chat/token leaks | Allowlisted schemas and sample-payload/log review for each production path | Sink absent |

## Required adversarial cases

Attempt role extraction, vote replay/manipulation, score/result forgery, reward replay, cross-account profile/history/loadout reads and writes, protocol mismatch, malformed/oversized payloads, rate-limit bypass, admin endpoint access, and report flooding. Record exact candidate, service revision, steps, evidence, severity, remediation, and retest. Do not record sensitive live credentials in the evidence bundle.
