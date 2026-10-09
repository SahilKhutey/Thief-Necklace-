# Scenario and Content Workflow

## Scenario template flow

1. Author a `ScenarioTemplate` using the existing scenario data model and registry/content asset workflow.
2. Run `ScenarioTemplateValidator` for 4-, 5-, and 6-player rosters and supported difficulty settings.
3. Use the Scenario Builder window for preview and seed-batch validation in the Unity Editor.
4. Playtest clarity, evidence coverage, false leads, Thief agency, innocent agency, and reveal coherence. Capture results in `Production/Logs/Templates.md`.
5. Add presentation, audio, VFX, localization, analytics identifiers, version, and review record before treating it as release content.

## Live content package foundation

`Scripts/Live/LiveContentOperations.cs` validates SHA-256 package hash format, package/config version, scenario lifecycle state, bounded weights, fallback availability, season UTC windows, event references, content completeness flags, and scenario rules for all player counts. A match pin records the selected scenario/version, package hash, config/content version, season, and active event IDs.

The local model does not calculate a package hash, archive package bytes, validate real asset/localization files, write pins to an authoritative match server, or publish a canary. Those steps belong in a signed backend content pipeline. Existing matches must retain the original content/config pin; rollback applies only to new matches.

## Scenario lifecycle

`Draft → Validated → Staging → Canary → Live → Watch → Retired`; `Disabled` removes a scenario from new selection. The current code only uses `Live`/`Watch` in the eligible pool. Canary cohorts and admin approvals are documented policy, not implemented code.
