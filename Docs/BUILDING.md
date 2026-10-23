# Build and Validate

## Environment

- Unity Editor: `6000.3.0f1` (`ProjectSettings/ProjectVersion.txt`).
- Package manifest currently lists Unity Test Framework `1.4.6` only.
- There is no connected Unity Cloud project, NGO/Relay package, online backend, or production credentials in this project.

## Open and run

Open the repository root as the Unity project through Unity Hub. Allow import/package resolution to finish and inspect Console diagnostics. The project uses an editor initialization path to create a Bootstrap scene/default scenario assets on first open; verify those generated assets before pressing Play. From the main menu, choose 4, 5, or 6 local players and set a match seed (10001 by default) to reproduce the same five-round scenario sequence.

## Validation order

1. Fresh import and compile with the recorded Unity version.
2. Edit Mode suites under `Assets/ThiefAndTheNecklace/Tests/EditMode`, including `ProductionMatchCoreTests`, `RuntimeIntegrationTests`, and `PB1FinalAcceptanceTests` for PB1.2/PB1.3/PB1-FINAL.
3. Offline fresh-session gameplay: scenario intro → private handoff → discussion → vote/runoff → reveal → score → five-round result.
4. Scenario validation across 4/5/6 players and Easy/Normal/Hard, including seed replay and fallback behavior.
5. Inspect GW13 replay/trace artifacts only through the development tooling; public replay records redact event payloads and seeds.
6. Before a production package, compose real provider adapters and run `ProductionIntegrationGate` with the release manifest. It requires the declared Windows, Android, iOS, and WebGL target matrix and rejects debug/fault-injection features or unsupported player counts.
7. Run provider-backed multiplayer, security/privacy, accessibility, performance, soak/load, rollback, and device-matrix validation on the release candidate.

No test result should be marked PASS until tied to a build ID, source revision, platform/environment, run ID, and evidence location. See the finalization checklist, GW14 QA release gate, and PB9 release gate. The provider integration gate and replay/readiness tests have been added to the edit-mode suite, but were not run in this repository update. No provider-backed package or external service connection is configured by these source changes.

GW15 composition and GW16 release-operation contracts are also covered by edit-mode test sources. `ReleaseCandidateGate` consumes actual candidate evidence; it does not perform or certify a deployment. `SafeAnalyticsQueue` requires a host-owned background drain and approved sink. Review `Production/Gameplay/GW15-Production-Integration-Packaging.md` and `Production/Gameplay/GW16-Production-Live-Operations.md` before connecting providers or opening a release channel.

GW17's stable test catalog is `Assets/ThiefAndTheNecklace/Tests/GW17-Test-Catalog.json`; specified cases must not be reported as executed until test-run artifacts exist. GW18's final approval model and current release blockers are documented in `Production/Gameplay/GW18-Final-Production-Release-Gate.md`. `FinalReleaseGate` requires candidate-revision evidence for every quality domain, all 4–8 player-count runs, Android/iOS/WebGL validation, critical verification checks, and matching sign-offs. The current runtime supports 4–6 players and no target build/test artifacts are present, so a production release remains blocked.
