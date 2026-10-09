# PB10 — Live 1.0 Operations Foundation

## Implemented in this workspace

`Assets/ThiefAndTheNecklace/Scripts/Live` adds an engine-independent content operations model around the existing scenario generator:

- Versioned live content packages with required SHA-256 package hash, scenario lifecycle state, bounded scenario weights, fallback scenario, season schedules, and event windows.
- A content validator that requires a nonempty eligible scenario pool, complete presentation/localization/analytics markers for live scenarios, validates scenario logic for 4/5/6 players using the existing scenario validator, and checks unique IDs and non-overlapping UTC seasons.
- A stage → activate → rollback-for-new-matches control flow. Rollback uses a new monotonic configuration version while referring to the prior content version.
- Immutable match pins containing content/config versions, package hash, selected scenario/version, season, and active event IDs. Match pins are created once from injected trusted UTC time, so an update does not change the identifiers already recorded for a match.
- Deterministic weighted scenario selection from the pinned active content pool, with stable ID ordering and an authored fallback.

The package does not change role assignment, clues, voting, or scoring. It is a control-plane seam, not a live publishing console or connected service.

## Deliberately not enabled

PB9 currently records NO-GO. The repository has no live backend, authentication, server time source, staging/canary deploy, persistent content archive, analytics sink, monitoring, or production client. Therefore this implementation does not publish content online, schedule a real season, issue event rewards, run experiments, alter live matchmaking, or enable social/ranked modes. The in-memory service must not be treated as authoritative in a shipped multiplayer game.

Friends/presence, moderation case management, competitive rating, ranked matchmaking, paid economy, seasonal progression, A/B assignment, and live dashboards remain future provider-backed product work. Ranked stays disabled until the PB10/PB9 gates in the release pack pass.

## Production adapter requirements

1. Store immutable content package artifacts by version and checksum, with signed/approved provenance.
2. Run validation server-side, including actual asset/localization references and analytics schema registration.
3. Publish staged configuration through an authenticated admin workflow with RBAC, audit events, approvals, and a measurable canary cohort.
4. Pin the resulting identifiers in authoritative match state and retain old scenario/config artifacts for support and replay.
5. Make rollback affect only new matches; an active match finishes with its original pin.
6. Use trusted server UTC, not client/device clocks. Define outage behavior and restore procedures before enabling schedules.

No tests or Unity compile were run for PB10. The Unity Editor is unavailable in this environment; provider integration and runtime validation remain outstanding.
