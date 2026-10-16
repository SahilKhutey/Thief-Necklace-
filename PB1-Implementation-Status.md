# PB1 core mechanics implementation

This note maps the PB1 pasted build brief to the current Unity project. Unity Editor compilation and Edit Mode execution are not claimed; see [Build and validation](Docs/BUILDING.md).

## PB1.2 deduction and scenario truth

PB1.2 adds the structured deduction layer in `Assets/ThiefAndTheNecklace/Scripts/Core/PB1/`: facts and stable fact keys, discrete timeline events, evidence and an evidence graph, player knowledge bundles, opportunity windows, temporal and fact contradiction checks, claim/retraction history, a deep-copy `RoundTruth` snapshot, deterministic authored scenario generation, and structural/critical-event validation. `GameController.StartLocalMatch` and each round setup now validate the generated model before entering private reveal; failures throw and do not start that round. The default Royal Necklace truth has two independent evidence paths for each critical theft event.

The controller records scenario, claim, contradiction, and opportunity signal hooks without writing player statements, roles, or private clues into the event log. Claims remain discussion records and do not mutate the stored truth snapshot. The local core exposes private evidence/fact IDs only through the owner-checked private view.

## PB1.3 deterministic runtime

`RuntimeStateModels.cs` adds canonical match/player/round/scenario/vote/score state, difficulty profiles, and round context/seed derivation. `MatchController.cs` is the PB1.3 command authority for start, discussion, voting, runoff, reveal completion, scoring, and five-round completion. Runtime vote/resolution/scoring services, a transition guard, invariant validator, validated round factory, snapshot capture, replay metadata, and a public-command test driver are in the same core assembly. The PB1.1 `GameController` and `GameState` remain for compatibility while PB1.4 connects a presentation layer to the canonical runtime.

Easy/Normal/Hard alter evidence load and add authored ambiguous evidence/false leads; they do not change vote or scoring rules. Round snapshots and replay records omit role and private-clue data. The scenario truth accessor returns deep copies, and validated `ScenarioState` cannot be modified through its runtime API.

## PB1-FINAL canonical runtime

`CoreGameRuntime.cs` provides the locked production command surface (`StartMatch`, discussion, voting, reveal, scoring, and round completion) as a facade over the tested PB1 controller. It also exposes owner-checked player views, safe public views, detached snapshots/replay data, and structured core log events. Structured logs contain event identifiers and match/round metadata only; they deliberately omit role assignments, private clues, and scenario truth. `PB1FinalAcceptanceTests.cs` exercises a five-round match through this API, checks same-seed state reproduction, and checks log/replay privacy.

## Implemented in the repository

| PB1 requirement | Implementation |
|---|---|
| Role, phase, vote-result, and truth enums | `Assets/ThiefAndTheNecklace/Scripts/Core/PB1/GameEnums.cs` |
| Player runtime, scenario truth/timeline, public/private view, match state | `Scripts/Core/PB1/PlayerRuntimeData.cs`, `ScenarioDefinitionData.cs`, `ScenarioEvent.cs`, `PlayerScenarioInfo.cs`, `PlayerViewData.cs`, `PrivatePlayerViewData.cs`, `GameState.cs` |
| Deterministic role allocation | `RoleAssignmentService.cs`, using the project’s cross-version `SeededRandomService` |
| Scenario private-information validation/assignment | `ScenarioService.cs`, with a Royal Necklace sample in `DefaultScenarioFactory.cs` |
| Ballot validation and one runoff; second tie escapes | `VoteService.cs`, `VoteResolutionService.cs`, and `GameController.cs` |
| Locked scoring and one-time score commit | `ScoreService.cs` |
| Legal phase graph and match progression | `RoundManager.cs`, `GameController.cs` |
| 180-second discussion clock with pause/resume and early end | `DiscussionTimer.cs`, `GameController.cs` |
| Replaceable vote presentation contract | `IVotingView.cs` |
| Unity role/scenario assets, timer, reveal delay, local bootstrap | `Scripts/PB1Unity/RoleDefinition.cs`, `ScenarioDefinition.cs`, `GameTimer.cs`, `RevealController.cs`, `LocalGameBootstrap.cs` |
| PB1 asset setup | `Editor/PB1ProjectSetup.cs` creates a sample scenario and six court-role assets on Unity Editor import |
| Mechanics and edge-case test sources | `Tests/EditMode/ProductionMatchCoreTests.cs`, alongside the existing `CoreGameplayTests.cs` and `ScenarioEngineTests.cs` |
| PB1.2 truth/evidence and validation tests | `Tests/EditMode/ProductionMatchCoreTests.cs` |
| PB1.3 state, deterministic runtime, snapshot/replay, difficulty, and full-match tests | `Tests/EditMode/RuntimeIntegrationTests.cs` |
| PB1-FINAL canonical API, five-round acceptance, deterministic state, and log/replay privacy | `Tests/EditMode/PB1FinalAcceptanceTests.cs` |

The existing `OfflinePrototypeApp` remains the project’s visible menu and pass-and-play implementation. Its state and screens already cover private handoff, free discussion, ballot handoff, runoff, reveal, score, profile, and five-round results. The PB1 `GameController` is a separately testable engine-independent implementation of the locked mechanics, not yet the presentation backend for that older UI.

## Role-rule interpretation

P2 and the existing scenario generator define each participant as a unique court role plus a separate Thief objective. PB1’s `RoleType.Thief` entry remains in the enum vocabulary, but role assignment never deals it as a court role. One participant receives the objective flag and corresponding private situation/clue. This preserves the established role pool for 4–6 players and keeps role score values attached to the court role.

## Verification boundary

Source-level checks found balanced delimiters across C# files, valid assembly-definition JSON, and no `git diff --check` errors. No Unity Editor executable or .NET SDK is available in this environment, so the project has not been compiled and the Edit Mode tests have not been run. Unity-generated scene/assets and visual behavior must be confirmed by opening the project in the specified Editor.
