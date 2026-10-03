# Deck builder and lobby

## Marvel Duel research

The original beginner deck-building guide (August 20, 2021, Chinese second-test version) states that the editable deck has 40 filled card slots: three hero subdecks and one neutral subdeck, ten slots each. Each subdeck selects two different card types at every cost from 2 through 6. Each slot contains one to four copies. Thus 40 is the number of selected card types, not the sum of purchased-copy availability: selected copies can total 40 to 160.

Source: https://www.iyingdi.com/tz/post/5105356
A second author describes the same three-hero plus neutral structure and reducing non-core cards to one copy to filter the shop pool: https://www.taptap.cn/moment/178083991134406546

The 2021 guide also describes 21 fixed locked card types, four copies each, not configurable by the player; most modes include them, with Arena as an exception. These locked cards are separate from the 40 editable slots. This is a historical baseline, not verification that the current locked pool is unchanged.

## Implemented Riftbound adaptation

The user chose the 40-distinct-slot model, with mock cards and categories added first. The catalog now has 60 mock card kinds: four hero groups (Knight, Ranger, Mage, Guardian), each with two kinds per cost from 2 to 6, plus Neutral with four kinds per cost. Players select three of the four heroes and two Neutral kinds at every cost. Each selected kind has 1-4 copies. Artwork reuses the six existing card textures; the groups and card names are mock data, not Marvel characters.

- `scenes/lobby.tscn` is the project startup scene. It provides a persistent main navigation sidebar (Play, Decks, Shop, Leaderboard, Achievements). Play includes room entry; Decks focuses on deck editing. Future menus show an explicit Coming soon page and preserve the selected deck when returning. The content area is separate from navigation, leaving room for future menu implementations. It shows saved decks as cover tiles with three hero placeholders, selected-deck preview, a prominent create-room button and a separate code/join row. It opens a new or existing deck in the builder and creates or joins a six-digit room using that deck.
- `scenes/deck_builder.tscn` supports naming, hero selection, card replacement and copy counts. Hero changes refill that group's ten required slots. The three-column editor shows an inspected card with 1-star stats on the left, a searchable cost-filtered catalog in the center, and persistent four-group completion counts plus the active group list on the right. Copies use bounded minus/plus buttons; cards can be removed and missing slots refilled. Unselected cards present explicit replacement choices when their cost already has two slots. Native drag/drop accepts only the same group and cost, both for replacing a selected card and filling an empty slot. Hero selection shows all four choices and disables already selected heroes. Hero groups currently have only the two mandatory choices per cost.
- Deck definitions are saved in `user://decks.json`; the selected ID is saved in `user://selected_deck.txt`. This initial implementation stores decks on the device, not the Nakama account. Lobby launches and the Leaderboard menu share a device account persisted in `user://account-device.txt`. Independent direct-main debug/test clients still receive new identities.
- `scenes/main.tscn` automatically connects and creates/joins the requested room. Its Lobby button returns to deck selection and closes the socket on scene exit. Room retry controls remain available when the server is unavailable.
- Room creation passes the deck to the server RPC; joining passes it as match metadata. The Go runtime validates the name, exactly three distinct hero groups, forty distinct selected kinds, two cards at every cost per group, and copies 1-4. Each player's private shop inventory starts with only their validated deck copies, independently of the opponent. Buying consumes a copy; exhausted kinds cannot reroll. Selection freezes before preparation.

All menu frames, input borders, buttons and backgrounds use existing `01_TravelBookLite` textures. Existing economy, four-star upgrades, six field slots, HP and preparation mechanics remain in use.

The historical Marvel Duel fixed locked-card pool is not included in this initial mock adaptation. Riftbound shops offer only the selected deck; adding locked cards would be a separate game rule decision. Hero abilities, card collection ownership, sharing codes and cloud deck storage are not part of this implementation.

## Verification

Go tests cover invalid decks, catalog grouping, private selected pools, copy exhaustion and freezing after preparation. The Godot deck integration runner checks deck validation, editor hero/card replacement, search and price filtering, copy boundaries, incomplete-deck validation, native drag/drop replacement and empty-slot filling, persistence, lobby rendering, automatic room creation and two real Nakama players with different decks. It checks both shop pools and excludes exhausted kinds after rerolls. UI captures are generated under `tests/deck-*.png` for visual inspection.
