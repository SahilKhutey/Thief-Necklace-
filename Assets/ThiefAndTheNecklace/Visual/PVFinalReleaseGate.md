# PV-FINAL release gate

PV-FINAL is a release gate, not a declaration that unchecked work has passed. The Editor validator creates a current report at `Visual/Validation/PVFinalReleaseGate.latest.md` and non-development builds require both a populated release profile and the existing PV3 visual-art validation.

## Unity commands

1. Run **Thief & the Necklace → Visual → Build PV3 Visual Data Assets**.
2. Import approved final role, card, Palace space, and Necklace art into the documented PV3 paths. Run **Validate PV3 Visual Art** and resolve every missing reference.
3. Run **Thief & the Necklace → Visual → Create or Update PV-FINAL Presentation Profile** after building PV3 data. The profile is created at `Visual/Profiles/TAN_ReleasePresentationProfile.asset`; rerun this menu item after import to fill its database references.
4. Create and wire the production vertical-slice scene. Add it to Build Settings and configure its screen/overlay assets, core bootstrap, presentation host, and approved VFX pool entries.
5. Run **Thief & the Necklace → Visual → Install PV5 Final Polish On Selected UIDocument** on the production UI host.
6. Run **Thief & the Necklace → Visual → Validate PV-FINAL Release Gate**, then execute the manual matrix below in Play Mode and on each target layout.
7. Only after those gates pass, review and approve the proposed freeze baseline in `PVFinalVisualFreeze.md`.

The release profile is created only by the Unity Editor command. It is not hand-authored as YAML, so Unity remains responsible for stable asset GUID references. The profile carries the selected role/card/space/prop databases, transition settings, opening lighting, quality level, approved VFX allowlist, and a human-owned `approvedForRelease` flag. That flag stays false until the manual gates and visual-director review are complete. The profile does not override player accessibility preferences.

## Current workspace evidence

The source workspace currently has no PNG/JPG production textures, no visual ScriptableObject instances, no Unity `.unity` scene, and no `ProjectSettings/EditorBuildSettings.asset`. The visual hierarchy contains UXML/USS and source data definitions, but no imported final art. The Unity Editor is not available in this task environment, so there is no compile, scene wiring, Play Mode run, WebGL build, profiler capture, or visual-director sign-off to report.

These are release blockers, not passing checklist items. The PV3 build preprocessor already rejects non-development builds while required art references are missing. The PV-FINAL report adds checks for the required screen and overlay assets, duplicate/missing role and space IDs, reveal-only Thief placement, profile references, imported textures, Unity scenes, and enabled Build Settings scenes.

## Manual release matrix

- **Flow:** create/join → lobby → scenario → private role → discussion → vote → lock → resolution → reveal → score → next round → match result → menu.
- **Outcomes:** caught Thief, wrong accusation/escape, first tie/revote, second tie/Thief escape.
- **Input safety:** self-vote rejection, rapid taps, double submit, repeated screen navigation, interruption, reconnect, player exit, and scene transition.
- **Visual states:** hover, selected, submitted, resolved, disabled, focus, error, success, reduced motion, large text, and high contrast.
- **Layouts:** desktop WebGL, tablet, and mobile landscape; inspect safe areas, card aspect, input, overlap, clipping, and reachable controls.
- **Production hygiene:** no debug HUD in release, no art placeholders, no temporary borders/grids/test controls, no unapproved VFX.
- **Performance:** cold launch, screens, lobby, chat load, vote, reveal, results, and multiple rounds; record memory, GC, draw calls, texture memory, and loading spikes.
- **Art:** role-family consistency, Thief ambiguity before reveal, all six distinct Palace spaces, Necklace continuity, sprite imports/compression/atlasing, and in-context readability.

No visual freeze or release approval is granted by generation of the report. It is a machine-readable-in-practice checkpoint plus a human review handoff.
