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
- BattleDemo.cs: alternating attacks, health/death, hitstop, shake and slow arena float.
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


