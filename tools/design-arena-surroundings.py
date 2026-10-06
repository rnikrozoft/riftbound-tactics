"""Rebuild editable surroundings; preserve the battlefield and use every atlas tile."""
import base64
import math
from pathlib import Path
import re
import struct

ROOT = Path(__file__).resolve().parents[1]
SCENE = ROOT / 'scenes/main.tscn'
ATLAS = sorted({tuple(map(int, m)) for m in re.findall(r'^(\d+):(\d+)/0 =', (ROOT / 'tilesets/arena_tileset.tres').read_text(), re.M)})
layers = {}

def layer(name, height, z):
    layers[name] = (height, z, {})
    return layers[name][2]

def safe(x, y):
    return not (33 <= x <= 69 and 9 <= y <= 25)

def put(grid, x, y, tile):
    if safe(x, y) and tile in ATLAS:
        grid[x, y] = tile

def noise(x, y):
    return ((x * 73856093) ^ (y * 19349663)) & 0xffff

soil = layer('GardenGround', 40, -50)
water = layer('GardenWater', 40, -49)
banks = layer('GardenBanks', 28, -48)
paths = layer('GardenTemplePaths', 28, -47)
# A winding sunken aqueduct, with islands rather than repeated parallel stripes.
for x in range(-6, 109):
    for y in range(-38, 73):
        if not safe(x, y):
            continue
        n = noise(x, y)
        grove = x < 49 and y < 16
        palette = [(2, 0), (3, 0), (1, 21)] if grove else [(3, 0), (0, 21), (4, 0)]
        put(soil, x, y, palette[(n // 31) % len(palette)] if n % 8 == 0 else palette[0])
        channel = 6 + round(2 * math.sin(x / 7))
        if abs(y - channel) <= 1 or (x < 33 and abs(x - (27 + round(2 * math.sin(y / 6)))) <= 1) or (y > 25 and abs(y - (30 + round(3 * math.sin(x / 9)))) <= 1):
            put(water, x, y, (8, 16) if n % 4 else (4, 16))
        elif abs(y - channel) == 2 and n % 3 == 0:
            put(banks, x, y, (3, 1))
        if (y in (6, 7, 8) and x in range(47, 51)) or (x in range(30, 33) and 15 <= y <= 18):
            water.pop((x, y), None)
            put(paths, x, y, (3, 21))

# Each terrace has actual stacked block sides, a distinct floor, and a broken outline.
terraces = [
    ('MossSanctuary', 34, 46, -4, 3, 3, [(7, 0), (6, 0), (3, 1)], (10, 0)),
    ('CrystalCloister', 53, 67, -6, 3, 5, [(8, 2), (4, 2), (6, 2)], (5, 2)),
    ('TimberOutpost', 22, 31, 8, 24, 2, [(3, 21), (4, 21), (8, 21)], (2, 5)),
    ('EmberRuins', 72, 81, 6, 24, 4, [(4, 0), (15, 5), (6, 6)], (14, 6)),
    ('LowerCourtyard', 36, 49, 31, 39, 2, [(7, 0), (6, 0), (7, 21)], (10, 0)),
    ('MarketSteps', 57, 72, 30, 39, 3, [(3, 21), (8, 21), (4, 21)], (2, 5)),
]
for name, x0, x1, y0, y1, height, floors, wall in terraces:
    grids = [layer(name + f'Level{i}', 28 - i * 12, -46 + i) for i in range(height + 1)]
    top = grids[-1]
    decor = layer(name + 'Details', 16 - height * 12, -35)
    for x in range(x0, x1 + 1):
        for y in range(y0, y1 + 1):
            if (x == x0 or x == x1) and (y == y0 or y == y1):
                continue
            put(top, x, y, floors[noise(x, y) % len(floors)] if noise(x, y) % 5 == 0 else floors[0])
            if x == x1 or y == y1:
                for i in range(height):
                    put(grids[i], x, y, wall)
            if y == y0 and (x - x0) % 3 == 0:
                put(decor, x, y, (3, 1) if name not in ('TimberOutpost', 'MarketSteps') else (4, 44))
    # Three step access aligned to the front edge of each terrace.
    for step in range(height):
        stairs = layer(name + f'Stairs{step}', 28 - step * 12, -36 + step)
        for dx in (0, 1):
            put(stairs, (x0 + x1) // 2 + dx, y1 + height - step, (14, 23) if 'Timber' not in name and 'Market' not in name else (6, 23))
    spots = [(x0 + 2, y0 + 2), (x1 - 2, y0 + 2), (x0 + 2, y1 - 2), (x1 - 2, y1 - 2)]
    choices = {
        'MossSanctuary': [(13, 4), (4, 17), (16, 4), (12, 4)],
        'CrystalCloister': [(10, 17), (1, 15), (11, 17), (10, 19)],
        'TimberOutpost': [(0, 21), (2, 19), (4, 19), (12, 19)],
        'EmberRuins': [(7, 18), (9, 18), (17, 8), (10, 19)],
        'LowerCourtyard': [(8, 17), (6, 17), (3, 18), (11, 19)],
        'MarketSteps': [(6, 19), (8, 19), (9, 19), (5, 19)],
    }[name]
    for (x, y), tile in zip(spots, choices):
        put(decor, x, y, tile)

foliage = layer('GardenFoliage', 16, -34)
for x in range(14, 90):
    for y in range(-18, 52):
        if safe(x, y) and (x, y) not in water and not any(x0 - 1 <= x <= x1 + 1 and y0 - 1 <= y <= y1 + 4 for _, x0, x1, y0, y1, *_ in terraces):
            n = noise(x, y)
            if n % 41 == 0:
                put(foliage, x, y, (12 + n % 6, 4) if x < 52 else [(3, 1), (14, 6), (4, 17)][n % 3])

# Remaining atlas pieces form distant excavations, stores, and broken settlements.
# Keep the atlas families together (stairs, walls, banners...) in irregular clusters.
used = {tile for _, _, grid in layers.values() for tile in grid.values()}
remaining = [tile for tile in ATLAS if tile not in used]
outer = layer('OuterDistrictRelics', 12, -33)
outerbase = layer('OuterDistrictFoundations', 28, -45)
positions = [(x, y) for y in range(-36, 71, 2) for x in range(-4, 107, 2)
             if safe(x, y) and (y <= -16 or y >= 48 or x <= 16 or x >= 88)
             and noise(x, y) % 5 != 0]
positions.sort(key=lambda p: (p[1] // 7, p[0] // 9, p[1], p[0]))
for tile, (x, y) in zip(sorted(remaining, key=lambda t: (t[1], t[0])), positions):
    put(outerbase, x, y, (6, 0) if tile[1] % 2 == 0 else (3, 21))
    put(outer, x, y, tile)

scene = SCENE.read_text()
# Replace only decorative Garden layers. All gameplay and battlefield cells are preserved.
scene = re.sub(r'\[node name="GardenGround".*?(?=\[node name="CliffDepth")', '', scene, flags=re.S)
blocks = []
for name, (height, z, grid) in layers.items():
    payload = b'\0\0' + b''.join(struct.pack('<hhhhhh', x, y, 0, ax, ay, 0) for (x, y), (ax, ay) in sorted(grid.items()))
    blocks.append(f'[node name="{name}" type="TileMapLayer" parent="."]\nz_index = {z}\ntexture_filter = 1\nposition = Vector2(0, {height})\ntile_map_data = PackedByteArray("{base64.b64encode(payload).decode()}")\ntile_set = ExtResource("1_o5qli")\n\n')
scene = scene.replace('[node name="CliffDepth"', ''.join(blocks) + '[node name="CliffDepth"', 1)
SCENE.write_text(scene)
coverage = {tile for _, _, grid in layers.values() for tile in grid.values()}
assert set(ATLAS) <= coverage, f'Missing tiles: {set(ATLAS) - coverage}'
print(f'Surroundings: {len(layers)} layers, {len(coverage)}/{len(ATLAS)} atlas tiles, six terraced districts. Battlefield preserved.')
