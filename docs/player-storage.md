# Player collection, decks, wallet and published game data

The lobby now has **Collection** and **Shop** pages. Deck edits and selected deck are saved to the authenticated Nakama account. The arena shop still uses temporary round coins; the lobby shop uses the persistent `coins` wallet.

## Stored data

| Collection / key | Owner | Read / write permissions | Contents |
|---|---|---|---|
| `riftbound_player / collection_decks` | Player user ID | 1 / 0 | Schema, owned character IDs, saved decks and selected deck ID |
| `riftbound_purchases / request_id` | Player user ID | 1 / 0 | Purchase receipt with character, server price and currency |
| `riftbound_grants / request_id` | Player user ID | 1 / 0 | Receipt for a trusted currency/unlock grant |
| `riftbound_config / published` | System | 0 / 0 | Character catalog and economy settings |

Nakama Wallet stores persistent `coins`. Deck cards refer to stable `character_id` values and copy counts; they do not duplicate character stats. Hero group identities and existing character IDs cannot be changed by the admin publication API. Local rendering assets stay in the client build.

New players permanently own the 40 character variants in the server starter deck and start with **0 wallet coins**. `economy.all_unlocked` defaults to **true**, allowing every enabled character during testing without permanently granting every character. Current price seeds are **100 coins per character**, editable through published backend settings. When all-access is disabled, the server checks ownership when saving decks, entering rooms and starting matchmaking. A character unlock permits the catalog's normal deck copy limit; it is not a consumable card copy.

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
