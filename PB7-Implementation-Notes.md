# PB7 implementation status

PB7 adds a local meta-game for the current offline pass-and-play build. The saved profile belongs to the local Player 1 identity; the other three pass-and-play seats do not each receive separate persistent accounts.

## Implemented

- Serializable player profile and statistics for matches, rounds, vote accuracy, Thief rounds/escapes/catches, role performance, loadout, achievements, and a short match history. Historical records omit private clues.
- `IProfileRepository` and `LocalProfileRepository`, with a versioned JSON profile in `Application.persistentDataPath`, backup copy, fallback recovery, and default loadout repair.
- Separate XP and match score systems. Completed rounds grant participation XP, with modest correct-vote/Thief outcome bonuses; completed matches grant a completion bonus and a winner bonus. No loss penalty, chat XP, paid currency, or gameplay advantage.
- Increasing level curve, unlock-by-level cosmetics, and cosmetic equip validation by type and ownership.
- Six achievements with persistent progress and one-time XP/cosmetic unlocks: First Accusation, Sharp Eye, First Escape, Five Mysteries, Regular at Court, and Perfect Deduction.
- Profile screen for display name, XP/level, stats, achievements, cosmetic collection/equipping, role performance, and recent match history.
- Reward event IDs are persisted and deduplicated before profile statistics are changed; history is bounded to 30 records.
- Four-player match results still determine the round/match points exactly as before. Profile XP is calculated separately from those points.

## Limits and remaining PB7 work

- The profile is local development data, not authenticated, encrypted, cloud-synced, or authoritative against tampering. There is no backend profile repository or reconnect/resume transaction across an interrupted match.
- Progression currently records Player 1 only because the app is local pass-and-play. It has no six-user account mapping, global leaderboard, party/social APIs, daily challenges, MMR, or moderation service.
- Cosmetic definitions are text-only placeholders; equipped avatar/frame/card-back/table/emote visuals are not yet bound into player and table presentation. No paid currency or shop is included.
- Achievement and reward values need playtesting and balance review. The local progression source is intentionally small and illustrative.
- Unity Editor compilation, save/recovery testing, and the PB7 persistence/regression matrix could not be run because Unity is not installed in this environment.
