# Character assets

Runtime character definitions live in `data/characters.json`; the backend has an identical copy. Existing IDs 0–89 are preserved. Added character cards occupy IDs 90–459. Each selectable character has ten cards: two choices per cost 2–6. Stats and basic attack behavior are initial templates, not unique class abilities. Neutral remains unchanged.

## Folder layout

- `assets/characters/<character_id>/`: runtime sprite sheets, lowercase snake_case filenames.
- `assets/characters/<character_id>/shadows/`: optional shadow variants.
- `assets/characters/<character_id>/source/`: Aseprite sources and alternate complete atlases.
- `assets/effects/characters/<character_id>/`: character effect sprite sheets.
- `assets/effects/projectiles/characters/<character_id>/`: character-specific projectiles.
- `assets/effects/projectiles/fantasy_pack_01/` and `fantasy_pack_02/`: shared projectiles.
- `scenes/characters/<character_id>.tscn`: five common animations (`idle`, `walk`, `attack`, `hit`, `die`). Flying characters use their flight animation for idle/walk.

Orc, Demon A and Blood Monster A reuse the existing characters. The new pack's Knight uses the name Knight Vanguard to distinguish it from the existing Knight. Alternate attacks, shadows, source files and effects are retained. The generated concept artwork is not used as an asset.

## Added selectable characters

- Archer
- Armored Axeman
- Armored Orc
- Armored Skeleton
- Black Knight A
- Black Knight B
- Black Knight C
- Blood Monster B
- Demon B
- Demon C
- Demon D
- Demon E
- Demoness A
- Demoness B
- Elite Orc
- Eyeball Monster
- Flame Golem
- Ghostfire
- Greatsword Skeleton
- Hellbat
- Hellhound
- Knight Templar
- Knight Vanguard
- Lancer
- Lava Slime
- Minotaur
- Orc Rider
- Priest
- Skeleton
- Skeleton Archer
- Slime
- Soldier
- Swordsman
- Warlock
- Werebear
- Werewolf
- Wizard
