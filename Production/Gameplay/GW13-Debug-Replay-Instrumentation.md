# GW13 — Debug, Replay, and Instrumentation

## Scope

GW13 supplies evidence for deterministic gameplay and network/recovery diagnosis while keeping private match data out of normal telemetry and public replay projections.

## Replay format and validation

`ReplayRecord` is schema-versioned and records rules version, scenario/generator version, match identity, and seed. Every event has an increasing sequence, round, event code, payload, prior hash, and SHA-256 integrity hash. `ReplayIntegrity.Validate` rejects unsupported schema, missing metadata, event gaps, malformed events, broken chains, and payload/hash changes. The hash chain detects accidental corruption or edits that are not re-hashed; it is not a digital signature and does not authenticate a replay against a malicious author.

The public `GetReplayRecord` projection clears event payloads and redacts the deterministic seed before re-hashing the resulting record. Full replay payload access is exposed only in Editor/Development builds. Authority replay payloads can include ballot/reveal information and the seed needed to regenerate hidden roles/truth; keep them in trusted, access-controlled diagnostics and never replicate or upload them as general telemetry.

The event trace is a separate safe diagnostic projection: monotonic sequence, timestamp, match/round, phase, event code, and a public-state digest. The digest omits roles, hidden clues, private truth, vote targets, display names, and random match/round IDs. It is suitable for comparing equivalent seeded public outcomes, not for proving private authoritative state is identical. Replay events provide a deterministic, tamper-evident event record; deterministic command playback still requires a command journal and replay executor.

## Evidence collection

Attach build version, platform, match/round, phase, replay ID, trace sequence, public state hash, and network profile to QA failures. Retain raw authority replay separately with restricted access when investigation requires ballots or reveal payloads. Do not put private fields into core logs, production telemetry, or screenshots shared outside the incident response group.

## Current implementation boundary

The core now records the event hash chain and safe trace on each match event. It does not implement persistent replay storage, signing/encryption, command playback, network-condition capture, remote upload, or a player-facing replay UI. Those require a trusted storage/identity provider and a product retention policy. Release builds do not expose the developer replay accessor; the public projection remains redacted.
