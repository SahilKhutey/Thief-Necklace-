# PB6 implementation status

PB6 adds presentation and content foundations to the local four-player prototype. It does not meet the document's six-player online Gold Master acceptance test.

## Implemented

- Typed, in-process `GameEventBus` with disposable subscriptions. The offline round flow publishes phase, vote, reveal and score feedback events; presentation listeners do not own game state.
- Optional `AudioCue` and `AudioCueLibrary` assets plus an `AudioService` with Music, Ambience, SFX and UI source groups, per-group persisted volume settings, cue variation, and phase/vote/reveal/score hooks. Missing cue assets intentionally produce silence.
- Mobile-only haptic pulse hook, gated by a persistent Haptics setting. The offline app invokes it on vote lock and reveal.
- Post-vote truth timeline, showing generated event times, locations, actors and actions only after the reveal phase begins.
- Six default scenario templates (up from three): Necklace, Seal, Letter, Gate Key, Heirloom and Treaty. They reuse the scenario event/clue framework and vary the target, location, premise and distraction.
- PB5 accessibility settings now support 100%, 115%, 130% and 150% text scale, in addition to high contrast and reduced motion.

## Still required

- Actual audio clips, mixer assets and authored cue library; crossfaded music, location ambience authoring and final mix tuning.
- Production particle/lighting VFX and pooling; the current presentation uses restrained UI feedback only.
- A guided playable tutorial, localization tables, scenario presentation packages, authored clue variants, and human balance/playtest passes.
- Connected six-player lobby/match flow, reconnect/network feedback and WebGL/mobile device QA. The project remains offline-only in this checkout.
- Unity Editor compilation and runtime review. Unity is not installed in this environment, so no Unity build or visual/runtime verification was possible.

To add sound, create cue assets via **Thief & the Necklace → Audio → Cue**, place an `AudioCueLibrary` asset at `Resources/Audio/AudioCueLibrary`, and add clips using the cue IDs listed in `AudioService.OnPhaseChanged` and its event handlers. Do not use role/suspicion-specific sounds before reveal.
