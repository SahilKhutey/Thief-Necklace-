# GW16 — Production Launch and Live Operations

## Release identity and compatibility

`BuildMetadata` identifies application, game/build, protocol, rules, scenario, source revision, and target. `VersionedMatchContract` pins build/protocol/rules/scenario versions for a match; compatibility is exact, so a client cannot silently join a match running different gameplay rules. Its seed is authority-private and is not part of client compatibility or analytics projections. Matches should retain this contract until completion.

`FrozenProductionConfiguration` copies runtime values and computes a stable SHA-256 digest for audit. Keys that indicate credentials are rejected; secrets must come from a secure provider. Feature flags use immutable definitions and deterministic percentage buckets from an anonymous key. Flags are rollout controls, not gameplay authority: they must not alter outcomes within an already-created match.

## Candidate and deployment controls

`ReleaseCandidateGate` rejects missing build provenance, candidate artifacts, regression/multiplayer/recovery/security/platform/performance evidence, frozen configuration, rollback capability, monitoring readiness, healthy gameplay integrity, or active P0/critical P1 incidents. It evaluates supplied evidence; it does not manufacture evidence or claim a build passed.

`ReleaseController` constrains candidate → deploying → live transitions. Deployment smoke must pass before marking a candidate live. A rollback requires a compatible target and a passing rollback smoke before it is complete. Maintenance and retirement are explicit states. `PostDeploymentValidation` records the operational 5-minute, 30-minute, and 1-hour checks in order. Hosts own the actual deployment, store submission, and monitoring schedule.

## Monitoring, analytics, privacy, and incidents

`ProductionHealth` validates bounded connection, reconnect, and crash rates and carries active incident count and gameplay-integrity status. Gameplay integrity remains a separate release blocker even when availability metrics look healthy.

`SafeAnalyticsEvent` accepts only an allowlisted event kind, bounded counts/durations, short build/platform tokens, and an enum result code. It has no fields for player IDs, chat, roles, vote targets, evidence, match seed, or arbitrary payloads. `SafeAnalyticsQueue` is bounded and nonblocking for producers; when full it drops the event and increments a counter. A host background worker drains it to an approved sink. Gameplay never waits for analytics delivery, and sink credentials/configuration remain outside this code.

`IncidentLedger` models safe category codes and explicit incident status transitions in memory. The production host must persist incidents in its approved operational system. Open P0 and critical P1 incidents block candidate validation. Incident records should not contain player content or hidden-role data.

## Hotfix, rollback, and regression loop

Operational sequence:

1. Record a privacy-safe incident and identify severity.
2. Reproduce using build/source/versioned-match metadata and authority-private replay/trace evidence where available.
3. Add a regression test and run the applicable GW14 gates.
4. Create and validate a new candidate using GW15 integration gates.
5. Deploy to staging, run smoke checks, then promote under the host release process.
6. Monitor post-deployment checkpoints. If integrity or critical health fails, stop rollout and use a compatible rollback target; rollback is complete only after its smoke passes.

Database migration, durable match history, leaderboard, crash reporting, alerts, app-store deployment, WebGL hosting, signing, and secret management require provider/operations adapters and are not implemented by these provider-neutral models.

## Current implementation status

The source now contains release-state, candidate-evidence, version compatibility, frozen configuration, feature-flag, safe analytics, health, incident, rollback, and post-deployment validation contracts. Edit-mode tests were added for these contracts but were not run in this change. No live service or release channel is configured, no build artifact is produced, and no deployment is authorized or claimed. Production stays locked until the actual host supplies and verifies the external evidence and infrastructure.
