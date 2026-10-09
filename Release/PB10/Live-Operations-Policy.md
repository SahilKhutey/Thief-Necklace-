# PB10 Live Operations Policy

## Release cadence

Use hotfixes only for urgent defects/security; schedule content updates at a cadence the team can validate and support. Every change is classified and versioned. High-risk gameplay, progression, or matchmaking changes require designer, engineering, and QA approval; security or operational risk adds the relevant reviewer. Critical identity, secrets, scoring, and persistent-reward changes require an explicit security review.

## Content lifecycle

`Draft → Validated → Staging → Canary → Live → Watch → Retired`

`Disabled` is a reversible state used to stop selection for new matches. Retiring a scenario does not delete its historical artifact. A candidate needs a working fallback, complete assets/localization/analytics identifiers, scenario validation at 4/5/6 players, human playtest evidence, and a rollback target before activation.

## Match pinning

At match creation, persist match ID, selected scenario ID/version, content package version, config version, seed, and any event/season IDs. Existing matches finish against their original pin. A new configuration or a season transition applies only to new matches. Keep old artifacts for result review and deterministic reconstruction.

## Rollout and rollback

Validate locally and server-side, deploy to staging, run regression and a controlled canary, compare pre-agreed health/fairness thresholds, then expand deliberately. Roll back for integrity/privacy/security failures, broken scenario logic, material reliability regressions, or threshold breaches. Rollback must stop new assignment without invalidating active matches or historical data. Do not tune one balance variable at a time based only on a small sample or a single aggregate win rate.

## Experiment policy

Experiments require a written hypothesis, one primary variable where practical, stable assignment, opt-in/privacy review where needed, success and stop criteria, and a documented end date. Experiments may not change identity, secret isolation, authority, ownership, or persistent reward security. No experiment should compromise match fairness or silently assign preferred Thief roles.

## Ranked gate

Ranked is not enabled by this PB10 foundation. It remains off until casual multiplayer, authoritative results, reconnect/abandonment rules, security, service capacity, rating simulation, and a closed competitive test all pass and receive a separate product approval.
