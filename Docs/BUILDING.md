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
5. For future online releases, add provider-backed integration, multiplayer, security, privacy, accessibility, performance, soak/load, rollback, and device matrix evidence.

No test result should be marked PASS until tied to a build ID, source revision, platform/environment, run ID, and evidence location. See the finalization checklist and PB9 release gate. No Unity build or tests were run while preparing this repository update.
