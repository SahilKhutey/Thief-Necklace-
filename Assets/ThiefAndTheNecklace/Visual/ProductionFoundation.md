# PV0 — Visual Production Foundation

This document is the art and implementation contract for the visual production track. UI behavior must remain presentation-only: core state supplies facts; visual data and components decide how to show them.

## Visual direction

**Royal mystery · illustrated card game · palace intrigue · social deduction · premium mobile/Web UI.** Use consistent painted illustration, readable silhouettes, controlled linework and restrained ornament. The interface should feel like a physical illustrated game table. Atmosphere sets mood; the UI communicates state.

### Hierarchy

| Rank | Content | Rule |
| --- | --- | --- |
| H0 | Vote, reveal, match result, primary action | Always wins visual conflicts |
| H1 | Role, scenario, timer, score | Clear at a glance |
| H2 | Clue, chat, player status | Readable and calm |
| H3 | Settings, metadata | Secondary |
| H4 | Decoration and atmosphere | Never obscures information |

Use rounded rectangles, ornate card frames, circular avatars, pill badges and a restrained crest motif. Surfaces are illustrated and layered, with subtle depth. Avoid glass-heavy styling, heavy gradients, excessive bevels and persistent glow. Standard frames suit ordinary cards, stronger double frames mark selection, and cinematic framing is reserved for reveals.

## Semantic design system

`UI/Foundations/VisualDesignTokens.cs` is the runtime source for color, spacing, radius, border and motion values. `UI/Foundations/Resources/DesignTokens.uss` defines reusable visual roles. Keep tokens semantic: surface, raised surface, primary and secondary text, accent, success, warning, error, information and disabled. The initial palette is a production starting point and must be calibrated against approved artwork.

Typography is hierarchical: display for short titles and role names, headline for screens and sections, readable body for clues and chat, and utility/numeric styles for buttons, timers and scores. Never use display type for paragraphs. Spacing uses 4, 8, 12, 16, 24, 32, 48, 64 and 96. Radius uses small, medium and large families. Shadows have none, soft and elevated roles and should establish layer, not decorate.

Every state communicates with more than color: selected, disconnected, error and success also need a shape, icon or text label. Focus must remain visible. Reduced motion changes presentation only. Timers, chat, clues and names must stay readable at all breakpoints.

## Art and card grammar

Characters share proportions, lighting, edge treatment and detail density. Role identity comes from costume, silhouette and props, never guilt. The game rules define King, Queen, Minister, Soldier, Guard and Servant as role identities; **Thief is a hidden objective, not a seventh role**. Thief/reveal art must use a plausible courtly appearance and must not signal guilt before authoritative game state reveals it.

Role cards share a fixed frame: identity and crest at top, art in the center, role value at bottom. Card backs are universal and symmetrical, with a royal crest and necklace motif and no role-specific marks. Clue cards prioritize the clue text and source. Scenario cards use banner art, title, premise, setting and difficulty. Player cards share avatar, name and status, with connection and score added where relevant.

The Necklace is one master design reused for world, card, icon, banner and reveal contexts. Palace spaces layer background, midground, foreground, props, lighting and atmosphere. Banquet Hall, Royal Chamber, Treasury, Garden, East Corridor and West Gate must be distinguishable by architecture and props as well as color.

## Component and screen grammar

Buttons have primary, secondary, ghost and danger variants with normal, hover, pressed, focus, disabled, loading and success states. Panels are standard, informational or cinematic. Reusable components include avatar, badge, status, timer, score, chat message/input, tab, modal, toast, tooltip, progress/loading, divider, header, footer and navigation. Components accept data and expose state hooks; they do not own gameplay truth.

Every screen provides background, header, content, primary action, secondary actions and overlay layer. Runtime visual layers are environment, characters/props, game objects, HUD, interaction overlay, then modal/cinematic. Normal navigation uses fade/slide, role presentation uses a card transition, and reveal uses a cinematic transition. Reduced-motion profiles may replace these with fades or static presentation.

## Responsive and accessibility contract

Use responsive composition rather than uniformly shrinking everything. Desktop gets breathing room, tablet compresses secondary content, and mobile landscape prioritizes the table and persistent critical action; chat can collapse to an overlay. Prefer flex, anchors and adaptive containers over fixed screen coordinates. Support large text, high contrast, focus, reduced motion, color-safe state communication and readable timers/chat.

## Art data and asset pipeline

Art is referenced through ScriptableObject data, never `King -> king.png` filename assumptions. Existing `Content/CharacterArtData` and `Content/ScenarioArtData` remain the compatibility layer for art already consumed by the prototype. New general-purpose role, screen, card, player and space presentation references live under `Visual/ArtData` and are independent of core gameplay models.

Pipeline: concept/reference → style sheet → asset specification → art creation → export → Unity import → visual data asset → reusable component/prefab → screen integration → device QA. Naming uses `TAN_` plus asset family and role, such as `TAN_Role_King_Portrait` and `TAN_Space_BanquetHall_Background`. Keep iterations versioned (`_v001`, `_v002`) until approval; do not overwrite approved art without review.

Review each asset for consistency, silhouette, small-scale readability, composition, color, export correctness, performance, accessibility and gameplay-information safety. Prefer atlases, compressed textures, shared materials, pooled frequent effects and limited transparent layers; avoid giant textures, unique materials per card, unbounded particles and unnecessary real-time lights.

## Unity layout

```text
Visual/
  Branding/ Characters/ Cards/ Spaces/ Props/ Icons/
  ArtData/ Prefabs/ VFX/ Materials/
  UI/
    Foundations/ Components/ Screens/ Overlays/ Motion/
```

Character art folders use the six canonical roles plus a `Thief` folder reserved for reveal/objective presentation. Prefabs are organized by reusable UI, cards, characters, props and effects. The UI Toolkit base template lives in `UI/Foundations`; UXML owns hierarchy, USS owns presentation, C# binds view data and events.

## PV0 review gate

- Art direction, hierarchy, shapes, surfaces and semantic colors are documented.
- Type, spacing, radius, border and motion tokens have a canonical source.
- Card, player, button, panel, icon, screen and space grammars are defined.
- Visual data assets separate art references from game state.
- Responsive, accessibility, reduced-motion and performance rules are documented.
- Naming, folder, versioning, import and art-review conventions are documented.
