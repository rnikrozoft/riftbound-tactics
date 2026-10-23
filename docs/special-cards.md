# Special cards

Nine generated transparent sheets live in `assets/special_cards/<character_id>/`. Each sheet has four columns and two rows: four looping front frames for the player, four rear frames for the opponent. Scenes use the first row; `enemy_animation_path` uses the second row without horizontal mirroring. Idle playback is 5 FPS. Original Healing Totem keeps its existing eight-frame sheets.

All ten support cards are available in each player’s shop pool (one copy each), gated by shop level and card cost. They deploy only to the five support slots, cannot attack or be targeted, and do not contribute survivor damage. Hero deck construction still uses 30 hero cards.

Persistent supports: Guardian Totem grants 2 armor once at battle start; War Banner boosts attack/counter/splash damage by 10%; Purifying Shrine cleanses fire first, otherwise poison, from the lowest-current-HP affected ally after every action and damage-over-time resolution.

Single-use traps: Thorn Trap retaliates for 4 armor-piercing damage after melee; Frost Rune stuns after an enemy attack; Fire Mine burns the attacker and side-adjacent allies after melee; Emergency Ward cancels one lethal damage instance; Mirror Sigil reflects 50% of ranged attack damage; Sealing Rune reduces a multi-hit action to one original-strength hit. Multiple distinct traps can trigger on the same action. Traps emit `trap_consumed` snapshots, flash, disappear, and are removed from deployed units when the battle finishes. Replay events retain the trigger.

Catalog IDs 505–513 are stable. Client and both Go catalog copies are identical, roster version 12. Ability descriptions have Thai and English entries in `data/ui_translations.json`. Online outcomes are calculated by Go; offline simulation mirrors these triggers in LocalCombat.

Validation: `special_cards.tscn`, `healing_totem_online.tscn`, `character_catalog.tscn`, `localization.tscn`, asset audit, .NET build, and Go tests including `special_cards_test.go`.
