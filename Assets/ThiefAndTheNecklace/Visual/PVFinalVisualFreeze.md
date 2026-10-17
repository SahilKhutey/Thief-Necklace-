# PV-FINAL visual freeze proposal

**State: PROPOSED — not approved.** Freeze the visual production systems only after final art is imported, the vertical slice passes, and a visual-director review approves the rendered build. Any later change to these items then requires project approval, as specified in PV-FINAL.

## Proposed baseline

- **Visual identity:** illustrated royal mystery; warm parchment, antique gold, deep ink, burgundy and courtly environment surfaces, using the PV0/PV1 palette as source of truth.
- **Design tokens:** `VisualDesignTokens` and `DesignTokens.uss` remain authoritative for spacing, radius, typography, and motion values. PV-FINAL does not replace those values.
- **Cards:** shared outer frame, inner border, art window, label hierarchy, universal back, explicit non-color selected state, and a consistent flip/reveal grammar. Normal Thief presentation stays in the same courtly family as other roles; Thief art is sourced only after authorized reveal.
- **Characters:** King, Queen, Minister, Soldier, Guard, Servant, and reveal-only Thief each use a consistent portrait/full-body/card/avatar/icon/silhouette family.
- **Palace spaces:** Banquet Hall, Royal Chamber, Treasury, Garden, East Corridor, and West Gate preserve background/midground/foreground ordering. Parallax baseline proposal: 0.00 / 0.15 / 0.30 / 0.50; UI remains fixed.
- **Motion:** retain PV5 reduced-motion behavior and review timings in a rendered build. Target bands: micro 100–180 ms, standard 180–350 ms, dramatic 350–900 ms, cinematic 900 ms or more.
- **Lighting:** normal, discussion, voting, reveal, and results presets; reveal de-emphasizes the environment while preserving readable game information.
- **VFX/audio:** release profile allowlist only; presentation events expose cue hooks, while game truth and audio decisions remain outside visual widgets.
- **Accessibility/layout:** high contrast, large text, reduced motion, focus frames, text/shape companions for color states, and desktop/tablet/mobile-landscape classes.
- **Release hygiene:** development HUD is compile guarded; missing-art markers remain development fallbacks and must not ship. Production builds are gated by required art references.

## Approval record

| Gate | Status |
|---|---|
| Final approved art imported and visually reviewed | Pending |
| Production scene and Build Settings wired | Pending |
| Unity compilation and Play Mode golden paths | Pending |
| Responsive/accessibility checks | Pending |
| Performance captures | Pending |
| Visual-director approval | Pending |
| Freeze approved | Pending |

Use `PVFinalReleaseGate.md` for the commands and release checklist. Do not change the state to approved until the Unity report has zero blockers and every manual gate above has evidence.
