# Menu layout

Menus use native custom styling from `MenuUi.cs` and `MenuButtonSkin.cs`. No texture UI pack is needed for panels, controls, tabs or menu buttons. Character portraits, card backs and arena artwork remain the existing game assets.

- Welcome: two-column introduction and sign-in card; the smaller-screen version keeps the sign-in card.
- Lobby: horizontal navigation, deck browser and a compact selected-deck/matchmaking panel.
- Deck builder: adaptive card grid and a right-hand workspace with Deck and Card details tabs. Search and cost filters stay above the grid. Small screens stack the search/filter row.
- Collection/shop: category submenu above the page heading, owned count beside Collection, and group filter on the same heading row. Clicking a character opens a side inspector with animated art, cost, group, tier selection, stats and abilities. The gallery reflows when the inspector opens or closes. Refresh and redundant explanatory text are removed. The first character is selected on entry, the inspector remains open, and the enlarged card art omits the extra emblem.
- Rankings/history: content-first rows and separate page actions.
- Combat Simulator: scrollable settings beside a larger preview stage.

Gameplay now shares the custom menu styling: flat panels, interactive button skins, profile/portrait frames and thin custom health bars. Battle positions, deployment slots and rules remain unchanged. Rankings load only the top 100 entries, plus a separate self row; refresh/pagination and replay-history summary copy are removed.

Verification: `tests/menu_layout.tscn`, `tests/mobile_layout.tscn`, `tests/combat_simulator.tscn` and the existing arena runner. Screenshots are written to `output/menu-*.png` by the menu runner.
