# Thief & the Necklace

## P6 — Art Direction, Illustration & Visual Production Specification

**Status:** Production art bible / technical art specification  
**Parent:** P0 → P1 → P2 → P3 → P4 → P5  
**Engine:** Unity 6 LTS  
**Platforms:** Mobile and desktop/WebGL  
**Visual identity:** Illustrated royal mystery + ornate card game  
**Primary goal:** Make roles, clues, scenarios, and reveals recognizable while preserving a premium storybook/card-game feeling.

P0 calls for role-card art, player portraits/icons, reusable card prefabs, and polished flips and reveal feedback. P5 defines the screen and animation behavior. P6 defines the art language, asset families, technical preparation, and Unity integration pipeline.

## 1. Visual north star

The player should feel seated at a beautifully illustrated royal mystery table, holding a secret card while trying to work out which person is lying.

The art combines **royal, mysterious, illustrated, ornate, and playful**. It is not photorealistic, horror-focused, or generic mobile card UI. The target is 2D character illustration presented with selective 2.5D depth: layered environments, restrained parallax, lighting, and particles, with 2D UI and no full 3D character pipeline.

Visual priority:

```text
GAMEPLAY READABILITY → VISUAL HIERARCHY → ORNAMENT
```

During discussion, keep art quiet so chat, players, and timer remain prominent. Voting increases focus on eligible portraits. Reveal shifts focus to character, role/result, and the Necklace. Results return to a calm, score-readable layout.

## 2. Core art principles

- **Illustrated consistency:** shared proportions, rendering, edge treatment, detail density, and lighting across characters, cards, props, and locations.
- **Small-size recognition:** roles and key objects must remain distinguishable in compact UI.
- **No visual guilt tells:** role art must not encode the hidden Thief objective. A Minister who is the Thief still uses neutral Minister art.
- **Text stays live:** names, clues, descriptions, and point values are UI text, not baked into illustrations.
- **Layer for reuse:** one master asset should support portrait, avatar, card, and reveal presentations where appropriate.
- **Performance is part of finish:** verify actual mobile and WebGL builds, not only source images.

## 3. 2D/2.5D approach

Use illustrated character and prop art with layered environment compositions:

```text
BACKGROUND → ARCHITECTURE → PROPS → FOREGROUND → UI
```

Use subtle parallax and lighting to give the palace depth without implying a side-scrolling or explorable world. Discussion scenes should have little motion; scenario intro and reveal may use stronger motion. Static art plus UI animation is sufficient for MVP; do not require skeletal/character animation for the first playable version.

## 4. Asset families

1. Characters and portraits
2. Role cards and universal card back
3. Clue cards and clue icons
4. Scenario cards and banners
5. Palace/environment illustrations
6. Props and critical objects
7. UI frames, buttons, panels, badges
8. Icons and emblems
9. VFX and transition elements
10. Animation-ready source layers
11. Platform-specific variants and atlases

## 5. Character system

P2 separates role identity from guilt. The MVP role pool is King, Queen, Minister, Soldier, Servant, and Guard (or another explicitly configured sixth role). **Thief is not a seventh role identity**: it is a hidden objective assigned to one player who retains their normal role. The Thief emblem and reveal art represent the objective/result, not a character with suspicious pre-reveal artwork.

### Silhouette guide

| Role | Personality | Costume / silhouette cue |
|---|---|---|
| King | Authoritative, approachable | Crown, rich mantle, broad shoulders, signet |
| Queen | Composed, intelligent | Headpiece, flowing vertical lines, jewelry |
| Minister | Intellectual, administrative | Structured court clothing, scroll/document, sash |
| Soldier | Protective, disciplined | Broad shoulders, armor, shield/spear, insignia |
| Servant | Observant, overlooked | Simpler palace clothes, utility pouch/tray/tools |
| Guard | Alert, dependable | Gate/guard insignia, distinct from Soldier's armor profile |

The Thief objective must not alter costume, expression, pose, palette, lighting, card back, or portrait before reveal. Avoid villain eyes, face shadows, obvious stolen Necklace props, or menacing poses. Use role-appropriate neutral art; reveal the objective through authoritative game state.

### Expressions and representations

MVP expression set: neutral, focused, reveal. Future set may add suspicious, surprised, confident, worried, angry, and happy. Each character design should support:

- Full or half-body master illustration.
- Bust portrait.
- Small avatar.
- Role card art.
- Reveal composition/crop.

Prefer a shared source design with considered crops over five unrelated redraws. At avatar size, rely on hair, headwear, costume silhouette, and accent shapes rather than facial detail.

## 6. Card system

Card families: role, clue, scenario, and reveal. Role cards share a master shell containing an outer/inner frame, art window, title plate, emblem, description area, and optional point badge. Keep fronts uncluttered; card backs are identical for all hidden roles and objectives.

### Role card

The front contains role name, character art, short identity description, emblem, and point value where the current UX needs it. Do not bake text or value into the image. The common back carries game branding/mystery motif but no role-specific mark.

### Clue card

Use a restrained parchment panel with a clear “What you know” heading, readable clue text, and one small custom icon. Information is the focus; avoid dramatic art that interferes with reading.

### Scenario card

Use a larger narrative composition for scenario title, public incident, setting, and target object. Keep text live and localizable.

### Reveal card

Prepare layers for card, role art, label/result, emblem or Necklace, glow, shadow, and particle overlay. The composition must support both accused-innocent-then-Thief and caught-Thief outcomes.

### Layered source guidance

Where animation requires it, retain separable source layers: card background, frame, character art, light/texture, title plate, emblem, text/value badge, shadow, glow, and effects. Export runtime layers only when needed; do not multiply draw calls or memory for ornamental detail without a demonstrated benefit.

## 7. Clue iconography

Create a custom coherent set for location, time, witness, action, object, and relationship. Icons should read as simple silhouettes at 24–32 px and remain recognizable at 48 px. Avoid emoji in shipped UI. Icons support written labels and must not replace them.

## 8. Scenario environments and modular palace kit

Initial scenario: **The Royal Necklace**. P2 locations receive visual identities from P6; art does not determine the canonical location graph or clue truth.

| Location | Visual purpose | Key visual elements |
|---|---|---|
| Banquet Hall | Public, social | Royal table, chandeliers, pillars, curtains, carpet, banners |
| Royal Chamber | Important, secretive | Display stand, curtains, furniture, cabinet, focused light |
| Treasury | Valuable, restricted | Chests, document shelves, security marks |
| Garden | Open, visually distinct | Fountain, trees, flowers, walls, paths, night sky |
| East Corridor | Transitional | Long perspective, lamps, archways, doors |
| West Gate | Guard/security | Gate, post, spears, banners, courtyard |

Build reusable components: walls, columns, arches, floors, doors, windows, ceiling, lamps, banners, paintings, rugs, tables, chests, books, scrolls, plants, and statues. A modular palace kit supports multiple mysteries with changed props, objects, lighting, and narrative while preserving known locations.

Tag props as background, narrative, or critical. Critical objects receive appropriate emphasis; they must not accidentally reveal the culprit. Discussion backgrounds should not overpower the chat or player area.

## 9. Signature Necklace asset

Treat the Necklace as the signature object. Design a distinct gold structure and gemstone centerpiece with a readable silhouette at small size. Derive related assets from one canonical design:

- Main/world prop.
- Card illustration.
- Small UI icon/badge.
- Reveal composition.
- Optional glow/particle overlay.

Future mysteries may use an `ImportantObject` art pattern for Royal Seal, Ancient Key, Letter, Crown Jewel, or document. Scenario art lookup must use object IDs, not hard-coded paths in gameplay code.

## 10. Palette, materials, and motif system

Use a controlled palette with warm parchment and palace materials, restrained metal/gold accents, and darker neutral panels for contrast. Color families can distinguish royal, security, information, mystery, neutral, success, and warning UI states, but never carry meaning alone. Role accents should be subtle and must not expose Thief status.

Core material language: parchment, wood, bronze, gold, painted stone, cloth, leather, and glass. Reuse a consistent frame language and ornamental vocabulary (royal seals, scrollwork, carved corners, embossed plates) across cards and UI. Avoid applying ornament to every surface; preserve open, readable text areas.

## 11. Reveal, VFX, and motion art

VFX language: dust, small sparks, restrained glow, light rays, shimmer, card trail, gold particles, and shadow vignette. Avoid combat explosions or horror effects. Voting uses subtle outline, pulse, and selection marker. Score uses a brief rising number and restrained sparkle. Reveal may combine card flip, Necklace glow, particles, and vignette.

Provide lightweight layers for environment atmosphere, card glow, Necklace highlight, reveal particles, and UI text. Effects must not obscure text, misrepresent game state, or expose hidden information before the reveal. Reduced-motion mode uses simpler fades and static highlights while retaining equivalent information.

## 12. Resolution and platform variants

Create master art at a size that permits crops and downscaling; export only appropriate runtime sizes. Review character art and icons at actual UI sizes. Test avatars at 32, 48, 64, 96, and 128 px. Review assets at 50%, 100%, and 200% scale.

Use atlases grouped by co-use (common UI, roles, icons, clues, environment, VFX). Configure compression per target platform and profile memory, image quality, and load time in actual iOS/Android and WebGL builds. Large optional scenario art should load on demand where the project uses Addressables. Avoid excessive real-time lights and post-processing. The asset pipeline should permit a shader-free fallback for every gameplay-critical state.

## 13. Animation-ready preparation

For selected character effects, preserve useful separations such as head, body, eyes, arms, clothing, and accessories only when the final motion needs them. MVP can use static character art plus UI motion. Scenario intro may use slow background/foreground offsets and a small parallax treatment for 1–3 seconds.

Card and reveal assets should support front/back swap, frame, character, shadow, glow, and separate VFX layers. No art animation determines game-state timing; P5/P4 own presentation triggers, and the authority owns outcomes.

## 14. Naming, folders, and art data

Use predictable names, for example:

```text
CHAR_King_Master, CHAR_King_Portrait, CHAR_King_Avatar
ROLE_King_CardArt, ROLE_CardBack_Common
CLUE_Icon_Witness, CLUE_CardFrame_Common
ENV_Palace_Banquet, ENV_Palace_Treasury
PROP_Necklace_Main, PROP_Necklace_Icon
VFX_Reveal_Glow, VFX_Score_Spark
```

Suggested Unity tree:

```text
Assets/ThiefAndNecklace/
├── Art/Characters
├── Art/Cards/Roles
├── Art/Cards/Clues
├── Art/Cards/Scenarios
├── Art/Environments
├── Art/Props
├── Art/Icons
├── Art/UI
├── Art/VFX
├── Materials
├── Atlases
├── Animation
└── Data/Art
```

Suggested ScriptableObject mapping:

```csharp
[CreateAssetMenu(fileName = "RoleArt_Minister",
    menuName = "ThiefAndNecklace/Art/Role")]
public class RoleArtData : ScriptableObject
{
    public string roleId;
    public Sprite portrait;
    public Sprite avatar;
    public Sprite cardArt;
    public Sprite emblem;
    public Sprite revealArt;
}

[CreateAssetMenu(fileName = "ScenarioArt_RoyalPalace",
    menuName = "ThiefAndNecklace/Art/Scenario")]
public class ScenarioArtData : ScriptableObject
{
    public string scenarioId;
    public Sprite background;
    public Sprite[] locationArt;
    public Sprite[] propArt;
}
```

An ArtRegistry maps `roleId`, `scenarioId`, and `objectId` to presentation assets. Gameplay data must not depend on hard-coded image file paths. Keep text externalized and localized separately from art.

## 15. Unity import and prefab integration

```text
SOURCE ART → CLEAN/CROP → EXPORT → IMPORT SETTINGS → ATLAS
           → ART DATA → PREFAB → UI INTEGRATION → ANIMATION → PROFILING
```

Set sprite texture types and filtering to suit the illustrated style. Define consistent pivots for character portraits, props, UI panels, and cards. Use 9-sliced frames/plates where corners must retain shape during resizing. Keep card artwork separate from UI text and value badges. Prefabs include RoleCard, ClueCard, PlayerPortrait, VoteTarget, ScenarioBanner, ScoreRow, and RevealCard.

Create an `ArtShowcase` scene with all role portraits/cards, clue icon families, card states, palace locations, props, and VFX. Include a card showcase for front/back, selected, disabled, flip, and reveal states. This is the visual regression scene for art and prefab integration.

## 16. MVP art inventory

### Characters

King, Queen, Minister, Soldier, Servant, Guard (or configured sixth role); neutral/common card back; Thief objective/reveal emblem and reveal graphic (not a separate guilty-looking role portrait).

### Cards

Common role-card front template, common card back, role fronts, clue card, scenario intro card/banner, reveal card.

### Environments and props

Banquet Hall, Royal Chamber, Treasury, Garden, East Corridor, West Gate; Necklace; tables, doors, chests, scrolls, lamps, banners, and reusable palace architecture.

### UI and effects

Frames, buttons, panels, badges, custom clue icons, player portraits, selection highlight, card glow, reveal particles, score particles, transition effects.

## 17. Art review and QA

### Character review

- Silhouette reads at small size.
- Face/costume and role identity are consistent.
- No accidental Thief cue before reveal.
- Avatar, card, and reveal crops remain legible.
- Role is not identified by color alone.

### Card review

- Title, role, emblem, description, and value hierarchy is clear.
- Correct art and point value; text remains live.
- Common back does not distinguish hidden roles.
- No clipping; mobile scale works; flip/reveal layers work.

### Environment and VFX review

- Background supports rather than competes with UI.
- Critical props remain visible without revealing hidden truth.
- Crop/parallax is safe; effects do not obscure text.
- Mobile/WebGL performance is acceptable; reduced motion remains coherent.

### Pipeline review

Concept review → style review → readability review → technical review → Unity import test → device/browser test → approval.

Test on phone, tablet, desktop, and supported WebGL browsers for clarity, cropping, loading, memory, and frame performance.

## 18. Production phases

1. Visual identity and style bible.
2. Character style guide and MVP role concepts.
3. Character masters, portrait/avatar/card/reveal representations.
4. Role card template, fronts, and common back.
5. Clue and scenario card systems; icon families.
6. Royal Palace modular environment and location kit.
7. Necklace and critical props.
8. Reveal/score VFX and animation-ready preparation.
9. Unity data assets, atlases, prefabs, and showcase scene.
10. Platform optimization and visual QA.

## 19. P6 definition of done

P6 is complete when all MVP roles have consistent readable art; guilt is not visually leaked; the card back is universal; role/clue/scenario/reveal cards are production-ready; the Necklace is a recognizable signature asset; the palace kit and critical props exist; the icon system and UI art match; required animation layers and VFX are prepared; art is mapped by data IDs; prefabs and atlases are configured; scenario art can load independently; and mobile/WebGL builds pass visual/performance review with reduced-motion support.

## 20. P6 design lock and P7 handoff

The visual system connects **Royal Mystery + Card Game + Illustrated Characters + Layered Palace + Restrained VFX** into one coherent experience. Role art identifies who a character is; only the game's private objective state determines whether that player is the Thief.

P7 — Scoring, Progression & Meta-Game System should define how role values become match scoring, scoreboards, rankings, player statistics, optional unlocks/cosmetics/achievements, anti-farming rules, balance formulas, and Unity data structures. It must preserve P1's distinction between round outcome and individual score and P4's authoritative score ownership.
