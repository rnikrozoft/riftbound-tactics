"""Index generated transparent atlases as Godot textures without modifying their pixels."""
import json
from pathlib import Path
from PIL import Image
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'assets/ui/slate_brass'
NAMES={
'controls': [f'{family}_{state}' for family in ['primary','secondary','tab','input'] for state in (['normal','focus','error','disabled'] if family=='input' else ['normal','hover','selected','disabled'] if family=='tab' else ['normal','hover','pressed','disabled'])],
'surfaces':['panel_plain','panel_ornate','tooltip','modal','profile','shop','hand_tray','standings','card_slot','card_slot_selected','portrait_frame','portrait_selected','title_banner','divider','badge','selection_row'],
'icons':['battle','coin','reroll','lock','unlock','upgrade','close','back','settings','heart','shield','star','check','pause','play','chest']}
manifest={'theme':'Slate & Brass','generator':'built-in image_gen','assets':[]}
for category,names in NAMES.items():
    path=OUT/(category+'.png')
    image=Image.open(path).convert('RGBA');w,h=image.size
    assert image.getextrema()[3][0]==0,category+' has no transparency'
    xs=[round(i*w/4) for i in range(5)];ys=[round(i*h/4) for i in range(5)]
    boxes=[]
    for i in range(16):
        x,y=xs[i%4],ys[i//4]
        box=image.getchannel('A').crop((x,y,xs[i%4+1],ys[i//4+1])).point(lambda a:255 if a>=32 else 0).getbbox()
        assert box is not None,(category,i,'empty cell')
        boxes.append(box)
    # Each control family shares a region size/origin across all interaction states.
    if category=='controls':
        for row in range(4):
            group=boxes[row*4:row*4+4]
            common=(min(b[0] for b in group),min(b[1] for b in group),max(b[2] for b in group),max(b[3] for b in group))
            boxes[row*4:row*4+4]=[common]*4
    dest=OUT/category;dest.mkdir(exist_ok=True)
    for i,(name,box) in enumerate(zip(names,boxes)):
        x,y=xs[i%4]+box[0],ys[i//4]+box[1]
        width,height=box[2]-box[0],box[3]-box[1]
        png='res://'+str(path.relative_to(ROOT));asset=dest/(name+'.tres')
        asset.write_text(f'[gd_resource type="AtlasTexture" load_steps=2 format=3]\n\n[ext_resource type="Texture2D" path="{png}" id="1"]\n\n[resource]\natlas = ExtResource("1")\nregion = Rect2({x}, {y}, {width}, {height})\nfilter_clip = true\n')
        manifest['assets'].append({'name':name,'category':category,'texture':'res://'+str(asset.relative_to(ROOT)),'atlas':png,'region':[x,y,width,height],'cell':[i%4,i//4]})
    print(category,image.size,'transparent; 16 indexed sprites')
(OUT/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
print('SLATE BRASS ASSET INDEX PASS: 48 textures; original generated pixels preserved')
# Optional reusable Theme, deliberately not installed as the project's global theme.
style_names=NAMES['controls']+['panel_plain','panel_ornate','tooltip','modal']
styles=OUT/'styles';styles.mkdir(exist_ok=True)
for name in style_names:
    entry=next(a for a in manifest['assets'] if a['name']==name)
    edge=max(6,min(18,round(min(entry['region'][2:])*.12)))
    texture=entry['texture']
    (styles/(name+'.tres')).write_text(f'''[gd_resource type="StyleBoxTexture" load_steps=2 format=3]

[ext_resource type="Texture2D" path="{texture}" id="1"]

[resource]
content_margin_left = 16.0
content_margin_top = 8.0
content_margin_right = 16.0
content_margin_bottom = 8.0
texture = ExtResource("1")
texture_margin_left = {edge}.0
texture_margin_top = {edge}.0
texture_margin_right = {edge}.0
texture_margin_bottom = {edge}.0
''')
lines=[f'[gd_resource type="Theme" load_steps={len(style_names)+1} format=3]','']
for name in style_names:
    lines.append(f'[ext_resource type="StyleBox" path="res://assets/ui/slate_brass/styles/{name}.tres" id="{name}"]')
lines+=['','[resource]','default_font_size = 16','Label/colors/font_color = Color(0.945, 0.914, 0.824, 1)','Button/colors/font_color = Color(0.945, 0.914, 0.824, 1)','Button/colors/font_disabled_color = Color(0.48, 0.53, 0.56, 1)','PrimaryButton/base_type = &"Button"','PrimaryButton/colors/font_color = Color(0.102, 0.149, 0.2, 1)','PrimaryButton/colors/font_hover_color = Color(0.102, 0.149, 0.2, 1)','PrimaryButton/colors/font_pressed_color = Color(0.102, 0.149, 0.2, 1)']
for state in ['normal','hover','pressed','disabled']:
    lines.append(f'Button/styles/{state} = ExtResource("secondary_{state}")')
    lines.append(f'PrimaryButton/styles/{state} = ExtResource("primary_{state}")')
lines+=['Button/styles/focus = ExtResource("tab_selected")','Panel/styles/panel = ExtResource("panel_ornate")','PanelContainer/styles/panel = ExtResource("panel_plain")','PopupPanel/styles/panel = ExtResource("tooltip")','LineEdit/styles/normal = ExtResource("input_normal")','LineEdit/styles/focus = ExtResource("input_focus")','LineEdit/styles/read_only = ExtResource("input_disabled")','TabContainer/styles/tab_unselected = ExtResource("tab_normal")','TabContainer/styles/tab_selected = ExtResource("tab_selected")','TabContainer/styles/tab_hovered = ExtResource("tab_hover")','TabContainer/styles/tab_disabled = ExtResource("tab_disabled")','TabContainer/styles/panel = ExtResource("panel_plain")']
lines+=['PopupMenu/styles/panel = ExtResource("panel_plain")','PopupMenu/styles/hover = ExtResource("tab_selected")','PopupMenu/colors/font_color = Color(0.945, 0.914, 0.824, 1)','PopupMenu/colors/font_hover_color = Color(0.945, 0.914, 0.824, 1)','PopupMenu/colors/font_disabled_color = Color(0.48, 0.53, 0.56, 1)','TooltipPanel/styles/panel = ExtResource("tooltip")','TooltipLabel/colors/font_color = Color(0.945, 0.914, 0.824, 1)']
(OUT/'slate_brass_theme.tres').write_text('\n'.join(lines)+'\n')
print('SLATE BRASS THEME: 20 styleboxes and one opt-in Godot Theme')
