# Game assets

All artwork is grouped under four categories. Asset pixels and original pack filenames are preserved.

| Folder | Contents |
| --- | --- |
| `characters/<character>/` | Character animation sheets, shadows and editable sources |
| `characters/packs/tiny_rpg_01/` | Original Tiny RPG pack, including its character, projectile and Aseprite folders |
| `characters/licenses/` | Character pack license information |
| `terrain/` | Arena ground and generated terrain atlases |
| `terrain/isometric/` | Original isometric terrain pack |
| `terrain/bushes/` | Original bush pack and its derived bush atlas |
| `terrain/critters/` | Wildlife sprite sheets |
| `ui/travel_book/` | UI sprites, animations, sheets and editable sources |
| `ui/cards/` | Card frames, backs and shape icons |
| `ui/cards/deco/` | Original DecoCards pack |
| `ui/status_icons/` | Buff/debuff icons, source files, palettes and previews |
| `ui/slate_brass/` | Existing alternate UI resources |
| `effects/characters/` | Character-specific effect sheets |
| `effects/projectiles/` | Shared and character-specific projectiles |
| `effects/cards/` | Card rip and sheen effects |
| `effects/screen/bloody/` | Original bloody screen effect pack |
| `effects/pixel/` | Effect frames and sheets, documentation and licenses |

## Conventions

- Use lower snake case for new category folders and runtime asset names.
- Preserve original filenames inside third-party packs, especially names used as lookup keys.
- Move Godot `.import` metadata with each image to retain its resource UID.
- Update resource paths in scenes, tile sets, code, tools and all three character catalog copies together.
- Character scenes belong in `scenes/characters/`; preserve published character IDs even when retiring a character.
- Generated atlases are rebuilt with `tools/build-bush-atlas.py` and `tools/blend-living-garden.py`.

Validation: `python3 tools/audit-character-assets.py`, the Godot/.NET build, card-idle/projectile tests and live catalog loading cover the consumers affected by asset moves.
