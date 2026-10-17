# PV5 visual polish integration

PV5 extends the PV4 visual host with a small set of event-driven presentation helpers. None of these systems reads or changes game rules, voting outcomes, or private role state.

## Install

1. Open the project in Unity and open the production scene that owns the game `UIDocument` and `ScreenManager`.
2. Select that host GameObject.
3. Run **Thief & the Necklace → Visual → Install PV5 Final Polish On Selected UIDocument**. The installer adds the PV5 `LightingController` and `VisualQualityController` alongside the PV4 adapter, environment, VFX, and audio components. The previous PV4 menu item remains as an alias.
4. Ensure the production screen host references `TANComponents.uss` through its UXML screens. The shared card and responsive states are defined there.
5. Add the authored `ToastController` to the UI layer that owns transient notices and call `Show(message)` after a successful action or recoverable error.

`LightingController` watches presentation events and transitions the existing `environment-atmosphere` layer over 0.35 seconds. It safely does nothing when a screen omits that optional layer. `VisualQualityController` applies accessibility classes and assigns a desktop, tablet, or mobile-landscape root class as the document resizes. Existing settings toggles continue to control reduced motion, high contrast, and large text.

`CardPresenter` supplies hover, press, selection, and reveal presentation for UI Toolkit cards. `RoleCardView` and `PlayerCardView` bind it to the existing reusable views. The explicit “✓ SELECTED” marker supplements the selected frame; Thief art keeps the same normal-play frame and palette as other roles. `CharacterPresentationController` exposes the seven subtle character states for hosts that have character art. `ButtonPresentationFeedback` provides reusable success/error feedback. `ScreenManager.ShowToast(message)` routes toast notices into the currently visible screen; toast elements are pooled.

PV-FINAL adds a `ReleasePresentationProfile` asset with a VFX allowlist and adds a visible resolution announcement for both the first vote tie and a second tie/Thief escape. The final-gate report and unapproved freeze baseline are documented in `PVFinalReleaseGate.md` and `PVFinalVisualFreeze.md`.

## Visual asset and runtime constraints

- Missing sprites continue to use the existing visible development placeholder; gameplay does not depend on an art reference.
- No Thief-only visual cue is shown before an authorized reveal. Reveal truth remains sourced from the PV4 authorized reveal model.
- VFX remain on the capped PV4 pool. PV5 makes the authored prewarm count a hard supply, so an exhausted pool skips that cosmetic effect instead of instantiating during gameplay. No particle spawning loop or new real-time light is added.
- The Necklace accent is a restrained state class; add it only to the authorized reveal prop.
- Responsive classes preserve minimum touch-control sizes and remove decorative spacing first on narrow landscape layouts.
- Art bundles are still authored/imported per PV3 presentation context. There are no Addressables groups configured by this code-only pass.

## Unity handoff QA

The runtime setup and styles are ready for an editor pass. These items require a Unity Editor/build and must be checked there before calling the PV5 vertical slice complete:

- Run the full menu → lobby → scenario → private role → discussion → voting → reveal → score → next round → match result path.
- Exercise a tie/revote and verify the presentation never determines the winner or exposes a hidden role.
- Resize through desktop, tablet, and mobile-landscape widths; inspect safe areas, chat, voting controls, player seats, and modal actions for clipping or overlap.
- Check Normal, Large Text, High Contrast, and Reduced Motion settings on each layout. Confirm keyboard focus and touch surfaces remain visible and usable.
- Inspect all authored sprites/materials, imports, texture memory, draw calls, GC allocations, and load spikes with real assets enabled.
- Verify VFX pool prewarm and cap against the actual assigned effect prefabs, then check pooled toast expiry/reuse in play mode.

There is no generated production art in this pass: the session did not expose `OPENAI_API_KEY`, so the user's previously approved CLI image-generation path was unavailable. This does not block the runtime fallback behavior, but it does mean the “no placeholder UI” art acceptance check remains dependent on importing the PV3 assets.
