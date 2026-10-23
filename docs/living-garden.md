# Stone sanctuary and wildlife

The map combines only the owned `stone_tiles.png`, `isometric tileset/spritesheet.png` and critters sprites. No generated artwork is used. Nature tiles are resampled with nearest-neighbour sampling to the existing 24 × 12 diamond grid. Stone sprites retain their original 32 × 32 atlas regions; both sources are defined in `woodland_tileset.tres`.

The playable sanctuary has a pale stone clearing, stacked stone edges, four corner braziers, narrow dark streams, earthen islands with flowers and bushes, scattered rocky outcrops, straight timber bridges, and four small supply/crystal/camp/relic courts. Vegetation uses the actual bush silhouette directly, without invented tree art or tint. The board has 351 cells spanning columns 38-64 and rows 12-24, centered on the deployment formations at row 18 with equal upper and lower margins. Root transform, tile projection and six deployment coordinates per side are preserved. A dedicated floor atlas origin aligns the stone block tops to the deployment plane. TerrainCoverage extends real grass tiles to every viewport edge on resize or camera changes; expand stretching prevents aspect-ratio bars. Border props leave the deployments and central combat area open.

All 971 defined stone atlas variations are present in the scene. The scenic core uses coherent materials; remaining variants form distant relic districts beyond the main gameplay view rather than cluttering the field.

`ArenaWildlife` creates 48 stag, boar, badger and wolf critters with individual speed, animation phase, rest durations and home ranges. The cell graph excludes water, earthen slopes, rocks, bushes, structures, raised bridges and the combat clearing. Animals reserve current and next cells to avoid collisions. The menu reuses the same terrain and wildlife; scene cleanup frees them together.

Run `python3 tools/blend-living-garden.py` to regenerate the mixed atlas and scene terrain. It preserves characters, UI and deployment coordinates. The original `design-arena-surroundings.py` is an older alternative; do not run it over this map unless intentionally restoring that design.

Validation: build the C# project and run `res://tests/arena_wildlife.tscn` with the compatibility renderer. It checks the terrain source, all stone variants, original board framing and twelve deployment points, bushes, five simulated minutes of roaming, habitat boundaries, overlap, every critter frame, real in-game slot markers, menu population and cleanup. Outputs are `tests/living-garden.png` (scenic overview) and `tests/sanctuary-in-game.png` (actual gameplay scene).

Special-card markers use columns 41 and 61, leaving a three-cell gap to the nearest hero column on each side. Hero deployment points stay unchanged. Player profiles display the equipped deck back on the right; online state and replays update each player independently, unknown IDs fall back to the default, and compact mobile profiles reserve space for the back.

The compact board trims four columns from each side to follow the relocated special-card markers, reducing its area by 23 percent. Each support row retains a three-cell outer margin, while hero positions, upper/lower margins and framing remain unchanged.

Desktop battle framing shifts the world down by 48 viewport pixels while CanvasLayer UI remains fixed. Combat presentation offsets return to this new baseline. Bots without a rating display MMR 1000; an explicit rating is retained across profile refreshes.

Upper and lower borders now both have exactly two cells of margin around deployment rows 14 and 22. Corner pillars and cliff layers follow the trimmed border; all slot coordinates stay unchanged.

The floor cube uses texture_origin (0, -6), matching its original top-diamond center to the deployment plane. Godot subtracts this origin: the previous +6 value displaced the visible surface by 12 pixels despite symmetric cell bounds. ProfileDisplayRunner checks the rendered surface offset and saves board-margins.png with units and UI hidden for visual inspection.

Experimental asset mix: GardenTrees now uses bush_tileset.tres built from 34 original BUSH BUSH BUSH sprite regions. Runtime bushes use the derived 48px atlas with a bottom anchor; green variants dominate and seasonal colours form small groups. CardCatalog.Art uses the restored original DecoCards/DecoCard.png frame, retaining character portraits, idle animation, stats, drag behaviour and equipped card-back selection.

Card stat badges use the original grey, gold and copper circle sprites from assets/ui/cards/card_frames.png. The upper-right weapon badge is omitted. Stats and icons ignore mouse input and stay within card padding, including drag previews.

Stat badges now use the patterned circular discs from card_frames.png (orange price, blue armour, red HP). The discs are 28px on shop cards and 22px on hand cards, with unclipped text. The upper-right weapon badge has been removed.

Corner number badges sit 2px from the card control edges. Character portrait width is 92 percent of the drawn card width, enlarged from 64 percent, and moved upward to leave room for lower badges.

Card economy: identical equal-star cards merge in pairs, conserving 1/2/4/8 underlying copies for one/two/three/four stars. Three copies leave a two-star card and a one-star card. Merge planning checks final hand capacity before spending, preserves veteran flags, and can consume equal-star field units. All hand and deployed sales return exactly one coin. Catalog copy limits are eight on both client and backend.

Stat badges use the plain gold price, grey armour and copper attack discs. Hand cards hide price, show the supplied shield badge at the upper right with star sprites underneath, and retain stars in drag previews. The 92px hand cards have equal 11.5px upper/lower padding inside the hand surface.


Slate & Brass is applied through `SlateBrassUi`, `GameUi`, and the shared
`TravelBookUi.StyleButton` entry point. Gold primary actions, teal selected tabs,
slate panels, input focus, profiles and shop icons use the generated assets.
The generated texture resolution is normalized in memory for UI use and buttons
use nine-slice skins. Original PNG pixels and DecoCards remain unchanged.


The battle standings now use structured rows with a local-player teal highlight,
character portraits and heart icons. Available deployed character visuals are
used for portraits; players without visible units use Knight/Orc fallbacks. Names
retain their existing lookup and truncation rules; the panel sizes to its rows.
The hand tray shows six empty visual card slots with plus signs, replacing slots
with real cards as the hand grows. The gameplay hand limit stays ten. The tray
uses the ornate chamfered Slate & Brass panel with balanced vertical spacing.
Validation: `tests/arena_hud_reference.tscn`, `tests/card_backs.tscn`, and the
rendered mobile layout test.
