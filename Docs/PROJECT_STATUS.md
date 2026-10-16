# Project Status

## Current state

This repository contains an offline four-to-six-player pass-and-play Unity prototype, scenario-generation code, test source files, and a series of architecture/control-plane foundations. It is **not** an online product or production release candidate.

| Area | In repository | Verified status |
|---|---|---|
| Core match flow | Offline 4–6 player local UI and core rules; five-round match; seeded scenarios | Not compiled or run in the current environment |
| Scenario generation | Seeded generator, authored templates, validator | Source present; Unity and mass-validation evidence not recorded |
| Multiplayer | Authority and routing contracts | No NGO/Relay/Auth/session adapter or online client |
| Backend | Match/profile/matchmaking/moderation contracts | No backend host, database, or provider adapter |
| Profile/progression | Local profile foundation | Not cloud-authoritative |
| Live content | In-memory versioned package, validation, match pin and rollback model | Not connected to server time, content archive, or publishing service |
| Logging | Allowlisted structured logger contract | No persistent or remote sink, retention policy, or operational dashboard |
| Release | Checklist, beta, threat, and operations templates | No built RC, CI release flow, beta cohort, platform certification, or sign-offs |

The final checklist and PB9 release gate are intentionally **BLOCKED / NO-GO** until candidate-specific evidence exists. A design document or source contract is not proof that the system is integrated or working.

## Immediate path

1. Open in the specified Unity Editor, resolve import/compiler errors, and run the included Edit Mode suites.
2. Verify the offline playable loop and scenario generation on 4, 5, and 6 players.
3. Decide whether the next milestone is an offline demo or a connected online product. For online, select a backend/provider and implement the server authority, authentication, lobby/matchmaking, reconnect, profiles, and reward ledger.
4. Complete art/audio/accessibility and platform-specific validation for the chosen targets.
5. Use the PB9 evidence pack and final checklist to make a release decision. Do not label a build RC/LC or GO until the same immutable artifact passes the gates.

## Out of scope for current implementation

No friends/presence, ranked mode, paid economy, battle pass, live event operations, canary publishing, production moderation, or real-time analytics pipeline is enabled.
