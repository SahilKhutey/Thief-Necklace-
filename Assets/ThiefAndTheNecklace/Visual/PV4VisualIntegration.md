# PV4 Visual Integration

PV4 joins the local authoritative game phases to the PV2 screen system and PV3 role artwork. Gameplay remains in `PB1.GameController`; the visual adapter sends only a local vote request and reads phase-appropriate view data.

## Install

1. Open the production UI scene and select its `UIDocument` host GameObject.
2. Run **Thief & the Necklace → Visual → Install PV4 Runtime Integration On Selected UIDocument**.
3. Confirm that the scene has a `LocalGameBootstrap`. The adapter can find it at runtime if it is not assigned in the inspector.
4. Run the PV3 visual data setup after importing artwork so the role database resolves card art.

The install command adds the phase adapter, environment mood controller, and VFX pool host. VFX effect prefabs can be assigned to the pool's entries when those prefabs exist. No scene is created or overwritten by the install command.

## Integrated flow

- `PrivateReveal` reads only `GetPrivateView(localPlayerId, localPlayerId)`, animates the role card, then advances the core only when the player continues.
- `Discussion` binds public seating and timer state. Warning and critical timer cues fire once at the 60 and 30 second boundaries.
- `Voting` remains an overlay on the game table. The vote list excludes the local player, restricts runoff targets to tied candidates, and shows aggregate vote progress without exposing voter choices.
- `Reveal` binds role data only after the core authorizes that phase. A no-accusation result never identifies the hidden Thief.
- `Scoring` reads the public round-score snapshot, animates result rows, and opens results. The Next Round button calls `FinishRound()`; match standings are bound at `MatchEnd`.
- UI transitions, role flips, timer cues, score animation and VFX publish presentation-only events. Cosmetic exceptions are logged and do not call back into gameplay.

## Runtime and verification notes

`ScreenManager` supports data-driven curtain timing via `TransitionProfile`; reduced motion uses direct/fade presentation, and the existing high-contrast/large-text preferences remain applied. The environment controller uses a single tint layer and optional subtle parallax. `VFXPool` is quality-gated and recycles short-lived particle effects.

This workspace does not include an authored Unity scene, and the Unity Editor executable was unavailable during this pass. The runtime integration code and Editor installation command are present, but scene wiring and in-Editor visual QA still require opening the project in Unity.
