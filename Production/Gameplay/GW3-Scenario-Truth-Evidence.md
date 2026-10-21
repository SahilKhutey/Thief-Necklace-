# GW3 — Scenario, Truth, Timeline, Evidence & Opportunity

**Track:** Gameplay Workflow Production  
**Status:** Authority-side scenario model, consistency validation, bounded end-to-end deterministic retries, and an authored unique-solution sample implemented; broader content and QA remain open
**Depends on:** GW0 foundation, GW1 match lifecycle, GW2 player and information workflow

## Implemented

- `ScenarioTruthModel` now carries scenario/generator versions, seed, theme, hidden Thief identity, stolen object, locations, actors, objects, facts, causal timeline events, evidence, opportunities, and player information. Deep copies preserve these fields.
- The Royal Necklace seed data defines the palace locations and connections, actor capabilities, Necklace/case/door objects and a Necklace object transition, causal links from entry to theft to exit and discovery, and traceable fact/evidence/event references.
- Match preparation binds symbolic scenario actors and facts to the match-local player identity after seeded role assignment. The Thief remains a hidden objective over a court role.
- `ScenarioValidator` checks unique identities, location/actor/object references, chronological event order, causal reference/order/cycles, simultaneous incompatible actor locations, evidence references, false-lead provenance, private fact/evidence references, and critical-event/fact support.
- Evidence independence counts distinct authored causal paths or sources rather than treating duplicate descriptions as independent support.
- `ScenarioSolvabilityValidator` returns `UniqueSolution`, `MultipleSolutions`, `NoSolution`, or `InsufficientEvidence`, plus remaining candidates and the authored evidence set used for elimination. The build pipeline rejects `NoSolution` and enforces unique solutions when configured. The default mystery has two plausible opportunity candidates; a privately distributed, explicitly authored alibi clue eliminates the innocent candidate.
- Public scenario projection includes public locations and public object identities while omitting private location/state; reveal projection is an authority-side full-truth snapshot and must only be exposed after the existing reveal gate.
- `ScenarioDeductionService` evaluates alibis against timeline events and creates typed contradictions only when the caller supplies evidence known to that player. Claims remain separate from truth.
- Private clue projection strips support/contradiction fact IDs, source event IDs, causal path IDs, ownership, and internal critical/false-lead flags. Authored clue templates can bind a named player target, and recipients receive rendered clue text without the authority's trace links.
- `RoundFactory` retries structurally invalid generated scenarios and failures discovered during role assignment, private package creation, or final solvability validation up to `MaxGenerationAttempts`. Each attempt uses a stable seed derived from the round seed; rejected candidates do not commit as the round scenario. The chosen attempt and seed are recorded in authority state and the round log, and exhausted retries return structured failure through `ScenarioGenerationException`.

## Remaining GW3 production gates

- Expand authored candidate-elimination rules across scenario content. The current solver only interprets explicit `EliminatesPlayerIds` metadata and does not infer arbitrary natural-language statements; its reported evidence set is the authored set used, not a general minimum-set proof.
- The primary `MatchController` round preparation uses end-to-end bounded retries. The older direct `GameController` entry point validates its completed scenario but does not share the same retry transaction; consolidate or retire that legacy path before relying on it for production round preparation.
- Expand scenario data authoring to cover richer alibi truth, typed contradictions, object access constraints, and clue text variants. Evaluation services and object transitions exist, but the complete authoring/UI workflows are not wired.
- Add serialization, deterministic replay, fairness, knowledge-distribution, and solvability tests in the GW14 suite. Tests were not run for this implementation pass.
- Connect scenario projections and private clues to an authenticated transport adapter; GW1/GW2 remain in-memory authority models.

## Runtime boundary

The complete `ScenarioTruthModel` is authority-only. Public and reveal DTO construction is separate. Do not serialize the mutable truth model to ordinary clients or telemetry. Client delivery, authentication, persistence, and post-restart recovery remain outside this local runtime.
