# Game assets

Runtime artwork is grouped under five categories. The supplied Undead source pack stays at its requested original path. Asset pixels and original pack filenames are preserved.

| Folder | Contents |
| --- | --- |
| `Free-Undead-Tileset-Top-Down-Pixel-Art/` | Supplied Undead Hollow environment pack and original license |
| `characters/<character>/` | Character animation sheets, shadows and editable sources |
| `characters/packs/tiny_rpg_01/` | Original Tiny RPG pack, including its character, projectile and Aseprite folders |
| `characters/licenses/` | Character pack license information |
| `terrain/atlases/` | Arena ground and generated terrain atlases |
| `terrain/isometric/` | Original isometric terrain pack |
| `terrain/bushes/` | Original bush pack and its derived bush atlas |
| `characters/wildlife/` | Wildlife sprite sheets |
| `ui/travel_book/` | UI sprites, animations, sheets and editable sources |
| `cards/` | Card frames, backs and shape icons |
| `cards/packs/deco/` | Original DecoCards pack |
| `cards/packs/fageltomten/` | Original themed playing-card packs |
| `cards/special/` | Support and trap art, animations and manifest |
| `ui/pixel_pack_4/` | Shared pixel UI sheets |
| `ui/skill_icons/paladin/` | Paladin skill icons with original license |
| `ui/status_icons/` | Buff/debuff icons, source files, palettes and previews |
| `ui/slate_brass/` | Existing alternate UI resources |
| `effects/characters/` | Character-specific effect sheets |
| `effects/projectiles/` | Shared and character-specific projectiles |
| `effects/cards/` | Card rip and sheen effects |
| `effects/screen/bloody/` | Original bloody screen effect pack |
| `effects/packs/four_fire_v1_1_0/` | Original Four Fire effect frames, sheets and license |
| `effects/pixel/` | Effect frames and sheets, documentation and licenses |

## Conventions

- Use lower snake case for new category folders and runtime asset names.
- Preserve original filenames inside third-party packs, especially names used as lookup keys.
- Move Godot `.import` metadata with each image to retain its resource UID.
- Update resource paths in scenes, tile sets, code, tools and all three character catalog copies together.
- Character scenes belong in `scenes/characters/`; preserve published character IDs even when retiring a character.
- Generated atlases are rebuilt with `tools/build-bush-atlas.py` and `tools/blend-living-garden.py`.

Validation: `python3 tools/audit-character-assets.py`, the Godot/.NET build, card-idle/projectile tests and live catalog loading cover the consumers affected by asset moves.


The 2026-10-23 folder move is recorded in `docs/asset-path-moves.json`. Artwork and binary source files retain their bytes; `.import` files retain their UIDs while Godot refreshes generated cache paths. Character catalog copies in client and backend share the relocated resource paths. Only the catalog content revision advances to refresh the published configuration; card identities, stats, availability and combat rules remain unchanged.


Asset-move validation: 483 import UIDs preserved after Godot reimport; 27,598 static resource references resolved; binary images/source files verified unchanged. Character asset audit, .NET build, Go tests/vet/Linux race build, card backs, playing-card packs, Paladin skills, special cards, pixel UI, projectiles, living garden, live catalog and combat simulator checks passed.
