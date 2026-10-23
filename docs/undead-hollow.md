# Undead Hollow

Select Play → Battle area → Undead Hollow → Preview.

The source is the supplied `Tiled_files/Undead_land.tmx` in
`assets/Free-Undead-Tileset-Top-Down-Pixel-Art/`.

The lower plateau (red outline in the latest reference) retains the complete,
unmodified native elevated_space2 layer, including its irregular cliff contour.
The small upper mound (green outline) is removed from elevated_space using exact
per-row bounds, without erasing the eastern cliff. The removed mound's footprint
is replaced with the pack's original cobbled floor swatches and transparent gravel
transition tiles. A short native back lip completes the edge previously hidden by
the upper mound. Original floor patches around it remain. Objects and detail props
within the red plateau are cleared; the eastern skull ridge is shifted 48 native pixels down toward the arena.
Seventy original-pack ruins, trees, crystals, graves and bones fill the peripheral
spaces without covering deployment slots. The former ellipse cutout and diagonal shearing are removed.

No source textures are recoloured, painted or replaced. No generated art, screenshot
textures or other environment packs are used. Native animated tiles use every original Tiled frame and its original duration,
including water/shoreline and animated relic tiles. UndeadTileAnimation registers
only placed clips and updates cells only when their frame changes. `tools/build-undead-hollow.py` regenerates the native Godot tileset and scene;
it asserts that the complete original lower plateau layer is preserved and no upper
mound tiles remain within the removed footprint.

The battlefield uses the original map's root transform and camera behavior: 1.4x
on desktop with the deployment-grid midpoint horizontally centered in the viewport
and Y=-206.2 after the shared display offset, and the existing 1.5x
mobile layout. There is no Undead-specific zoom-to-fit or rightward camera offset.
All unit, support and slot coordinates therefore project to the same screen
positions as the original map. Markers are enabled independently from cloud art.

Only terrain presentation differs: peripheral scenery uses 2x native scale to fill
the screen; the central plateau uses (1.9, 1.35) at (-139.2, 151.2), giving a broad,
shallow battlefield under the same deployment grid. The original plateau tile
contour is retained, with no game-rule or formation changes. The generator samples
every pixel inside all 22 slot diamonds against the rendered native ground to
ensure complete walkable coverage, including support-row corners.

Validation: tests/undead_hollow.tscn checks source texture provenance, all existing
hero/support coordinates, advancing animation frames, ridge relocation and props, absence of map objects in deployment slots, original/new
area selection, preview, gameplay loading and viewport coverage at multiple sizes.
Screenshots are in output/undead-hollow-{scenery,gameplay,menu-preview}.png.

Additional dressing fills the strip behind the battlefield with ruins, graves,
dead trees, a skull pile, bones and crystals, plus rocks, graves and a fallen trunk
in the south-west pocket. All added objects remain outside the deployment rows.

Dense dressing now includes 70 additional instances across 58 original sprite
variants. Upper pockets and plateau margins include skeletal arms, ribs, fallen
trunks, several grave shapes, five plant types, rocks and small ruin fragments.
An opacity check against all deployment diamonds found no object pixels covering
a slot; no unit, camera, networking or combat rules changed.

Horizontal centering is shared by both maps and reacts to viewport resizing.
It keeps scale and vertical placement unchanged and does not move the HUD.
At 1280px the battlefield moves right by 22px (33px on a 1920px scaled view).

Desktop scenery and battlefield are raised by 32 logical pixels (48 pixels in a
1920-wide scaled view), shared by both maps. Horizontal centering, scale and HUD
placement remain unchanged.
