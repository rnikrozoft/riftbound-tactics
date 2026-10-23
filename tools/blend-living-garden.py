"""Compose the sanctuary from the owned stone, isometric and critters libraries."""
import base64
import math
import re
import struct
from pathlib import Path
from PIL import Image
import subprocess
subprocess.run(["python3",str(Path(__file__).with_name("build-bush-atlas.py"))],check=True)
ROOT=Path(__file__).resolve().parents[1]
# Only resample the supplied nature sprites to the existing 24 x 12 gameplay grid.
Image.open(ROOT/'assets/terrain/isometric/spritesheet.png').resize((264,264),Image.Resampling.NEAREST).save(ROOT/'assets/terrain/atlases/woodland_atlas.png')
stone_defs=(ROOT/'tilesets/arena_tileset.tres').read_text()
stone_tiles=[tuple(map(int,m)) for m in re.findall(r'^(\d+):(\d+)/0 =',stone_defs,re.M)]
lines=['[gd_resource type="TileSet" format=3]','','[ext_resource type="Texture2D" path="res://assets/terrain/atlases/woodland_atlas.png" id="1"]','[ext_resource type="Texture2D" path="res://assets/terrain/atlases/stone_tiles.png" id="2"]','','[sub_resource type="TileSetAtlasSource" id="Nature"]','texture = ExtResource("1")','texture_region_size = Vector2i(24, 24)']
for i in range(121):
 x,y=i%11,i//11;lines += [f'{x}:{y}/0 = 0',f'{x}:{y}/0/texture_origin = Vector2i(0, -3)']
lines+=['','[sub_resource type="TileSetAtlasSource" id="Stone"]','texture = ExtResource("2")','texture_region_size = Vector2i(32, 32)']
for x,y in stone_tiles:lines += [f'{x}:{y}/0 = 0',f'{x}:{y}/0/texture_origin = Vector2i(0, -6)']
lines+=['','[sub_resource type="TileSetAtlasSource" id="Floor"]','texture = ExtResource("2")','texture_region_size = Vector2i(32, 32)','17:0/0 = 0','17:0/0/texture_origin = Vector2i(0, -6)','1:22/0 = 0','1:22/0/texture_origin = Vector2i(0, -6)']
lines+=['','[resource]','tile_shape = 1','tile_layout = 5','tile_size = Vector2i(24, 12)','sources/0 = SubResource("Nature")','sources/1 = SubResource("Stone")','sources/2 = SubResource("Floor")']
(ROOT/'tilesets/woodland_tileset.tres').write_text('\n'.join(lines)+'\n')
p=ROOT/'scenes/main.tscn';scene=p.read_text()
if 'id="16_bush"' not in scene:
 scene=scene.replace('[sub_resource type="ShaderMaterial"','[ext_resource type="TileSet" path="res://tilesets/bush_tileset.tres" id="16_bush"]\n\n[sub_resource type="ShaderMaterial"',1)
if 'id="15_coverage"' not in scene:
 scene=scene.replace('[sub_resource type="ShaderMaterial"','[ext_resource type="Script" path="res://scripts/arena/TerrainCoverage.cs" id="15_coverage"]\n\n[sub_resource type="ShaderMaterial"',1)
battle=re.search(r'\[node name="TileMapLayer".*?(?=\[node)',scene,re.S).group()
raw=base64.b64decode(re.search(r'PackedByteArray\("([^"]+)"',battle)[1])
# Center the board on deployment rows 14, 18, 22: equal two-cell margins.
battle_cells={(x,y) for x in range(38,65) for y in range(12,25)}
def noise(x,y):return ((x*73856093)^(y*19349663))&65535
def safe(x,y):return not(32<=x<=70 and 8<=y<=26)
def nature(t):return (0,t%11,t//11)
def stone(t):
 assert t in stone_tiles,t
 return (1,*t)
layers={}
def layer(name,z=-50,h=0):
 layers[name]=(z,h,{})
 return layers[name][2]
def put(grid,x,y,t):grid[x,y]=t
soil=layer('GardenGround',-50,24);water=layer('GardenWater',-49,24);banks=layer('GardenBanks',-48,18)
flora=layer('GardenFoliage',-45,12);trees=layer('GardenTrees',-44,24);occupied=set()
# Keep open glades; broad irregular streams wrap around the original battlefield.
for x in range(-6,109):
 for y in range(-38,73):
  n=noise(x,y);patch=math.sin(x*.12+y*.06)+math.cos(y*.17-x*.03)
  soil[x,y]=nature((22 if patch>.2 else 27) if n%13 else (24 if patch>.2 else 28))
  if not safe(x,y):continue
  upper=3+round(2.5*math.sin(x/12))
  lower=32+round(2.5*math.sin(x/13))
  tributary=27+round(2*math.sin(y/9))
  dist=min(abs(y-upper),abs(y-lower) if y>26 else 999,abs(x-tributary) if x<32 else 999)
  if dist<=1:water[x,y]=nature([88,89,90,91,92,93][n%6])
  elif dist==2:
   soil[x,y]=nature([0,2,9,21][n%4])
   if n%8==0:banks[x,y]=nature([69,70,71,74,75,76][n%6])
# Low earthen islands: layered existing soil and grass diamonds, no invented trees.
heights={}
for cx,cy,rx,ry,peak in [(34,-2,9,7,4),(74,4,9,7,4),(26,34,8,7,3),(78,35,8,7,3)]:
 for x in range(cx-rx,cx+rx+1):
  for y in range(cy-ry,cy+ry+1):
   radius=((x-cx)/rx)**2+((y-cy)/ry)**2
   if radius>1 or (x,y) not in soil or not safe(x,y) or (x,y) in water:continue
   n=noise(x,y);height=max(1,round((1-radius)*peak));heights[x,y]=height;occupied.add((x,y))
   for level in range(height):
    name=f'EarthBank{level}'
    if name not in layers:layer(name,-42+level,24-level*6)
    layers[name][2][x,y]=nature([0,1,2,7][n%4])
   name=f'IslandGrass{height}'
   if name not in layers:layer(name,-36+height,24-height*6)
   layers[name][2][x,y]=nature([22,23,24,26][n%4])
   if n%4==0:
    name=f'IslandFoliage{height}'
    if name not in layers:layer(name,-28+height,12-height*6)
    layers[name][2][x,y]=nature([29,30,33,34,43,45][n%6])
# Ragged rocky outcrops in the background, rather than four uniform mountains.
rocks=layer('MountainRocks',-29,12)
for cx,cy,rx,ry in [(58,-8,7,4),(18,16,5,8),(84,34,5,6)]:
 for x in range(cx-rx,cx+rx+1):
  for y in range(cy-ry,cy+ry+1):
   radius=((x-cx)/rx)**2+((y-cy)/ry)**2
   if radius<1 and (x,y) in soil and safe(x,y) and (x,y) not in water and noise(x,y)%3==0:
    rocks[x,y]=nature([61,62,64,66,67][noise(x,y)%5]);occupied.add((x,y))
# Group dense original bush sprites at forest edges, leave continuous animal paths.
for x in range(15,90):
 for y in range(-16,52):
  if (x,y) not in soil or not safe(x,y) or (x,y) in water or (x,y) in occupied:continue
  n=noise(x,y);grove=math.sin(x*.23)+math.cos(y*.25)
  if grove>.25 and n%4==0:
   index=([6,10,14,24,25,32][n%6] if (x//9+y//7)%5 else [0,7,15,18,22,27][n%6]);trees[x,y]=(0,index%6,index//6)
  elif n%29==0:flora[x,y]=nature([41,42,44,46,47,48,50,51,52][n%9])
# The board mask, elevation and all deployment points are unchanged.
floor=layer('TileMapLayer',0,0)
for x,y in battle_cells:floor[x,y]=(2,17,0)
front={c for c in battle_cells if (c[0]+1,c[1]) not in battle_cells or (c[0],c[1]+1) not in battle_cells}
for name,h,z in [('CliffDepth',24,-2),('CliffFace',12,-1)]:
 grid=layer(name,z,h)
 for c in front:grid[c]=stone((11,0))
# Border towers, crystal stands and furniture keep the middle and six slots clear.
base=layer('SanctuaryStoneBases',1,0);pillars=layer('SanctuaryPillars',2,-12);caps=layer('SanctuaryPillarCaps',3,-24);torches=layer('SanctuaryBraziers',4,-36)
for x,y in [(38,12),(38,24),(64,12),(64,24)]:
 for grid in [base,pillars,caps]:grid[x,y]=stone((17,0) if noise(x,y)%2 else (11,0))
 torches[x,y]=stone((7,18))
# Small raised courts outside combat for coherent object groupings.
terraces=[('SupplyCourt',37,43,-2,2,(4,21),1),('CrystalCourt',73,78,15,22,(7,22),2),('CampCourt',39,46,32,36,(4,21),1),('RelicCourt',61,67,34,39,(13,21),1)]
prop_choices=[[(0,19),(2,19),(4,19),(12,19)],[(3,20),(11,17),(14,17),(10,19)],[(0,21),(6,19),(8,19),(9,19)],[(13,4),(4,17),(16,4),(12,4)]]
for idx,(name,x0,x1,y0,y1,material,height) in enumerate(terraces):
 for level in range(height+1):
  grid=layer(name+f'Floor{level}',-22+level,24-level*12)
  for x in range(x0,x1+1):
   for y in range(y0,y1+1):
    if (x,y) in soil and (x,y) not in water and safe(x,y):grid[x,y]=stone(material);occupied.add((x,y))
 stairs=layer(name+'Stairs',-15,12)
 for dx in range(2):stairs[(x0+x1)//2+dx,y1+1]=stone((14,23))
 details=layer(name+'Objects',-10,12-height*12)
 for (x,y),t in zip([(x0+1,y0+1),(x1-1,y0+1),(x0+1,y1-1),(x1-1,y1-1)],prop_choices[idx]):details[x,y]=stone(t)
bridges=layer('GardenTemplePaths',-19,12)
for x0,y0 in [(48,3),(28,18),(51,33)]:
 for x in range(x0,x0+3):
  for y in range(y0-3,y0+4):
   if (x,y) in soil and safe(x,y):bridges[x,y]=stone((3,21));water.pop((x,y),None)
# Distant modular relic districts retain every supplied stone tile variation.
# They are outside the main camera's scenic core, so colour variants do not clutter play.
used={t[1:] for _,_,grid in layers.values() for t in grid.values() if t[0]==1}
relics=layer('DistantRelics',-20,12)
positions=[(x,y) for y in range(-104,-59,2) for x in range(-40,141,2)]
for tile,c in zip([t for t in stone_tiles if t not in used],positions):relics[c]=stone(tile)
coverage={t[1:] for _,_,grid in layers.values() for t in grid.values() if t[0]==1}
assert set(stone_tiles)<=coverage

def block(name,z,h,cells):
 raw=bytes(2)+b''.join(struct.pack('<hhhhhh',x,y,*t,0) for (x,y),t in sorted(cells.items()))
 tileset='16_bush' if name=='GardenTrees' else '13_woodland'
 hidden='visible = false\n' if name=='GardenTrees' else ''
 script='script = ExtResource("15_coverage")\n' if name=='GardenGround' else ''
 return f'[node name="{name}" type="TileMapLayer" parent="."]\n{hidden}{script}z_index = {z}\ntexture_filter = 1\nposition = Vector2(0, {h})\ntile_map_data = PackedByteArray("{base64.b64encode(raw).decode()}")\ntile_set = ExtResource("{tileset}")\n\n'
scene=re.sub(r'\[node name="[^"]+" type="TileMapLayer"[^\n]*\n.*?(?=\[node|\Z)','',scene,flags=re.S)
scene=scene.replace('[node name="Ally_01"',''.join(block(name,*values) for name,values in layers.items())+'[node name="Ally_01"',1)
p.write_text(scene.rstrip()+'\n')
print(f'Sanctuary: {len(trees)} bush groups, {len(floor)} balanced board cells, {len(coverage)}/{len(stone_tiles)} stone variations, four scenic courts.')
