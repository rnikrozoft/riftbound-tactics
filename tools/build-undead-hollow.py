"""Import the supplied original Tiled map; edit only the battle clearing and inner ridge."""
from pathlib import Path
import base64, json, re, struct, xml.etree.ElementTree as ET
from PIL import Image
ROOT=Path(__file__).resolve().parents[1]
PACK="res://assets/Free-Undead-Tileset-Top-Down-Pixel-Art/Tiled_files/"
SOURCE=ROOT/PACK[6:]
root=ET.parse(SOURCE/"Undead_land.tmx").getroot()
sets=[]
animations=[]
for entry in root.findall("tileset"):
    ts=ET.parse(SOURCE/entry.get("source")).getroot() if entry.get("source") else entry
    sets.append((int(entry.get("firstgid")),int(ts.get("columns")),ts.find("image").get("source")))
    source_id=len(sets)-1;cols=int(ts.get("columns"))
    for tile in ts.findall("tile"):
        animation=tile.find("animation")
        if animation is None:continue
        n=int(tile.get("id"))
        animations.append({"source":source_id,"x":n%cols,"y":n//cols,"frames":[[int(f.get("tileid"))%cols,int(f.get("tileid"))//cols,int(f.get("duration"))] for f in animation]})
layers=[]
for layer in root.findall("layer"):
    cells={}
    for chunk in layer.find("data").findall("chunk"):
        x,y,w=int(chunk.get("x")),int(chunk.get("y")),int(chunk.get("width"))
        for i,gid in enumerate(map(int,chunk.text.replace("\n","").split(","))):
            if gid: cells[x+i%w,y+i//w]=gid
    layers.append((layer.get("name"),cells))
# Keep the original lower plateau and its native cliff silhouette exactly intact.
# Only the green-marked upper mound is flattened; the red-marked plateau is cleared.
RED_OUTLINE=[(14,6.5),(18,7),(20,6),(22,7),(23,9.5),(25,10.5),(25,13.5),(28,15),(27,17),(25,17.5),(25,19),(22,19),(20,21),(15,20.5),(14,21),(13,19),(12,18),(9,17),(9,13),(11,10),(11.5,8)]
def in_plateau(x,y):
    inside=False
    for (ax,ay),(bx,by) in zip(RED_OUTLINE,RED_OUTLINE[1:]+RED_OUTLINE[:1]):
        if (ay>y)!=(by>y) and x<(bx-ax)*(y-ay)/(by-ay)+ax:inside=not inside
    return inside
# These cells belong to the small mound in the original elevated_space layer.
# Per-row boundaries avoid cutting into the neighboring eastern cliff.
UPPER_ROWS={7:(16,21),8:(14,22),9:(14,23),**{y:(14,24) for y in range(10,16)},16:(18,22)}
upper={(x,y) for y,(left,right) in UPPER_ROWS.items() for x in range(left,right+1)}
slots=[(x,y) for x in (44,48,54,58) for y in (14,18,22)]+[(x,y) for x in (41,61) for y in (14,16,18,20,22)]
combat_cells={((12*(x-y)+12+168)//32,(6*(x+y)+6+18)//32) for x,y in slots}
for name,cells in layers:
    if "object" in name.lower():
        original=cells.copy();authored={};remaining=set(original)
        while remaining:
            seed=remaining.pop();component={seed};todo=[seed]
            while todo:
                x,y=todo.pop()
                for n in [(x-1,y),(x+1,y),(x,y-1),(x,y+1)]:
                    if n in remaining:
                        _,cols,_=next(v for v in reversed(sets) if v[0]<=(original[x,y]&0x0fffffff))
                        if original[n]-original[x,y]==(n[0]-x)+(n[1]-y)*cols:
                            remaining.remove(n);component.add(n);todo.append(n)
            if not any(in_plateau(x+.5,y+.5) or (x,y) in combat_cells for x,y in component):
                authored.update({pos:original[pos] for pos in component})
        cells.clear();cells.update(authored)
    elif name in ("elevated_space","stairs","darker_surface2"):
        for pos in upper:cells.pop(pos,None)
    elif name.startswith("detail"):
        for x,y in list(cells):
            if in_plateau(x+.5,y+.5):cells.pop((x,y))
# Extend the native cobbled floor into the former upper mound. The 3x3 native
# gravel patch supplies transparent transitions; interior swatches are source tiles.
lower=next(c for n,c in layers if n=="elevated_space2")
floor_mask={p for p in upper if lower.get(p,55)==55}
floor={}
for x,y in sorted(floor_mask):
    left=(x-1,y) not in floor_mask;right=(x+1,y) not in floor_mask
    top=(x,y-1) not in floor_mask;bottom=(x,y+1) not in floor_mask
    # Join the retained native cobbled lip instead of edging it with a grey seam.
    if top and lower.get((x,y-1),55)!=55:top=False
    if left or right or top or bottom:
        ax=16 if left else 18 if right else 17
        ay=35 if top else 37 if bottom else 36
    else:ax,ay=1+2*((x*7+y*11)%5),20
    floor[x,y]=1+ax+ay*26
index=next(i for i,(n,_) in enumerate(layers) if n=="elevated_space")
floor[23,13]=522 # Fill the eastern support-slot corner, formerly an inward notch.
layers.insert(index+1,("BattleFloor",floor))
# Complete the short back lip previously concealed by the upper mound, using
# the same two-row stone/rim module already present immediately to its left.
rim={(x,6):549 for x in range(18,21)}
rim.update({(21,6):550,**{(x,7):575 for x in range(18,21)},(21,7):576,(22,7):31,(22,8):57,**{(x,8):522 for x in range(18,21)}})
layers.insert(index+2,("RestoredBackLip",rim))
# Authoring checks: the red lower plateau and surrounding source terrain may not drift.
original_root=ET.parse(SOURCE/"Undead_land.tmx").getroot()
original_lower=next(l for l in original_root.findall("layer") if l.get("name")=="elevated_space2")
expected={}
for c in original_lower.find("data").findall("chunk"):
    for i,g in enumerate(map(int,c.text.replace("\n","").split(","))):
        if g:expected[int(c.get("x"))+i%int(c.get("width")),int(c.get("y"))+i//int(c.get("width"))]=g
assert lower==expected,"Lower reference plateau changed"
assert not upper.intersection(next(c for n,c in layers if n=="elevated_space")),"Upper mound remains"
ground={(x,y):55 for x in range(-32,65) for y in range(-24,57)}
layers.insert(0,("Ground",ground))
used={i:set() for i in range(len(sets))}
def unpack(gid):
    raw=gid&0x0fffffff
    i=next(i for i in range(len(sets)-1,-1,-1) if sets[i][0]<=raw)
    first,cols,_=sets[i];n=raw-first
    # Godot atlas alternative transform flags correspond to Tiled's H/V/D flags.
    flags=(4096 if gid&0x80000000 else 0)|(8192 if gid&0x40000000 else 0)|(16384 if gid&0x20000000 else 0)
    used[i].add((n%cols,n//cols));return i,n%cols,n//cols,flags
def data(cells):
    raw=b"\x00\x00"
    for (x,y),gid in sorted(cells.items()):raw+=struct.pack("<hhhhhh",x,y,*unpack(gid))
    return base64.b64encode(raw).decode()
encoded=[]
central_footprint={pos for pos in lower if 8<=pos[0]<=28 and 6<=pos[1]<=22}|upper|set(rim)
# Validate the complete marker diamonds against the actual native terrain pixels.
surface=Image.new("RGBA",(1024,768));atlas=Image.open(SOURCE/"Ground_rocks.png").convert("RGBA")
for name,cells in layers:
    if name not in ("elevated_space2","bricks","BattleFloor","RestoredBackLip","stairs"):continue
    for (x,y),gid in cells.items():
        if (x,y) not in central_footprint:continue
        n=gid-1;ax,ay=n%26*16,n//26*16
        surface.alpha_composite(atlas.crop((ax,ay,ax+16,ay+16)),((x+16)*16,(y+16)*16))
for x,y in slots:
    for dx in range(-19,20):
        for dy in range(-9,10):
            if abs(dx)/19+abs(dy)/9>1:continue
            px=int((12*(x-y)+12+dx+139.2)/1.9);py=int((6*(x+y)+6+dy-151.2)/1.35)
            pixel=surface.getpixel((px+256,py+256))
            assert pixel[3]>=250 and pixel[0]>=85,f"Slot diamond outside walkable plateau: {(x,y,dx,dy)}"

for name,cells in layers:
    if name.startswith("darker_surface"):
        cells={} # These baked shadows belong to the former large island footprint.
    central={pos:g for pos,g in cells.items() if pos in central_footprint} if name in ("elevated_space2","bricks","BattleFloor","RestoredBackLip","stairs") else {}
    background={pos:g for pos,g in cells.items() if pos not in central}
    east={pos:g for pos,g in background.items() if pos[0]>=23 and pos[1]<=12} if name!="Ground" else {}
    encoded.append((name,data({pos:g for pos,g in background.items() if pos not in east}),"ReferenceMap"))
    if east:encoded.append((name+"_East",data(east),"ReferenceMap/EasternRidge"))
    if central:encoded.append((name+"_Plateau",data(central),"BattlePlateau"))
lines=['[gd_resource type="TileSet" format=3]']
for i,(_,_,name) in enumerate(sets):lines.append(f'[ext_resource type="Texture2D" path="{PACK+name}" id="t{i}"]')
active_animations=[a for a in animations if (a["x"],a["y"]) in used[a["source"]]]
for a in active_animations:
    for x,y,_ in a["frames"]:used[a["source"]].add((x,y))
(ROOT/'tilesets/undead_animations.json').write_text(json.dumps(active_animations),encoding='utf-8')
for i,tiles in used.items():
    lines+=['',f'[sub_resource type="TileSetAtlasSource" id="Atlas{i}"]',f'texture = ExtResource("t{i}")','texture_region_size = Vector2i(16, 16)']
    for x,y in sorted(tiles):lines.append(f'{x}:{y}/0 = 0')
lines+=['','[resource]','tile_size = Vector2i(16, 16)']
for i in used:lines.append(f'sources/{i} = SubResource("Atlas{i}")')
(ROOT/'tilesets/undead_hollow.tres').write_text('\n'.join(lines)+'\n',encoding='utf-8')
lines=['[gd_scene format=3]','[ext_resource type="Script" path="res://scripts/arena/AreaGroundCoverage.cs" id="coverage"]','[ext_resource type="TileSet" path="res://tilesets/undead_hollow.tres" id="tiles"]','[ext_resource type="Script" path="res://scripts/arena/UndeadTileAnimation.cs" id="animation"]','[node name="UndeadHollow" type="Node2D"]','texture_filter = 1','script = ExtResource("animation")','[node name="ReferenceMap" type="Node2D" parent="."]','position = Vector2(-168, -18)','scale = Vector2(2, 2)','[node name="EasternRidge" type="Node2D" parent="ReferenceMap"]','position = Vector2(0, 48)','[node name="BattlePlateau" type="Node2D" parent="."]','position = Vector2(-139.2, 151.2)','scale = Vector2(1.9, 1.35)']
for index,(name,encoded_data,parent) in enumerate(encoded):
    # Expose the coverage layer at the established path for viewport checks.
    if index==0:parent='.'
    lines+=['',f'[node name="{name}_{index}" type="TileMapLayer" parent="{parent}"]',f'z_index = {-25 if parent=="BattlePlateau" else -80+index}',f'tile_map_data = PackedByteArray("{encoded_data}")','tile_set = ExtResource("tiles")']
    if index==0:
        lines[-4]='[node name="Ground" type="TileMapLayer" parent="."]'
        lines+=['position = Vector2(-24, 90)','scale = Vector2(1.5, 1.5)','script = ExtResource("coverage")']
props=[("Dead_tree_shadow1_1",142,360),("Ruin_shadow1_2",92,445),("Crystal_shadow1_2",180,466),("Bones_shadow1_4",174,530),("Grave_shadow1_2",110,526),("Dead_tree_shadow1_3",744,494),("Ruin_shadow1_3",727,586),("Crystal_shadow1_3",660,575),("Bones_shadow1_6",666,536),("Grave_shadow1_7",772,548),("Thorn_plant_shadow1_3",198,318),("Thorn_plant_shadow1_5",718,402)]
# Dress the open strip behind the arena and the small south-west pocket.
# All footprints stop before the deployment rows or stay west of the plateau.
props += [
    ("Ruin_shadow1_1",220,266),("Grave_shadow1_4",252,273),
    ("Bones_shadow1_3",194,272),("Thorn_plant_shadow1_3",274,260),
    ("Dead_tree_shadow1_2",323,258),("Grave_shadow1_10",352,271),
    ("Pile_sculls_shadow1",402,266),("Bones_shadow1_2",374,277),
    ("Crystal_shadow1_1",474,262),("Rock_shadow1_5",443,273),
    ("Broken_tree_shadow1_3",503,271),("Thorn_plant_shadow1_5",510,248),
    ("Rock_shadow1_2",66,486),("Grave_shadow1_6",99,501),
    ("Broken_tree_shadow1_1",49,507),("Thorn_plant_shadow1_3",91,474),
]
# Denser clusters in the remaining marked pockets; varied silhouettes, not a grid.
props += [
    ("Dead_arm_shadow1_1",281,219),("Grave_shadow1_8",303,183),
    ("Bones_shadow1_13",263,242),("Rock_shadow1_4",287,250),
    ("Plant_shadow1_4",316,213),("Bones_shadow1_18",311,240),
    ("Broken_tree_shadow1_5",334,205),("Grave_shadow1_11",343,232),
    ("Dead_arm_shadow1_3",399,200),("Plant_shadow1_1",447,224),
    ("Ruin_shadow1_5",478,182),("Grave_shadow1_12",425,171),
    ("Bones_shadow1_14",469,245),("Grave_shadow1_13",491,218),
    ("Plant_shadow1_5",417,241),("Rock_shadow1_3",461,276),
    ("Thorn_plant_shadow1_1",382,229),("Bones_shadow1_16",438,254),
    ("Ruin_shadow1_4",443,323),("Grave_shadow1_14",472,311),
    ("Bones_shadow1_17",408,322),("Plant_shadow1_2",492,329),
    ("Rock_shadow1_5",462,335),("Plant_shadow1_5",423,307),
    ("Dead_arm_shadow1_4",530,367),("Grave_shadow1_15",566,365),
    ("Bones_shadow1_10",495,362),("Broken_tree_shadow1_6",580,379),
    ("Plant_shadow1_3",545,382),("Bones_shadow1_18",512,381),
    ("Tree_shadow1_3",150,424),("Plant_shadow1_4",198,407),
    ("Bones_shadow1_14",183,439),("Grave_shadow1_16",142,463),
    ("Dead_arm_shadow1_2",212,471),("Plant_shadow1_5",166,474),
    ("Ruin_shadow1_5",274,503),("Grave_shadow1_17",306,500),
    ("Bones_shadow1_13",283,470),("Thorn_plant_shadow1_2",301,526),
    ("Plant_shadow1_5",262,476),("Broken_tree_shadow1_4",265,529),
]
resources=[];nodes=['[node name="SceneryProps" type="Node2D" parent="."]','z_index = -10','y_sort_enabled = true']
for i,(name,x,y) in enumerate(props):
    path="res://assets/Free-Undead-Tileset-Top-Down-Pixel-Art/PNG/Objects_separately/"+name+".png"
    image=Image.open(ROOT/path[6:]);a,b,r,d=image.getbbox()
    resources.append(f'[ext_resource type="Texture2D" path="{path}" id="prop{i}"]')
    nodes+=['',f'[node name="{name}_{i}" type="Sprite2D" parent="SceneryProps"]',f'position = Vector2({x}, {y})',f'texture = ExtResource("prop{i}")','centered = false',f'offset = Vector2({-(a+r)/2}, {-d})']
lines[1:1]=resources;lines+=nodes
(ROOT/'scenes/areas/undead_hollow.tscn').write_text('\n'.join(lines)+'\n',encoding='utf-8')
# Keep gameplay/UI nodes and their paths; replace all environment dependencies.
main=(ROOT/'scenes/main.tscn').read_text(encoding='utf-8')
resources=[]
for line in main.splitlines():
    if line.startswith('[ext_resource') and not any(key in line for key in ['TileSet','ArenaWildlife','TerrainCoverage']):resources.append(line)
resources.append('[ext_resource type="PackedScene" path="res://scenes/areas/undead_hollow.tscn" id="undead"]')
lines=['[gd_scene format=3]']+resources+['','[sub_resource type="TileSet" id="CoordinateGrid"]','tile_shape = 1','tile_layout = 5','tile_size = Vector2i(24, 12)']
subpart=main.split('[sub_resource type="ShaderMaterial"')[1:]
for part in subpart:
    block='[sub_resource type="ShaderMaterial"'+part.split('[node ')[0]
    lines.append(block.rstrip())
sections=re.split(r'(?=\[node )',main)
for section in sections:
    if not section.startswith('[node '):continue
    header=section.splitlines()[0]
    keep=('name="Main"' in header or 'name="Ally_' in header or 'parent="Ally_' in header or 'name="Enemy_' in header or 'parent="Enemy_' in header or 'name="UI"' in header or 'parent="UI' in header or 'name="BattleDemo"' in header or 'name="OnlineBattle"' in header)
    if not keep:continue
    if 'name="Main"' in header:
        section=section.replace('name="Main"','name="UndeadBattle"',1).rstrip()+'\nDecorateArena = false\nShowDeploymentSlots = true\n'
        lines+=['',section,'[node name="UndeadHollow" parent="." instance=ExtResource("undead")]','','[node name="TileMapLayer" type="TileMapLayer" parent="."]','visible = false','tile_set = SubResource("CoordinateGrid")']
    else:lines+=['',section.rstrip()]
(ROOT/'scenes/undead_battle.tscn').write_text('\n'.join(lines)+'\n',encoding='utf-8')
print('Undead Hollow: original Tiled composition imported; lower reference plateau retained, upper mound removed, native cobbled floor restored; combat coordinates retained.')

