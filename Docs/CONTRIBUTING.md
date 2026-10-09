# Contributing

## Before changing code

- Read the relevant P1–P9 design spec and PB implementation notes.
- Preserve the offline-only boundary unless a network provider is selected and integrated deliberately.
- Keep game truth/secret data out of public snapshots, logs, telemetry, and UI state beyond the authorized player.
- Avoid changing vote/scoring/progression rules without updating the design docs and release traceability.

## Changes

- Keep Core and contract assemblies engine-independent where possible.
- Keep authored scenario/content data versioned and validate for all supported player counts.
- Do not commit credentials, service keys, player exports, crash dumps, or private beta data.
- Keep Unity `.meta` files for assets; do not commit generated `Library`, `Temp`, build output, or local editor settings.
- Use focused commits with a clear area and outcome. Do not call a feature verified without a Unity/editor run and recorded evidence.

## Review evidence

Include the intent, affected systems, compatibility/migration impact, tests run (or why blocked), and relevant checklist/evidence IDs. Security- or privacy-sensitive changes require review of logs, telemetry, and recipient state as well as code.
