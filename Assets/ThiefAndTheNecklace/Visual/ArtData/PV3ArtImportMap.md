# PV3 art import map

Run **Thief & the Necklace → Visual → Build PV3 Visual Data Assets** after adding art. The command configures imported raster textures for UI sprites and fills existing ScriptableObject slots by these stable filenames. It preserves already assigned sprites when an optional source file is absent.

## Characters

For King, Queen, Minister, Soldier, Guard, and Servant, place six sprite PNGs in `Characters/<Role>/`:

```text
TAN_Role_<Role>_Portrait.png
TAN_Role_<Role>_FullBody.png
TAN_Role_<Role>_CardArt.png
TAN_Role_<Role>_Avatar.png
TAN_Role_<Role>_Icon.png
TAN_Role_<Role>_Silhouette.png
```

Thief objective art uses the same six suffixes with `TAN_Thief_Reveal_` in `Characters/Thief/`. It is stored only on `RoleVisualDatabase.thiefObjectiveRevealData`; `Find("Thief")` stays null before the Core authorizes reveal. Keep the Thief's costume, palette, lighting, pose, icon, and effects within the court-role family so the illustration cannot disclose guilt.

## Palace spaces

For each of `BanquetHall`, `RoyalChamber`, `Treasury`, `Garden`, `EastCorridor`, and `WestGate`, place:

```text
Spaces/<Space>/TAN_Space_<Space>_Background.png
Spaces/<Space>/TAN_Space_<Space>_Midground.png
Spaces/<Space>/TAN_Space_<Space>_Foreground.png   (optional)
Spaces/<Space>/TAN_Space_<Space>_Thumbnail.png
Spaces/<Space>/TAN_Space_<Space>_Lighting.png     (optional)
Spaces/<Space>/TAN_Space_<Space>_Atmosphere.png   (optional)
```

Keep the central table clear in the Banquet Hall, and distinguish locations by architecture and props as well as hue. Keep UI and card safe areas free of busy detail.

## Cards and Necklace

```text
Cards/Common/TAN_Card_UniversalBack.png
Cards/Role/TAN_Card_RoleFrame.png
Cards/Role/TAN_Card_RevealFrame.png
Cards/Clue/TAN_Card_ClueFrame.png
Cards/Clue/TAN_Card_ClueArt.png
Cards/Scenario/TAN_Card_ScenarioFrame.png
Cards/Scenario/TAN_Card_ScenarioArt.png
Props/Necklace/TAN_Necklace_Master.png
Props/Necklace/TAN_Necklace_Icon.png
Props/Necklace/TAN_Necklace_Reveal.png
```

Keep role names, values, clue body text, scenario titles, and difficulty labels in live UI text. The universal back contains no role-specific marks. Generate raster transparencies only through a workflow that supports real alpha, or author cutouts in a graphics editor; do not simulate a transparent sprite with a visible flat-color background.

## Import and release gate

The setup command uses single-sprite import, bilinear filtering, clamp wrapping, no mipmaps, compression, and a 2048 px maximum texture dimension. Run **Validate PV3 Visual Art** after importing. Development builds can still run while art is incomplete; release builds stop with asset-specific errors until all required art is assigned.
