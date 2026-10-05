# Riftbound Tactics

Godot 4.7.2 **.NET** project. Runtime scripts are C#; scenes, shaders, tiles and supplied art retain their existing formats.

## Build and run
- Open project.godot using the .NET edition of Godot.
- Build: dotnet build RiftboundTactics.csproj
- Run the main scene with F5.
- Release build: dotnet build RiftboundTactics.csproj -c ExportRelease
- GUI integration test: Godot_console.exe --path . res://tests/integration.tscn
  Build first. The test drives the mouse in its own game window and exits with code 1 on failure.
  Screenshot: tests/preparation-csharp.png.

## Architecture
- BattleDemo.cs: alternating attacks, health/death, hitstop and shake. Arena float is disabled by default.
- BattleUnit.cs: typed unit state, cached animation node and health-change signals.
- CardShop.cs: round/phase state, card pool, six-slot occupancy, swapping and selling.
- ShopCard.cs, FieldDrop.cs, HandZone.cs, ShopZone.cs: GUI clicks and native drag/drop.
- SafeArea.cs: display safe-area mapping and padding.
- DamageSimulator.cs: effect browser and lazy SpriteFrames cache.
- CardDrag.cs: typed GUI payload plus immutable card/deployment data.

UI card controls and atlas textures are reused. Details update on health signals, not each frame.
Grid drawing processes only during a relevant drag. Six fixed occupancy entries replace searches through unit metadata.
Battle caches node references and reuses its hitstop speed buffer. Desktop safe-area polling is disabled.
Animation names are interned once. JSON metadata is source-generated for typed parsing without runtime reflection.
Gameplay collections use C# lists/arrays/dictionaries; only the GUI payload crosses the Variant boundary.

## Validation
Integration tests cover native loose purchase/deployment, selling, six-slot swaps/capacity,
no hand placeholders, battle phase locks, live and dead inspection, round persistence,
veteran return restrictions, all 192 effect frame sets, cache reuse and teardown during combat.
Performance changes reduce known redundant work; no comparative FPS or mobile benchmark has been claimed.
Local Debug diagnostic: 10,000 grid validations took 1.27 ms and allocated 40 managed bytes (the Stopwatch instance).
This measures only grid validation on this Windows PC, not overall game FPS or mobile performance.

## Mobile
The project retains landscape, safe-area and fixed desktop-window behavior.
Desktop uses .NET 8. Android uses .NET 9 when GodotTargetPlatform=android.
Actual Android/iOS exports require their platform toolchains and have not been tested in this Windows session.
See https://docs.godotengine.org/en/4.7/tutorials/scripting/c_sharp/index.html



## Online multiplayer

Start the Go/Nakama/PostgreSQL backend in `C:\Users\zebas\go\src\github.com\rnikrozoft\riftbound-tactics-backend` with `docker compose up -d --build`, then build this client and run F5.

For two debug windows: `./tools/start-debug-clients.ps1`. Pass `-GodotPath` if the Godot executable moves. Each run creates a fresh Nakama user. Press CREATE in one window; enter its six-digit room code and JOIN in the other.

Both players prepare for 60 seconds. Both pressing BATTLE starts immediately; the timer starts battle automatically otherwise. Each client shows its own heroes on the left and opponents on the right. Opponent formations are withheld by the backend during preparation and revealed when battle starts. Each owner has a private shop and hand. Damage, targets, HP, deaths and winner come from the backend's complete battle plan; clients only replay it. Hand cards and unit details remain visible during combat. Both players press NEXT to prepare the next round.

Connection defaults to 127.0.0.1:7350. For another PC/phone, set OnlineBattle.Host in main.tscn or the RIFTBOUND_HOST environment variable to the backend PC's address. Server settings and protocol are documented in the backend README.

Online scripts: NakamaConnection handles fresh authentication, socket transport and clock synchronization; OnlineBattle handles room UI, snapshot acknowledgement and replay scheduling; OnlineProtocol defines source-generated JSON contracts. BattleDemo.NetworkEnabled selects authoritative online playback; offline integration and the effects laboratory retain local simulation.

The lobby's REPLAYS menu stores the ten newest matches on this device, grouped by game with every captured round. History includes local play date, outcome, placement, final player HP and server-reported MMR delta with before/after ratings. Pending MMR stays marked until a final server update arrives; leaving early is recorded as LEFT MATCH. New matches replace the oldest, and playback loads recorded combat plans without connecting to the server. Each persisted account instance has its own `user://replays-N.json` archive, written atomically. New recordings also save each acknowledged preparation change: shop, coins, hand, formation and ready state. The bottom timeline selects recorded preparation steps and battles directly. A single Play/Pause control resumes playback from that point through subsequent rounds; pausing freezes both presentation and replay time. The arrow handle hides or reveals the drawer, and Escape returns to replay history. Long preparation gaps are capped at five seconds during playback; step labels retain their recorded timestamps. Older battle-only recordings omit preparation points.

Online integration: `Godot_console.exe --headless --path . res://tests/online_integration.tscn` after building. Requires the running local backend and takes roughly 90 seconds because it verifies the real 60-second timeout. Go tests and the Linux race detector validate the backend separately.

During battle the hidden shop is replaced by a centered row of opponent card backs from assets/cards/cardBacks.png. Only the opponent's hand_count is public; card identities remain redacted. No placeholder backs appear for an empty hand. Opening details shifts the opponent row together with the player hand.

Opponent deployment slots rotate 180 degrees around the battlefield center: owner slots 0,1,2,3,4,5 render as enemy slots 5,4,3,2,1,0. This preserves front/back relationships from the other player's view. Server slot IDs and combat unit IDs remain unchanged.

## Character content

Character combat now uses HP as attack power, separate armor, multi-hit impacts, status effects and per-character abilities. Blue armor and red HP/attack numbers appear beside each unit, and inspection shows live state and skill conditions. See [combat rules and validation](docs/character-combat.md).

Guest login downloads character definitions from the backend's JSON catalog through `character_catalog`. `CharacterData` supplies group names, prices, enabled flags, copy limits, images, scene resources, descriptions and four star-stat rows. The packaged `data/characters.json` provides the offline baseline. Orc, Demon and Blood Monster are independently selectable character groups, with ten cards each. A deck selects three character groups and ten Neutral slots. New asset paths must be included in a client release before the server enables that character. Unknown ability mechanics require a combat implementation; editing descriptive text alone does not grant an effect.
