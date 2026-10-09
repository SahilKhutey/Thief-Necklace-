# PB8 — Backend, Matchmaking & Production Boundaries

## Implemented foundation

`Assets/ThiefAndTheNecklace/Scripts/Backend` is an engine-independent assembly containing:

- Explicit Development / Staging / Production environment selection and version handshake validation. Production configuration forcibly disables debug tools and synthetic accounts.
- Validated, versioned remote configuration with last-known-good retention, duplicate flag/ID checks, bounded timings/rewards, and defensive copies.
- Matchmaking request/roster contracts, authenticated identity checks, and an explicit unavailable service that never invents tickets or players.
- An authenticated profile gateway with per-account ownership checks and optimistic profile-version writes. The repository's PB7 local profile remains local; this is the seam for a real provider adapter.
- Server-authority match finalization: it fetches completed match data from a trusted authority, validates the five-round scoring and roster, hashes a stable canonical result, and delegates atomic idempotent persistence to a store.
- An allowlisted telemetry record and bounded acknowledged queue. It contains no fields for chat text, private clues, hidden roles, or the secret event timeline. Player report contracts likewise have no free-text field.
- Bounded display-name/input validation, process-local rate-limit helper, local mute/block affordance, moderation contract, and provider-neutral circuit-breaker state helper.
- Redacted security-audit and dependency-health data contracts for provider monitoring.

## Important deployment boundary

This project still has no authenticated online client, backend host, database, matchmaking provider, cloud profile adapter, moderation provider, analytics sink, secret management, environment credentials, CI/CD pipeline, production telemetry exporter, or live service health checks. No client secret or environment credential has been added. The offline PB1 UI remains the only playable flow; PB8 does not claim online matchmaking, cloud persistence, or server-side enforcement.

The interfaces deliberately require trusted implementations. In particular, a client must never be allowed to submit a match result or XP grant as authoritative data. The production result store must implement atomic insert-if-absent on `matchId`; repeat submissions with the same canonical hash must return success, while a different hash for an existing match must be a conflict. The server must derive rewards from its own completed match state and authenticated roster.

## Provider integration still required

1. Select and configure the backend and identity provider separately for dev, staging, and production.
2. Add verified Unity packages and implement auth/session, matchmaking, authoritative match, profile, moderation, telemetry, config, and result-store adapters.
3. Keep authorization, rate limits, result validation, reward grants, report throttling, and audit logs on the server. Client-side checks are usability guardrails only.
4. Define retention/deletion and consent behavior for account, report, and analytics data; configure operational dashboards, alerts, incident response, and service-health checks.
5. Add secured CI builds, signing/secrets management, staged deployment, canary checks, rollback, compatibility policy, load/security testing, and provider-backed integration tests.

No Unity Editor compile or service integration was available in this environment. This document records implementation scope, not a production-readiness certification.
