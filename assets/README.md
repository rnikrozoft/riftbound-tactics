# Game assets

- `characters/knight`: Knight atlas, card portrait and one editable Aseprite source (original pack name: Soldier).
- `characters/orc`: Orc idle/walk/attack/hit/death sheets and one editable Aseprite source.
- `characters/demon` and `characters/blood_monster`: the newly supplied monster packs, including original atlases, alternate attacks, shadow variants and editable sources.
- `terrain`: the arena tile atlas.
- `ui/travel_book`: supplied sprites, animated sprites, sprite sheets and editable sources. Original sprite names are kept because `TravelBookUi` resolves icons by name.
- `ui/cards`: card frames, backs and shape icons.
- `effects/pixel`: animation frames and sprite sheets grouped by effect category, plus the supplier's documentation and license.
- `effects/cards`: card rip and sheen effects.
- `effects/projectiles`: both supplied arrow sizes.

Use lower snake case for new character/terrain files and category directories. Do not rename TravelBook icon keys or frame sequences without updating their consumers. Godot `.import` files travel with their source images to retain resource IDs.

Character scenes live in `scenes/characters`. The backend runtime JSON catalog and packaged `data/characters.json` point at these resources. Both teams use each card's `scene_path`; facing direction and outline material identify the opponent. Keep existing numeric kinds stable when renaming a displayed character or a resource.

Cleanup removed unused shadow/alternate Knight/Orc sheets and duplicate Knight/Orc sources. The newly supplied Demon/Blood Monster packs are retained and registered in the character catalog. All UI and effect files were retained. Every moved asset was checked against its original bytes.
