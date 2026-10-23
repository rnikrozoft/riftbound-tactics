# Slate & Brass UI asset pack

Generated with the built-in image generation tool using the approved concept.
The original DecoCards art has been restored separately; the theme is now applied to menus and battle HUD through shared UI helpers.
It does not replace card art, characters or terrain.

## Contents

- `controls.png`: primary and secondary buttons, tabs, inputs; four states each.
- `surfaces.png`: panels, tooltip, modal, profile, shop, hand tray, standings,
  slots, portraits, title banner, divider, badge and row highlight.
- `icons.png`: 16 utility icons, including battle, coin, reroll and lock.
- `controls/`, `surfaces/`, `icons/`: 48 individual Godot AtlasTexture resources.
- `styles/`: 20 reusable StyleBoxTexture resources for nine-slice controls/panels.
- `slate_brass_theme.tres`: Godot Theme, assigned in project.godot and normalized by SlateBrassUi.
- `manifest.json`: names, source atlas regions and texture paths.
- `GENERATION_PROMPTS.md`: the exact final generation prompts.

## Use in Godot

Use the named `.tres` textures directly in TextureRect, TextureButton or
NinePatchRect. All three PNGs have transparent alpha. Keep texture filtering
Nearest for pixel art. Four interaction states within each control family share
identical atlas region dimensions, so changing states preserves control size.
Text belongs in Godot labels, not the art.

Assign `slate_brass_theme.tres` to a selected preview/root Control to try the
secondary button, panel, tab and input styles. Set a Button's Theme Type
Variation to `PrimaryButton` for gold actions. TextureButton skins must be
assigned explicitly: use primary_normal / hover / pressed / disabled, or the
corresponding secondary textures. TravelBookUi.StyleButton now delegates to SlateBrassUi. TextureButton backgrounds
use a nine-slice child so long/narrow controls retain crisp corners. UI sprites
are downsampled in memory to logical pixel scale; source PNGs remain unchanged.
StyleBoxTexture margins are supplied as starting presets; the fixed shop/hand
tray artwork has embedded slots and should be treated as a composed surface.

Run `res://tools/ui_theme/slate_brass_preview.tscn` to browse all textures.
The preview opts into the new Theme without changing the running game UI.
Use `-- --capture` to export each preview page to `/tmp` and exit.

## Re-indexing

`python3 tools/ui_theme/prepare_slate_brass.py` rebuilds the resource index and
Theme from the source atlases. It reads alpha bounds and writes `.tres`/JSON;
it does not edit, recolor, resize or redraw the generated PNG pixels. Bounds use
alpha >= 32 to omit nearly invisible generation padding from the atlas region.
