# Character combat

The shipped JSON catalogs now include `combat` rules for all 41 character assets (460 stable card kinds). Online results are calculated only by the Go backend. The client replays per-impact HP, armor, status and slot snapshots; it does not infer damage from animation playback. `LocalCombat` emits the same protocol for offline play.

## Stats and deck compatibility

Current HP is current attack power. Armor absorbs damage before HP and does not increase attack. The left blue number is armor; the right red number is HP/attack. Details show live armor, HP, statuses, scaled effect amounts, attack cycles and passive conditions, including when inspecting dead units.

Card identities, groups, copies and prices stay compatible with existing decks. Each character's existing ten card kinds still cover costs 2–6. The reviewed workbook supplies base HP and armor at its recommended reference cost. Other price tiers scale base HP and armor by `card cost / reference cost`, rounded to whole numbers. Stars multiply by 1 / 1.7 / 2.4 / 3.1. Fixed effect amounts multiply by 1 / 1.5 / 2 / 2.5. Action frequency stays constant.

`Demoness` without an A/B suffix has no distinct asset/catalog entry. Its reviewed profile exists in the importer, but no placeholder character is enabled. Demoness A, Demoness B and Elite Orc are separate implemented characters.

## Combat rules

- Alternate teams, selecting a random eligible living attacker and random living enemy. Area effects use logical slots: 0–2 are rear, 3–5 front, and `slot % 3` is the row. Slots are mirrored only for client presentation.
- Melee retaliation uses the defender's HP before the action, happens once per action, and still occurs when the defender dies. Only the primary target retaliates. Ranged actions do not receive retaliation.
- Characters with multiple genuine attack sheets cycle them in regular combat, including sheets previously reserved for finishers. Visual variants keep the original ability cadence, damage and status rules; each sheet supplies its own impact frames. Characters with a single sheet keep that animation. Swordsman uses the original `swordsman_attack3.png` for its four-hit third attack.
- Multi-hit actions use the attacker's HP at the beginning of the action. Two hits total 120%; four total 140%. Round the total and distribute any remainder across early hits. Armor is deducted on each hit; further hits stop after the target dies. `hit_frames` specifies separate animation impacts.
- Black Knight A attack01, Demon E attack02, Knight Vanguard attack02 and Knight Templar's finishing attack03 have two impacts. Swordsman attack02 has two, attack03 four. Normal actions cycle in the workbook's proposed order; targets remain random.
- Fire adds its scaled bonus once to the next incoming attack action, then expires; it is applied after the triggering hit. Stun skips one action and cannot be reapplied until the victim completes one subsequent action. Poison damages HP directly after two victim actions and is not reduced by armor or first-hit block. Statuses do not stack.
- Lifesteal is capped by HP damage actually inflicted, occurs once per action and requires the attacker to survive retaliation. Reactive healing is excluded from the damage accounting. Healing never exceeds maximum HP or revives a dead character. Anti-heal expires after the victim's next action.
- First block halves the incoming action, rather than randomly avoiding damage. Execute bypasses armor and block, requires HP at or below 25% of maximum, and is available once per battle. Finisher animation selection itself gives no extra damage, except the explicit Templar two-hit finisher.
- Summoning/revival consume an action and a vacant slot. Summons wait for their team's next opportunity, do not persist into preparation, and do not contribute stars to player damage. Warlock can summon twice. Wizard revives once, with 40% HP and zero armor, excluding summons and previously revived characters.
- A simulation stops after 512 actions; if both teams remain alive, the result is a draw. This prevents healing/stun loops from blocking the server. The existing league presentation cap still applies, and late/capped replays apply the final authoritative snapshot.

The four star rows and rule data in `data/characters.json` are the runtime source. Editing an ability description alone does not change rules. Legacy workbook imports without `combat` retain the earlier simulator.

## Status frequency

Status-focused characters open with their status skill: Black Knight A/B, Demon C/D,
Demoness A, Lancer, Lava Slime and Flame Golem. Black Knight A applies fire or stun
on every action and its area attacks also apply the matching status to surviving
neighbors. Lava Slime alternates stun and a basic attack. Eyeball Monster poisons
on every attack as well as its existing first-kill spread; Knight Vanguard applies
fire on every attack. Stun immunity and non-stacking rules still apply. Targets
must survive damage to receive a status.

For a three-character deck with frequent, varied overhead icons, use Black Knight A
(fire/stun), Eyeball Monster (poison) and Demoness A (curse/team healing).
Replace Demoness A with Lancer to see anti-heal instead of curse. Ghostfire is a
ranged alternative for fire on every attack. Healing itself has no persistent
status icon. Online catalog changes require a Nakama restart and a fresh catalog
fetch at login.

## Verification

From the backend, generate the client fixture with `go run ./cmd/combatfixture <client>/tests/combat_fixture.json`. Build the client, then run Godot with `--path <client> res://tests/combat_effects.tscn` (add `--headless` for nonvisual checks). The runner checks alternate animations, both colored stat labels, live details, separate four-hit updates, offline profiles and reconciliation of an actual server plan. It saves `tests/combat-*.png` when rendering is enabled. The upgraded-card integration runner also checks the new stats.

Backend `go test ./...` covers armor/retaliation, multi-hit totals and early death, fire/stun/poison, lifesteal/anti-heal, execution, bodyguard, splash, summon limits and revival. Docker builds also run `go test -race ./...` before building the Nakama module.

Player-facing Thai descriptions are generated from the combat rules by `CharacterData.AttackDescription` and `PassiveDescriptionFor`, with effect values calculated for the inspected star level. Animation-only variations and workbook development notes are excluded. After building, refresh catalog copy with Godot `--headless --path <client> res://tools/character_copy_export.tscn -- <client-catalog> <backend-catalog> <embedded-catalog>`; this updates descriptive fields only.

To reapply the reviewed design to all price tiers, run `tools/apply-combat-catalog.py --backend <backend-directory> --workbook <reviewed-xlsx> --godot <Godot-dotnet-executable>` using Python with Pillow after building the client. It preserves stable kinds, writes the client, server and embedded catalogs, and regenerates descriptions from rules. Validate/test both projects before restarting Nakama.

## Combat simulator and saved visuals

Open **COMBAT SIMULATOR** from the lobby. Choose A and B,
then choose one of the character's actual in-game attacks and a visually unique
animation to display in its place. The simulator compares every
frame's pixels to hide duplicate names and fallback copies, while keeping genuine
variants. Existing saved aliases still load as their equivalent unique animation. Select an effect, body/feet/automatic anchor, X/Y offsets
and a size multiplier. Selecting an effect with the mouse or browsing it with Up/Down automatically
replays A's selected attack and displays the chosen effect on B at impact, including
highlighted items in the open dropdown without pressing Enter. Rapid browsing
cancels the previous preview and starts the latest selection. The stage shows the effect's position as values change;
**PLAY ATTACK** previews the attack and its impacts. Stage zoom does not affect
saved size. B is a preview target: a saved entry applies to every opponent and
all price/star tiers of A's character. The source-attack picker lists only attacks used by the character's combat rules,
including first attacks, finishers and executions. Different skill slots remain
independent even when they share a visual animation.

**SAVE** writes `user://combat_visuals.json` atomically and applies immediately
in offline/online battles and replays on this device. The file survives restarts.
Each character/original-attack pair keeps an independent entry. **RESET** removes
its local override and restores the packaged default. **EXPORT JSON** exports all
saved entries; unsaved edits are excluded. To ship these settings for everyone,
use the exported file as `data/combat_visuals.json` in a client build. Packaged
defaults load first, then local overrides. The server does not need visual data.

The versioned JSON records `character`, `source_animation`, `animation`, `effect`,
`offset_x`, `offset_y`, `scale` and `anchor`. Positions are battlefield units
(positive X right, positive Y down), and scale multiplies the effect's normal
battle size. Blank `effect` disables that attack's effect. Invalid entries are
ignored; invalid saves do not replace existing settings. Changing the displayed
animation remaps server impact frames onto its frame count, preserving authoritative
HP, damage, targets, ability cadence, statuses and multi-hit counts.

## Experimental brawl presentation

`BattleDemo.PresentationMode` defaults to `TurnBased`. The experimental `Brawl`
mode remains available in the inspector. In Brawl, living units walk toward enemies
at `BrawlWalkSpeed` battlefield units per second, stay in melee, and play concurrent
combat poses. Only ordered server hit snapshots apply HP, armor, statuses, slot
changes, summons and revival. Background attack poses never calculate damage or
create hit particles. Server action selection, winner, damage and deadlines remain
unchanged. Online battles, offline plans and saved replays use the same presenter.

Brawl does not use the turn-based dash, return-to-slot, global hitstop or finishing
camera sequence. Death remains authoritative, stunned units stop background attacks,
and pause freezes all movement. Cancellation restores formation positions; live
battle state refreshes do not snap the scrum back onto deployment slots. Preparation
restores the next round's formation normally.

To revert, set the BattleDemo inspector's **Presentation Mode** to **TurnBased**
(or `PresentationMode = BattlePresentationMode.TurnBased`). The existing turn-based
presentation remains intact. Run `res://tests/brawl_combat.tscn` after building to
verify concurrent movement, unchanged HP during background poses, ordered multi-hit
snapshots, pause, deadline reconciliation, cancellation and switching back.
