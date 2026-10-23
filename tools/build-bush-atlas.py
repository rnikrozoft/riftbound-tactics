"""Pack original BUSH BUSH BUSH sprite regions without drawing new artwork."""
from pathlib import Path
from PIL import Image
ROOT=Path(__file__).resolve().parents[1]
source=Image.open(ROOT/'assets/terrain/bushes/bushbushbush-Both.png').convert('RGBA')
xs=[0,144,288,432,576,704,800];ys=[0,112,208,304,400,464,528]
out=Image.new('RGBA',(288,288))
for row in range(6):
 for col in range(6):
  if row==5 and col>3:continue
  sprite=source.crop((xs[col],ys[row],xs[col+1],ys[row+1]));box=sprite.getbbox()
  if box:sprite=sprite.crop(box)
  sprite.thumbnail((46,44),Image.Resampling.NEAREST)
  out.alpha_composite(sprite,(col*48+(48-sprite.width)//2,row*48+46-sprite.height))
out.save(ROOT/'assets/terrain/bushes/bush-atlas.png')
lines=['[gd_resource type="TileSet" format=3]','[ext_resource type="Texture2D" path="res://assets/terrain/bushes/bush-atlas.png" id="1"]','[sub_resource type="TileSetAtlasSource" id="Bushes"]','texture = ExtResource("1")','texture_region_size = Vector2i(48, 48)']
for i in range(34):lines += [f'{i%6}:{i//6}/0 = 0',f'{i%6}:{i//6}/0/texture_origin = Vector2i(0, 22)']
lines+=['[resource]','tile_shape = 1','tile_layout = 5','tile_size = Vector2i(24, 12)','sources/0 = SubResource("Bushes")']
(ROOT/'tilesets/bush_tileset.tres').write_text('\n'.join(lines)+'\n')
