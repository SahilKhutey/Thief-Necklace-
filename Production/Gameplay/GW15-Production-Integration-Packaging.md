# GW15 — Production Integration and Packaging

## Integration boundary

GW15 assembles the provider-neutral gameplay, backend, networking, recovery, and presentation systems into a release composition. `ProductionIntegrationManifest` requires non-secret build provenance and a frozen Production config snapshot, while `ProductionServiceBindings` receives concrete authentication, transport, matchmaking/session, profile persistence, and health adapters from the application host. `ProductionIntegrationGate` rejects incomplete composition, placeholder application identity, invalid versions, debug/fault-injection switches in a release manifest, incomplete target matrix, and unsupported room capacity. `ProductionRuntimeServices.Compose` is the guarded composition root; it creates the network manager and authenticated profile gateway only after every production gate passes.

The gate requires the current target matrix: Windows development/QA; Android development/QA/release; iOS QA/release; WebGL development/QA/release. It validates that targets are declared; Unity modules and actual artifacts still need to be built on appropriately licensed hosts. Credentials and service secrets belong in the provider's secure configuration, never in this manifest or source control.

`GameCompositionRoot` is the single owner for a match-scoped PB1 runtime. It registers the canonical `CoreGameRuntime`, an `IGameApplication` facade, a typed `IGameEventBus`, and a safe event bridge in an explicitly disposable service registry. `ProductionRuntimeServices.CreateGameplay` creates that composition only after the host has a validated roster/configuration and scenario generator. The facade forwards to existing authoritative commands; it does not create a parallel rules implementation. Event-bus listener exceptions are isolated and counted, and the core event bridge publishes metadata only, never private event payloads.

`RuntimeHealthGate` checks named mandatory services, `MatchReadinessGate` checks connection, room, configuration, authority, service health, and the implemented player range, and `BuildMetadata.Validate` requires provenance (application/game/build/protocol/rules/scenario/source/target). These are host-callable gates: a platform host still has to provide live health results and metadata. The manifest and metadata are deliberately credential-free.

The gameplay composition accepts `IGameClock`, `IRandomSource`, and host `IGameCompositionModule` dependencies. Production modules register long-lived provider services as borrowed so a match teardown cannot dispose application-wide network/auth services; match-owned services use the registry's owning registration. The production default uses UTC and a seeded xorshift source; edit-mode callers can inject `ManualGameClock` and a fixed seed. The clock drives discussion, voting, reveal, results, core logs, and match traces. `GameFlowCoordinator` forwards lifecycle calls and gates phase transitions against the canonical runtime. `OperationScope` owns scene/session async work and cancels it in reverse registration order during teardown, continuing if one cancel callback fails. `IScreenRouter`, `IRuntimeAssetProvider`/lease, and `IObjectPool` are provider-neutral scene, Addressables, and pooling seams because the current Unity package manifest does not include Addressables or a network provider. `RuntimeErrorBoundary` forwards only safe error categories and exception types to diagnostics.

`BuildProfileSettings.For` supplies development, QA, and release toggle policy. Environment configuration supports Development, QA, Staging, and Production; release configuration must still be injected by the build/bootstrap host and must not contain secrets.

## Production checks

- Supply production `EnvironmentConfig` and valid game/content/profile/protocol versions.
- Bind authenticated identity, network transport, session/matchmaking, profile storage, and backend health to real provider adapters.
- Configure a real application ID and all required build targets.
- Disable mutation tools, developer inspector, synthetic accounts, and fault injection in release configuration.
- Keep room size within the currently implemented 4–6 range. The separate 4–8-player design goal is not shipped by this codebase.
- Run Unity compilation, edit-mode tests, gameplay smoke, network/recovery/security tests, release presentation validation, and device/browser build smoke on the candidate revision.
- Record build ID, source revision, target, test run, and artifacts before making a release decision.
- Run `RuntimeHealthGate` and `MatchReadinessGate` before accepting boot or starting an authoritative match.
- Keep one composition per match and dispose it at session teardown; use its `IGameApplication` facade for lifecycle commands.

## Current integration status

The repository provides provider-neutral interfaces and local/in-memory implementations for development, plus this composition validator. No concrete authentication, cloud session/transport, profile persistence, or health provider is installed/configured. The project has no production application ID, connected Unity project, release credentials, or generated platform builds in this workspace. As a result, a production service binding cannot be instantiated here, and the gate correctly remains blocked. Do not substitute development stubs and report the result as production-ready.

This workflow adds the match composition root, app facade/event bridge, disposal ownership, service-health and match-readiness gates, and build provenance validation; it does not claim that iOS, Android, or WebGL packages have been produced or that external services are live. Final release still requires provider adapters, secure environment setup, real target builds, QA evidence, and sign-off. The release gate remains intentionally blocked without those concrete inputs. The current runtime is still limited to 4–6 players, so an 8-player readiness request fails explicitly.
