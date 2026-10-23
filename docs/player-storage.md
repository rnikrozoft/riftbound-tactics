# Player collection, decks, wallet and published game data

**Current free distribution:** every account permanently receives all 40 active character cards and the default card back only. Existing profiles receive missing items on account load, without changing wallets, saved decks, selected deck IDs, or historical purchases. Character purchases are paused; Shop displays all 20 card backs, with 19 paid designs at 100 coins each, while character cards are browsable in Collection and select three groups plus one back in Deck → Edit deck. Character and group purchase RPCs reject purchases while distribution is enabled; card-back purchases remain enabled. `economy.free_character_cards` is enabled and `economy.free_card_backs` is disabled for this rollout; historical commerce documentation below describes the retained implementation for a future reopening.

The lobby now has **Collection** and **Shop** pages. Deck edits and selected deck are saved to the authenticated Nakama account. The arena shop still uses temporary round coins; the lobby shop uses the persistent `coins` wallet.

## Stored data

| Collection / key | Owner | Read / write permissions | Contents |
|---|---|---|---|
| `riftbound_player / collection_decks` | Player user ID | 1 / 0 | Schema, owned character IDs, saved decks and selected deck ID |
| `riftbound_purchases / request_id` | Player user ID | 1 / 0 | Purchase receipt with character, server price and currency |
| `riftbound_grants / request_id` | Player user ID | 1 / 0 | Receipt for a trusted currency/unlock grant |
| `riftbound_config / published` | System | 0 / 0 | Character catalog and economy settings |

Nakama Wallet stores persistent `coins`. Deck cards refer to stable `character_id` values and copy counts; they do not duplicate character stats. Hero group identities and existing character IDs cannot be changed by the admin publication API. Local rendering assets stay in the client build.

New players permanently own all 40 active characters and one default card back and start with **0 wallet coins**. The three free starter hero groups are **Knight Legion, Orc Horde and Demon Covenant**. Ranger, Mage, Guardian and Neutral are retired; the deck now has 30 slots. `economy.all_unlocked` defaults to **false**; all active characters are currently granted automatically. Current price seeds are **100 coins per card variant**, editable through published backend settings. When all-access is disabled, the server checks ownership when saving decks, entering rooms and starting matchmaking. A character unlock permits the catalog's normal deck copy limit; it is not a consumable card copy.

Purchases use server prices and identity from the authenticated request context. A versioned collection update, purchase receipt and wallet deduction are committed together using Nakama `MultiUpdate`, with wallet ledger enabled. Retries use the same request ID; concurrent retries and insufficient funds cannot partially charge/grant. The same transaction pattern supports trusted unlock/currency grants for future rewards or store fulfillment. There is no new automatic currency reward, paid checkout or achievement system in this change.

## Login, migration and cache

Login calls `player_bootstrap`. Collection/decks/wallet are loaded from the account every login. Only the original account instance slot imports the old shared local decks once, and only when no server profile exists. Other instance accounts start independently. Legacy files are preserved and are not used as authoritative online saves. Invalid legacy decks are skipped; the starter deck remains if no valid import exists.

The client caches the backend catalog and economy settings by server address and `config_version`. Each login and new arena launch checks the active version before gameplay resources are constructed. Unchanged versions omit the large config body; changed or invalid caches download it again. Authoritative online loading failures block entry rather than substituting local gameplay values. Offline labs/tests may still use packaged data.

Deck writes require the current profile/config version. A conflict reloads account data, preserves the editor draft and asks the player to review/save again. Player storage is owner-readable but clients cannot write it directly. The Shop's Refresh Collection button reloads balances and ownership.

## Admin tools

`tools/nakama-admin.py` is in the backend repository. It uses the trusted runtime HTTP key; authenticated player tokens cannot invoke admin operations. Set `RIFT_NAKAMA_ADMIN_HTTP_KEY` to the deployment's runtime HTTP key. Keep it out of the client. The existing local debug configuration has a development key; use deployment secrets for public servers.

Examples, run from the backend repository:

```sh
python3 tools/nakama-admin.py config-get /tmp/riftbound-config.json
# Edit config.catalog character stats/combat settings or config.economy in that file.
python3 tools/nakama-admin.py config-publish /tmp/riftbound-config.json
python3 tools/nakama-admin.py grant USER_ID --coins 500 --request-id unique-reward-id
python3 tools/nakama-admin.py grant USER_ID --character CHARACTER_ID --request-id unique-unlock-id
```

The publish tool compares the exported config with the current stored config and sends only changed character fields/economy settings. Patches are bounded to 128 KiB and use the existing HTTP request limit. Large edits must be published in smaller sets. Config writes validate the complete resulting catalog, use storage version checks, and report `restart_required: true`.

**Publication becomes active after backend restart.** A node uses one immutable catalog/economy revision for its lifetime, so admin edits cannot change active battles halfway through. A restart ends existing in-memory matches under the current architecture. No admin GUI or seamless live revision switching is included; the API/tool provide the persistent backend path for a future admin UI. Adding new character assets also requires a compatible client build.

The bundled character JSON now seeds the central Storage object only if it does not exist. Once initialized, publish changes to the central configuration; rebuilding with a different bundled JSON does not overwrite admin settings.

## Checks

Backend tests cover ownership enforcement versus temporary all-access, atomic/idempotent purchases and grants, independent accounts, legacy import once, deck CAS, permissions, stable IDs and publication without mutating the active catalog. The integration tools exercise the real Nakama Storage/Wallet permission and transaction behavior, including relogin persistence. Godot checks cover bootstrap/cache, account switching, cloud deck save and the Collection/Shop UI.

References: [Nakama access controls](https://heroiclabs.com/docs/nakama/concepts/storage/permissions/) and [Go MultiUpdate](https://heroiclabs.com/docs/nakama/server-framework/go-runtime/function-reference/#multiupdate).

Validation completed on 2026-10-06: .NET build (zero warnings/errors), backend race tests, real Storage/Wallet integration (24 checks), Godot collection/cache/cloud-save/arena-launch flow (13 checks), matchmaking (56 checks), and same-session recovery/latest-login enforcement. Collection and Shop were also inspected in a rendered Godot window.

The lobby can delete any saved deck, including the final deck. An empty deck list has an empty selected deck ID; returning to the account does not recreate the starter deck. Deletion uses the existing version-checked deck save RPC and preserves collection ownership and wallet balances. A player with no decks must create a deck before entering matchmaking.

## Character group shop

The lobby Shop supports individual card purchases and **UNLOCK ALL** for the selected character group. `character_group_purchase` accepts `group`, `request_id` and `config_version`. The server sums prices for enabled, unowned card variants and atomically writes ownership, a receipt and the wallet deduction. Already owned variants cost nothing again; concurrent/repeated purchases cannot charge twice. A complete standard group has ten distinct characters, costing 1,000 coins at seed prices. Purchased groups become selectable in Deck Builder immediately and remain owned across login sessions. Existing purchases and grants are preserved when free testing access is disabled.

The wallet still begins at zero. This change does not add an automatic reward or real-money payment system; trusted admin/reward APIs can fund the wallet.

## Card backs

The Shop and Collection pages have a **Card Backs** tab with previews of the 20 existing designs. Ember Crest (`card_back_00`) is free; each of the other 19 designs costs 100 wallet Coins. Ownership belongs to the account and survives logout/login. Character test access does not unlock paid cosmetics. Duplicate/concurrent purchase retries charge once; insufficient funds leave the wallet and collection unchanged.

In **Decks → Edit deck → Choose card back**, choose one owned design and press **Save deck**. The selected design applies to every card in that deck. Different decks may use different backs. Buying a design does not automatically equip it. Deck clones, local saves, cloud saves, lobby previews, multiplayer opponent hands and replay snapshots retain the selection. Older decks without a selection use the free default.

The fallback catalog is `data/card_backs.json`; the server sends the same definitions through bootstrap configuration. Saved decks use `card_back_id`; profiles use `owned_card_backs`; purchases call `card_back_purchase`. Online deck save and match entry reject unowned or unknown designs.

## Four unique character groups (roster 2)

Only four groups are selectable. Each contains ten different character assets, with no asset shared between any active cards. Decks select three groups and contain thirty distinct cards (two at each cost 2–6 per group), with 1–8 copies per card.

| Group | Characters |
|---|---|
| Knight Legion | Knight, Knight Templar, Knight Vanguard, Soldier, Swordsman, Lancer, Archer, Priest, Wizard, Warlock |
| Orc Horde | Orc, Armored Orc, Elite Orc, Orc Rider, Armored Axeman, Minotaur, Werebear, Werewolf, Slime, Lava Slime |
| Demon Covenant | Demon, Demon B, Demon C, Demon D, Demon E, Demoness A, Demoness B, Hellbat, Hellhound, Flame Golem |
| Blood Legion | Blood Monster, Blood Monster B, Armored Skeleton, Black Knight A, Black Knight B, Black Knight C, Eyeball Monster, Greatsword Skeleton, Skeleton, Skeleton Archer |

Ghostfire is held in reserve. New accounts own all four groups; their initial deck still selects the first three. The old catalog identities remain reserved for saved-data compatibility and are hidden from selection and purchase. Existing decks are migrated while preserving their IDs, names, copy counts, selected deck and card backs. Existing owned assets transfer to their new card identity; historical purchases remain recorded. Deleted deck lists stay empty. The published catalog receives a one-time roster upgrade on server initialization, preserving economy settings and existing prices; new cards default to 100 coins each.

The brief all-backs giveaway is corrected once per account: accounts owning all 20 designs retain default plus designs supported by purchase receipts. A deck using a removed giveaway design returns to the default back; its identity, cards and selected-deck status remain unchanged. Wallet balances are never charged by this correction.

Deck card copies are capped at four. The catalog, Deck Builder and server deck
validation share this cap. Startup clamps older published catalog limits to four;
player bootstrap clamps existing saved deck quantities above four and persists the
repair without changing deck IDs, names, selected deck, or smaller quantities.
