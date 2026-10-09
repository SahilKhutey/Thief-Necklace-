# PB5 implementation status

PB5 adds a UI Toolkit presentation foundation to the existing offline pass-and-play prototype. It does not turn the prototype into the six-player online milestone described by the PB5 release gate.

## Implemented in this project

- Shared royal palette, spacing, corner and motion tokens in `Assets/ThiefAndTheNecklace/Scripts/UI/UIThemeData.cs`. An optional `Resources/UIThemeData` asset overrides the runtime defaults.
- Reusable shared card and button styling in `OfflinePrototypeApp`, including hover/pressed/focus/disabled states, visible focus borders and high-contrast borders.
- Responsive page insets for narrow and wide screens; content remains centered and capped for readability.
- Local Settings and How to Play screens. Reduced Motion, High Contrast and Large Text persist using `PlayerPrefs` and do not change game rules.
- Short UI-only fades that stop when an element is removed and are disabled by Reduced Motion.
- Optional scenario illustration binding by scenario ID through `ScenarioArtData` in `Resources/ScenarioArt`. Existing `RoleDataAsset` already supports portrait, card art and emblem sprites. Empty art references are supported.
- Private role content continues to be shown only on the private handoff/review view and the post-vote reveal view.

## Still required for the PB5 release gate

- Production character, scenario, palace, necklace and UI illustration assets; this repository currently contains no art files or licensed fonts.
- Connected create/join, lobby, ready/presence, reconnect and six-player multiplayer UI. The current runtime remains offline four-player pass-and-play, and no PB4 transport/session adapter exists in this checkout.
- Full presentation services for audio/haptics, production scene navigation, visual regression and multiplayer/secret-state QA.
- Unity Editor compilation, device/responsive review, and playtesting. Unity is not installed in this authoring environment, so the code changes could not be compiled or visually verified here.

## Art handoff notes

Create a `ScenarioArtData` asset under `Resources/ScenarioArt` for each scenario, set `scenarioId` to the matching scenario template ID, and assign its key illustration. Keep the key subject readable at the intro panel's wide aspect ratio. Use `CharacterArtData` for role portrait/card/emblem direction; the existing role assets remain the runtime source for role sprites. Use licensed or commissioned source files and import them with consistent sprite pivots, compression, and target-platform sizes.
